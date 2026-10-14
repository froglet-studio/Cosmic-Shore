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

## 5. Hosting contract (what `/play` expects)

The build is hosted on a CDN. GitHub Pages cannot do it: it sets no custom headers, and files are capped
at 100 MB. The host choice is open in round 2 of the plan artifact. Whatever hosts it must serve:

- `<build_root>/latest.json`, written by the upload step:
  `{ "version", "loader", "data", "framework", "code", "streamingAssets", "bytes" }`. The paths are
  relative to `build_root` (e.g. `Build/1a2b….data.br`); `bytes` is the total first download.
- `Build/` and `StreamingAssets/`, with:
  - `Content-Encoding: br` (or `gzip`) on the compressed files, and **no re-compression** by the CDN
    (`Cache-Control: no-transform`);
  - `Content-Type: application/wasm` for `.wasm(.br)`, `application/javascript` for `.js(.br)`, and
    `application/octet-stream` for `.data(.br)`;
  - CORS: `Access-Control-Allow-Origin: https://www.froglet.games`;
  - long, immutable caching for the hashed files, and `no-cache` for `latest.json`.

`/play` (website repo) fetches `latest.json`, waits for the player's click (that also unlocks audio),
loads the loader script and calls `createUnityInstance` with the four URLs. A new build ships by
uploading the files and rewriting `latest.json`; the website is not redeployed.

## 6. Verification checklist (nobody has run this yet)

Recorded in `Docs/UNITY_VERIFICATION_CHECKLIST.md` under the toybox branch's entry.
