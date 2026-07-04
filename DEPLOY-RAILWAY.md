# Deploying GDD as a remote MCP server on Railway

GDD.Headless already speaks MCP over HTTP (`POST /mcp`, `GET /sse`) on port `9700`,
so it can run as a **remote** MCP server that your AI client connects to over the
internet — no local install required.

> ⚠️ **GDD has no built-in authentication and sends `Access-Control-Allow-Origin: *`.**
> Anyone who can reach `/mcp` gets full control of the browser farm
> (`gdd_execute_js`, `gdd_navigate`, `gdd_cookies`, …). Never expose GDD directly on
> a public URL. This recipe puts a **Caddy reverse proxy with a Bearer-token gate** in
> front of it, so only requests carrying your token reach GDD.

```
Internet ──HTTPS──> Railway edge ──> Caddy (:$PORT, public)
                                       │  checks Authorization: Bearer $GDD_TOKEN
                                       │  missing/wrong → 401
                                       ▼
                                    GDD.Headless (127.0.0.1:9700, /mcp)
```

## Files

The deploy is driven by three files at the repo root:

| File | Purpose |
|------|---------|
| [`Dockerfile.railway`](Dockerfile.railway) | Builds GDD.Headless, adds the Caddy static binary, runs both via the entrypoint. |
| [`Caddyfile`](Caddyfile) | Public `:$PORT`; Bearer-token gate → `127.0.0.1:9700`; open `/healthz`; `flush_interval -1` for SSE/streaming. |
| [`docker-entrypoint.railway.sh`](docker-entrypoint.railway.sh) | Launches GDD (`:9700`) + Caddy (`:$PORT`); exits if either dies so Railway restarts cleanly. |

## One-time setup

1. **Create a Railway project** (or use an existing one). You need the **Admin** or
   **Member** role in its workspace — a **Viewer** cannot create services
   (`serviceCreate` → `Not Authorized`).

2. **Add a service from this GitHub repo**, tracking the branch that contains the
   deploy files. In the service settings:
   - **Source:** the GitHub repo, branch of your choice.
   - **Variables:**

     | Variable | Value | Why |
     |----------|-------|-----|
     | `GDD_TOKEN` | a long random secret (`openssl rand -hex 32`) | The Bearer token clients must send. |
     | `RAILWAY_DOCKERFILE_PATH` | `Dockerfile.railway` | Tells Railway to build this Dockerfile instead of the default one. |
     | `GDD__BindAddress` | `0.0.0.0` | GDD must listen on all interfaces inside the container so Caddy can reach it (also baked into `Dockerfile.railway`). |
     | `PORT` | `8080` | Port Caddy binds; must match the service's public **target port**. |

3. **Generate a public domain** for the service with **target port `8080`**
   (Networking → Generate Domain).

Every push to the tracked branch now triggers an automatic rebuild + redeploy.

## Verify

```bash
BASE="https://<your-service>.up.railway.app"
TOKEN="<your GDD_TOKEN>"
INIT='{"jsonrpc":"2.0","id":0,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"probe","version":"1.0"}}}'

curl -s -o /dev/null -w "%{http_code}\n" "$BASE/healthz"                                   # 200
curl -s -o /dev/null -w "%{http_code}\n" -X POST "$BASE/mcp" -d "$INIT"                    # 401 (no token)
curl -s -X POST "$BASE/mcp" -H "Authorization: Bearer $TOKEN" -d "$INIT"                   # MCP result
```

## Connect your AI client

```json
{
  "mcpServers": {
    "gdd_railway": {
      "type": "http",
      "url": "https://<your-service>.up.railway.app/mcp",
      "headers": { "Authorization": "Bearer <your GDD_TOKEN>" }
    }
  }
}
```

## Notes

- **Memory:** each `gdd_add_players` spawns a Chromium instance. At idle (0 players) the
  container is light; size the service for the number of concurrent players you expect.
- **Rotating the token:** update the `GDD_TOKEN` variable in Railway (triggers a redeploy)
  and update your client config.
- **Private-only alternative:** skip the public domain and reach the service over Railway
  private networking, or keep GDD on a private network (e.g. Tailscale) as the local/VAIO
  setups do.
