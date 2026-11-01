# Multiplayer architecture: the concepts, where Cosmic Shore stands, and what we need

Written 2026-11-01 from an audit of `Ys-bleeding-edge` @ `21735dcd9`. It explains each netcode
concept once, in terms of Netcode for GameObjects (NGO) 2.13 and Unity Gaming Services (UGS), then
says what Cosmic Shore does today and whether we need more. Read `Docs/MULTIPLAYER_START_HERE.md`
first for the standing rules (a party is 4, free tiers, push only to Ys-bleeding-edge), and
`Docs/MultiplayerArchitecture/ROADMAP.md` for the hardening queue this builds on.

## 0. The game in one paragraph (what shapes every decision below)

Cosmic Shore is a **co-op / casual-competitive** arcade flyer. Its matches have these properties:

- **Size:** 1-4 humans (6 seats on arena cards), with AI filling the rest.
- **Content:** dozens of short arcade modes (`Assets/_Scripts/Controller/Arcade/`).
- **Interaction:** ships fly fast and lay **trails of prisms**, and interaction is mostly **ship vs. prism**: hits, steals and destruction scored by volume. Twitch ship-vs-ship hitscan is not the core.
- **Who plays together:** friends who invite each other into a party.

That profile tells us the following:

- Latency **feel** of your own ship matters a lot. It is already solved, because you own it.
- Perfect shared hit-registration matters a little.
- Cheating matters only as much as leaderboards and economy do.
- Server bills must stay near zero.

## 1. Current architecture (audit summary)

| Area | What the code does | Where |
|---|---|---|
| Topology | **Client-hosted (listen server)**: one player is NGO host, the others are clients. `NetworkTopology: 0` (ClientServer), no distributed authority (`UseCMBService: 0`). No dedicated servers. | `Assets/_Prefabs/CORE/NetworkManager.prefab` |
| Transport / NAT | Unity Transport over **UGS Relay**. Sessions are created with `.WithRelayNetwork()`, so NAT is never an issue, and nobody's IP is exposed. | `Controller/Multiplayer/MultiplayerSetup.cs:436-443`, `Controller/Party/Services/PartySessionService.cs:229-235` |
| Sessions / lobby | Two layers: a **presence lobby** (who is online), plus a **party session** (the people you play with, max 4) created by the inviter. | `Controller/Party/`, `Docs/PresenceSystem/`, `Docs/PartySystem/` |
| Matchmaking | **None.** Play is invite-only, with no `WithMatchmaking` / Matchmaker / QuickJoin. | grep |
| Tick rate | `TickRate: 30`. `EnableTimeResync: 0`. Heartbeat 500 ms, connect timeout 1 s × 10 attempts. | `NetworkManager.prefab` |
| Ship authority | **Owner-authoritative movement.** Every vessel prefab uses `ClientNetworkTransform` (`OnIsServerAuthoritative() => false`). Owner-written input state (`InputStatus` `n_xSum`, `n_throt`, …) and owner-written `Player` NetworkVariables (name, vessel, avatar). The host owns roster and score state (`NetDomain`, `NetIsAI`, …, `Server` write). | `Utility/Network/ClientNetworkTransform.cs`, `_Prefabs/Spacevessels/*.prefab`, `Controller/IO/InputStatus.cs:37-41`, `Controller/Player/Player.cs:30-111` |
| NetworkTransform | `Interpolate: 1`, `PositionMaxInterpolationTime: 0.1`, position threshold 0.001, scale synced. Full-precision floats (no half-float), Euler rotation (no quaternion sync or compression). Reliable deltas (`UseUnreliableDeltas: 0`). | `_Prefabs/Spacevessels/TigerMoth.prefab:100-130` (all hulls alike) |
| Prisms / trails | **Not NetworkObjects.** Each peer grows trails locally from the replicated ship motion. Destruction and steals are **reported to the host** by ServerRpc (`ReportEnvironmentPrismDestroyed_ServerRpc`, `ReportPrismStolen_ServerRpc`), and the host scores. This is the main reason bandwidth stays small despite thousands of prisms. | `Controller/Player/Player.cs:722-820`, `Controller/Managers/StatsManager.cs:417-432` |
| Other replicated systems | Fauna, Fulmen (server-time-stamped events), wrecking ball, arcade lobby (`LobbySnapshot` NetworkVariable). | `Controller/Environment/FloraAndFauna/FaunaNetworkSync.cs`, `Controller/Environment/Fulmen/FulmenNetSync.cs`, `Controller/Multiplayer/ArcadeConfigSyncManager.cs` |
| RTT / ping | **Not used in gameplay or UI.** NGO metrics are enabled (`NetworkMessageMetrics: 1`). Diagnostics are in `Docs/NetworkDiagnostics/`. | grep `GetCurrentRtt` → none |
| Prediction / lag comp | **None**, and none is needed for your own ship (you own it, so there is zero input latency). Remote ships are interpolated ~0.1 s behind. | — |
| Interest management | **None** (no `CheckObjectVisibility` / `NetworkShow` / `NetworkHide`). Everyone receives everything. | grep |
| Host migration | **None.** If the host leaves, the match ends (`DisconnectNotice` paths). | `Tests/Editor/DisconnectNoticeTests.cs`, `Controller/Party/StateMachine/PartyStateMachine.cs` |
| Reconnection | **Mid-match rejoin exists** (correction to the first draft of this doc). When a guest drops, the host hands its Player and vessel to the AI (`DontDestroyWithOwner`, `NetIsAI`), so its score keeps counting. The client remembers the match (`MatchRejoin` record, 30 min) and `DisconnectNotice` offers Rejoin. The host matches the UGS PlayerId from the approval payload and gives the seat back (`TryReclaimSeat`). | `Controller/Multiplayer/ServerPlayerVesselInitializer.cs` (`HandleClientDisconnectedForAITakeover`, `TryReclaimSeat`), `Controller/Multiplayer/MatchRejoin.cs`, `UI/Elements/DisconnectNotice.cs`, `Tests/Editor/MatchRejoinTests.cs` |
| Anti-cheat | **Observe-only host checks**: `MovementPlausibilityMonitor` measures guest speed and teleports and raises signals, never kicks. Connection approval is on. | `Controller/Multiplayer/MovementPlausibilityMonitor.cs`, `Docs/AntiCheat/README.md` |

## 2. The concepts

Each concept below has three parts: **what it is**, **NGO / UGS specifics**, then **Cosmic Shore today** and the **verdict**.

### 2.1 Authoritative server (and who has authority)

**What it is.** One machine decides the truth: positions, hits and score. The others send inputs or requests, then render what the authority tells them. There are three flavours:

- **Dedicated server:** a headless build in a data centre. It is the most cheat-proof and costs money per hour.
- **Listen server / host:** one player's game is also the server. It is free, but that player has 0 ms latency and the others pay the full RTT. If they quit, the match dies.
- **Distributed authority:** every client owns its own objects, and a cloud service relays state between them.

**NGO / UGS.**

- `NetworkTopology` is ClientServer or DistributedAuthority.
- Authority is per object. A `NetworkTransform` is server-authoritative by default; `ClientNetworkTransform`, or `AuthorityMode.Owner`, makes it owner-authoritative.
- `NetworkVariable` write permission (Server or Owner) is per variable.
- Dedicated servers would mean **Multiplay Hosting**. Unity has been winding that product down; check its status before betting on it. The other option is a self-hosted headless Linux build.

**Cosmic Shore.**

- Listen server. The host is authoritative for **rules, roster and score**.
- Each owner is authoritative for its own **ship movement**.
- Hits are client-detected and host-scored.

**Verdict: Need now — keep "host-authoritative rules + owner-authoritative movement".** This is the right model for a 4-player co-op flyer:

- Instant ship response with no prediction code.
- Zero server cost.
- Relay hides IPs.

Do **not** move to dedicated servers before there is ranked play with stakes. Do tighten the host's authority over what decides a score (§2.13).

### 2.2 Matchmaking

**What it is.** Strangers are automatically grouped into a match, by skill, region, latency or mode. This is different from lobbies and invites, where friends join each other.

**NGO / UGS.**

- `SessionOptions.WithMatchmaking()` / `MatchmakerService` (UGS Matchmaker): rules-based tickets, QoS region selection, and a backfill option.
- The cheap alternative is **QuickJoin**: `MultiplayerService.MatchmakeSessionAsync` with a filter, which joins any open session of a mode or else creates one. It is free within Lobby quotas.

**Cosmic Shore.** None. Play is invite-only, plus AI fill.

**Verdict: Later.**

- With a small launch population, matchmaking creates empty queues. AI fill already gives every player a full match.
- When there is demand, add **QuickJoin into open parties** first (days of work, free). Use real Matchmaker only for ranked play.
- Keep the AI as the backfill.

### 2.3 Tick rate

**What it is.** How many times per second the network layer samples, sends and applies state.

- Higher means smoother and fresher state, at the cost of more bandwidth and CPU.
- Rendering stays at 60-144 fps; between ticks, remote objects are interpolated.

**NGO / UGS.**

- `NetworkConfig.TickRate`; the default is 30.
- `NetworkTime` / `ServerTime` advance per tick.
- `NetworkTransform` sends at most once per tick, and only past its thresholds.
- `EnableTimeResync` periodically corrects clock drift between peers.

**Cosmic Shore.** 30 Hz, and time resync is off.

**Verdict: Need now (keep 30 Hz) — tune later.**

- 30 Hz is the industry default for 4-player action games. Fast ships at 30 Hz plus 100 ms interpolation look smooth.
- Measure before going to 60 Hz; it doubles the transform traffic.
- **Turn `EnableTimeResync` on.** Fulmen already stamps events with `ServerTime`, and drift over long sessions skews it.

### 2.4 Interest management (relevancy / visibility)

**What it is.** Each client only receives the objects that matter to it: nearby objects, its team's, and the ones on its screen. This saves bandwidth and CPU at scale, and also hides information.

**NGO / UGS.**

- `NetworkObject.CheckObjectVisibility` callbacks.
- `NetworkShow` / `NetworkHide` per client.
- `SpawnWithObservers = false`.
- NGO has no built-in spatial grid; you write the rule.

**Cosmic Shore.** None. With ≤ 6 ships, and prisms that are not NetworkObjects, there is nothing big to cull.

**Verdict: Not needed (for now).**

- The cost driver would be thousands of networked prisms, and the architecture already avoids that by simulating prisms locally.
- Revisit only if fauna or creature counts become networked in the hundreds. `FaunaNetworkSync` is the place to watch.

### 2.5 Ping / RTT checking

**What it is.** Measuring round-trip time to the host or relay. It is used for:

- showing bars or ms in the UI;
- picking a region;
- warning or kicking on bad connections;
- feeding lag compensation and interpolation delay.

**NGO / UGS.**

- `NetworkManager.NetworkConfig.NetworkTransport.GetCurrentRtt(clientId)`. `UnityTransport` gives RTT per connection.
- The `NetworkManager` Runtime Network Stats Monitor (`com.unity.multiplayer.tools`).
- Relay **QoS** (`IQosService`) picks the best relay region before allocation; Sessions does this automatically.
- Unity Transport's `SimulatorPipeline` / Network Simulator can inject latency and loss for testing.

**Cosmic Shore.** Not read anywhere. Players can't see whether a stutter is the network, and the host can't see a bad guest.

**Verdict: Need now (cheap, high value).**

- Replicate each player's RTT, measured by the host per client at 1 Hz, as a server-written `NetworkVariable<ushort>` on `Player`.
- Show it on the scoreboard and party panel, and add it to the multiplayer debug overlay.
- Warn at > 200 ms, and log it with match analytics.

### 2.6 Client-side prediction and server reconciliation

**What it is.** Under server authority, a client applies its own input **immediately** (prediction) and keeps a history. When the authoritative state arrives, it rewinds to that state and **replays** the unacknowledged inputs (reconciliation). This hides latency for your own character.

**NGO / UGS.** NGO has **no** built-in prediction. You would write input buffers, tick-stamped state and replay yourself, or use Netcode for Entities, which has it but is a different stack.

**Cosmic Shore.** Not needed for ships: owner authority **is** zero-latency control without the cost of reconciliation. Where it does matter:

- **Host-spawned things the client triggers**, such as projectiles and ability effects. These should be spawned locally at once (a cosmetic "predicted spawn") and replaced or confirmed by the host's.
- **Score feedback** (a prism hit should pop instantly, with the host confirming the number).

**Verdict: Not needed for movement. Later, narrowly, for client-triggered spawns and effects.** Audit abilities that round-trip through the host before anything shows, and make their visuals local-first.

### 2.7 Interpolation (and extrapolation)

**What it is.** Remote objects are rendered slightly in the past, blending between two received snapshots, so motion is smooth despite 30 Hz updates and jitter.

- **Extrapolation** (dead reckoning) guesses forward from velocity to reduce the delay. It overshoots on turns.

**NGO / UGS.**

- `NetworkTransform.Interpolate`.
- The interpolation type (Lerp / SmoothDampening in 2.x) and `PositionMaxInterpolationTime`.
- `SlerpPosition` for orbital motion.
- Interpolation uses `NetworkTimeSystem` buffers.

**Cosmic Shore.** Interpolation is on with a 0.1 s max; there is no extrapolation.

**Verdict: Need now — already have it. Tune it.**

- **Trail coherence:** remote ships are drawn ~100 ms + ½RTT in the past, and their prisms are laid locally from that interpolated path. They therefore land slightly differently on each peer. That is fine as long as hits are judged by one party (see §2.8).
- Measure jitter with the Network Simulator at 150 ms / 2 % loss, and try SmoothDampening on fast hulls.
- Consider velocity-based extrapolation **only** for the camera-followed spectator view.

### 2.8 Lag compensation

**What it is.** When the server judges a hit, it rewinds the other players to where the **shooter saw them** (its time minus RTT and interpolation delay), so you hit what you aimed at. This is essential for hitscan shooters.

**NGO / UGS.** There is nothing built in. You would keep a ring buffer of collider poses per tick on the server and rewind for the check.

**Cosmic Shore.**

- Hits are detected on the **client that collides**, then reported to the host. That is "favour the shooter" by construction, so no rewind is needed.
- The cost is that the host trusts the report. That is an anti-cheat question (§2.13), not a lag question.

**Verdict: Not needed.**

- Prisms are local. Ship-vs-ship is not hitscan-central.
- If a mode ever needs authoritative ship-vs-ship hits (dogfight ranked), add a host-side 1 s pose history at that point.
- Short of that, keep client-detects and host-validates (plausible distance, rate limits).

### 2.9 Relay and NAT traversal

**What it is.** Most home routers block inbound connections. A relay forwards packets between peers so that nobody has to open ports, and it hides IPs.

**NGO / UGS.**

- UGS Relay (via `WithRelayNetwork()`), with region choice through QoS.
- Free tier allowances apply.
- Steam Networking Sockets (SDR) is the free alternative on Steam.

**Cosmic Shore.** All sessions use Relay.

**Verdict: Need now — done.**

- On Steam, consider a Steam transport later to lower cost and latency (`Docs/MULTIPLAYER_START_HERE.md` mentions Steam's free relay).
- Make sure the host's relay region is chosen by QoS for the party, not just for the host.

### 2.10 Host migration

**What it is.** When the host leaves, another player becomes host and the match continues.

**NGO / UGS.**

- UGS Sessions has `WithHostMigration()`. It picks a new session host and re-allocates Relay, but **NGO game state is not migrated**: you must snapshot it and rebuild it yourself.
- Distributed authority avoids the problem, because there is no single host.

**Cosmic Shore.** None. Host leave → match over → back to the menu.

**Verdict: Later.**

- Matches are short (`Docs/ARCADE_MATCH_LENGTH.md`), so losing one is annoying rather than fatal.
- Now: make host-leave **graceful**. Show a clear notice, return everyone to the party with the party intact, and offer "rematch with new host".
- Real migration costs weeks of work: snapshot every replicated system.

### 2.11 Reconnection

**What it is.** A player who drops (Wi-Fi blip, app backgrounded) rejoins the **same running match** and resumes their seat.

**NGO / UGS.**

- Session membership survives a short drop.
- NGO needs a new connection plus approval. The game has to map the returning `PlayerId` back to the seat and re-own the ship.
- `HeartbeatTimeoutMS` / `ClientConnectionBufferTimeout` decide how fast a drop is declared.

**Cosmic Shore.** It is already built: AI takeover on drop, then seat reclaim by UGS PlayerId through a connection-approval payload, with the client side in `DisconnectNotice`. The AI holds the seat for the rest of the match, not just 60 s, and the score is preserved on the Player object. (The first draft of this doc missed this.)

**Verdict: Done — verify it.** What remains is testing on real networks (checklist §5) and mobile backgrounding. The original plan was:

- Rejoin by UGS PlayerId within 60 s.
- Restore domain and score.
- Let the AI pilot hold the seat meanwhile. The AI already exists, so this is a natural fit.

### 2.12 Bandwidth and serialization

**What it is.** Keeping bytes per second low: delta compression, quantization (half floats, compressed quaternions), sending only on change, unreliable channels for state that is superseded anyway, and avoiding per-object RPC spam.

**NGO / UGS.**

- NetworkTransform options: `UseHalfFloatPrecision`, `UseQuaternionSynchronization` plus `UseQuaternionCompression`, `UseUnreliableDeltas`, thresholds, and per-axis sync.
- `INetworkSerializable` structs, `FastBufferWriter`, and `NetworkVariable` with custom write checks.
- Relay has per-connection bandwidth limits, and the free tier is metered.

**Cosmic Shore.**

- **Good:** prisms are not networked, the lobby is one struct, and Fulmen sends events, not state.
- **Not tuned (before 2026-11-01):** ship transforms were full-precision Euler with reliable deltas, and Tiger Moth also synced scale. **Now tuned — see §5.** Five owner float NetworkVariables are sent for input.

**Verdict: Need now (low effort).** For every vessel prefab's `ClientNetworkTransform`:

- turn off Scale sync;
- turn on quaternion sync plus compression (this avoids Euler wrap on barrel rolls);
- turn on half-float position if worlds are within ±~2 km of the origin (check arena sizes);
- turn on `UseUnreliableDeltas`.

Then measure with the Network Profiler before and after. Pack the 5 input floats into one `INetworkSerializable` struct.

### 2.13 Anti-cheat basics

**What it is.** Never trust a client for anything that changes someone else's outcome. Validate inputs and reports on the authority, rate-limit RPCs, keep secrets off clients, and sign or validate leaderboard submissions server-side.

**NGO / UGS.**

- `ConnectionApproval`.
- `RequireOwnership` on RPCs.
- Server write permission on NetworkVariables.
- **Cloud Code** for leaderboard and economy writes, so a client can't post a score directly.

**Cosmic Shore.**

- Owner-authoritative movement is trusted.
- Prism destroy and steal reports carry the **volume from the client** (`ReportEnvironmentPrismDestroyed_ServerRpc(float volume, …)`).
- `MovementPlausibilityMonitor` observes guest speed and teleports and signals, but never acts.

**Verdict: Need now for score integrity, Later for movement.**

1. Have the host **re-derive volume** from its own copy of the prism (or clamp to the hull's maximum), instead of trusting the float.
2. Rate-limit report RPCs per player per second.
3. Submit leaderboard and weekly-challenge scores **via Cloud Code**. The client half already exists (`System/CloudData/ServerScores.cs`, `weekly-score` endpoint), behind `ServerScoreGate.Enabled = false` until the script and the Access Control policy are deployed (`Tools/UGS/README.md`).
4. Set the plausibility ceilings once enough data is measured.

Movement cheats in a friends-only co-op game are low priority.

## 3. Verdict table

| Concept | Verdict | One-line reason |
|---|---|---|
| Authoritative server | **Need now (keep host-auth rules + owner-auth movement)** | The right, free model for 4-player co-op; no dedicated servers until ranked play with stakes. |
| Matchmaking | **Later** | Invite + AI fill covers a small population; add QuickJoin first, Matchmaker only for ranked. |
| Tick rate | **Need now (keep 30 Hz, enable time resync)** | 30 Hz is enough with interpolation; only drift correction is missing. |
| Interest management | **Not needed** | ≤ 6 ships and local prisms; nothing large to cull. |
| Ping / RTT | **Need now** | Cheap, and it is the first thing players and the host need to see when it stutters. |
| Client prediction / reconciliation | **Not needed (movement); Later (local-first ability VFX)** | Owner authority already gives zero-latency control. |
| Interpolation | **Need now — have it; tune it** | On at 0.1 s; tune it under the simulator and fix the transform settings. |
| Lag compensation | **Not needed** | Hits are judged by the colliding client; the risk is trust, not lag. |
| Relay / NAT | **Need now — done** | All sessions use Relay; Steam SDR is an optional later cost saver. |
| Host migration | **Later (graceful host-leave now)** | Short matches; true migration costs weeks. |
| Reconnection | **Done (pre-existing) — verify** | AI takeover + seat reclaim by PlayerId already ship. |
| Bandwidth / serialization | **Need now (low effort)** | NetworkTransform is untuned (scale synced, Euler, full floats, reliable). |
| Anti-cheat | **Need now for scores; Later for movement** | Clients send score volumes the host believes. |

## 4. Roadmap to seamless multiplayer (prioritised)

| # | Step | Why | Rough effort |
|---|---|---|---|
| 1 | **Finish the lobby / presence fixes and run the bug-hunt checklist** (`Docs/PresenceSystem/MULTIPLAYER_TEST_CHECKLIST.md`) | If you can't find or join friends reliably, nothing else matters | 1–2 days of testing + fixes |
| 2 | **RTT everywhere**: host measures per client via `GetCurrentRtt`, replicates it on `Player`, and shows it on the party panel, scoreboard and overlay; poor-connection warning; Relay region by QoS | Makes the network visible; drives every later decision | 1–2 days |
| 3 | **NetworkTransform tuning + Network Simulator profile**: drop scale, quaternion + compression, half floats where safe, unreliable deltas, pack input floats, `EnableTimeResync`; record bytes/s and jitter at 0/100/200 ms and 2 % loss | Smoother remote ships, less bandwidth, a repeatable test bench | 2–3 days |
| 4 | **Score integrity**: host re-derives or clamps prism volume, rate-limits report RPCs, posts leaderboard scores via Cloud Code | Scores and weekly challenges are the only place cheating hurts others | 3–5 days |
| 5 | **Mid-match reconnection**: hold the seat with the AI pilot for 60 s, rejoin by PlayerId, restore domain, score and ownership | Biggest seamless win on mobile and flaky Wi-Fi | 1–2 weeks |
| 6 | **Graceful host-leave**: everyone returns to the party intact with a clear notice and one-click rematch under a new host | Turns the worst failure into a 5-second hiccup | 3–4 days |
| 7 | **Local-first ability and impact VFX** (cosmetic prediction) | Removes any remaining "lag feel" on client-triggered actions | 1 week, as found |
| 8 | **QuickJoin "open party" matchmaking** with AI backfill | Lets solo players find humans when the population exists | 1 week |
| 9 | **Automated NGO host + client PlayMode tests** (MPPM / two NetworkManagers in one process) for spawn, kick AI, reconnect and host-leave | Stops regressions in the paths above | 1–2 weeks to set up the harness |
| 10 | **Host migration**, or a dedicated / distributed-authority option for ranked | Only when ranked play or long matches justify it | Weeks; decide later |

## 5. Implementation status (2026-11-01)

| Roadmap step | Status | What changed |
|---|---|---|
| 2. RTT everywhere | **Done (overlay + badge); partial (UI)** | `Player.NetRttMs` (server-write ushort) is sampled by the host once a second via `NetworkTransport.GetCurrentRtt(OwnerClientId)`. 0 for host and AI. `Utility/Network/ConnectionQuality.cs` holds the thresholds: ≤100 Good, ≤200 Fair, >200 Poor. The debug overlay has a Network (RTT) section. `Utility/Network/ConnectionWarningBadge.cs` is a runtime-built, release-build "Poor connection" badge on clients after 3 s above 200 ms. **Not done:** RTT on the party panel and scoreboard rows (bind `Player.NetRttMs` + `ConnectionQuality.Label`), and a party-wide relay region choice: Sessions picks the relay by the host's QoS, and changing that is not low-risk. |
| 3. Transform tuning | **Done** | All 17 vessel prefabs (`_Prefabs/Spacevessels/*.prefab`, `ClientNetworkTransform`): `UseQuaternionSynchronization 0→1`, `UseQuaternionCompression 0→1`, `UseHalfFloatPrecision 0→1`, `UseUnreliableDeltas 0→1`. Tiger Moth `SyncScaleX/Y/Z 1→0` (the others already had it off). `NetworkManager.prefab` `EnableTimeResync 0→1` (interval 30 s). Packed input floats: **skipped**. `InputStatus` NetworkVariables are read widely; there is little gain for the risk. |
| 4. Score integrity | **Done (host guard); Cloud Code pre-existing, gated off** | `Controller/Multiplayer/HitReportGuard.cs`: per-player token bucket (600 burst, 300/s) and a per-report volume cap of 10 000. It is applied in `ReportEnvironmentPrismDestroyed_ServerRpc` and `ReportPrismStolen_ServerRpc`, and the clamped volume is also what is debited from a steal victim. Turning on `ServerScoreGate` needs the Cloud Code script and the Access Control deploy (follow-up). |
| 5. Mid-match rejoin | **Already implemented** | See §2.11. No code change; checklist §5 cases 7-9 verify it. |

Tests: `Tests/Editor/ConnectionQualityTests.cs`, `Tests/Editor/HitReportGuardTests.cs`, plus the existing `MatchRejoinTests`. Manual: `Docs/PresenceSystem/MULTIPLAYER_TEST_CHECKLIST.md` §5.
