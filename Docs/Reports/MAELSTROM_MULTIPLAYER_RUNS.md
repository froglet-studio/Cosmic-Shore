# Maelstrom multiplayer: three session-driven runs (2026-11-16)

Three two-player Maelstrom tournaments on `Ys-bleeding-edge` `f80a064aad`, driven by a session with no person at
the keyboard. Asked for by Yash ("multiplayer Maelstrom testing 3 times, log all reports").

**Result: 1 of 3 finished.** Run 1 lost the host to a 72 s freeze, run 2 never finished its first round, run 3
played three rounds to the summary with the same standings on both machines.

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
