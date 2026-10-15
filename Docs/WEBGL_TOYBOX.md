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

## 0. Fastest path: play it on the site today

The full setup (§5) moves froglet.games's DNS to Cloudflare first. To play sooner, use the bucket's own
`r2.dev` address and move DNS later; only `build_root` changes when you do. `r2.dev` is rate-limited by
Cloudflare and meant for testing, so switch to `play.froglet.games` before announcing the page.

1. **Unity Hub:** add **Web Build Support** to 6000.3.17f1. Check out `claude/webgl-toybox-kg5qx7` (later
   `bleeding-edge`). In Unity, File > Build Profiles > Web > Switch Platform (a long reimport the first time).
2. **Build:** FrogletTools > Build > **WebGL Toybox**. Output: `Builds/WebGLToybox/`.
3. **Try it locally:** `python3 Tools/Build/publish_toybox.py serve --build-dir Builds/WebGLToybox`, open
   http://localhost:8000 in Chrome or Edge. (Opening `index.html` from disk cannot work: the files are
   Brotli-compressed and need the server's `Content-Encoding`.)
4. **Cloudflare (free account; R2 asks for a payment method even on its free tier):** R2 > Create bucket
   (`froglet-play`). Bucket > Settings > **Public Development URL** (the r2.dev subdomain) > Enable; note the
   `https://pub-….r2.dev` address. Settings > CORS policy: the JSON in §5.2 B. R2 > Manage API
   tokens > Create, Object Read & Write on that bucket; note the Access Key ID, the Secret and the account ID.
5. **Upload:** `pip install boto3`, set `R2_ACCOUNT_ID`, `R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY`, `R2_BUCKET`, then
   `python3 Tools/Build/publish_toybox.py upload --build-dir Builds/WebGLToybox --version 1`.
6. **Check the host:** `python3 Tools/Build/publish_toybox.py check --root https://pub-….r2.dev/toybox` must end
   "All good". It reads every file the way `/play` will, from the www.froglet.games origin.
7. **Website:** in `cosmic-shore-landing-page`, set `_data/toybox.yml` `build_root: https://pub-….r2.dev/toybox`, and
   merge the website PR. GitHub Pages republishes in a minute or two; www.froglet.games/play shows the launch button.

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

**Build.** `Editor/Build/WebToyboxBuild.cs`: FrogletTools > Build > WebGL Toybox (+ Development), or
`-executeMethod CosmicShore.Editor.WebToyboxBuild.Build`. Exactly Bootstrap, Authentication and Menu_Main, with
`FROGLET_WEB_TOYBOX` passed for that build only (`extraScriptingDefines`), to `Builds/WebGLToybox/`, plus a
`build_manifest.txt`. It refuses unless the editor is on Web, and sets the editor's Development checkbox to match the
build, because the release guards exempt any build while it is ticked. Unity Build Automation calls
`WebToyboxBuild.PreExport` instead (batchmode only): it sets the three scenes and appends the define. The release
guard checks the scenes a scripted build declares (`UnityPipelineReleaseGuard.DeclareBuildScenes`) as well as Build
Settings, and `BuildSceneListReconciler` stands down in a toybox-defined editor so UBA's define reload cannot re-add
every arcade scene.

**Player settings (Web only).** Brotli with the decompression fallback off (the host sets `Content-Encoding`); hashed
file names (immutable caching); 256 MB initial and 4 GB maximum memory (boot alone passes 256 MB); exceptions
"without stack trace", which keeps IL2CPP's null and bounds checks (explicit-only drops them, so a null dereference
would read garbage instead of reaching a catch); template `PROJECT:FrogletToybox`. No threads, no debug symbols.

**Template.** `Assets/WebGLTemplates/FrogletToybox/index.html`: a canvas and Unity's loader, used when a build is
opened on its own (`serve`, a UBA share link). It posts `progress`, `ready`, `warning` and `error` to a parent page
and exposes `window.frogletUnity`. www.froglet.games/play does not use it: it reads `latest.json` and calls the
loader itself.

**Web profile.** `PlatformProfile_Web` (in `Resources/PlatformProfiles`) is what any WebGL player runs, whatever its
tier; the tier itself is unchanged, so input is unaffected. It keeps every toy and the no-trail-cap law. It turns the
first-run heuristic off (a browser hides the real hardware), starts at the Low preset with a 2.1 MP budget, FXAA and
60 fps, uses the baked sky, and takes MobileLow's CPU strips: the freestyle pen waits at 10,000 and resumes at 9,700,
a smaller Wanderway belt (8 scenes of 600 prisms) with its lifeforms and crystals, menus off while flying, no
cytoplasm. The reason is CPU: wasm runs single-threaded, without Burst, at roughly half native speed.

**Threads.** A WebGL player has none. `CrystalHullFusion`'s fallback solve runs inline there (at vessel spawn, and only
for a missing or stale bake); `TextureScale` scales in one slice. Swarm, Substrate and the NCA creatures already ran
their sims inline on WebGL (`SimulateOffMainThread` is ignored there).

**GPU: no compute on WebGL2.** Swarm members and substrate agents draw through `SwarmMemberFallbackDraw`: the same
80-byte instances as five per-instance float4 rows, 128 per `RenderMeshInstanced` (10 KB, inside the 16 KB uniform
block), through `SwarmMemberInstancedFallback.shader` (SM 3.5), which runs the same pose and shade code. The
renderers choose it from `SystemInfo`, not the define; `memberdraw sm35|auto` forces it in the editor for an A/B
(`SWARM_FAUNA.md` §14.8, `SUBSTRATE_FAUNA.md` §5.1). VFX Graph needs compute too: `ArcLightningFallback` draws the
arc lightning's two strands as LineRenderers (the Rhino's arc is switched off on every platform today, so nothing
shows there yet).

**Prism animation without Entities Graphics.** Entities Graphics needs compute too, so in a browser
`PrismRenderService` stays off and every prism draws through its MeshRenderer. The clock-material stamps (grow-in,
colour lerp, sway, shield bloom, jiggle, turret/builder/plough flight, cell-swap suction) go to each prism's
MaterialPropertyBlock instead of its entity, through `PrismMeshRendererStamps`, and deaths (explosion, suction,
shield shards) are drawn by a capped pool of MeshRenderer pieces (`PrismMeshRendererDebris`, 96 at once,
`PrismRenderConfig.meshRendererDebrisCap`). Same values, same shader properties, one write at the stamp and one at
the end; a settled prism drops its block so it rejoins the SRP Batcher. The choice comes from the device probe
(`PrismRenderService.UsesMeshRendererStamps`), not the define; `prismpath mpb` forces it in the editor for an A/B,
and `prismpath` reports how many prisms hold a block and how many debris pieces are live
(`PRISM_ANIMATION.md` §4.11). Not covered: the Rhino slice (a blade kill explodes instead) and ThreatGrove's danger
glow colour.

In-browser checks (a Development WebGL build, F7 console): prisms laid by the vessel bloom in rather than pop;
a domain change and a fresh Wanderway scene fade their colours; living flora and fauna mass sways with its limbs and
a skeleton stops; a shield blooms on and sheds shards when it breaks; ramming or blasting prisms shows flying,
fading debris and fauna feeding shows suction toward the eater; the console carries no `[PrismClock] STRICT MODE`,
`[PrismDebris] ... batch spawn failed` or `[PrismFactory] ... declined` lines; `prismpath` shows the block count
falling back after laying stops (only swaying mass should keep one), and frame time with a full freestyle cell is
not worse than before.

**Painting SHARE** downloads the viewer `.html` (`Plugins/WebGL/FrogletDownload.jslib`); a WebGL player has no share
sheet, and its files live in IndexedDB.

**Audio (FMOD).** The WebGL node inherits the Default settings, like every shipping platform, and Unity 6 links
`html5/lib/3.1.39`. Banks load asynchronously from StreamingAssets, so the toybox waits on "Loading sound…" (up to
90 s) before the menu: an event created before its bank arrives fails once and stays silent. FMOD resumes the
browser's audio only on click and touch; `WebAudioResume` + `FrogletAudioResume.jslib` add a key press. Open idea:
FADPCM web banks, since Vorbis decodes on the main thread.

**Display.** `GraphicsSettingsApplier.ApplyDisplay` skips WebGL: the page sizes the canvas, and a full-screen mode
there would leave the page on the first click. The page's full-screen button owns that.

**Controllers.** The game reads `Gamepad.current` and follows the last device used (`InputDeviceActuation`); on WebGL
the Input System fills it from the browser's Gamepad API (`WebGLGamepad`, standard mapping: Xbox and PlayStation
pads). No device filter is set, so nothing else is needed. A browser reveals a pad only after one of its buttons is
pressed with the page focused, which `/play` tells the player, and it shows "Controller connected" when it is.
Rumble does nothing in a browser. Menu button glyphs (`ControllerButtonPress`) take the PlayStation set when the
browser's id names Sony's vendor id (054c) and the Xbox set otherwise, and are re-picked whenever `Gamepad.current`
changes, so a pad revealed after the menu opened still gets them.

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

**C. Unity Build Automation target.** Add a target: platform **Web (WebGL)**, Unity **6000.3.17f1**, branch
`bleeding-edge` (or `claude/webgl-toybox-kg5qx7` until it merges), Advanced > **Pre-Export Method**
`CosmicShore.Editor.WebToyboxBuild.PreExport`, Development off. Then create an API key, and note the org ID, project
ID and the new target's ID.

**D. GitHub (game repo).** Settings → Environments → `toybox`: add the secrets `UBA_API_KEY`, `UBA_ORG_ID`,
`UBA_PROJECT_ID`, `UBA_TOYBOX_TARGET`, `R2_ACCOUNT_ID`, `R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY` and `R2_BUCKET`.
Adding required reviewers to the environment makes every publish an approved step.

### 5.3 Publish and roll back

- **Publish:** Actions → *Publish browser Toybox* → Run workflow. Run it with **dry run** first. It downloads the
  newest successful UBA build, uploads it to `toybox/builds/<build>/`, then rewrites `toybox/latest.json`.
- **By hand** (a local build): `python3 Tools/Build/publish_toybox.py upload --build-dir Builds/WebGLToybox --version <name>`
  with the R2 variables set. Add `--dry-run` to see every object and header first.
- **Check a host:** `python3 Tools/Build/publish_toybox.py check --root <build_root>` reads `latest.json` and every
  build file the way `/play` does and names anything that would stop it loading.
- **Play a local build:** `python3 Tools/Build/publish_toybox.py serve --build-dir Builds/WebGLToybox`, then
  http://localhost:8000.
- **Roll back:** rewrite `toybox/latest.json` with an older build's paths (copy it from the dry-run output of that
  build, or edit `version` and the four paths). Old builds stay in `builds/` until deleted.

Verified 2026-10-14 without the real accounts: the dry run, three refusing inputs (no `Build/`, two `.wasm`
files, a legacy `.unityweb`), and a real upload to a local S3 server (moto), whose stored `Content-Type`,
`Content-Encoding` and `Cache-Control` were read back per object. The UBA download has not run: it needs the
real API key.

## 6. Verification checklist (nobody has run this yet)

Recorded in `Docs/UNITY_VERIFICATION_CHECKLIST.md` under the toybox branch's entry.

## 7. Follow-ups

- **Release guards trust the Development checkbox** (all three: `UnityPipelineReleaseGuard`, `CreditsReleaseGuard`,
  `WeeklyChallengeTestModeBuildGuard`). A scripted release build made while it is ticked skips them.
  `WebToyboxBuild` works around it by forcing the checkbox to match; `CosmicShoreBuildPipeline` does not. Filed as a
  separate task.
- **Display settings in a browser.** The settings modal still shows display mode and resolution, which do nothing
  on WebGL now (`ApplyDisplay` returns). Hide those rows when `Application.platform == WebGLPlayer`.
- **FMOD on the web.** The toybox waits up to 90 s for the banks (SFX.bank is ~37 MB, downloaded after the build
  itself). Measure the real wait; if it is long, build FADPCM or smaller web banks, or load SFX.bank lazily.
- **A Web Build Profile asset** (Build Profiles > Add > Web, the three scenes, `FROGLET_WEB_TOYBOX`) would let anyone
  build from the Build Profiles window; nothing in the repo is a donor for its schema, so it was not hand-authored.
- **The Rhino's arc lightning** is inactive with its renderer off on every platform, so `ArcLightningFallback` has
  nothing to stand in for yet. Incompleteness, not a defect: decide whether the arc should show.
- **Move off `r2.dev`** once DNS is on Cloudflare (§5.2 A): connect `play.froglet.games` to the bucket and change
  `build_root`. `r2.dev` is rate-limited and meant for testing.
