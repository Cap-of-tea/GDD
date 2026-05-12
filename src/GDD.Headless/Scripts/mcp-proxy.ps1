$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$gddExe = Join-Path $scriptDir "GDD.Headless.exe"
$baseUrl = "http://localhost:9700/mcp"
$gddProcess = $null
$gddArgs = @()
if ($args -contains '--headed') { $gddArgs += '--headed' }

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

    if (-not (Test-Path $gddExe)) {
        [Console]::Error.WriteLine("GDD.Headless not found at $gddExe")
        exit 1
    }

    $script:gddProcess = Start-Process $gddExe -ArgumentList $gddArgs -WorkingDirectory $scriptDir -PassThru

    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Seconds 1
        if (Test-GddAlive) { return }
    }

    [Console]::Error.WriteLine("GDD MCP server did not respond after 20s")
    exit 1
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

try {
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
}
finally {
    if ($script:gddProcess -and -not $script:gddProcess.HasExited) {
        $script:gddProcess | Stop-Process -Force -ErrorAction SilentlyContinue
    }
}
