# Multiplayer in Amoebius: the networking system, its backends and its test tools

Written 2026-10-08. Owner: Froglet Inc. This is the plan and the manual for Cosmic Shore's
four-player online play in Amoebius, and for the tools that test it **without Unity**. Read it before
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
   tested in Amoebius.
3. **Built-in multiplayer test tools**, matching what Unity gives (Multiplayer Play Mode, the
   Network Simulator, the Runtime Network Stats Monitor, the Network Profiler) and adding what
   Unity lacks (session-service fault injection, scripted multi-player scenarios an agent can run).

The Unity build keeps shipping on Unity's Netcode for GameObjects (NGO) and UGS. Nothing here
changes `Assets/`, `Packages/` or `ProjectSettings/` (`Port/CLAUDE.md`, "the one rule").

---

## 2. Can we use Unity's Netcode in our engine? (licensing)

| Package | Licence | In the Unity build | In Amoebius |
|---|---|---|---|
| Netcode for GameObjects 2.13.3 | Unity Companion License (UCL) | Allowed: the UCL covers content made under a Unity engine licence | **Its source may not be vendored or shipped.** The UCL grants use "in connection with" a Unity Engine License only |
| Unity Transport 2.7.4 | UCL | Allowed | Same: not usable |
| Multiplayer Services SDK 2.3.3 (Lobby/Relay/Sessions client) | UCL | Allowed | Same: not usable. **The services behind it are reachable** (§4) |
| Multiplayer Play Mode, Multiplayer Tools | UCL, editor-only | Allowed | Not needed: Amoebius builds its own (§6) |

So the answer is **no, we do not run Unity's Netcode code in Amoebius, and we do not need to**:

- **Amoebius already has its own netcode.** `NetDriver` (`src/CosmicShore.Engine/Networking/Wire/`)
  was written for the port. It offers types *named* like NGO's (`NetworkManager`, `NetworkObject`,
  `NetworkVariable`, RPCs) so the game's 2,090 scripts compile unchanged, but none of the code
  behind those names is Unity's. That is the API re-implementation pattern the whole engine uses
  (`LEGAL_REVIEW.md` §3: Google v. Oracle, Wine, Mono). Whether the engine may *declare* Unity's
  namespaces is an open counsel question (review item A1, gate G3). Amoebius sidesteps it today by
  rewriting `Unity.Netcode` to `CosmicShore.Engine.Networking` at sync time.
- **Rule for every session**: never copy code from the NGO, Unity Transport or Multiplayer
  Services packages. Reading their *documentation* to match behaviour is fine. Reading their
  *source* to find out what a behaviour is (for example, the disconnect callback order) is
  allowed. Copying its expression is not.
- **Consequence**: a Unity build and an Amoebius build cannot play each other. Their wire formats
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

### 4.1 UGS from Amoebius: yes, without Unity's SDK

UGS is a set of web services. Unity documents their REST APIs for non-Unity clients, and
documents the Relay *wire protocol* for "an alternative engine or networking solution". So Amoebius
can use UGS without any UCL code:

| Service | How Amoebius reaches it | What we write |
|---|---|---|
| Authentication | REST: anonymous sign-in with the project id, then a session token | A small HTTP client (`HttpClient`, no new dependency) |
| Lobby | REST: create/join/query/heartbeat/update player data | The session-service implementation behind the game's `ISession` calls |
| Relay allocations | REST: Allocations API (create, join code, join) and QoS discovery | Same HTTP client |
| **Relay data** | **UDP, the documented Relay message protocol**: big-endian header, `BIND` signed with HMAC from the allocation's key, `BIND_RECEIVED`, `CONNECT_REQUEST`, `ACCEPTED`, `RELAY`, `PING` (the server drops a client after 10 s idle), `CLOSE`, `DISCONNECT` | A `RelayTransport : INetTransport` that frames our reliable-UDP packets inside `RELAY` messages |
| Cloud Save, Leaderboards, Friends | REST | Implementations behind the game's existing facades |

Limits that come with it:
- Relay connects only players of **the same UGS project and environment**. An Amoebius test build and
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

1. **Now: test everything locally in Amoebius** with the stand-in session service and the tools in
   §6. Most multiplayer bugs this project has had (B2-B29) were decision bugs in the party,
   ready-gate and spectator code. Five local processes reproduce those.
2. **Gate G2 (2026-12-15)**: build the backend that matches the shipped Unity build: **UGS**
   (Auth, Lobby, Relay over REST and the Relay protocol). Amoebius testers then use the same lobby
   service as the Unity game, for free inside the allowance.
3. **Steam** (Steamworks.NET, MIT; a new dependency to approve at that time) for Steam auth,
   achievements, and optionally Steam lobbies plus Steam Datagram Relay for the PC build. This is
   free and the strongest NAT traversal on PC.
4. **Our own servers** only if UGS's bill or limits ever bite. The seams mean it is a new
   implementation, not a rewrite.

---

## 5. What exists today (2026-10-09)

| Piece | Where | Status |
|---|---|---|
| Netcode model (approval, scene sync, spawns, variables, RPCs, ownership, parenting, transforms, named messages, scene loads) | `Wire/NetDriver.cs` | Works. LAN parties and five-process harness runs pass |
| Transports | `Wire/INetTransport.cs` | Froglet's UDP (`UdpTransport`, the default, §6.6) directly or through a relay (§6.7); TCP (`NetSocket`); an in-memory loopback for tests |
| Relay | `Wire/Relay/` | Unity Relay's protocol client and Froglet's own relay server (§6.7) |
| UGS sign-in | `Wire/Ugs/UgsAuthentication.cs` | Anonymous sign-in and session-token resume over REST; feeds UGS Relay its bearer token (§6.8). Tested against local stand-ins; the live project waits for the owner |
| Session service | `Wire/DirectoryMultiplayerService.cs` | One JSON file per session in `COSMIC_SHORE_NET_DIR` (the stand-in for UGS Lobby until P8); records an address and port, or a relay join code |
| Five-player scenario harness | `Tools/Build/prisma_party_scenarios/` | 14/14 directly, on simulated 4G, and through the relay. No engine patches since Step 0 |
| Offline switch, per-instance profile | `COSMIC_SHORE_NET=off`, `COSMIC_SHORE_PROFILE=<name>` | Works |
| Test tools | §6 | Network simulator, stats and capture, session faults, the Launcher's NET page, MCP `net_*` tools |

## 6. The tools, mapped to Unity's

| Unity | Amoebius | Step |
|---|---|---|
| Multiplayer Play Mode (virtual players with tags) | **MULTIPLAYER panel** in the Launcher: 1-4 players, each its own process, profile and save folder, one shared session folder; MCP `net_players` | 4 |
| Network Simulator (latency, jitter, loss, disconnect) | **`SimulatedTransport`**: wraps any transport. Set at launch with `COSMIC_SHORE_NET_SIM` or live with `do netsim` | 1 |
| Runtime Network Stats Monitor | **`net` overlay and `do net` verb**: per-peer bytes and messages in/out, RTT, RPCs by name, variable updates, spawned objects | 2 |
| Network Profiler | **Per-frame capture** to JSON (`do net capture`) and the session report's `net` block; a timeline view on the Launcher | 2 (capture), 4 (view) |
| *(none)* | **Session-service faults**: lobby full, rate limited, relay allocation fails, service down. `COSMIC_SHORE_NET_FAULT` or `do netfault` | 3 |
| *(none)* | **Scenario harness as a tool**: `run.sh` without patches; MCP `net_scenario` | 0, 4 |
| *(none, Unity's transport is fixed)* | **Our reliable-UDP transport** with reliable and unreliable channels | 5 |
| Relay (UGS Relay through Unity Transport) | **`RelayLink`** under our UDP transport, speaking Unity Relay's protocol; **Froglet's relay server** (`--relay-server`) speaking it too. `COSMIC_SHORE_RELAY`, NET page RELAY, `net_players relay=local`, `PRISMA_RELAY=1` | 6 |
| Authentication + Relay SDKs (sign in, allocate) | **`UgsAuthentication`** (REST) and `COSMIC_SHORE_RELAY=ugs`; **`--ugs-relay-check`** / NET page UGS RELAY CHECK proves the live path in one step | 7 |

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
- ~~The relay~~: done in Step 6 (§6.7). Our packets ride inside Relay `RELAY` messages, through UGS
  Relay or Froglet's own relay server. Steam Datagram Relay stays an option for the PC build.
- **Congestion control** past the fixed window: needed only on internet paths, measured first.
- **Delta-compressed `NetworkVariable` writes:** the first measurement (§6.4) shows they dominate
  traffic.

### 6.7 Step 6: the relay (Unity Relay's protocol, and Froglet's own relay server)

**Why.** A listen server needs the host to accept connections, and a home router does not let
strangers in. A relay is a server everyone can reach: each player sends only to it, and it forwards
between players that joined the same allocation. Nobody opens a port. UGS Relay is one. **Froglet's
relay server speaks the same protocol and the same REST shape**, so one client serves both: test
against ours on any machine, play over UGS, or run ours on a server if the bill ever asks for it
(§4.2).

**The pieces** (all under `src/CosmicShore.Engine/Networking/Wire/`):

| Piece | File | Job |
|---|---|---|
| `IDatagramLink` | `DatagramLink.cs` | Where `UdpTransport`'s datagrams go. `DirectLink` is a socket (as before Step 6); `RelayLink` goes through a relay. The transport's reliability, acks and channels are unchanged above it |
| `RelayProtocol` | `Relay/RelayProtocol.cs` | The codec, written from Unity's public protocol page (§8). No Unity code |
| `RelayLink` | `Relay/RelayLink.cs` | The client: bind, connect, keep alive, re-bind, close |
| `RelayAllocationClient` | `Relay/RelayAllocations.cs` | The Relay Allocations REST API: `POST /v1/allocate`, `/v1/joincode`, `/v1/join` |
| `FrogletRelayServer` | `Relay/FrogletRelayServer.cs` | Our relay: the UDP protocol plus the same REST endpoints |
| `RelaySessions`, `RelayTransportFactory` | `Relay/RelayTransportFactory.cs` | Hands an allocation to the driver's next Listen/Connect |

**Wire facts** (verified against the protocol page; the endianness differs per field, and a test
pins each one):

| Message | Layout | Size |
|---|---|---|
| Header | `DA 72` signature, version `00`, type | 4 |
| BIND (0) | header, accept mode `0`, nonce **u16 little-endian**, length `255`, the allocation's 255-byte connection data, HMAC-SHA256 (key = the allocation's key) over everything before it | 295 |
| BIND_RECEIVED (1) | header | 4 |
| PING (2) | header, own allocation id (16), ping number u16 | 22 |
| CONNECT_REQUEST (3) | header, own allocation id, length `255`, the host's connection data | 276 |
| ACCEPTED (6) / DISCONNECT (9) | header, from id, to id | 36 |
| RELAY (10) | header, from id, to id, length **u16 big-endian**, content (at most 1,400) | 38 + content |
| CLOSE (11) | header, own allocation id | 20 |
| ERROR (12) | header, own allocation id, code (0 invalid version, 1 not connected, 2 client/player mismatch, 3 allocation not found, 4 unauthorized, 5 self-connect, 6 timeout) | 21 |

Our transport's packets are at most 1,200 bytes (§6.6), so each fits one RELAY message.

**A session through the relay:**
1. **Host.** `CreateSessionAsync` with `UseRelay` allocates (`maxConnections` = party size - 1),
   gets a join code, hands the allocation to `RelaySessions.HostWith`, then `StartHost`. The
   driver's Listen opens `UdpTransport` over a `RelayLink`. The join code goes into the session
   record (`relayJoinCode`), in place of an address and port.
2. **Joiner.** `JoinSessionByIdAsync` reads the record, joins by code (`/v1/join` returns the
   joiner's allocation plus the host's connection data) and connects to the address `relay`.
3. **Link.** BIND every 200 ms until BIND_RECEIVED. A joiner then sends CONNECT_REQUEST every
   200 ms until ACCEPTED, which names the host's allocation. From then on our transport's own
   handshake and data run inside RELAY messages.
4. **Alive.** PING every second (the relay drops a client after 10 s of silence). An ERROR saying
   client/player mismatch or timeout re-binds with the next nonce; allocation not found,
   unauthorized, invalid version or self-connect fails the connection with the error's name.
5. **Close.** CLOSE three times when the transport is disposed.

**Froglet's relay server** enforces what UGS does: a BIND needs a valid HMAC, and a BIND from a
new address needs a higher nonce; RELAY is forwarded only between allocations linked by an
ACCEPTED connect, and only from the address the sender bound from (otherwise ERROR not connected
or client/player mismatch); a host takes at most `maxConnections` clients; a binding silent for
10 s gets ERROR timeout. Join codes use UGS's alphabet (`6789BCDFGHJKLMNPQRTW`, 6 characters,
case-insensitive). `/health` reports allocations, forwarded and refused counts.

**How to use it:**

| Where | Switch |
|---|---|
| Any player | `COSMIC_SHORE_RELAY=<allocations URL>` (a relay server's HTTP address), or `ugs` (UGS Relay, §6.8). Unset or `off` = direct. `COSMIC_SHORE_RELAY_REGION` picks a region; `COSMIC_SHORE_RELAY_SECRET` is our server's token |
| Run our relay | `CosmicShore --relay-server [UDP] [HTTP] [HOST]`: 0 = any free port; HOST is the name players reach it by (default `127.0.0.1`). It prints `COSMIC_SHORE_RELAY=...` for the players |
| Launcher NET page | Connection = **RELAY** starts our relay beside the players; **UGS RELAY** sends them through UGS |
| MCP | `net_players relay=local` (or a relay URL, or `ugs`); `net_scenario relay=true` |
| Party harness | `PRISMA_RELAY=1 bash Tools/Build/prisma_party_scenarios/run.sh` (`PRISMA_RELAY=ugs`: through a local UGS sign-in stand-in, §6.8) |

**Running ours on a server** (only if wanted; UGS Relay is the plan): run
`CosmicShore --relay-server 7780 7781 relay.example.com`, open UDP 7780 and TCP 7781, and set
`COSMIC_SHORE_RELAY=http://relay.example.com:7781` on the players. Start it with
`COSMIC_SHORE_RELAY_SECRET=<a long random value>` and give the players the same value: then only
callers carrying it can allocate (401 otherwise; `/health` stays open). **Before it faces the public
internet it still needs TLS**: the allocations endpoint is plain HTTP, so the secret and the
allocation keys cross the network in the clear. Put it behind a TLS proxy first. Testing on a LAN
or one machine needs neither.

**Measured** (2026-10-09):

| Check | Result |
|---|---|
| Codec | BIND layout and HMAC match a reference computed outside .NET (Python `hmac`); RELAY's big-endian length; every message size; the documented UGS allocation JSON parses |
| Server enforcement | A BIND signed with the wrong key is ignored; PING is echoed; a RELAY to an allocation the sender never connected to gets ERROR not connected |
| REST | Join code format, the same code twice for one allocation, case-insensitive join, 404 for an unknown code |
| Transport | Host plus 3 clients through the relay; a 300 KB frame under 15% datagram loss through the relay; a join code whose host never bound fails the connect |
| Contract | The 7 transport contract checks pass over `relay` and `sim-relay` (56/56 with the other transports) |
| Engine suite | CosmicShore.Tests 1,891/1,891 |
| MCP route | Through the MCP server over stdio: `net_players relay=nonsense` is refused before anything starts; `relay=local` started the relay and two headless players with its URL; they partied up through it (host 2/4, both names on both rosters); `net_players status` read `relay ... · 3 allocation(s) · 833 forwarded · 0 refused`, RTT 30 ms and 25.6 ms (direct on the same machine: 27.7 ms); `stop` left no relay process |
| Five-player party harness, every pilot through the relay (`PRISMA_RELAY=1`) | **14/14** (T1, T5-accept, T2b, kick, leave, T2, T5-join, launch, T4, T3, T6, net, T7, T4-lobby). The relay ended the run with 22 allocations (every solo party and every host after a party change allocates), 21,227 datagrams forwarded, 0 refused, 22 of 22 binds accepted; no pilot logged a relay error or a re-bind. T7 (host killed): the survivors saw the dead host through the 10 s silence timeout, as on a direct connection |
| The same, every pilot also on a simulated 4G line (`COSMIC_SHORE_NET_SIM=4g`) | **14/14**; the relay: 24 allocations, 32,426 forwarded, 0 refused, 24 of 24 binds. This run predates the boot line that logs the simulated line, so its 4G setting is the harness environment's, not logged per pilot; the UGS-path run below logs it |

**Not proven yet.** Nothing here has talked to UGS's own relay servers: the sign-in exists (§6.8),
and the first live call waits for the owner, because it creates players in the live project
(`Docs/MULTIPLAYER_START_HERE.md` §5.3). Only the plain `udp` endpoint is used; DTLS (`dtls`) and
WebSocket (`ws`/`wss`) endpoints are not.

### 6.8 Step 7: UGS sign-in, so the relay can be UGS's

**What UGS Relay needs from a client.** Every Relay Allocations call carries
`Authorization: Bearer <access token>`, and the token is the signed-in player's (read from Unity's
relay client package: it sends the Authentication service's access token, nothing else). So the only
piece Amoebius lacked was the sign-in.

**`UgsAuthentication`** (`Wire/Ugs/UgsAuthentication.cs`) is that sign-in, over the Player
Authentication REST API (§8):

| Call | When | Sends | Keeps |
|---|---|---|---|
| `POST https://player-auth.services.api.unity.com/v1/authentication/anonymous` | A profile's first sign-in | header `ProjectId` (and `UnityEnvironment` when one is named), body `{}` | `idToken` (the bearer), `expiresIn`, `userId`, `sessionToken` |
| `POST .../v1/authentication/session-token` | Every later sign-in, and every refresh | the same headers, `{"sessionToken": ...}` | the same; the player stays the same |

- The session token is stored per save profile (`ugs-session-token` in the profile's folder), so a
  profile stays one UGS player across runs instead of creating one per launch. A token UGS refuses
  (400, 401, 403, 404) falls back to a new anonymous player.
- The `idToken` is reused until a minute before it expires, then the player signs in again with the
  session token. Concurrent callers wait for one sign-in.
- The project id comes from the Unity project (`cloudProjectId` in `ProjectSettings.asset`, this
  game's is `3030fd69-28ab-433f-b4bd-22b9b93c5118`) unless `COSMIC_SHORE_UGS_PROJECT` names another.
  No environment is sent unless `COSMIC_SHORE_UGS_ENVIRONMENT` names one, which matches the game:
  its code sets none, so it uses the project's default environment.
- Amoebius's game-side identity (`AuthenticationService`) is still the local stand-in; only the relay
  signs in to UGS. P8 (Lobby) is where the two must become the same player.

**Using it:**

| Where | Switch |
|---|---|
| Any player | `COSMIC_SHORE_RELAY=ugs` (sessions allocate on UGS Relay as this profile's UGS player); `COSMIC_SHORE_RELAY_REGION=<region id>` to pin a region |
| One-step live check | `CosmicShore --ugs-relay-check [REGION]`, or NET page > **UGS RELAY CHECK**: two players (the same two every run: their session tokens stay in the `relaycheck` profile) sign in, allocate, join by code, connect through UGS Relay and time ten round trips. Each step logs a line; exit 0 = PASS |
| Launcher NET page | Connection = **UGS RELAY** |
| MCP | `net_players relay=ugs` |
| Tests without UGS | `COSMIC_SHORE_UGS_AUTH_URL` / `COSMIC_SHORE_UGS_RELAY_URL` point the same code at stand-ins: `Tools/Build/prisma_party_scenarios/ugs_auth_standin.py` and our relay server. `PRISMA_RELAY=ugs` runs the party harness that way |

**Measured** (2026-10-09, all against local stand-ins; nothing called UGS):

| Check | Result |
|---|---|
| `UgsAuthenticationTests` | 13/13: anonymous first, with `ProjectId` and `UnityEnvironment`; no environment header when none is named; the token reused until a minute before expiry, then renewed as the same player; the session token outlives the process (a second instance of the same profile resumes, another profile is another player); a refused token falls back to a new player; eight concurrent callers share one sign-in; a wrong project is a `UgsServiceException` with status 400; the project id read from `ProjectSettings.asset`; the relay client sends the signed-in player's token (our relay with that token as its secret allocates for it and refuses no token or another one with 401); `RelayCheck` passes through our relay and names the failed step when it cannot |
| `--ugs-relay-check` against the stand-ins | PASS: two anonymous sign-ins, allocate, join code, join, connected in 15 ms, ten round trips avg 8.3 ms. Run again: PASS, both players resumed by session token (no new players). A wrong project id: FAIL at 'sign in' with the service's 400 message |
| Five-player party harness, `PRISMA_RELAY=ugs`, every pilot on simulated 4G | **14/14**. Each pilot's own boot lines (results.json `network`) read `[net] transport: udp`, `[relay] sessions go through UGS Relay`, `[netsim] starting on COSMIC_SHORE_NET_SIM=4g: latency=60 jitter=20 loss=1`. The sign-in stand-in saw 5 sign-ins (one per pilot) for 23 relay allocations: the token is reused, not fetched per call. The relay: 28,127 forwarded, 0 refused, 23 of 23 binds |
| Engine suite | CosmicShore.Tests 1,913/1,913 |

**What the owner's live check will show** (`Docs/MULTIPLAYER_START_HERE.md` §5.3): the region UGS
picked, a join code, `connected through the relay`, and ten round trips whose average is about
twice the round trip from this PC to that region's relay (both players are on this PC, so each frame
goes out and back twice). A failure names its step: `sign in` (project id, network, or the project's
Authentication settings), `allocate` / `join code` / `join` (Relay not enabled for the project, or
rate limited: the status code says which), or `bind and connect` (UDP to the relay blocked).

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
| 6 | Relay: Unity Relay's protocol under our UDP transport, Froglet's relay server, session integration, tool switches | Done 2026-10-09 | `RelayTests` 10/10; contract checks over `relay` and `sim-relay`; `MultiplayerRunTests` 16/16; Launcher tests 21/21; CosmicShore.Tests 1,891/1,891; two players partied up through `net_players relay=local`; the five-player party harness through the relay: 14/14 (§6.7) |
| 7 | UGS sign-in for the relay, `COSMIC_SHORE_RELAY=ugs`, the one-step UGS relay check, our relay's secret | Built 2026-10-09; the live check waits for the owner | `UgsAuthenticationTests` 13/13; `--ugs-relay-check` PASS against local stand-ins (twice, the second resuming both players); CosmicShore.Tests 1,913/1,913; party harness via the UGS path on 4G: 14/14 (§6.8) |
| G2 | UGS Lobby (P8) | Next | The relay half of UGS is done; Lobby replaces the session folder |

## 8. Sources

- Unity Relay message protocol (for alternative engines):
  <https://docs.unity.com/en-us/mps-sdk/advanced-config/relay-message-protocol>
- Relay REST API (allocations, QoS): <https://docs.unity.com/en-us/mps-sdk/advanced-config/relay-rest-api>;
  the Allocations reference: <https://docs.unity.com/legacy-services-docs/relay-allocations/v1/>
- Player Authentication REST API (anonymous and session-token sign-in):
  <https://docs.unity.com/legacy-services-docs/player-auth/v1/>
- Unity Companion License: <https://unity.com/legal/licenses/unity-companion-license>
- UGS pricing: <https://unity.com/products/gaming-services/pricing>
- How Relay is priced: <https://support.unity.com/hc/en-us/articles/4410136449812-How-is-the-Relay-Service-Priced>
