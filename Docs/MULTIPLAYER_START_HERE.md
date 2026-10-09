# Multiplayer — START HERE

**Read this before any multiplayer work on this branch (`Ys-bleeding-edge`), in Unity or in Prisma.**
It holds what the owner wants, the rules that do not change, where everything lives, what is
done, what the owner still has to test by hand, and what comes next. Each section points to the
detailed document; this file stays short enough to read first. Keep it current: a session that
finishes a step updates §4 and §6 in the same commit.

Last updated: 2026-10-09.

---

## 1. What the owner wants

In the owner's words, condensed:

- **A very good four-player multiplayer game, using UGS** (Unity Gaming Services: Auth, Lobby,
  Relay). The fewest network problems a player can see, and the best performance.
- **As free as possible.** Use free tiers and free platforms (UGS's free allowance, Steam's free
  relay for the PC build), and run servers of our own only if the bill ever demands it.
- **Our own networking system in our own engine, Prisma** (`Port/`), so multiplayer can be built
  and tested **without Unity**, with **built-in tools like Unity's** (Multiplayer Play Mode,
  Network Simulator, stats monitor), and more.
- **Clean, step-by-step testing.** The owner tests by hand later; every session leaves a list of
  exactly what to test and what the result should be (§5).
- The Unity build keeps shipping on Netcode for GameObjects + UGS. Prisma runs the same game code.

## 2. Rules every session keeps

These are on top of the root `CLAUDE.md`; they are decisions the owner has already made.

- **A party is 4. Never 6, anywhere** (B25: the session's own seat count is the authority).
- **Push only to `Ys-bleeding-edge`.** No pull request unless the owner asks. Fetch before
  pushing; merge (never rebase or force-push) someone else's commits.
- **Do not change Unity versions or packages.** A new dependency in Prisma (for example
  Steamworks.NET) is flagged to the owner first.
- **Never accept consent or a privacy prompt on the owner's or a player's behalf** (harnesses
  press "No thanks").
- **`/verify-unity` gates every Unity C# commit.** A cloud session has no editor: say so in the
  commit and add the change to `Docs/UNITY_VERIFICATION_CHECKLIST.md` with an MPPM repro. Never
  claim a verification that did not run.
- **Prisma (`Port/`) never changes the Unity project** (`Port/CLAUDE.md`). A commit is either
  Port-only or Unity-side, never both.
- **Never copy code from Unity's Netcode, Unity Transport or Multiplayer Services packages.**
  They are under the Unity Companion License. Reading them to learn a wire fact is allowed
  (`Port/docs/MULTIPLAYER.md` §2).
- **Do not test against the live UGS project without the owner's go-ahead.** A real sign-in
  creates a player account in project `3030fd69-28ab-433f-b4bd-22b9b93c5118`. Use the local
  stand-ins; the owner runs the live test (§5.3).
- **Skip non-multiplayer issues in multiplayer work** (Grizzly, lizard, Sparrow, RaceRankToast
  failures are known and not ours).
- The two LOCKED party decisions (two-level presence lobby + party session, eager per-user Relay)
  are given: `Docs/prompts/MULTIPLAYER_HARDENING_PROMPT.md` § Standing constraints.

## 3. Where everything is

| What | Where |
|---|---|
| Prisma's multiplayer: licensing, backends, every tool, the UDP transport, measurements | `Port/docs/MULTIPLAYER.md` |
| The Unity-side hardening programme (Blocks 1-6, status, decisions already made) | `Docs/prompts/MULTIPLAYER_HARDENING_PROMPT.md` |
| Every party bug, with status | `Docs/PartySystem/BUGS.md` |
| MPPM test procedures (smoke, stress, failure gates) | `Docs/PartySystem/TESTS.md` |
| What each unverified Unity change needs in the editor | `Docs/UNITY_VERIFICATION_CHECKLIST.md` |
| Party architecture | `Docs/PartySystem/ARCHITECTURE.md`, `Docs/MultiplayerArchitecture/` |
| Five-player scenario harness (runs the real game code in Prisma, no Unity) | `Tools/Build/prisma_party_scenarios/` (README) |
| The game's edit-mode tests, run in Prisma | `Tools/Build/prisma_edit_mode_tests/run.sh` |
| Prisma's Launcher NET page (Multiplayer Play Mode for Prisma) | `Port/docs/LAUNCHER.md` § NET |
| Netcode, transports, simulator, stats, faults (code) | `Port/src/CosmicShore.Engine/Networking/Wire/` |

## 4. Where things stand (2026-10-09)

### Prisma (our engine): built and tested

| Step | What | State |
|---|---|---|
| 0 | `--realtime` headless pacing, save-path fallback, missing SDK error codes | Done |
| 1 | Network simulator (latency, jitter, loss, bandwidth, pulled cable; per player, live) | Done |
| 2 | Network stats monitor (`do net`, window titles, NET page, `net capture`) | Done |
| 3 | Session-service faults (full, 429, relay failure, outage, slow) | Done |
| 4 | Launcher NET page + MCP `net_*` tools (1-4 players at once) | Done |
| 5 | Froglet's own UDP transport + unreliable channel; **the default** | Done. Party harness 14/14 clean, 14/14 on simulated 4G after the B29 fix |
| 6 | Relay: Unity Relay's wire protocol under our UDP transport + Froglet's own relay server | Done. Party harness 14/14 through the relay (`Port/docs/MULTIPLAYER.md` §6.7) |

### Unity game: the hardening programme

| Block | What | State |
|---|---|---|
| 1 | Observability (session record, NetDiag, `net` console) | Landed; needs MPPM confirmation |
| 2 | Verify what landed | Owner's MPPM runs (§5.2) |
| 3 | Test level: the five-process party scenarios | Landed (14/14 on Prisma) |
| 4 | Offline mode, seven cases | Landed (L1 tests); needs a player build pass |
| 5 | **Push instead of poll — a launch blocker** (week one is 10-40 concurrent players) | Not started |
| 6 | Reconnection grace | Not started |

Open bugs that matter: **B29 Count case** (a party that just shrank reads as full; Block 5 cures
it). The B29 Session case ("defect 5") is fixed but unverified in Unity.

## 5. What the owner tests by hand

Run these when you sit down with the machines. Each has the result to expect; a different result
is a bug, and the run's console log is what a session needs to fix it.

### 5.1 Prisma tools (no Unity; Windows, from Prisma.exe)

Open **Prisma > NET**. A fresh profile asks for a birth year, data-collection consent (your
choice) and a username the first time; later runs on the same profile skip them.

1. **Four players start and tile.** Players = 4, Transport = UDP, Start in = Bootstrap, START.
   Expect four windows in a 2x2 grid, titles `Cosmic Shore · player1 · ...` up to `player4`.
2. **They party up.** In player1 press F7, run `party invite player2` (use the names shown in
   the windows), accept in player2. Expect player2 seated; LIVE shows player1 as `server` with
   `client 1` and an RTT under ~50 ms, player2 as `client`.
3. **Simulated line.** In LIVE set player2's line to `4g`. Expect its RTT to rise by roughly
   +120-160 ms within ~10 s, and the window title to show `sim latency=60 ...`.
4. **Pull the cable.** PULL CABLE on player2. Expect nothing for 10 s, then player2 back in its
   own menu and player1's party back to 1/4. PLUG IN afterwards.
5. **Session fault.** Fault `full` on player3, then `party join player1` from player3's F7
   console. Expect the toast "That party is full." and player3 in its own menu. Join again:
   seated.
6. **Capture.** CAPTURE 10 S on any player. Expect a console line naming a JSON file with 600
   frames.
7. **TCP still works.** CLOSE, set Connection = TCP, repeat step 2. Expect the same result.
8. **Through the relay.** CLOSE, set Connection = RELAY, START, repeat step 2. Expect the log to
   show `[relay] listening ...` first, then each player's `[relay] sessions go through the relay`
   and, as it opens its own party, `[relay] hosting through ... (join code XXXXXX)`. When player2
   accepts: `[P2] [relay] joining XXXXXX` with player1's code. Seated as in step 2, with about the
   same RTT as direct on one PC (measured: 25-30 ms). Then repeat step 4 (pull the cable): same
   result as direct.

### 5.2 Unity (MPPM, 3-4 virtual players)

Prerequisite for every test: **each virtual player has its own tag** (P2, P3, P4), or they share
one UGS account (`Docs/PartySystem/TESTS.md` § prerequisites).

Run in this order. The checklist entry named in the last column holds the exact steps.

| # | Test | Expect | Checklist entry |
|---|---|---|---|
| 1 | Smoke gate S1-S4, S9 | All pass | `TESTS.md` § Smoke gate |
| 2 | Host drop, then the new host invites at once (repeat 5×) | The Accept seats the guest; never "party is no longer available" | "B29 Session case (defect 5)" |
| 3 | Two players press Join on a 3/4 party together | 4/4; the loser sees "That party is full." | "`party` console command, the five-process party scenarios…" |
| 4 | A member leaves at the ready gate / the arcade launch lobby | The other three still launch | same entry (B20) |
| 5 | A guest leaves mid-match | Its ship flies on under AI; its score row stays | same entry (B21) |
| 6 | Offline: the seven cases | As listed | "Offline mode: the seven cases tested…" |
| 7 | `net` / `net dump` in the F7 console on host and guest | Each names its role; the host's record holds the lifecycle marks | "Session record…" |
| 8 | Request discipline: idle 5 min in the menu | No 429s; GETs within the entry's budget | "Party request discipline — review Phases 0–1" |

### 5.3 The live UGS test (when the owner says go)

This touches the real UGS project, so it waits for the owner. When §6 step P7 lands, a session
gives the exact command here. Expected shape: two Prisma players on two different networks (two
PCs, or a PC and a laptop on a phone hotspot) form a party through UGS Lobby and play a Bloomrush
match through UGS Relay.

**What you can already run across two networks, with no UGS involved** (Froglet's own relay;
P6). On a machine both players can reach (a PC with a forwarded port, or any small server), run
`CosmicShore --relay-server 7780 7781 <its public name or IP>` and open UDP 7780 and TCP 7781.
On each player set `COSMIC_SHORE_RELAY=http://<that name>:7781` and
`COSMIC_SHORE_NET_DIR` to a folder both can see (a shared drive). The session folder is still the
lobby stand-in until P8, which is why it has to be shared. Do this only on a server you control,
for a test: the relay's allocations endpoint has no authentication yet (`MULTIPLAYER.md` §6.7).
This route has run on one machine (the harness), not yet across two real networks: whoever tries
it first, record what happened here.

## 6. What comes next, in order

Each step has the check that proves it done. Update the state in §4 when a step lands.

**Prisma track (internet play in our engine, on UGS):**

| # | Step | Done when |
|---|---|---|
| ~~P6~~ | ~~**Relay.**~~ Done 2026-10-09 (§4). Unity Relay's protocol under our UDP transport, Froglet's relay server (same protocol, same REST shape), session integration, switches in every tool | Contract checks pass over `relay`/`sim-relay`; party harness 14/14 with every pilot through the relay |
| P7 | **UGS Auth**: anonymous sign-in over REST (`POST https://player-auth.services.api.unity.com/v1/authentication/anonymous`, header `ProjectId`), its `idToken` sent as `Authorization: Bearer` on the relay allocation calls (what Unity's own relay client sends), refreshed before `expiresIn`; the `sessionToken` kept per profile to sign the same player back in. The allocations client already speaks UGS's REST shape. `COSMIC_SHORE_RELAY=ugs` turns it on | Unit tests against a local stand-in of the sign-in endpoint; the owner's live test (§5.3) allocates on real UGS |
| P8 | **UGS Lobby** as Prisma's session service (create, query, join, heartbeat, player data, the presence lobby) | The party harness passes on the local stand-in; two Prisma players on two networks form a party on real UGS |
| P9 | **Less traffic.** `NetworkVariable` writes dominate (7:1 over transforms, `MULTIPLAYER.md` §6.4): batch per tick, send deltas | `net` shows the drop on the same scenario, with the harness still 14/14 |
| P10 | Congestion control past the fixed window, measured on a real internet path | A 4-player match on two networks holds RTT and shows no resend storms in `net` |

**Unity track (launch-critical):**

| # | Step | Done when |
|---|---|---|
| U1 | The owner's MPPM runs in §5.2 | Each 🔴 checklist entry becomes 🟢 with a date |
| U2 | **Block 5: push instead of poll** (hardening prompt) | GETs ≤ 4/min/client at rest; invite visible < 1 s p95; no stale-index lines in a 30-minute 4-client run |
| U3 | Block 6: reconnection grace | A 15 s network pull mid-match hands the vessel back with no menu |

## 7. How a session starts here

1. Read this file, then the document for your track (§3).
2. `git fetch origin Ys-bleeding-edge` and merge it before working.
3. Prisma work: read `Port/CLAUDE.md` and `Port/docs/MULTIPLAYER.md`; build with
   `dotnet build Port/src/CosmicShore.Player` (the .NET 10 SDK); test with
   `dotnet test Port/tests/CosmicShore.Tests`.
4. Party logic: run `bash Tools/Build/prisma_party_scenarios/run.sh` before and after (~15 min,
   14/14 expected; add `COSMIC_SHORE_NET_SIM=4g` for the bad-line run, `PRISMA_RELAY=1` to send
   every pilot through the relay).
5. Unity C#: follow §2's `/verify-unity` rule and add the checklist entry.
6. Finish by updating §4 and §6 here.
