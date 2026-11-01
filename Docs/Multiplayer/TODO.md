# Multiplayer to-do list and roadmap

The one live list. Updated 2026-11-02 from the code on `Ys-bleeding-edge`. Per-system detail lives in the linked TODO files; this list says what state each item is in.

## Now: verify what landed

1. **Run [`TEST_CHECKLIST.md`](TEST_CHECKLIST.md) in MPPM with 4 players** (not run yet). It covers the four lobby fixes, RTT, transform tuning, the score guard and rejoin.
2. **Fix or triage `OfflineSessionTests.Case4`** (the one failing multiplayer EditMode test, [`TESTS.md`](TESTS.md)).
3. **Two-machine retest of B39-B44** and the B46 / B47 rejoin faults ([`BUGS.md`](BUGS.md)).
4. **B36 / B45 boot order**: owner decision to start the Relay host after the menu is up.

## Next: test automation

5. **MPPM play-mode scenarios** (4 virtual players, scripted) so the checklist runs without a person.
6. **Auto host/join harness**: a dev build or MPPM entry point that hosts or joins from the command line and logs presence, converge, ready, RTT and roster drift, so a multi-instance smoke test can run headless.
7. Rebuild the Amoebius edit-mode suite (fill the 102 engine APIs).

## Then: netcode roadmap ([`MULTIPLAYER_ARCHITECTURE.md`](MULTIPLAYER_ARCHITECTURE.md) §4 / §5)

| Item | State |
|---|---|
| RTT sampling, overlay Network section, poor-connection badge | ✅ Done 2026-11-01 |
| RTT on the party panel and the scoreboard | ⬜ Not started |
| NetworkTransform tuning, time resync | ✅ Done (needs the simulator check, checklist §5) |
| `HitReportGuard` on prism reports | ✅ Done |
| `ServerScoreGate` / Cloud Code `weekly-score` | 🟡 Built, gated off until the script and Access Control policy are deployed ([`../../Tools/UGS/README.md`](../../Tools/UGS/README.md)) |
| Mid-match rejoin | ✅ Exists; B46 / B47 open |
| Party-level reconnection grace period | ⬜ Not started |
| Host migration | ⬜ Not started (listen server; a host loss ends the match) |
| Party-wide Relay region selection | ⬜ Not started |
| Packed inputs / bandwidth beyond transform settings | ⏸ Skipped, not needed at 4 players |
| Movement anti-cheat beyond observe-only | ⏸ Later ([`../AntiCheat/README.md`](../AntiCheat/README.md)) |

## Per-system backlogs

- Presence: [`../PresenceSystem/TODOS.md`](../PresenceSystem/TODOS.md) (P1, P2, P4-P7 open; P3 done)
- Party: [`../PartySystem/TODOS.md`](../PartySystem/TODOS.md) (TODO-1 re-scoped; 5-8, 10-12 open; 2, 3, 9 done)
- Network diagnostics: [`../NetworkDiagnostics/TODOS.md`](../NetworkDiagnostics/TODOS.md) (TODO-1 to 8 open)
- Refactors: [`../PartySystem/REFACTOR.md`](../PartySystem/REFACTOR.md), [`../PresenceSystem/REFACTOR.md`](../PresenceSystem/REFACTOR.md)
- Launch hardening blocks: [`../MULTIPLAYER_START_HERE.md`](../MULTIPLAYER_START_HERE.md) §4, [`../MultiplayerArchitecture/HARDENING_PLAN_STEAM_LAUNCH.md`](../MultiplayerArchitecture/HARDENING_PLAN_STEAM_LAUNCH.md)
