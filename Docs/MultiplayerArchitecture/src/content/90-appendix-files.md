<div class="sec-eyebrow">Appendix A</div>

# File & class index

Canonical locations for everything referenced in this document. Line counts are approximate.

## Party / presence services — `Assets/_Scripts/Controller/Party/`

| File | Lines | Role |
|---|---|---|
| `HostConnectionService.cs` | 2034 | Orchestrator; single writer to `HostConnectionDataSO` |
| `PartyInviteController.cs` | 463 | Accept / decline / leave transition sequencing |
| `FriendsInitializer.cs` | 236 | Friends service bridge + presence |
| `Services/PresenceLobbyService.cs` | 503 | Lobby-only session lifecycle |
| `Services/PartySessionService.cs` | 369 | Relay-backed session lifecycle + retry |
| `Services/AcceptanceSignalService.cs` | 316 | Sender↔receiver handshake |
| `Services/NetworkTransitionService.cs` | 288 | NM shutdown + connection/scene waits |
| `Services/InviteService.cs` | 269 | Outgoing-invite tracking + serialization |
| `Services/SoapPartyEventBus.cs` | 206 | Centralized SOAP raises |
| `Services/PartyMemberService.cs` | 188 | Member list diffing |
| `Services/LobbyPropertyWriter.cs` | 182 | Mutex + retry property writes |
| `Services/LobbyRefreshScheduler.cs` | 173 | Refresh cadence + boost |
| `StateMachine/PartyStateMachine.cs` | 155 | Validated lifecycle transitions |
| `StateMachine/PartyState.cs` | 90 | The 7 lifecycle states |
| `Interfaces/*` | — | `IPresenceLobbyService`, `IPartySessionService`, `INetworkTransitionService`, `IPartyMemberService`, `IInviteService`, `IPartyStateQuery` |

## Netcode / multiplayer — `Assets/_Scripts/Controller/Multiplayer/`

| File | Lines | Role |
|---|---|---|
| `ServerPlayerVesselInitializer.cs` | 472 | Base server vessel spawner |
| `MultiplayerSetup.cs` | 462 | NetworkManager lifecycle + UGS sessions |
| `ClientPlayerVesselInitializer.cs` | 395 | Client pair init + RPCs |
| `ServerPlayerVesselInitializerWithAI.cs` | 353 | AI pre-spawn + team balancing |
| `MenuCrystalClickHandler.cs` | 347 | Play-from-menu freestyle toggle |
| `MenuVesselSelectionPanelController.cs` | 251 | Network-aware vessel swap |
| `ArcadeConfigSyncManager.cs` | 243 | Config sync |
| `MenuServerPlayerVesselInitializer.cs` | 238 | Menu autopilot spawner |
| `DomainAssigner.cs` | — | Team pool assignment |

## Cross-cutting & SOAP

| File | Role |
|---|---|
| `Assets/_Scripts/System/AuthenticationServiceFacade.cs` | Auth single-writer |
| `Assets/_Scripts/System/FriendsServiceFacade.cs` | Friends single-writer |
| `Assets/_Scripts/Controller/Player/Player.cs` | `NetworkBehaviour` + 6 NetworkVariables |
| `Assets/_Scripts/Utility/MainThreadDispatcher.cs` | SyncContext-based main-thread switch |
| `Assets/_Scripts/Utility/ClassExtensions/UniTaskExtensions.cs` | `.AsMainThread()` overloads |
| `Assets/_Scripts/Utility/NetworkDiagnostics.cs` | NetDiag classifier |
| `Assets/_Scripts/Utility/DataContainers/HostConnectionDataSO.cs` | Party SOAP container |
| `Assets/_Scripts/Utility/DataContainers/FriendsDataSO.cs` | Friends SOAP container |

## Canonical engineering docs — `Docs/`

`PartySystem/` (ARCHITECTURE · REFACTOR · BUGS · TESTS · TODOS · MPPM_SESSION_LOG) ·
`PresenceSystem/` (ARCHITECTURE · REFACTOR · BUGS · TESTS · TODOS) ·
`NetworkDiagnostics/` (README · TESTS · TODOS) · `THREADING.md` · `SCENES.md`. This document is
synthesised from those sources.
