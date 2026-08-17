# FarmReel

Facebook bulk-account & page automation suite — a functional clone of **Bob Prime** + **FarmReel** (codename *bobplayer*).

**English UI. Windows only (LDPlayer / MuMu / real Android devices).**

## Docs

- [Deep analysis: Bob Prime](docs/deep-analysis-bob-prime.md)
- [Deep analysis: FarmReel](docs/deep-analysis-farmreel.md)
- [Bob Prime ⇄ FarmReel comparison + merged blueprint](docs/bob-prime-vs-farmreel.md)

## Tech stack

C# 12 / **.NET 8 LTS**, **WPF** desktop app, `Microsoft.Data.Sqlite` (SQLite), MailKit (IMAP OTP), System.Drawing (template matching), DPAPI + AES-GCM (secrets). Five projects:

| Project | Purpose |
|---|---|
| `src/FarmReel.Core` | Domain models, SQLite data layer, services (scheduler, orchestrator, posting, interaction, email OTP, captcha, SMS, AI, backup, license+quotas, **updates**), FlowScript engine |
| `src/FarmReel.Automation` | Device backends (LDPlayer/MuMu/real phone via `ldconsole` + ADB), OpenVPN/proxy managers, vision (template match + pluggable OCR), FlowHost + FlowRunner + **50 built-in flows** |
| `src/FarmReel.App` | WPF application (English UI): Dashboard, Devices, Accounts, Pages, Post Table, Active, Groups, Emails (+**mail stock storefront client**), Templates, **Flows (editor)**, **Recorder**, Logs, Settings (+**update check**) |
| `src/FarmReel.Server` | ASP.NET Core server: license activation, **metered Time Change Key quota**, server-generated device fingerprints, **mail stock API**, **update channel** |
| `tests/FarmReel.Tests` | xUnit tests: file naming conventions, cron parser, TOTP (RFC 6238), threat classifier, credential vault, repositories, flow engine, template matching |

## Build & run (Windows)

1. Install [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and **Visual Studio 2022** (or `dotnet build` CLI).
2. `dotnet restore` (pulls MailKit, Microsoft.Data.Sqlite, System.Drawing.Common, ProtectedData).
3. `dotnet build FarmReel.sln -c Release`
4. Run `src\FarmReel.App\bin\Release\net8.0-windows\FarmReel.exe` **as Administrator** (needed for LDPlayer window arrangement, OpenVPN and DPAPI).

## Tests

```bash
dotnet test tests/FarmReel.Tests -c Release
```

## Server

```bash
dotnet run --project src/FarmReel.Server
```

License activation, Time Change Key quota enforcement, device-info generation, the
mail-stock storefront and the update channel. See `src/FarmReel.Server/README.md`.
Point the app's **Settings → License server** at it; without a server the app runs
fully offline with local quotas.

## Release build / installer

```powershell
powershell -ExecutionPolicy Bypass -File publish\publish.ps1
```

Produces a single-file self-contained `FarmReel.exe` in `publish\win-x64` and (with
Inno Setup 6 installed) `FarmReelSetup-1.0.0.exe`. Code signing is optional and
enabled via `$env:CERT_THUMBPRINT` + `-Sign`. To stage an update, drop the new
`FarmReel.exe` into the server's `update-files/` folder.

## Quick start

1. **Settings** tab → point `ldconsole.exe` / `MuMuManager.exe` / `adb.exe` paths → **Check Environment**.
2. **Devices** tab → add LDPlayer/MuMu instances (index 0-based) or a real phone (ip:port), **Generate Fingerprint**, assign a **VPN profile** and/or **proxy**.
3. **Accounts** tab → add/import accounts (`email|password|2fa|phone|dob`), assign devices, **Check Live**, **Login**.
4. **Pages** tab → add pages (or **Create Page** from an account).
5. **Post Table** → add post jobs (Photo/Video/Reel/Status/Story, content folder, captions/comments, schedule) → **Run** in the main toolbar.

## How automation flows work

Flows are **JSON, not code** (data-driven UI automation — the same architecture Bob Prime and FarmReel use to survive Facebook UI changes). Built-ins live in `FarmReel.Automation/FlowRunner.cs`; drop a `myflow.json` into `%LocalAppData%\FarmReel\flows\` to override, and record new ones with the Flow Recorder workflow described in `docs/`.

> ⚠️ **Compliance:** this software automates Facebook accounts at scale, which violates Facebook's Terms of Service and can lead to account bans. It is provided for engineering/educational use. Understand the legal and ToS risks before operating it at scale (see the risk registers in the docs).

## Status

Feature-complete codebase — **all modules of the merged Bob Prime ⇄ FarmReel matrix implemented and reviewed**, including:

- **50 built-in FlowScript flows** (login, reg-full, verify-novery, unlock-282, appeal, all post types, comments, groups, friends, live/story viewing, notifications, reviews, dashboard, backups…) — flows are JSON data, overridable per FB app version
- **Device plane**: LDPlayer / MuMu / real-phone backends, instance add/copy/remove/backup/restore, network bridge, fingerprint + GPS/timezone, OpenVPN + proxy, APK install, window arrange
- **Account lifecycle**: import, live/die, 2FA/TOTP, manual login + profile backup, create page, pro mode, check-in, story, share, friends, groups (join/leave/post/suggestions)
- **Posting**: photo (multi-photo), video, reel, status, story+link with per-file captions/comments, AI captions, AI label, audio/location toggles, comment-delete-after-use, schedules, daily limits, file lifecycle
- **Page tools**: dashboard/monetization/waitlist, delete posts/page, copyright cleanup, bulk edit
- **Ops**: scheduler (auto-stop, shutdown PC, cache cleanup), backup (daily DB/profiles/groups), templates, logs viewer, search/filter on all grids, license + time-change-key quotas

**What still needs live validation (by design):** flows that drive the Facebook app must be tuned to your FB APK version on a real device/emulator — the framework (Flow Recorder workflow, JSON overrides) exists for exactly that. Build with `dotnet build FarmReel.sln -c Release` and run as Administrator.
