# Multiplayer — START HERE

**Read this before any multiplayer work on this branch (`Ys-bleeding-edge`), in Unity or in Amoebius.**
It holds what the owner wants, the rules that do not change, where everything lives, what is
done, what the owner still has to test by hand, and what comes next. Each section points to the
detailed document; this file stays short enough to read first. Keep it current: a session that
finishes a step updates §4 and §6 in the same commit.

Last updated: 2026-10-14 (Amoebius online is UGS only: §4).

---

## 1. What the owner wants

In the owner's words, condensed:

- **A very good four-player multiplayer game, using UGS** (Unity Gaming Services: Auth, Lobby,
  Relay). The fewest network problems a player can see, and the best performance.
- **As free as possible.** Use free tiers and free platforms (UGS's free allowance, Steam's free
  relay for the PC build), and run servers of our own only if the bill ever demands it.
- **Our own networking system in our own engine, Amoebius** (`Port/`), so multiplayer can be built
  and tested **without Unity**, with **built-in tools like Unity's** (Multiplayer Play Mode,
  Network Simulator, stats monitor), and more.
- **Clean, step-by-step testing.** The owner tests by hand later; every session leaves a list of
  exactly what to test and what the result should be (§5).
- The Unity build keeps shipping on Netcode for GameObjects + UGS. Amoebius runs the same game code.

## 2. Rules every session keeps

These are on top of the root `CLAUDE.md`; they are decisions the owner has already made.

- **A party is 4. Never 6, anywhere** (B25: the session's own seat count is the authority).
- **Push only to `Ys-bleeding-edge`.** No pull request unless the owner asks. Fetch before
  pushing; merge (never rebase or force-push) someone else's commits.
- **Do not change Unity versions or packages.** A new dependency in Amoebius (for example
  Steamworks.NET) is flagged to the owner first.
- **Never accept consent or a privacy prompt on the owner's or a player's behalf** (harnesses
  press "No thanks").
- **`/verify-unity` gates every Unity C# commit.** A cloud session has no editor: say so in the
  commit and add the change to `Docs/UNITY_VERIFICATION_CHECKLIST.md` with an MPPM repro. Never
  claim a verification that did not run.
- **Amoebius (`Port/`) never changes the Unity project** (`Port/CLAUDE.md`). A commit is either
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
| Amoebius's multiplayer: licensing, backends, every tool, the UDP transport, measurements | `Port/docs/MULTIPLAYER.md` |
| The Unity-side hardening programme (Blocks 1-6, status, decisions already made) | `Docs/prompts/MULTIPLAYER_HARDENING_PROMPT.md` |
| Every party bug, with status | `Docs/PartySystem/BUGS.md` |
| MPPM test procedures (smoke, stress, failure gates) | `Docs/PartySystem/TESTS.md` |
| What each unverified Unity change needs in the editor | `Docs/UNITY_VERIFICATION_CHECKLIST.md` |
| Party architecture | `Docs/PartySystem/ARCHITECTURE.md`, `Docs/MultiplayerArchitecture/` |
| Five-player scenario harness (runs the real game code in Amoebius against local UGS stand-ins, no Unity) | `Tools/Build/prisma_party_scenarios/` (README) |
| The game's edit-mode tests, run in Amoebius | `Tools/Build/prisma_edit_mode_tests/run.sh` |
| Amoebius online: START vs START ONLINE, `+ PLAYER 2`, SETTINGS > ONLINE, F3 diagnostics | `Port/docs/LAUNCHER.md` § START ONLINE |
| Amoebius on UGS Lobby + Relay (how a session becomes a lobby, errors, the UGS environment) | `Port/docs/MULTIPLAYER.md` §6.10, `Port/docs/ARCHITECTURE.md` §8 |
| Netcode, transports, relay, UGS clients, simulator, stats, faults (code) | `Port/src/CosmicShore.Engine/Networking/Wire/` (`Ugs/` for sign-in, Lobby and sessions) |

## 4. Where things stand (2026-10-14)

### Amoebius (our engine): online is UGS only

Since 2026-10-14 Amoebius plays online exactly one way, the way the Unity build does
(`Port/docs/MULTIPLAYER.md` §6.10):

- **START** plays offline (`COSMIC_SHORE_NET=off`, one process, no network).
- **START ONLINE** signs in to UGS as this profile's player and plays through **UGS Lobby**
  (presence, parties, sessions) and **UGS Relay** (the match traffic). It signs in to the
  **`amoebius` UGS environment** (it exists in the dashboard), which keeps Amoebius's lobbies out
  of the Unity build's queries: the two netcodes cannot play each other. SETTINGS > ONLINE >
  UGS environment overrides it.
- **`+ PLAYER 2`** on the PLAY page, beside a running online game, starts a second online game on
  the same PC as the `player2` profile (its own save and UGS player, console lines prefixed `[P2]`):
  the one-PC party test. **CLOSE P2** closes it.
- **F3** in any game shows the diagnostics overlay: frame, memory, scene, and online status (stack,
  UGS sign-in, lobby, match role and relay join code, per-peer RTT and traffic).

Removed with it, and no longer to be used or documented as live: the self-hosted online server,
LAN discovery, shared-folder sessions (`COSMIC_SHORE_NET_DIR`, `DirectoryMultiplayerService`),
`COSMIC_SHORE_NET_SERVER`, `COSMIC_SHORE_RELAY`, `--server`, `--relay-server`, the launcher's NET
page and the MCP `net_*` tools. What those tools tested stays in the engine and works on any
online game through its control port: the simulator (`COSMIC_SHORE_NET_SIM`, `do netsim`), session
faults (`COSMIC_SHORE_NET_FAULT`, `do netfault`) and stats (`do net`).

| Step | What | State |
|---|---|---|
| 0 | `--realtime` headless pacing, save-path fallback, missing SDK error codes | Done |
| 1 | Network simulator (latency, jitter, loss, bandwidth, pulled cable; per player, live) | Done (`COSMIC_SHORE_NET_SIM`, `do netsim`) |
| 2 | Network stats monitor (`do net`, `net capture`, the F3 overlay's NETWORK block) | Done |
| 3 | Session-service faults (full, 429, relay failure, outage, slow) | Done (`COSMIC_SHORE_NET_FAULT`, `do netfault`) |
| 4 | ~~Launcher NET page + MCP `net_*` tools~~ | Retired 2026-10-14 with local play; `+ PLAYER 2` and two PCs replace it |
| 5 | Froglet's own UDP transport + unreliable channel; **the default** | Done |
| 6 | Relay: Unity Relay's wire protocol under our UDP transport; Froglet's relay server (`FrogletRelayServer`) is the tests' stand-in for UGS Relay | Done |
| 7 | UGS sign-in (anonymous + session-token resume), UGS Relay as that player, the one-step UGS CHECK | Built and tested against local stand-ins. **The live check waits for you (§5.3)** |
| 8 | UGS Lobby as the session service; START / START ONLINE; `+ PLAYER 2`; F3 diagnostics | Built 2026-10-14 (`UgsMultiplayerTests` 6/6 on stand-ins); party harness on the stand-ins: **14/14** (2026-10-14, `Tools/Build/prisma_party_scenarios/README.md` § State). **The live test waits for you (§5.3)** |

**When you merge into this branch, run `NetworkWiringGuardTests` and `NetStackTests`.** On 2026-10-14 a
merge took one side of `PlayerBoot` whole and switched the UDP transport, the relay, the simulator and the
faults off with every other test green; those two tests fail if a merge drops that wiring again.

### Unity game: the hardening programme

| Block | What | State |
|---|---|---|
| 1 | Observability (session record, NetDiag, `net` console) | Landed; needs MPPM confirmation |
| 2 | Verify what landed | Owner's MPPM runs (§5.2) |
| 3 | Test level: the five-process party scenarios | Landed. 14/14 on Amoebius, on the UGS stand-ins since 2026-10-14 |
| 4 | Offline mode, seven cases | Landed (L1 tests); needs a player build pass |
| 5 | **Push instead of poll — a launch blocker** (week one is 10-40 concurrent players) | Not started |
| 6 | Reconnection grace | Not started |

Open bugs that matter: **B29 Count case** (a party that just shrank reads as full; Block 5 cures
it). The B29 Session case ("defect 5") is fixed but unverified in Unity.

**Fixed 2026-10-14: networked Bloomrush matches crawled in Amoebius (not a network bug).** Once a
match started, frames fell to ~4 a second, and the pre-game cinematic outlasted the party harness's
3-minute wait for GO, so the harness stopped at T4 (8/14). The flora growth in the trace (the Rampage
cell grows ~60k prisms by design) was not the cost. `dotnet-trace` put ~95% of the main thread in
`CrystalChargeArcs.LateUpdate`, because Amoebius's `Renderer.isVisible` read true for every renderer and
every Charge crystal rebuilt its arc mesh every frame. `isVisible` now means what Unity's does (drawn by a
camera in the last rendered frame; `Port/src/CosmicShore.Engine/Rendering/RenderVisibility.cs`). One
pilot runs the grown arena at 54-61 fps, and the harness passes 14/14.

## 5. What the owner tests by hand

Run these when you sit down with the machines. Each has the result to expect; a different result
is a bug, and the run's console log is what a session needs to fix it.

### 5.1 Amoebius offline (no Unity, no UGS; Windows, from Amoebius)

A fresh profile asks for a birth year, data-collection consent (your choice) and a username the
first time; later runs on the same profile skip them.

1. **START plays offline.** PLAY > **START**. Expect the menu; press **F3**: the NETWORK line reads
   `offline (START)`. Nothing is sent to UGS.
2. **F3 overlay.** Press F3 again: the overlay hides. SETTINGS > GAME > Diagnostics overlay on, START:
   the game starts with it shown.

Everything online in Amoebius goes through the real UGS project, so it is §5.3.

### 5.2 Unity (MPPM, 3-4 virtual players)

Prerequisite for every test: **each virtual player has its own tag** (P2, P3, P4), or they share
one UGS account (`Docs/PartySystem/TESTS.md` § prerequisites).

**First, after pulling (MPPM 2.0, 2026-10-14):** let the project compile. Expect no "Script Updating
Consent" dialog and no errors in `AuthenticationServiceFacade.cs` or `MultiplayerSetup.cs`; a clone still
signs in on its own profile. If anyone clicked "Yes" on that dialog before pulling, discard their local
rewrite of those two files first (checklist entry "No more Script Updating Consent prompt").

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

This touches the real UGS project (`3030fd69-28ab-433f-b4bd-22b9b93c5118`, read from
`ProjectSettings.asset`), in its **`amoebius` environment**: the first run of each profile creates
one anonymous player there, and every later run signs the same one back in (its session token stays
in the profile's folder). Nothing before this step has called UGS. Every step reads the same in the
F3 overlay of each game; when a step fails, paste that game's console log to the next session.

**Step 1: the one-click check.** Amoebius > SETTINGS > ONLINE > **UGS CHECK** (or, from a repo
checkout with the .NET 10 SDK: `dotnet run --project Port/src/CosmicShore.Player -- --ugs-relay-check`).
Expect, in the log, in order:

```
[relay-check] UGS project 3030fd69-..., environment amoebius, region chosen by the service
[relay-check] host signed in (anonymous): player ...
[relay-check] joiner signed in (anonymous): player ...
[relay-check] allocated: region <a UGS region>, relay <ip:port>, allocation <id>
[relay-check] join code XXXXXX
[relay-check] joined: allocation <id>, relay <ip:port>
[relay-check] connected through the relay in N ms
[relay-check] 10 round trips: avg N ms ...
[relay-check] PASS
```

The average is about twice your ping to that relay region (both players are on this PC). Click it
again: both lines say `(session-token)` with the same player ids, so no new players were made. A
FAIL names its step (`Port/docs/MULTIPLAYER.md` §6.8 lists what each one means).

**Step 2: two profiles on one PC (`+ PLAYER 2`).**

1. PLAY > **START ONLINE**. Expect in the console `[online] UGS project 3030fd69-... · environment
   amoebius: Lobby and Relay`, then `[online] signed in to UGS (anonymous) as <player id>` once the
   menu signs in (`(session-token)` on later runs). F3: `online (START ONLINE)`, `stack ... relay >
   udp · sessions ... ugs`, `ugs signed in as <id>`. A sign-in failure says so in red in F3 and in
   the console, with the environment it tried.
2. **`+ PLAYER 2`**. Expect a second, smaller window, its console lines prefixed `[P2]`, signing in
   as a **different** player id.
3. **Party invite.** In the first game: ARCADE, the party panel's **+**, invite player 2's name.
   Accept in player 2. Expect player 2 seated in player 1's party, both rosters showing both names.
   F3 on both: a `lobby` line for the party.
4. **Match.** Player 1 opens a card (Bloomrush); both press Start. Expect both games in
   the match: F3 shows player 1 `match HOST · relay code XXXXXX` with `client 1` and an RTT, player 2
   `match CLIENT` with `host` and an RTT, and **each sees the other's vessel flying**. The console
   shows the host's `[relay] hosting through <a UGS relay> (join code XXXXXX)` and `[P2] [relay]
   joining XXXXXX`.
5. **Leave.** Player 2 leaves the party. Expect player 2 back in its own menu, player 1's party
   back to 1/4. **CLOSE P2**.

**Step 3: two PCs (or a PC and a laptop on a phone hotspot).** On each, START ONLINE (each PC is its
own profile and its own UGS player). Repeat Step 2's invite, accept, match and leave across the two
machines. Expect the same results, with the RTT now about twice each machine's ping to the relay
region. This is the first run across two real networks: whoever runs it, record what happened here
(date, the two networks, the RTTs F3 showed).

## 6. What comes next, in order

Each step has the check that proves it done. Update the state in §4 when a step lands.

**Amoebius track (internet play in our engine, on UGS):** P6-P8 are built; what is left of them
is the owner's live test (§5.3).

| # | Step | Done when |
|---|---|---|
| ~~P6~~ | ~~**Relay.**~~ Done 2026-10-09 (§4). Unity Relay's protocol under our UDP transport, Froglet's relay server (same protocol, same REST shape), session integration, switches in every tool | Contract checks pass over `relay`/`sim-relay`; party harness 14/14 with every pilot through the relay |
| P7 | **UGS Auth.** Built 2026-10-09: anonymous sign-in and session-token resume over REST, the `idToken` as UGS Relay's (and Lobby's) bearer, the one-step UGS CHECK (§4) | ~~Unit tests against a local stand-in~~ (13/13); **the owner's UGS CHECK (§5.3 step 1) still to run** |
| P8 | **UGS Lobby** as Amoebius's session service. Built 2026-10-14: START ONLINE is UGS only, `+ PLAYER 2`, F3 (§4) | ~~The party harness passes on the local stand-ins~~ (14/14, 2026-10-14); **two profiles and then two PCs form a party and play a match on real UGS (§5.3 steps 2-3)** |
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
3. Amoebius work: read `Port/CLAUDE.md` and `Port/docs/MULTIPLAYER.md`; build with
   `dotnet build Port/src/CosmicShore.Player` (the .NET 10 SDK); test with
   `dotnet test Port/tests/CosmicShore.Tests`.
4. Party logic: run `bash Tools/Build/prisma_party_scenarios/run.sh` before and after (~15 min,
   14/14 expected; add `COSMIC_SHORE_NET_SIM=4g` for the bad-line run). Every pilot signs in, allocates
   and finds parties through local UGS stand-ins (sign-in, Relay, Lobby), as START ONLINE does on the
   real service. Never point a session's tests at the live UGS project: that is the owner's §5.3.
5. Unity C#: follow §2's `/verify-unity` rule and add the checklist entry.
6. Finish by updating §4 and §6 here.
