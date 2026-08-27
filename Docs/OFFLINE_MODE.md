# Offline / Single-Player Fallback

**Status: IMPLEMENTED (2026-08-26).** The boot fallback, the offline local host, the local
last-known-good data cache, the online-only UI gating and the in-place reconnect are in. §6
records what shipped, §7 the reconnect design. Short version: `Docs/OFFLINE_MODE_SUMMARY.md`.
§§0–3 are kept as the diagnosis of the world before the fix — the "today" they describe is the
pre-implementation state. Read this before touching any offline code.

---

## 0. The one-line diagnosis

> **Nothing in the project ever calls `NetworkManager.StartHost()`.** The Netcode host only ever
> comes up as a *side effect* of the UGS SDK creating a Relay-backed session. No Relay, no host.
> No host, no vessel — because every vessel in the game spawns through `Player.OnNetworkSpawn`.

Verify it yourself:

```
grep -rn "StartHost" Assets/_Scripts --include=*.cs
```

Four hits, **all of them comments**. The host is started by
`MultiplayerService.CreateSessionAsync(opts.WithRelayNetwork())` inside
`PartySessionService.CreateAsync` — the SDK starts NM for us. That is the entire reason offline
play is impossible: the game has no way to be a host on its own machine.

One of those four hits is worse than a missing feature — it is a **claim that the feature exists**:

```csharp
// MultiplayerSetup.cs:164-167
// Host startup is delegated to HostConnectionService which creates a
// Relay-backed party session (via CreateSessionAsync + WithRelayNetwork).
// AuthenticationSceneController.EnsureHostStartedAsync provides a local
// host fallback if the Relay allocation times out.
```

`EnsureHostStartedAsync` **does not exist anywhere in the codebase**. The local-host fallback is
documented, relied on in a comment, and absent. Anyone reading that comment concludes offline is
handled.

---

## 1. What actually happens today when you launch with no network

The boot chain and where each link breaks:

| # | Step | Offline behaviour |
|---|---|---|
| 1 | `AppManager.Start` → `AuthenticationServiceFacade.StartAuthentication` | `UnityServices.InitializeAsync()` may succeed; `SignInAnonymouslyAsync()` **throws** |
| 2 | `authenticationData.OnSignedIn` | **never raised** — this is the trunk everything else hangs off |
| 3 | `HostConnectionService.HandleSignedInEvent` (subscribed to `OnSignedIn`) | **never runs** → no presence lobby, no party session, **no NM host** |
| 4 | `UGSDataService.HandleSignedIn` (subscribed to `OnSignedIn`) | **never runs** → `IsInitialized` stays false, no repository ever loads |
| 5 | `MultiplayerSetup.OnAuthenticationSignedIn` (subscribed to `OnSignedIn`) | **never runs** → Netcode callbacks (`ConnectionApproval`, `OnClientDisconnect`, `OnTransportFailure`) are **never wired** |
| 6 | `SplashToAuthFlow` | ✅ degrades correctly — waits out `authWaitTimeout`, routes to the Auth scene anyway |
| 7 | `AuthenticationSceneController.RunAuthFlowAsync` | ✅ degrades correctly — `safetyTimeout` (10 s) fires, shows the offline notice, calls `NavigateToMainMenu()` |
| 8 | `AuthenticationSceneController.LoadMainMenuNetworkedAsync` | ❌ **HARD BLOCK — the player never gets past the boot splash** |

### 1.1 The hard block, exactly

```csharp
// AuthenticationSceneController.cs — LoadMainMenuNetworkedAsync
for (int attempt = 1; attempt <= 3 && !networkReady; attempt++) {
    ...CancelAfter(15s);
    await WaitForRelayReadyAsync(linkedCts.Token);        // waits for OnHostConnectionEstablished + NM.IsListening
    ... on failure: await hcs.EnsurePartySessionAsync();  // throws offline
}

if (!networkReady) {
    bootStatusEvent?.Raise(new BootStatusRequest(BootStatusMode.Retry, "Could not connect. Tap retry."));
    await WaitForRelayReadyAsync(ct);                     // ← NO TIMEOUT. Unbounded wait.
}

NetworkManager.Singleton.SceneManager.LoadScene(menuScene, LoadSceneMode.Single);
```

Offline this is **45 seconds of retries followed by an infinite await**. `Menu_Main` is loaded
through `NetworkManager.SceneManager`, so the scene load is *unreachable* without a host. The
retry button is the only affordance and it can never succeed. **The game is unplayable — not
degraded, bricked.**

### 1.2 It would still be broken one layer down

Even if you patched step 8 to load `Menu_Main` locally, you would land in an empty world:
`ServerPlayerVesselInitializer` is driven by `Player.OnNetworkSpawn` → `OnPlayerNetworkSpawnedUlong`.
With no host, no `Player` NetworkObject spawns, so no vessel spawns, so there is nothing to fly and
no camera target. `SceneLoader.LoadSceneAsync` already has a local-load fallback for exactly this
reason — and it produces that empty world.

**This is why the fix is not "bypass Netcode when offline".**

---

## 2. The design: offline is a LOCAL HOST, not "no netcode"

> **Offline mode starts `NetworkManager` as an ordinary host on `127.0.0.1` with the default
> `UnityTransport`, and changes nothing else.**

Host == server == client on one machine. Every downstream system — the spawn chain, `ClientRpc`,
`NetworkVariable`, `NetworkManager.SceneManager.LoadScene`, AI backfill, the domain-scored
minigames — runs **byte-identically to a solo online session**. One code path, no `if (offline)`
branches scattered through gameplay.

### 2.1 This is already possible with zero prefab changes

`Assets/_Prefabs/CORE/NetworkManager.prefab` is *already* configured for it:

| Field | Value | Meaning |
|---|---|---|
| `m_ProtocolType` | `0` | **UnityTransport**, not Relay. Relay data is written at runtime by the session SDK. |
| `Address` / `Port` | `127.0.0.1` / `7777` | loopback |
| `ServerListenAddress` | `0.0.0.0` | binds locally |
| `ConnectionApproval` | `1` | approval callback path is live |
| `PlayerPrefab` | assigned | player object spawns on host start |

`NetworkManager.Singleton.StartHost()` works offline **today**. Nothing calls it.

### 2.2 The one real trap

A local `StartHost()` after a previous Relay session will inherit **stale Relay transport data**.
The offline entry point must reset the transport before starting:

```csharp
transport.SetConnectionData("127.0.0.1", 7777, "0.0.0.0");
// and clear any relay server data the SDK wrote
```

This belongs in `INetworkTransitionService` (which already owns `ShutdownAsync`), as a new
`StartLocalHostAsync(float timeoutSeconds, CancellationToken ct)`. It is the mirror of the shutdown
primitive that already exists there, and it keeps NM lifecycle in the one class that owns it —
`PartySessionService`'s header explicitly states it must not touch NetworkManager.

### 2.3 One authoritative flag

Add a single readable state — `SessionMode { Online, Offline }` — written by exactly one owner
(the same single-writer discipline as `AuthenticationData` and `HostConnectionData`), exposed as
SOAP so UI reads it without a direct reference. Every gate below reads that one flag rather than
each surface re-deriving "am I offline?" from `Application.internetReachability`,
`AuthenticationService.Instance.IsSignedIn`, or `NetworkManager.IsListening` — three different
questions that are currently used interchangeably.

**Decision point:** `AuthenticationSceneController`, at the two places that already know the
network failed — the `safetyTimeout` branch (which already shows an offline notice and then walks
into the infinite wait) and the exhausted-retry branch in `LoadMainMenuNetworkedAsync`.

### 2.4 The retry surface stays, and becomes honest

Today's "Could not connect. Tap retry." is a dead end. Under this design it becomes a *choice*:
enter offline mode now (local host, menu loads, everything single-player works), and keep the
retry available so a player who gets their connection back can re-enter online mode. The upgrade
path is a full re-boot of the party layer, not an in-place promotion — see §5.

### 2.5 Relationship to the LOCKED party design

`Docs/PartySystem/ARCHITECTURE.md` locks **EAGER per-user Relay**: every player hosts their own
Relay-backed party session on entering `Menu_Main`, and LAZY / on-first-invite creation must not
return.

**This design does not touch that rule.** Offline mode is not lazy Relay creation — it is the
absence of Relay entirely, chosen only when Relay is provably impossible. When online, the eager
session is created exactly as it is today. The offline path never creates a Relay session late; it
never creates one at all. ⚠ Because it neighbours a locked system, get explicit sign-off before
implementing.

---

## 3. Impact list — every surface this affects

### 3.1 Blocking (offline is impossible until these are fixed)

| Surface | File | What breaks | Fix |
|---|---|---|---|
| **Boot navigation** | `AuthenticationSceneController.LoadMainMenuNetworkedAsync` | Unbounded `await WaitForRelayReadyAsync(ct)` after retries exhaust — the player never leaves the splash | Fall into offline mode: start local host, then `NetworkManager.SceneManager.LoadScene(menuScene)` on it |
| **Host startup** | *(does not exist)* | Nothing can start a host without Relay | New `INetworkTransitionService.StartLocalHostAsync` + transport reset (§2.2) |
| **Netcode callbacks** | `MultiplayerSetup.OnAuthenticationSignedIn` | Gated on `OnSignedIn`, which never fires offline → approval/disconnect/transport-failure callbacks unwired | Also wire on offline-mode entry |
| **Stale comment** | `MultiplayerSetup.cs:164-167` | Claims a fallback that does not exist | Delete or make true |

### 3.2 Degraded (playable, but the session is throwaway)

| Surface | File | Offline behaviour | Notes |
|---|---|---|---|
| **All cloud data** | `UGSDataService` | `InitializeAsync` only runs from `HandleSignedIn` → `IsInitialized` false forever; **all ten repositories** (profile, mode stats, progression, hangar, episodes, settings, daily challenge, training, squad, loadout) sit at in-memory defaults | **No local disk cache exists.** Everything earned offline is lost at quit |
| **Player identity** | `PlayerDataService.CreateLocalDefaultProfile` | Fresh `Guid.NewGuid()` + random `Pilot####` **every launch** | The player has no stable identity offline |
| **Settings** | `PlayerSettingsRepository` | Cloud-only. Volume/graphics/accessibility changes do not survive a quit | `ClientPrefs` (PlayerPrefs) already exists for master/music volume — the local tier is half-built |
| **Vessel unlocks** | `HangarRepository`, `VesselUnlockSystem` | `SyncHangarToVessels()` never runs → only `OwnedFromStart` vessels are available | Player may see fewer vessels than they own |
| **Progression / episodes** | `GameProgressionRepository`, `EpisodeProgressRepository` | Mode unlock state defaults; episode tokens unavailable | May hide game modes |
| **Leaderboards** | `UGSStatsManager` | `AddPlayerScoreAsync` throws, caught | Scores silently dropped, not queued |
| **Analytics** | `AnalyticsServiceFacade`, `UgsAnalyticsSink` | Guarded, degrades | Already handles offline |
| **Cloud save writes** | `UGSCloudSaveProvider.SaveAsync` | ✅ Correct already — returns false, repo stays dirty, retries on reconnect | Only works if the repos were ever loaded, which offline they are not |

### 3.3 Must be hidden or disabled offline

| Surface | Why |
|---|---|
| **Party / invite panel** (`ArcadeLobbyList`, `OnlineInfoEntry`, `PartyInviteNotificationPanel`) | Presence lobby never joined; every invite action throws |
| **Friends** (`FriendsListPanel`, `RequestInfoEntry`, `FriendsServiceFacade`) | `FriendsService.InitializeAsync` gated on sign-in |
| **Leaderboards screen** (`LeaderboardsMenu`) | No data; needs an empty/offline state |
| **Store / IAP** (`StoreScreen`, `IAPManager`, `EpisodeTokenService`) | Purchases must be refused offline, not attempted |
| **Daily challenge** (`DailyChallengeSystem`) | Server-dated; must not award or reset offline |
| **Display-name setup** (`DisplayNameRegistry`, uniqueness check) | Cannot verify uniqueness — either skip the name gate or accept locally and re-validate on reconnect |
| **Ads** | Requires network |

### 3.4 Game modes — what is actually playable offline

Every domain minigame is a **multiplayer-mode scene backfilled with AI**
(`ServerPlayerVesselInitializerWithAI`). With a local host they all work solo — that is exactly
what a "single-player fallback" is in this architecture. Confirmed playable offline once §3.1 is
fixed:

- All 15 multiplayer scenes (HexRace, Joust, Crystal Capture, Astro League, Brood Rush, Rampage,
  Peel the Cage, Wildlife Liberation, Dog Fight, The Bends, Scarab Scramble, …) — solo + AI
- Menu_Main lava-lamp / freestyle, all toys (cell selector, painting, Wanderway, vessel/domain changers)
- Maelstrom / Tournament — sequential `Single` scene loads, all local
- The two genuine single-player scenes (`MinigameCellularDuel`, `MinigameWildlifeBlitz`) use the
  non-networked `PlayerSpawner` path and would work even without a host

**Not playable offline:** party play with real humans. That is inherent, not a defect.

### 3.5 Mid-session network loss (a separate, live bug)

Independent of cold-boot offline, and **already broken today**:

```csharp
// ApplicationStateMachine.cs
if (_networkMonitorData?.Value?.OnNetworkLost != null)
    _networkMonitorData.Value.OnNetworkLost.OnRaised += HandleNetworkLost;   // → Disconnected

void HandleNetworkLost() => TransitionTo(ApplicationState.Disconnected);
```

**`OnNetworkFound` is never subscribed.** Once Wi-Fi drops, the app sits in `Disconnected`
permanently, even after the connection returns. Nothing currently *consumes* `Disconnected`, so the
symptom is latent — but any future UI that reacts to it will be wrong on first contact.

Scenarios to cover:

| Scenario | Current | Wanted |
|---|---|---|
| Wi-Fi drops in menu | Relay session dies → `OnHostConnectionLost` → "tap retry" over a working menu | Drop to offline mode, keep playing, offer reconnect |
| Wi-Fi drops mid-solo-game | Relay transport fails → `OnTransportFailure` → `HandleActiveSessionEnd` → kicked to menu, game lost | A solo game has no remote peer — it should survive the drop |
| Wi-Fi drops mid-party-game | Genuine session end, correct to leave | Unchanged |
| Airplane mode at launch | **Bricked at splash** | Offline mode |
| Network returns | Nothing recovers | Offer re-entry to online (§5) |

The mid-solo-game case is the sharpest: a player alone with AI loses their run to a network event
that has no bearing on their game.

---

## 4. Suggested implementation order

1. **`INetworkTransitionService.StartLocalHostAsync`** + transport reset (§2.2). Small, isolated,
   independently testable — and it is the missing primitive everything else needs.
2. **Unblock boot.** Bound the final wait in `LoadMainMenuNetworkedAsync`; on exhaustion start the
   local host and load `Menu_Main` through it. **This alone makes the game playable offline.**
3. **`SessionMode` flag** + wire `MultiplayerSetup`'s callback wiring to offline entry.
4. **Local persistence tier** under `UGSDataService` — a disk cache behind `ICloudSaveProvider` so
   an offline session survives a quit and reconciles on reconnect. Largest piece; design the merge
   policy (last-write-wins vs. per-field) before writing it.
5. **UI gating** (§3.3) — hide or empty-state the online-only surfaces.
6. **Network-loss recovery** (§3.5) — subscribe `OnNetworkFound`, stop killing solo games on
   transport failure.

Steps 1–2 are a few hours and remove the brick. Steps 4–6 are the real work.

---

## 5. Open questions — need a decision before implementing

1. **Offline → online promotion.** When the connection returns mid-session, do we (a) offer a
   "Reconnect" that re-runs the auth + eager-Relay boot in place, (b) require an app restart, or
   (c) silently upgrade? (c) is tempting and wrong — the local host must be shut down and a Relay
   session created, which respawns every player object. **(a) with an explicit confirm** is the
   honest option.
2. **Merge policy for offline progress.** An offline session earns crystals/XP/unlocks against a
   throwaway local identity. On reconnect, does it merge into the cloud profile, or is offline
   progress local-only and non-syncing? Merging invites duplication exploits; not merging makes
   offline play feel worthless. **Recommendation: merge additive counters, cloud wins on
   identity/purchases.**
3. **Is offline explicit or automatic?** Auto-detect only, or also a manual "Play Offline" button
   on the boot retry surface? Manual is worth it — detection has false negatives on captive portals
   and flaky connections.
4. **Anonymous auth without network.** UGS anonymous sign-in needs one online round-trip *ever* to
   mint a session token. A player whose **first ever launch** is offline has no cached token and no
   `PlayerId`. Confirm the local-identity path is acceptable for that case (§3.2).

---

## 6. What shipped (2026-08-26) — and what remains

### 6.1 Implemented

| Piece | Where | What it does |
|---|---|---|
| **Offline local host** | `OfflineModeService` (`_Scripts/System/`, pure C# lazy DI singleton, registered in `AppManager.InstallBindings`) | Restores local data first, wires the Netcode callbacks via `MultiplayerSetup.EnsureNetcodeCallbacksWired()` (with a minimal approval-callback fallback), re-asserts the loopback transport (`SetConnectionData("127.0.0.1", 7777)`), `StartHost()`, waits for `IsListening`. Single writer of `GameDataSO.IsOfflineSession`. |
| **Boot unblock** | `AuthenticationSceneController.LoadMainMenuNetworkedAsync` | Device unreachable → skips the 3×15 s Relay attempts entirely; otherwise attempts them, then falls into `EnterOfflineSessionAsync` instead of the old unbounded wait. `Menu_Main` then loads through Netcode scene management on the local host — the whole spawn chain runs unchanged. The manual retry surface survives only as the last resort when even `StartHost` fails. The per-attempt `EnsurePartySessionAsync` retry is also exception-hardened: an unauthenticated create used to throw out of the `UniTaskVoid` and strand the flow before any fallback could run. |
| **Session flag** | `GameDataSO.IsOfflineSession` (`[NonSerialized]`) | Read by the gates below. Deliberately not cleared by `ResetRuntimeData`/`ResetAllData` — the offline session lasts until app restart. |
| **Local last-known-good data** | `LocalCloudDataCache` (`_Scripts/System/CloudData/Providers/`) + `CloudDataRepository.LoadAsync/SaveAsync` | Every cloud key is mirrored to `{persistentDataPath}/CloudCache/{key}.json` on successful load and on every save attempt; when the cloud answers nothing, the snapshot restores. Covers **all ten repositories** — profile (display name, avatar, crystals), hangar (vessel unlocks), episodes, game progression, mode stats, settings, daily challenge, training, squad, loadout. Newtonsoft serialization (same as the UGS SDK), root path captured on the main thread at startup, all IO fail-soft. Cloud wins whenever it answers; `ResetAsync` overwrites the snapshot so a reset cannot resurrect data. |
| **Offline data init** | `UGSDataService.InitializeOfflineAsync` | Runs the ordinary `InitializeAsync` pipeline with the provider unavailable — every repo answers from its snapshot, `IsInitialized` flips true, `SyncHangarToVessels` restores unlocks, `OnInitialized` lets `PlayerDataService` merge the cached profile through its normal path. Called by `OfflineModeService` *before* `StartHost`, so the host's Player object resolves the cached display name instead of minting `Pilot####`. A late sign-in reconciles: clean repos re-load from cloud, dirty repos keep offline progress and flush through the existing debounce loop. |
| **Matchmaking stand-down** | `MultiplayerSetup.OnAuthenticationSignedIn` (+ `Start`) | Offline session → wires callbacks + raises `SessionStarted` in game scenes, never shuts the local host down for UGS matchmaking. `EnsureNetcodeCallbacksWired()` extracted public so both paths share one callback set. Stale `EnsureHostStartedAsync` comment fixed. |
| **Party stand-down** | `HostConnectionService.EnsurePartySessionAsync` | No-ops for the whole offline session, so a late Relay success can never `ShutdownAsync` the local host out from under a running offline game. |
| **Network recovery** | `ApplicationStateMachine` | Subscribes `OnNetworkFound` (the §3.5 bug): `Disconnected` now captures the state it interrupted and resumes it when reachability returns — including `InGame`, mirroring the Paused restore. |

Verified in this pass (no Unity Editor available in the implementation environment — see
`Docs/UNITY_VERIFICATION_CHECKLIST.md`): Roslyn syntax pass over all 11 touched files;
`OfflineModeService` compiled against API-accurate NGO/UniTask/UnityEngine stubs; the
cache + repository layer compiled against the **real** Newtonsoft and exercised end-to-end
(12 assertions: offline restore of name/vessels/episodes incl. `Dictionary` round-trip,
offline progress surviving a quit, cloud-wins on reconnect, reset overwrite, corrupt-file
degradation); `check_conditional_compilation.py` clean.

### 6.2 Still open

- **UI gating (§3.3)** — party/friends/leaderboards/store/daily-challenge surfaces are not yet
  hidden or empty-stated when `GameDataSO.IsOfflineSession` is true; their actions fail
  guarded-but-visibly.
- **Offline → online promotion (§5.1)** — the session stays offline until restart, by design in
  this pass. A "Reconnect" flow is future work and must re-boot the party layer, never promote
  in place.
- **Mid-session network loss on a solo game (§3.5)** — `OnTransportFailure` still ends a solo
  game. Rare on a live Relay solo session; revisit with the reconnect flow.
- **First-ever launch offline (§5.4)** — no cached UGS token and no snapshots yet: the player
  gets a fresh local default profile (random `Pilot####`) whose progress lands in the snapshot
  store, but is superseded when the first real cloud profile loads. Offline-earned progress on a
  *never-signed-in* install does not merge into the account created later.
- **Snapshot scope** — the cache is per-device last-known-good, not per-account. One anonymous
  UGS account per device makes this safe today; revisit if account switching ever ships.

---

## 7. Online-only UI gating + reconnect (2026-08-26, second pass)

### 7.1 UI gating — two layers, deliberately

**`OfflineUIGate`** (`_Scripts/UI/Elements/`) is one reusable, inspector-wired component rather
than an offline branch inside every screen: wire the online-only objects/controls and the
offline-only ones (notice, reconnect button) and the panel gates itself. `Hide` or
`DisableAndDim` per panel — dim where hiding would collapse a layout or hide that the feature
exists at all. Re-applies on enable (which covers every appearance, since screens and modals are
activated on navigation) and on reconnect state changes, and again in `Start` because `[Inject]`
lands between `Awake` and `Start`.

**The gate is presentation only, and is not the enforcement.** The services refuse online work
themselves while offline:

| Guard | Where | Why there |
|---|---|---|
| Invites | `HostConnectionService.SendInviteAsync` | No presence lobby and no Relay session to invite into; without it the call fell through to `EnsurePartySessionAsync` (now an offline no-op) and dereferenced a null session ref. |
| Leaderboard writes | `UGSStatsManager.SubmitScoreInternal` | Deliberately **not queued** — a leaderboard entry is a claim about a live ranking, not progress to replay. (Cloud-save data *is* mirrored locally and flushed on reconnect; the two are different kinds of write.) |
| Purchases | `IAPManager.OpenCheckout` | The shared choke point of both purchase entry points. Opening a browser at an unreachable checkout *and* arming a pending purchase awaiting a confirmation that can never arrive is worse than declining. |

General rule: **gate the UI so players are never offered something that cannot work; never rely
on the UI alone to enforce it.** A screen nobody wired must still be unable to fire a doomed
request.

### 7.2 Reconnect — and why it does NOT reload Bootstrap

`ReconnectService` + `ReconnectButton`: one tap tears the offline host down, clears
`IsOfflineSession`, resets the auth facade, and loads the **Authentication scene** — which *is*
the boot chain (sign in → wait for the Relay host → load `Menu_Main` through Netcode). If the
network is still down, that same flow falls back to `OfflineModeService` exactly as at cold boot,
so a failed retry lands the player back in a working offline menu instead of stranding them.

`AuthenticationServiceFacade.ResetForReconnect()` is the load-bearing detail: the pre-existing
`ResetStartupState()` re-arms the startup guard but leaves `_successNotified` latched, so a
*successful* reconnect would not re-raise `OnSignedIn` — the trunk that starts
`HostConnectionService`'s lobby + Relay session, `UGSDataService`'s cloud load, and
`MultiplayerSetup`'s host wiring. A reconnect that silently starts nothing is worse than one that
fails loudly.

**Why not literally re-load the Bootstrap scene** (the first instinct, and what was asked for):
Bootstrap is where the persistent layer is *built* — ~15 `DontDestroyOnLoad` roots (SceneLoader,
MultiplayerSetup, SceneTransitionManager, UGSDataService, AudioSystem, CameraManager,
PartyServices, the splash canvas, NetworkManager, …) — and **only `AppManager` guards against a
second copy of itself** (`_hasBootstrapped`). Re-loading that scene spawns a duplicate of every
other one. `UGSDataService.Awake` alone does an unguarded `Instance = this`, so the duplicate
would clobber the real instance and then *null* it in its own `OnDestroy` — and `Destroy` during
`Awake` is deferred to end-of-frame, so the duplicate's `Awake` runs regardless. Two `SceneLoader`s
would double every scene load; two `MultiplayerSetup`s would double the Netcode wiring.

Bootstrap's remaining jobs — platform config and DI registration — are session-scoped and already
done. The Authentication scene is precisely the part of the boot chain that needs re-running, and
it is loaded and unloaded routinely, so the reconnect reuses a proven path instead of making a
build-the-world scene re-entrant. **If a literal Bootstrap reload is ever wanted, the prerequisite
is a duplicate guard on every persistent root** — that is the real work, and it is a separate
change with its own blast radius.

### 7.3 Scene wiring still required

`OfflineUIGate` and `ReconnectButton` are code + prefab-ready but are **not placed in any scene**.
In `Menu_Main`, add an `OfflineUIGate` to the party/lobby panel, the friends panel, the
leaderboards screen and the store screen, wiring each panel's online-only objects; and put a
`ReconnectButton` (plus an "Offline" notice) in each gate's offline-only list — or once,
somewhere always visible in the menu. Until then the gating is inert and the service-level
guards above are the only thing standing.
