# FarmReel — Deep Feature Analysis (bobplayer clone blueprint, part 2)

**Source analyzed:** https://www.farmreel.me/ (homepage + changelog) + official Telegram channels (@farmreel, @FARMREELDEMO) + YouTube (@FARMREEL_Official) + Wayback CDX
**Analysis date:** 2026-08-16
**Product version at analysis time:** 3.3.2.1 (changelog last updated Aug 9, 2026)

> This is the second product analyzed for the **bobplayer / FarmReel** clone project. The first (Bob Prime) is in `docs/deep-analysis-bob-prime.md`. This document is a feature-by-feature deep analysis of **FarmReel** — the product this repo is named after — followed by a Bob Prime ⇄ FarmReel comparison and the merged clone blueprint in `docs/bob-prime-vs-farmreel.md`.

> **Compliance disclaimer:** FarmReel automates Facebook accounts at scale, including *creating* accounts, and sells email accounts (farmmails.me). This violates Facebook's ToS and raises serious legal/ethical risks (platform abuse, anti-bot statutes, account selling). This document is a technical feature analysis for engineering purposes only. See §7.

---

## Table of contents

1. [Product snapshot & business model](#1-product-snapshot--business-model)
2. [How FarmReel works — architecture (deduced)](#2-how-farmreel-works--architecture-deduced)
3. [Feature-by-feature deep analysis](#3-feature-by-feature-deep-analysis)
4. [Evidence base & sources](#4-evidence-base--sources)
5. [What FarmReel teaches us that Bob Prime doesn't](#5-what-farmreel-teaches-us-that-bob-prime-doesnt)

---

## 1. Product snapshot & business model

**FarmReel** is a Windows desktop Facebook-automation suite built and sold by a Cambodian team (Khmer-language tutorials, supporter handle @farmreel_supporter, admin @hearhour, ~542 YouTube subscribers). Unlike Bob Prime (emulator-only, LDPlayer), FarmReel's signature is **breadth of device backends** — LDPlayer **mods**, **MuMu**, and **real Android phones** (Wi-Fi / 4G SIM) — plus an **account creation & verification pipeline** ("Reg Full", "Verify Novery") that Bob Prime does not have.

### 1.1 Pricing & plans (from homepage)

| Plan | Price | Key entitlements |
|---|---|---|
| 1 Month | $15.99/mo | 2 Time Change Keys, Bulk Upload, Function Schedule, Unlimited real devices / LDPlayer / FB accounts |
| 3 Months | $47.99 (+15 days free) | 10 Time Change Keys + everything above |
| 12 Months | $179.99/yr (+30 days free) | 100 Time Change Keys, **Register Accounts**, **Verify Account Novery**, Custom Features (paid) |

- **"Time Change Key"** = a metered quota for changing the device time/fingerprint context (anti-detection). Sold as a consumption unit — a novel monetization: the *feature* is quota-gated, not just license-gated.
- 12-month plan gates the two most powerful/risky features: **account registration** and **novelty verification**.
- The site also runs an **"Available Stock"** section: a mail inventory (redirects to **farmmails.me** — "Buy Fresh Email Accounts. Hotmail, Outlook, and more"), with **API-key top-ups payable via Binance Pay**.

### 1.2 Version timeline (from Telegram changelogs)

| Version | Date | Milestones |
|---|---|---|
| 2.7.x | Apr–May 2024 | Folder-based data layout (DB, device_info, profiles); daily DB backup; display configs; find page on list |
| 2.8.x | Jun–Sep 2024 | Create Page new UI; proxy find; manual login/backup; delete page; copy 2FA code; delay mail-code approval 30s; watch live; post+comment |
| 2.8.8.8 | Jan 2025 | Zoho mail add; check reach; create page; **MuMu global change**; **FB 490 support**; 1 path folder for multiple pages (beta) |
| 3.0.x | Feb–Apr 2025 | Major v3 rework; post video (multi-path, AI label, location); reel columns (email, personal, 2FA); groups suite (join by ID/suggestion, leave, post, view, backup); 4G SIM; invite collaborator; set location; crosspost/share groups (reel) |
| 3.1.x | Aug–Sep 2025 | Reg Full verify; Verify Novery Gmail; launch MuMu auto-connect; change password; delete post; skip device offline; view story by id |
| 3.2.x | Feb–May 2026 | Novery VIP (register/verify); Microsoft mail import; save action template; reach sorting; check-in auto location; reel/video+comment choice; reel filename filter; OpenVPN OTG; unlock 282; enable 2FA (Novery); auto backup cookie; trusted-mail new UI; CM waitlist check; pro-mode story/reel |
| 3.3.x | Jun–Aug 2026 | **3.3.0.0 is a hard base version** (older installs can't update past it); identity-OTP auto-confirm; USA phone-OTP reg; copy/sell accounts; report log; pro-mode deep link; verify number Cambodia; appeal status submit; change profile auto image; new server Gmail; post reel AI label; unlock 282 improvements |

**Takeaway:** ~2 major releases per year, near-monthly patches. Same FB-UI-churn reality as Bob Prime, plus *new* churn surfaces: Facebook **registration** flows, **verification** flows, and **checkpoint/unlock** flows.

---

## 2. How FarmReel works — architecture (deduced)

### 2.1 Runtime model

```
┌────────────────────────────────────────────────────────────────┐
│  Windows PC                                                        │
│                                                                     │
│  ┌─────────────────────────────┐     ┌──────────────────────────┐ │
│  │  FarmReel (desktop app)     │     │  Emulator fleet          │ │
│  │  - Home / Account / Post /  │────▶│  - LDPlayer Mod (Official│ │
│  │    Active / Groups tabs     │ADB  │    / Chinese)            │ │
│  │  - Task scheduler           │     │  - MuMu (Installer/Import│ │
│  │  - UI automation driver     │     │  - Real phones (WiFi/4G) │ │
│  │  - Device & proxy manager   ├────▶│  Magisk/Root on LDPlayer │ │
│  │  - DB + profiles/cookies    │     │  OpenVPN per instance    │ │
│  └─────────────────────────────┘     └──────────────────────────┘ │
│        │                                                           │
│        ▼                                                           │
│  FarmReel servers: license keys · device-info server · Gmail/     │
│  mail servers (farmmails.me) · "New Server Gmail" · stock API     │
│  Third parties: Zoho/Gmail/Outlook IMAP · phone-OTP services      │
│  (incl. "Verify Number Cambodia") · Binance Pay top-up            │
└────────────────────────────────────────────────────────────────┘
```

### 2.2 Data layout (from the v2.7.3 install tutorial)

Explicit folder structure revealed in Telegram:
- **`DB`** — the app database (SQLite; backed up **every day** automatically since 2.7.5).
- **`device_info`** — per-account device fingerprint records ("Fixed server new device info" ⇒ some device info is **generated server-side** and delivered to the client — a fingerprint-service architecture, not purely local tables).
- **`profiles`** — Facebook app-data/profile backups per account (manual backup added 2.8.6.1; "check backup wrong profile" guard; "backup full cookie" / "auto backup cookie" added later).
- Updates = copy the FarmReel folder to another drive, install new version over it (3.3.0.0 introduced a base-version requirement).

### 2.3 Device plane — the differentiator

| Backend | Control mechanism (deduced) |
|---|---|
| LDPlayer Mod (Official/Chinese) | `ldconsole` + ADB; **Magisk/Root preserved** (they ship a "don't break Magisk/Root" tutorial) — root is used for fingerprint spoofing, app-data manipulation, and cookie extraction |
| MuMu (global / "MuMu Global" change) | MuMu's own console (`MuMuManager`) + ADB on its ports; "Launch Mumu auto connect" = boot+connect automation |
| Real Android phones | ADB over **Wi-Fi** (`adb connect <ip>`) and **4G SIM** devices (USB/wifi tethering); "Skip device offline" = offline-device resilience; "Scale size window" = per-device screen handling |

**VPN/proxy plane:**
- **OpenVPN profiles per account**: "Import Profile openvpn 2 options" (two import methods), "Delay OPENVPN (HOME)", "Improve OpenVPN Support OTG" (USB-OTG network sharing), "Display IP Location".
- **Proxy support**: "Add Find with PROXY" (search/lookup through proxies), "Improve Proxy".
- Combined with per-account device_info ⇒ same "stable fingerprint + consistent IP geo" strategy as Bob Prime, but with *user-supplied VPN profiles* instead of a built-in GPS/timezone sync (they still have device time changes — the "Time Change Key").

### 2.4 Account creation & verification pipeline (FarmReel's unique core)

Bob Prime *manages* existing accounts; **FarmReel creates them**:

1. **Reg Full** — bulk account registration on the emulator/phone:
   - mail sources: **Microsoft (Hotmail/Outlook)**, **Gmail** ("Improve Reg full gmail", "New Server Gmail"), Phone OTP (incl. **USA** numbers), "Lite (Beta)" tier, "Zin ⇒ Lite" migration (likely a mail/OTP vendor codename).
   - "Reg Full verify" = post-registration verification step.
2. **Verify Novery** — a "novelty" verification service that makes fresh accounts look legitimate/aged:
   - channels: Gmail, **Phone OTP**, **VIP** tier ("Register Novery VIP", "Verify Novery VIP").
   - "Enable Two-Factor (Verify Novery)" — 2FA is enabled *as part of* the verification recipe.
   - "Novery can't make this change" error ⇒ it drives FB's "confirm your identity" flows.
3. **Identity/OTP handling** — "Auto Confirm identity OTP Phone", "Add Delay approve code by mail (30s)", "skip acc confirm code by mail", "mail trusted not get code" fixes ⇒ a robust email-OTP reader with timing control.
4. **Unlock 282** — automated flow for **Facebook error 282** ("You can't use this feature right now" / unusual-activity lock) — effectively a checkpoint-escape bot.
5. **Status Submit Appeal** — appeal submission with status tracking (parallel to Bob Prime's 180-day appeal).
6. **Verify Number Cambodia** — phone-number verification via a Cambodian number service.

**Why this matters for a clone:** this is a *second, much harder* UI-automation surface (registration + identity confirmation + checkpoint screens) on top of the posting/interaction surfaces. It also has a services economy (mails, numbers, novelty) that Bob Prime outsources to 2Captcha/SMS-Activate.

### 2.5 Server-side components (deduced)

- **Licensing** (license keys + login; "License Note"; time-change-key quotas enforced server-side).
- **Device-info server** (fingerprint generation).
- **Mail servers**: farmmails.me stock API + "New Server Gmail" (a Gmail provisioning service) + "Fixed mail trusted" flows.
- **Realtime alerts** ("Realtime alert after transfer acc").
- Payments: Binance Pay top-ups.

### 2.6 UI automation layer

Same stack as Bob Prime's, per the failure patterns in changelogs: screenshots + element/OCR detection + ADB input; FB app version pinned (**"support facebook 490"** — they explicitly add support for specific FB app versions). Unique additions:
- **"Enable Pro Mode Deep Link"** — they use Facebook **deep links (`fb://`)** for professional-mode toggling — direct evidence of a deep-link toolkit.
- **Action templates** ("Save action template") + **auto-save last interact list** + **auto-save file by category** ⇒ configurable, repeatable job recipes (FarmReel's answer to Bob Prime's per-page post settings).
- **REPORT Log** + command console display ⇒ structured logging UI.

---

## 3. Feature-by-feature deep analysis

Format per feature: **What it does → Likely implementation → Clone notes → Complexity / Priority / Dependencies → Failure modes.**

Complexity: S (days) · M (1–2 wks) · L (2–4 wks) · XL (4–8 wks). Priority: P0 core, P1 parity, P2 nice-to-have.
"Evidence:" = where the feature is documented (TG = Telegram changelog, YT = YouTube demo, Site = farmreel.me, CL = site changelog).

---

### 3.1 Device & environment management

#### 3.1.1 Multi-emulator support: LDPlayer Mod + MuMu
- **What:** Drives **LDPlayer (Mod Official / Mod Chinese)** and **MuMu (Installer / Import)**; "Change Mumu Global" (switch MuMu build to global), "Launch Mumu auto connect" (auto boot + ADB connect).
- **Evidence:** Site quick-setup; TG 2.8.8.8, 3.1.8.0.
- **Likely impl:** Abstract device backend: LDPlayer via `ldconsole`, MuMu via `MuMuManager.exe` (MuMu's console: `launch`, `install`, `quit`) + ADB ports; auto-connect = poll ADB until device online.
- **Clone notes:** Design the `IDeviceBackend` abstraction from day one (LD9/LD14/MuMu/real-device). MuMu differs: no `ldconsole`; different config format; different windowing.
- **Complexity:** L · **P0** · backend abstraction.
- **Failure modes:** MuMu console flags differ across versions; ADB port collision between emulators.

#### 3.1.2 Real-device support (Wi-Fi + 4G SIM)
- **What:** Attaches real Android phones: **"Unlimited Real Devices Support"** (site), "Connect WIFI Phone" (TG 3.0.7.0), "Added 4G SIM" (TG 3.0.7.0), "Skip device offline" (TG 3.1.8.0).
- **Likely impl:** `adb connect <phone-ip>:5555` (Wi-Fi) and USB/4G-tether devices; device detected via ADB; per-device screen size ("Scale size window").
- **Clone notes:** Real devices are *more* anti-detection-friendly (genuine fingerprint) and cheaper than emulator farms. Add: USB-debugging setup guide, `adb reconnect` loops, offline retry queue.
- **Complexity:** M · **P1** · ADB + backend.
- **Failure modes:** Battery/charging policy kills sessions; Wi-Fi drops; device sleep; 4G SIMs get flagged for mass registration.

#### 3.1.3 Magisk/Root-preserving LDPlayer setup
- **What:** Tutorial: "របៀបប្រើ LD កុំអោយដាច់ Magisk, Root" ("how to use LD so Magisk/Root doesn't break").
- **Evidence:** TG 2.7.x-era post.
- **Likely impl:** Root access on LDPlayer (Magisk) for deep operations: `pm clear`, spoofing, cookie/DB extraction, time changes; tool must avoid commands that trip SafetyNet/Magisk detection.
- **Clone notes:** Root elevates capability (device-info spoofing via `resetprop`, direct SQLite reads of FB app data, `run-as` access) but adds detection risk. Keep root *optional*, degrade gracefully.
- **Complexity:** M · **P1** · ADB + root tooling.
- **Failure modes:** Magisk hide detection; FB detects root via Play Integrity — many farmers disable root before FB runs and re-enable after.

#### 3.1.4 Device-info fingerprint management (+ server-generated)
- **What:** Per-account `device_info` records; "Fixed **server** new device info" ⇒ fingerprints can be generated server-side and pushed to instances.
- **Evidence:** TG 2.7.3 (device_info folder), 2.8.6.1.
- **Likely impl:** Same replay-every-run strategy as Bob Prime (MAC/IMEI/AndroidID/model/etc.), but with a server endpoint generating consistent random device profiles.
- **Clone notes:** For the clone, a local generator suffices initially; server generation matters for *distribution* (users can't all use same fingerprints). Store fingerprint + IP geo + timezone per account; re-apply before every session.
- **Complexity:** M · **P0** · device layer.
- **Failure modes:** Fingerprint conflicts across customers (why they moved it server-side); FB cross-checks.

#### 3.1.5 OpenVPN per account (2 import options, delay, OTG)
- **What:** Import OpenVPN profiles (two methods), attach per account, "Delay OPENVPN (HOME)", "Improve OpenVPN Support OTG" (USB-OTG / tethering).
- **Evidence:** CL 3.3.0.2, TG 3.2.7.0, 3.2.9.3.
- **Likely impl:** OpenVPN CLI/Windows service per profile; app manages connect/disconnect per instance; delay = stagger VPN connects to avoid IP-flood patterns; OTG = route emulator traffic through a phone's VPN/tether.
- **Clone notes:** Implement a `VpnManager` (profile import from `.ovpn` files — 2 flows: OpenVPN GUI import vs direct config), per-account binding, IP verification + geo check after connect.
- **Complexity:** L · **P1** · network layer.
- **Failure modes:** OpenVPN on Windows requires admin; DNS leaks; emulator network modes (NAT/bridge) block VPN; profile parsing edge cases.

#### 3.1.6 Proxy support
- **What:** "Add Find with PROXY" (TG 2.8.2), "Improve Proxy" (TG 3.3.0.0).
- **Likely impl:** Per-instance HTTP/SOCKS proxy in emulator settings or app-level; "Find with proxy" = use proxy for lookups (group search, page find).
- **Clone notes:** Proxy per account + IP verification; support auth; rotate on failure.
- **Complexity:** M · **P1** · network layer.
- **Failure modes:** Emulator proxy support is poor (Android emulator network config via `adb shell settings`); apps that ignore system proxy.

#### 3.1.7 Environment setup wizard
- **What:** "Setup Environment | Check and Setup", "Check Environment", "Added Check Environment" (TG 3.2.7.0).
- **Likely impl:** One-click dependency check: LDPlayer/MuMu path, ADB, root, OpenVPN, APKs, network, license — with auto-fix.
- **Clone notes:** High UX value, cheap to build; runs pre-flight before batches.
- **Complexity:** M · **P1** · system tooling.
- **Failure modes:** Paths with spaces/Unicode; missing VC++ runtimes.

#### 3.1.8 Backup/restore suite
- **What:** "Backup Database (everyday)" (2.7.5), "Manual Backup" (2.8.6.1), "check backup wrong profile" (2.8.2), "Backup Groups" (3.0.7.0), "Auto Backup Cookie" (3.2.8.0), "backup full cookie" (CL 3.3.0.2), "Fixed cookie backup issues" (CL 3.3.2.1).
- **Likely impl:** Scheduled DB snapshot; per-account profile & cookie backups (FB session cookies survive restarts); verify profile matches account before restore.
- **Clone notes:** Cookies + profiles are the *real* session assets; backup them frequently and atomic-rename. DB backup daily with retention.
- **Complexity:** M · **P0** · persistence.
- **Failure modes:** Cookie expiry; wrong-profile restore corrupts account mapping.

#### 3.1.9 Time Change Key (quota-gated time spoofing)
- **What:** Site pricing: 2/10/100 "Time Change Key" per plan; presumably changing the device time/timezone context per account, consumed per use.
- **Likely impl:** Server-metered operation: `adb shell date` / emulator timezone change + device "time context" record; decrements quota.
- **Clone notes:** If we clone the *business*, quotas need server enforcement; the local feature is trivial (set timezone/date + GPS).
- **Complexity:** S · **P2** · metering service.
- **Failure modes:** Quota abuse; time changes visible to FB (GPS/time mismatch).

#### 3.1.10 Task / post scheduling
- **What:** "Function Schedule" (site), "Fixed Limit Post schedule" (3.1.8.0), "Fixed Task Schedule" (3.2.9.4).
- **Likely impl:** Cron-like scheduler with per-page/per-account limits.
- **Clone notes:** Same as Bob Prime's scheduler; add *limits* (max posts per day per account).
- **Complexity:** M · **P0** · scheduler.
- **Failure modes:** PC asleep; clock changes.

---

### 3.2 Account lifecycle & creation (FarmReel's crown jewels)

#### 3.2.1 Login & Manual Login
- **What:** Bulk login (email/UID + password + 2FA) + **Manual Login** (2.8.6.1) for sessions the tool can't automate.
- **Likely impl:** UI-automation login; manual mode = user logs in, tool captures cookie/profile after ("Manual Backup").
- **Clone notes:** Login + cookie capture is the critical path for everything else. Capture cookies from FB's app-data DB or network.
- **Complexity:** L · **P0** · auth pipeline.
- **Failure modes:** Checkpoints; captcha; 2FA routes (SMS/email/TOTP).

#### 3.2.2 Reg Full — bulk account registration
- **What:** Fully automated Facebook account creation: **"Reg Full"** with mail Microsoft (3.2.8.0), Gmail (3.3.0.0), **Phone OTP incl. USA** (3.3.0.0), "Reg Full verify" (3.1.8.0), "Reg Full verify Lite (Beta)" (3.2.9.0), "Reg Full Zin ⇒ Lite" migration (3.2.9.2), "Reg full Phone OTP" improvements (CL 3.3.0.2).
- **Likely impl:** Fresh device (or wiped instance) → FB install → register flow → fill name/DOB/gender from template → email or phone OTP capture → solve captcha (they fixed captcha bugs) → complete → immediately verify → record UID/cookie → move to "new accounts" pool. "Zin" is likely an SMS/OTP vendor; "Lite" a cheaper tier.
- **Clone notes:** This is an XL feature. Requires: name/DOB pools (realistic profiles), OTP readers (email + SMS), captcha gateway, per-account fingerprint + IP, and *post-registration warm-up* scheduling (avoid instant-farming detection). Ties into account selling ("copy sell accounts").
- **Complexity:** XL · **P1 (feature-parity), but P0 for the FarmReel-style business** · auth pipeline, OTP, captcha, fingerprints.
- **Failure modes:** FB heavily bots registration (identity confirm, "try again later", IP bans); provider email domains flagged; phone-OTP cost; captcha cost; high ban rate on fresh accounts.

#### 3.2.3 Verify Novery (novelty verification) + VIP
- **What:** "Verify novery Gmail" (3.1.8.0), "Verify Novery with Phone OTP" (3.3.0.2), "Register Novery VIP" / "Verify Novery VIP" (3.2.6.0), "Enable Two-Factor (Verify Novery)" (3.2.8.0), "Skip novery on Interact" (3.2.7.0), "Novery can't make this change" fix (3.2.8.0), "Verify Novery Gmail bug" fix (3.3.1.7).
- **What it is:** "Novery" ≈ **novelty**: making fresh accounts *look* aged/authentic — confirming identity (email/phone), enabling 2FA, setting profile elements, so the account passes automated and human review. Likely a *paid service tier* (VIP) the team runs.
- **Likely impl:** A recipe engine over FB's security flows: profile completion → email confirm → phone confirm → 2FA enable → avatar/cover → checkpoints pre-empted.
- **Clone notes:** Feature = "identity-confirmation recipe engine" + optional service. Recipe: fill profile, confirm email, confirm phone, enable 2FA, add friends/posts pacing. This is where most of the "anti-detection value" lives.
- **Complexity:** L · **P2** · auth flows + services.
- **Failure modes:** FB requires photo-ID for some; "can't make this change" (temporary locks); rate limits.

#### 3.2.4 2FA management suite
- **What:** "copy code 2Fa" (2.8.6.1), "Enable Two-Factor (Verify Novery)" (3.2.8.0), "Fixed Off-On 2FA" (3.2.8.0), "Fixed bug enable 2FA" (3.0.6.0), "Fixed Enable 2FA" (3.1.8.0), "Added POST REEL Column 2Fa" (3.0.6.0 — 2FA column in the post table).
- **Likely impl:** Enable via app flow (SMS/email/app authenticator), copy TOTP secret/recovery codes into the account row, disable when needed.
- **Clone notes:** Store TOTP secret; generate codes locally (RFC 6238). Add 2FA column to the accounts grid.
- **Complexity:** S–M · **P1** · auth.
- **Failure modes:** FB moved 2FA setup into "Accounts Center"; recovery-code capture screens differ.

#### 3.2.5 Unlock 282 (FB error 282 / unusual-activity lock)
- **What:** "FarmReel Solve Facebook 282" (YT), "Fixed unlock 282" (3.2.9.3), "Improved Unlock 282" (3.3.2.0).
- **Background:** FB error **282** = "You can't use this feature right now" — a temporary block on actions (commenting, posting, adding friends), common on farmed accounts.
- **Likely impl:** Detection → guided/automatic "get unlocked" flow: verify identity (email/phone OTP), wait-out periods, safe-activity warm-up, appeal where possible; re-check state after.
- **Clone notes:** Build a `BlockState` classifier (282, checkpoint, ban, temp-lock) + per-block remediation recipes + cooldown schedules.
- **Complexity:** L · **P2** · auth flows + state machine.
- **Failure modes:** FB changes unlock paths; some locks are permanent; over-automation re-triggers.

#### 3.2.6 Status Submit Appeal
- **What:** "Improved Status Submit Appeal" (3.3.1.6).
- **Likely impl:** Appeal submission for disabled/locked accounts with status tracking (parallel to Bob Prime's 180-day appeal).
- **Clone notes:** Shared appeal engine: capture account DOB/email, submit form, track status.
- **Complexity:** M · **P2** · auth flows.
- **Failure modes:** Appeal forms vary by violation type; captcha on submit.

#### 3.2.7 Phone verification (incl. Cambodia)
- **What:** "Verify Number Cambodia" (3.3.2.0), "Add Auto Confirm identity OTP Phone" (3.3.0.0), "reg with Phone OTP (USA)" (3.3.0.0).
- **Likely impl:** Integration with phone-number services (incl. a Cambodian provider) for receiving OTPs; auto-confirm identity via phone OTP.
- **Clone notes:** Pluggable SMS gateway; per-country inventory; OTP polling + auto-fill.
- **Complexity:** M · **P2** · SMS services.
- **Failure modes:** Number reuse; carrier delays; service country coverage.

#### 3.2.8 Mail import & trusted mail (Zoho / Gmail / Microsoft)
- **What:** "Fixed Add mail zoho" (2.8.8.8), "Added Microsoft Mail Import" (3.2.6.0), "Reg Full with mail Microsoft" (3.2.8.0), "New Server Gmail" (3.3.1.7), "Add mail New UI (Trusted mail)" (3.2.7.0), "mail trusted not get code" fix (3.2.9.3), "Remove Mail" (3.2.8.0).
- **Likely impl:** IMAP readers for Zoho/Outlook(Gmail via service); "trusted mail" = mail addresses that FB reliably sends codes to; per-account mail mapping; manual add UI.
- **Clone notes:** IMAP OTP reader (Bob Prime's Email Manager covers Zoho/Yandex; add Outlook/Gmail IMAP + app passwords); "trusted mail" scoring.
- **Complexity:** M · **P1** · IMAP + services.
- **Failure modes:** Microsoft OAuth-only IMAP; Gmail app passwords; provider throttling.

#### 3.2.9 Account info management
- **What:** "Change Name (New UI)" (3.2.6.0), "Change profile, cover" (3.1.8.4), "Set wallpaper" (3.0.7.0), "Download Profile Picture" (3.2.7.0), "Display Profile List view" (3.2.7.0), "Change Profile Auto Image" (3.3.1.7), "Set Location" (3.0.9.0), "Check Date created" (3.0.6.0), "Sort Date Created" (3.0.9.0), "Copy info home" (3.0.7.0), "Home Note" (3.0.6.0), "Fixed Check primary location"-type flows ("Check in").
- **Likely impl:** Profile-edit flows via UI automation; auto images from a pool; location setting; account-meta columns (created date, note).
- **Clone notes:** Same as Bob Prime's "Set account info" + extra: wallpaper, auto profile image, DOB/created-date records, notes field.
- **Complexity:** M · **P2** · UI driver.
- **Failure modes:** Name-change cooldowns; auto-image quality.

#### 3.2.10 Account selling / transfer
- **What:** "Improve copy sell accounts" (3.3.0.0), "Realtime alert after transfer acc" (3.1.8.0).
- **Likely impl:** Export account bundles (cookies/profiles/device info) for sale; alerts when an account is transferred away (server-tracked).
- **Clone notes:** *Business* feature: secure export format + ownership transfer + server records. High liability.
- **Complexity:** L · **P3** · server + security.
- **Failure modes:** Cookie theft liability; legal exposure.

---

### 3.3 Posting engine

#### 3.3.1 Post Reel (+ comment, random folder, filters)
- **What:** Reels posting with auto-comment: "Function POST REEL" / "POST REEL + Comment" (YT), "Post Reel + cmt (Random Folder)" (CL 3.3.1.0), "Post Reel Filter filename first&last" (3.2.9.0), "choose Reel or Video + Comment" (3.2.9.3), "Fixed Post Reel AI Label" (3.3.1.7), "Added POST REEL Column Email/Pessimal/2Fa" (3.0.6.0).
- **Likely impl:** Reels composer automation; random folder = pick reel files from random content folders; filename filters (first/last characters) for ordering; per-row columns (email, personal note, 2FA) in the post table; AI label toggle.
- **Clone notes:** Reuse the Bob Prime reels flow + add: random-folder selection, filename filters, "reel OR video" toggle, per-row metadata columns.
- **Complexity:** XL · **P0** · posting engine.
- **Failure modes:** FB reel UI churn (constant fixes in both products); audio picker; <3s/>90s validation.

#### 3.3.2 Post Image (+ comment, AI caption, groups, public)
- **What:** "Function POST IMAGE" / "Function Image + Comment" (YT), "Fixed Post Image Caption AI" (3.2.9.0), "Fixed Post Image Group" (3.2.9.0), "Post Image Set Public" (3.2.9.3).
- **Likely impl:** Photo composer; AI caption generation; optional cross-post to a group; audience = public.
- **Clone notes:** Same as Bob Prime photo post + audience toggle + AI caption.
- **Complexity:** M · **P0** · posting engine.
- **Failure modes:** Gallery selection; "Share" button (Bob Prime had same fix).

#### 3.3.3 Post Video (multi-path, AI label, location)
- **What:** "POST VIDEO Multiple path" (3.0.6.0), "POST VIDEO AI label" (3.0.6.0), "POST VIDEO Location" (3.0.6.0), "Fixed Post Video" (3.3.2.0).
- **Likely impl:** Video composer; multiple content folders; AI-label disclosure; location attach.
- **Clone notes:** Same as Bob Prime video post + multi-path + location.
- **Complexity:** L · **P1** · posting engine + codec check.
- **Failure modes:** Codec support; upload timeouts.

#### 3.3.4 Post Status (+ comment, AI)
- **What:** "Add Post status + comment" (CL 3.3.0.2), "Fixed Post Status AI" (3.2.9.5).
- **Likely impl:** Text posts from file/AI + optional self-comment.
- **Clone notes:** Trivial on top of text pipeline.
- **Complexity:** S · **P1** · text pipeline.

#### 3.3.5 Story + Link (+ Pro Mode, privacy)
- **What:** "Function Story + Link" (YT), "Fixed Story + Link" (3.3.0.0), "Add Story Pro Mode" (3.2.9.5), "Improve Story privacy public" (3.2.9.4), "View Story" / "View story by id" (3.1.8.4).
- **Likely impl:** Story composer with link sticker from per-file link txt; professional-mode identity for story; audience public; view stories by profile id (impression generation).
- **Clone notes:** Same story module as Bob Prime + pro mode + view-by-id (opens `fb://story/<id>`).
- **Complexity:** L · **P1** · story engine.

#### 3.3.6 Captions & content helpers
- **What:** File captions; AI captions (3.2.9.0); "Auto Save File (category)" (3.2.9.0); "Save action template" (3.2.6.0); "Auto Save last List Interact" (3.2.9.0).
- **Likely impl:** Caption resolution (per-file txt, random, AI), content categorization on save, reusable action templates (named job configs), auto-restore last interact list.
- **Clone notes:** Add "action templates" to the clone — a big usability win over Bob Prime's per-row configs.
- **Complexity:** S–M · **P1** · data layer.

#### 3.3.7 Invite collaborator
- **What:** "Added Invite collaborator" (3.0.7.0).
- **Note:** Same feature as Bob Prime's collaborator invite. **M · P2.**

#### 3.3.8 Crosspost / share to groups
- **What:** "Added Crosspost Groups (REEL)" (3.0.9.0), "Added Share groups (REEL)" (3.0.9.0), "Fixed Post Groups" (3.2.9.5), "1 Path Folder for Multiple Pages (Beta)" (2.8.8.8).
- **Likely impl:** After posting, share to N groups; crosspost reel to group; one content folder shared across multiple pages.
- **Clone notes:** Same share engine as Bob Prime + shared-folder multi-page mode.
- **Complexity:** M · **P1** · share engine.

#### 3.3.9 Monetization & analytics suite
- **What:** "Check Monetization" (CL 3.3.1.0), "Check CM Waitlist criteria" (3.2.9.4 — Creator **Monetization** waitlist eligibility), "Check Reach" (2.8.8.8), "Improved Reach Sorting" (3.2.6.0), "Check Recommendations" (3.2.9.4), "Label: LIVE, DIE" (CL 3.3.1.0), "Show count page" (2.8.2).
- **Likely impl:** OCR dashboard; classify account/page status (LIVE/DIE, monetization eligibility, waitlist criteria, reach, recommendations); sortable columns.
- **Clone notes:** Same "dashboard pull" as Bob Prime + *waitlist-criteria check* + reach sorting + recommendations — richer page-health model.
- **Complexity:** L · **P2** · OCR + parsing.

#### 3.3.10 File lifecycle & post management
- **What:** "Fixed Move file with username" (2.8.2), "Fixed Delete Post" (3.1.8.0), "Delete Page" (2.8.6.1), "Fixed Delete Page session expired" (3.2.8.0), "Fixed Limit Post schedule" (3.1.8.0).
- **Likely impl:** Posted files moved into per-username folders; delete posts/pages flows; per-account daily post limits.
- **Clone notes:** Same .posted/.failed lifecycle + per-username folder naming + session-expired handling.
- **Complexity:** M · **P1** · file + UI driver.

---

### 3.4 Active / interaction engine

#### 3.4.1 Active Acc or Pages
- **What:** "Active Acc or Pages" (YT) — the engagement loop on accounts *and* pages.
- **Likely impl:** Feed/watch loops: scroll, view, like/follow/comment/share with probability policy, per-account caps, checkpoint detection.
- **Clone notes:** Same as Bob Prime's Active engine, extended to Pages.
- **Complexity:** L · **P1** · interaction engine.

#### 3.4.2 Interact Post (share / comment / delete-after)
- **What:** "Fixed share interact post" (3.1.8.4), "Fixed Share Group (Interact Post)" (3.2.7.0), "Added Delete after used comment (Interact Post)" (CL 3.3.1.0), "Auto Save last List Interact" (3.2.9.0).
- **Likely impl:** Share posts during interaction; comment then *delete* the comment after use (comment-farming trick to inflate engagement without leaving spam); save last interacted list for resume.
- **Clone notes:** Add "delete after used comment" + resume lists — nice differentiators.
- **Complexity:** M · **P2** · interaction engine.

#### 3.4.3 Like & Follow / Watch Live / View story
- **What:** "Improve Like & Follow" (CL 3.3.0.2), "Add Watch Live" (2.8.6.7), "View story by id" (3.1.8.4).
- **Likely impl:** Like/follow loops; live-view dwell; story impressions by id.
- **Clone notes:** Same as Bob Prime's view features + live.
- **Complexity:** M · **P2** · interaction engine.

#### 3.4.4 Check-in (audience/public/auto-location)
- **What:** "Fixed check in" (2.8.6.7), "Check In Set Public" (3.2.9.3), "Fixed Check in change audience" (3.2.7.0), "Added Check In Auto Location" (3.2.9.0).
- **Likely impl:** Location check-in posts; audience control; auto-location from IP geo.
- **Clone notes:** Same as Bob Prime's check-in + audience + auto-location.
- **Complexity:** M · **P2** · composer.

---

### 3.5 Groups management

#### 3.5.1 Full groups suite
- **What:** "Join Groups by ID" (3.0.7.0), "Join Groups Suggestion" (3.0.7.0), "Leave Group" (3.0.7.0), "Post Groups" (3.0.7.0), "View Group Lists" (3.0.7.0), "Groups (count)" (3.0.7.0), "Backup Groups" (3.0.7.0).
- **Likely impl:** Group join by link/ID, suggested groups, leave, post into groups, view/backup joined-group lists.
- **Clone notes:** Bob Prime has join by ID/link & keyword; FarmReel adds leave, post-into-group, suggestions, backup. Merge both sets.
- **Complexity:** M · **P1** · join/post engine.
- **Failure modes:** Group caps; private-group requests; FB layout changes.

---

### 3.6 Platform & business layer

#### 3.6.1 License system + quotas + updates
- **What:** License keys & login (site: "Login License Keys"), "License Note" (2.8.2), Time Change Key quotas, "Fixed license" items; hard base-version gates (3.3.0.0).
- **Likely impl:** Server-validated licenses; quota ledger; version-gated updates (old versions disabled remotely).
- **Clone notes:** If commercializing: server licensing + metering + forced-update channel.
- **Complexity:** L · **P2** · server.

#### 3.6.2 Reporting & UI ergonomics
- **What:** "REPORT Log" (3.3.2.0), "Fixed command console display" (CL 3.3.2.1), "Fixed UI Row Color" (3.2.9.4), "Display configs" (2.7.5), "Find page on list" (2.7.5), "Search on list" (2.8.2), "Scale size window" (3.1.8.0), "Fixed long filename" (2.7.5), "Fixed Search Limit" (3.2.7.0).
- **Likely impl:** Structured logs + in-app console; list search/filter; window scaling for varied DPI.
- **Clone notes:** Logs viewer + searchable grids + DPI-safe UI — cheap and high-value.
- **Complexity:** S–M · **P1** · UI/data.

#### 3.6.3 Mail stock business (farmmails.me)
- **What:** "Available Stock — mail services and prices" (site); farmmails.me "Buy Fresh Email Accounts — Hotmail, Outlook, and more"; API-key top-up via Binance Pay (TG 3.2.8.0-era posts).
- **Likely impl:** E-commerce storefront + API to deliver purchased mails into the tool.
- **Clone notes:** Only if replicating the *business model*; otherwise out of scope for the software clone.
- **Complexity:** XL · **P3** · e-commerce + delivery.

---

## 4. Evidence base & sources

| Source | What it provided |
|---|---|
| https://www.farmreel.me/ | Pricing tiers, Time Change Key, device/emulator list, "Available Stock", positioning, quick-setup, features screenshot (image only) |
| https://www.farmreel.me/changelog | v3.3.2.1 (Aug 9 2026): Reg Full, Verify Novery, Post Reel + cmt (Random Folder), Check Monetization, LIVE/DIE labels, phone-OTP verify, backup full cookie, etc. |
| https://t.me/s/farmreel | Version history 2.7.3 → 3.2.9.3: folder layout (DB/device_info/profiles), daily DB backup, proxy find, manual login/backup, 2FA copy, watch live, MuMu global, FB 490, 4G SIM, invite collaborator, set location, novery VIP, Microsoft mail, unlock 282, OTG |
| https://t.me/s/FARMREELDEMO | Versions 3.2.7.0 → 3.3.2.0: trusted-mail UI, check environment, download profile picture, IP location display, Reg Full with MS mail, auto backup cookie, reel filename filters, CM waitlist, pro mode, report log, verify number Cambodia, appeal status, deep-link pro mode |
| https://www.youtube.com/@FARMREEL_Official (DEMO playlist) | Tutorial titles: Story+Link, POST REEL+Comment, Image+Comment, POST IMAGE, POST REEL, Solve FB 282, View Story/Verify novery, Active Acc or Pages, Login Accounts, Setup |
| Wayback CDX (farmreel.me*) | Confirms Vite/React SPA structure; no archived readable content beyond SPA shell |
| https://www.facebook.com/farmreelofficial/ | Business presence, Telegram handles |
| https://www.farmmails.me/ | Mail-stock storefront (Hotmail/Outlook fresh accounts) |

Facts not directly observed are labeled "likely impl" / "deduced" in this document.

---

## 5. What FarmReel teaches us that Bob Prime doesn't

1. **Emulator choice is a feature.** Bob Prime = LDPlayer 9/14 only. FarmReel = LDPlayer mods + MuMu + real phones (Wi-Fi/4G). A clone should abstract the device backend from day one.
2. **Account *creation* is the harder (and more valuable) surface.** Registration, novelty verification, 2FA enablement, and error-282 unlocking are flows Bob Prime doesn't even attempt. They need their own state machines, OTP pipeline, and captcha handling.
3. **VPN is per-account, user-supplied.** OpenVPN profiles + proxies beat built-in GPS/timezone sync for IP hygiene. Provide a VPN manager.
4. **Fingerprints can be a service.** Server-generated device info prevents fingerprint collisions between customers — a real operational insight.
5. **Quota-based monetization** (Time Change Key) and **version-gated updates** (hard base versions) shape the product lifecycle.
6. **An adjacent business** (selling mails/numbers/novelty verification) is how these teams actually make money — the software is the funnel.
7. **"Trusted mail" and mail-provider quality matter** for OTP reliability — an operational dimension Bob Prime's docs barely touch.

---

*Next: see `docs/bob-prime-vs-farmreel.md` for the merged comparison and the unified clone blueprint (feature parity matrix + updated architecture + roadmap).*
