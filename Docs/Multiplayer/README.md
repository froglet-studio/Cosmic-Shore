# Multiplayer docs — index

Start with [`../MULTIPLAYER_START_HERE.md`](../MULTIPLAYER_START_HERE.md) for the rules and current state. Use this page to find the right doc.

| Topic | Doc |
|---|---|
| Netcode concepts (authority, tick, interpolation, prediction, lag compensation, interest management, RTT), the verdict for this game, and implementation status | [`MULTIPLAYER_ARCHITECTURE.md`](MULTIPLAYER_ARCHITECTURE.md) |
| Hand bug-hunt checklist: friends panel, presence, arcade lobby, RTT, transform tuning, score guard, rejoin (§5) | [`../PresenceSystem/MULTIPLAYER_TEST_CHECKLIST.md`](../PresenceSystem/MULTIPLAYER_TEST_CHECKLIST.md) |
| Presence lobby: design, poll and converge timings, bugs, tests | [`../PresenceSystem/`](../PresenceSystem/) (`ARCHITECTURE.md` § Freshness fixes, `BUGS.md`, `TESTS.md`, `TODOS.md`) |
| Party session, invites, spectators, UI | [`../PartySystem/`](../PartySystem/) (`ARCHITECTURE.md`, `BUGS.md`, `TESTS.md`, `BRANCH_AUDIT_2026-10.md`) |
| Roadmap, invariants, launch hardening, invite/resilience review, PDF dossier | [`../MultiplayerArchitecture/`](../MultiplayerArchitecture/) |
| NetDiag overlay and NetworkMonitor | [`../NetworkDiagnostics/ARCHITECTURE.md`](../NetworkDiagnostics/ARCHITECTURE.md) |
| Cheating threats, host checks, `HitReportGuard`, server gates | [`../AntiCheat/README.md`](../AntiCheat/README.md) |
| Cloud Code deploy for the gated server checks (`ServerScoreGate` and others) | [`../../Tools/UGS/README.md`](../../Tools/UGS/README.md) |
| Offline mode | [`../OFFLINE_MODE.md`](../OFFLINE_MODE.md) |
| Ecosystem sync between peers | [`../ECOSYSTEM_NETWORK_SYNC.md`](../ECOSYSTEM_NETWORK_SYNC.md) |
| Amoebius engine multiplayer | [`../../Port/docs/MULTIPLAYER.md`](../../Port/docs/MULTIPLAYER.md) |

## Shipped 2026-10-31 → 11-01 (quick facts)

- **Presence:** a 20 s safety poll, dropping to 8 s while an online list is open; a row goes after 2 absent reads; converge at +2 s / +7 s after joining; leaves on `ExitingPlayMode`; Friends set to Offline on quit; Friends re-read every 15 s while the panel is open.
- **Arcade lobby:** guests draw their AI roster from the host's `LobbySnapshot` and resync on drift.
- **Debug overlay:** Ctrl+Shift+M, Editor/Development builds only. Shows presence, Friends, the arcade snapshot and Network (RTT).
- **RTT:** the host samples once a second into `Player.NetRttMs`. `ConnectionQuality` sets the bands (≤100 Good, ≤200 Fair, >200 Poor). Clients see the poor-connection badge after 3 s above 200 ms, release builds included.
- **Ships:** quaternion sync and compression, half floats and unreliable deltas on all 17 vessel prefabs; Tiger Moth no longer syncs scale. `EnableTimeResync: 1` (30 s).
- **Anti-cheat:** `HitReportGuard` caps each report at 10 000 volume, with a 600 burst / 300 per s token bucket. `ServerScoreGate` stays off until Cloud Code and the access policy are deployed.
- **Rejoin:** a dropped player's ship becomes AI; reconnecting within 30 min (`MatchRejoin`) reclaims the seat by UGS id (`TryReclaimSeat` → `seatReclaimed`). "Not now" forgets the record, and the AI keeps the seat to the end of the match.
- **Tests:** `MultiplayerBugHuntTests`, `ConnectionQualityTests`, `HitReportGuardTests`, `MatchRejoinTests`, `DisconnectNoticeTests`.
