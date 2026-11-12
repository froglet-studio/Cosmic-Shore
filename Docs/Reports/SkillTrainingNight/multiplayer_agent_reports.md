## None
**Summary**

1. Wrote `/home/user/Cosmic-Shore/.claude/skills/multiplayer/SKILL.md` (199 lines; frontmatter `name: multiplayer` + one-line description with every trigger word; the harness already lists `/multiplayer`): §0 the owner's wants and the START_HERE §2 rules verbatim with two dated notes (verify rule, branch rule), §1 the reading order (which doc answers what), §2 code roots / `UgsRequestPolicy` / the pinned packages / the protocol-version rule, §3 the B ledger with the every-branch next-free-number one-liner (tested, 4 s), §4 the owner's table with the machine column, §5 `net_logs.py show` (summary line, every mark verb, what a good run looks like), §6 the seven anti-patterns at `ANTI_PATTERNS.md:13, :39, :69, :70, :71, :74, :75`, §7 traps (fake null `:102`, scaled waits `:101`, latches on replay, server-only/MPPM-only effects, DTO flags, B29/B36/B38/B40/B41/B48), §8 hand-off (checklist entry, one team-task line per branch via `/team-tasks` suggest, who to ask).
2. Wrote `/home/user/Cosmic-Shore/.claude/skills/multiplayer/references/HAND_TESTS.md` (130 lines): the 8 rows plus the §5.3 live check with "a machine can run it: how", then per row the timeline marks and console lines a machine checks (`party a->b`, `clientApproved [rejoin]`, `clientLeft`, `refused`, `bounce`, `readyGate`, `leaverToAI`, `seatReclaimed`, `rejoinRefused`, `stall`/`relayDrop`, `netConfig`, `429s`/`reads`/pushes, the `[relay-check]` lines).
3. `Tools/Build/net_scenarios.py` is named in both files as the scaffold for rows 2-5 (not created here; the other agent's), mapped onto the five-process harness rows T7+T4-lobby / T2b / T4+T4-lobby / T3; the live UGS check is marked Shombith's call; owner Yash; nobody assigned.
4. `who_works_where` names `claude/mp-plan-p0` (2026-11-02) in flight on these docs: it already adds `Tools/Build/mp_auto.py` (built development players host/join from a terminal, `-csMpAutoHost`/`-csMpAutoJoin`, `mpAuto` marks), `verify_unity.sh`, B47 round 2 and B48/B49. Both files tell `net_scenarios.py` to build on it; it should be read before that scaffold is written.
5. Checks run: frontmatter parses (PyYAML), no non-ASCII beyond the status glyphs, every table row has a consistent cell count; nothing committed, no git write; `Tools/Build/look_kit/` (untracked) is another agent's.
6. Facts worth the morning review: B48 is claimed twice (below), the next free bug number is B50, and the plan's "two-player" rows 2-4 need 3, 5 and 4 pilots.

**Contradictions between the docs (date-stamped)**

- **B48 taken twice.** `origin/Ys-bleeding-edge` (2026-11-07): "a reaped party session rebuilt in place leaves the pilot unpaired, the next match seats them on the wrong domain" vs `origin/claude/mp-plan-p0` (2026-11-02): "a re-invite does nothing" (and its B49: "the host stays `Inviting` after its last invite expires"). This branch's ledger stops at B47. One B48 needs renumbering (Yash); next free is B50.
- **Boot order.** `Docs/Multiplayer/BUGS.md` and `Docs/Multiplayer/TODO.md` (both "updated 2026-11-02") and `Docs/PartySystem/SWEEP_LOG.md` (2026-11-01) say the Relay-host-after-menu decision "is waiting on the owner"; `Docs/PartySystem/BUGS.md` B36 row and `MULTIPLAYER_START_HERE.md` §6 U6 say "Boot drop fixed 2026-11-01", and PR #1713 (`7efef6e98d`, merged 2026-11-02 by Shombith) is in HEAD and `origin/bleeding-edge`. The index and TODO are a day stale.
- **The verify rule.** START_HERE §2 (2026-10-09): "`/verify-unity` gates every Unity C# commit" vs CLAUDE.md (Shombith, 2026-10-29): `unity_refcompile` + the ship gates, `/verify-unity` not required until it merges; `Tools/Build/verify_unity.sh` (`claude/mp-plan-p0`, 2026-11-02) is the runnable headless check.
- **The branch rule.** START_HERE §2 (2026-10-09): "Push only to `Ys-bleeding-edge`. No pull request unless the owner asks" vs CLAUDE.md/TEAM (PRs, `/ship`), the in-flight note `claude/gracious-johnson-s1lkvt` (2026-10-26: only Yash's sessions build on Ys-bleeding-edge, everyone else on bleeding-edge), B39-B44 landing on `claude/intelligent-turing-aw29so` (2026-10-31), PR #1713 (2026-11-02) and B38-again `93765d6aa8` on `bleeding-edge` (2026-11-06).
- **START_HERE's own date.** Header "Last updated: 2026-10-09" vs §4 headed "(2026-10-14, after PRs #1087-#1090)" and U4-U6 rows dated 2026-10-17 to 2026-11-01.
- **Player counts.** `Docs/SKILL_TRAINING_PLAN.md` §3.6 (2026-11-07) calls rows 2-5 "scripted Amoebius two-player runs"; the checklist steps (2026-10-08/09) need 3 pilots (row 2), 5 (row 3, harness T2b), 4 (row 4, T4-lobby), 2 (row 5). Its "`--self-test` that runs the five-process party scenarios in the parity CI" also meets START_HERE §4 (2026-10-14): that harness does not run (needs the shared session folder #1089 removed; the move to UGS stand-ins is not done) and takes 10-15 min; the Amoebius edit-mode suite stopped building on 2026-11-01 (102 APIs; 72 filled 2026-11-05).
- **`PartyAcceptFlowPlayModeTests`.** `Docs/Multiplayer/TESTS.md` (2026-11-02): "not implemented, needs a two-client harness" vs the checklist entry "Party request discipline" (2026-10-07): "`PartyAcceptFlowPlayModeTests` (4) — all green". The 4 are benign-error unit tests; the play-mode flow is the TODO at line 83; `claude/mp-plan-p0` (2026-11-02) adds `TwoStandalonePlayers_HostAndJoin_BothReachTheParty` behind `CS_MP_PLAYER`.
- **B31's remaining 429s.** `Docs/Reports/MULTIPLAYER.md` (2026-10-24) §1: "most likely the UGS SDK's own reads" vs its §5 (same date): "it was our own two reads in one second, not the SDK" - superseded inside the one file.
- **Live UGS vs `mp_auto.py`.** START_HERE §2 (2026-10-09): never the live project without the owner's go-ahead vs `mp_auto.py` (`claude/mp-plan-p0`, 2026-11-02): "signs anonymous players in to the UGS project the build points at; point the build at a development environment, never the live one". A development ENVIRONMENT of project `3030fd69…` still creates players in that project. Question for Shombith: does an environment count as "the live project"?
- **Ticket #1530** (T-20261025-edc2): the plan calls it "open two weeks"; the issue is labelled `live`, `taken-by: Yash` since 2026-10-25 with an empty branch, so under rule 2 it reverts to open on 2026-11-08. Minor.
- **The daily sweep** (START_HERE §3.1): `SWEEP_LOG.md`'s last row is 2026-11-01; the tests of 11-02 (phone, mp-plan-p0), 11-06 (Caleb's Regatta, B38 again) and 11-07 (Yash's Thresher Time, B48) were read by sessions, not the sweep. A gap, not a contradiction.
- **Stale route table.** `Docs/claude/MULTIPLAYER_AND_SOCIAL.md` § Party docs: "Log / triage a bug: `Docs/PartySystem/BUGS.md` (B2/B3/B5/B7)" vs the ledger's B2-B47 (B49 in flight). Staleness only.

## None
Built and verified. Nothing committed (as instructed); the one file I own is `/home/user/Cosmic-Shore/Tools/Build/net_scenarios.py` (1,333 lines, stdlib only, imports `net_logs.py` for every bit of log parsing).

## What it is

- `SCENARIOS` holds rows 2-5 of the owner's table as data: players by role, steps (each with its control-port form — `do console <DiagnosticsHUD line>`, `do arcade ...`, `do vessels` / `do score`, the ReadyButton click — or "kill -9 / stop the virtual player" where no command exists), faults (documented `netsim` / `netfault` tokens, or `fault: to name with Yash`), the expected timeline, and the checks the timeline cannot carry.
- `judge(rows, scenario, cycles, peers, variant)` takes `net_logs.timeline()` rows (or `show` text parsed back) and returns pass/fail with the first unmet expectation. Vocabulary: `MUST` (ordered), `MUST (any order)`, `NEVER` and `EXACTLY n` (windowed between the neighbouring MUSTs), `within N s`, `$role` aliases that bind to the first matching peer (re-bound per cycle where the roles rotate; `--peer role=peer` pins one).
- `--list`, `--plan <scenario> [--variant launch-lobby]`, `--judge <scenario> --timeline <folder of net_*.json | net_*.json | show text> [--cycles N] [--peer r=p] [--test id]`, `--marks`, `--self-test`, `--run <scenario>` (thin, marked UNVERIFIED on every print: detects `Port/src/CosmicShore.Player/bin/Debug/net10.0/CosmicShore[.dll]` or `--player`; exits 3 with the build recipe when absent, exits 3 with the UGS-only rule unless `--live` or stand-in URLs are set; launches one player per role as `prisma_party_scenarios/run.sh` does plus `COSMIC_SHORE_NET_LOG_DIR` / `COSMIC_SHORE_NET_PEER=<role>`, plays the steps through `prisma_party_scenarios/driver.py`, quits guests first then the host so the teardown bounces nobody, judges the written logs).

## The four expectation lists (as judged, in order)

**host-drop-reinvite** (row 2, repeat 5x, roles re-bind each cycle)
1. MUST `*` `bounce` "Host disconnected" | "Connection lost" — a survivor lands in its own menu (B10)
2. MUST `$newhost` `party` "->Inviting" within 30 s of 1 — later misses the B29 window and proves nothing
3. NEVER `$guest` `refused` "no longer available" (window 2→5) — the B29 Session case
4. NEVER `*` `bounce` "no longer available"
5. MUST `$guest` `party` "InParty->JoiningParty"
6. MUST any order: `$newhost` `clientApproved` /^[1-9]\d*$/ and `$guest` `party` "JoiningParty->InParty"
7. NEVER `$guest` `bounce` "party is full" | "no longer available" | "has ended"

**double-join-full** (row 3)
1. MUST `$host` `clientApproved` /^[1-9]\d*$/ (2/4); 2. MUST again (3/4)
3. EXACTLY 1 `$host` `clientApproved` /^[1-9]\d*$/ after that — 4/4, never a fifth (B25)
4. NEVER `*` `refused` "party is full" — a pre-flight refusal on a stale count means the race was not exercised (B29 count case)
5. EXACTLY 1 `*` `bounce` "That party is full." — the loser
6. NEVER `*` `bounce` "no longer available"

**leave-at-ready-gate** (row 4; `--variant launch-lobby` for the card lobby)
1-3. MUST `$host` `clientApproved` /^[1-9]\d*$/ ×3 (4/4)
4. MUST any order: `$host` `clientLeft`, `$host` `readyGate` "disconnected" (gate passed because of the disconnect, not a fourth press; B20 match half)
5. NEVER `*` `bounce`
Variant: 4 becomes `$host` `clientLeft` + `$host` log line "launching game" (the lobby writes no mark).

**guest-leaves-mid-match** (row 5)
1-3. MUST `$host` `clientApproved` /^[1-9]\d*$/ ×3
4. MUST `$host` `readyGate` (the match started)
5. MUST any order: `$host` `clientLeft`, `$host` `leaverToAI` (B21)
6. NEVER `*` `bounce`; 7. NEVER `$host` `seatReclaimed`
Not judgeable from the timeline, listed as checks: the ship flies on (`vessels` owner=0 and moving) and the score row stays (`score`).

## Grounding: what is read from code/docs vs guessed

Grounded (the `Mark(...)` call sites in `Assets/_Scripts`, which Amoebius compiles unchanged; `NetTestLogFormat.cs`; `net_logs.py`; `prisma_party_scenarios/README.md` row `net`): `party` with the `PartyState` names and the legal transitions (`InParty->Inviting`, `InParty->JoiningParty`, `JoiningParty->InParty`, `HostingParty->InParty`), `clientApproved` (`<id>`, `<id> spectator`, `<id> rejoin`; client 0 = the host itself, hence the regex), `clientLeft`, `readyGate` (`<Controller>: <name> pressed Ready` | `client <id> disconnected`, marked only on PASS), `leaverToAI`, `seatReclaimed`, `refused` (the `JoinTargetValidator` texts "...'s party is no longer available." / "...'s party is full."), `bounce` (the toast: "That party is full.", "That party has ended."), `rejoinRefused`, `relayDrop`, `stall`, `stallContext`, `netConfig`, `manual`, `offlineChosen`, `offlineFallback`; the netsim presets/tokens and the netfault tokens (`full[=N]`, `ratelimit[=N]`, `relayfail[=N]`, `down`/`up`, `slow=MS`); `netfault full` → "That party is full." bounce (measured 2026-10-08, MULTIPLAYER.md §7); the console lines "launching game" (verbose, ArcadeMatch), "Bouncing to solo menu" (warning), "Join pre-flight refused" (warning), S1/S3's "Accept flow completed successfully." / "Leave-lobby flow completed." (verbose, Party). TESTS.md's S1-S4/S9 criteria are console lines plus eyes, so they are catalogued, not judged.

Guesses / inferences (never seen in a recorded log): the host-loss bounce detail is exactly "Host disconnected" or "Connection lost" (read from `MultiplayerSetup.cs:579/634` → `HandleHostLossAsync` → `BounceToSoloMenuAsync(toast)`); the cross-player ORDER host `clientApproved` vs guest `JoiningParty->InParty` and `clientLeft` vs `readyGate` (avoided by "any order" groups); the 30 s "at once" bound; `netfault slow=1500` on the guest widening the B29 window (marked `to name with Yash`); the host drop itself has no token (`to name with Yash`; `netsim down` keeps the host process alive). The whole `--run` driver is unverified (no .NET SDK here).

## Runs
`--self-test`, `--list`, `--plan` for all four (+ the lobby variant), `--marks`, `--judge` on show-format text (a 2-cycle pass and a B29-refusal fail, exit 1 naming B29), `--run` (exit 3 with the build recipe) all ran. Self-test last line: `net_scenarios: self-test OK` (exit 0).

Follow-ups for whoever owns the gate list (Shombith): add `('net_scenarios_self_test', 'Tools/Build/net_scenarios.py', ['--self-test'])` to `Tools/Build/ship_gates.py`. The sibling `.claude/skills/multiplayer/SKILL.md` already references this file by name (rows 2-5 table); its row 5 says "2 pilots" where the scenario launches 4.
