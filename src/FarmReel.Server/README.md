# FarmReel server

License activation, metered quotas (Time Change Keys), server-generated device
fingerprints, the mail-stock storefront, and the update channel.

## Run

```bash
dotnet run --project src/FarmReel.Server
# listens on http://localhost:5000 (see Properties/launchSettings.json or set ASPNETCORE_URLS)
```

## Endpoints

| Method | Path | Purpose |
|---|---|---|
| POST | `/api/license/activate` | `{key, machineId}` → plan, expiry, key quota, feature flags |
| GET | `/api/license/status?key=` | quota/plan status |
| POST | `/api/license/consume` | `{key, feature:"timechange"}` → decrements quota, returns remaining |
| POST | `/api/deviceinfo/generate` | `{key}` → server-generated device fingerprint (Mac/IMEI/AndroidID/model) |
| GET | `/api/stock?key=` | mail inventory by provider with prices |
| POST | `/api/stock/order` | `{key, count, provider}` → marks mails sold, returns credentials |
| GET | `/api/update/manifest` | `{version, url, sha256, notes}` for the staged FarmReel.exe |
| GET | `/api/update/download` | downloads the staged FarmReel.exe |

## Configuration (appsettings.json)

- `LicenseDbPath` — SQLite file (default `farmreel_server.db`).
- `StockCsvPath` — optional `email,password,provider` CSV to seed the mail stock
  (if empty, demo stock is generated so the storefront is testable).
- `UpdateFilesDir` — drop the published `FarmReel.exe` here to enable the update channel.
- `UpdateVersion` / `UpdateNotes` — what the client sees.

## Client wiring

In the FarmReel app Settings tab set **License server** to
`http://<host>:5000` (use HTTPS in production). The client then:
- activates licenses via the server,
- consumes Time Change Keys via the server (hard quota),
- fetches/orders mail stock from the Emails tab,
- checks for updates on startup / on demand.

If no server URL is configured, everything runs offline with local quotas.
