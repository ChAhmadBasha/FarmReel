# Bob Prime — Deep Feature Analysis (Clone Blueprint for FarmReel / "bobplayer")

**Source analyzed:** https://www.bobdevteam.com/bob-prime + official tutorial pages
**Analysis date:** 2026-08-16
**Purpose:** Reverse-engineer the *feature set* of Bob Prime so we can design and build a functionally-equivalent clone (codename: **bobplayer**, repo: **FarmReel**).
**Scope note:** "Bob Player" on the web is an unrelated IPTV app; the linked product is **Bob Prime** (Facebook automation), which is what this document analyzes.

> **Compliance disclaimer:** Bob Prime automates Facebook accounts at scale. This violates Facebook's Terms of Service and can result in account bans/checkpoints. This document is a technical feature analysis for engineering purposes. Any clone should be built and operated with a clear understanding of the ToS, legal (anti-bot / platform abuse), and reputational risks — see §6.

---

## Table of contents

1. [Product snapshot](#1-product-snapshot)
2. [How Bob Prime works — architecture](#2-how-bob-prime-works--architecture)
3. [Feature-by-feature deep analysis](#3-feature-by-feature-deep-analysis)
4. [Clone blueprint for bobplayer/FarmReel](#4-clone-blueprint-for-bobplayerfarmreel)
5. [Effort estimate & MVP roadmap](#5-effort-estimate--mvp-roadmap)
6. [Risk register & compliance](#6-risk-register--compliance)
7. [Appendix — feature traceability checklist](#7-appendix--feature-traceability-checklist)

---

## 1. Product snapshot

**Bob Prime** is a Windows desktop application that automates the **Facebook mobile app** running inside **LDPlayer** (an Android emulator for Windows). Its value proposition is "bulk": dozens/hundreds of Facebook accounts and Pages, each pinned to its own emulator instance ("one account = one instance"), all driven from one PC.

**Positioning in the market:** it sits in the "Facebook farming / page building" gray-market category, competing with tools like Autobots Soft, TMT, etc. Its differentiators (from the landing page):

- One-click LDPlayer setups (instances created/copied/configured automatically)
- Device-fingerprint management per instance (MAC, IMEI, Android ID, model, GPS) for account safety
- OTP handling (email IMAP + SMS services) and captcha solving (2Captcha)
- Page building & monetization tooling (dashboard pull, monetization eligibility, support-inbox cleanup, AI captions)
- Integrated AI (captions/comments) and Amazon affiliate link injection

**Key versions/facts learned from patch notes (V1.5.x–V1.6.16):**
- Supports **LDPlayer 9 and LDPlayer 14**; official Facebook APK (`com.facebook.katana`) and "Facebook clone" apps.
- Patch-update distribution = password-protected RAR files applied over an install (so the core is a portable/fixed install + config dir).
- Features that break often (from the fix log): reel posting (FB UI changes), photo/video post buttons, switch-profile, share-to-group, add-music, delete-all-posts, LDPlayer device-info backup/restore, network bridging toggle. **This is the single most important engineering lesson: the clone's cost center is keeping UI automation selectors working against a moving Facebook app.**
- AI features: "reply chat with AI", "check-in AI status", "AI caption if post has no caption", "AI generate status post".

---

## 2. How Bob Prime works — architecture

### 2.1 Runtime model

```
┌────────────────────────────────────────────────────────────┐
│  Windows PC                                                    │
│                                                                │
│  ┌─────────────────────────────┐    ┌──────────────────────┐  │
│  │  Bob Prime (desktop app)    │    │  LDPlayer 9/14       │  │
│  │  - UI (tabs & tables)       │    │  instance #1         │  │
│  │  - Scheduler/orchestrator   │───▶│  Facebook app        │  │
│  │  - UI-automation driver     │    │  (katana / clone)    │  │
│  │  - Device manager (console) │    ├──────────────────────┤  │
│  │  - DB + encrypted secrets   │    │  instance #2 ... #N  │  │
│  │  - 3rd-party API clients    │    └──────────────────────┘  │
│  └─────────────────────────────┘                              │
│        │ LDPlayer console (ldconsole.exe) + ADB (127.0.0.1:port)│
│        ▼                                                     │
│   Internet: 2Captcha, SMS-Activate, IMAP (Zoho/Yandex),      │
│   AI provider, proxies/VPN per instance                      │
└────────────────────────────────────────────────────────────┘
```

### 2.2 Control plane — LDPlayer console + ADB

LDPlayer ships a console executable (`ldconsole.exe` on LDPlayer 9, similar on 14) and an ADB server. Bob Prime uses it for:

| Capability | Console/ADB mechanism |
|---|---|
| Create/copy/remove instances | `ldconsole add/copy/remove --name --index` |
| Launch/quit instances | `ldconsole launch --index`, `ldconsole quit --index`, `quitall` |
| Install/update apps | `ldconsole installapp --packagename <apk path>` |
| Set RAM/CPU | `ldconsole modify --index --cpu N --memory N` |
| Set device fingerprint | `modify --imei --imsi --simserial --androidid --mac --model --manufacturer --serial --resolution --dpi` |
| GPS | virtual GPS toggle + coordinate injection (`setprop` / LDPlayer GPS settings) |
| Backup/restore | `ldconsole backup --index --file`, `restore --index --file` |
| Screen control | `ldconsole screencap`, `adb shell input ...` |
| App-level control | `adb shell am start/force-stop`, `adb shell input tap/swipe/text`, `uiautomator dump`, `screencap` |
| Network bridging | LDPlayer "network bridge" mode → per-instance distinct IP; Bob Prime can enable/disable + pull per-instance IP and geo-lock ("Block"/"Allow" country lists) |

**Clone takeaway:** the entire device plane is scriptable without any LDPlayer SDK — plain subprocess calls to `ldconsole` + `adb`. The clone does not need to touch the LDPlayer emulator internals.

### 2.3 UI automation layer (the heart of the product)

Bob Prime drives the Facebook **Android app** like a human would. The stack is almost certainly:

1. **Screen acquisition:** `adb exec-out screencap -p` (or LDPlayer screencap).
2. **Element/coordinate detection:** mix of
   - `uiautomator dump` → XML hierarchy → find nodes by text/resource-id (fast, but Facebook renders much of the UI in a custom WebView/canvas where the tree is empty);
   - **OCR** on screenshots (Tesseract/PaddleOCR) for text-heavy flows (comments, captions, buttons with text);
   - **Template matching (OpenCV)** for icon/button images that have no text and no accessibility nodes.
3. **Input:** `adb shell input tap x y`, `input swipe`, `input text`, `input keyevent`, `am start`/`am force-stop`, plus Facebook app deep links (`fb://`) where available.
4. **State machine per flow:** each feature (login, post reel, join group…) is a scripted sequence of *wait-for-element → act → verify* steps with retries and timeout handling. Patch notes like "fixed reel post, no button" show these flows are brittle and version-specific.

**Why not Appium/uiautomator2 APIs inside the app?** A production tool of this type avoids anything that requires an instrumentation agent installed on the emulator (extra fingerprint risk, slower). Pure ADB + screenshot/OCR keeps the emulator looking like a stock device with just Facebook installed. The clone should follow the same approach.

### 2.4 Data & persistence

- Local database (SQLite most likely) storing accounts, pages, LD instances, device info, post jobs, logs.
- Credentials stored **encrypted** ("passwords are saved in encrypted form" — Email Manager docs). Likely Windows DPAPI or AES with a machine-derived key.
- File conventions for content (from the Auto Post tutorial):
  - Content folders scanned for `photo.jpg`, `video.mp4`, etc.
  - Per-file captions: `filename.txt` or `filename_caption.txt`
  - Per-file comments: `filename_comment.txt`; comment photos in `.comment_photos/<filename>`
  - Multi-photo posts: `filename_M1.ext … filename_M9.ext` + `filename_M.txt` caption (max 9)
  - Story links: `filename_link.txt`
  - Results moved to `.posted/` or `.failed/`; optional delete after posting.
- Account login profiles backed up as `UID.tar.gz` (Facebook app-data backup per instance).

### 2.5 Scheduling & orchestration

- **Post When Run** — immediate execution on clicking Run.
- **Schedule Post** — wall-clock time match on the PC (24h) and weekly schedules; batch engine runs N active batches ("Number of Active Batch: max 5").
- Throttling knobs: number of simultaneous LD instances, wait-after-boot (35s+), delay between instance starts (15s+), wait after upload (30s+), cache-clear every 100–200 runs.
- **Auto-stop at** a clock time; **shutdown PC** after finishing or at a set time.
- Devices tab: "Run at startup after X" (auto-click Run 30s after launch).

### 2.6 Third-party integrations

| Service | Purpose |
|---|---|
| **2Captcha** | Image/funcaptcha solving for login, checkpoints, 180-day appeals (API key configured by user) |
| **SMS-Activate** | Virtual numbers for SMS OTP / phone verification |
| **Zoho / Yandex IMAP** (app passwords) | Read verification codes from inboxes automatically (Email Manager) |
| **AI provider** (GPT-class) | Caption generation, status-post generation, chat reply, "check-in AI status" |
| **Amazon Associates** | Affiliate link injection into posts/comments |
| **VPN/proxy** | Per-instance IPs (recommended by docs); IP country allow/block lists |

### 2.7 Licensing & updates

- License key system ("How to apply license of Bob Prime Pro"), machine-bound presumably.
- Updates shipped as RAR patches (password `bobprime`) — a base install + incremental patch files. The clone can ship a simpler auto-updater, but should plan for **versioned flows** (Facebook app version → UI flow version).

### 2.8 Detection-resistance design (what Bob Prime does)

This is the "account safety" selling point. Components:

1. **Device fingerprint stability** — each account stays on the same LDPlayer instance forever; per-instance device info (MAC, model, manufacturer, Android ID, IMEI, GPS, network-bridging setting) is stored in a table and **re-applied on every run** ("Every time you click Run, this information will be backed to each LDPlayer … even if you delete and create an LDPlayer at the same ID").
2. **IP hygiene** — GPS + timezone auto-set to match the instance's IP; country allow/block lists; network bridging for unique IPs; proxy/VPN recommended.
3. **Human-like pacing** — waits between instance starts, randomized interactions (Random Caption, random react/comment), interaction limits configurable.
4. **Cleanup** — clear Facebook user data, clear LDPlayer VMs, cache clearing, force-stop management.

> Engineering note: fingerprint *stability* is the hard part. Facebook checks consistency of device model/Android ID/IMEI/MAC/IP geo across sessions. A clone must persist and replay device info deterministically per instance.

---

## 3. Feature-by-feature deep analysis

Format per feature: **What it does → Likely implementation → Clone notes → Complexity / Priority / Dependencies → Failure modes.**

Complexity scale: S (days) · M (1–2 wks) · L (2–4 wks) · XL (4–8 wks) for one engineer.
Priority: P0 = core to MVP, P1 = needed for parity, P2 = nice-to-have.

---

### 3.1 Facebook Account module (Account Manager)

#### 3.1.1 Auto switch Profiles
- **What:** Switches the Facebook app between multiple logged-in profiles (the app's "Switch Account" flow) by account name, so several accounts can share… actually in Bob Prime's model each instance has one account; this handles the multi-profile case / retry switching ("Fixed failed switch Main profile").
- **Implementation:** Open app menu → Profile → Switch accounts → pick profile by name (OCR/uiautomator) → wait for feed to confirm; retry with back/force-stop on failure.
- **Clone notes:** Flow engine primitive "switchProfile(name)"; verify by checking the top nav / profile name after switch. Support switching to "Main profile".
- **Complexity:** M · **P1** · depends on core UI driver.
- **Failure modes:** FB moved switch-account entry points; names differ for Pages vs profiles.

#### 3.1.2 Login (UID/Email | Password | 2FA)
- **What:** Logs an account into Facebook on its instance: email-or-UID + password + 2FA (TOTP, SMS, or email code). Re-login automatically when logged out during any run.
- **Implementation:** `force-stop` FB → clear/keep data → `am start` FB → tap "Log in" → fill email (UID resolved to email via profile pull), password → detect 2FA screen (code entry) → obtain code from TOTP secret / SMS-Activate / Email Manager IMAP → submit → handle "save password/device" dialogs → verify feed. Backup of login session as `UID.tar.gz` for restore.
- **Clone notes:** Need (a) TOTP (RFC 6238) lib, (b) IMAP OTP reader, (c) SMS API client, (d) captcha hook (FB login throws funcaptcha). Deep-link `fb://` can shortcut some steps. Keep an encrypted credential vault. Store last-login state per account.
- **Complexity:** L · **P0** · Email Manager, captcha, SMS services.
- **Failure modes:** Checkpoint/identity-confirm screens, captcha, "account locked", app version changes, 2FA via backup codes, unicode/paste issues (`input text` doesn't support all chars → use `adb shell am broadcast` clipboard trick).

#### 3.1.3 Check Live/Die
- **What:** Verifies account status (live vs disabled/checkpointed) using UID (no password needed).
- **Implementation:** Query Facebook profile via web view/GraphQL-like public profile fetch by UID, or open `fb://profile/<uid>` in the app and classify the screen (profile visible = live; "account temporarily locked" / "disabled" = die; review-required = checkpoint).
- **Clone notes:** HTTP request to `https://www.facebook.com/<uid>` with a mobile UA + parse title/OG tags is cheap and fast; fallback to in-app check for accuracy. Batch endpoint with concurrency.
- **Complexity:** S · **P0** · none (HTTP only).
- **Failure modes:** Rate limiting by Facebook when probing many UIDs; UID privacy (some profiles hidden).

#### 3.1.4 Create Pages
- **What:** Automatically creates Facebook Pages from the account.
- **Implementation:** In-app flow: Menu → Pages → Create new Page → choose type/name/category → optional profile pic → publish. Fill from a per-account template (name list / category list).
- **Clone notes:** Needs a "fill form from CSV/JSON template" capability + button flow; verify page appears under Pages; record page name/ID.
- **Complexity:** L · **P1** · UI driver, page-info setter.
- **Failure modes:** FB often checkpoints fresh accounts trying to create pages; category picker is a custom scroll view (OCR/coordinate hell); name validation.

#### 3.1.5 Auto pull account & page name
- **What:** Reads the logged-in account name (and Page names) from each instance and syncs into the managers ("Auto Pull Account/Page Name"; "Improved scan page name").
- **Implementation:** Open profile → read name text via OCR/uiautomator; for pages, open Pages menu and scrape list; dedupe vs existing rows (or "Clear Existing Names" first).
- **Clone notes:** A "scrape current instance state" utility; tie to account row by instance index.
- **Complexity:** M · **P0** (needed to map instances→accounts) · UI driver.
- **Failure modes:** Name rendering as image, localized FB, partial page lists.

#### 3.1.6 Auto appeal 180 days suspended
- **What:** Automates Facebook's appeal process for accounts disabled for 180 days (the "appeal" form flow).
- **Implementation:** Navigate to the disabled screen → Appeal → fill form (ID/date of birth/email; "Record profile Date of birth" patch note implies DOB is stored per account and used here) → submit → solve any funcaptcha via 2Captcha ("Auto solve Captcha" / "Fixed submit 180 days").
- **Clone notes:** Store DOB + original email per account; a dedicated appeal flow with captcha hook; log appeal submission receipts.
- **Complexity:** M · **P2** · captcha, DOB storage.
- **Failure modes:** FB constantly changes the appeal flow; many accounts are not appealable; captcha cost.

#### 3.1.7 Auto solve Captcha
- **What:** Solves image/funcaptcha screens during login/appeal/checkpoint.
- **Implementation:** Screenshot captcha → 2Captcha API (`in.php` → poll `res.php`) → paste answer → submit. Supports image captchas and FunCaptcha token flow.
- **Clone notes:** Generic "captcha gateway" module with pluggable provider (2Captcha, CapSolver, etc.); token-type captchas need the page's public key (can be scraped from the FB screen URL/JS). Retry/limit costs.
- **Complexity:** M · **P0** · API keys, screenshot pipeline.
- **Failure modes:** Provider outages, wrong solves, token captchas requiring WebView context.

#### 3.1.8 Check Notification
- **What:** Opens notifications, reads/records them (e.g., "you have new followers", platform alerts).
- **Implementation:** Tap bell icon → OCR list → classify (friend request, comment, system) → record to log/DB.
- **Clone notes:** Notification icon detection via template match (no text). Useful for trigger-based actions.
- **Complexity:** M · **P2** · UI driver, OCR.
- **Failure modes:** Bell icon changes position; notification list virtualization.

#### 3.1.9 Check chat inbox & reply
- **What:** Opens Messenger-style inbox in the FB app, reads unread messages, optionally replies — including **AI replies** ("Active reply chat with AI", "Check-in AI status").
- **Implementation:** Inbox → iterate conversations → OCR last message → generate reply (AI or canned list) → type & send; mark as handled; respect intervals.
- **Clone notes:** "Check-in AI status" = verify AI API key/quota before running. Reply sources: AI prompt, random-caption list, per-conversation rules.
- **Complexity:** L · **P2** · AI gateway, OCR, message-state machine.
- **Failure modes:** FB app inbox ≠ Messenger app; sending too fast triggers spam filters; OCR garbles names/emojis.

#### 3.1.10 Interaction NewsFeed/Videos/Reel
- **What:** Scrolls the feed / watch tab / reels and performs engagement (view time, likes, comments) — the "Active" engine's core.
- **Implementation:** Launch tab → loop: screencap → detect posts (bounding boxes via layout/template) → decide action (probability-weighted: like/comment/follow/none) → scroll → repeat N times per session; per-instance and per-account limits.
- **Clone notes:** This is the most detection-sensitive module. Needs per-account daily caps, random delays (e.g., 3–9 s between actions), stop-on-checkpoint detection (any screen that isn't feed → bail & log).
- **Complexity:** L · **P1** · UI driver, action policy engine.
- **Failure modes:** "We noticed unusual activity" interstitial; infinite scroll stalls; reels autoplay loops.

#### 3.1.11 Randomly React on Post
- **What:** Random like/reactions (like, love, care, haha, wow, sad, angry) on feed posts.
- **Implementation:** Long-press or hover the react row → pick reaction by random index → confirm; vary reaction distribution to look human.
- **Clone notes:** Reaction picker is a horizontal icon strip → template match the 7 reaction icons.
- **Complexity:** M · **P1** · template matching.
- **Failure modes:** FB changed to long-press-only on some versions; reaction bar animations.

#### 3.1.12 Randomly Comment on Post
- **What:** Comments random text (from caption/comment file lists) on posts in feed/reels/videos.
- **Implementation:** Tap comment field → type from random pool (or AI) → post; optional photo attach; emoji occasionally.
- **Clone notes:** Random Caption module feeds this. Comment cooldown + length variance.
- **Complexity:** M · **P1** · Random Caption, UI driver.
- **Failure modes:** Comment field is a sheet with keyboard — coordinates shift; spam filters; captcha on comment (rare).

#### 3.1.13 Add/Confirm Friends
- **What:** Sends friend requests and/or confirms incoming ones.
- **Implementation:** Friend requests screen → Confirm each; "Add friends" flow uses UID/link list (below). Rate-limit (FB hard-limits ~20–25 requests/day for new accounts).
- **Clone notes:** Confirming = simple button taps; adding = navigate to profile → Add Friend → confirm dialog.
- **Complexity:** S–M · **P1** · UI driver.
- **Failure modes:** Daily request caps; "Add Friend" button turns into "Pending" — must detect state.

#### 3.1.14 Check-in Post (With Photo)
- **What:** Creates a status post that includes a location check-in with an attached photo ("Fixed check-ins without AI API" — so location names can come from AI or a list).
- **Implementation:** Composer → photo attach → "Check in" location search → type location (from list/AI) → pick first match → post.
- **Clone notes:** Location search results are a custom list (scroll + OCR); fallback "post without location" if none found.
- **Complexity:** M · **P2** · composer automation + AI/list.
- **Failure modes:** Location suggestions vary by IP geo; no results for invented places.

#### 3.1.15 Create story
- **What:** Posts a photo/video Story, optionally with link file (`filename_link.txt`) and audio ("Fixed post story add audio").
- **Implementation:** Stories camera → gallery pick → edit (text/link sticker via `_link.txt`) → add music (audio flow) → share.
- **Clone notes:** Story composer is heavily custom-rendered; plan template matching for the "Your Story" button.
- **Complexity:** L · **P1** · audio module, link sticker flow.
- **Failure modes:** Story editor UI changes frequently; audio picker is a full-screen scroll.

#### 3.1.16 Check Primary Location
- **What:** Reads/records the account's primary location (from profile "About" or home-town).
- **Implementation:** Profile → About → location section → OCR.
- **Clone notes:** Minor utility; store in account row.
- **Complexity:** S · **P2** · OCR.
- **Failure modes:** Localized labels; empty for some accounts.

#### 3.1.17 Add friends by UID/Link list
- **What:** Bulk friend requests from a file of UIDs/links.
- **Implementation:** For each UID: `fb://profile/<uid>` → wait → Add Friend → back; pause between; stop at daily cap.
- **Clone notes:** Needs URL→profile navigation + "Add" state detection; CSV importer.
- **Complexity:** M · **P2** · UI driver, file import.
- **Failure modes:** UID pages that don't exist; "Add" vs "Message" button variance; caps.

#### 3.1.18 Join groups by ID/Link & Search keyword
- **What:** Joins groups via direct link/ID or by searching keywords and joining results.
- **Implementation:** Search → Groups tab → type keyword → iterate results → Join; or open `fb://group/<id>` → Join; handle join-approval screens ("request sent").
- **Clone notes:** Group search results list = scroll + OCR; keep joined-list to avoid re-joining.
- **Complexity:** M · **P1** · UI driver.
- **Failure modes:** Join button hidden for private groups; "pending" state; FB caps group joins/day.

#### 3.1.19 On/Off Professional mode
- **What:** Toggles the account's Professional Mode (creator tools).
- **Implementation:** Menu → Settings → Professional mode → toggle; confirm.
- **Clone notes:** Simple toggle flow; verify state via screen text.
- **Complexity:** S · **P2** · UI driver.
- **Failure modes:** FB moved this into "Professional dashboard" settings on new versions.

#### 3.1.20 Create Instagram account linked to FB
- **What:** Creates an Instagram account using the Facebook login ("Continue as" flow), installing the Instagram APK first.
- **Implementation:** Install Instagram APK (`installapp`) → open → "Continue with Facebook" → consent → handle username/password creation screens → link.
- **Clone notes:** Needs Instagram APK path config (Devices tab has an Instagram APK selector). Username generation from account data.
- **Complexity:** L · **P2** · APK install, dual-app automation.
- **Failure modes:** FB/IG cross-login screens change; username taken; phone verification.

#### 3.1.21 Set account info
- **What:** Edits profile: add new email, change account name, profile picture, cover photo, workplace/college/city.
- **Implementation:** Profile → Edit profile → per-field flows: name (typing), picture/cover (gallery pick from content folder), workplace/college/city (search autocomplete), email (add-email flow with OTP confirmation).
- **Clone notes:** Each sub-field is a small flow; autocomplete lists are custom — search + tap first result. Provide per-account CSV/JSON templates.
- **Complexity:** M (email L) · **P2** · OTP reader for email confirmation.
- **Failure modes:** FB limits name changes (60 days); email add triggers security check.

#### 3.1.22 Share posts to profile and join groups
- **What:** Shares a given post (by link) to the profile timeline and/or to joined groups.
- **Implementation:** Open post link → Share → "Share now" (profile) / select groups → confirm; per-group limit.
- **Clone notes:** Reuses the "Share post (given links)" page feature — shared engine, two targets.
- **Complexity:** M · **P2** · UI driver.
- **Failure modes:** Share sheet is custom; groups list virtualization.

#### 3.1.23 View posts, story, video, photo
- **What:** Watch/impression generation on given content (Reels, Photos, Stories, Videos, Live) — "Get more engagement on your posts" (View Post feature).
- **Implementation:** Open each link/type → hold view time (configurable seconds) → optionally like/comment → next. For reels: allow N seconds playback.
- **Clone notes:** Needs link list ingestion + per-type dwell times; detect "watched" state.
- **Complexity:** M · **P1** · UI driver.
- **Failure modes:** Reels auto-skip; live ends; stories expire (24h).

#### 3.1.24 Reviews page/profile
- **What:** Reviews a Page or profile (star rating + comment) — "Reviews page/profile" (typo on their site).
- **Implementation:** Page → Reviews tab → rate stars → write review text (random/AI) → post.
- **Clone notes:** Reviews require "recommended" rating UI; some pages disable reviews.
- **Complexity:** M · **P2** · UI driver.
- **Failure modes:** Review button hidden for pages without reviews enabled.

#### 3.1.25 Record IP location
- **What:** Captures each instance's egress IP + geolocation and stores it ("Check IP", "Record IP location"; used for GPS/timezone sync and allow/block lists).
- **Implementation:** `curl ifconfig.co/json` / ip-api inside the instance (via adb shell or host fetch through the same network) → store country/city/coords.
- **Clone notes:** Simple HTTP util; tie to device row. GPS/timezone auto-set from this.
- **Complexity:** S · **P0** (feeds GPS/geo-lock) · network.
- **Failure modes:** IP lookup services rate-limit; VPN region drift.

#### 3.1.26 Auto Shutdown PC (account side)
- **What:** Shuts down the PC after the account batch finishes or at a set time.
- **Implementation:** `shutdown /s /t <seconds>` from the app when all queues empty / stop time reached.
- **Clone notes:** Global scheduler option; cross-check pending jobs before shutdown.
- **Complexity:** S · **P2** · scheduler.
- **Failure modes:** None significant (user opt-in).

---

### 3.2 Facebook Page module (Page Manager)

#### 3.2.1 Auto switch Pages
- **What:** Switches the app from profile mode to a specific Page (or between Pages).
- **Implementation:** Menu → Pages → select page → wait for page toolbar (or "Switch to Page" confirmation). Works with "Auto switch Pages" + "Remove all pages" (right-click utility added in V1.6.16).
- **Clone notes:** Page-switch primitive; needed by every page feature. Verify by checking page name in header.
- **Complexity:** M · **P0** · UI driver.
- **Failure modes:** FB's page-switcher moved into "Professional dashboard" on newer versions; slow page loads.

#### 3.2.2 Auto post on Page/Account/Professional Mode
- **What:** Publish posts as the Page, the personal profile, or in "Professional mode" (creator account). This is the *posting mode selector* for the Posting Engine.
- **Implementation:** Composer identity toggle — ensure the app is in the right identity before composing (switch page/profile/pro-mode).
- **Clone notes:** A precondition step in the posting pipeline: `ensureIdentity(mode)`.
- **Complexity:** M · **P0** · switch primitives.
- **Failure modes:** Identity mismatch → posts go to wrong target; FB mislabels buttons.

#### 3.2.3 Auto Reels post + comment on each post
- **What:** Uploads reels (vertical video, ≤90s) and auto-comments on the just-posted reel ("comment on each post").
- **Implementation:** Create → Reel → pick file from folder → (trim if needed) → caption (file/AI) → hashtags → audio option (mute original) → "AI label" toggle → post → then comment flow on the new post (self-comment for engagement).
- **Clone notes:** Reels composer is the most UI-volatile flow (many patch notes). Build with heavy OCR + template matching; support per-file caption/comment.
- **Complexity:** XL · **P0** · full posting engine, audio, AI label.
- **Failure modes:** "Add reel" button moved; audio picker overlay; FB rejects <3s or >90s videos; "no button" bugs (per patch notes).

#### 3.2.4 Auto Photo post + comment on each post
- **What:** Uploads photo(s) (incl. multi-photo `_M1.._M9`) and comments on each.
- **Implementation:** Composer → photo → pick 1..9 files → caption → share; multi-select naming convention handled by file-scanner; self-comment after.
- **Clone notes:** Simplest post type — build first in MVP. Multi-photo via long-press select in gallery (coordinate-based).
- **Complexity:** M · **P0** · file scanner, composer.
- **Failure modes:** Gallery grid layout changes; "Share" button position (patch note "Fixed photo SHARE button").

#### 3.2.5 Auto Video post + comment on each post
- **What:** Uploads horizontal video posts with caption/comment.
- **Implementation:** Same composer pipeline as reels but "Video" post type; verify LDPlayer codec support ("Check the video files if LDPlayer does not support them" — decode check, e.g., `ffprobe` codec whitelist H.264/AAC; warn/re-encode).
- **Clone notes:** Add a pre-flight video validation module (ffprobe) + optional transcode (ffmpeg) to H.264+AAC.
- **Complexity:** L · **P1** · posting engine + codec validation.
- **Failure modes:** Unsupported codecs → black/processing-stuck; long uploads time out.

#### 3.2.6 Auto Status post (txt file and AI generate)
- **What:** Text-only status posts from a text file or generated by AI.
- **Implementation:** Composer → text mode → read line from `status.txt` or prompt AI ("AI caption if post has no caption — prompt from status post") → post.
- **Clone notes:** AI prompt template config; file rotation (consume one line per run, rotate).
- **Complexity:** S · **P1** · AI gateway.
- **Failure modes:** AI rate limits; empty file.

#### 3.2.7 Auto invite collaborators to post (Reel/Photo/Video)
- **What:** Adds collaborators (tagged co-authors) to posts — "enter collaborators name or ID or profile url, set the number of collaborators".
- **Implementation:** In composer → "Invite collaborators" → search each name → select; limit count.
- **Clone notes:** Search-and-select loop; collaborator list per job.
- **Complexity:** M · **P2** · composer.
- **Failure modes:** Invite UI only appears for eligible posts/accounts; names ambiguous.

#### 3.2.8 Add audio/music to post (mute original audio)
- **What:** Adds music from FB's library to Reel/Story/Video, muting original audio.
- **Implementation:** Composer → Add audio → search/suggested tracks → select → confirm "Original audio muted" state.
- **Clone notes:** Music picker is a full-screen custom list; save "Saved audio" favorites to speed up. Toggle per post type.
- **Complexity:** L · **P2** · audio module.
- **Failure modes:** Music library regional availability; audio overlay breaks flow ("Fixed add music").

#### 3.2.9 Enable AI label
- **What:** Toggles Facebook's "AI-generated content" disclosure label on a post.
- **Implementation:** Composer → "..." → toggle "This content was created with AI" (or the "AI info" toggle on newer FB) → confirm.
- **Clone notes:** Template-match the toggle; remember per-post setting.
- **Complexity:** S · **P2** · composer.
- **Failure modes:** Label toggle only shown when captions/edits look AI-made; moves between FB versions.

#### 3.2.10 Auto add Story + link
- **What:** Posts a Story from page content with an appended link (from `filename_link.txt`).
- **Implementation:** Story composer → photo → add link sticker → type link → share.
- **Clone notes:** Link sticker is draggable — tap sticker, paste link, position optional.
- **Complexity:** M · **P2** · story composer + link file.
- **Failure modes:** Link sticker UI changes; link validation.

#### 3.2.11 Share cross-post to groups
- **What:** After posting, shares the new post to N groups (the post engine's "Groups: automatically share to group, set the number of group to share to").
- **Implementation:** Post → Share → Groups → select up to N joined groups → share. Also the standalone "Share post (given links)" feature.
- **Clone notes:** Keep a cached list of joined groups per account to speed selection; handle "already shared" states.
- **Complexity:** M · **P1** · share sheet automation.
- **Failure modes:** Share sheet group list is virtualized; FB spam filters on cross-posting.

#### 3.2.12 Amazon affiliate link support
- **What:** Injects Amazon affiliate URLs into posts/comments ("Amazon link: Amazon affiliate URL will be automatically placed in the product link during post").
- **Implementation:** Template substitution in comment/caption text; per-account tag IDs.
- **Clone notes:** String templating module: `{amazon_tag}`, `{product_link}` placeholders.
- **Complexity:** S · **P2** · text pipeline.
- **Failure modes:** URL length limits; FB link stripping.

#### 3.2.13 Post When Run
- **What:** All enabled posts execute immediately when Run is clicked.
- **Implementation:** Orchestrator mode: on Run → queue all enabled jobs across instances, respecting batch/waits.
- **Clone notes:** Job queue + worker pool (N batches).
- **Complexity:** M · **P0** · orchestrator.
- **Failure modes:** Resource exhaustion if too many instances launch at once (hence wait settings).

#### 3.2.14 Schedule Post (Time/Weekly)
- **What:** Posts at wall-clock times (24h) and on weekly schedules.
- **Implementation:** Scheduler daemon compares PC time to job times each minute; weekly = day-of-week mask. "Auto Stop at" + shutdown tie-in.
- **Clone notes:** Cron-like engine (or use a lib); persist next-run; handle PC asleep/downtime (catch-up policy).
- **Complexity:** M · **P0** · scheduler.
- **Failure modes:** Clock skew; missed windows after PC wake.

#### 3.2.15 Delete all posts
- **What:** Deletes all posts from a Page one-by-one ("Delete all posts: Reel, Photo, and Video").
- **Implementation:** Page → Activity/Posts log → iterate posts → delete → confirm; repeat until empty ("Fixed deleting all posts").
- **Clone notes:** Needs "Activity log" or "Posts" list navigation + delete confirmation dialog handling.
- **Complexity:** M · **P2** · UI driver.
- **Failure modes:** Confirmation dialogs vary; pagination; slow for many posts.

#### 3.2.16 Join groups by ID/Link
- **What:** (Page side) Joins groups as the Page via link/ID.
- **Implementation:** Same join engine as account-side, but while in Page identity.
- **Clone notes:** Shared join engine with identity parameter.
- **Complexity:** M · **P1** · join engine.
- **Failure modes:** Some groups disallow Page join.

#### 3.2.17 Join groups by Keywords
- **What:** Searches keywords and joins resulting groups.
- **Implementation:** Search → Groups filter → iterate results → join; configurable max joins.
- **Clone notes:** Search results pagination + dedupe.
- **Complexity:** M · **P2** · join engine + search.
- **Failure modes:** Result relevance; FB result layout changes.

#### 3.2.18 Set page info (Profile, Cover, Bio, Email, Address)
- **What:** Updates Page profile pic, cover, bio, email, address.
- **Implementation:** Page → Edit settings → per-field flows with file/gallery pick and text input.
- **Clone notes:** Templated from content folder (profile.jpg, cover.jpg) + CSV.
- **Complexity:** M · **P2** · UI driver.
- **Failure modes:** "About" page layout differs for new pages; address autocomplete.

#### 3.2.19 Bulk edit page post info
- **What:** Edits post settings (caption, hashtags, folder, comment, schedule) across many Pages/LDPlayers at once ("Quick page's post settings by selected multiple pages or LDPlayer").
- **Implementation:** Post Table multi-select → apply field changes to all selected rows.
- **Clone notes:** Pure data-layer feature (no UI automation) — the Post Table already holds all job configs.
- **Complexity:** S · **P1** · data layer.
- **Failure modes:** Conflicting per-page overrides (define precedence).

#### 3.2.20 Captions/Hashtags from file or input (Random)
- **What:** Captions/hashtags pulled from a file (random line) or typed box; hashtags file-only option.
- **Implementation:** Random pick from lines in `caption.txt`/`hashtags.txt`; support `%random%` placeholders.
- **Clone notes:** Random Caption module (see §3.9) feeds this.
- **Complexity:** S · **P0** · text module.
- **Failure modes:** File encoding (UTF-8 BOM), empty lines.

#### 3.2.21 Support post caption from text file (file.txt, file_caption.txt)
- **What:** Per-file captions named like the media file.
- **Implementation:** File scanner resolves `photo.jpg` → `photo.txt` (fallback `photo_caption.txt`) → inline caption.
- **Clone notes:** File naming conventions documented in §2.4; implement the scanner + precedence rules.
- **Complexity:** S · **P0** · file scanner.
- **Failure modes:** Case sensitivity, extension mismatches.

#### 3.2.22 Posted files will be moved or deleted (Option)
- **What:** After successful post, move file to `.posted/` or delete; failures to `.failed/`.
- **Implementation:** Post-run file lifecycle hook.
- **Clone notes:** `posted`/`failed` folder conventions; dedupe before posting (check file already in `.posted`).
- **Complexity:** S · **P1** · file module.
- **Failure modes:** File locks; path length limits.

#### 3.2.23 Check video files if LDPlayer does not support them
- **What:** Pre-flight codec/compatibility validation.
- **Implementation:** ffprobe metadata → whitelist (H.264, HEVC, AAC, MP3) → flag/transcode.
- **Clone notes:** ffmpeg bundled; optional auto-transcode to H.264 baseline.
- **Complexity:** M · **P2** · ffmpeg.
- **Failure modes:** HEVC unsupported on some emulator images.

#### 3.2.24 Pull dashboard info, Monetization Setup
- **What:** Opens the Page Professional dashboard, extracts metrics (followers, reach, monetization eligibility/status).
- **Implementation:** Page → Professional dashboard → OCR key figures + monetization section ("Pull dashboard info, Monetization Setup"; "Check dashboard, extra bonus").
- **Clone notes:** OCR table-to-JSON; snapshot history per page.
- **Complexity:** L · **P2** · OCR + parsing.
- **Failure modes:** Dashboard is canvas-rendered (heavy OCR); metric cards shuffle.

#### 3.2.25 Check Page status, Not Recommended, Flagged, Support Inbox
- **What:** Detects page-level status: "Not Recommended", flagged, and reads Support Inbox messages.
- **Implementation:** Page Quality / Support Inbox screens → OCR status labels; store state per page.
- **Clone notes:** Status classifier over OCR text; alerting on change.
- **Complexity:** M · **P2** · OCR.
- **Failure modes:** Status text localized; screens renamed ("Page Quality" vs "Page Health").

#### 3.2.26 Auto Delete copyright videos (Support Inbox)
- **What:** Reads Support Inbox, finds copyright-strike notifications, deletes the offending videos automatically.
- **Implementation:** Support Inbox → detect "copyright" notifications → open video → delete post → mark resolved.
- **Clone notes:** Notification classifier + delete flow reuse.
- **Complexity:** L · **P2** · classifier + delete.
- **Failure modes:** Missed notifications; FB UI changes in support inbox.

#### 3.2.27 Auto Delete flagged content
- **What:** Deletes posts flagged/restricted ("This post was flagged") automatically.
- **Implementation:** Scan posts list for flagged markers → delete.
- **Clone notes:** Extends delete-all-posts with a flag filter.
- **Complexity:** M · **P2** · delete engine.
- **Failure modes:** Flag markers are icons — template match.

#### 3.2.28 Share post (given links) to the page and join groups
- **What:** Shares arbitrary post links to the Page timeline and/or groups.
- **Implementation:** Page identity → open link → Share → Page / groups → confirm.
- **Clone notes:** Same share engine as account-side.
- **Complexity:** M · **P2** · share engine.
- **Failure modes:** Links of deleted posts fail gracefully needed.

#### 3.2.29 Auto Shutdown PC (page side)
- Same as §3.1.26. **S · P2.**

---

### 3.3 LDPlayer / device module

#### 3.3.1 Auto open/close LDPlayer instances
- **What:** Launches/quits the required instances in order, with boot-wait and between-start delays; "Close all LDPlayer when stop".
- **Implementation:** `ldconsole launch/quit/quitall`; poll boot completion (ADB device online + Facebook process present) before controlling.
- **Clone notes:** Boot-detector: wait until `adb shell pm path com.facebook.katana` succeeds AND window focus is FB. Batch limiter.
- **Complexity:** M · **P0** · console wrapper.
- **Failure modes:** LDPlayer "already running", crash loops, slow boots on weak PCs.

#### 3.3.2 Auto GPS & Timezone
- **What:** Sets virtual GPS and timezone per instance to match its IP.
- **Implementation:** `ldconsole modify --index ...` + GPS injection; `adb shell settings put global auto_time_zone 0` + `setprop persist.sys.timezone <tz>` based on IP geolookup.
- **Clone notes:** IP→geo→(lat,lng,tz) mapping service; apply before login.
- **Complexity:** M · **P1** · IP module + console.
- **Failure modes:** Emulator GPS API quirks; timezone DB lookup.

#### 3.3.3 Auto install/update App via APK
- **What:** Installs/updates Facebook/Instagram APKs (user-provided files copied into the tool's system folder).
- **Implementation:** `ldconsole installapp` (or `adb install -r`); detect installed version (`dumpsys package`).
- **Clone notes:** APK vault: store last-used APKs, hash + version them; rollback to known-good versions when FB update breaks flows.
- **Complexity:** S · **P0** · console wrapper.
- **Failure modes:** Signature mismatch (clone apps), large APK copy time.

#### 3.3.4 Auto arrange LDPlayers
- **What:** Arranges running emulator windows into rows/columns on a chosen screen ("Auto fit" computes per-row count).
- **Implementation:** Win32 window enumeration (FindWindow by title/LDPlayer class) + SetWindowPos; screen index selection.
- **Clone notes:** Windows API via P/Invoke (C#) or `pywin32` (Python). Needs admin rights sometimes (docs say run as admin).
- **Complexity:** M · **P2** · Win32 API.
- **Failure modes:** LDPlayer window class names vary 9 vs 14; virtual display offsets.

#### 3.3.5 Set RAM/CPU/Info to LDPlayers
- **What:** Configures per-instance CPU/RAM and device info.
- **Implementation:** `ldconsole modify --index --cpu --memory --imei ...`; recommended 2 cores/2048MB.
- **Clone notes:** Thin wrapper + defaults + per-group overrides.
- **Complexity:** S · **P1** · console wrapper.
- **Failure modes:** LDPlayer requires instance stopped to modify some fields.

#### 3.3.6 Add/Copy/Remove LDPlayer instances
- **What:** Creates new instances, clones from a template, deletes; "Check Copy From".
- **Implementation:** `ldconsole add --name`, `ldconsole copy --from <index>`, `ldconsole remove`.
- **Clone notes:** Name convention mapping to account IDs (e.g., `fb_<uid>_<n>`).
- **Complexity:** S · **P0** · console wrapper.
- **Failure modes:** Disk space; copy time for big instances.

#### 3.3.7 Backup/Restore LDPlayer instances
- **What:** Full instance backup/restore ("Files must be in format ID_filename.ldbk").
- **Implementation:** `ldconsole backup --index --file <path>`, `ldconsole restore --index --file`.
- **Clone notes:** Backup manifest + folder picker; schedule rotation.
- **Complexity:** M · **P1** · console wrapper + file mgmt.
- **Failure modes:** Large backups (GBs), restore index conflicts.

#### 3.3.8 Network bridging toggle
- **What:** Enable/disable LDPlayer 9/14 network bridging so each instance gets its own IP.
- **Implementation:** LDPlayer setting toggling via registry/config or `ldconsole modify` (LDPlayer exposes network bridge in newer console versions) — patch note "Fixed enable/disable Network bridging".
- **Clone notes:** Detect LDPlayer version; wrap bridge config; verify per-instance IP changed.
- **Complexity:** M · **P2** · console/registry.
- **Failure modes:** Bridge conflicts with VPN; requires emulator restart.

#### 3.3.9 Clear cache / clear FB user data / clear LDPlayer VMs
- **What:** Maintenance: `Clear cache every run counts (100–200)`; clear FB app data; wipe VMs.
- **Implementation:** `adb shell pm clear com.facebook.katana`; delete LDPlayer VM folders; app-level cache clear.
- **Clone notes:** Maintenance scheduler with counters.
- **Complexity:** S · **P2** · adb.
- **Failure modes:** pm clear logs accounts out (must re-login — expected behavior).

---

### 3.4 Email Manager (OTP inbox)

#### 3.4.1 Store encrypted email credentials
- **What:** Vault for Zoho/Yandex emails used to receive verification codes; "passwords saved in encrypted form".
- **Implementation:** DPAPI/AES-encrypted table; app-password flow (IMAP with app password).
- **Clone notes:** Encrypt with Windows DPAPI (`CryptProtectData`) — free, machine-bound.
- **Complexity:** S · **P0** · security module.
- **Failure modes:** DPAPI breaks when user profile migrates — export/import feature needed.

#### 3.4.2 Get code from receiver email
- **What:** Given a receiver address, IMAP-search for the latest FB code email and extract the 6-digit code.
- **Implementation:** IMAP login (Zoho/Yandex app password) → `search` recent from `facebookmail.com` → regex `\b\d{6}\b` → return code; timeout/error states.
- **Clone notes:** IMAP client (MailKit / imaplib); sender whitelist; polling interval; dedupe by Message-ID.
- **Complexity:** S · **P0** · IMAP lib.
- **Failure modes:** 2FA emails arrive as push notifications only; inbox delay; code in HTML image (rare).

---

### 3.5 LD Group & Manage tab

#### 3.5.1 LD grouping
- **What:** Create/delete groups, add/remove LDPlayer instances to groups for quick selection ("Easily select LDPlayer by group").
- **Implementation:** DB relation `ld_group(id,name)`, `ld_instance(group_id)`; filter dropdowns everywhere.
- **Clone notes:** Pure data feature — cheap, high UX value.
- **Complexity:** S · **P1** · data layer.
- **Failure modes:** None.

#### 3.5.2 Manage tab — Add/Backup/Restore LDPlayer, Pull names
- Covered by §3.3.6/§3.3.7/§3.1.5.

---

### 3.6 Post Table & Bulk Edit

#### 3.6.1 Post Table
- **What:** Central grid of every page × post-type × schedule; enable/disable, edit folders/captions, view history ("Table of time, check/select post folder").
- **Implementation:** DataGrid over the jobs DB; per-page allow-posting checkboxes; quick filters.
- **Clone notes:** This is the "control surface" — design it first; everything else writes into it.
- **Complexity:** M · **P0** · UI/data.
- **Failure modes:** Large datasets need virtualization + search.

#### 3.6.2 Bulk Edit
- See §3.2.19.

---

### 3.7 Random Caption

#### 3.7.1 Random text generator
- **What:** Super-random text for captions, comments, replies from files/input, with placeholders.
- **Implementation:** Line pool + random pick; template variables (date, time, emoji, `%name%`); no repeats until pool exhausted.
- **Clone notes:** Simple but central — feeds posting, comments, AI fallback.
- **Complexity:** S · **P0** · text module.
- **Failure modes:** Encoding; empty pools.

---

### 3.8 Logs viewer

#### 3.8.1 Run status logs
- **What:** Per-run record of actions: setup issues, active status, post status ("Record of after-running status").
- **Implementation:** Append-only log DB with levels; live view + export CSV.
- **Clone notes:** Structured log events (account, action, instance, result, duration, screenshot on failure).
- **Complexity:** S · **P1** · data layer.
- **Failure modes:** Log growth — rotate.

---

### 3.9 API / third-party integrations

#### 3.9.1 Captcha API (2Captcha)
- See §3.1.7.

#### 3.9.2 SMS API (SMS-Activate)
- **What:** Rents virtual numbers for SMS verification (login/registration).
- **Implementation:** REST: `getNumber?service=fb&country=..` → receive SMS → poll `getStatus` → extract code; release number after.
- **Clone notes:** Generic SMS-gateway module; per-account phone recording; cost tracking.
- **Complexity:** M · **P2** · HTTP + polling.
- **Failure modes:** Number reuse by FB; service country availability; SMS delay.

#### 3.9.3 AI integration
- **What:** GPT-class captions, status posts, chat replies; "Check-in AI status" (health check on API key before runs).
- **Implementation:** OpenAI-compatible chat completions; prompt templates; cache to avoid repeat calls.
- **Clone notes:** Abstraction layer (OpenAI/Anthropic/local); per-feature prompts; offline fallback to file pools.
- **Complexity:** M · **P1** · HTTP + prompt design.
- **Failure modes:** API cost, rate limits, content-policy refusals.

---

### 3.10 Platform / support features

#### 3.10.1 Any PC that can run LDPlayer; LDPlayer 9/14
- **What:** Broad hardware support; two emulator generations.
- **Implementation:** Feature-detect LDPlayer version at runtime; path config for install folder.
- **Clone notes:** Abstract `IDeviceBackend` interface (LD9 vs LD14 console differences).
- **Complexity:** M · **P0** · abstraction.
- **Failure modes:** Console flag differences between 9/14 (e.g., `--gps`, bridge flags).

#### 3.10.2 Facebook App / Facebook clone
- **What:** Works with official APK or modified/cloned FB apps.
- **Implementation:** Package-name configurable (default `com.facebook.katana`), APK vault.
- **Clone notes:** Keep package name as a per-instance config; flows driven by UI, not package specifics.
- **Complexity:** S · **P1** · config.
- **Failure modes:** Clone apps have different UI versions.

#### 3.10.3 Optional hardware acceleration; turn off screen while running
- **What:** Performance toggle; headless-ish running (turn monitor off while automation continues).
- **Implementation:** LDPlayer renderer setting; app-level: keep instances running with screen off (LDPlayer window can be minimized; ADB still works).
- **Clone notes:** Minimal work — ADB control is independent of the visible window.
- **Complexity:** S · **P2** · settings.
- **Failure modes:** Some flows need screen state (rare).

#### 3.10.4 Run at startup / Auto stop at / Shutdown after
- See §3.1.26 + scheduler.

---

## 4. Clone blueprint for bobplayer/FarmReel

### 4.1 Recommended stack

| Layer | Choice | Why |
|---|---|---|
| Language/UI | **C# (.NET 8 LTS) + WPF** (CommunityToolkit.Mvvm; hosting via `Microsoft.Extensions.Hosting`) | Native Windows, best Win32 interop for LDPlayer window arrangement, DPAPI for secrets, single EXE distribution, DataGrid-heavy UI is a first-class citizen. Python/PySide6 is a faster-prototyping alternative but loses on packaging/AV-false-positives; Electron is viable but weaker at native window control. |
| Device control | `ldconsole.exe` subprocess + **ADB** (`adb.exe` from platform-tools) via `System.Diagnostics.Process` | No SDK dependency; matches Bob Prime's approach. |
| UI automation | `uiautomator dump` XML + **OpenCvSharp** template matching + **Sdcb.PaddleOCR** (ONNX) | Handles FB's hybrid rendering; offline, fast, no in-emulator agents. |
| DB | SQLite (Dapper + FluentMigrator) | Zero-config local; easy backup. |
| Secrets | Windows **DPAPI** + AES-256-GCM for export | Matches "encrypted passwords" promise. |
| Scheduling | In-process cron engine inside the host worker (custom; cron-parser lib) | Full control of batch/wait policies and shutdown. |
| 3rd-party | 2Captcha/CapSolver, SMS-Activate, OpenAI-compatible API, **MailKit** (IMAP), ffmpeg/ffprobe | Direct parity. |
| Logs | Serilog (file + in-app console) + failure screenshots | Structured events; screenshot-on-failure culture. |
| Distribution | `PublishSingleFile` + Inno Setup + signed manifest auto-update | AV-friendly single EXE; update channel. |

### 4.2 Module map

```
bobplayer (FarmReel)
├── Core
│   ├── Orchestrator        — run-loop, batch pool, throttling, stop/shutdown
│   ├── Scheduler           — clock + weekly triggers, catch-up policy
│   ├── FlowEngine          — executes FlowScripts (wait→act→verify) with retries
│   └── JobQueue            — persistence of pending/active jobs
├── Devices
│   ├── LDConsoleAdapter    — ldconsole wrapper (LD9/LD14)
│   ├── ADBAdapter          — shell/tap/swipe/type/screencap/pm/am
│   ├── FingerprintStore    — per-instance device info + replay
│   ├── GPSTimezoneSync     — IP→geo→gps/tz
│   └── WindowArranger      — Win32 arrange
├── Automation
│   ├── ScreenService       — screencap, scaling (dpi), crop
│   ├── ElementFinder       — uiautomator tree + OCR + template match
│   ├── ActionPrimitives    — tap/swipe/type/waitFor/waitGone/scrollTo
│   ├── Flows/              — login, postPhoto, postReel, postVideo, postStatus,
│   │                         postStory, comment, share, joinGroup, addFriend,
│   │                         react, checkLive, appeal, setInfo, createPage,
│   │                         dashboard, deletePosts, inboxReply, ...
│   └── CaptchaGateway      — 2Captcha etc.
├── Domain
│   ├── AccountManager      — accounts, pages, live status, 2FA vault
│   ├── PageManager         — page rows, dashboard snapshots, page status
│   ├── PostingEngine       — file scanner, caption/hashtag resolution, job runner
│   ├── InteractionEngine   — active loop with action policy
│   ├── EmailManager        — IMAP OTP reader
│   ├── SMSGateway          — SMS-Activate client
│   └── AIGateway           — captions/status/replies, health check
├── Data
│   ├── SqliteStore         — schema (§4.3)
│   └── SecretVault         — DPAPI
├── UI (WPF)
│   ├── DevicesTab · AccountTab · PageTab · PostTableTab · ActiveTab
│   ├── EmailTab · GroupsTab · LogsTab · SettingsTab
│   └── FlowRecorder        — record a manual session → FlowScript (huge dev accelerator)
└── Licensing               — machine-bound license (if commercial)
```

### 4.3 Data model (core tables)

```sql
accounts(id, uid, email, password_enc, totp_secret_enc, phone, dob,
         status, live, last_check_at, created_at, extra json)
pages(id, account_id, page_id, name, url, identity_mode, status json,
      dashboard_snapshot json, created_at)
ld_instances(id, name, index, group_id, account_id, package_name,
             device_info json,      -- mac/imei/android_id/model/manufacturer/gps/tz
             cpu, ram, resolution, network_bridge, last_ip json, created_at)
post_jobs(id, page_id, instance_id, post_type,        -- photo|video|reel|status|story
          content_folder, caption_mode, caption_text, hashtag_mode, hashtag_text,
          comment_mode, comment_text, comment_photo, amazon_tag,
          collaborators json, share_groups int, audio_enabled, ai_label,
          schedule_mode,          -- when_run | clock | weekly
          schedule_cron, enabled, last_run_at, next_run_at, state)
interaction_jobs(id, instance_id, action,        -- feed|watch|reels|friends|groups|...
                 limits json, policy json, enabled, last_run_at)
emails(id, email, password_enc, provider, last_code, updated_at)
ld_groups(id, name)
logs(id, ts, level, instance_id, account_id, action, result, message, screenshot_path)
```

### 4.4 FlowScript engine (the differentiator)

Define flows as declarative JSON so they can be **updated without rebuilding the app** (Bob Prime's patch cycle is exactly this problem):

```json
{
  "flow": "post_photo",
  "fb_versions": [">= 480.0"],
  "steps": [
    { "op": "startApp", "pkg": "com.facebook.katana", "fresh": false },
    { "op": "waitFor", "find": {"text": "What's on your mind"}, "timeout": 30 },
    { "op": "tap", "find": {"text": "Photo"}, "alt": {"resourceId": "photo_button"} },
    { "op": "waitFor", "find": {"cls": "gallery_grid"}, "timeout": 20 },
    { "op": "selectFiles", "names": ["{{FILES}}"] },
    { "op": "tap", "find": {"text": "Done"}, "or": {"text": "Next"} },
    { "op": "type", "find": {"text": "Say something"}, "text": "{{CAPTION}}" },
    { "op": "tap", "find": {"text": "Post", "exact": true} },
    { "op": "verify", "find": {"text": "is now on Facebook"}, "or": {"text": "Post shared"}, "timeout": 60 },
    { "op": "commentOnPost", "text": "{{COMMENT}}", "optional": true }
  ]
}
```

Primitives needed: `startApp/forceStop/clearData, waitFor, waitGone, tap, tapIf, longPress, swipe, scrollTo, type (unicode-safe), selectFiles, verify, commentOnPost, back, openLink(fb://), screenshot`.

### 4.5 Key engineering decisions

1. **Coordinate-independent steps:** always anchor to elements/OCR text, never raw pixels — except where template matching is required (reaction strip, AI-label toggle).
2. **Screen-size normalization:** LDPlayer resolution configurable per instance; scale all coords from a canonical 1080×1920.
3. **Unicode input:** `adb shell input text` fails on emoji/special chars — use clipboard (`am broadcast` with `adb shell cmd clipboard`) or `input keyevent` fallback.
4. **Detection of checkpoint/ban screens:** a global "threat screen" classifier runs after every step; on detection → stop instance, screenshot, log, don't retry.
5. **Per-account rate limits as data** (likes/day, comments/day, joins/day, posts/day) — enforced by the Interaction/Posting engines, not ad-hoc.
6. **Version pinning:** remember which FB APK version each flow set was validated against; auto-warn on app update; keep APK vault for rollback.
7. **Failure screenshots** at every failed step → hugely speeds up flow debugging (mirrors Bob Prime's constant "fixed X" patches).
8. **Flow Recorder:** manual-control mode where the user operates one instance while the tool records taps → generates FlowScript skeleton. This is the #1 way to keep up with FB UI changes.

---

## 5. Effort estimate & MVP roadmap

Assumes one experienced engineer (Windows + ADB + OpenCV). FB-version churn adds a permanent 10–20% maintenance tax.

| Milestone | Scope | Effort | Deliverable |
|---|---|---|---|
| **M0 — Proof of concept** | Launch LDPlayer via console, ADB connect, screencap, tap/type into FB app | 1–2 wks | "Drive one instance by hand" harness |
| **M1 — Device layer** | Instance CRUD/copy/backup, CPU/RAM, device-info fingerprint + replay, GPS/TZ sync, APK install, batch launch with waits | 3–4 wks | Devices tab; multi-instance boots reliably |
| **M2 — Account manager** | Credential vault, login (+TOTP/email-OTP/captcha), live/die, pull names, switch profile | 3–4 wks | Account tab; accounts logged in & mapped to instances |
| **M3 — Posting engine v1** | Photo post (+multi-photo), captions/hashtags (file/random), per-file comment, .posted/.failed lifecycle, Post When Run | 3–4 wks | Post Table v1; photo posting works end-to-end |
| **M4 — Scheduling + video** | Schedule (clock/weekly), video post + codec validation, status post + AI caption | 2–3 wks | Full scheduler; video/status posting |
| **M5 — Page manager** | Page switch, dashboard pull, delete posts, join groups (ID/keyword), share post, set page info | 3–4 wks | Page tab |
| **M6 — Reels/story/audio** | Reel post + comment, story + link, add audio, AI label, collaborators | 3–4 wks | Highest-churn flows |
| **M7 — Active engine** | Feed/watch/reels interaction, random react/comment, add/confirm friends, check-in, view posts | 3–4 wks | Active tab |
| **M8 — Hardening** | Email manager, SMS gateway, appeal flow, bulk edit, LD groups, logs viewer, auto-stop/shutdown, arrange windows, licensing | 3–4 wks | v1.0 parity |

**Total: ~24–33 engineer-weeks to functional parity** with Bob Prime's headline features; ongoing maintenance after.

---

## 6. Risk register & compliance

| Risk | Severity | Mitigation |
|---|---|---|
| **Facebook ToS violation** — automation of accounts at scale | High (platform) | Understand this is a gray-market tool; market it for compliance-conscious use only; never promise "ban-proof". |
| **Account loss for end users** — checkpoints, 180-day bans | High (product trust) | Conservative defaults, fingerprint stability, pacing, threat-screen detection, no "unlimited" promises. |
| **FB app UI churn** — flows break monthly | High (engineering) | FlowScript JSON updates, APK pinning + vault, Flow Recorder, screenshot-on-failure culture. |
| **Detection via device fingerprint inconsistency** | High | Deterministic per-instance fingerprint replay; GPS/TZ tied to IP; network bridging/proxy support. |
| **Captcha/SMS/AI API costs & outages** | Medium | Pluggable gateways, caching, budget caps, graceful fallback (skip job → log). |
| **LDPlayer 9 vs 14 divergence** | Medium | Backend abstraction; CI smoke tests on both. |
| **Legal** (bot abuse, affiliate abuse, scraping) | Medium | Product disclaimers; avoid including credential theft / phish-adjacent features; comply with Amazon Associates terms for affiliate injection. |
| **Security of stored credentials** | Medium | DPAPI + AES-GCM, no plaintext, export with encryption passphrase. |

---

## 7. Appendix — feature traceability checklist

Use this to track clone coverage. Status: 🟢 built · 🟡 partial · ⬜ planned.

### Facebook Account
- [ ] Auto switch Profiles
- [ ] Login (UID/Email|Password|2FA)
- [ ] Check Live/Die
- [ ] Create Pages
- [ ] Auto pull account & page name
- [ ] Auto appeal 180 days suspended
- [ ] Auto solve Captcha
- [ ] Check Notification
- [ ] Check chat inbox & reply (AI)
- [ ] Interaction NewsFeed/Videos/Reel
- [ ] Randomly React on Post
- [ ] Randomly Comment on Post
- [ ] Add/Confirm Friends
- [ ] Check-in Post (With Photo)
- [ ] Create story
- [ ] Check Primary Location
- [ ] Add friends by UID/Link list
- [ ] Join groups by ID/Link & Search keyword
- [ ] On/Off Professional mode
- [ ] Create Instagram account linked FB
- [ ] Set account info (email/name/pic/cover/work/college/city)
- [ ] Share posts to profile and join groups
- [ ] View posts, story, video, photo
- [ ] Reviews page/profile
- [ ] Record IP location
- [ ] Auto Shutdown PC

### Facebook Page
- [ ] Auto switch Pages
- [ ] Auto post on Page/Account/Professional Mode
- [ ] Auto Reels post + comment on each post
- [ ] Auto Photo post + comment on each post
- [ ] Auto Video post + comment on each post
- [ ] Auto Status post (txt file and AI generate)
- [ ] Auto invite collaborators to post
- [ ] Add audio/music to post (mute original)
- [ ] Enable AI label
- [ ] Auto add Story + link
- [ ] Share cross-post to groups
- [ ] Amazon affiliate link supports
- [ ] Post When Run
- [ ] Schedule Post (Time/Weekly)
- [ ] Delete all posts
- [ ] Join groups by ID/Link
- [ ] Join groups by Keywords
- [ ] Set page info (Profile/Cover/Bio/Email/Address)
- [ ] Bulk edit page post info
- [ ] Captions/Hashtags from file or input (Random)
- [ ] Caption from text file (file.txt, file_caption.txt)
- [ ] Posted files moved or deleted (Option)
- [ ] Check video files LDPlayer support
- [ ] Pull dashboard info, Monetization Setup
- [ ] Check Page status, Not Recommended, Flagged, Support Inbox
- [ ] Auto Delete copyright videos (Support Inbox)
- [ ] Auto Delete flagged content
- [ ] Share post (given links) to page and join groups
- [ ] Auto Shutdown PC

### LDPlayer
- [ ] Auto open/close LDPlayer instances
- [ ] Auto GPS & Timezone
- [ ] Auto install/update App via APK
- [ ] Auto arrange LDPlayers
- [ ] Set RAM/CPU/Info to LDPlayers
- [ ] Add/Copy/Remove LDPlayer instances
- [ ] Backup/Restore LDPlayer instances
- [ ] Network bridging toggle

### Platform / Support
- [ ] Any PC running LDPlayer
- [ ] LDPlayer 9/14 support
- [ ] Facebook App / Facebook clone
- [ ] Optional hardware acceleration
- [ ] Turn off screen while running
- [ ] Auto Shutdown PC after finish/time
- [ ] Run at startup / Auto stop at
- [ ] LD grouping
- [ ] Bulk edit
- [ ] Random caption
- [ ] Logs viewer
- [ ] Email manager (Zoho/Yandex IMAP OTP)
- [ ] Captcha API (2Captcha)
- [ ] SMS API (SMS-Activate)
- [ ] AI integration (+health check)
- [ ] License system
- [ ] Update/patch mechanism

---

*End of analysis. Next step suggestion: build M0 (single-instance drive harness) to validate the ADB/console approach, then M1 (device layer) — both de-risk the entire clone.*
