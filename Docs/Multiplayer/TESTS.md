# Multiplayer tests — inventory and latest results

## Latest results (Cosmos, 2026-11-02, `Ys-bleeding-edge`, Unity 6000.3.17f1, batchmode)

- **Compile:** clean.
- **EditMode:** 4917 / 4952 passed. **Every multiplayer suite passes except one test:** `OfflineSessionTests.Case4_NetworkLostMidSession_RaisesTheNoticeEventOnce`. The other 34 failures are non-multiplayer content / bake tests.
- **In-game checklist ([`TEST_CHECKLIST.md`](TEST_CHECKLIST.md)):** not run. It needs the interactive editor with MPPM.
- **Multi-instance smoke test:** not possible yet, because there are no scripted MPPM scenarios and no automatic host/join harness ([`TODO.md`](TODO.md) items 5-6).

## EditMode suites (`Assets/_Scripts/Tests/Editor/`)

| Area | Suites |
|---|---|
| Presence | `AbsenceStrikesTests`, `PresencePushTests`, `PresenceLobbyCreateTests`, `MultiplayerBugHuntTests` |
| Party | `PartyInviteControllerTests`, `PartyInviteDataTests`, `PartyInviteSystemTests`, `PartyPlayerDataTests`, `PartyPushAndVoidedReadTests`, `PartyRecoveryTests`, `PartyRecoveryAfterPlayTests`, `PartyRefreshRecoveryTests`, `PartyRosterTests`, `PartyTrayTests`, `LobbyPropertyWriterRetryTests`, `HostConnectionDataSOTests`, `FriendProfileSourceTests`, `SpectatorSessionTests` |
| Arcade lobby | `ArcadeLobbySnapshotTests`, `ArcadeGridCapacityTests`, `ArcadeGridOrderTests` |
| Netcode | `ConnectionQualityTests`, `HitReportGuardTests`, `NetworkConfigMarkTests`, `NetworkExplodeParamsTests`, `EcologyNetworkAuthorityTests`, `CombatHitDrainTests` |
| Rejoin / disconnect / offline | `MatchRejoinTests`, `DisconnectNoticeTests`, `OfflineSessionTests` (Case4 failing) |
| Diagnostics | `NetSessionRecorderTests` |

## Manual procedures

| Doc | Use |
|---|---|
| [`TEST_CHECKLIST.md`](TEST_CHECKLIST.md) | The current bug hunt: lobby fixes, RTT, transform tuning, score guard, rejoin |
| [`../PartySystem/TESTS.md`](../PartySystem/TESTS.md) | Party smoke, stress and failure gates |
| [`../PresenceSystem/TESTS.md`](../PresenceSystem/TESTS.md) | Presence smoke, stress and failure gates |
| [`../NetworkDiagnostics/TESTS.md`](../NetworkDiagnostics/TESTS.md) | NetDiag accuracy tests A-E |
| [`../MULTIPLAYER_START_HERE.md`](../MULTIPLAYER_START_HERE.md) §5 | Owner's MPPM and live UGS runs; `net_logs.py` log collection |
