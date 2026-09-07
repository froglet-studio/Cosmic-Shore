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
// MultiplayerSetup.EnsureHostStarted - the comment as it stood BEFORE this branch
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

*Proposed* home at design time: `INetworkTransitionService`, which already owns `ShutdownAsync`.
**What shipped instead:** the host start lives in `OfflineModeService.EnterOfflineSessionAsync`,
because starting the offline host is not a bare Netcode primitive — it is a sequence (stand the
party layer down → restore local data → wire callbacks → reset transport → `StartHost` → await
`IsListening`) whose steps belong to the offline session, not to a transport-lifecycle helper.
`INetworkTransitionService` still owns the *shutdown* half, which both `ReconnectService` and the
offline entry call. There is no `StartLocalHostAsync` — do not go looking for one.

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
| **Host startup** | *(does not exist)* | Nothing can start a host without Relay | `OfflineModeService.EnterOfflineSessionAsync` — transport reset + `StartHost` (§2.2, §6.1) |
| **Netcode callbacks** | `MultiplayerSetup.OnAuthenticationSignedIn` | Gated on `OnSignedIn`, which never fires offline → approval/disconnect/transport-failure callbacks unwired | Also wire on offline-mode entry |
| **Stale comment** | `MultiplayerSetup.EnsureHostStarted` (tail comment) | Claims a fallback that does not exist | Delete or make true |

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

- All 15 multiplayer scenes (SkimRace, Joust, Crystal Capture, Astro League, Brood Rush, Rampage,
  Peel the Cage, Wildlife Liberation, Dog Fight, The Bends, Scarab Scramble, …) — solo + AI
- Menu_Main lava-lamp / freestyle, all toys (cell selector, painting, Wanderway, vessel/domain changers)
- Maelstrom / Maelstrom — sequential `Single` scene loads, all local
- The two genuine single-player scenes (`MinigameDuelForTheCell`, `MinigameWildlifeBlitz`) use the
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

1. **The local-host start** + transport reset (§2.2) — shipped as
   `OfflineModeService.EnterOfflineSessionAsync`, not as the `INetworkTransitionService`
   primitive this section originally proposed.
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

---

## 8. The player-facing online/offline toggle (2026-08-27)

Offline stopped being only a fallback: the player can now **choose** it, Steam-style.

### 8.1 The lamp is the toggle

`OnlineStatusIndicator` (`_Scripts/UI/Elements/`) is one control doing both jobs — it reads
green (lime) online, grey offline, and tapping it asks whether to switch. One control rather
than two buttons because the question is always the same one ("which mode am I in, and do I want
the other?") and the answer is the colour.

Colours come from the shared `ElementalBarsConfigSO` ladder (lime / grey), not local literals, so
the lamp speaks the palette's existing language — grey already means *not in use* on every
element flower.

Confirmation runs through **`ConfirmQuestionBar`**, a reusable inline yes/no bar that carries no
knowledge of what it is confirming: `Ask(question, onAccept)`. It wipes open horizontally with
its content fading in and closes the same way (the continuity law — nothing pops), the answer
buttons punch on press, and every tween is on **unscaled** time and `SetLink`ed, so a paused
timescale or a destroyed panel cannot strand one. One pending question at a time: a second `Ask`
replaces the first rather than queueing, and the replaced callback is dropped, never invoked — a
stale question the player has moved past is worse than no question.

Two ordering details are load-bearing there: the accept callback is **captured before `Close()`**
(which clears the field via `CloseImmediate`), and the bar stops taking input the instant an
answer lands rather than when the close animation finishes — a flourish must never be able to
absorb a second answer.

### 8.2 Both directions run the same boot chain

`ReconnectService` grew `GoOfflineAsync()` beside `ReconnectAsync()`; both funnel into one
private `RunBootChainAsync`. Going offline does **not** swap the host underneath a live
`Menu_Main`: the player object and its vessel belong to the host being torn down, so the spawn
chain has to run again on the new one, and the boot chain is the proven path that does it.

### 8.3 Preference vs. session state — deliberately two things

| | Meaning | Lifetime |
|---|---|---|
| `GameDataSO.IsOfflineSession` | what the session **is right now** | this session |
| `OfflineModeService.OfflinePreferred` | what the player **asked for** | persisted (`PlayerPrefs`) |

A deliberate choice that silently reverts next launch is not a choice, so the preference
persists and `AuthenticationSceneController` reads it at boot — skipping the Relay attempts
entirely, and skipping the "could not reach the servers" apology, because neither applies when
offline was *chosen*. A player who never touched the toggle can still be in an offline session
with the preference false; going online then costs them one tap.

`ReconnectAsync` clears the preference *before* the boot chain reads it; `RunBootChainAsync`
clears only `IsOfflineSession`, never the preference.

### 8.4 Wiring it: `FrogletTools > Interface > Wire Offline Menu Surfaces`

`OfflineMenuWirer` wires Menu_Main: the lamp, the confirm bar, and an `OfflineUIGate` on each
online-only panel.

Panels are found **by component type, not by GameObject name** — they live inside prefab
instances whose object names differ from their script names (`LeaderboardsMenu` sits on
*PortScreen*, `StoreScreen` on *ArkScreen*), so a name match would silently miss most of them.
And the style is per-target, which is load-bearing: **a whole screen the nav bar can reach must
never be hidden** — the player would navigate to a blank panel — so screens dim in place and stay
navigable while sub-panels hide outright. Resolved against the shipped scene:

| Target | Panel object | Gate host | Style |
|---|---|---|---|
| `FriendsListPanel` | FriendListPanel | ArcadeScreenModal | Hide |
| `ArcadeLobbyList` | ArcadeLobbyList | Arcade_Panel | Hide |
| `LeaderboardsMenu` | PortScreen | PortScreen | DisableAndDim |
| `StoreScreen` | ArkScreen | ArkScreen | DisableAndDim |
| `PartyInviteNotificationPanel` | (prefab instance) | its parent | Hide |

A hidden panel is gated from its PARENT — a gate on the object it deactivates would kill its own
`OnEnable` and could never restore it — while a dimmed screen keeps its GameObject active and so
hosts its own gate. It works on the **open scene, never the YAML**, so unsaved authoring is
preserved and adopted; it finds every object **by name** and only creates what is missing, so it
is idempotent and safe to re-run after hand-tuning. It warns rather than proceeding when the
scene has no `ContainerScope` (without one `[Inject]` never resolves and every offline surface is
inert), and it reports any panel it could not find by name instead of silently doing nothing.

It also generates the accept/cancel icons into `Assets/_Graphics/UI/Offline/` — a check and a
cross drawn as anti-aliased distance-to-segment strokes. Generated because they are pure
geometry and it keeps the tool self-contained; it will never overwrite an existing file, so
replacing them with authored art is just dropping the PNGs in.

Output is tracked through `FrogletToolChangeLedger`, so the scene and icons ship via
**FrogletTools > Build > Pending Tool Changes**. The tool is permanent (idempotent, re-runnable
whenever a panel is added), not a one-off to retire.

---

## 9. Two reconnect bugs found in play (2026-08-27)

Going offline worked; coming back **online** hit three Relay timeouts and then threw
`get_internetReachability can only be called from the main thread`. Two independent defects,
both worth generalising.

### 9.1 `OnSignedIn` never re-raised — the reconnect had nothing to wait for

`AuthenticationSceneController.RunAuthFlowCoreAsync` short-circuits on `IsAlreadySignedIn()` and
jumped straight to the post-auth flow **without touching the facade**. At cold boot that is
correct - `AppManager` already drove the sign-in. On a **reconnect** it is fatal: coming back
online never signs out, so the UGS session is still live, the branch is taken, and
`OnSignedIn` is never raised.

That event is the trunk the entire online stack hangs off — `HostConnectionService`'s presence
lobby and Relay session, `UGSDataService`'s cloud load, `MultiplayerSetup`'s Netcode wiring all
subscribe to it **and to nothing else**. So no party session was ever created, and
`WaitForRelayReadyAsync` sat waiting for an event nobody was going to fire: 3 × 15 s, then the
offline fallback. The fallback working is what made it look like a network failure rather than a
wiring one.

The fix is to re-announce: the branch now calls `EnsureSignedInAnonymouslyAsync()`, which
fast-paths on `IsSignedIn` with no round-trip and raises only when the facade's success latch was
cleared. `ResetForReconnect` also stopped resetting `State` — sending it to `NotInitialized`
forced a pointless `UnityServices.InitializeAsync()` re-run *and* defeated the very fast path the
re-announce depends on. Clearing the latch is the whole job.

> **The general rule:** *a "we're already in the right state, skip the work" fast path must still
> emit the state's ANNOUNCEMENT.* Skipping the work and skipping the event look identical at the
> call site and are not: everything that subscribed is still waiting.

Proven by executing the shipped latch logic (`ResetForReconnect` → re-announce): cold boot raises
once, re-entry does not double-raise, a reconnect raises again, and the pre-fix path is kept as a
negative control showing the trunk staying silent.

### 9.2 `.AsMainThread()` marshals the SUCCESS path only

`await WaitForRelayReadyAsync(linkedCts.Token).AsMainThread()` — when the wait **times out**, the
`OperationCanceledException` is raised from `linkedCts`'s timer, so it propagates out of the
*inner* await and the marshaling step never runs. The `catch` block therefore resumed on the
timer's thread, and everything after it (`Application.internetReachability`, `PlayerPrefs`, the
status text, the scene load) was main-thread-only. Hence the exception, immediately after the
"auto-retry exhausted" log.

Fixed with an explicit `await MainThreadDispatcher.SwitchToMainThreadAsync()` at the top of the
catch and again after the loop — the shape `Docs/THREADING.md` already prescribes for a catch
block. The new re-announce await uses `.AsMainThread()` rather than `.AsUniTask()` for the same
reason: its fast path happens to complete synchronously today, and "it resumes inline" is exactly
the assumption that put a reachability read on a timer thread.

> **The general rule:** *`.AsMainThread()` is a success-path guarantee.* Any `catch` after a
> cancellable await, and any code after a loop containing one, must marshal explicitly. A
> timeout is not an edge case on these paths — it is the path.

### 9.3 The first-activation race in `ConfirmQuestionBar`

`Awake` closes the bar so it can be authored in whatever state is convenient. That is correct
while the bar is authored **active** (as the shipped scene has it) and quietly wrong if anyone
later deactivates it — the natural thing to do to a hidden panel:

`Ask` → `Open` → `SetActive(true)` → **Unity runs `Awake` synchronously inside that call** →
the auto-close nulls `_onAccept` and deactivates the object again → the rest of `Open` then runs
on a dead bar. No error, no bar, and the accept callback silently gone.

Guarded with an `_opening` flag set around `SetActive`, so `Awake` skips its auto-close when it is
being driven *by* `Open`. The component now behaves identically whether the bar is authored active
or inactive.

> **The general rule:** *`SetActive(true)` on a never-activated object runs `Awake` before it
> returns.* Any first-activation initialiser that also mutates state — closing, resetting,
> clearing a callback — can therefore fire in the middle of the very operation that activated it.

---

## 10. Three more found in play (2026-08-27) — the switch collided with its own leftovers

The reconnect now re-raised `OnSignedIn` (§9.1 worked — `PresenceLobbyService` was clearly
running), and immediately hit a different wall: `player is already a member of the lobby`,
`Illegal transition: Reconnecting → InPresenceLobby`, `Invalid transition: ShuttingDown →
MainMenu`, then three Relay timeouts and a fall back to offline.

### 10.1 The party layer was never LEFT — only disconnected

`ReconnectService` tore down `NetworkManager` and nothing else. But **UGS lobby and session
membership is server-side**: shutting the transport down does not release it. So HCS's re-init
tried to re-join a presence lobby this player was still a member of, UGS refused it, HCS never
finished initialising, and no Relay session was ever created — so the auth scene's Relay wait
timed out against a session nobody was going to make. Exactly §9.1's failure shape, one layer
further out.

`HostConnectionService.ResetPartyLayerAsync()` now leaves the Relay session **and** the presence
lobby and returns the party state machine to `Disconnected`. **Order matters**: it runs BEFORE the
Netcode shutdown, because the leave calls need a live transport to reach UGS. It is fail-soft at
every step — a teardown that throws costs the online attempt, not the switch.

It deliberately does not raise `HostConnectionLost`: that drives the "tap retry" surface, and this
teardown is a step inside a transition already covered by the loading veil — the same suppression
`BootStatusBroadcaster` applies to launch and party transitions.

**`OfflineModeService` calls it too**, which is the other half. An offline session has no lobby
and no Relay, and a presence lobby left running keeps its refresh/converge loop hammering UGS for
the whole offline session — a stream of join and query errors on a screen the player was just told
is offline. It also releases the membership *now*, so the next attempt to come online is not
refused by our own leftovers.

> **The general rule:** *tearing down the transport is not the same as leaving the service.*
> Anything with server-side membership has to be told, and the telling needs the transport still
> up — so it goes first.

### 10.2 `Reconnecting → InPresenceLobby` was not a legal transition

The refresh watchdog drops the party machine into `Reconnecting` after
`MAX_REFRESH_ERRORS_BEFORE_RECONNECT` — which is exactly what a mode switch causes. HCS's init
then tries `InPresenceLobby` as its first move and was refused, so initialisation stopped dead.
Added as a legal recovery edge: re-entering through the front door after a drop is a legitimate
recovery, not a bug to log. (Pre-existing gap; the switch just made it reachable every time.)

### 10.3 `ApplicationStateMachine` inherited a dead state from the previous play session

`ApplicationStateDataVariable` is a ScriptableObject **asset**, so in the Editor its value
survives play-mode exit. Every quit ends in `ShuttingDown` (`AppManager.OnDisable` /
`OnApplicationQuit` → `Shutdown`), and `ShuttingDown` is terminal — so the *next* play session
started there and refused every transition for the entire run. Hence `Invalid transition:
ShuttingDown → MainMenu` at boot, unrelated to offline mode but poisoning it.

The machine is constructed exactly once per app run, so it now clears any non-`None` persisted
state in its constructor.

> **The general rule:** *a SOAP variable holding RUNTIME state must be reset by its single writer
> at construction.* In the Editor an asset does not go away between play sessions, so
> "it starts empty" is only true on a fresh install.

Proven by transcribing both state machines and executing them: 9/9 assertions, including negative
controls that reproduce the pre-fix `ShuttingDown` deadlock and confirm the watchdog path.

---

## 11. The status lamp icon set (2026-08-27)

**State is carried by both tint and artwork.** The lamp tints lime online / grey offline, and
swaps between two authored sprites — `Assets/_Graphics/Port/OnlineIndicator.png` and
`OfflineIndicator.png`, wired on `OnlineStatusIndicator.onlineSprite` / `offlineSprite`. Artwork
matters here because colour alone is a weak signal for a player who cannot separate lime from
grey.

The sprite swaps at the **midpoint** of the colour crossfade, not at either end: the artwork
change and the colour change then read as one motion. Swapped at an end it reads as two, with a
visible hitch. Both fields empty is a supported configuration — the lamp falls back to tint-only.

> **The wiring tool never assigns or overwrites the lamp's sprite or its rect.** Art and layout
> belong to whoever authored them, and a wiring pass that "helpfully" rewrites them destroys work
> silently on every re-run. `OfflineMenuWirer` binds `lamp` and `questionBar` and stops there.

### 11.1 A generated lamp pair was built and removed

Generated filled/hollow lamp sprites were built here first (procedural, ring border, alpha-derived
depth). They were **removed** once authored art landed: the tool now generates only the accept and
cancel glyphs. Recorded because the reasoning still holds if the art is ever revisited — the point
of the pair was that *shape* carries state where colour cannot.

### 11.2 What the removal kept — the renderer

The icon renderer was rebuilt during that work and stays, because the check/cross benefit. The
first pass took one centre sample per pixel plus a fixed 1.5px linear feather on the distance
field — a coarse approximation of coverage that shows as ragged curves. It now evaluates a
**hard** 0/1 shape function over a 4×4 grid per pixel at 256px, so all smoothing is real coverage
and an edge resolves correctly at any curvature.

Measured on an identical shape against a 16×16 reference:

| | edge error | edge pixels |
|---|---|---|
| old — 128px, 1 sample + feather | 0.3978 | 404 |
| new — 256px, 4×4 supersampled | **0.0277** | 828 |

**14.4× more accurate per edge pixel, at 2× the edge resolution.** The check/cross on this branch
were regenerated through it (2,080 → 4,007 and 1,651 → 4,187 bytes).

Two details that matter for UI sprites: **mipmaps are on** (these scale *down* through a
CanvasScaler, and a thin stroke shimmers without them), and **RGB stays white even where alpha is
zero** — a sprite carrying black RGB in its transparent pixels fringes dark when filtered.

`EnsureIcon` never overwrites, so authored art always wins; **FrogletTools ▸ Interface ▸ Wire
Offline Menu Surfaces (Regenerate Icons)** forces a re-render after a geometry change.

---

## The snapshot is a data layer nobody remembers

`LocalCloudDataCache` writes to `{persistentDataPath}/CloudCache/{key}.json` — **neither PlayerPrefs
nor UGS**. It is what makes offline play work, and it is also why "I cleared PlayerPrefs and deleted
the player in the UGS dashboard, and they still had their data" is a thing that happens: every
repository falls back to this snapshot when the cloud answers with nothing, so a deleted account can
launch straight back into its own save.

Player data has **four** layers, and the dashboard shows one:

| Layer | Where | Cleared by |
|---|---|---|
| Cloud Save | UGS, keyed by player id | the dashboard, or `ICloudSaveProvider.DeleteAsync` |
| Local snapshot | `{persistentDataPath}/CloudCache/` | `LocalCloudDataCache.DeleteAll` |
| PlayerPrefs | per-platform prefs store | `PlayerPrefs.DeleteAll` |
| Session token | the Authentication SDK's own storage | `ClearSessionToken` |

The session token is the subtle one: without clearing it the next launch re-authenticates as the
**same player id**, so no fresh anonymous account is minted and the data appears to come back even
when the first three were wiped.

**FrogletTools ▸ Services ▸ Wipe Player Data** does all four, each as its own switch, and reports
which layer held what — the thing that was actually missing when this was first diagnosed by hand.
Its Cloud Save key list is read off `UGSKeys` by reflection rather than kept by hand, because a
stale list in a wipe tool fails as a wipe that quietly leaves data behind.
