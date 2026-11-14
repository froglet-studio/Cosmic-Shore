---
name: multiplayer
description: The one skill for ANY Cosmic Shore multiplayer work - the party (invite, join, accept, leave, kick, spectate), the presence lobby, UGS (Auth, Lobby, Relay, Sessions, Friends), Netcode for GameObjects (NGO), a host or a guest, a rejoin or a host drop, MPPM virtual players, two machines, the QA-NET hand tests, the B ledger (B20...B48), the multiplayer test logs ("read my multiplayer logs", net_logs.py), a desync, "the host dropped", a Relay drop, a 429, a NetworkConfig mismatch, and Amoebius's START ONLINE / + PLAYER 2 / UGS CHECK. Loads what the owner wants and the standing rules, the reading order, where everything is (code roots, UgsRequestPolicy, the pinned packages, the protocol-version rule), the B ledger and how "fixed again" is stopped, the owner's hand tests with what a machine can run (Tools/Build/net_scenarios.py), how to read a test's timeline, the seven anti-patterns, the traps, and the hand-off. Trigger on "multiplayer", "party", "lobby", "relay", "UGS", "netcode", "NGO", "host", "guest", "rejoin", "invite", "MPPM", "two machines", "QA-NET", "B20"-"B48", "net logs", "read my multiplayer logs", "desync", "the host dropped", or any file under Assets/_Scripts/Controller/Multiplayer/**, Assets/_Scripts/Controller/Party/**, Port/src/CosmicShore.Engine/Networking/**, Docs/Multiplayer*/**, Docs/PartySystem/**, Docs/PresenceSystem/**.
---

# Multiplayer: the owner's game, its rules, its ledger, its tests

Multiplayer's owner is **Yash**. The UGS layer and the backend (Cloud Code, the live project, its environments) are
**Shombith's**: tell him and queue code changes there for his `/approve` (`Docs/TEAM.md` rule 13; Yash does the Cloud
Code work for now); anything POSTED to the live backend still goes through Shombith first. Never assign a task to anyone (`Docs/TEAM.md`). Before the first edit:
`python3 Tools/Build/who_works_where.py query <paths>`. On 2026-11-07 it names `claude/mp-plan-p0` (2026-11-02: B47
round 2, B48/B49, `mp_auto.py`; its `verify_unity.sh` has since landed on `bleeding-edge`) in flight on these docs;
build on it, never beside it.

## 0. What the owner wants, and the standing rules (`Docs/MULTIPLAYER_START_HERE.md` §1-2)

**Wanted:** a very good four-player game on UGS (Auth, Lobby, Relay), the fewest network problems a player can see
and the best performance; as free as possible (free tiers, Steam's free relay on PC, servers of our own only if the
bill ever demands it); our own networking in Amoebius (`Port/`) so multiplayer is built and tested without Unity,
with built-in tools like Unity's; clean, step-by-step testing: the owner tests by hand later, and every session
leaves exactly what to test and the result to expect. Unity keeps shipping on NGO + UGS; Amoebius runs the same code.

**Rules (the owner's decisions; the words are START_HERE's):**
- **A party is 4. Never 6, anywhere** (B25: the session's own seat count is the authority).
- **Push only to `Ys-bleeding-edge`.** No pull request unless the owner asks. Fetch before pushing; merge (never
  rebase or force-push) someone else's commits. *Dated note: that is START_HERE of 2026-10-09; since 2026-10-26 only
  Yash's sessions build on `Ys-bleeding-edge` and everyone else lands on `bleeding-edge` through PRs (`/ship`; B36's
  boot fix was PR #1713, 2026-11-02; B38-again `93765d6aa8` is on `bleeding-edge`). Say which branch you are on.*
- **Do not change Unity versions or packages.** A new dependency in Amoebius (for example Steamworks.NET) is flagged to
  the owner first.
- **Never accept consent or a privacy prompt on the owner's or a player's behalf** (harnesses press "No thanks").
- **`/verify-unity` gates every Unity C# commit.** A cloud session has no editor: say so in the commit and add the
  change to `Docs/UNITY_VERIFICATION_CHECKLIST.md` with an MPPM repro. Never claim a verification that did not run.
  *Dated note: CLAUDE.md (Shombith, 2026-10-29) spells the rule as `bash Tools/Build/unity_refcompile/run.sh` plus
  `bash .claude/skills/ship/gates.sh`; `bash Tools/Build/verify_unity.sh --project <worktree>` is the headless
  real-editor compile and EditMode run; `/verify-unity` itself is not required until that skill merges.*
- **Amoebius (`Port/`) never changes the Unity project** (`Port/CLAUDE.md`). A commit is either Port-only or Unity-side,
  never both.
- **Never copy code from Unity's Netcode, Unity Transport or Multiplayer Services packages.** They are under the Unity
  Companion License. Reading them to learn a wire fact is allowed (`Port/docs/MULTIPLAYER.md` §2).
- **Do not test against the live UGS project without the owner's go-ahead.** A real sign-in creates a player
  account in project `3030fd69-28ab-433f-b4bd-22b9b93c5118`. Use the local stand-ins; the owner runs the live test (§5.3).
- **Skip non-multiplayer issues in multiplayer work** (Grizzly, lizard, Sparrow, RaceRankToast failures are known and
  not ours).
- The two LOCKED party decisions (two-level presence lobby + party session, eager per-user Relay) are given:
  `Docs/prompts/MULTIPLAYER_HARDENING_PROMPT.md` § Standing constraints. *That section spells them out: "Always-InParty"
  (`ROADMAP.md`: never reintroduce lazy, on-first-invite creation) and the `ROADMAP.md` § Genuinely strong invariants:
  single-writer SOAP, `.AsMainThread()` at every UGS / Netcode await, `PartyStateMachine` the only lifecycle authority,
  `NetworkSceneObjectGuard.Sweep` before every join, one party size of 4 by the session's own seat count, every catch
  mapped to a named recovery; `Docs/PartySystem/ARCHITECTURE.md` § Unbreakable holds the exit criteria.*

Not §2's, but standing: **logs are never pasted** (START_HERE §5 and §7; §5 here), and a person is never asked to run a
learner, a report or a test command (root `CLAUDE.md` § "AI learning is automatic").

## 1. Read before you touch (in this order; each answers one question)

| Read | It answers |
|---|---|
| `Docs/MULTIPLAYER_START_HERE.md` | What the owner wants, the rules, where everything is (§3), where things stand (§4), the hand tests (§5), what comes next (§6), how a session starts (§7). A session that finishes a step updates §4 and §6 in the same commit |
| `Docs/Multiplayer/README.md` | The index: one canonical home per topic. Then `TODO.md` (the one live list), `BUGS.md` (every bug's one-line state), `TESTS.md` (the suites, the latest run), `TEST_CHECKLIST.md` (the hand bug hunt and the Ctrl+Shift+M overlay) |
| `Docs/claude/MULTIPLAYER_AND_SOCIAL.md` | Netcode and the spawn chain, the party / presence / friends / AI-backfill / SkimRace reference, the protocol-version rule, the party docs route table (§2) |
| `Docs/PartySystem/BUGS.md`, `TESTS.md`, `ARCHITECTURE.md` | The full B entries (§3); the MPPM prerequisites and the S-gates; the locked design and the unbreakable exit criteria. Presence: `Docs/PresenceSystem/*` |
| `Docs/MultiplayerArchitecture/REVIEW_INVITE_AND_RESILIENCE.md` | Before ANY UGS call: why fixes never stuck (polling at the rate limit, ownerless retries, inverted timeouts), the one request policy. Its timings are superseded; `ROADMAP.md` and `HARDENING_PLAN_STEAM_LAUNCH.md` hold the invariants and the blocks |
| `Docs/NetworkDiagnostics/ARCHITECTURE.md` | Classifying and logging a catch: NetDiag decides what to LOG, `UgsRequestPolicy` decides what to DO |
| `Docs/DIAGNOSTICS.md` § "Multiplayer test logs"; `/bug-ledger` § the `net` report kind | How a test's logs reach a `bugledger/<user>` branch and how `net_logs.py` lines them up (§5) |
| `Docs/Reports/MULTIPLAYER.md` | The report shape (2026-10-24). A report on request is built with `python3 Tools/Build/team_report.py area multiplayer`, never from memory |
| `Docs/claude/ANTI_PATTERNS.md` | The seven multiplayer / threading / backend bullets (§6) and the two trap bullets (§7) |
| `Port/CLAUDE.md`, `Port/docs/MULTIPLAYER.md`, `Port/docs/LAUNCHER.md` § START ONLINE | Amoebius: licensing, the UDP transport, the relay, UGS sign-in and the one-step relay check (§6.8), UGS Lobby (§6.10), `NetStack`, the simulator and the faults (§6.3) |
| As the task needs | `Docs/OFFLINE_MODE.md`, `Docs/CREW_STATIONS.md`, `Docs/THREADING.md`, `Docs/AntiCheat/README.md`, `Tools/UGS/README.md` (Cloud Code deploy: Shombith's) |

## 2. Where everything is

| What | Where |
|---|---|
| Netcode, the spawn chain, rejoin, crew seats, the stray-object guard, the config hash | `Assets/_Scripts/Controller/Multiplayer/` (`MultiplayerSetup`, `ServerPlayerVesselInitializer*`, `ClientPlayerVesselInitializer`, `MatchRejoin`, `CrewStations`, `NetworkSceneObjectGuard`, `ArcadeConfigSyncManager`, `HitReportGuard`, `SpectatorSession`) |
| The party and the presence lobby | `Assets/_Scripts/Controller/Party/` (`HostConnectionService`, `PartyInviteController`, `PartyConsoleCommand`, `StateMachine/PartyStateMachine`; `Services/PartySessionService`, `PresenceLobbyService`, `LobbyRefreshScheduler`, `JoinTargetValidator`) |
| Request discipline and the record | `Assets/_Scripts/Utility/`: **`UgsRequestPolicy`**, the ONE place a UGS failure is classified and the one retry decision (jittered back-off, single-flight, a budget, retry at one layer; `Classify` for a catch, `ExecuteAsync` where the result is spent; every UGS create / join / query / save goes through it; `UgsRequestPolicyTests` 77); `LobbyReadPacer` (no two lobby GETs within 1.05 s, B31); `NetSessionRecorder` (the marks, §5); `FrameStallWatch` (`stall`, `relayDrop`); `NetworkDiagnostics` |
| Offline, reconnect, the monitor | `Assets/_Scripts/System/` (`OfflineModeService`, the single writer of the offline flag; `ReconnectService`; `NetworkMonitor`) |
| The wire format's one version | `Assets/_Prefabs/CORE/NetworkManager.prefab` `NetworkConfig.ProtocolVersion` (24 in the 2026-10-29 history entry; read the prefab). **Rule (2026-10-05): every change to what crosses the wire bumps it** - a `NetworkVariable` struct, the order or count of a behaviour's variables, an RPC signature, a network prefab added or removed - after checking `bleeding-edge` AND every in-flight branch (19 was claimed twice on 2026-10-27); on a merge conflict keep the HIGHER number and both history entries. Netcode refuses a mismatched build at the connection request and the HOST's console names it (`NetworkConfig mismatch`); each test log records `netConfig`; `Tools/Build/check_network_prefab_hashes.py` fails on a stale `GlobalObjectIdHash` (B35). Two players testing a join run the same build |
| The pinned packages (`Packages/manifest.json`; never bumped without the owner) | NGO `com.unity.netcode.gameobjects` 2.13.3, Sessions `com.unity.services.multiplayer` 2.3.3, Transport 2.7.4, MPPM `com.unity.multiplayer.playmode` 2.0.2, Friends 1.3.0, `com.unity.multiplayer.tools` 2.2.12 (`com.unity.multiplayer.widgets` removed) |
| The test logs and the sweep | `Tools/Build/net_logs.py` (§5); `Assets/_Scripts/Editor/Diagnostics/NetTestLogs.cs` and `BugHuntAutoSend.cs` (the writer and the send); `Docs/PartySystem/SWEEP_LOG.md` (one row per sweep) |
| Scenarios a machine runs | `Tools/Build/prisma_party_scenarios/` (five Amoebius processes, 14 rows: T1-T7, T4-lobby, `net`; 14/14 on 2026-10-09; down since #1089, 2026-10-14, until it moves to the UGS stand-ins); **`Tools/Build/net_scenarios.py`** (the owner's rows 2-5 as scripted Amoebius runs with injected faults, judged from the merged timeline; `--self-test` green on 2026-11-07, its `--run` driver unverified; §4); `Tools/Build/mp_auto.py` (in flight on `claude/mp-plan-p0`: built development players host and join from a terminal, `-csMpAutoHost` / `-csMpAutoJoin`, each step an `mpAuto` mark) |
| The edit-mode suites | `Assets/_Scripts/Tests/Editor/` (`Docs/Multiplayer/TESTS.md` lists them by area); headless in Amoebius: `bash Tools/Build/prisma_edit_mode_tests/run.sh` (it stopped building on 2026-11-01, 102 engine APIs; 72 filled 2026-11-05) |
| Amoebius networking, built in one place (`NetStack.Install`) | `Port/src/CosmicShore.Engine/Networking/Wire/` (transport, relay, `Ugs/` sign-in and Lobby, simulator, faults: `COSMIC_SHORE_NET_SIM`, `COSMIC_SHORE_NET_FAULT`, `do netsim` / `do netfault` on the control port); `Port/src/CosmicShore.Player/NetTestLog.cs` |
| The Relay region (Settings > REGION) | `RelayRegionPreference` (the choice; `CreateWithFallbackAsync` wraps every Relay session create and falls back to Auto once), `RelayRegionProbe` (regions + QoS ping), `UI/Modals/RegionSettingsView`. Only the HOST's region counts: Auto puts the relay closest to the host, which is what gave a US-host / India-guest party ~1200 ms |
| The party docs route table | `Docs/claude/MULTIPLAYER_AND_SOCIAL.md` § "Party / Presence / NetDiag docs": the doc to read first for each class you touch, and the rule that every UGS call goes through `UgsRequestPolicy.ExecuteAsync` |

## 3. The B ledger

- **A B number** is one party-side bug: a row in the table at the top of `Docs/PartySystem/BUGS.md` (🔴 open, 🟡 fixed
  and awaiting its retest, 🟢 fixed and verified, 🟠 root-caused and not fixed, ⚪ deferred) and a full entry below it:
  Seen in (the `net_logs.py` test id, the Bug Ledger `E-` signature), Symptom, Root cause, Fix, Held by (the tests),
  Hand test. Presence-lobby bugs (B1, B4, B6) live in `Docs/PresenceSystem/BUGS.md`; `Docs/Multiplayer/BUGS.md` is the
  one-line index of both (`P-`, `Pr-`), by state. Fixing one is closed out everywhere with `/bug-closeout`.
- **Adding one.** The next free number is checked on EVERY branch, never on this checkout, after `git fetch origin`:
  `for r in $(git for-each-ref --format='%(refname:short)' refs/remotes/origin); do git show "$r:Docs/PartySystem/BUGS.md" 2>/dev/null | grep -oE '^(## B|\| B)[0-9]+'; done | grep -oE '[0-9]+$' | sort -n | tail -1`
  (4 s). On 2026-11-07 it answers 49: **B48 is taken twice** - `Ys-bleeding-edge` (2026-11-07, a reaped party session
  rebuilt in place leaves the pilot unpaired) and `claude/mp-plan-p0` (2026-11-02, a re-invite that did nothing; its
  B49 is the host stuck `Inviting`) - so the next free is **B50**, and one B48 needs renumbering (Yash's ledger: ask
  him which). Then the
  table row, the entry, the index line in `Docs/Multiplayer/BUGS.md`, the checklist entry (§8), and the sweep-log row.
- **How "fixed again" happens.** B31: fixed 2026-10-17 by averaging under 1 GET/s; 1,061 429s on 21-23 Oct because the
  two reads still left in the same second; fixed again 10-24 (`LobbyReadPacer`). B38: fixed 10-25 (`PlayEnded`);
  again 11-06 because `Application.isPlaying` stays true after a requested Stop while UniTask's editor loop already
  resumes the flow. B36 "again" at boot four times, each one a measurement, until the boot order changed (PR #1713).
  B16 recurred through three producers that skipped the seam. B20's lobby half "did not work and was re-fixed
  2026-10-08", caught by the harness's T4-lobby. The shape every time: the fix met the AVERAGE or the WRITTEN
  condition, and the real one was narrower.
- **How it stops: the test that holds it.** A fix lands with (1) an edit-mode test that plants the exact defect and
  fails without the fix (`PartyRefreshRecoveryTests` 13 for B31, `PartyRecoveryAfterPlayTests` 4 for B38,
  `FrameStallWatchTests` 5, `NetworkExplodeParamsTests`, `JoinTargetValidatorTests`), (2) a gate when the defect is a
  producer class (`check_network_prefab_hashes.py`, `NetworkSceneObjectGuard` and Audit Stray Network Objects,
  `net_logs.py --self-test`), and (3) the mark or count in `net_logs.py show` that proves it in the next real test
  (`429s 0`; no `refused` with `SessionChanged`; no `Double initialization`). An entry is never closed on a stale
  repro: an audit of the named causes is evidence about where you looked, not where the bug is ("How we work bugs").
  The daily sweep (START_HERE §3.1) reads every new log against the list, so a recurrence is named, not rediscovered.

## 4. The owner's hand tests, and what a machine can run

The owner's table (START_HERE §5.2 in order, §5.3 the live check) is copied to **`references/HAND_TESTS.md`** with the
column *a machine can run it: how* and, per row, the timeline marks a machine checks. Rows that need the editor's own
UI (the invite popup, the scoreboard's Main Menu, MPPM tags) stay MPPM. **Rows 2-5 are the Amoebius scenarios
`Tools/Build/net_scenarios.py` scaffolds** (`--list`, `--plan`, `--judge`, `--marks`, `--self-test` run here; `--run` is unverified) on the five-process harness's rows
(T7 + T4-lobby, T2b, T4 / T4-lobby, T3); the plan calls them two-player, the checklist steps need 3, 5, 4 and 2
pilots (the tool seats 3, 5, 4 and 4: row 5 keeps a full party so the other three play on). **The live UGS check (§5.3: the `amoebius` environment, UGS CHECK, a party on one PC, two networks; later a
scheduled task on the VM) is Shombith's call**: it signs players into the live project; a session never runs it.
`mp_auto.py` (in flight) is the built-player route for rows 1 and 8 once it merges. Prerequisite of every MPPM run:
each virtual player has its own tag (P2, P3, P4), or every clone signs in as ONE UGS account.

| # | Test | Expect | A machine |
|---|---|---|---|
| 1 | Smoke gate S1-S4, S9 | All pass | MPPM; `mp_auto.py` covers the join half of S1 |
| 2 | Host drop, then the new host invites at once (repeat 5x) | The Accept seats the guest; never "party is no longer available" (B29 Session case) | `net_scenarios.py` row 2 (3 pilots) |
| 3 | Two players press Join on a 3/4 party together | 4/4; the loser sees "That party is full." (B25) | `net_scenarios.py` row 3 = T2b (5 pilots) |
| 4 | A member leaves at the ready gate / the arcade launch lobby | The other three still launch (B20) | `net_scenarios.py` row 4 = T4, T4-lobby (4 pilots) |
| 5 | A guest leaves mid-match | Its ship flies on under AI; its score row stays (B21) | `net_scenarios.py` row 5 = T3 (4 pilots: the leaver and three who play on; the checklist's minimum is 2; in-match rows wait on START_HERE §4's blocker) |
| 6 | Offline: the seven cases | As listed in the checklist entry | A player build with the NIC pulled; `OfflineSessionTests` (15; Case4 failing on 2026-11-02) and Amoebius `netfault down` hold the decisions |
| 7 | `net` / `net dump` in the F7 console on host and guest | Each names its role; the host's record holds the lifecycle marks | The harness's `net` row; any sent log through `net_logs.py show` |
| 8 | Request discipline: idle 5 min in the menu | No 429s; GETs within budget (about 0.1 reads/s since Block 5) | Read from any log (`reads`, `429s 0`); the run needs real UGS |
| 5.3 | The live UGS check | `[relay-check] PASS`; player2 seated; two networks party up | Shombith's call; never from a session |

## 5. How a session reads a test's logs

Nobody pastes a console. Every player of a test (the main editor and each MPPM virtual player; Amoebius `main` and
`p2`) writes `net_<utc>_<peer>.json` when it stops (`cosmicshore.nettestlog.v1`: role, counters, verdict, the
lifecycle marks, the console), and the after-Play send pushes them to `bugledger/<user>` (Unity asks nothing since
2026-10-16; Amoebius asks SEND TO CLAUDE; a killed process writes nothing, so player 2 is closed with CLOSE P2).

```
git fetch origin
python3 Tools/Build/net_logs.py collect              # every test on every origin/* branch, newest first
python3 Tools/Build/net_logs.py show 1               # the newest: each player's line, then ONE merged timeline
python3 Tools/Build/net_logs.py show <id> --lines    # ... with every console line
python3 Tools/Build/net_logs.py --self-test
```

`show` prints, per player, `<user>/<peer> <role> CLEAN|DIRTY errors N warnings N reads N 429s N [(console M)]
[voided N] [party pushes N] [presence pushes N] [longest freeze Ns] events N (why)` with its branch and commit; then
`NetworkConfig differs between players (B35)` when the `netConfig` hashes disagree; `Loads (Load Time Insights)` with
**RELAY RISK** on any frame over 9 s; then the timeline, `+<s> <peer> event|warning|error <text>`. The marks
(`net_scenarios.py --marks` is the written set): `party <from>-><to>` (`Disconnected`, `HostingParty`, `InParty`,
`Inviting`, `Reconnecting`, ...), `clientApproved <id> [rejoin]`, `clientLeft <id>`, `refused <why>`, `bounce <toast>`,
`readyGate <controller: why>`, `leaverToAI <name>`, `seatReclaimed <name>`, `rejoinRefused <id>`, `stall <s>s in
<scene>` + `stallContext` (focus, memory against RAM, clones in flight), `relayDrop after a N s freeze`,
`netConfig hash=...`, `offlineChosen`, `offlineFallback`, `manual`, `mpAuto <step>` (in flight). No runtime call writes
`start`, `signedIn` or `presenceJoined` (only `NetSessionRecorderTests` and net_logs.py's fixtures). Amoebius writes the
same verbs.

**A good run:** every player present and CLEAN; `429s 0` and no `rate-limited` line; one `netConfig` hash and no B35
block; no `stall` of 9 s or more and no `relayDrop`; no `refused` or `bounce` the test did not ask for; no RELAY RISK
load; `events` non-zero on every player; no "carried over". Read in this order: the summary lines, the config block,
the loads, then the first player that saw something wrong in the timeline. Match each finding to a B number or open
one (§3). A test missing a player: that player never stopped cleanly. No logs at all: Send was not pressed, or the
main editor saw no other player (solo online play writes nothing). Say so in one line; never ask for a console.

## 6. The seven anti-patterns (`Docs/claude/ANTI_PATTERNS.md`, one line each)

- `:13` Instantiating a prefab that carries a `NetworkObject` without spawning it: the second un-spawned instance breaks synchronization for every later guest, invisibly on the machine that caused it (B16); `NetworkSceneObjectGuard.NeutralizeStray` at the creator, `Sweep` before anything starts a NetworkManager.
- `:39` `AutoPilotEnabled` is false on every vessel at `Start`, so it is not "is this my ship": per-vessel ownership comes from `VesselController.Initialize` and `ChangePlayer` under `IPlayer.IsLocalPilot`; a shared surface is guarded by identity.
- `:69` `UniTask.SwitchToMainThread()` / `Yield(PlayerLoopTiming.Update)` as a thread fix: unreliable on this UniTask; `.AsMainThread()` (`Docs/THREADING.md`).
- `:70` Raising a SOAP `ScriptableEvent` from a UGS / Netcode `Task` continuation before it is back on the main thread: `Raise()` runs listeners inline and crashes any that touch Unity.
- `:71` Touching a `UnityEngine.Object` (a `== null` included) in a `Task` continuation without `.AsMainThread()` upstream: `EnsureRunningOnMainThread`.
- `:74` Caching a UGS `*.Instance` in a service constructor: DI builds the service before `UnityServices.InitializeAsync` completes, so it pins null forever; resolve at use time (`PartySessionService`).
- `:75` Adding a field to a payload that crosses the wire through a separate DTO (`Crystal.ExplodeParams` -> `NetworkExplodeParams`): compiles, reads, does nothing on every peer, the host included; five places plus the reflection round-trip test.

## 7. Traps (from `Docs/PartySystem/BUGS.md` and `Docs/Reports/MULTIPLAYER.md`)

- **Fake null** (`ANTI_PATTERNS.md:102`): `??` / `?.` on a `UnityEngine.Object` says "set" for an unassigned field or a destroyed `Player` in the editor; write `if (!x)`. B46's missing name was a C# null that `NetName.ToString()` can never produce: a Player never paired on that machine.
- **Scaled waits at timeScale 0** (`:101`): every Menu_Main screen but HOME pauses time, so a `UniTask.Delay` with no `DelayType` in a network, presence or backend flow never completes (nineteen did; one held `_lobbyMutex` and froze the online list). `DelayType.UnscaledDeltaTime`; a deadline in `unscaledTime` around a scaled delay is not a timeout.
- **Latches on replay**: a flag set once and never re-decided or reset. The ready gate counted only inside the press RPC (B20: the ROSTER is the question, re-decide on every departure); `ActiveSession` outlived Play (B33; the pacer, the stall watch and the recorder all reset at Play start); "Already transitioning" dropped a Rejoin and a host loss (B42); the host stayed `Inviting` after its last invite expired (B49, in flight). After a fix, ask what releases it on the second run and the second Play.
- **Server-only effects, and MPPM-only ones**: a SOAP raise is local, so on two machines a client never runs `LaunchGame` / `ReturnToMainMenu`: the scene-follow watchdog's real call site is `ShowReturnToMenuVeil_ClientRpc`. A contact-driven chain needs three branches (offline runs it, the owner broadcasts, a non-owner copy does nothing) or a ghost crystal shatters on every peer. Netcode destroys a client's owned objects on disconnect, so the vessel and the `Player` carry `DontDestroyWithOwner` (B21). MPPM virtual players share one process and one `GameDataSO`: what works there can protect nobody in a build.
- **DTO flags** (`:75`) and an unbumped protocol version (§2): a wire change that compiles and dies later, silently, on the joiner.
- **A new Relay session create bypasses the region** unless it goes through `RelayRegionPreference.CreateWithFallbackAsync` (`WithRelayNetwork()` with no argument is Auto, closest to the host). **The SETTINGS modal never deactivates on close** (`ModalWindowManager.ModalWindowOut` skips `DisableWindow` for it), so a settings tab's `OnEnable`/`OnDisable` are scene load/unload: hook `OnModalOpened`/`OnModalClosed`.
- **Polled presence lies by up to two ticks** (B29): the session is authoritative; a false "full" costs one retry, never a teardown. **Relay drops a player silent for ~10 s** (B36 / B45): a frozen frame sends nothing, so read `stall` before blaming the network. **A recovery that runs on after Play stops** (B38) **or in place on the live menu** (B48 on `Ys-bleeding-edge`): tear down, reload Menu_Main, then the solo session. **A failed join leaves a stale membership** ("already a member", B40) and **rows keep offering a dead host** (B41). **One tag per MPPM virtual player**, or every clone is one UGS account.

## 8. Hand-off

1. **The checklist entry** (`Docs/UNITY_VERIFICATION_CHECKLIST.md`, or the PR body's *Verification status*): a 🔴 header
   with the branch and the date; **What landed**; **Proven without the editor** (the tests, the harness rows, the gates,
   the negative control); **Not run** (name every verify step that did not run); **Needs the editor** as numbered steps,
   each with an Expect and the mark it leaves in the log (§5). `/bug-closeout` when a B number closes.
2. **One team-task line per branch.** A hand test, a two-machine retest or a decision your branch leaves goes through
   `/team-tasks` as a SUGGESTION the person approves (`team_tasks.py step --line` appends a checklist line to this
   branch's one ticket): never filed on your own, never assigned, never a second QA-NET ticket beside #1530
   (T-20261025-edc2, the eleven QA-NET tests). When you read logs: a dated row in `Docs/PartySystem/SWEEP_LOG.md`.
   When a step lands: START_HERE §4 and §6 in the same commit. In the commit: `Affects: <person>'s <area>`.
3. **Who to ask.** Yash for anything multiplayer (the party, netcode, the AI handover, `EnvironmentPrismPool`).
   Shombith for UGS, the backend, Cloud Code (Yash does the work, posted through Shombith), the live project and its
   environments, the scheduled live check, and the tooling; the boot order (Relay host after the menu) landed as PR
   #1713, merged by him. Garrett's machine holds the B45 freezes (a Profiler capture needs that machine; name him,
   never assign it). Name the owner in the hand-off; never assign.
