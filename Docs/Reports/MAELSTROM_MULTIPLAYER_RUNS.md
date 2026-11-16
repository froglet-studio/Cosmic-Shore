# Maelstrom multiplayer: three session-driven runs (2026-11-16)

Three two-player Maelstrom tournaments on `Ys-bleeding-edge` `f80a064aad`, driven by a session with no person at
the keyboard. Asked for by Yash ("multiplayer Maelstrom testing 3 times, log all reports").

**Result: 1 of 3 finished.** Run 1 lost the host to a 72 s freeze, run 2 never finished its first round, run 3
played three rounds to the summary with the same standings on both machines.

## Correction (2026-11-17): the driver itself froze both editors every few seconds

Yash watched a later run and saw the game freeze about 2 s every 3-5 s in every map. Measured in a Skim Race round
with both players in the party: with nothing polling, 35 s had **no** frame over 0.25 s on the host and one of 0.37 s
on the guest; with the driver's state probe running as in every run below, the same 35 s had **four freezes of
1.9-3.0 s on each editor**, one per probe. Each probe is a Pipeline `eval`, which compiles its C# on the editor's main
thread. So in all the runs in this report:

- every match was played at a few seconds of freeze in every ten, on both machines;
- every frame time and freeze length below may include one probe (2-3 s). The two 10.0 s load frames profiled on
  2026-11-17 each carry 2.4-2.7 s of `EditorLoop`, which is most likely the probe;
- run 2's Scurry round that never ended, and the weak AI scores, were measured under that stutter;
- a probe cannot by itself make a 72 s frame, but run 1's freeze was not measured free of it either.

What still stands: the same-display-name finding and its fix (a logic fault, checked in code and live), the guest's
missing intensity ceiling, the console counts, and that a load frame near 10 s drops the host. What needs a re-run
with a driver that does not `eval` during play (or with a person playing): every freeze length, finding 1's 72 s,
and finding 3.

**Profile, 2026-11-17** (host, a Maelstrom forced to draw Cleave first): Cleave loaded and played to the end with no
long freeze, so the 72 s frame is 2 of 3 Cleave draws and was not captured. Two 10.0 s frames were: Cleave to the
hub, and the hub to Wildlife Liberation. Both are the Single-load frame B45 already describes: activating the scene
3.4-3.9 s (a 1.2-1.4 s `GC.Collect`, about 14,000 prisms destroyed), the `sceneLoaded` handlers 2.2-2.8 s (one lump,
not split per handler), `EditorLoop` 2.4-2.7 s, first-frame setup 0.9-1.3 s.

## Re-run (2026-11-17): a driver that lives inside the editors, 0 of 3 finished

The driver is now one hook installed in each editor in the menu (`EditorApplication.update`); nothing is compiled
during play, and it logs every frame over 0.5 s. With it, a round has hitches only at scene loads (0.5-2.7 s).
Runs 1 and 2 were still `ys1` / `ys1`; from run 3 the accounts are named Lion and Tiger (Yash: never the same name on
both instances).

| Run | Round 1 | Outcome |
|---|---|---|
| 1 | Joust I1 | **Stalled**: still running 20 min after the turn started. No joust was scored (all four rows show the same number, which is the clock). Frames over 0.5 s: 9 in the first 9 minutes, then rising to one every second from minute 17 (0.6-1.4 s each). Not looked into. |
| 2 | Cleave I2 | **Host froze, guest bounced** 45 s after launch. The hub's preview of the arena froze the host 9.2 s; the round's lay then froze it for about 91 s (the agent's last line 19:41:33 UTC, the pool's "clone batch (2048 prisms) not integrated after 4s" warning at 19:43:04). **No probe was running, so the Cleave freeze is real**: 3 of 4 Cleave draws in two days. |
| 3 | Scurry I2 | **Not a valid test.** The host froze 13.2 s and then 65.0 s mid-match and the guest bounced. At 01:13 local another session had merged `origin/bleeding-edge` into this checkout (1,312 files under `Assets` changed), and the editor imported them during the match: the console shows model-import warnings (`JewelWasp`) at the start of the first freeze. |

What this adds:
- The Cleave lay freeze stands without the driver. In all three captures the pool's 4 s watchdog warning is logged
  AFTER the freeze, so the frozen frame is one in which an `InstantiateAsync` batch is pending; what the engine does
  in it is still not known (the Profiler run of the same day drew Cleave and did not freeze).
- Joust, like Scurry, has no clock: with nobody jousting the round does not end, and a Maelstrom stalls there.
- A test in the shared checkout is only valid if nothing else writes to it. A pull or merge by another session
  during Play makes the editor import mid-match and freezes it for tens of seconds; the long "hang at Stop" after
  run 2 was the same import. Yesterday's runs were not checked for this.

## Third set (2026-11-17, `036c5f3054`): 2 of 3 finished, clean

Same in-editor driver, accounts Lion and Tiger, intensity ceiling 2. The checkout's commit and the editor's import
count were recorded around each run: no change, no import, in any of the three.

| Run | Rounds | Outcome | Longest frame, host / guest |
|---|---|---|---|
| 1 | Skim Race I1 x4 | **PASS**: summary in 432 s, Ruby 7 (1/1/2/1), Gold 5, Jade 0; last round's stats identical on both machines | 2.2 s / 2.8 s (all at scene loads) |
| 2 | Skim Race I1, I2, I2, I2 | **PASS**: summary in 523 s, Gold 7 (1/1/2/1), Ruby 5, Jade 0; stats identical on both | 2.3 s / 2.9 s |
| 3 | none | **FAIL at boot**: after 5 min the host was still `InPresenceLobby`, never a solo host `InParty`, so no invite was sent | - |

- **Every round was Skim Race, by the new design.** `ce38e023aa` (Garrett's four tiers, merged that night) makes
  `GamesForIntensity` deal only tier N, and tier 2 holds one game. A Maelstrom at intensity 2 is now Skim Race
  back to back until someone reaches 6; the bag's "never the same mode twice running" cannot hold with a pool of one.
  Cleave is in tier 4, so these runs could not draw it.
- No freeze, no Relay drop, 0 429s, no gameplay error in either passing run. With no probe and no import, the worst
  frame of a Maelstrom of four rounds is under 3 s.
- Jade (Lion's hull on AI, Tiger's idle) came last in all eight rounds: half the team is not flying.
- **Run 3's boot (new, party layer):** the third Play within 20 minutes logged `[AuthScene] Relay session not ready
  (attempt 1/3)`, `[PartyStateMachine] Illegal transition: Disconnected -> HostingParty` and `-> InParty`, then
  `[PresenceLobbyService] Converge join ... failed (SessionException): player is already a member of the lobby`.
  The party panel then read `state=InPresenceLobby` with a session and one connection for the rest of the 5 minutes.
- **Cause and fix (same day).** The presence-lobby join was slow, so the state machine was still in its initial
  `Disconnected` when the boot's Relay wait timed out. The boot's "retrying HCS init" does not run the init: it calls
  `HostConnectionService.EnsurePartySessionAsync` directly, which had no check for a `Disconnected` machine and
  ignored both refused transitions, so it built the Relay host anyway. When the join finished, the init moved
  `Disconnected -> InPresenceLobby` and its own session call returned at the "already hosting" fast path. Nothing
  moves `InPresenceLobby -> InParty` after that, so the host could not show `Inviting` either.
  Fixed in `EnsurePartySessionAsync`: on a `Disconnected` machine it starts no host (it returns if the init is in
  flight, else runs the init), and a refused `HostingParty` transition now stops the call. `LegalTransitions` is
  unchanged. Held by `OfflineSessionTests.EnsurePartySession_WhileThePresenceJoinIsStillRunning_StartsNoHost`.
  Checked live: a boot that again logged "Relay session not ready (attempt 1/3)" refused no transition and was
  `role=host state=InParty` 38 s after Play. No B number assigned (B48 is taken twice; Yash's call).
- Not fixed: why the presence join is slow on a repeated Play (`Still listed in <lobby> from an earlier join or Play
  - leaving it and joining again`, then the converge join failing as "already a member"). The boot now waits for it
  instead of racing it; a join slower than the boot's three 15 s waits ends in the offline host.

## How the runs were made

- Host: the main Unity editor (6000.3.17f1). Guest: MPPM Player 2 (its own UGS account, tag `Player2`). Live UGS
  Relay. The game added two AI seats (the Maelstrom field is fixed at four).
- Driven through the editor's Pipeline `/api/exec` `eval` on both editors: `party invite` / `party accept`,
  `GameDataSO.SyncFromArcadeGame(ArcadeGameMaelstrom)` + `InvokeGameLaunch` at intensity ceiling 2,
  `MaelstromLobby.SetLocalReady`, `MiniGameControllerBase.OnReadyClicked`, `Scoreboard.OnContinueButtonPressed`.
- **Nobody flew.** Each round the host's own hull was handed to the AI (`PilotControl.TrySetAIFlies`). The guest's
  hull sat idle: only the host can hand a hull over, and a remote hull is refused.
- Both human accounts have the same display name, `ys1`. That turned out to matter (finding 2).
- Records: `Logs/NetSessions/net_20261116_*.json` on Yash's machine (ignored by git, not on a `bugledger` branch:
  the after-Play send did not push them). Read with `net_logs.py collect --dir Logs/NetSessions`. Each round load
  also has a Load Insights report under `LocalLow/Froglet Games/Cosmic Shore/Benchmarks/LoadInsights/`.

## The three runs

| Run | `net_logs.py` id | Rounds drawn | Outcome | Host | Guest |
|---|---|---|---|---|---|
| 1 | `yskha-20261116_145629` | Cleave (never started) | **FAIL**: host froze 72.4 s laying the Cleave arena, Relay dropped it, both bounced to the menu | DIRTY, 11 errors, 2 429s, longest freeze 72.4 s | DIRTY, bounced "Host disconnected" 10.5 s into the freeze |
| 2 | `yskha-20261116_151045` | Scurry I1 | **FAIL (not proven a defect)**: the round had not ended 12 min after the turn started; the driver stopped it | DIRTY (1 429), longest freeze 3.3 s | DIRTY (5 429s at boot) |
| 3 | `yskha-20261116_152816` | Wildlife Liberation I1, Skim Race I2, Switchback I2 | **PASS**: summary after 3 rounds in 734 s; Gold 6 (1/1/1), Ruby 2, Jade 1 on both machines | CLEAN, 0 429s, longest freeze 3.0 s | CLEAN, 0 429s |

A fourth Play between runs 2 and 3 (`yskha-20261116_152604`) was a driver fault, not a test: a third player (Kyoki)
was online, the driver parsed two names as one and invited nobody. The driver now invites Player 2 by its player id.

All three runs: one `netConfig` hash on both players (`c07d2c5827482b83`, 34 prefabs); the party formed in 10-16 s
every time; no `refused`, no unexpected `bounce` outside run 1.

## Findings

Each was read from the logs by one reader per run, then checked against the code by a separate verifier.

### 1. Host freeze of 72.4 s loading Cleave, Relay drop, party lost (run 1) - B45 again, with new evidence

- 15:00:10 to 15:01:23 UTC the host's main thread sat in one frame inside "Streamed prism lay
  (SpawnablePanes::BLOCK, 15380 prisms)". One `InstantiateAsync` clone batch took 72.5 s from call to integrated; the
  other seven batches took about 0.3 s each. The guest laid the same arena in 11.8 s with no batch over 0.31 s.
- The hub's preview of the same arena 25 s earlier already froze the host for 8.8-10.6 s (one batch of 8.9 s), just
  under Relay's limit.
- `stallContext`: focused yes, 1.8 GB of 32 GB, async prism clones waiting 1. So not focus, not memory.
- **This does not fit B45's 2026-11-15 reading** ("a Single scene load that grows with the prisms alive"): the
  Single load finished in 3.4 s and the hub's 15,495 preview prisms were destroyed inside it. The freeze began 1.9 s
  later, 0.44 s into the lay, so the pool's 4 s `WaitForCompletion` watchdog cannot have started it either (its
  warning is logged after the freeze).
- Nothing is reused between the hub preview and the round: "Prisms reused from the environment pool 0" both times,
  so the host cloned 30,760 prisms in about 90 s.
- **Not known:** what the engine did inside that batch. No profiler capture exists. It happened in the editor; a
  build was not tested. Needs a Profiler capture of the frame with Maelstrom forced to Cleave (the mp-drive
  `prof_on.cs` recipe in `Docs/PartySystem/BUGS.md` B45).
- Also in run 1: the host handled the transport failure twice (two `relayDrop`, two shutdown errors 0.2 s apart);
  only one bounce ran. After Play was stopped the host editor hung for about 13 minutes (one core at 100 %, 7.5 GB)
  and recovered by itself. Runs 2 and 3 did not hang at Stop. No log covers it.

### 2. Two players with the same display name share one roster row (all runs) - new, confirmed in code

- `GameDataSO.AddPlayer` (`GameDataSO.cs` about 1123-1145) keys `Players` and `RoundStatsList` by `Name`. The
  second `ys1` is never added to `Players`, and each `ys1`'s RoundStats evicts the other's as a "stale shadow"
  (the Scoring B12 fix cannot tell a dead component from a live namesake). Log line: "[GameDataSO] Replacing stale
  RoundStats entry for 'ys1' with the live component."
- Seen: three stats rows for four pilots on both machines, and the machines disagreed on the `ys1` row (Skim Race:
  host 165 points and 7 crystals, guest 140 and 0).
- For real players who pick the same name: one scoreboard row for two people, both machines mark it "you", and
  everything credited by name (crystals, score, domain sums, end conditions) lands on one row.
- **Fixed 2026-11-16** (`RosterNames`, `Player.ResolveRosterName`). The whole game keys a pilot by `Name` (251 sites
  in 119 files: the roster, score sync, end conditions, HUDs), so the fix makes the name unique instead of re-keying
  everything: among the spawned players with the same display name, the lowest `NetworkObjectId` keeps the name and
  each later one is named from the authored template `GameDataSO.duplicatePlayerNameTemplate` (`{name} ({number})`,
  so the second `ys1` reads `ys1 (2)`). Every machine reaches the same answer from the replicated names and ids;
  nothing new crosses the wire, so the protocol version is unchanged. Held by `RosterNamesTests` (8).
- Checked live the same day (a fourth driven run, same two `ys1` accounts): four stats rows on both machines,
  `ys1` and `ys1 (2)`, with identical scores and crystals at the end of both rounds played (Skim Race I1, Joust I1),
  and no "Replacing stale RoundStats" warning. That run's third round drew Cleave again and ended the same way as
  run 1: the host froze, "Transport failure", the guest bounced (finding 1, now 2 of 2 Cleave draws).
- Known limit: the numbered name is also what a match report or a spectate-by-name lookup sees for that pilot.

### 3. Scurry round that did not end in 12 minutes (run 2) - not proven a defect

- Scurry has **no clock**: its scene lists one turn monitor (crystals) and `ScurryController.HasEndGame` is false.
  The round ends only when one domain's summed crystals reach the target.
- The target was 14, not 39. `EndConditionOverrides.asset` gives Scurry `crystalCaptureCrystalCount: 14`; the
  "No crystal count configured ... Defaulting to 39" warning is logged before the override applies, on every Scurry
  turn. It is a false alarm and should be removed or reworded.
- In this run Ruby and Gold each had one I1 AI seat (skill 0.25) and Jade had the host's AI-flown hull plus an idle
  hull. Scores were not sampled in run 2, so whether the AIs were collecting at all is not known. For scale, the AI
  seats took 0-2 crystals in 3 minutes of Wildlife Liberation in run 3.
- So this is most likely the test (nobody flying) meeting a mode with no time limit. What a real party can still
  hit: if every human idles or parks, a Scurry round in a Maelstrom never ends and the tournament stalls.
- To settle it: one Scurry I1 run with a person flying, or with per-domain crystals sampled every 30 s.

### 4. The guest never learns the Maelstrom intensity ceiling (runs 2 and 3) - new, cosmetic

- `MaelstromDataSO.IntensityCeiling` is captured on each peer from its own `SelectedIntensity`
  (`MaelstromController.cs:281-284`) and is never replicated. Host 2, guest 1 for the whole tournament.
- The rounds are the host's draw, so play is identical. The guest's hub pool line shows its own menu intensity.
- Suggested fix: carry the ceiling on `MaelstromRoundTicket`.
- Checked and by design: the guest's READY press counted (the label follows one network round trip later; hub to
  round load took 15-22 s each time, inside the 30 s countdown). The guest's stale `GameMode` in the hub and on the
  summary has no on-screen effect in the Maelstrom scene.

### 5. Console volume in a passing run (run 3)

- Guest 864 errors: every captured one (719 of 864; the first 71 s were dropped from the ring) is FMOD "Event not
  found" from the MPPM clone having no sound bank. No gameplay exception, no RPC or ownership error.
- About 1,500 warnings per side: 1,227 (host) / 1,089 (guest) are `NetworkSceneObjectGuard` "Stripped the
  NetworkObject" for locally spawned fauna (QuadFish, MassBrittlestarFauna), once per instance where the ledger
  expects once per prefab (B16's guard working, too loudly). Then "No projectile factory found to release
  projectile!" 179 / 76 at the end of Wildlife Liberation, Animator "Parameter 'Boost' does not exist" 439 times in
  10 s in the hub (host), and the Skim loop's missing 'Boost Amount' parameter 165 times.
- Small, every run: `[Deferred OnSpawn]` timeouts for an object the receiver no longer has at each hub load;
  `FindUnprocessedPlayerByOwnerClientId(n) returned NULL` on each host start (the pilots paired anyway); the camera
  loses its follow target at each hub-to-round hand-over; the hub's MAIN MENU button sits under an inactive root, so
  a player cannot leave a tournament between rounds; 429s at boot in runs 1 and 2 (two peers on one IP), none in run 3.

## What these runs do not show

- Feel, look and audio: nobody watched a screen. The guest's hull never moved.
- A build, a second machine or a second network: one PC, the editor plus an MPPM clone.
- More than three rounds, intensity 3-4, Play Again, a leaver or a rejoin inside a tournament.
- No B numbers were opened. Findings 2 and 4 are candidates; finding 1 belongs on B45's entry.
