# Bob Prime ⇄ FarmReel — Comparison & Merged Clone Blueprint

**Companion to:** `docs/deep-analysis-bob-prime.md` and `docs/deep-analysis-farmreel.md`
**Date:** 2026-08-16
**Goal:** Decide what the **bobplayer** clone (repo: FarmReel) must include, combining the best of both products.

---

## 1. Head-to-head

| Dimension | Bob Prime (bobdevteam.com) | FarmReel (farmreel.me) |
|---|---|---|
| Product type | Facebook account **management** automation | Facebook account **creation + management** automation |
| UI language | English (Khmer tutorials) | English UI, Khmer-first tutorials |
| Pricing | License (Pro tier; no public pricing on site) | $15.99/mo, $47.99/3mo, $179.99/yr + **metered quota** ("Time Change Key") |
| Emulators | LDPlayer **9 / 14** only | LDPlayer **Mod** (Official/Chinese), **MuMu**, **real phones** (Wi-Fi / 4G SIM) |
| Root | Not required | **Magisk/Root** used on LDPlayer (with preserve-root tutorial) |
| VPN | Proxy/VPN recommended, IP country allow/block lists | **OpenVPN profiles per account** (2 import methods, delay, OTG), proxy find |
| Device fingerprints | Local table, re-applied every run | Local `device_info` folder + **server-generated fingerprints** |
| Account registration | ❌ No | ✅ **Reg Full** (mail/phone OTP, USA numbers, Gmail/MS), novelty verify |
| Novelty/verification | 180-day appeal, captcha solve | **Verify Novery** (Gmail/Phone/VIP), unlock **error 282**, appeal status |
| OTP handling | Email Manager (Zoho/**Yandex** IMAP), TOTP, SMS-Activate | Mail import (**Zoho/Gmail/Microsoft**), trusted mail, 2FA suite, phone OTP (incl. Cambodia) |
| Captcha | 2Captcha API | (implied internal/gateway; captcha fixes in changelogs) |
| AI | Captions, status posts, **chat reply**, check-in AI | Captions (**AI caption per post type**), status AI, comment AI |
| Posting | Reel/Photo/Video/Status/Story, schedule (time/weekly), per-file captions/comments, multi-photo (9), audio/music add, AI label, collaborators, Amazon affiliate, share to groups, delete all posts | Reel/Image/Video/Status/Story+Link, random-folder reel pick, filename filters, reel-or-video choice, crosspost/share groups, pro-mode posting, 1 folder → many pages, AI label, collaborators, monetization checks |
| Interaction/Active | Newsfeed/videos/reels loops, random react/comment, add/confirm friends, check-in w/ photo, view posts/stories/videos/live | Active Acc **or Pages**, interact-post share/comment (delete-after-use), like & follow, watch live, view story by id, check-in auto-location |
| Groups | Join by ID/link + keyword, share to groups | Join by ID/**suggestion**, **leave**, **post into groups**, view lists, count, **backup** |
| Monetization tooling | Dashboard pull, monetization setup, page status (Not Recommended/Flagged), support inbox, delete copyright/flagged | **Monetization check**, **CM waitlist criteria**, reach + sorting, recommendations, LIVE/DIE labels |
| Account info | Email add, name, pic/cover, workplace/college/city | Name, profile/cover, **wallpaper**, **download profile pic**, **set location**, date-created, notes |
| Backup | LDPlayer instance backup/restore (`*.ldbk`) | **Daily DB backup**, profile/cookie backup (full), backup groups, wrong-profile guard |
| Scheduling | Post When Run, Schedule Post (time/weekly), auto-stop, shutdown PC | Function/Task Schedule with limits |
| Ops extras | Auto-arrange LD windows, clear cache/VMs, shutdown PC, LD groups, bulk edit post settings, logs viewer, RAR patch updates | **Setup/Check Environment wizard**, action templates, report log, console, search-on-lists, **realtime transfer alerts**, **account selling/export**, mail stock storefront (farmmails.me), Binance Pay top-ups |
| Distribution | Installer + RAR patches (password-protected) | Copy-folder + install over; **hard base version** (3.3.0.0) gating |

## 2. Feature parity matrix (what the merged clone must ship)

Legend: 🟢 both have it (must have) · 🔵 Bob Prime only · 🔴 FarmReel only · ⚪ neither explicitly — decide ourselves

### Device / environment
- 🟢 Emulator fleet control (open/close/launch with waits)
- 🔵 LDPlayer 9/14 console support
- 🔴 LDPlayer Mod (Official/Chinese)
- 🔴 MuMu support (installer/import, auto-connect)
- 🔴 Real-device support (Wi-Fi, 4G SIM, offline-skip)
- 🔴 Magisk/Root-preserving operations (optional)
- 🟢 Per-account device fingerprint (store + replay)
- 🔴 Server-generated device info (or local generator w/ seeding)
- 🔵 Auto GPS & timezone from IP
- 🔴 OpenVPN profile manager (import ×2, delay, OTG)
- 🟢 Proxy support per account
- 🟢 APK install/update (FB + clone apps; pin FB versions — "support FB 490")
- 🔵 Auto-arrange emulator windows
- 🔵 Backup/restore LDPlayer instances
- 🔴 Daily DB backup + profile/cookie backup + backup groups
- 🔴 Setup/Check Environment wizard
- 🟢 Batched execution with per-batch limits + waits

### Account lifecycle
- 🟢 Login (UID/email + password + 2FA) with auto re-login
- 🟢 Check Live/Die (LIVE/DIE labels)
- 🟢 Manual login + manual backup (session capture)
- 🔴 **Reg Full** — bulk account registration (mail: Gmail/MS; phone OTP incl. USA; Lite/VIP tiers)
- 🔴 **Verify Novery** — novelty verification (Gmail / phone OTP / VIP; incl. enable-2FA recipe)
- 🟢 2FA: copy code, enable/disable, TOTP store, mail-code approval with delay
- 🔴 Unlock FB error 282
- 🔵 Auto appeal 180-day suspension
- 🔴 Status Submit Appeal
- 🔴 Phone verification (incl. Cambodia numbers)
- 🟢 Captcha solving gateway
- 🟢 Create Pages (new-UI compatible)
- 🟢 Pull account/page names; find/search page on list; show page count
- 🟢 Set account info (name, pic, cover, email)
- 🔴 Wallpaper, download profile pic, auto profile image, set location, date-created, notes
- 🔵 Create Instagram account linked to FB
- 🔵 Add/confirm friends; add by UID/link list
- 🔵 Check notifications; chat inbox + AI reply
- 🔴 Copy/sell accounts + transfer alerts

### Posting
- 🟢 Post Reel (+ comment; random folder; filename filters; reel-or-video)
- 🟢 Post Photo/Image (+ comment; multi-photo; AI caption; set public; group cross-post)
- 🟢 Post Video (multi-path; AI label; location; codec validation)
- 🟢 Post Status (file + AI) (+ comment)
- 🟢 Story (+ link file; pro mode; privacy public; view story by id)
- 🟢 Captions/hashtags: file / random / AI; per-file caption & comment files
- 🟢 Auto-comment on each post (incl. delete-after-use)
- 🟢 AI label toggle
- 🟢 Invite collaborators
- 🟢 Share/crosspost to groups (reels + posts)
- 🔵 Amazon affiliate link injection
- 🔵 Add audio/music to post (mute original)
- 🔵 Multi-photo up to 9 (`_M1.._M9`)
- 🔵 Comment with attached photo (`.comment_photos`)
- 🔴 1 content folder → multiple pages
- 🟢 Post When Run + Schedule (time/weekly) with per-account limits
- 🟢 Delete posts (all / single) & delete page
- 🟢 File lifecycle: move/delete posted, .failed folder, per-username naming

### Monetization & analytics
- 🔵 Dashboard pull + Monetization Setup + page status (Not Recommended/Flagged/Support Inbox) + auto-delete copyright/flagged
- 🔴 Check Monetization + **CM waitlist criteria** + reach sorting + recommendations
- 🟢 Followers/reach capture (LIVE/DIE, counts)

### Groups
- 🟢 Join by ID/link + search keyword
- 🔴 Join suggestions, leave group, post into groups, view lists, count, backup groups

### Active / interaction
- 🟢 Feed/videos/reels interaction loops (accounts and pages)
- 🟢 Random react / random comment
- 🔴 Interact-post share + delete-after-used-comment + resume list
- 🔴 Like & Follow, Watch Live, view story by id
- 🔵 Check-in post with photo
- 🟢 Check-in with auto-location + audience control

### Data & ops
- 🟢 SQLite DB + encrypted credentials (DPAPI/AES)
- 🟢 Logs viewer / report log + command console
- 🟢 Action/job templates + bulk edit post settings
- 🟢 LD/device grouping + search-on-list
- 🟢 Auto-stop / shutdown PC
- 🔵 Clear cache/FB data/LD VMs; network bridging toggle
- 🟢 License system + version gating + update channel
- 🔴 Metered quotas (time-change keys), mail-stock storefront, Binance Pay top-up (business layer)

## 3. Merged architecture for the clone

```
bobplayer (FarmReel repo)
├── Core
│   ├── Orchestrator / Scheduler / JobQueue / FlowEngine (FlowScript JSON)
│   └── ActionTemplates + Limits (per-account caps, batch limits)
├── Devices                      ← KEY CHANGE: abstract backends
│   ├── IDeviceBackend
│   │   ├── LDPlayerBackend      (ldconsole, LD9/14 + Mod builds)
│   │   ├── MuMuBackend          (MuMuManager console + ADB)
│   │   └── RealDeviceBackend    (adb connect Wi-Fi/USB/4G, offline-skip)
│   ├── FingerprintStore         (+ optional server-generated profiles)
│   ├── GpsTzSync                (IP→geo→gps/timezone; time-change keys)
│   ├── VpnManager               (OpenVPN import ×2, delay, OTG, per-account)
│   ├── ProxyManager             (per-account proxy + find-through-proxy)
│   └── WindowArranger           (Win32; emulators only)
├── Automation
│   ├── ScreenService / ElementFinder / ActionPrimitives (as in Bob Prime plan)
│   ├── Flows/
│   │   ├── login · relogin · captcha · 2fa · manualLogin · cookieCapture
│   │   ├── regFull (Gmail/MS/phone-OTP) · verifyNovery · unlock282 · appeal
│   │   ├── postPhoto · postReel · postVideo · postStatus · postStory
│   │   ├── checkIn · share · comment · inviteCollab · aiLabel
│   │   ├── groups (join/link/id/suggest/leave/post/view/backup)
│   │   ├── active (feed/watch/live/story, react, comment, like-follow)
│   │   ├── dashboard (reach/monetization/waitlist/recommendations)
│   │   └── accountInfo (name/pic/cover/wallpaper/location/created-date)
│   └── ThreatScreenClassifier   (282, checkpoint, ban, temp-lock + remedies)
├── Domain
│   ├── AccountManager · PageManager · PostingEngine · InteractionEngine
│   ├── EmailManager  (Zoho / Gmail / Outlook / Yandex IMAP + trusted-mail scoring)
│   ├── SmsGateway    (generic; Cambodia/USA providers pluggable)
│   ├── AIGateway     (captions/status/comments, health check)
│   └── MailStock     (farmmails-style inventory API — optional business layer)
├── Data: SqliteStore · SecretVault · BackupService (daily DB, cookies, profiles)
├── UI (WPF): Home/Devices · Accounts · Pages · PostTable · Active · Groups ·
│             Emails · Logs · Settings · Templates · EnvironmentWizard
└── Platform: Licensing (server) · Quotas (metered keys) · UpdateChannel (version-gated)
```

**Design decisions carried over + new:**
1. FlowScript JSON flows with a Flow Recorder (both products fight FB UI churn monthly).
2. `IDeviceBackend` abstraction — LDPlayer/MuMu/real-device from day one (FarmReel's lesson).
3. Auth pipeline as a first-class subsystem: login, registration, verification, unlock, appeal share the OTP/captcha/cookie machinery.
4. Cookie/profile = first-class session assets (capture, backup, restore, verify-correct-profile).
5. VPN/proxy manager with IP-geo verification (FarmReel's OpenVPN approach) + GPS/timezone sync (Bob Prime's approach) both supported.
6. Threat-screen classifier that routes to remediation recipes (282 unlock, checkpoint confirm, appeal) instead of just stopping.
7. Quota + license server if commercializing; version-gated forced updates.

## 4. Updated roadmap (merged)

| Milestone | Scope (both products) | Effort |
|---|---|---|
| M0 | Drive harness: LDPlayer + MuMu + real phone over ADB; screencap/tap/type | 2 wks |
| M1 | Device layer: backends, fingerprint store/replay, GPS/TZ, APK install, batch launch, VPN/proxy managers, env wizard | 4–5 wks |
| M2 | Account manager: vault, login (+TOTP/email-OTP/captcha), live/die, pull names, manual login + cookie capture, backups | 4–5 wks |
| M3 | Posting v1: photo (+multi-photo, AI caption, public), captions/hashtags, per-file comments, lifecycle folders, Post When Run | 3–4 wks |
| M4 | Scheduler (time/weekly + limits), video post + codec check, status post + AI | 2–3 wks |
| M5 | Page manager: switch, create page (new UI), dashboard/monetization/waitlist/reach, delete posts/page, set page info | 4 wks |
| M6 | Reels/story/audio: reel+comment (random folder, filename filters), story+link/pro-mode, AI label, collaborators, groups crosspost | 3–4 wks |
| M7 | Active engine: feed/watch/live/story loops, react/comment (delete-after-use), like&follow, check-in auto-location, friends | 3–4 wks |
| M8 | Groups suite: join (id/link/keyword/suggestion), leave, post-into-group, view/backup | 1–2 wks |
| M9 | **Auth pipeline (FarmReel differentiator)**: Reg Full (Gmail/MS/phone OTP), Verify Novery, unlock 282, appeal, 2FA suite, phone verify | 5–7 wks |
| M10 | Hardening/business: templates, bulk edit, logs/report, LD groups, licensing+quotas+update channel, mail stock (optional) | 3–4 wks |

**Total: ~30–42 engineer-weeks** to full merged parity (vs ~24–33 for Bob Prime alone); M9 is the biggest single new block.

## 5. Risk register (merged additions)

| Risk | Severity | Mitigation |
|---|---|---|
| Account **creation/registration** is the most aggressive ToS violation (fake accounts) | Critical | Consider excluding Reg Full from an initial compliance-safe release; clearly document intended use; never market as "ban-proof" |
| Selling/transferring accounts & mails (farmmails model) | Critical legal | Not software risk but business risk; requires its own legal review |
| Root/Magisk + fingerprint spoofing increase platform-detection stakes | High | Optional root; keep spoofing modular and defaults conservative |
| SMS/OTP/novelty services add cost + third-party failure points | Medium | Pluggable gateways, budget caps, graceful skip |
| Multi-backend support (LD/MuMu/real) triples device-testing matrix | Medium | CI smoke matrix; backend abstraction; auto-detect |

---

## 6. Final tech stack (decision)

**Verdict: C# 12 / .NET 8 LTS + WPF, Windows-only, single-file distribution.**

Rationale: the product is inherently Windows-native (LDPlayer/MuMu consoles, Win32 window arrangement, DPAPI, ADB server, OpenVPN), data-grid-heavy, and must ship as a small installer that survives antivirus scrutiny. C#/.NET is the best fit for all of those; it is also almost certainly what Bob Prime and FarmReel are built with (their patch/distribution patterns match .NET apps).

### 6.1 Stack table

| Layer | Choice | Why / alternatives considered |
|---|---|---|
| Language/runtime | **C# 12, .NET 8 LTS** | 3-yr support; single-file publish; best Windows interop; mature ecosystem |
| Desktop UI | **WPF + CommunityToolkit.Mvvm** | DataGrid-heavy tables (accounts/pages/posts) are first-class; WinForms as fallback; Avalonia only if we want cross-platform dev |
| App hosting | `Microsoft.Extensions.Hosting` (BackgroundService workers) | Orchestrator/scheduler run headless even when UI is minimized; DI + config built in |
| Device control | `Process` → `ldconsole.exe`, `MuMuManager.exe`, `adb.exe`, `openvpn.exe`, `ffmpeg/ffprobe` | Zero SDK deps; we own the failure handling; matches how both products work |
| Screen & vision | **OpenCvSharp** (template matching) + **Sdcb.PaddleOCR** (ONNX, better than Tesseract on FB screenshots) + `uiautomator dump` XML | FB renders much of the UI in canvas/WebView → need template + OCR, not just hierarchy |
| Input | `adb shell input/am/pm`, clipboard broadcast for unicode | `input text` breaks on emoji/special chars |
| DB | **SQLite + Dapper** (migrations via FluentMigrator) | Zero-config local; matches "data stays on your PC" promise |
| Secrets | **DPAPI** (`ProtectedData`) + AES-256-GCM for portable export | Free machine-bound encryption; export passphrase for migrations |
| Scheduler | Custom cron engine (cron-parser lib) inside the host | Full control over batch limits, catch-up, auto-stop/shutdown |
| Logging | **Serilog** (file + in-app console sink) + screenshot-on-failure | Structured events per account/action |
| HTTP | `HttpClient` + Polly (retry/circuit-breaker) | Captcha/SMS/AI/license endpoints |
| IMAP (OTP) | **MailKit** | Zoho/Gmail/Outlook/Yandex app-password IMAP |
| Media check | ffprobe/ffmpeg (subprocess) | Codec validation + optional transcode to H.264/AAC |
| Packaging | `PublishSingleFile` self-contained + **Inno Setup**; auto-update via manifest (signed) | AV-friendly, small, update channel like their patch model but cleaner |
| Optional server (commercial) | ASP.NET Core minimal API (licenses, quotas, device-info, mail stock) | Only when we monetize; keep client self-sufficient until then |

### 6.2 Why not the alternatives

- **Python + PySide6** — fastest prototyping, great vision libs (PaddleOCR, OpenCV), built-in imaplib. *Rejected for production:* packaged exes get **AV false-positives constantly** (a killer for this gray-market category), PyInstaller/Nuitka distribution is brittle, GIL hurts per-instance parallelism (mitigable since we're subprocess-bound, but still), DPAPI is clunky, DataGrid UX is weaker.
- **Electron/Node** — beautiful UI, but weak at native window arrangement, DPAPI, process/console management; huge footprint; native OpenCV bindings are painful.
- **Go / Rust + GUI** — excellent CLIs but immature desktop grid/UI ecosystems; slower to build the table-heavy UI.
- **C++/Qt** — overkill build complexity for the schedule; same capability as C# with 2–3× the effort.

### 6.3 Process model & dev workflow

- One WPF UI process + in-process `BackgroundService` workers (one orchestrator, N batch workers). No separate service install needed for v1 (schedule can run with UI minimized).
- **Flow Recorder** (record manual session → FlowScript JSON) + **FlowScript versioned against FB app version**; flows are data, not code — FB UI churn = update a JSON file, not rebuild.
- CI: compile + unit tests for pure logic (file naming conventions, scheduler, flow parser, OCR text parsing). Emulator end-to-end tests run on a dedicated Windows box, not CI.

---

*Both deep analyses remain the source of truth per feature: `deep-analysis-bob-prime.md`, `deep-analysis-farmreel.md`.*
