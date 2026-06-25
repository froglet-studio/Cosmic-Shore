# WebGL Main-Menu Build — Setup Blueprint

**Branch:** `claude/nifty-bohr-61x85r` (web build lives only here; never merges to the native game).
**Deliverable:** a WebGL build that boots straight into a stripped-down main menu (the real menu look + a
working Settings button) with the lava-lamp autopilot vessel drifting behind it, fully **offline** — no UGS
auth, no Relay, no party/presence, no analytics, no audio.

This is a **WebGL target build**, not a gameplay build. Launching actual games is out of scope (the website
"custom game" integration is a separate, later phase — see §10).

---

## 0. Decisions (locked)

| Decision | Choice |
|---|---|
| Networking on WebGL | **Fully offline shell** — no auth/UGS/Relay/party/analytics |
| Behind-menu visual | **Lava-lamp vessel** via a non-networked spawn path + **toned-down ecosystem** |
| Audio | **Silent for v1** (FMOD guarded off; 0 sound banks exist anyway) |
| Isolation | **Dedicated `Menu_Main_WebGL` scene** + **dedicated branch** |
| Menu scope | **Real menu, stripped** — keep the polished look, nav, and Settings; disable/empty the online screens |

---

## Status

**Landed in code on this branch** (compile-verify on first open in Unity — written without an Editor/compiler here):
- `BootstrapConfigSO.OfflineMenuShell` flag (auto-true on WebGL) — §4.1
- `AppManager` skips `StartAuthentication()` + boots straight to the WebGL menu scene when offline — §4.2/§4.3
- `SceneNameListSO.MainMenuWebGLScene` (`"Menu_Main_WebGL"`)
- `MenuOfflineVesselSpawner.cs` — non-networked lava-lamp spawn + autopilot + `OnClientReady` — §4.4
- `Assets/link.xml` — IL2CPP preservation — §7

**Pending (you, in the Unity Editor):** the §5 scene duplication/strip/wiring (incl. adding the
`MenuOfflineVesselSpawner` + `PlayerSpawner` objects and registering `Menu_Main_WebGL` in the build), the §6
ecosystem `.asset` tuning, the VFX audit, WebGL player settings, package guards, and the build itself.

**Verify in-editor:** `MainMenuController.HandleMenuReady()` / `ApplyMenuVesselClassToHost()` assume a
networked Player — confirm they don't NRE with a single non-networked player; guard behind `OfflineMenuShell`
if they do. Also confirm Script Execution Order: `MenuOfflineVesselSpawner` must subscribe before
`MainMenuController` raises `OnInitializeGame`.

---

## 1. Why a separate scene + branch (and what each actually buys)

- **The branch is what gives isolation.** Every change here — including edits to shared startup code —
  lives only on this branch and never reaches the native game. So we don't need the config-flag gymnastics a
  shared-codebase approach would require; we can edit freely.
- **The scene gives a clean strip surface.** We hard-strip multiplayer/party/instrumentation by *omission*
  (don't place those GameObjects) rather than by guards.
- **What a new scene does NOT save us from:** the menu UI still `[Inject]`s `GameSetting` / `AudioSystem`
  (so we keep a `ContainerScope` + the Bootstrap DI graph), the lava-lamp vessel still needs the non-networked
  spawn path below, and the project-wide WebGL hygiene (§7) still applies. The ongoing cost is **scene drift**:
  `Menu_Main_WebGL` won't auto-track future native-menu changes. Acceptable for a web portal snapshot.

---

## 2. The hard platform facts (why the native flow can't run on WebGL)

1. **WebGL cannot be a Netcode host/server.** Browsers can't open listening sockets — NGO on WebGL is
   *client-only*. The native design is "eager per-user Relay" (every player hosts their own Relay session on
   entering the menu). That design **physically cannot execute on WebGL.**
2. **Transport is UDP/DTLS.** `NetworkManager.prefab` has `m_UseWebSockets:0`; sessions use default
   `.WithRelayNetwork()` (dtls). WebGL has no UDP.
3. **`Menu_Main` loads only via `NetworkManager.SceneManager.LoadScene`, gated on a live host**
   (`AuthenticationSceneController.LoadMainMenuNetworkedAsync` → `WaitForRelayReadyAsync`). No host → the menu
   never loads.
4. **The splash/black overlay is only released by `GameDataSO.OnClientReady`**, which only fires after a
   *networked* Player+vessel spawn (`SceneLoader.FadeFromSplashOnReady`). No networked vessel → permanent
   black screen even if the scene loads.

**The unlock:** nearly all of the UGS/Relay/instrumentation startup hangs off the **`AuthenticationData.OnSignedIn`
event**. If auth never signs in on this branch, `HostConnectionService` never creates a Relay session, the
Friends/Analytics facades never do their UGS work, and no host ever starts — the whole stack stays dormant on
its own. So the offline path reduces to: **(a) don't sign in, (b) load `Menu_Main_WebGL` with a plain scene
load, (c) spawn the lava-lamp vessel non-networked and raise `OnClientReady` to reveal the menu.**

---

## 3. Division of labor

| Area | Who | Where |
|---|---|---|
| Code changes (startup redirect, offline spawner, package guards) | **Claude (this branch)** | `Assets/_Scripts/**` |
| `link.xml`, WebGL build profile/player settings | Claude drafts / you apply in Editor | `Assets/link.xml`, `ProjectSettings` |
| Ecosystem SO tuning | Claude (via `/ecology` protocol) | `Assets/_SO_Assets/Cell Configs/Blob Cell/*` |
| **Scene duplication + GameObject stripping + wiring** | **You, in the Unity Editor** | `Menu_Main_WebGL.unity` |
| **VFX Graph audit** (compute → CPU/Shuriken) | **You, in the Unity Editor** | menu VFX assets |
| **The WebGL build itself + in-browser test** | **You, machine with Unity + WebGL module** | Build Profiles |

> ⚠️ This repo is checked out in a headless container with **no Unity Editor**. Scene editing, the VFX audit,
> and the actual `.wasm` build cannot be produced/verified here — they need a Unity install with the WebGL
> build support module. The code + checklists below are written so you can execute the Editor side as a script.

---

## 4. Code changes (Claude — this branch)

All grounded in current code (line refs as of this writing). Snippets are illustrative — **verify signatures
when you compile in the Editor.**

### 4.1 Offline flag — `BootstrapConfigSO.cs`
Add a flag, exposed as a platform-aware runtime property so WebGL auto-goes offline and you can force it
in-editor for testing:

```csharp
[Header("WebGL / Offline")]
[SerializeField, Tooltip("Boot straight into the offline main-menu shell: no auth, no Relay, no party.")]
bool _offlineMenuShell;

// WebGL can never host Netcode, so always offline there regardless of the flag.
public bool OfflineMenuShell => _offlineMenuShell || Application.platform == RuntimePlatform.WebGLPlayer;
```

### 4.2 Don't sign in — `AppManager.StartAuthentication()` (`AppManager.cs:535-557`)
When `bootstrapConfig.OfflineMenuShell`, skip `authenticationServiceFacade.StartAuthentication()` entirely and
instead publish a synthetic "offline" `AuthenticationData` (signed-out-but-ready) so SOAP readers
(`PlayerDataService`, profile widgets) resolve a value instead of NRE-ing. No `OnSignedIn` ⇒ no host, no party,
no Friends sync, no analytics-on-signin. This single change cascades off most of the networking/instrumentation.

### 4.3 Redirect the boot to the WebGL menu scene — `SplashToAuthFlow.RunSplashFlowAsync()` (`SplashToAuthFlow.cs:95-104`)
Today it *always* routes through the Authentication scene (to start the host). When offline, skip the auth
scene and load the WebGL menu directly with a **plain** transition (not Netcode):

```csharp
if (bootstrapConfig.OfflineMenuShell)
{
    await LoadSceneWithTransitionAsync(_sceneNames.MainMenuWebGLScene); // plain SceneTransitionManager load
    return;
}
// ...existing auth-scene routing unchanged for native...
```

Add `MainMenuWebGLScene` to `SceneNameListSO` and to the WebGL build profile's scene list. This bypasses
`AuthenticationSceneController.LoadMainMenuNetworkedAsync` and its Relay-gated networked load completely.

### 4.4 Non-networked lava-lamp vessel — new `MenuOfflineVesselSpawner.cs`
Reuse the existing single-player spawn (`PlayerSpawner.SpawnPlayerAndShip`, `PlayerSpawner.cs:23-44`) +
replicate autopilot activation (`MenuServerPlayerVesselInitializer.ActivateAutopilot`,
`MenuServerPlayerVesselInitializer.cs:219-233`). Pattern mirrors `MiniGamePlayerSpawnerAdapter`:

```csharp
// Assets/_Scripts/Controller/Player/MenuOfflineVesselSpawner.cs  (sketch — verify APIs in-editor)
public class MenuOfflineVesselSpawner : MonoBehaviour
{
    [SerializeField] PlayerSpawner playerSpawner;
    [Inject] GameDataSO gameData;
    [Inject] SceneTransitionManager sceneTransition;

    void Start()   => gameData.OnInitializeGame.OnRaised += SpawnMenuVessel;
    void OnDisable()=> gameData.OnInitializeGame.OnRaised -= SpawnMenuVessel;

    void SpawnMenuVessel()
    {
        var data = new IPlayer.InitializeData {
            vesselClass   = gameData.selectedVesselClass.Value, // Squirrel (set by AppManager.ConfigureGameData)
            PlayerName    = "Pilot",
            AvatarId      = 0,
            AllowSpawning = true,
            IsAI          = false,
        };

        var player = playerSpawner.SpawnPlayerAndShip(data);
        if (player == null) return;

        gameData.AddPlayer(player);          // sets LocalPlayer, assigns spawn pose
        player.StartPlayer();                // == ActivateAutopilot
        player.Vessel.ToggleAIPilot(true);
        player.InputController.SetPause(true);

        gameData.InvokeClientReady();        // releases the splash via existing OnClientReady wiring
        sceneTransition?.FadeFromBlack().Forget(); // belt-and-suspenders reveal (scene name ≠ "Menu_Main")
    }
}
```

Notes:
- `gameData.InvokeClientReady()` makes `MainMenuController.HandleMenuReady()` run (→ `Ready` state) and lets
  `SceneLoader.FadeFromSplashOnReady` fade the splash — **no `SceneLoader` edit needed.** The explicit
  `FadeFromBlack()` is a safety net because `SceneLoader` only auto-arms the fade when the loaded scene name
  equals `Menu_Main` (`SceneLoader.cs:84-100`); our scene is `Menu_Main_WebGL`.
- Verify `MainMenuController.HandleMenuReady()` doesn't hard-assume a *networked* Player (it calls
  `ActivateLocalPlayerAutopilot` / `SetNonOwnerPlayersActiveInNewClient`). If anything NREs on a single
  non-networked player, guard it behind `OfflineMenuShell`.

### 4.5 Guard mobile-only manager init for WebGL
Wrap `IAPManager`, Ads, Mobile Notifications, Adaptive Performance, and NiceVibrations init in
`#if !UNITY_WEBGL` (these have no WebGL backend; they're inert at best, error spam at worst).

---

## 5. Unity Editor checklist — build `Menu_Main_WebGL` (You)

1. **Duplicate** `Assets/_Scenes/Menu_Main.unity` → `Menu_Main_WebGL.unity` (same folder).
2. **Remove these networked objects** (replaced by the offline spawner or dead without a host):
   - `ServerPlayerVesselInitializer` / `MenuServerPlayerVesselInitializer` (NetcodeHooks object)
   - `ClientPlayerVesselInitializer` (NetworkBehaviour)
   - `NetworkCrystalManager` (NetworkBehaviour, `spawnOnClientReady` — won't fire without a host)
   - Any `NetworkManager`-dependent helpers placed in the scene
3. **Add** an empty GameObject `MenuOfflineVesselSpawner` with the new component (§4.4) + a `PlayerSpawner`
   wired to the player prefab + `VesselSpawner` (copy the wiring from the single-player path).
4. **Strip the online UI** (per "real menu, stripped"): remove/disable party + online panels —
   `ArcadeLobbyList`, `FriendsListPanel`, `OnlineInfoEntry` rows, `PartyInviteNotificationPanel`. Disable or
   empty the screens that need UGS (Store, Leaderboards/`LeaderboardsMenu`, online Profile). Keep **Home**,
   nav, and **Settings**.
5. **Keep:** `ContainerScope` (Reflex), `ScreenSwitcher`, `MainMenuController`, the Settings modal
   (`SettingsModal` + `ModalWindowManager`), camera rig, skybox, and the `Cell` (retuned in §6) +
   `MenuCrystalClickHandler` (optional freestyle toggle).
6. **Confirm DI:** the `ContainerScope` resolves `GameSetting` + `AudioSystem` (both PlayerPrefs/offline-safe).
   These two are the *only* hard DI deps of the Settings path.
7. **Build settings:** in the WebGL Build Profile, scene list = `Bootstrap`, `Menu_Main_WebGL` (drop
   `Authentication`, `Menu_Main`, and all gameplay scenes). Register `Menu_Main_WebGL` in `SceneNameListSO`.

---

## 6. Ecosystem tune-down (Claude, via `/ecology`)

`Menu_Main` runs the **full** Blob-cell ecosystem (it is *not* just a trail). Invariant-safe levers only —
**no caps / TTL / decay** (explicitly forbidden + previously reverted). All edits scoped to the
`Blob Cell` SO assets:

- **Lower `FrenzyEnterVolume` / `FrenzyEnter`** on `Blob Cell Config.asset` (currently `3600 / 57600`) — the
  single biggest lever; drops the steady-state prism count → memory + render + fauna-scan + AOE-job cost all
  at once.
- **Lower fauna `MaxLivePopulation` / `PopulationSize`** (tadpole 4/6, brittlestar 3/5, shark 1/2).
- **Raise `BaseFaunaSpawnTime`** (currently 12s) so seeding ticks less often.
- Optionally **drop species** from `SupportedFloras` / `SupportedFaunas` (e.g. the shark predator) for the
  most aggressive cut.

> Decision needed: tune the **shared** Blob assets (native menu also gets lighter — harmless/beneficial), or
> create **WebGL-specific Blob variants** to leave native untouched? Default: tune shared. Verify with
> `EcosystemPerfProbe` (`[ECOSIM] prisms= volume= colliders= fauna= phase= fps=`).

---

## 7. WebGL hygiene (mixed)

- **`link.xml`** (Claude drafts → `Assets/link.xml`): preserve `CosmicShore.*` Reflex-DI-registered services +
  Newtonsoft.Json DTOs + any reflection-only types. `managedStrippingLevel` for WebGL is Low but reflection-only
  types still strip. There is currently **no first-party `link.xml`** (only `PlayFabSDK/link.xml`, inert).
- **VFX Graph audit** (You): WebGL2/GLES3 has **no compute shaders**. Any GPU-simulated VFX Graph effects on
  the menu (skybox, crystal/vessel FX) must be switched to CPU simulation or legacy Particle Systems.
- **Player settings** (You): WebGL2 graphics API, `webGLThreadsSupport=0` (single-thread — already set),
  reduce texture max sizes, switch compression Gzip→**Brotli** if hosting allows, watch the **2 GB** heap
  ceiling (real OOM risk on mobile browsers for this asset set). Serve over **HTTPS**.
- **Strip dead packages** from the WebGL build profile: Ads, Purchasing, Mobile Notifications, Adaptive
  Performance (+samsung), NiceVibrations native.

### Perf expectations
- **Burst has no WebGL backend** → `[BurstCompile]` falls back to plain managed IL2CPP (≈5–10× slower).
- **Jobs run single-threaded** (no workers). The prism scale/material managers lose parallelism *and* Burst.
- Mitigation is the universal one: keep menu prism mass low (§6 + spawner throttle / fauna cleanup) — **never**
  trail caps. Lean on `AdaptiveAnimationManager` frame-skip. Profile in a real WebGL build, not the editor.

---

## 8. Note: audio is **FMOD**, not Wwise

CLAUDE.md says "Audio: Wwise" — that's stale. `Assets/Wwise` is an empty husk (no code/libs). The real
middleware is **FMOD** (`Assets/Plugins/FMOD`), which *does* build for WebGL but has **0 `.bank` files** in the
project. For v1 we ship **silent**: guard FMOD init off on WebGL. (Worth fixing the CLAUDE.md line + deleting
the Wwise husk in a separate cleanup.)

---

## 9. Verification (in a real WebGL build)

1. App boots → menu paints with **no stuck black screen**, no UGS/Relay console errors.
2. Lava-lamp vessel drifts behind the UI (autopilot).
3. **Settings** opens/closes, sliders/toggles persist (PlayerPrefs), no NRE.
4. `EcosystemPerfProbe` shows bounded prisms/fauna at acceptable fps.
5. Peak browser heap stays well under 2 GB (test on mobile browser too).

---

## 10. Website "custom game" integration (deferred — info needed later)

When we start the website embed, decide:
- **Launch:** iframe + URL params? `postMessage` handshake?
- **Inbound payload:** player id / display name / avatar? a custom-game config blob?
- **Outbound events:** scores / telemetry back to the site via JS interop (`.jslib`)?
- **Hosting target:** your CDN / itch / S3? HTTPS is mandatory (and required anyway if WSS is ever added).
- **Scope:** does the embed stay menu-only, or eventually launch actual games (which reopens the WebGL
  client-only multiplayer question)?

---

## Appendix — key files

| Purpose | File:line |
|---|---|
| Splash→scene routing (redirect point) | `Assets/_Scripts/System/SplashToAuthFlow.cs:95-104` |
| Offline flag | `Assets/_Scripts/System/Bootstrap/BootstrapConfigSO.cs` |
| Auth start (no-op when offline) | `Assets/_Scripts/System/AppManager.cs:535-557` |
| Eager facades (dormant without sign-in) | `Assets/_Scripts/System/AppManager.cs:98-108` |
| Networked menu load (bypassed) | `Assets/_Scripts/System/AuthenticationSceneController.cs:435-510` |
| Splash release on OnClientReady | `Assets/_Scripts/System/SceneLoader.cs:84-100, 179-184` |
| Non-networked vessel spawn (reused) | `Assets/_Scripts/Controller/Player/PlayerSpawner.cs:23-44` |
| Single-player spawn adapter (pattern) | `Assets/_Scripts/Controller/Player/MiniGamePlayerSpawnerAdapter.cs` |
| Autopilot activation (replicated) | `Assets/_Scripts/Controller/Multiplayer/MenuServerPlayerVesselInitializer.cs:219-233` |
| Eager Relay session (stays dormant) | `Assets/_Scripts/Controller/Party/HostConnectionService.cs:443-480` |
| Menu ecosystem cell config | `Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Cell Config.asset` |
