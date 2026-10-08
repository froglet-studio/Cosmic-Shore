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
| Transport | **Our own reliable UDP** (Step 5) with two channels: *reliable-ordered* (RPCs, spawns, variables, scenes) and *unreliable-latest* (transforms) | TCP stalls every message behind one lost packet (head-of-line blocking). On a 1% loss line that means visible vessel hitches. Transforms only ever need the newest value |
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
| Transport seam | `Wire/INetTransport.cs` | Landed. TCP (`NetSocket`) is the only real implementation; tests use an in-memory loopback |
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

Faults the directory session service can be told to raise, so the game's error paths
(`UgsRequestPolicy`, the offline fallback, the party's full-lobby message) run without a real
outage: `full` (next join reports a full session), `ratelimit` (next N calls raise a 429-shaped
`SessionException`), `relayfail` (allocation fails), `down` (every call fails until cleared),
`slow=MS` (every call waits).

### 6.4 Step 2: stats and capture

`NetDriver` counts, per peer and in total: frames and bytes in and out by message kind, RPC calls
by method name, NetworkVariable updates, spawns and despawns, and round-trip time from the
existing `TimePing`/`TimePong` clock exchange. `do net` prints it. The debug overlay shows it.
`do net capture N` writes N frames of per-frame counters to JSON, and the session report gains a
`net` block.

### 6.5 Step 4: the Launcher's MULTIPLAYER panel and MCP tools

Players 1-4 (a party is always four), each row with a name (`PilotA`...), a profile, windowed or
headless, and the simulator preset it runs under. **START ALL** creates a fresh session folder,
gives each player its own save folder and control port, and tiles the windows. **STOP ALL** ends
them. Each row has a log tab and a control-port console. MCP: `net_players` (start/stop N),
`net_sim`, `net_fault`, `net_stats`, `net_scenario`.

### 6.6 Step 5: our reliable-UDP transport

One UDP socket per process. Packets carry a sequence number and an ack field. The
**reliable-ordered** channel resends on a timeout derived from the measured RTT and delivers in
order. The **unreliable-latest** channel drops anything older than what it has. Frames bigger than
a packet are fragmented. A connect handshake with a token prevents spoofed connects. The same
`NetTransportContractTests` that hold TCP to the contract hold it too. Then NetDriver moves
transforms to the unreliable channel, which changes `INetTransport.Send` (it gains a channel
argument, and TCP treats both channels as reliable).

## 7. Status

| Step | What | Status | Evidence |
|---|---|---|---|
| Doc | This file | Done 2026-10-08 | |
| 0 | Real-time pacing, API gaps, save-path fallback | Done 2026-10-08 | `RealtimePacerTests` 4/4; 1200 headless frames: 11.9 s unpaced, 28.3 s with `--realtime`; player builds (it did not: 19 missing `SessionError` codes) |
| 1 | Network simulator | Done 2026-10-08 | `SimulatedTransportTests` 14/14; the 7 transport contract checks pass behind a bad line (latency 5, jitter 10, loss 20%) over TCP and loopback; `NetDriverTransportTests` approves a real TCP client behind 40 ms each way in >= 80 ms; 5 repeat runs stable |
| 2 | Stats, `do net`, capture | Planned | |
| 3 | Session-service faults | Planned | |
| 4 | Launcher MULTIPLAYER panel, MCP tools | Planned | |
| 5 | Reliable-UDP transport, unreliable transforms | Planned | |
| G2 | UGS backend (Auth, Lobby, Relay protocol) | After gate G2 | |

## 8. Sources

- Unity Relay message protocol (for alternative engines):
  <https://docs.unity.com/en-us/mps-sdk/advanced-config/relay-message-protocol>
- Relay REST API (allocations, QoS): <https://docs.unity.com/en-us/mps-sdk/advanced-config/relay-rest-api>
- Unity Companion License: <https://unity.com/legal/licenses/unity-companion-license>
- UGS pricing: <https://unity.com/products/gaming-services/pricing>
- How Relay is priced: <https://support.unity.com/hc/en-us/articles/4410136449812-How-is-the-Relay-Service-Priced>
