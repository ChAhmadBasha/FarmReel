# Flow Tuning & Validation Checklist

How to take the FarmReel codebase from "compiles" to "reliably automates Facebook" on your
own hardware. Facebook changes its app UI frequently, so this checklist is the recurring
maintenance procedure — not a one-time setup.

---

## 0. Prerequisites

- [ ] Windows 10/11 PC that can run LDPlayer (8 GB+ RAM recommended; 16 GB for >4 instances)
- [ ] **.NET 8 SDK** + **Visual Studio 2022** (or `dotnet` CLI)
- [ ] `dotnet restore && dotnet build FarmReel.sln -c Release` — **fix all compile errors first**
- [ ] Run `FarmReel.exe` **as Administrator** (LDPlayer window arrangement, OpenVPN, DPAPI)
- [ ] Install LDPlayer 9 or 14 (or MuMu 12 / a real Android phone with USB debugging)
- [ ] Download the **Facebook APK** you intend to use (official `com.facebook.katana` or a clone)
- [ ] adb reachable: in FarmReel **Settings → Check Environment** → all paths OK, ADB devices listed

## 1. Environment validation (Settings tab)

- [ ] `ldconsole.exe` path points to your LDPlayer install (`LDPlayer9\ldconsole.exe`)
- [ ] `MuMuManager.exe` path (if using MuMu)
- [ ] `adb.exe` — use the one bundled with LDPlayer or platform-tools
- [ ] `ffprobe.exe`/`ffmpeg.exe` — needed for video codec checks
- [ ] `openvpn.exe` (if using VPN profiles)
- [ ] Captcha / SMS / AI API keys entered
- [ ] License key activated (server or offline), quota shown

## 2. Device setup (Devices tab)

- [ ] Add one LDPlayer instance per account (index 0-based); set CPU/RAM
- [ ] **Generate Fingerprint** for each instance and save (never share fingerprints between accounts)
- [ ] Install the FB APK via **Install APK…**
- [ ] **Scan ADB** — instance shows online
- [ ] Start the instance, confirm Facebook launches to the login screen manually (sanity check)

## 3. Core flow validation (do these first — everything depends on them)

Log into a **test account** and validate each flow through **Flows → Load → Validate → Save
override** if selectors fail. Record the FB app version (Settings → About on the device).

| # | Flow | What to verify | Common failure & fix |
|---|---|---|---|
| 1 | `login` | Opens FB, types email+password, handles 2FA, lands on feed | FB moved login fields; update the `waitFor`/`type` text regexes. `input text` can't type some chars — use the clipboard path. |
| 2 | `pull_names` | Reads profile name text | Profile renders in canvas (no uiautomator nodes) → enable OCR engine and re-record with the Recorder. |
| 3 | `post_photo` | Gallery → select photo → caption → Post → confirmation | "Share" button moved; fix the `^post$\|share` regex or record a tap. |
| 4 | `post_reel` / `post_video` | Composer → media → caption → Post | Reels composer is the most volatile; expect to re-tune every FB update. |
| 5 | `post_status` | Text composer → Post | Check the "What's on your mind" entry point per FB version. |
| 6 | `interact_view` | Feed scroll works | Swipe coordinates assume 1080×1920; adjust for your resolution/DPI. |
| 7 | `comment_on_post` | Comment sheet opens, text typed, sent | Comment field is a bottom sheet — coordinates/selectors change often. |
| 8 | `check_live` (HTTP) | UID → LIVE/DIE/CHECKPOINT | This is HTTP-based, usually stable; verify the account's UID is correct. |

## 4. Per-feature checklist (when you need the feature)

### Accounts
- [ ] `login` with 2FA (TOTP secret stored; email-OTP via Emails tab; SMS via gateway)
- [ ] `manual_login` + `backup_profile` (session capture) — validates profile backup works
- [ ] `check_notifications`, `reply_inbox` (chat reply)
- [ ] `add_friend` / `confirm_friend` (uid/link lists)
- [ ] `join_group` / `leave_group` / `post_to_group` / `groups_suggestions`
- [ ] `checkin_post`, `create_story` (incl. `*_link.txt` story link)
- [ ] `create_page`, `set_account_info`, `professional_mode`
- [ ] `appeal`, `unlock282`, `verify_novery`, `reg_full` (+ confirm) — **start with one test account; these are the most aggressive flows and the most likely to trigger checkpoints**
- [ ] `reviews`, `check_primary_location`, `watch_live`, `view_story`

### Pages
- [ ] `page_dashboard` — reads followers/reach/monetization/waitlist correctly
- [ ] `delete_all_posts` (loop until empty), `delete_page`
- [ ] `auto_delete_copyright` (Support Inbox)
- [ ] `share_post`, `set_page_info`, bulk edit

### Posting engine
- [ ] Captions: per-file `photo.txt`, `photo_caption.txt`, random caption file, AI caption
- [ ] Comments: `photo_comment.txt`, random file, comment-with-photo (`.comment_photos/`), delete-after-use
- [ ] Multi-photo `photo_M1..M9` + `photo_M.txt`
- [ ] Lifecycle: successful files → `.posted/`, failures → `.failed/` (toggleable)
- [ ] Schedule: WhenRun / Clock `HH:mm` / Weekly `mon 09:00`, daily limits, auto-stop, shutdown PC

### Active engine
- [ ] `feed`, `reels`, `watch`, `live`, `story`, `friends`, `groups` kinds each produce sane action plans
- [ ] Per-account caps respected (likes/comments/follows/shares per run)
- [ ] Random delays (3–12 s) applied; checkpoint/282 screens detected and stop the instance

## 5. How to fix a broken flow (the important workflow)

1. **Flows** tab → select the flow → **Load** → see the JSON.
2. Identify the failing step (Logs tab shows "Flow 'X' failed at step 'Y'").
3. Two options:
   - Edit the JSON directly (regex/text), **Validate**, **Save override** (goes to `%LocalAppData%\FarmReel\flows\`).
   - **Recorder** tab → start → operate the emulator manually → click preview to record taps → **Save** → it becomes the override.
4. Re-run; check Logs; iterate. No rebuild needed for flow changes.

## 6. Validation log template (keep per FB version)

```
FB app version : 4xx.x.x.xx (from device Settings)
LDPlayer ver   : 9.x / 14.x      Device: LD_1 (index 0)
Date           : YYYY-MM-DD      Tester: ____

flow            status   notes
login           [PASS/FAIL]
post_photo      ...
post_reel       ...
post_video      ...
post_status     ...
post_story      ...
comment_on_post ...
interact_view   ...
join_group      ...
page_dashboard  ...
reg_full        ...   (test account only)
unlock282       ...
```

## 7. Production ops

- [ ] Back up `%LocalAppData%\FarmReel` (DB, profiles, cookies, device_info) daily — or enable auto-backup
- [ ] Pin the FB APK version your flows were validated against; keep the APK in the vault for rollback
- [ ] After every FB app update: re-run section 3 first, then the features you actively use
- [ ] Watch the Logs tab for `locked282` / `checkpoint` classifications — stop and remediate before continuing
- [ ] Test the update channel: stage a `FarmReel.exe` in the server's `update-files/`, click **Check for Updates** in Settings
- [ ] Run `dotnet test tests/FarmReel.Tests` after any code change
