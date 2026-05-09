$ErrorActionPreference = 'Stop'
$baseUrl = "http://localhost:9700/mcp"
$gddExe = "c:\VS\BrowserXn\src\BrowserXn\bin\Debug\net8.0-windows\GDD.exe"
$gddDir = "c:\VS\BrowserXn\src\BrowserXn\bin\Debug\net8.0-windows"

[Console]::InputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

function Test-GddAlive {
    try {
        $req = [System.Net.HttpWebRequest]::Create($baseUrl)
        $req.Method = "POST"
        $req.ContentType = "application/json"
        $req.Timeout = 2000
        $probe = '{"jsonrpc":"2.0","id":0,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"probe","version":"1.0"}}}'
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($probe)
        $req.ContentLength = $bytes.Length
        $s = $req.GetRequestStream()
        $s.Write($bytes, 0, $bytes.Length)
        $s.Close()
        $resp = $req.GetResponse()
        $resp.Close()
        return $true
    }
    catch {
        return $false
    }
}

function Ensure-GddRunning {
    if (Test-GddAlive) { return }

    $proc = Get-Process -Name "GDD" -ErrorAction SilentlyContinue
    if (-not $proc) {
        Start-Process $gddExe -WorkingDirectory $gddDir
    }

    for ($i = 0; $i -lt 15; $i++) {
        Start-Sleep -Seconds 1
        if (Test-GddAlive) { return }
    }

    [Console]::Error.WriteLine("GDD MCP server did not respond after 15s")
}

Ensure-GddRunning

function Send-McpRequest($json) {
    for ($attempt = 0; $attempt -lt 2; $attempt++) {
        try {
            $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
            $req = [System.Net.HttpWebRequest]::Create($baseUrl)
            $req.Method = "POST"
            $req.ContentType = "application/json"
            $req.ContentLength = $bytes.Length
            $req.Timeout = 120000
            $reqStream = $req.GetRequestStream()
            $reqStream.Write($bytes, 0, $bytes.Length)
            $reqStream.Close()
            $resp = $req.GetResponse()
            $sr = New-Object System.IO.StreamReader($resp.GetResponseStream(), [System.Text.Encoding]::UTF8)
            $result = $sr.ReadToEnd()
            $sr.Close()
            $resp.Close()
            return $result
        }
        catch {
            if ($attempt -eq 0) {
                Ensure-GddRunning
                continue
            }
            $errResp = @{
                jsonrpc = "2.0"
                id = $null
                error = @{
                    code = -32000
                    message = $_.Exception.Message
                }
            } | ConvertTo-Json -Compress
            return $errResp
        }
    }
}

while ($true) {
    $line = [Console]::In.ReadLine()
    if ($null -eq $line) { break }
    $line = $line.Trim()
    if ($line -eq "") { continue }

    $parsed = $line | ConvertFrom-Json
    if ($parsed.method -and $parsed.method.StartsWith("notifications/")) {
        try { Send-McpRequest $line | Out-Null } catch {}
        continue
    }

    $response = Send-McpRequest $line
    [Console]::Out.WriteLine($response)
    [Console]::Out.Flush()
}
