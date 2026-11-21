# Multiplayer docs — start here

Every multiplayer topic has **one canonical home**. Other docs link to it rather than repeating it. For rules every session follows and the owner's priorities, read [`../MULTIPLAYER_START_HERE.md`](../MULTIPLAYER_START_HERE.md).

## The hub (this folder)

| Doc | What it is |
|---|---|
| [`MULTIPLAYER_ARCHITECTURE.md`](MULTIPLAYER_ARCHITECTURE.md) | Topology, netcode concepts (authority, tick, interpolation, prediction, lag compensation, interest management, RTT), the verdict for this game, and the netcode roadmap status (§5) |
| [`TODO.md`](TODO.md) | **The one live multiplayer to-do list and roadmap**, by real state, with links to the per-system detail |
| [`BUGS.md`](BUGS.md) | **Known-bugs index**: open, fixed-pending-verification, fixed. Detail stays in the per-system BUGS files |
| [`TESTS.md`](TESTS.md) | **Test inventory** (EditMode suites, manual procedures) and the latest results |
| [`TEST_CHECKLIST.md`](TEST_CHECKLIST.md) | Hand bug-hunt checklist for MPPM / multi-machine runs, with the Ctrl+Shift+M debug overlay |

## Canonical home per topic

| Topic | Canonical doc |
|---|---|
| Presence lobby (online list, converge, poll timings, freshness fixes) | [`../PresenceSystem/ARCHITECTURE.md`](../PresenceSystem/ARCHITECTURE.md) |
| Party session, invites, Relay host, recovery | [`../PartySystem/ARCHITECTURE.md`](../PartySystem/ARCHITECTURE.md), [`../PartySystem/INVITE_ENHANCEMENTS.md`](../PartySystem/INVITE_ENHANCEMENTS.md) |
| Party UI, spectators | [`../PartySystem/UI.md`](../PartySystem/UI.md), [`../PartySystem/SPECTATOR.md`](../PartySystem/SPECTATOR.md) |
| Arcade config sync (host `LobbySnapshot`, guest roster drift) and match length | [`MULTIPLAYER_ARCHITECTURE.md`](MULTIPLAYER_ARCHITECTURE.md) §1, [`../ARCADE_MATCH_LENGTH.md`](../ARCADE_MATCH_LENGTH.md) |
| Mid-match rejoin (AI takeover → seat reclaim) | [`MULTIPLAYER_ARCHITECTURE.md`](MULTIPLAYER_ARCHITECTURE.md) §2.11 |
| Offline mode | [`../OFFLINE_MODE.md`](../OFFLINE_MODE.md) |
| Ecosystem sync between peers | [`../ECOSYSTEM_NETWORK_SYNC.md`](../ECOSYSTEM_NETWORK_SYNC.md) |
| NetDiag overlay, NetworkMonitor | [`../NetworkDiagnostics/ARCHITECTURE.md`](../NetworkDiagnostics/ARCHITECTURE.md) |
| Debug overlay (Ctrl+Shift+M), RTT badge | [`TEST_CHECKLIST.md`](TEST_CHECKLIST.md), [`MULTIPLAYER_ARCHITECTURE.md`](MULTIPLAYER_ARCHITECTURE.md) §1 |
| Anti-cheat (threats, `HitReportGuard`, server gates) | [`../AntiCheat/README.md`](../AntiCheat/README.md); Cloud Code deploy in [`../../Tools/UGS/README.md`](../../Tools/UGS/README.md) |
| Launch hardening plan, invariants | [`../MultiplayerArchitecture/HARDENING_PLAN_STEAM_LAUNCH.md`](../MultiplayerArchitecture/HARDENING_PLAN_STEAM_LAUNCH.md), [`../MultiplayerArchitecture/ROADMAP.md`](../MultiplayerArchitecture/ROADMAP.md) (invariants) |
| Refactor backlogs | [`../PartySystem/REFACTOR.md`](../PartySystem/REFACTOR.md), [`../PresenceSystem/REFACTOR.md`](../PresenceSystem/REFACTOR.md) |
| Amoebius engine multiplayer | [`../../Port/docs/MULTIPLAYER.md`](../../Port/docs/MULTIPLAYER.md) |

## History (read-only records)

- [`../MultiplayerArchitecture/REVIEW_INVITE_AND_RESILIENCE.md`](../MultiplayerArchitecture/REVIEW_INVITE_AND_RESILIENCE.md): the 2026-10-06 review (its timings are superseded)
- [`../PartySystem/MPPM_SESSION_LOG.md`](../PartySystem/MPPM_SESSION_LOG.md), [`../PartySystem/SWEEP_LOG.md`](../PartySystem/SWEEP_LOG.md), [`../PartySystem/BRANCH_AUDIT_2026-10.md`](../PartySystem/BRANCH_AUDIT_2026-10.md)
- [`../MultiplayerArchitecture/README.md`](../MultiplayerArchitecture/README.md): the PDF dossier (a snapshot)
