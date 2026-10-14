# The browser Toybox (WebGL)

**What it is.** A WebGL build of Menu_Main's freestyle toybox, played at **www.froglet.games/play**
(website repo `froglet-studio/cosmic-shore-landing-page`, `play.html`). The scope was decided by the owner
on 2026-10-14: freestyle in Menu_Main with **all** the toys, single player, and no arcade, party or
matchmaking. The owner's 2026-10-05 decision that the toybox is **not** cut down to "light toys only"
(`PLATFORM_UNIFICATION.md`) holds here too: no toy is removed for the web.

**Status (2026-10-14): code complete, never built or run.** There is no Unity Editor in the session that
wrote it. The C# compiles against the Unity 6 reference assemblies (`unity_refcompile --config webgl`
and `--config player`, 0 project errors). Everything below that says "does" means "is written to do",
until the checklist at the end has been run.

---

## 1. One define, read one way

The build defines **`FROGLET_WEB_TOYBOX`**. Code reads it through **`WebToybox.IsBuild`**
(`Assets/_Scripts/System/WebToybox.cs`), not through its own `#if`. Every branch therefore compiles
in every configuration, and a broken toybox path cannot hide until the WebGL build runs
(`CONDITIONAL_COMPILATION.md`). The only real `#if UNITY_WEBGL` is around the jslib import.

Differences that are about **WebGL** rather than about the toybox (no compute, no threads, no
NativeShare, no listening sockets) key off the platform or `SystemInfo`, so they also hold for any
other WebGL or low-end build.

## 2. Boot: no online services

| Step | Ordinary build | Browser Toybox |
|---|---|---|
| `AppManager.Start` | network monitor, UGS sign-in, disconnect notice | all skipped |
| First-run privacy overlay | age question + consent | not shown: nothing is collected |
| Authentication scene | cached / anonymous sign-in, username, Relay attempts | straight to the local host (`RunAuthFlowAsync`) |
| `PlanBootNetwork` | may attempt Relay; counts an unwanted fallback | `webToybox: true`: never attempts Relay, never counts |
| Offline host | `UnityTransport` on 127.0.0.1 | **`SinglePlayerTransport`** (NGO 2.13.3), in memory |
| Menu_Main | menu, then the player taps to fly | enters freestyle once at the first menu-ready |

Because UGS never signs in, nothing that starts from `OnSignedIn` runs: Cloud Save, the presence lobby,
Friends, party, leaderboards, UGS Analytics and PostHog. The local data layer still initialises from
`LocalCloudDataCache` (IndexedDB in a browser) through `OfflineModeService.InitializeLocalDataAsync`,
so unlocks and painting progress persist per browser.

`OfflineModeService.UsesInMemoryTransport` is true on a WebGL player and in the toybox build (including
in the Editor, so the toybox path can be played before it is built). Everywhere else the loopback
`UnityTransport` host is unchanged.

## 3. Menu: the toybox and nothing else

- `ScreenSwitcher.IsModalInWebToybox`: Toybox, Toybox configure, Settings and Credits open; every other
  modal is refused, and `WebToyboxTests` enumerates the enum, so a new modal is refused until someone decides.
- Every screen but HOME, and the Arcade, Arena and Mission hub entries, read **Locked: "In the full game"**.
  The layout stays intact, the tap explains itself, and it points at the full game.
- `OfflineUIGate` keeps hiding online-only UI but no longer reveals the offline notice or reconnect button.
  `OnlineStatusIndicator` and `ReconnectButton` hide themselves, and `ReconnectService.ReconnectAsync` refuses.

## 4. Platform work

_Filled in when the platform and rendering work lands on this branch: build entry point, WebGL player
settings, the web PlatformProfile, threads, painting share, FMOD, and the non-compute renderers._

## 5. Hosting: Cloudflare R2 at play.froglet.games

**Decided by the owner (2026-10-14, round 2):** Cloudflare R2 behind `play.froglet.games`, with froglet.games's DNS
moved to Cloudflare; builds come from a Unity Build Automation WebGL target. GitHub Pages cannot host the build:
it sets no custom headers, and files are capped at 100 MB.

### 5.1 What `/play` expects

- `https://play.froglet.games/toybox/latest.json`, written last by `Tools/Build/publish_toybox.py`:
  `{ "version", "loader", "framework", "code", "data", "streamingAssets", "bytes" }`. The paths are relative to
  `toybox/` and point into a versioned folder, `builds/<UBA build number>/Build/…`.
- Each build file carries `Content-Encoding: br` (or `gzip`) when compressed. Content types: `application/wasm`
  for the code (without it the browser will not stream-compile), `application/javascript` for `.js`, and
  `application/octet-stream` for `.data` and the FMOD banks.
- `Cache-Control`: `public, max-age=31536000, immutable, no-transform` on build files, which never change because
  each build has its own folder; `no-cache, no-transform` on `latest.json`. `no-transform` stops the CDN
  re-compressing.
- CORS on the bucket for `https://www.froglet.games`.

In the website repo, `_data/toybox.yml` `build_root: https://play.froglet.games/toybox` turns `/play` on.

### 5.2 One-time setup (a person with the accounts)

**A. Move froglet.games's DNS to Cloudflare (free plan).** Its nameservers today are Google's
(`ns-cloud-a1..a4.googledomains.com`: Google Domains, now Squarespace, or Google Cloud DNS). First **export the
zone** from there; a public lookup cannot see every record. Then add the site in Cloudflare, check its scan
against the export and this list (public records on 2026-10-14), and only then switch the nameservers at the registrar:

| Type | Name | Value | Proxy |
|---|---|---|---|
| A | `@` | `185.199.108.153`, `.109.153`, `.110.153`, `.111.153` (GitHub Pages) | DNS only |
| CNAME | `www` | `froglet-studio.github.io` | DNS only |
| MX | `@` | `1 aspmx.l.google.com`, `5 alt1.aspmx.l.google.com`, `5 alt2…`, `10 alt3…`, `10 alt4…` | — |
| TXT | `@` | `235538` (purpose unknown, likely a verification token: keep it) | — |

Keep GitHub Pages records **DNS only** (grey cloud), so GitHub keeps issuing the site's certificate. After the switch,
check that https://www.froglet.games loads and that mail to and from an @froglet.games address works.

**Add mail authentication while there.** froglet.games has **no SPF, DKIM or DMARC** today (froglet.studio has
SPF), so replies from `support@froglet.games` are likely to land in players' spam:

- TXT `@`: `v=spf1 include:_spf.google.com ~all`
- DKIM: Google Admin → Apps → Gmail → Authenticate email → froglet.games → generate, add the TXT record it gives
  (`google._domainkey`), then press Start authentication.
- TXT `_dmarc`: `v=DMARC1; p=none; rua=mailto:support@froglet.games` (tighten to `quarantine` once reports look clean).

**B. R2.**
1. R2 → Create bucket (e.g. `froglet-play`).
2. Bucket → Settings → Custom domains → connect `play.froglet.games`. Cloudflare adds the record and certificate.
3. Bucket → Settings → CORS policy:
   `[{"AllowedOrigins":["https://www.froglet.games","https://froglet.games"],"AllowedMethods":["GET","HEAD"],"AllowedHeaders":["*"],"MaxAgeSeconds":86400}]`
4. Rules → Compression Rules: turn compression off for `play.froglet.games`. The files are already compressed and
   carry their own `Content-Encoding`.
5. R2 → Manage API tokens → create one with Object Read & Write on that bucket only. Note the Access Key ID, the
   Secret and the account ID.

**C. Unity Build Automation target.** Add a WebGL target using the toybox build entry point (§4). Then create an
API key, and note the org ID, project ID and the new target's ID.

**D. GitHub (game repo).** Settings → Environments → `toybox`: add the secrets `UBA_API_KEY`, `UBA_ORG_ID`,
`UBA_PROJECT_ID`, `UBA_TOYBOX_TARGET`, `R2_ACCOUNT_ID`, `R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY` and `R2_BUCKET`.
Adding required reviewers to the environment makes every publish an approved step.

### 5.3 Publish and roll back

- **Publish:** Actions → *Publish browser Toybox* → Run workflow. Run it with **dry run** first. It downloads the
  newest successful UBA build, uploads it to `toybox/builds/<build>/`, then rewrites `toybox/latest.json`.
- **By hand** (a local build): `python3 Tools/Build/publish_toybox.py upload --build-dir Builds/WebGLToybox --version <name>`
  with the R2 variables set. Add `--dry-run` to see every object and header first.
- **Roll back:** rewrite `toybox/latest.json` with an older build's paths (copy it from the dry-run output of that
  build, or edit `version` and the four paths). Old builds stay in `builds/` until deleted.

Verified 2026-10-14 without the real accounts: the dry run, three refusing inputs (no `Build/`, two `.wasm`
files, a legacy `.unityweb`), and a real upload to a local S3 server (moto), whose stored `Content-Type`,
`Content-Encoding` and `Cache-Control` were read back per object. The UBA download has not run: it needs the
real API key.

## 6. Verification checklist (nobody has run this yet)

Recorded in `Docs/UNITY_VERIFICATION_CHECKLIST.md` under the toybox branch's entry.
