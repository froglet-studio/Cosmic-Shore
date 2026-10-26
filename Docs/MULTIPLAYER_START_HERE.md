# Multiplayer — START HERE

**Read this before any multiplayer work on this branch (`Ys-bleeding-edge`), in Unity or in Amoebius.**
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
| A multiplayer test's logs, every player, one timeline (sent by the after-test "Send to Claude") | `python3 Tools/Build/net_logs.py collect` / `show 1`; `Docs/DIAGNOSTICS.md` § "Multiplayer test logs" |
| Party architecture | `Docs/PartySystem/ARCHITECTURE.md`, `Docs/MultiplayerArchitecture/` |
| Who sits where: every person's seat and hull (`Player.NetStation`), the one swap ring (seats, then the domain's hulls), riders on another person's crew vessel, the crew relay RPCs | `Docs/CREW_STATIONS.md`, `Assets/_Scripts/Controller/Multiplayer/CrewStations.cs` |
| Five-player scenario harness (runs the real game code in Amoebius, no Unity; down until it moves to the UGS stand-ins, §4) | `Tools/Build/prisma_party_scenarios/` (README) |
| The game's edit-mode tests, run in Amoebius | `Tools/Build/prisma_edit_mode_tests/run.sh` |
| Amoebius online play: START ONLINE, + PLAYER 2, SETTINGS > ONLINE (UGS environment, UGS CHECK) | `Port/docs/LAUNCHER.md` § START ONLINE |
| Netcode, transports, relay, UGS sign-in and Lobby, simulator, stats, faults (code; built in one place, `NetStack.cs`) | `Port/src/CosmicShore.Engine/Networking/Wire/` |

### 3.1 What reaches Claude by itself, and the sweep that reads it

Nobody has to press anything. After any Play in the editor that went online (a party, a match, solo against AI
online), the Bug Hunt's after-Play send (`BugHuntAutoSend`) pushes whatever is new to `bugledger/<user>`:

| What | Written by | Read with |
|---|---|---|
| The multiplayer test log, every player of the test (session marks: joins, `stall`, `relayDrop`, 429s, config hash) | `NetTestLogs`, `NetSessionRecorder` | `net_logs.py collect`, `show <n>` |
| A Load Time Insights report of every load while online: each launch, and each return to the menu (`loadKind: "menuReturn"`) | `LoadInsights` (records unarmed while online since 2026-10-25) | `net_logs.py show <n>`, section "Loads"; RELAY RISK marks a frame over 9 s |
| New red console errors | Bug Ledger | `bug_ledger.py` |

**The multiplayer sweep** (a scheduled Claude session, once a day; it can also be run by hand from any session):

1. `git fetch origin`, check out `Ys-bleeding-edge` and merge it; `python3 Tools/Build/net_logs.py collect`.
2. For each test newer than the last sweep (`Docs/PartySystem/SWEEP_LOG.md`, newest first): `net_logs.py show <n>`.
   Read the joins, `stall` and `relayDrop` marks, 429 counts, config-hash mismatches and the loads with RELAY RISK.
   Match each to a bug in `Docs/PartySystem/BUGS.md` (B-number) or open a new one (next free number, checked on
   every branch).
3. Fix what can be fixed from the code, on `Ys-bleeding-edge` only, with the usual proof (§2: tests in Amoebius,
   `unity_refcompile`, the gates, the checklist entry). Never push to `bleeding-edge`; never touch the live UGS project.
4. What needs a person (a hand retest, an editor step, a design call): *suggest* a Froglet Tasks ticket on the page
   (one per branch; nobody is named or assigned; an approver approves it). Never file one unapproved.
5. Add a dated row to `Docs/PartySystem/SWEEP_LOG.md`: tests read, what was found, what was fixed (commits),
   what was suggested. A sweep that found nothing new writes one line and changes nothing else.

## 4. Where things stand (2026-10-14, after PRs #1087-#1090)

### Amoebius (our engine): built and tested

| Step | What | State |
|---|---|---|
| 0 | `--realtime` headless pacing, save-path fallback, missing SDK error codes | Done |
| 1 | Network simulator (latency, jitter, loss, bandwidth, pulled cable; per player, live) | Done |
| 2 | Network stats monitor (`do net`, window titles, `net capture`; F3 shows online status since #1089) | Done |
| 3 | Session-service faults (full, 429, relay failure, outage, slow) | Done |
| 4 | Launcher NET page + MCP `net_*` tools (1-4 players at once) | **Removed 2026-10-14** (#1089, your call: online is UGS only, no LAN or local play). START ONLINE and + PLAYER 2 replace it |
| 5 | Froglet's own UDP transport + unreliable channel; **the default** | Done. Party harness 14/14 clean, 14/14 on simulated 4G after the B29 fix |
| 6 | Relay: Unity Relay's wire protocol under our UDP transport + Froglet's own relay server | Done. Party harness 14/14 through the relay, and 14/14 through it on simulated 4G (`Port/docs/MULTIPLAYER.md` §6.7) |
| 7 | UGS sign-in for the relay, the one-step UGS relay check (now SETTINGS > ONLINE > UGS CHECK) | Built and tested against local stand-ins. **The live check waits for you (§5.3)** |
| 8 (P8) | **UGS Lobby: START ONLINE plays through UGS Lobby + UGS Relay** (#1089, another session; `Port/docs/MULTIPLAYER.md` §6.10) | Built 2026-10-14 against stand-ins (`UgsMultiplayerTests` 6/6). **The live check waits for you (§5.3)** |
| — | Amoebius online server (#1080) and the shared-folder sessions | **Removed 2026-10-14** (#1089) |

**Networking is built in one place, `NetStack.Install`** (`Port/docs/MULTIPLAYER.md` §6.9): transport,
UGS Relay, UGS Lobby, then the simulator and faults. A merge on 2026-10-13 had silently switched the UDP
transport, relay, simulator and faults off; `NetStackTests` and `NetworkWiringGuardTests` now fail if that
happens again. **When you merge into this branch, run the engine tests.**

**The five-player party harness does not run at the moment:** it needs the shared session folder that
#1089 removed, and its move to the UGS stand-ins is not done (`MULTIPLAYER.md` §6.10 "Not done yet"). Its
last result, on 2026-10-14 just before #1089, was 8/14, stopped by the blocker below.

### Unity game: the hardening programme

| Block | What | State |
|---|---|---|
| 1 | Observability (session record, NetDiag, `net` console) | Landed; needs MPPM confirmation |
| 2 | Verify what landed | Owner's MPPM runs (§5.2) |
| 3 | Test level: the five-process party scenarios | Landed (14/14 on Amoebius) |
| 4 | Offline mode, seven cases | Landed (L1 tests); needs a player build pass |
| 5 | **Push instead of poll — a launch blocker** (week one is 10-40 concurrent players) | Not started |
| 6 | Reconnection grace | Not started |

Open bugs that matter: **B29 Count case** (a party that just shrank reads as full; Block 5 cures
it). The B29 Session case ("defect 5") is fixed but unverified in Unity.

**Blocker for match testing in Amoebius (found 2026-10-14, still there after #1087-#1090; not a network
bug):** once a Bloomrush match starts, the scene fills with objects and the game crawls. Measured on the
latest code with ONE OFFLINE player: 1.3k to 269k behaviours in 107 s, about 4 frames a second, and since a
frame advances game time 1/60 s the GO button appeared only after ~107 s. Networked matches did the same on
TCP and UDP, with and without relay and simulator. The objects are mostly flora `Branch` objects beside the
SnowChanger's ~4k `shard` motes. It arrived with bleeding-edge content between 2026-10-09 (the harness's
T4 passed in 38 s) and 2026-10-14. Needs an ecology/performance session (`/ecology`); until then judge
Amoebius's lobby and party steps, not in-match ones.

## 5. What the owner tests by hand

Run these when you sit down with the machines. Each has the result to expect; a different result
is a bug, and the run's console log is what a session needs to fix it.

**The logs collect themselves (2026-10-15).** Every player of a multiplayer test writes its own log when it
stops: each MPPM virtual player and the main editor in Unity, each player of START ONLINE / + PLAYER 2 in
Amoebius. When the test ends, the main editor sends every player's logs to Claude by itself (no question since 2026-10-16). Do not paste consoles. Then tell
the session "read my multiplayer logs" and it runs `python3 Tools/Build/net_logs.py show 1`, which lines
every player up into one timeline (`Docs/DIAGNOSTICS.md` § "Multiplayer test logs"). In Amoebius, close player 2's
window (or press CLOSE P2) rather than killing it, or its log is lost.

### 5.1 Amoebius tools (no Unity)

The NET page and its local multi-player runs were removed on 2026-10-14 (#1089): Amoebius multiplayer is
UGS only now, so its hand tests are the live ones in §5.3. The network simulator and session faults are
still in every player (`COSMIC_SHORE_NET_SIM`, `COSMIC_SHORE_NET_FAULT`; `do netsim` / `do netfault` on the
control port).

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
`ProjectSettings.asset`): the first run creates test players in it, and later runs sign the same ones
back in (one per profile). Nothing before this step has called UGS.

**Step 0: the environment.** Amoebius plays in its own UGS environment, `amoebius` by default, so its
lobbies never show up in the Unity build's queries (the two netcodes cannot play each other). Create it
once: UGS dashboard > Project settings > Environments > Add > `amoebius` (or name another one in Amoebius
SETTINGS > ONLINE > UGS environment). Until it exists, sign-in fails and START ONLINE plays offline.

**Step 1: the check.** Amoebius > SETTINGS > ONLINE > **UGS CHECK** (or, from a repo checkout with the .NET
10 SDK: `dotnet run --project Port/src/CosmicShore.Player -- --ugs-relay-check`). Expect, in order:

```
[relay-check] UGS project 3030fd69-... (...environment...), region chosen by the service
[relay-check] host signed in (anonymous): player ...
[relay-check] joiner signed in (anonymous): player ...
[relay-check] allocated: region <a UGS region>, relay <ip:port>, allocation <id>
[relay-check] join code XXXXXX
[relay-check] joined: allocation <id>, relay <ip:port>
[relay-check] connected through the relay in N ms
[relay-check] 10 round trips: avg N ms ...
[relay-check] PASS
```

The average is about twice your ping to that relay region (both players are on this PC). Click again:
both say `(session-token)` with the same player ids. A FAIL names its step (`Port/docs/MULTIPLAYER.md`
§6.8); paste the log to the next session.

**Step 2: a party through UGS Lobby + Relay on one PC.** PLAY > **START ONLINE**, then **+ PLAYER 2** (a
second online game as the `player2` profile; its console lines start `[P2]`). In the first game press F7 and
run `party invite <player2's name>`, accept in player2. Expect player2 seated; F3 in either shows the UGS
sign-in, the lobby, the relay and the ping. Launching a match works, but it crawls (the blocker in §4).

**Step 3: two networks.** Two PCs, or a PC and a laptop on a phone hotspot: both START ONLINE and party up
as in step 2. This is the goal: two networks, UGS Lobby + Relay, no ports opened.

## 6. What comes next, in order

Each step has the check that proves it done. Update the state in §4 when a step lands.

**Amoebius track (internet play in our engine, on UGS):**

| # | Step | Done when |
|---|---|---|
| ~~P6~~ | ~~**Relay.**~~ Done 2026-10-09 (§4). Unity Relay's protocol under our UDP transport, Froglet's relay server (same protocol, same REST shape), session integration, switches in every tool | Contract checks pass over `relay`/`sim-relay`; party harness 14/14 with every pilot through the relay |
| P7 | **UGS Auth for the relay.** Built 2026-10-09: anonymous sign-in and session-token resume over REST, the `idToken` as UGS Relay's bearer, `COSMIC_SHORE_RELAY=ugs`, the one-step check, our relay's secret (§4) | ~~Unit tests against a local stand-in~~ (13/13, plus the CLI and the harness via the UGS path); **the owner's live check (§5.3) still to run** |
| P8 | **UGS Lobby.** Built 2026-10-14 (#1089, `MULTIPLAYER.md` §6.10). Left: the party harness on the UGS stand-ins (it still needs the removed shared folder), join by lobby code, quick join, lobby events | The party harness passes on the stand-ins; **your §5.3 steps 2-3 on real UGS** |
| P9 | **Less traffic.** `NetworkVariable` writes dominate (7:1 over transforms, `MULTIPLAYER.md` §6.4): batch per tick, send deltas | `net` shows the drop on the same scenario, with the harness still 14/14 |
| P10 | Congestion control past the fixed window, measured on a real internet path | A 4-player match on two networks holds RTT and shows no resend storms in `net` |

**Unity track (launch-critical):**

| # | Step | Done when |
|---|---|---|
| U1 | The owner's MPPM runs in §5.2 | Each 🔴 checklist entry becomes 🟢 with a date |
| U2 | **Block 5: push instead of poll** (hardening prompt) | GETs ≤ 4/min/client at rest; invite visible < 1 s p95; no stale-index lines in a 30-minute 4-client run |
| U3 | Block 6: reconnection grace | A 15 s network pull mid-match hands the vessel back with no menu |
| U4 | **What the first real logs showed** (Caleb ↔ ys1, two machines, 2026-10-16; `Docs/PartySystem/BUGS.md` B31-B33). **Fixed 2026-10-17, awaiting the hand retest:** the party session is read at most every 6 s, and since 2026-10-24 no two lobby reads go out in the same second (`LobbyReadPacer`; the first fix still drew 1,061 429s) (B31); leaving the shared presence lobby no longer deletes it under everyone, and a rejoin leaves `Reconnecting` (B32); a new editor Play no longer inherits the last Play's session (B33) | B31: no `rate-limited` lines in a 10-minute party (`net_logs.py` shows `429s 0`); B32: a guest's mid-match leave keeps the host `InParty`; B33: a second Play's log starts clean |
| U5 | **Joins refused with `NetworkConfig mismatch`** (Caleb ↔ Gradi, 21-23 Oct; `Docs/PartySystem/BUGS.md` B35). **Fixed 2026-10-24, awaiting the hand retest:** 11 network prefabs carried a stale `GlobalObjectIdHash`; they now carry Unity's value and `check_network_prefab_hashes.py` fails on a stale one. Each test log records `netConfig` | Two machines: a direct join and an invite accept both land in the party; `net_logs.py show` prints no `NetworkConfig differs` |
| U6 | **Relay drops the host** ("player timed out due to inactivity"; owner, 20 Oct; `Docs/PartySystem/BUGS.md` B36). **Root-caused 2026-10-25, not fixed:** Relay cuts a player silent for ~10 s, and a frozen frame sends nothing; the drops followed Bloomrush teardowns in a mode running at 3-5 fps on that machine. The test log now records each freeze (`stall`) and the freeze each drop followed (`relayDrop`) | Play Bloomrush solo to the end and back to the menu; `net_logs.py show` gives the longest freeze. A 10 s one before a drop sends the fix to the freezing frame (profile). Since 2026-10-25 every online load, the return to the menu included, sends its own Load Time Insights report (§3.1) |

## 7. How a session starts here

1. Read this file, then the document for your track (§3).
2. `git fetch origin Ys-bleeding-edge` and merge it before working.
3. Amoebius work: read `Port/CLAUDE.md` and `Port/docs/MULTIPLAYER.md`; build with
   `dotnet build Port/src/CosmicShore.Player` (the .NET 10 SDK); test with
   `dotnet test Port/tests/CosmicShore.Tests`.
4. Party logic: run `bash Tools/Build/prisma_party_scenarios/run.sh` before and after (~15 min,
   14/14 expected - it does not run until it moves to the UGS stand-ins, §4). Never point a session's
   tests at the live UGS project: that is the owner's §5.3.
5. Unity C#: follow §2's `/verify-unity` rule and add the checklist entry.
5a. A test anyone ran: read its logs first (`python3 Tools/Build/net_logs.py collect`, then `show <n>`; §3.1).
    Ask for a pasted console only if no log reached a branch.
6. Finish by updating §4 and §6 here.
