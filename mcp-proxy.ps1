$ErrorActionPreference = 'Stop'
$baseUrl = "http://localhost:9700/mcp"

[Console]::InputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

function Send-McpRequest($json) {
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
