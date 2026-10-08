# Multiplayer Architecture Review — Invite / Join, request discipline, disconnect resilience

**Date:** 2026-10-06 · **Branch reviewed:** `Ys-bleeding-edge` @ `94c14ccc` (bleeding-edge + Bug_Hunt +
bold-fermi + confident-pascal integrated) · **Scope:** the party / presence / invite layer, the
Netcode + Relay transport settings around it, and every disconnect path — read against the SDK
sources actually compiled into this branch (Netcode for GameObjects 2.13.3, Multiplayer Services
2.3.3, Friends 1.3.0, Unity Transport 2.7.4 on Unity 6000.3.17f1).

**Constraints honoured:** Unity and package versions are **unchanged**. The two LOCKED decisions
(two-level Presence-lobby + Party-session model; **EAGER per-user Relay "Always-InParty"**) are
taken as given and not relitigated — every recommendation below fits inside them.

This is a review, not a change set: nothing in `Assets/` was modified. Where a recommendation
touches a prefab value or C#, it says so and goes through `/verify-unity` + an MPPM run like any
other change.

---

## 0. TL;DR — what is actually wrong, in order

The "always some issue here and there" history (Party B2–B24, Presence B1/B4/B6) is not twenty-four
unrelated bugs. Almost all of them are **symptoms of three structural choices**:

1. **The presence layer polls a push service, at or above its rate limit.** The Sessions SDK
   already opens a Wire subscription for every lobby it joins or creates and raises `ISession`
   events (`PlayerPropertiesChanged`, `PlayerJoined`, `PlayerLeft`, `SessionPropertiesChanged`,
   `Deleted`, `RemovedFromSession`, `StateChanged`, …). The game subscribes to exactly two of them
   (`PlayerLeaving`, `Deleted`) and instead re-reads **both** sessions every 1.5 s (0.75 s for 15 s
   after any action), runs a converge query every 4 s, and does a full re-read **before and after
   every property write**. Per client that is ≈1.3 lobby GETs/s at rest and ≈2.7/s boosted against a
   limit of 1/s. **B24 (429 → offline fallback), B1 (LobbyPatcher spam) and B6 (stale-index NRE) are
   one defect**: our own refreshes mutate the lobby object concurrently with the SDK's delta
   application. The fix is to **delete** the per-tick refreshes and listen to the events the SDK is
   already paying for, keeping one slow reconciliation poll (15–30 s) as a safety net.
   *(§3.1, §5.1)*

2. **Retries have no owner.** Four divergent `IsRateLimitException` implementations (one structured,
   three string-matching "Too Many Requests"), three independent retry loops (fixed 2000 ms ×3;
   exponential ×3/×5; "host conflict" ×2 with **no** backoff and a classifier that matches any
   message containing the word *host*), no jitter, no budget, and **retry at two layers** — a 429
   inside `SaveWithRetryAsync` is retried there and *also* counted by `RefreshAsync`'s
   `MAX_REFRESH_ERRORS_BEFORE_RECONNECT` toward a `ForceReset`. Industry practice is one policy
   object: classify → backoff with full jitter → single-flight per operation → a per-minute retry
   budget → retry at **one** layer. ~150 lines replace ~400. *(§3.2, §5.4)*

3. **Every disconnect is terminal.** A client that loses Wi-Fi for 8 s mid-match is bounced to a solo
   menu and its vessel is converted to AI *permanently*; a lobby-side blip is handled by rebuilding
   the party layer or re-running the whole boot chain through the Authentication scene. Yet
   `PartyStateMachine` already has a `Reconnecting` state, `ISession.ReconnectAsync()` and
   `MultiplayerService.ReconnectToSessionAsync(id)` exist and are never called, the Lobby service
   keeps a disconnected player for **120 s** by default, `Player.NetUgsPlayerId` already gives the
   host a stable identity for the slot, and the connection-approval payload already carries a
   token (the spectator one). All the parts of a Boss-Room-style **reconnection grace** exist; none
   are wired. *(§3.4, §5.6, §6)*

Two smaller findings that each cause a visible class of "it just didn't work":

4. **Timeouts are nested the wrong way round.** UTP will keep trying to connect for
   `MaxConnectAttempts 60 × ConnectTimeoutMS 1000 = 60 s` while `PartyInviteController` gives up at
   **10 s** (`PartyServices.prefab`; `Bootstrap.unity` overrides it to 30 s — the C# initializers
   say 8 s and are not the shipped values, a correction made while implementing) and starts tearing
   the client down — the late connect then races the shutdown (the B8 "phantom rejoin" shape).
   `DisconnectTimeoutMS 30000` means a dead peer holds the ready gate and its vessel for 30 s before
   anyone is told. *(§3.3, §5.5 — fixed in 8a0eaa0d)*

5. **The legacy PENDING acceptance handshake still runs.** With eager sessions the host's session id
   is always real, yet every Accept still writes `accepted_invite` to the lobby (one of the 60
   player-updates/min), with a full refresh before and after. It is dead protocol that costs budget
   exactly when four players are accepting at once. *(§3.5, §5.3)*

What is **right** and must not move: the two-level model, eager per-user Relay, single-writer SOAP,
`.AsMainThread()` at every await, `PartyStateMachine` as the only lifecycle authority,
`NetworkSceneObjectGuard.Sweep` before every join, the `maxPartySlots 6 / partyDisplaySlots 4`
split, and "every catch maps to a named recovery". *(§9)*

---

## 1. What was reviewed

| Area | Read |
|---|---|
| Orchestration | `Controller/Party/HostConnectionService.cs` (2 688 lines, fully), `PartyInviteController.cs`, `StateMachine/PartyStateMachine.cs` |
| Services | `Services/PartySessionService.cs`, `PresenceLobbyService.cs`, `LobbyRefreshScheduler.cs`, `AcceptanceSignalService.cs`, `LobbyPropertyWriter.cs`, `NetworkTransitionService.cs` |
| Transport / match | `Controller/Multiplayer/MultiplayerSetup.cs`, `ArcadeConfigSyncManager.cs`, `ServerPlayerVesselInitializer.cs`, `SpectatorSession.cs`, `Controller/Arcade/MultiplayerMiniGameControllerBase.cs`, `System/SceneLoader.cs`, `Assets/_Prefabs/CORE/NetworkManager.prefab` |
| Recovery / boot | `System/ReconnectService.cs`, `System/AuthenticationSceneController.cs`, `System/NetworkMonitor.cs`, `UI/Elements/DisconnectNotice.cs` |
| UI | `UI/Elements/FriendsListPanel.cs`, `PartyInviteNotificationPanel`, `Utility/DataContainers/HostConnectionDataSO.cs` |
| Docs | `Docs/PartySystem/*` (ARCHITECTURE, BUGS, TESTS, SPECTATOR, INVITE_ENHANCEMENTS, MPPM_SESSION_LOG), `Docs/PresenceSystem/*`, `Docs/MultiplayerArchitecture/ROADMAP.md` + dossier chapters 03/05/13/14/15/17/21/26, `Docs/OFFLINE_MODE.md` |
| SDK sources (compiled refs) | `com.unity.services.multiplayer@2.3.3` — `Session/SessionHandler.cs`, `Lobby/LobbyHandler.cs`, `MultiplayerService.cs`; `com.unity.services.friends@1.3.0` — `IFriendsService.cs`, `IMessagingService.cs` |
| Tests | 19 `[Test]`s under `Controller/Multiplayer/Tests/Editor/`, the `Party*Tests` under `Tests/Editor/` |

Industry references used for the "what is standard" column: Unity's own Boss Room reconnection
design (client GUID → slot reclaim, `ClientReconnectingState` with a bounded attempt count), the
Netcode manual's *Session management* and *Reconnecting mid-game* chapters, the UGS Lobby
*Config options* and *Rate limits* pages, the AWS Architecture Blog *Exponential Backoff and Jitter*,
and the Google SRE book *Handling Overload* (retry budgets, retry at one layer). Links in Appendix B.

---

## 2. The system as it stands

### 2.1 Layers

```
 UGS Lobby (presence lobby, 100 players, no Relay)   <- discovery + invite carrier (player properties)
      |  polled every 1.5 s / 0.75 s boosted; converge query every 4 s
 UGS Sessions + Relay (party session, 6 transport slots / 4 shown)  <- eager, one per user ("Always InParty")
      |  polled every tick too (RefreshAsync); PlayerLeaving/Deleted subscribed
 Netcode for GameObjects 2.13.3 host/client over UTP+Relay          <- NetworkManager.prefab, approval on
      |  OnClientDisconnect / OnTransportFailure
 HostConnectionService (orchestrator, Update()-driven)  <->  PartyStateMachine (7 states)
      |  single-writer SOAP: HostConnectionDataSO
 FriendsListPanel  [Invite] [Join] [Spectate]      PartyInviteNotificationPanel [Accept] [Decline]
```

### 2.2 The two buttons today

**Invite** (`FriendsListPanel.OnInviteClicked` :647 → `HostConnectionService.SendInviteAsync` :601):
guards offline / spectating / full; idempotent per target (re-click returns the in-flight invite);
`RefreshAsync` the presence lobby; append the payload to the local player's `invite_payloads`
property via `LobbyPropertyWriter.WriteAsync` (mutex → refresh → set → `SaveWithRetryAsync` →
refresh). The recipient discovers it on its next poll (`RefreshAsync` :1356 → invite scan).
Expiry is client-side only (`OUTGOING_INVITE_TIMEOUT_SECONDS 60` :105).

**Accept** (`PartyInviteController.AcceptInviteAsync` → `HostConnectionService.AcceptInviteAsync`
:781): `AcceptanceSignalService.PublishSignalAsync` (:219, writes `accepted_invite`) → **leave own
party session** (`ShutdownAsync` host, 5 s cap) → `NetworkSceneObjectGuard.Sweep` →
`PartySessionService.JoinByIdAsync` (:256) → seed roster → publish `joined_party` → `StartClient`
→ wait ≤ 8 s for `IsConnectedClient` → wait ≤ 10 s for join-ready.

**Join** (`OnJoinClicked` :716 → `PartyInviteController.JoinPartyAsync` → `RunClientJoinAsync` :279
→ `JoinPartyDirectAsync` :881): same as Accept minus the handshake write. Requires the target's
`HasJoinableSession` (derived from its published `partySession` / `partyCount` / `partyMax`
properties, i.e. up to one poll interval stale).

### 2.3 The polling engine

`HostConnectionService.Update()` (:429) asks `LobbyRefreshScheduler.ShouldFireNow` and fires
`RefreshAsync` (:1356), which per tick: refreshes the presence lobby, every 4th second converges to
the canonical lobby (:118), diffs online players, scans invite payloads, scans acceptance signals,
scans `joined_party`, refreshes the **party** session (`PartySessionService.RefreshAsync`), rebuilds
`PartyMembers`, and publishes party state if it changed (another property write → two more
refreshes). Cadence: 1.5 s ± 10 % jitter, 0.75 s for 15 s after any user action
(`LobbyRefreshScheduler.cs:66/73/88`). Error handling: benign (LobbyPatcher / stale index) →
ignore; rate-limit → back off `refreshIntervalSeconds × 2`; anything else ×3
(`MAX_REFRESH_ERRORS_BEFORE_RECONNECT` :106) → `ForceReset` → `Reconnecting` → presence rejoin with
3 → 60 s backoff.

What the SDK does underneath, unasked: `LobbyHandler.InitLobbyEventsAsync` (~:371) subscribes to
Wire for every lobby joined or created (it only falls back to its own polling when `m_UsePolling`),
`LobbyCallbacks` (:221) maps `PlayerDataChanged / PlayerJoined / LobbyChanged / LobbyDeleted /
KickedFromLobby` deltas onto the local lobby object, and `SessionHandler` re-raises them as the
`ISession` events listed in §0. **The game is therefore paying for push and then polling anyway**,
and the two write the same object.

### 2.4 Three views of "who is in the party"

1. `ISession.Players` of the party session (authoritative, event-driven, mostly unused);
2. `NetworkManager.ConnectedClients` (transport truth, only meaningful once connected);
3. the presence lobby's `joined_party` / `accepted_invite` properties, scanned every tick.

`RefreshPartyMembersAsync` reconciles them. The flicker this produces on join/leave is documented
as the reason `maxPartySlots` carries a spare seat (`HostConnectionDataSO.cs:62`, comment) — i.e. a
transport parameter was widened to absorb a reconciliation artefact.

### 2.5 Disconnect paths

| Trigger | Handler | Outcome |
|---|---|---|
| Netcode `OnClientDisconnect` (remote id, we are host) | `MultiplayerSetup.OnClientDisconnect` :498 → `ReconcilePartyMembersNow`; `ArcadeConfigSyncManager.HandleClientDisconnected` :404; `MultiplayerMiniGameControllerBase.HandleClientDisconnectedForReadyGate` :632; `ServerPlayerVesselInitializer.HandleClientDisconnectedForAITakeover` :834 → `ConvertPlayerToAI` :844 | Slot re-decided at once; vessel becomes AI **permanently** |
| Netcode `OnClientDisconnect` (local id, we are client) / `OnTransportFailure` :566 | `PartyInviteController.HandleHostLossAsync` :571 → `BounceToSoloMenuAsync` :202 → `RecoverFromFailedTransitionAsync` :253 | Destroy vessel → leave session → `ShutdownAsync` → load `Menu_Main` → `EnsurePartySessionAsync` (new own Relay). **Any** client-side loss is treated as host loss |
| Lobby refresh errors ×3 | `RefreshAsync` error matrix → `ForceReset` | Presence layer rebuilt (3→60 s backoff) |
| Session gone (404/403/Deleted) | `HandleDefiniteSessionGoneAsync` :1974 | Clear + `EnsurePartySessionAsync` |
| Reachability lost | `NetworkMonitor` 5 s poll of `Application.internetReachability` (:76) → `OnNetworkLost` | `DisconnectNotice` shown; `ApplicationStateMachine` → Disconnected; `CatalogManager` loads inventory from disk |
| User "Reconnect" | `ReconnectService.RunBootChainAsync` :114 | Reset party layer → shutdown NM → **reload Authentication scene** |
| Boot cannot reach InParty | `AuthenticationSceneController.LoadMainMenuNetworkedAsync` :732 — 3 × ≥15 s | **Offline local host** (the B24 landing zone) |

No path attempts to re-attach to the session or lobby the player was just in.

---

## 3. Root-cause classes (why the fixes never stuck)

### 3.1 Polling a push service at its rate limit → 429s, stale-index churn, false "gone"

Lobby limits as read on the UGS Lobby *Rate limits* page during this review (re-check before
tuning; Unity adjusts them): **Get 1 req/s/player · Query 1/s · Join 1/s · Update player 60/min ·
Create 20/min · Reconnect 1/s.** Per client at rest the engine issues 2 GETs per 1.5 s (presence +
party) = 1.33/s; boosted 2.67/s; plus a query every 4 s; plus 2 GETs around every write. Four
players inviting/accepting in the same ten seconds is therefore a *guaranteed* 429 burst — not a
flake. B24 is the one that reached the offline fallback; the others surface as the "sometimes the
roster is wrong for a few seconds" the team has been chasing.

The SDK applies Wire deltas to `Lobby` in `LobbyHandler.OnLobbyChanged` while our `RefreshAsync`
replaces the same object from a GET — the concurrent mutation behind B1 (`LobbyPatcher` errors)
and B6 (stale-index NRE). `INVITE_ENHANCEMENTS.md` lever L5 flagged "push-based presence" as risky
*because of* B1/B6; the causality is the other way round — **removing the polling removes the
second writer**.

### 3.2 Retries without an owner → amplification, misclassification, no budget

| Site | Classifier | Retry shape | Problem |
|---|---|---|---|
| `PartySessionService.cs:436/441` | structured 429 ✔ · host-conflict = message contains "host" or "NetworkManager" | rate 3 × 2 s exp; transient 5 × 1 s exp; conflict 2 × **0 s** | over-matching conflict class; no jitter |
| `LobbyPropertyWriter.SaveWithRetryAsync` :115 | "Too Many Requests" / "Index was out of range" | 3 × fixed 2000 ms (:138) | retries a 429 with a fixed delay → synchronised retry storm across 4 clients |
| `PresenceLobbyService.cs:196` | message-only | caller-dependent | misses structured `SessionError.RateLimitExceeded` |
| `MultiplayerSetup.cs:34` | message-only | — | same |
| `HostConnectionService.cs:2559` | message-only | counts toward `ForceReset` after 3 | **second layer** counting failures already retried below |

Google's SRE guidance: a request is retried at **one** layer, with a budget (≈10 % of requests per
minute) after which callers degrade instead of retrying; AWS: exponential backoff with **full
jitter**, because fixed or un-jittered delays re-synchronise clients that failed together — which
is exactly the 4-player accept storm.

### 3.3 Inverted timeout nest

| Layer | Value | Where |
|---|---|---|
| UTP connect window | 60 × 1 000 ms = **60 s** | `NetworkManager.prefab:99-100` |
| UTP disconnect detection | **30 s** (heartbeat 500 ms) | `:101 / :98` |
| NGO client buffer / spawn | 10 s / 10 s | `:59 / :71` |
| NGO scene load | 120 s | `:70` |
| PIC shutdown / connection / join-ready / spectate-ready | 5 / **10** (30 via `Bootstrap.unity`'s override) / 30 / 45 s | `PartyServices.prefab:83-85`, `Bootstrap.unity` (the C# initializers read 2 / 8 / 10 and were NOT the shipped values — corrected 2026-10-07) |
| SceneLoader client follow | 90 s | `SceneLoader.cs:49` |
| Lobby service (dashboard defaults) | active lifespan 30 s · disconnect removal **120 s** · host-migration 120 s | UGS Lobby *Config options* |

The app-level wait (10–30 s) sits *inside* a transport that keeps retrying for 60 s: when PIC times
out and tears down, UTP may complete the connect into a NetworkManager that is shutting down (B8's
phantom-rejoin shape; the B5/B14/B16 stray-NetworkObject family grew out of the same window).
Inverted the other way, 30 s to notice a dead peer is three times the industry norm for a casual
4-player game (8–12 s detection, with a *separate, longer* reconnection grace).

### 3.4 No reconnection grace → every blip is a reset

A client-side `OnClientDisconnect` is always interpreted as host loss (§2.5). Nothing distinguishes
"my Wi-Fi hiccuped" from "the host quit", although `NetworkMonitor` and the session's `StateChanged`
can tell them apart. The unused building blocks:

- `PartyStateMachine.Reconnecting` — exists, only entered from lobby `ForceReset`, never from a
  transport loss.
- `ISession.ReconnectAsync()` (`SessionHandler.cs:856` → `LobbyHandler.ReconnectToLobbyAsync`) and
  `MultiplayerService.ReconnectToSessionAsync(sessionId)` (`MultiplayerService.cs:80`) — never called.
- Lobby keeps a Wire-disconnected player for 120 s before removal (default) — the window in which
  a reconnect is free.
- `Player.NetUgsPlayerId` (`Player.cs:82`) — a stable identity per vessel; the Boss Room pattern
  keys slot-reclaim on exactly this, never on `clientId` (which is re-issued).
- `SpectatorSession.ArmApprovalPayload` (:109) — proves the approval payload already carries an
  app token; the UGS PlayerId can ride there so the host maps `clientId → player` at approval time.
- `ConvertPlayerToAI` (:844) — already the right *mechanism* for a vanished player; it only lacks a
  grace window and a hand-back.

### 3.5 Dead protocol on the hot path

`AcceptanceSignalService` (`PENDING` sentinel, :3) existed for the lazy-session era: the acceptor
signalled before the host had a real session id, and `WaitForRealSessionIdAsync` (:262) polled every
400 ms for it. Eager sessions made the id always real; `RepublishWithRealIdAsync` is now a no-op,
but `PublishSignalAsync` (:219) still runs on every Accept: one player-update (of 60/min) and two
GETs, right when the budget is scarcest. The host does not need it — it sees `PlayerJoined` on its
own party session.

### 3.6 Membership reconciled by scan (§2.4)

Three views and a per-tick merge produce the flicker that forced transport headroom and that makes
`HasJoinableSession` on the Join button up to a poll interval stale. The party session's own
`PlayerJoined / PlayerLeft / PlayerPropertiesChanged` events are the one roster the UI should read.

### 3.7 Create-before-validate on Accept/Join

Both flows **leave the local party session (and shut down the local host) before** trying to join
the target. A target that is full, gone, or unreachable therefore costs the player their own
session and a full `EnsurePartySessionAsync` rebuild (5 s shutdown cap, sweep, create) — the
`HostConnectionDataSO.cs:62` comment describes precisely this: "session-full … propagates straight
to a bounce". A single GET on the target (or the already-pushed `partyCount` property) before
teardown removes the whole class.

### 3.8 UI without in-flight state

`SendInviteAsync` is idempotent per target and `PartyInviteController._transitioning` (:24) rejects a
second join — but the **buttons don't know**: a second tap on Invite/Join/Accept is swallowed
silently, and a stale invite (sender left, party filled) is discovered only when the join fails.
Industry norm: the control reflects the operation's state (disabled + spinner while in flight,
"Sent ✓", "Full", "Offline") and the operation is single-flight underneath.

---

## 4. Industry reference points (the "standard" column)

| Topic | Standard shape | This codebase |
|---|---|---|
| Lobby / presence updates | Push (UGS Wire, Steam lobby callbacks, EOS lobby notifications, Photon room events) + a slow reconciliation poll (30–60 s) as safety net | Poll at 0.75–1.5 s; push ignored |
| Invite delivery | A *message* to a player (Friends/relationship messaging, platform invite API), not a mutation of shared state read by everyone | Mutation of own lobby properties, read by everyone's poll |
| Retries | One policy: classify → exponential backoff with full jitter → honour `Retry-After` → single-flight → retry budget → retry at one layer | Four classifiers, three loops, fixed delays, two layers |
| Peer-loss detection | 8–12 s transport timeout; heartbeat ≤ 1 s | 30 s |
| Reconnection | Grace window (30–60 s) with slot reserved by stable player id; bounded reconnect attempts (Boss Room: `ClientReconnectingState`, N attempts, GUID-keyed `SessionManager`) | None; bounce to solo / boot-chain rerun |
| Timeouts | Nested inner < outer; app timeout ≥ transport connect window | Inverted (8 s inside 60 s) |
| Membership | One authority per layer: session players (lobby), connected clients (match) | Three views merged by scan |
| Degradation | 429 → slow down, never → lose state or go offline | 429 can count toward `ForceReset`; reached offline fallback once (B24) |
| UI | Controls reflect operation state; operations idempotent | Service-level idempotency only |
| Observability | Per-operation correlation id; 429 / retry / reconnect counters surfaced | Verbose channel logs; no counters |

---

## 5. Target design — "push first, poll as safety net" (kept simple)

Everything here *removes* more code than it adds. Phase order and acceptance criteria are in §7.

### 5.1 Presence transport: subscribe, don't poll

- On presence-lobby join/create, subscribe to `ISession.PlayerPropertiesChanged`, `PlayerJoined`,
  `PlayerLeft`, `SessionPropertiesChanged`, `Deleted`, `RemovedFromSession`, `StateChanged`
  (the SDK already holds the Wire subscription; this is wiring, not a new connection).
- Run the existing scans (invite payloads, online diff, party-state publish) **from the event
  handlers**, marshalled with `.AsMainThread()`.
- Keep `RefreshAsync` as a **reconciliation** pass every 20 s ± 25 % jitter (configurable in an SO),
  not a transport. Delete the 0.75 s boost window and the 4 s converge cadence (converge once at
  join and on `Deleted`/`RemovedFromSession`).
- `LobbyPropertyWriter.WriteAsync`: set → save. Drop the pre-save and post-save `RefreshAsync`;
  the SDK already keeps the local lobby current from deltas and the service rejects stale versions
  with a classifiable error (§5.4 handles that with one re-read + one retry).
- Party session: stop `RefreshAsync` per tick; roster comes from its own
  `PlayerJoined/PlayerLeft/PlayerPropertiesChanged`.
- Budget after this: ≤ 4 GETs/min/client at rest (vs ≈80 today). The 1 req/s limits become
  unreachable in normal play; a 429 becomes a real anomaly worth a warning.

B1/B6 answer: the SDK remains the only writer of the `Lobby` object between reconciliations, so the
concurrent-mutation defect goes away rather than getting a new entry point.

### 5.2 Invite carrier

Keep the lobby player-property payload (`invite_payloads`) as the carrier — it works, it is
visible to tooling, and it needs no new service. The *recipient* learns of it from
`PlayerPropertiesChanged` for the sender (sub-second) instead of from a scan.

Optional later (Phase 4): Friends 1.3.0 is already in the project and already used for presence;
`IMessagingService.MessageAsync<T>(targetUserId, payload)` + `IFriendsService.MessageReceived` is a
direct, targeted push with **no lobby write at all** — the industry shape ("an invite is a
message"). Not needed to reach the §7 acceptance criteria; listed so nobody rebuilds the carrier
twice.

### 5.3 Delete the PENDING handshake

Remove `AcceptanceSignalService.PublishSignalAsync` from `AcceptInviteAsync`, the acceptance scan
from the refresh pass, and `WaitForRealSessionIdAsync`. The host learns of the acceptor from the
party session's `PlayerJoined`; the "Pending…" affordance on the sender's tile stays client-side
(`OUTGOING_INVITE_TIMEOUT_SECONDS`). Keep `joined_party` **only** if something still reads it for
display; otherwise delete it too (one fewer write per join).

### 5.4 One request policy: `UgsRequestPolicy`

A single static helper, config in an SO (cooldowns and budgets belong in config, per CLAUDE.md):

```csharp
// Sketch — the whole retry surface of the party layer, in one place.
public enum UgsFailureClass { RateLimited, Transient, Conflict, Full, Gone, Fatal }

public static class UgsRequestPolicy
{
    public static UgsFailureClass Classify(Exception e) =>
        e switch
        {
            SessionException { Error: SessionError.RateLimitExceeded }            => UgsFailureClass.RateLimited,
            RequestFailedException { ErrorCode: 429 }                             => UgsFailureClass.RateLimited,
            SessionException { Error: SessionError.SessionFull }                  => UgsFailureClass.Full,
            SessionException { Error: SessionError.SessionNotFound
                                   or SessionError.SessionDeleted
                                   or SessionError.Forbidden }                    => UgsFailureClass.Gone,
            SessionException { Error: SessionError.LobbyAlreadyExists
                                   or SessionError.AlreadyMember }                => UgsFailureClass.Conflict,
            RequestFailedException { ErrorCode: >= 500 }                          => UgsFailureClass.Transient,
            OperationCanceledException                                            => UgsFailureClass.Fatal,
            _ when IsNetworkTimeout(e)                                            => UgsFailureClass.Transient,
            _                                                                     => UgsFailureClass.Fatal,
        };

    // Full-jitter exponential backoff (AWS): delay = random(0, min(cap, base * 2^attempt)).
    public static async UniTask<T> ExecuteAsync<T>(string operationKey, Func<UniTask<T>> call,
                                                   UgsRequestPolicySO cfg, CancellationToken ct)
    {
        // 1. single-flight: an identical operationKey in flight returns the same task
        // 2. budget: if cfg.RetryBudget.TryConsume() fails -> throw after the FIRST failure (no retry)
        // 3. loop attempts 0..cfg.MaxAttempts: Classify -> RateLimited/Transient retry with jitter
        //    (RateLimited uses the longer base and honours Retry-After when the SDK exposes it),
        //    Conflict -> one re-read then one retry, Full/Gone/Fatal -> throw immediately
        // 4. CSDebug.LogVerbose(CSLogChannel.Party, $"{operationKey} attempt {n} {cls} {delay}ms")
    }
}
```

Rules the helper enforces, so no call site can get them wrong again:

| Class | Retry? | Backoff | Counts toward `ForceReset` / offline? |
|---|---|---|---|
| RateLimited | ≤ 3 | full jitter, base 1 s, cap 8 s (`Retry-After` if present) | **never** |
| Transient | ≤ 3 | full jitter, base 500 ms, cap 4 s | only after the helper gives up |
| Conflict | 1 (after one re-read) | — | no |
| Full | no | — | no → UI "Party is full" |
| Gone | no | — | no → `HandleDefiniteSessionGoneAsync` |
| Fatal | no | — | yes |

- **Retry at one layer**: `PartySessionService`, `PresenceLobbyService`, `LobbyPropertyWriter` call
  through the helper; `HostConnectionService.RefreshAsync` and `MultiplayerSetup` **stop retrying
  and stop classifying** — they only react to the class the helper throws.
- **Budget**: ≤ 10 retries/min/client across all UGS calls; when spent the layer *degrades*
  (lengthen reconciliation, drop boost) and never recreates a session.
- **Single-flight keys**: `invite:{targetId}`, `join:{sessionId}`, `accept:{inviteId}`,
  `ensure-party`, `presence-join`. Replaces the ad-hoc per-target idempotency and `_transitioning`
  with one mechanism the UI can also query ("is `join:*` in flight?").
- Delete `IsHostConflictException` (message contains "host") — the only legitimate conflict is a
  structured `SessionError`; the NetworkManager-still-listening case is a precondition
  (`NetworkTransitionService.ShutdownAsync` must have completed), not a retry.

### 5.5 Nest the timeouts (prefab + two constants; needs `/verify-unity` + MPPM)

| Setting | Today | Proposed | Why |
|---|---|---|---|
| UTP `MaxConnectAttempts` × `ConnectTimeoutMS` | 60 × 1 000 = 60 s | 10 × 1 000 = **10 s** (8a0eaa0d) | transport window must end *before* the app gives up |
| PIC `connectionTimeout` | 10 s (30 s via the Bootstrap override) | kept | already ≥ the 10 s transport window; `WaitForClientConnectionAsync` now returns the moment the client stops listening instead of sitting out the remainder (8a0eaa0d) |
| UTP `DisconnectTimeoutMS` | 30 000 | **10 000** (8a0eaa0d) | 8–12 s is the norm; heartbeat 500 ms already supports it. Trade-off until Phase 3's grace: a phone backgrounded > 10 s is now dropped where 30 s used to survive |
| UTP `HeartbeatTimeoutMS` | 500 | 500 | fine |
| PIC `joinReadyTimeout` | 30 s | kept | already outside NGO `ClientConnectionBufferTimeout 10` + `SpawnTimeout 10` worst case |
| Reconnect grace (new, §5.6) | — | **45 s** in match · 30 s pre-match | deliberately *longer* than detection, *shorter* than lobby removal (120 s) |

Also: `RecoverFromFailedTransitionAsync` must `ShutdownAsync` with a cancellation that aborts a
pending UTP connect, and only then sweep — closes the B8 window regardless of the numbers.

### 5.6 Reconnection grace instead of terminal bounce

**Client side** (new path in `PartyInviteController`, state `Reconnecting` from
`PartyStateMachine`):

1. `OnClientDisconnect(localId)` / `OnTransportFailure` → **classify first**: if the party
   session's `StateChanged` says *Deleted/RemovedFromSession* or the host's `PlayerLeft` arrived →
   host loss → existing bounce. Otherwise → `Reconnecting`.
2. Wait for reachability (`NetworkMonitor` is only a hint; also try immediately).
3. Up to 3 attempts with jitter, within the 45 s grace: `MultiplayerService.ReconnectToSessionAsync
   (sessionId)` (or `ISession.ReconnectAsync()` if the object is still held) → re-read the Relay
   join data from the session → `StartClient` with the UGS PlayerId in the approval payload.
4. Success → the host hands the vessel back (below); UI pill "Reconnected". Failure → the existing
   `BounceToSoloMenuAsync`, now with a reason the UI can show ("Connection lost").

**Host side** (`ServerPlayerVesselInitializer`, `ArcadeConfigSyncManager`,
`MultiplayerMiniGameControllerBase`):

1. `OnConnectionApprovalCallback` reads the UGS PlayerId from the payload (same mechanism as the
   spectator token) and keeps `ugsPlayerId → clientId`.
2. On remote disconnect: `ConvertPlayerToAI` as today, **but** record `ugsPlayerId → vessel, until
   = now + grace`; pre-match, mark the slot *reconnecting* instead of re-deciding the ready gate.
3. On approval of a client whose UGS id has a live reservation: re-own the vessel (NGO
   `ChangeOwnership`), stop the AI pilot, restore the ready-gate slot. After `until`: finalise as AI /
   free the slot (today's behaviour).

**Lobby side**: nothing to build — the service keeps the disconnected player 120 s; the SDK's
`ReconnectAsync` is the call. `HostConnectionService` must stop treating a Wire disconnect as a
reason to `EnsurePartySessionAsync` a *new* session while a reconnect is possible.

**Host loss stays a bounce** (host migration is on the roadmap, not in this review's scope) — but
with 10 s detection, an explicit reason, and a one-tap **"Rejoin ⟨host⟩'s party"** when the host
comes back (the host recreates eagerly; presence then shows `HasJoinableSession`; the existing
Join-direct path is the rejoin).

### 5.7 One roster

`PartyMembers` is rebuilt from the party session's `Players` on its `PlayerJoined / PlayerLeft /
PlayerPropertiesChanged`, and from `ConnectedClients` **only** while in a match. The `joined_party`
scan goes. Keep `maxPartySlots 6 / partyDisplaySlots 4` as is — headroom is cheap insurance and the
UI already reads the display number.

### 5.8 UI contract for the two buttons (and Accept)

| Control | Enabled when | While in flight | Result states |
|---|---|---|---|
| **Invite** | target online · target not in a match that disallows joins · `HasOpenDisplaySlots` · no outstanding invite to this target (60 s) | disabled + spinner ("Inviting…") | "Sent ✓" (60 s) → auto-flips to member on `PlayerJoined`; "Declined"; "Expired" |
| **Join** | target `HasJoinableSession` · `partyCount < partyDisplaySlots` (push-fresh) · no `join:*` in flight | disabled + "Joining…" with **Cancel** (cancels the single-flight token; own session untouched until validation passed) | success → lobby; `Full` → "Party is full"; `Gone` → "Party no longer available"; `Transient` → "Couldn't reach the party — try again"; timeout → same |
| **Accept** (notification) | invite not expired · sender still online & joinable (push-fresh) | disabled + "Joining…" | as Join; stale invite auto-dismisses on sender `PlayerLeft` / `partyCount` full |
| **Spectate** | match in progress · spectator slot | disabled + "Connecting…" (≤ 45 s) | as Join |

Mechanics: one `_busy` `CancellationTokenSource` per panel (double-tap guard at the control),
single-flight keys in `UgsRequestPolicy` (guard at the service), four user-facing messages mapped
from `UgsFailureClass` — and **no path from a button to the offline fallback**.

### 5.9 Observability (cheap, do first)

Counters on `NetworkDiagnostics` / the NetDiag overlay: lobby GETs/min, 429/min, retries/min,
reconciliation drift (events vs poll disagreements), reconnect attempts/successes, time-to-detect
peer loss. One `operationKey` on every `CSLogChannel.Party` verbose line. Without these, "it felt
smoother" is the only acceptance test — and it is how the last fifteen fixes were judged.

---

## 6. Edge-case matrix — client suddenly stops / loses network, at every stage

Legend: *Today* = behaviour on `94c14ccc` with anchors; *Target* = after §5. "Bounce" =
`BounceToSoloMenuAsync` → `RecoverFromFailedTransitionAsync` → solo menu with a new own session.

### 6.1 The local player (client-side events)

| # | Stage × event | Today | Target |
|---|---|---|---|
| 1 | **Menu, idle (solo InParty)** — network drops 10 s | `NetworkMonitor` flips within 5 s; refresh errors ×3 → `ForceReset` → `Reconnecting` → presence rejoin 3→60 s; party session Wire drops; nothing calls `ReconnectAsync`; may recreate the own session (`ShutdownAsync` 5 s + create) | `StateChanged`→Disconnected → wait for reachability → `ReconnectAsync()` ≤ 3 tries; **no recreate** inside the 120 s lobby window; "Reconnecting…" pill |
| 2 | **Invite sent** — sender loses network | Payload stays in sender's properties until lobby removal (120 s); recipient may accept into a host that is gone → join fails/8 s timeout → **bounce**, own session lost | Recipient sees sender `PlayerLeft`/offline → invite auto-dismissed; if it does accept: validate target first → `Gone` → toast, own session intact |
| 3 | **Accepting / joining** — app killed mid-way | `accepted_invite` lingers → host scans see a phantom "accepted" for up to 120 s; roster/"Pending" UI stale | No handshake artefact (§5.3); host roster is `PlayerJoined` only; sender's "Sent ✓" expires at 60 s client-side |
| 4 | **Joined, pre-match ready gate** — network drops 10 s | Host: nothing for 30 s (UTP), then re-decides the gate without the player. Client: `OnClientDisconnect` → treated as **host loss** → bounce → new own session | Detect at 10 s; client `Reconnecting` (≤ 30 s); host holds the slot as *reconnecting*; back in place with no UI churn; else bounce with reason |
| 5 | **Mid-match** — network drops 10–20 s | After 30 s host converts vessel to AI **permanently**; client bounces to solo menu | Detect 10 s → AI takes the stick; client reconnects within 45 s → vessel handed back by UGS id; "Reconnecting (12 s)…" |
| 6 | **Mid-match** — crash / force-quit | As #5, no return | Detect 10 s → AI; after 45 s reservation freed; everyone's roster updates from `PlayerLeft` (no scan) |
| 7 | **Post-match → menu** — network drops | Scene follow 90 s; `LeavePartyAsync` bounded 3 s; usually fine | Same + Reconnecting pill; no own-session recreate inside 120 s |
| 8 | **Mobile sleep/wake** (background > 30 s) | Wire + transport drop → bounce; refresh errors → `ForceReset`; possible offline-fallback-shaped recovery | On resume: reachability → `ReconnectAsync`; lobby removed (> 120 s) → `EnsurePartySessionAsync`; **never** offline fallback from one failure |
| 9 | **Double-tap** Invite / Join / Accept | Service swallows the second call (idempotent / `_transitioning`); button gives no feedback | Control disabled + spinner on first tap; single-flight returns the same task |
| 10 | **Join when the last seat was just taken** | Both joiners `JoinByIdAsync`; loser gets session-full after already leaving its own session → **bounce** | `partyCount` is push-fresh so the button is usually already disabled; if raced: `Full` → toast, own session intact |
| 11 | **Accept a stale invite** (sender left / party filled) | Discovered on join failure → bounce | Dismissed on `PlayerLeft`/full before the tap; else `Gone`/`Full` toast |

### 6.2 The host (what the other three experience)

| # | Stage × event | Today | Target |
|---|---|---|---|
| 12 | **Host crash or network loss, pre-match** | Clients notice after 30 s (UTP) → each bounces → each creates its own Relay (3 creates in a second; within 20/min) | Detect 10 s; "Host disconnected"; host recreates eagerly on return → presence shows joinable → one-tap **Rejoin**; host migration remains roadmap |
| 13 | **Host crash mid-match** | Same, after 30 s; match ends | Detect 10 s; results screen "Host lost" → menu; roadmap: migration |
| 14 | **Host's transport dies silently** (NAT rebind, Relay allocation lost) | Join code dead; every joiner times out at 8 s → bounce | Host `OnTransportFailure` → `EnsurePartySessionAsync` → republish session id; joiners get `Transient` → "Couldn't reach — try again", own session intact |
| 15 | **Four players invite/accept in the same 10 s** (429 burst) | Four classifiers disagree; some paths count 429 toward `ForceReset`; in boot it reached offline (B24) | One classifier; 429 → jittered backoff, never `ForceReset`/offline; UI stays "Working…" |
| 16 | **Host kicks a member who is mid-reconnect** | Kick writes + member's reconnect race on the session | Kick cancels the reservation; `RemovedFromSession` on the member aborts its reconnect → bounce with "Removed from party" |

---

## 7. Rollout — four phases, each independently shippable and MPPM-testable

Each phase: C# through `/verify-unity`, one 4-player MPPM run against §8, counters from §5.9 in the
PR.

| Phase | Change | Deletes | Acceptance |
|---|---|---|---|
| **0 — see it** | §5.9 counters; `UgsRequestPolicy.Classify` + tests; replace the 4 classifiers with the one | 3 classifiers | counters visible in NetDiag; `CSDebugTests`-style unit tests for every class |
| **1 — request discipline** | `UgsRequestPolicy.ExecuteAsync` in PSS / PLS / LPW; HCS + MultiplayerSetup stop retrying; §5.3 handshake removal; §5.5 timeout nest; §3.7 validate-before-teardown | `SaveWithRetryAsync`, PSS loops, `IsHostConflictException`, `AcceptanceSignalService` publish path | 4 players · 10 min · invite/accept/leave every 10 s → **0** 429s, **0** `ForceReset`, **0** offline fallbacks; join failures never cost the own session |
| **2 — push first** | §5.1 events; reconciliation at 20 s; §5.7 single roster; §5.8 button contract | per-tick refreshes, boost window, 4 s converge, pre/post-save refreshes, `joined_party` scan | invite visible on recipient < 1 s p95; GETs ≤ 4/min/client at rest; roster never shows a count the session doesn't; no B1/B6 log lines in a 30 min run |
| **3 — grace** | §5.6 client reconnect + reversible AI takeover + approval-payload identity | nothing (adds one state path) | pull a client's network 15 s mid-match → vessel handed back, no menu seen; 60 s → AI permanent, roster correct; pre-match 20 s → slot kept |
| **4 — optional** | Friends messaging as invite channel; host migration (roadmap) | lobby invite carrier | only if Phase 2 counters still show lobby-write pressure |

Phases 0–1 are the "stop the bleeding" set and need no design discussion. Phase 2 is the one that
makes four players feel instantaneous. Phase 3 is what "a client suddenly stops or loses network"
actually needs.

**Landed (2026-10-07, `Ys-bleeding-edge`):** Phase 0 in `931dcd51` (one classifier, counters; the
review undercounted — there were FIVE classifier copies, and a test reflected into one by name);
Phase 1 in `0521b858` (the executor; also fixed the classifier commit's single-flight, which could
not serve two concurrent awaiters), `d31e01b2` (PENDING handshake deleted — REFACTOR.md D1 closed),
`12e2cb1b` (`JoinTargetValidator` pre-flight before teardown, §3.7 / §5.8) and `8a0eaa0d` (UTP
10 s connect window / 10 s disconnect, early exit on transport give-up). Verified without the
Editor: `unity_refcompile` 0 project errors on every commit, the shipped edit-mode tests executed
headlessly (policy, telemetry, validator, accept-flow), the textual gates. **Not yet run:** the
4-player MPPM acceptance (§8 T1–T5, T8) — recorded in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

---

## 8. Four-player MPPM test plan

Run with MPPM 2.0.2 (one host + three virtual players), `CSLogChannel.Party` verbose ON, NetDiag
counters visible. Every scenario has a **pass** line; "no red console" is implied for all.

| ID | Scenario | Pass |
|---|---|---|
| T1 | Host invites P2, P3, P4 within 5 s; all accept within 10 s | roster 4/4 on all four screens < 2 s after the last accept; 0 × 429 |
| T2 | P2, P3, P4 **Join-direct** the host simultaneously (no invites) | all seated; order irrelevant; 0 bounces |
| T3 | Double-tap Invite / Join / Accept on each client | exactly one operation per tap burst; button disabled + spinner visible |
| T4 | Party 4/4; P5 (second MPPM run) presses Join | "Party is full" toast; P5 keeps its own session (no `EnsurePartySessionAsync` log) |
| T5 | P3 force-quits during Accept (between leave-own and connect) | host roster never shows P3; sender's "Sent ✓" expires at 60 s; no stale "accepted" |
| T6a | Mid-match, cut P2's network 15 s (`Network Simulator` / airplane) | AI drives P2's vessel; P2 sees "Reconnecting…"; vessel handed back; no menu |
| T6b | Same, 60 s | P2 bounced with reason; host vessel stays AI; roster 3 humans |
| T7a | Host force-quit in lobby | clients see "Host disconnected" ≤ 12 s; each lands in a solo menu with its own session; host returns → "Rejoin" works |
| T7b | Host force-quit mid-match | ≤ 12 s to results/menu for all; no stuck scene load (SceneLoader 90 s never reached) |
| T8 | Stress: all four spam Invite/Cancel for 60 s | 429 counter may rise; `ForceReset` = 0; offline fallback = 0; UI never stalls > 2 s |
| T9 | P4 sleeps the device 40 s in lobby, wakes | P4 back in the party without re-auth; if > 120 s, clean rebuild with no error dialog |
| T10 | P2 accepts an invite whose sender (P3) left 2 s earlier | notification auto-dismissed, or toast "Party no longer available"; P2 keeps its session |
| T11 | Ready gate: P3 drops 20 s pre-match, returns | slot held as *reconnecting*; gate re-decides only after grace |
| T12 | Kick P4 while P4 is mid-reconnect | P4 lands in solo menu with "Removed from party"; host roster 3/4 |

Automated (edit-mode, no editor dependencies): `UgsRequestPolicy` classification table, backoff
bounds (never > cap, never 0 after attempt 0 for RateLimited), budget exhaustion, single-flight
coalescing; `PartyStateMachine` transitions for `Reconnecting` from `InParty` / `JoiningParty` and
back; a fake-`ISession` roster test proving `PartyMembers` follows events without a refresh.

---

## 9. What not to change (and why the LOCKED items are right)

- **Eager per-user Relay** — the only reason Join-direct can be a single hop and the reason host
  loss is survivable at all (everyone already has a session to fall back to). Cost: one Relay
  allocation per online player; worth it at this scale.
- **Two-level model** — the presence lobby's 100-player ceiling and the party session's 6 are
  different problems; merging them was the pre-B2 design.
- **Single-writer SOAP / `.AsMainThread()` / `PartyStateMachine` authority** — every event handler in
  §5.1 and every reconnect path in §5.6 lands on these; they are what make a push model safe here.
- **`NetworkSceneObjectGuard.Sweep`** — keep before every `StartClient`; §5.5 just removes the race
  that kept producing things to sweep.
- **Not now**: host migration, Netcode 3 / Unity 6.7, dedicated servers, a custom relay. None are
  needed for four friends to join each other without errors; all are on `ROADMAP.md` where they belong.

---

## 10. Re-check after the 2026-10-08 bleeding-edge merge

`Ys-bleeding-edge` took 251 upstream commits (afd66621) on 2026-10-08, merged twice in parallel by
two sessions (dcead731 landed; the other was folded in as 226448c0). The Phase 0-1 work was then
re-checked against the merged tree, and the multiplayer test suites were **executed** for the first
time - not just compiled - on Prisma's engine (`Tools/Build/prisma_edit_mode_tests/run.sh`).

**What ran, on 280c0475 + the runner commit:**

| Check | Result |
|---|---|
| `unity_refcompile` player config | 0 project errors, 0 unverified (95 assemblies) |
| `unity_refcompile` editor config | 0 errors in files changed since the merge base |
| `check_ugs_request_discipline.py` (new, b1aa0a0c) | OK, 1933 files; negative control on the pre-merge tree reports the 2 bypasses + 6 scaled waits below |
| Edit-mode suites on Prisma's engine, 35 files | **549 / 559 pass.** Every party, presence, invite, lobby, spectator, UGS-policy, join-validator, bootstrap and app-state suite is green (detail below) |
| Other `check_*.py` gates | 25 / 27; the two red ones fail identically on bleeding-edge (row F6) |

Multiplayer suites, all green: UgsRequestPolicy 72, UgsRequestTelemetry 5, JoinTargetValidator 14,
PartyInviteSystem 96, PartyInviteController 8, PartyInviteData 7, PartyPlayerData 11, PartyRoster 12,
PartyAcceptFlow 4, SpectatorSession 10, ArcadeLobbySnapshot 5, HostConnectionDataSO 17,
ServerPlayerVesselInitializerWithAI 15, DisconnectNotice 5, ApplicationStateMachine 30,
ApplicationLifecycleManager 11, BootstrapConfigSO 9, NetworkExplodeParams 2, EcologyNetworkAuthority 4,
IRoundStatsCleanup 11.

**Findings.**

| # | Finding | Status |
|---|---|---|
| F1 | **Nothing enforces the 4-player party size.** The session holds 6 (transport headroom); every 4-check runs on the joining/inviting client against polled data; two Joins on a 3/4 party inside one presence refresh seat a fifth. `SendInviteAsync`'s backstop checks 6. | 🟡 fixed 2026-10-08 (one party size, 4: the session's seat count, so UGS refuses a fifth) - **B25**, needs the MPPM retest |
| F2 | Two UGS calls still bypassed `UgsRequestPolicy` after Phase 1a: the canonical-lobby converge join, and the party session's name/avatar save. Neither had a retry loop, which is what the Phase 1 census counted. | ✅ fixed b1aa0a0c, and gated (R1) |
| F3 | Upstream c2825eb4 moved every backend wait to unscaled time (Menu_Main screens set `timeScale = 0`). The policy's own back-off was already unscaled; two settle waits lost the flag in conflict resolution and got it back. | ✅ in the merge, gated (R5) |
| F4 | The landed merge dcead731 dropped upstream 1b5c4522 (InitializeAfterDelay is cancelled when its controller is destroyed - the host-loss bounce raised InitializeGame into the next scene), dropped the thumb controls' inert-until-initialised guard, and declared `MainMenu_To_Authenticating_Succeeds_Reconnect` twice (CS0111 - the whole editor test assembly would not compile). | ✅ fixed 226448c0 |
| F5 | Three `PartyInviteSystemTests` asserted `HasOpenSlots == false` at 4 members - the rule from before the capacity split - so they failed against the shipped design (in Unity too). | ✅ fixed 280c0475; negative-controlled |
| F6 | Upstream's own red gates: `check_fauna_replication_seam` (a heuristic false positive on the NCA lizard's `HealthBlock` hit prism, which carries no NetworkObject) and `check_vessel_on_vessel_motion` (`VesselImpulseByExplosionEffectSO` from the Grizzly merge moves an opposing vessel). | open, upstream's - not multiplayer defects |
| F7 | Prisma's live build of this branch does not compile: 11 engine API gaps, nine from upstream gameplay code and two (`SessionError.TransportComponentMissing/TransportInvalid`) from this review's own classifier. Filled in a throwaway worktree for the test run (`gapfill.py`); belongs in a port session. | open, port |
| F8 | Non-multiplayer failures in the run, each pre-existing on bleeding-edge's and the pre-merge branch's assets: SparrowCombatTier 2 (rocket costs 25 prisms where the test wants 50; the rocket-blast hit effect is wired into a container the SkyBurst prefab does not use), RaceRankToastDriver 4 (the driver posts NewRaceLeader as well as Overtake when a pass takes the lead). Harness, not game: SceneTransitionManager 3 (main-thread identity on NUnit's thread), AppManagerBootstrap 1 (Unity-Editor-only "Destroy may not be called from edit mode"). | open, owners of those systems |

**Re-run it:** `bash Tools/Build/prisma_edit_mode_tests/run.sh` (needs the .NET 10 SDK; the script
header says how to install it). It tests this checkout's `Assets/_Scripts`, committed or not.

## Appendix A — Evidence anchors

| Claim | Where |
|---|---|
| Polling cadence 1.5 s / 0.75 s boosted for 15 s, ±10 % | `LobbyRefreshScheduler.cs:66, :73, :88` |
| `Update()` drives `RefreshAsync`; converge every 4 s; errors ×3 → reset | `HostConnectionService.cs:429, :1356, :118, :106` |
| Invite 60 s client-side expiry; 4 s session-creation grace | `HostConnectionService.cs:105, :133` |
| Message-only rate-limit classifiers | `HostConnectionService.cs:2559`, `PresenceLobbyService.cs:196`, `MultiplayerSetup.cs:34` |
| Structured classifier + over-matching host-conflict | `PartySessionService.cs:436, :441` |
| Fixed 2000 ms ×3 property-save retry | `LobbyPropertyWriter.cs:115, :138` |
| Accept writes `accepted_invite` (PENDING protocol) | `AcceptanceSignalService.cs:3, :219, :262` |
| Leave-own-before-join; Join-direct path | `HostConnectionService.cs:781, :881` |
| Eager create surface | `HostConnectionService.cs:1166` |
| Only `PlayerLeaving` / `Deleted` subscribed | `PartySessionService.cs:207, :272`; `HostConnectionService.cs:2496`; `MultiplayerMiniGameControllerBase.cs:154-155` |
| Client-side disconnect = host loss → bounce | `MultiplayerSetup.cs:498, :566`; `PartyInviteController.cs:571, :202, :253` |
| PIC timeouts 5 / 10 (30 override) / 30 / 45 s; `_transitioning` | `PartyServices.prefab:83-85`, `Bootstrap.unity` (connectionTimeoutSeconds override), `PartyInviteController.cs` (spectate 45 s, `_transitioning`) |
| Permanent AI takeover | `ServerPlayerVesselInitializer.cs:834, :844` |
| Ready gate re-decided immediately | `ArcadeConfigSyncManager.cs:404`; `MultiplayerMiniGameControllerBase.cs:632` |
| Boot-chain rerun; offline fallback after 3 × ≥15 s | `ReconnectService.cs:114`; `AuthenticationSceneController.cs:732` |
| Reachability poll 5 s | `NetworkMonitor.cs:76` |
| Scene-follow 90 s | `SceneLoader.cs:49` |
| Transport: protocol 9, tick 30, buffer 10, approval on, scene 120, spawn 10; UTP 500 / 1000 × 60 / 30000 | `NetworkManager.prefab:51, :58, :59, :60, :70, :71, :98-101` |
| Capacity 6 / display 4 and the flicker rationale | `HostConnectionDataSO.cs:62, :77` |
| Stable identity already replicated; approval payload already carries a token | `Player.cs:82`; `SpectatorSession.cs:109` |
| Buttons | `FriendsListPanel.cs:647, :716, :748` |
| SDK subscribes to Wire itself; maps deltas; exposes reconnect | `LobbyHandler.cs:~371 (InitLobbyEventsAsync), :221 (LobbyCallbacks)`; `SessionHandler.cs:856 (ReconnectAsync)`; `MultiplayerService.cs:80 (ReconnectToSessionAsync)` |
| Friends push surface available | `IMessagingService.cs:22 (MessageAsync<T>)`; `IFriendsService.cs:173 (SetPresenceAsync), :196 (PresenceUpdated), :201 (MessageReceived)` |

## Appendix B — External references

- UGS Lobby **Config options** (Active Lifespan 30 s · Disconnect Removal Time 120 s · Disconnect
  Host Migration Time 120 s, per environment in the dashboard):
  <https://docs.unity.com/ugs/en-us/manual/lobby/manual/config-options>
- UGS Lobby **Rate limits** (values quoted in §3.1 as read on 2026-10-06; re-check before tuning):
  <https://docs.unity.com/ugs/en-us/manual/lobby/manual/rate-limits>
- Netcode for GameObjects — *Session management* (GUID-keyed slot reclaim, why `clientId` is not an
  identity): <https://mp-docs.dl.it.unity3d.com/netcode/2.0.0/advanced-topics/session-management>
- Netcode for GameObjects — *Reconnecting mid-game* (bounded `ReconnectCoroutine`):
  <https://mp-docs.dl.it.unity3d.com/netcode/2.2.0/advanced-topics/reconnecting-mid-game/>
- Boss Room `ClientReconnectingState` / `ConnectionManager` (the reference state machine):
  <https://plastichub.unity.cn/unity-tech-cn/BossRoomDGS/src/commit/60ec75a9-66ee-4d95-9878-46a0ae0c9314...Assets/Scripts/ConnectionManagement/ConnectionState/ClientReconnectingState.cs>
- Netcode (Entities) *Lobby and Relay integration* — the "balance detection vs. migration time"
  principle behind §5.5: <https://docs.unity3d.com/Packages/com.unity.netcode@1.5/manual/host-migration/lobby-relay-integration.html>
- AWS Architecture Blog — *Exponential Backoff and Jitter* (full jitter):
  <https://aws.amazon.com/blogs/architecture/exponential-backoff-and-jitter/>
- Google SRE book — *Handling Overload* (retry budgets; retry at one layer):
  <https://sre.google/sre-book/handling-overload/>
- Unity Discussions threads that describe the same failure shapes seen here (host leaves, lobby
  players not updating until removal time):
  <https://discussions.unity.com/t/lobby-players-does-not-update-on-player-disconnect/924115>,
  <https://discussions.unity.com/t/unity-lobby-relay-onplayerleft-not-being-called-after-timeout-application-close/944428>

## Appendix C — Every timeout in the system, by layer (for nesting checks)

| Layer | Name | Value | Nest rule |
|---|---|---|---|
| Lobby service | Active lifespan / disconnect removal / host-migration | 30 s / 120 s / 120 s | outermost; reconnect grace must be < 120 s |
| UTP | Heartbeat / connect window / disconnect | 0.5 s / 10 s / 10 s (were 60 s / 30 s until 8a0eaa0d) | connect window < app connection timeout |
| NGO | Client buffer / spawn / scene load | 10 s / 10 s / 120 s | inside join-ready |
| PIC | Shutdown / connection / join-ready / spectate-ready | 5 / 10 (30 via Bootstrap override) / 30 / 45 s — serialized on `PartyServices.prefab`, not the C# initializers | connection > UTP window; join-ready > buffer + spawn |
| HCS | Invite expiry / creation grace / converge / refresh errors | 60 s / 4 s / 4 s / ×3 | invite expiry ≥ accept path worst case |
| Scheduler | Base / boosted / boost window | 1.5 s / 0.75 s / 15 s (→ 20 s reconciliation only) | — |
| Retry | one `UgsRequestPolicy` (since 0521b858): rate / transient / conflict | ≤3 × min(8 s, 1 s·2^n) jittered / ≤3 × min(4 s, 0.5 s·2^n) jittered / 1 × 250 ms; budget 10 per minute | one layer only |
| Presence | Rejoin backoff | 3 → 60 s | — |
| Boot | Attempts × wait → offline | 3 × ≥ 15 s | must never be reached by a 429 (counted as `offline` in the NetDiag snapshot since 931dcd51) |
| Reachability | Poll | 5 s | hint only |
| SceneLoader | Client follow | 90 s | > NGO scene load? **No — 90 < 120**: tighten NGO `LoadSceneTimeOut` to 60 s or raise follow to 150 s |
| New | Reconnect grace (match / pre-match) | 45 s / 30 s | > detection (10 s), < lobby removal (120 s) |
