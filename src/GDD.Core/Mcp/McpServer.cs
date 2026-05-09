using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using GDD.Abstractions;
using Serilog;

namespace GDD.Mcp;

public sealed class McpServer : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<McpServer>();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly McpToolRegistry _registry;
    private readonly IMainThreadDispatcher _dispatcher;
    private readonly int _port;
    private readonly ConcurrentDictionary<string, SseSession> _sessions = new();
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private bool _disposed;

    public int ActualPort { get; private set; }

    public McpServer(McpToolRegistry registry, IMainThreadDispatcher dispatcher, int port)
    {
        _registry = registry;
        _dispatcher = dispatcher;
        _port = port;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();

        for (var port = _port; port < _port + 10; port++)
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{port}/");
                _listener.Start();
                ActualPort = port;
                Logger.Information("MCP server started on http://localhost:{Port}", port);
                break;
            }
            catch (HttpListenerException)
            {
                _listener?.Close();
                _listener = null;
                Logger.Warning("Port {Port} in use, trying next", port);
            }
        }

        if (_listener is null)
            throw new InvalidOperationException($"Could not bind MCP server to any port in range {_port}-{_port + 9}");

        _listenTask = Task.Run(() => ListenLoop(_cts.Token));
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener!.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(context), ct);
            }
            catch (ObjectDisposedException) { break; }
            catch (HttpListenerException) { break; }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in MCP listen loop");
            }
        }
    }

    private async Task HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, DELETE, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, Mcp-Session-Id");
        response.Headers.Add("Access-Control-Expose-Headers", "Mcp-Session-Id");

        if (request.HttpMethod == "OPTIONS")
        {
            response.StatusCode = 204;
            response.Close();
            return;
        }

        try
        {
            var path = request.Url?.AbsolutePath ?? "";

            if (request.HttpMethod == "POST" && path == "/mcp")
            {
                await HandleStreamableHttp(request, response);
                return;
            }

            if (request.HttpMethod == "GET" && path == "/sse")
            {
                await HandleSse(response);
                return;
            }

            if (request.HttpMethod == "POST" && path == "/message")
            {
                await HandleSseMessage(request, response);
                return;
            }

            response.StatusCode = 404;
            response.Close();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling MCP request");
            response.StatusCode = 500;
            response.Close();
        }
    }

    private async Task HandleStreamableHttp(HttpListenerRequest request, HttpListenerResponse response)
    {
        using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        Logger.Debug("MCP streamable-http request: {Body}", body);

        var rpcRequest = JsonSerializer.Deserialize<JsonRpcRequest>(body, JsonOptions);
        if (rpcRequest is null)
        {
            await WriteJsonResponse(response, new JsonRpcResponse
            {
                Error = new JsonRpcError { Code = -32700, Message = "Parse error" }
            });
            return;
        }

        if (rpcRequest.Method.StartsWith("notifications/"))
        {
            response.StatusCode = 204;
            response.Close();
            return;
        }

        var rpcResponse = await ProcessRequest(rpcRequest);

        var sessionId = request.Headers["Mcp-Session-Id"] ?? Guid.NewGuid().ToString("N");
        response.Headers.Add("Mcp-Session-Id", sessionId);

        await WriteJsonResponse(response, rpcResponse);
    }

    private async Task HandleSseMessage(HttpListenerRequest request, HttpListenerResponse response)
    {
        var sessionId = request.QueryString["sessionId"];
        if (sessionId is null || !_sessions.TryGetValue(sessionId, out var session))
        {
            response.StatusCode = 400;
            response.Close();
            return;
        }

        using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        Logger.Debug("MCP SSE request: {Body}", body);

        JsonRpcRequest? rpcRequest;
        try
        {
            rpcRequest = JsonSerializer.Deserialize<JsonRpcRequest>(body, JsonOptions);
        }
        catch (JsonException ex)
        {
            Logger.Warning("SSE session {SessionId} JSON parse error: {Error} | Body: {Body}", sessionId, ex.Message, body);
            var errorResp = new JsonRpcResponse
            {
                Error = new JsonRpcError { Code = -32700, Message = "Parse error" }
            };
            await session.SendEventAsync(errorResp);
            response.StatusCode = 202;
            response.Close();
            return;
        }

        if (rpcRequest is null)
        {
            response.StatusCode = 202;
            response.Close();
            return;
        }

        Logger.Information("SSE session {SessionId} <- {Method} (id={Id})", sessionId, rpcRequest.Method, rpcRequest.Id);

        if (rpcRequest.Method.StartsWith("notifications/"))
        {
            response.StatusCode = 202;
            response.Close();
            return;
        }

        response.StatusCode = 202;
        response.Close();

        var rpcResponse = await ProcessRequest(rpcRequest);
        try
        {
            await session.SendEventAsync(rpcResponse);
        }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or IOException)
        {
            Logger.Warning("SSE session {SessionId} write failed (client disconnected): {Message}", sessionId, ex.Message);
            _sessions.TryRemove(sessionId!, out _);
        }
    }

    private async Task HandleSse(HttpListenerResponse response)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var session = new SseSession(response, sessionId);
        _sessions[sessionId] = session;

        Logger.Information("SSE session {SessionId} connected", sessionId);

        response.SendChunked = true;
        response.ContentType = "text/event-stream";
        response.Headers.Add("Cache-Control", "no-cache, no-transform");
        response.Headers.Add("Connection", "keep-alive");
        response.Headers.Add("X-Accel-Buffering", "no");

        var endpointUrl = $"http://localhost:{ActualPort}/message?sessionId={sessionId}";
        await session.SendRawEventAsync("endpoint", endpointUrl);

        try
        {
            while (!_cts!.Token.IsCancellationRequested)
            {
                await Task.Delay(30000, _cts.Token);
                await session.SendCommentAsync("keepalive");
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception) { }
        finally
        {
            _sessions.TryRemove(sessionId, out _);
            Logger.Information("SSE session {SessionId} disconnected", sessionId);
        }
    }

    private async Task<JsonRpcResponse> ProcessRequest(JsonRpcRequest rpcRequest)
    {
        switch (rpcRequest.Method)
        {
            case "initialize":
                return new JsonRpcResponse
                {
                    Id = rpcRequest.Id,
                    Result = new
                    {
                        protocolVersion = "2024-11-05",
                        capabilities = new
                        {
                            tools = new { listChanged = false }
                        },
                        serverInfo = new
                        {
                            name = "gdd",
                            version = "1.0.0"
                        }
                    }
                };

            case "tools/list":
                return new JsonRpcResponse
                {
                    Id = rpcRequest.Id,
                    Result = new { tools = _registry.GetDefinitions() }
                };

            case "tools/call":
                var callParams = rpcRequest.Params.HasValue
                    ? JsonSerializer.Deserialize<McpToolCallParams>(
                        rpcRequest.Params.Value.GetRawText(), JsonOptions)
                    : null;

                if (callParams is null)
                {
                    return new JsonRpcResponse
                    {
                        Id = rpcRequest.Id,
                        Error = new JsonRpcError { Code = -32602, Message = "Invalid params" }
                    };
                }

                var tcs = new TaskCompletionSource<McpToolResult>();
                await _dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        var toolResult = await _registry.InvokeAsync(callParams.Name, callParams.Arguments);
                        tcs.SetResult(toolResult);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                });
                var result = await tcs.Task;

                return new JsonRpcResponse
                {
                    Id = rpcRequest.Id,
                    Result = result
                };

            case "ping":
                return new JsonRpcResponse
                {
                    Id = rpcRequest.Id,
                    Result = new { }
                };

            default:
                return new JsonRpcResponse
                {
                    Id = rpcRequest.Id,
                    Error = new JsonRpcError { Code = -32601, Message = $"Method not found: {rpcRequest.Method}" }
                };
        }
    }

    private static async Task WriteJsonResponse(HttpListenerResponse response, JsonRpcResponse rpcResponse)
    {
        response.ContentType = "application/json";
        var json = JsonSerializer.Serialize(rpcResponse, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();
        _listener?.Close();
        _listener = null;
        _cts?.Dispose();
        _cts = null;
    }

    private sealed class SseSession
    {
        private readonly StreamWriter _writer;
        private readonly SemaphoreSlim _lock = new(1, 1);
        public string SessionId { get; }

        public SseSession(HttpListenerResponse response, string sessionId)
        {
            SessionId = sessionId;
            _writer = new StreamWriter(response.OutputStream, new UTF8Encoding(false)) { AutoFlush = false };
        }

        public async Task SendRawEventAsync(string eventType, string data)
        {
            await _lock.WaitAsync();
            try
            {
                await _writer.WriteAsync($"event: {eventType}\ndata: {data}\n\n");
                await _writer.FlushAsync();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task SendEventAsync(JsonRpcResponse rpcResponse)
        {
            var json = JsonSerializer.Serialize(rpcResponse, JsonOptions);
            await _lock.WaitAsync();
            try
            {
                await _writer.WriteAsync($"event: message\ndata: {json}\n\n");
                await _writer.FlushAsync();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task SendCommentAsync(string comment)
        {
            await _lock.WaitAsync();
            try
            {
                await _writer.WriteAsync($": {comment}\n");
                await _writer.FlushAsync();
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
