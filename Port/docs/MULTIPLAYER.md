# Multiplayer in Prisma: the networking system, its backends and its test tools

Written 2026-10-08. Owner: Froglet Inc. This is the plan and the manual for Cosmic Shore's
four-player online play in Prisma, and for the tools that test it **without Unity**. Read it before
touching `src/CosmicShore.Engine/Networking/**`, the Launcher's multiplayer panel or the `net_*`
MCP tools. It extends `ARCHITECTURE.md` §8 (how the netcode works today) and the C6 row of
`ROADMAP.md`.

*The licensing sections are an engineering review, not legal advice (same footing as
`LEGAL_REVIEW.md`). Have counsel confirm before a commercial release on the engine (gate G3).*

---

## 1. The goal

1. **A very good four-player online game**: the fewest network problems a player can see, and the
   best performance, on a budget as close to free as possible.
2. **Our own networking system in our own engine**, so the multiplayer can keep being built and
   tested in Prisma.
3. **Built-in multiplayer test tools**, matching what Unity gives (Multiplayer Play Mode, the
   Network Simulator, the Runtime Network Stats Monitor, the Network Profiler) and adding what
   Unity lacks (session-service fault injection, scripted multi-player scenarios an agent can run).

The Unity build keeps shipping on Unity's Netcode for GameObjects (NGO) and UGS. Nothing here
changes `Assets/`, `Packages/` or `ProjectSettings/` (`Port/CLAUDE.md`, "the one rule").

---

## 2. Can we use Unity's Netcode in our engine? (licensing)

| Package | Licence | In the Unity build | In Prisma |
|---|---|---|---|
| Netcode for GameObjects 2.13.3 | Unity Companion License (UCL) | Allowed: the UCL covers content made under a Unity engine licence | **Its source may not be vendored or shipped.** The UCL grants use "in connection with" a Unity Engine License only |
| Unity Transport 2.7.4 | UCL | Allowed | Same: not usable |
| Multiplayer Services SDK 2.3.3 (Lobby/Relay/Sessions client) | UCL | Allowed | Same: not usable. **The services behind it are reachable** (§4) |
| Multiplayer Play Mode, Multiplayer Tools | UCL, editor-only | Allowed | Not needed: Prisma builds its own (§6) |

So the answer is **no, we do not run Unity's Netcode code in Prisma, and we do not need to**:

- **Prisma already has its own netcode.** `NetDriver` (`src/CosmicShore.Engine/Networking/Wire/`)
  was written for the port. It offers types *named* like NGO's (`NetworkManager`, `NetworkObject`,
  `NetworkVariable`, RPCs) so the game's 2,090 scripts compile unchanged, but none of the code
  behind those names is Unity's. That is the API re-implementation pattern the whole engine uses
  (`LEGAL_REVIEW.md` §3: Google v. Oracle, Wine, Mono). Whether the engine may *declare* Unity's
  namespaces is an open counsel question (review item A1, gate G3). Prisma sidesteps it today by
  rewriting `Unity.Netcode` to `CosmicShore.Engine.Networking` at sync time.
- **Rule for every session**: never copy code from the NGO, Unity Transport or Multiplayer
  Services packages. Reading their *documentation* to match behaviour is fine. Reading their
  *source* to find out what a behaviour is (for example, the disconnect callback order) is
  allowed. Copying its expression is not.
- **Consequence**: a Unity build and a Prisma build cannot play each other. Their wire formats
  differ. Cross-engine play is out of scope; each build plays its own kind.

## 3. Topology: what a "very good four-player system" needs

Cosmic Shore's matches are **four humans (or AI backfill), host-authoritative, short sessions**.
For that shape:

| Choice | Decision | Why |
|---|---|---|
| Topology | **Listen server (one player hosts)** through a **relay** | Same as the Unity build. No dedicated-server fleet to pay for. The relay gets through NAT, so nobody opens ports. Four players stay well inside one home connection's upload |
| Authority | Server-authoritative state, owner-authoritative vessel transforms | What the game's code already assumes (NGO model). Unchanged |
| Transport | **Our own UDP** (`UdpTransport`, Step 5): one reliable-ordered stream (RPCs, spawns, variables, scenes, and the transforms NGO also sends reliably) plus an unreliable channel for the transforms whose prefab opts in with `UseUnreliableDeltas`, as NGO does | A lost TCP segment stalls everything behind it until TCP's resend timer fires, and that timer has a floor of 200 ms on Linux and 300 ms on Windows. Our resend timer follows the measured RTT with a 30 ms floor, so a loss costs about one round trip. Vessel transforms stay reliable because the game authors them so (`UseUnreliableDeltas: 0`): parity with the Unity build comes first |
| Tick | 30 Hz send, interpolated on receivers (unchanged) | `NetworkConfig.TickRate`; NetDriver already interpolates `NetworkTransform` |
| Bandwidth | Delta-compressed transforms, quantised (later) | Lists as gap 6 in `ROADMAP.md`. Only needed if a profile shows it (§6.4 measures it) |
| Host leaves | Remaining players return to their menus (B10) | Host migration is not planned: a four-player match is short, and the party survives |

Budget check (estimate, to be replaced by §6.4's measurement): four vessels × 30 Hz × ~40 bytes
per transform ≈ 4.8 KB/s per receiver, under 20 KB/s up for the host with all traffic. That is far
inside any broadband line and inside UGS Relay's free egress (§4.3).

## 4. Backends: UGS from our engine, our own servers, or Steam

The netcode does not care where its bytes travel. Two seams keep the backend swappable:

- **`INetTransport`** (exists: `Wire/INetTransport.cs`) carries frames between peers.
- **The session service** (exists in stand-in form: `DirectoryMultiplayerService`) finds and joins
  sessions: lobby, roster, invites, host endpoint.

### 4.1 UGS from Prisma: yes, without Unity's SDK

UGS is a set of web services. Unity documents their REST APIs for non-Unity clients, and
documents the Relay *wire protocol* for "an alternative engine or networking solution". So Prisma
can use UGS without any UCL code:

| Service | How Prisma reaches it | What we write |
|---|---|---|
| Authentication | REST: anonymous sign-in with the project id, then a session token | A small HTTP client (`HttpClient`, no new dependency) |
| Lobby | REST: create/join/query/heartbeat/update player data | The session-service implementation behind the game's `ISession` calls |
| Relay allocations | REST: Allocations API (create, join code, join) and QoS discovery | Same HTTP client |
| **Relay data** | **UDP, the documented Relay message protocol**: big-endian header, `BIND` signed with HMAC from the allocation's key, `BIND_RECEIVED`, `CONNECT_REQUEST`, `ACCEPTED`, `RELAY`, `PING` (the server drops a client after 10 s idle), `CLOSE`, `DISCONNECT` | A `RelayTransport : INetTransport` that frames our reliable-UDP packets inside `RELAY` messages |
| Cloud Save, Leaderboards, Friends | REST | Implementations behind the game's existing facades |

Limits that come with it:
- Relay connects only players of **the same UGS project and environment**. A Prisma test build and
  the Unity build can share the project but, per §2, not a match.
- Relay forwards datagrams. Reliability is ours, which is why Step 5's reliable-UDP layer comes
  first: the same layer runs over direct UDP, over UGS Relay and over our own relay.
- Credentials: the project id and environment name are not secrets (the shipped Unity build
  carries them). No service-account key may ever ship in a client.

### 4.2 UGS-like servers of our own: possible, small, not free

What a self-hosted backend would be, in .NET with no new packages:

| Piece | Size | Notes |
|---|---|---|
| Lobby service | One HTTP service (sessions in memory, JSON over HTTP) | `DirectoryMultiplayerService`'s file format is already this shape |
| Relay | One UDP forwarder: bind, connect, forward by allocation id | A few hundred lines. Same protocol idea as UGS Relay |
| Auth | Steam auth tickets, or signed anonymous ids | Needed so players can't impersonate each other |

Cost: one small VM (~$4-6/month, or a provider's always-free tier while testing) and our time
running it (uptime, DDoS, regions). **It only becomes cheaper than UGS above UGS's free
allowance**, and Cosmic Shore's early-access player counts sit inside that allowance.

### 4.3 What it costs (check before launch)

UGS's published pricing as of this writing: Relay includes **50 average monthly CCU free**
(then $0.16 per average CCU) and **3 GiB egress per CCU** (up to 150 GiB/month free). Lobby is
billed on bandwidth past 10 GiB/month per region group. Steam's lobbies and Steam Datagram
Relay cost nothing extra for a Steam game. Re-check
<https://unity.com/products/gaming-services/pricing> at gate G2. These numbers change.

### 4.4 Recommendation

1. **Now: test everything locally in Prisma** with the stand-in session service and the tools in
   §6. Most multiplayer bugs this project has had (B2-B29) were decision bugs in the party,
   ready-gate and spectator code. Five local processes reproduce those.
2. **Gate G2 (2026-12-15)**: build the backend that matches the shipped Unity build: **UGS**
   (Auth, Lobby, Relay over REST and the Relay protocol). Prisma testers then use the same lobby
   service as the Unity game, for free inside the allowance.
3. **Steam** (Steamworks.NET, MIT; a new dependency to approve at that time) for Steam auth,
   achievements, and optionally Steam lobbies plus Steam Datagram Relay for the PC build. This is
   free and the strongest NAT traversal on PC.
4. **Our own servers** only if UGS's bill or limits ever bite. The seams mean it is a new
   implementation, not a rewrite.

---

## 5. What exists today (2026-10-08)

| Piece | Where | Status |
|---|---|---|
| Netcode model (approval, scene sync, spawns, variables, RPCs, ownership, parenting, transforms, named messages, scene loads) | `Wire/NetDriver.cs` | Works. LAN parties and five-process harness runs pass |
| Transport seam | `Wire/INetTransport.cs` | Landed. TCP (`NetSocket`) and, from Step 5, UDP (`UdpTransport`); tests also use an in-memory loopback |
| Session service stand-in | `Wire/DirectoryMultiplayerService.cs` | One JSON file per session in `COSMIC_SHORE_NET_DIR` |
| Five-player scenario harness | `Tools/Build/prisma_party_scenarios/` | T1-T7 pass. Patches the engine in a throwaway worktree (Step 0 removes that need) |
| Offline switch, per-instance profile | `COSMIC_SHORE_NET=off`, `COSMIC_SHORE_PROFILE=<name>` | Works |
| Launcher | One game at a time, with the Online toggle and the Profile field | No multi-player panel |
| Network simulator, stats, profiler, fault injection | | Missing |

## 6. The tools, mapped to Unity's

| Unity | Prisma | Step |
|---|---|---|
| Multiplayer Play Mode (virtual players with tags) | **MULTIPLAYER panel** in the Launcher: 1-4 players, each its own process, profile and save folder, one shared session folder; MCP `net_players` | 4 |
| Network Simulator (latency, jitter, loss, disconnect) | **`SimulatedTransport`**: wraps any transport. Set at launch with `COSMIC_SHORE_NET_SIM` or live with `do netsim` | 1 |
| Runtime Network Stats Monitor | **`net` overlay and `do net` verb**: per-peer bytes and messages in/out, RTT, RPCs by name, variable updates, spawned objects | 2 |
| Network Profiler | **Per-frame capture** to JSON (`do net capture`) and the session report's `net` block; a timeline view on the Launcher | 2 (capture), 4 (view) |
| *(none)* | **Session-service faults**: lobby full, rate limited, relay allocation fails, service down. `COSMIC_SHORE_NET_FAULT` or `do netfault` | 3 |
| *(none)* | **Scenario harness as a tool**: `run.sh` without patches; MCP `net_scenario` | 0, 4 |
| *(none, Unity's transport is fixed)* | **Our reliable-UDP transport** with reliable and unreliable channels | 5 |

### 6.1 Step 0: engine features that replace the harness's throwaway patches

- **Real-time pacing** for `--headless`: `--realtime` (or `COSMIC_SHORE_HEADLESS_REALTIME=1`).
  A frame that finishes early sleeps out its 1/60 s, and debt past a quarter second is dropped.
  Without it, an idle headless menu runs its game clock ~200× faster than the wall clock. Its
  timers then fire early against the session folder and the sockets, which run on the wall clock
  (run 3 of the harness: an invite expired before the guest polled it).
- **The API gaps `gapfill.py` fills** become real engine members.
- **Saves never go relative**: when .NET returns "" for `LocalApplicationData` (a missing XDG
  folder on Linux), fall back to `$HOME/.local/share`.

### 6.2 Step 1: the network simulator

`SimulatedTransport` wraps any `INetTransport` and delays what it carries:

| Setting | Meaning |
|---|---|
| `latency=MS` | One-way delay added to every frame, both directions |
| `jitter=MS` | Random extra delay, 0..MS. Order is kept: the transport promises ordered frames, so a late frame holds back the ones behind it, exactly like TCP |
| `loss=PCT` | Each frame is "lost" with this chance and **resent** after one round trip, so it arrives late. The contract is reliable, so loss shows up as delay spikes, which is what a reliable stream does on a lossy line. The unreliable channel (Step 5) will drop for real |
| `bandwidth=KBPS` | Upload cap per peer; frames queue behind it |
| `down` / `up` | Pull the cable: every peer stops hearing this process (`down`) until `up`. After 10 s down, the peers are disconnected, as UTP's timeout would |

Presets, after Unity's: `lan` (latency 1), `broadband` (latency 20, jitter 5), `dsl` (40/10,
loss 0.5), `4g` (60/20, loss 1), `3g` (120/40, loss 2), `poor` (200/80, loss 5, bandwidth 128).

How to use it (`src/CosmicShore.Engine/Networking/Wire/NetSimulator.cs`):

```bash
COSMIC_SHORE_NET_SIM=4g CosmicShore ...                  # at launch
curl -s -X POST -d '{"cmd":"do","arg":"netsim poor"}' http://127.0.0.1:47800/     # live
curl -s -X POST -d '{"cmd":"do","arg":"netsim latency=150 jitter=30"}' ...        # tokens apply in order
curl -s -X POST -d '{"cmd":"do","arg":"netsim down"}' ...   # pull the cable; "up" plugs it back in
curl -s -X POST -d '{"cmd":"do","arg":"netsim off"}' ...
```

Each process simulates its own line, so a 4-player test can give each player a different one.
Arriving frames are released when the driver polls (once a frame, like everything the game
receives); leaving frames are released on time by a small pump thread, so a sender busy loading
a scene still delivers on schedule.

### 6.3 Step 3: session-service faults

`NetFaults` (`Wire/NetFaults.cs`) wraps the session service (whichever backend: the directory
stand-in today, UGS later), so the game's error paths (`UgsRequestPolicy`, the offline fallback,
the party's full-lobby message) run without a real outage. Set at launch with
`COSMIC_SHORE_NET_FAULT=SPEC` or live with `do netfault SPEC`:

| Token | Effect | Error |
|---|---|---|
| `full[=N]` | The next N joins find the session full | `SessionException(Unknown, "Session is full.")`, the stand-in's own full shape |
| `ratelimit[=N]` | The next N calls of any kind fail | `RateLimitExceeded` (429) |
| `relayfail[=N]` | The next N creates or joins fail | `NetworkSetupFailed` |
| `down` / `up` | Every call fails until `up`. Leaving and deleting still work, so no session is stranded | `Unknown`, "Service Unavailable" |
| `slow=MS` | Every call waits MS first | none |
| `off` | Clear everything | |

`do netfault` with no spec prints what is armed and what has been raised. A bad spec changes
nothing. The error shapes are the stand-in's reading of the SDK's: only an MPPM run against real
UGS confirms them.

### 6.4 Step 2: stats and capture

`NetStats` (`Wire/NetStats.cs`) counts what `NetDriver` hands the transport and what it receives:
per peer and in total, bytes and messages in and out, by message kind (`NetVar`, `Transform`,
`Rpc`, `Spawn`...) and RPCs by method name, plus one-second rates and their peaks. Round-trip
time comes from the driver's clock ping, now once a second: the client measures it, and sends its
latest sample inside its next ping so the host knows every member's RTT. Each peer shows a smoothed
RTT (weight 1/4), the last sample and the minimum. Counters reset when a session starts.

| Where | What |
|---|---|
| `do net` | The report: role, rates, per-peer RTT and traffic, kinds out/in, top RPCs |
| `do net json` | The same, structured (what the MULTIPLAYER panel and `net_stats mode=json` read) |
| `do net reset` | Zero the counters |
| `do net capture N [PATH]` | N frames of per-frame bytes/messages/RTT to JSON (a temp file by default): the Network Profiler's raw data |
| Window title | `Cosmic Shore · PilotB · CLIENT · rtt 28 ms · in 5.7 KB/s out 4.4 KB/s · sim ...`, once a second: tiled windows say who is who and how each link is doing |
| Session report | A `net` block (the summary above) when the run was networked |

First measurement (2026-10-08, two players in Menu_Main, the lava-lamp vessels flying): about
5 KB/s each way; `NetVar` messages outnumber `Transform` ~7 to 1. That makes variable traffic, not
transforms, the first place to look when bandwidth matters (§3).

Reading RTT: two headless players on one machine at `--realtime` see ~18-30 ms, which is the
frames each side takes to read a message (the driver polls once a frame), not the network.

### 6.5 Step 4: the Launcher's NET page and the MCP tools

**The Launcher's NET page** (`src/CosmicShore.Launcher/LauncherApp.Net.cs`; manual:
`LAUNCHER.md` § NET). PLAYERS: 2-4 windows (a party is four), tiled two by two, each its own
profile (`player1`...) and save, one fresh session folder per run, a simulated line per player.
LIVE: each player's scene, role, traffic and RTT once a second, and per player a line picker, PULL
CABLE / PLUG IN, a fault picker and CAPTURE 10 S. It replaces the TIME page's old MULTIPLAYER
card, which allowed six players and had no control ports.

**The MCP tools** (`src/CosmicShore.Mcp/Tools.Multiplayer.cs`), backed by `Shared/MultiplayerRun.cs`:

| Tool | What |
|---|---|
| `net_players` | `action=start` 1-4 players (headless by default, `--realtime`), `profiles`, `sims`, `faults` per player; `status`; `stop` |
| `net_input` | Any `--do` action on one player or all (`party invite PilotB` through the console route, `click X,Y` ...) |
| `net_command` | Any control-port command on one player or all (`state`, `wait`, `find`, `get`, `screenshot` ...) |
| `net_sim` / `net_fault` | The simulator and the session faults, live, per player |
| `net_stats` | `do net`: text, `json`, `reset`, `capture N [PATH]` |
| `net_logs` | One player's console, optionally filtered |
| `net_scenario` | The five-player party scenario harness (`Tools/Build/prisma_party_scenarios/run.sh`), ~10-15 min |

From a shell, without MCP, the same pieces are the player's own flags:

```bash
P=Port/src/CosmicShore.Player/bin/Debug/net10.0/CosmicShore.dll
export COSMIC_SHORE_NET_DIR=/tmp/mp/sessions COSMIC_SHORE_AUDIO=off
COSMIC_SHORE_PROFILE=PilotA dotnet $P --control-port 47801 --position 0,0 &
COSMIC_SHORE_PROFILE=PilotB COSMIC_SHORE_NET_SIM=4g dotnet $P --control-port 47802 --position 960,0 &
curl -s -X POST -d '{"cmd":"do","arg":"net"}' http://127.0.0.1:47801/
```

### 6.6 Step 5: our UDP transport

`UdpTransport` (`Wire/UdpTransport.cs`) is Froglet's own transport. It keeps the `INetTransport`
contract (reliable, ordered, whole frames), so `NetDriver` runs on it unchanged, and the same
`NetTransportContractTests` hold it, TCP and the loopback to that contract. **It is the default for
every networked player since 2026-10-08**; TCP is one switch away: `COSMIC_SHORE_NET_TRANSPORT=tcp`,
`net_players transport=tcp`, or TCP on the NET page. Every player of a session must use the same
transport.

**Wire** (little-endian; every packet starts with a type byte):

| Packet | Layout | When |
|---|---|---|
| ConnectRequest | `[1][magic "CSNP" u32][version u8][nonce u64]` | Client to server, every 100 ms until accepted or the connect timeout |
| ConnectAccept | `[2][nonce u64][token u64]` | Server to client; repeated if a request repeats |
| Data | `[3][token u64][ackNext u32][ackBits u64]`, then fragments `[seq u32][flags u8][len u16][bytes]` | Everything else. flags: 1 = last fragment of a frame, 2 = an unreliable frame |
| Disconnect | `[4][token u64]` | Either way, sent three times |

**How it works:**

- **Fragments.** A frame is split into fragments of at most 1,150 bytes, so a packet stays under
  1,200 bytes, inside any internet path's MTU. Each fragment takes one u32 sequence number per
  direction (no wraparound in practice: 4 TB per connection).
- **Acks.** The receiver delivers fragments in order and acks in every packet it sends: "everything
  below ackNext arrived", plus a 64-bit mask of what arrived after it.
- **Resends.** The sender resends a fragment when its timeout passes: smoothed RTT + 4 × variance,
  between 30 ms and 2 s, doubling per resend up to 8×. Only first sends are timed (Karn's rule).
- **Window.** At most 512 fragments are in flight. The kernel buffers are 4 MB, so a scene snapshot
  burst fits.
- **Tokens.** Each connection has a random token, so a packet from another address or an old
  connection is ignored.
- **Liveness.** An idle connection sends an ack every 250 ms. Ten seconds without a packet drops
  the peer, Unity Transport's own default.
- **Closing.** A Disconnect is sent only after the frames queued before it are acked (or after
  2 s), so a kick's reason arrives first. `Dispose` behaves like closing a TCP socket: queued frames
  still go out.
- **Threading.** One background thread owns the socket and all peer state. The main thread only
  queues frames and reads events.

**The unreliable channel.** `INetTransport.Send(peer, payload, length, NetChannel)`:
- **TCP** sends an unreliable frame reliably (the interface's default).
- **UDP** sends it once, inside a single packet. It is never resent and never held behind a
  reliable resend. A frame too big for one packet goes reliably instead.
- **The simulator** really loses unreliable frames (`loss=`), and delays them on their own queue.

`NetDriver` uses the channel only for a `NetworkTransform` whose prefab sets `UseUnreliableDeltas`.
Unity's Netcode has the same option; in this project the four fauna prefabs set it (MassShark,
MassBrittlestar, QuadFish, TadPole) and no vessel does. Such a transform sends
`TransformStamped` messages that carry the network time the pose was taken at:
- receivers drop any pose older than the newest one they have applied;
- a teleport always goes reliably;
- a quarter second after the last move, the settled pose is sent once more, reliably, so a lost
  final datagram cannot leave the object stuck.

**What this buys.** A lost packet on our reliable stream costs about one round trip, where TCP's
resend timer waits at least 200 ms. The unreliable fauna poses never wait for anything. Loss shows
up in `do net` as resends, and as `unreliable out/in` counts.

**Measured** (2026-10-08):

| Check | Result |
|---|---|
| Contract checks over UDP, alone and behind a bad simulated line | Pass |
| Loss | Under 10% and 30% real datagram loss in both directions, 300 frames (one of 200 KB, ~175 fragments) arrive whole and in order |
| Liveness | A silent peer times out on both ends; an idle connection survives on keepalives |
| Spoofing | A forged packet from another address is ignored |
| Close | `Dispose` flushes 50 queued frames under 20% loss before the goodbye |
| Five-player party harness on UDP (`COSMIC_SHORE_NET_TRANSPORT=udp`) | **14/14** (T1, T5-accept, T2b, kick, leave, T2, T5-join, launch, T4, T3, T6, net, T7, T4-lobby). In T7 (host killed) the survivors detected the dead host through the 10 s silence timeout, as on Unity Transport |
| The same, with the unreliable channel and every player on `4g` (`COSMIC_SHORE_NET_SIM=4g`) | **13/14**. T4-lobby failed on a game defect, not the transport: the invitee's Accept pre-flight refused a fresh invite because the new host's `partySession` advertisement lagged its invite (B29; the fix is in the game code). Five game processes on a 4-core container are CPU-bound, so RTTs there (300-900 ms) include several frame waits on top of the simulated line |

**Next for the transport.**
- **The relay (gate G2).** Our fragments ride inside UGS Relay's `RELAY` messages, with the Relay
  protocol's `BIND`/`PING` around them. Or they ride Steam Datagram Relay.
- **Congestion control** past the fixed window: needed only on internet paths, measured first.
- **Delta-compressed `NetworkVariable` writes:** the first measurement (§6.4) shows they dominate
  traffic.

## 7. Status

| Step | What | Status | Evidence |
|---|---|---|---|
| Doc | This file | Done 2026-10-08 | |
| 0 | Real-time pacing, API gaps, save-path fallback | Done 2026-10-08 | `RealtimePacerTests` 4/4; 1200 headless frames: 11.9 s unpaced, 28.3 s with `--realtime`; player builds (it did not: 19 missing `SessionError` codes) |
| 1 | Network simulator | Done 2026-10-08 | `SimulatedTransportTests` 14/14; the 7 transport contract checks pass behind a bad line (latency 5, jitter 10, loss 20%) over TCP and loopback; `NetDriverTransportTests` approves a real TCP client behind 40 ms each way in >= 80 ms; 5 repeat runs stable |
| 2 | Stats, `do net`, capture, window-title monitor | Done 2026-10-08 | `NetStatsTests` 8/8. Two real players: traffic, kinds and RPC names on both; localhost RTT 27.7 ms (min 18); `netsim latency=100` on the guest read 238 ms on both ends (28 + 200) |
| 3 | Session-service faults | Done 2026-10-08 | `NetFaultsTests` 10/10. Two real players: `netfault full` refused the guest's join with the game's "That party is full." and bounced it to its menu (host stayed 1/4; the next join seated it); `ratelimit=3` raised 3 and the party survived |
| 4 | Launcher NET page, MCP `net_*` tools | Done 2026-10-08 | `MultiplayerRunTests` 7/7, Launcher tests 21/21; NET page screenshotted under xvfb (empty and with four player rows); the MCP server over stdio started 2 headless players (one on `4g` from launch), and `net_sim`, `net_fault`, `net_stats`, `net_players status`, `net_logs` and `stop` worked against them |
| 5 | UDP transport, unreliable channel for opted-in transforms | Done 2026-10-08 | `UdpTransportTests` 6/6, `UnreliableChannelTests` 8/8, `UnreliableDeltasPrefabTests` 3/3; contract checks over `udp` and `sim-udp`; network suites stable over 5 repeats; CosmicShore.Tests 1862/1862; the five-player party harness **14/14 on UDP** |
| G2 | UGS backend (Auth, Lobby, Relay protocol) | After gate G2 | |

## 8. Sources

- Unity Relay message protocol (for alternative engines):
  <https://docs.unity.com/en-us/mps-sdk/advanced-config/relay-message-protocol>
- Relay REST API (allocations, QoS): <https://docs.unity.com/en-us/mps-sdk/advanced-config/relay-rest-api>
- Unity Companion License: <https://unity.com/legal/licenses/unity-companion-license>
- UGS pricing: <https://unity.com/products/gaming-services/pricing>
- How Relay is priced: <https://support.unity.com/hc/en-us/articles/4410136449812-How-is-the-Relay-Service-Priced>
