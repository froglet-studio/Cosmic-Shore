<div class="sec-eyebrow">Part II · Foundations</div>

# Architecture in layers

The party subsystem is deliberately layered so that each concern is testable in isolation and the UI
never reaches past the SOAP boundary into a UGS call.

::: figure architecture-layers
Five layers, top to bottom: UI reads SOAP; SOAP is written by the orchestration layer; orchestration
delegates to focused services; services call the UGS SDK and Netcode. Dependencies point downward
only.
:::

## The orchestration layer

| Class | Role |
|---|---|
| `HostConnectionService` | The orchestrator and **single writer** to `HostConnectionDataSO`. Auto-joins the presence lobby on sign-in, eagerly creates the party session, runs the refresh watchdog, and exposes party operations (`AcceptInviteAsync`, `EnsurePartySessionAsync`, `KickPartyMemberAsync`). `DontDestroyOnLoad` singleton. |
| `PartyInviteController` | The user-facing flow controller. Sequences the Netcode host↔client transitions for accept / decline / leave, with linked-CTS timeouts and explicit recovery. |
| `PartyStateMachine` | The single source of truth for the party lifecycle phase. Validates every transition against a static table; replaced a drift-prone scatter of boolean flags. |

## The service layer

The 17-commit refactor extracted nine focused services behind interfaces, so each does one thing:

| Service | Responsibility |
|---|---|
| `PresenceLobbyService` | Lobby-only session lifecycle — join-or-create, converge-to-canonical, refresh, leave. |
| `PartySessionService` | Relay-backed session lifecycle — create / join / leave, with three retry classifiers. |
| `InviteService` | Outgoing-invite tracking, payload serialization, timeout. |
| `PartyMemberService` | Diffs the SOAP party-member list against the live session player list. |
| `NetworkTransitionService` | `NetworkManager` shutdown and connection/scene-sync waits, all with timeouts. |
| `AcceptanceSignalService` | The sender↔receiver acceptance handshake. |
| `LobbyPropertyWriter` | Mutex-protected, retry-wrapped lobby property writes. |
| `LobbyRefreshScheduler` | The polling cadence — base interval plus a post-invite boost window. |
| `SoapPartyEventBus` | Centralizes the SOAP event raises so they happen in one auditable place. |

::: insight Interfaces are the seams
Every service sits behind an interface (`IPresenceLobbyService`, `IPartySessionService`,
`INetworkTransitionService`, `IPartyMemberService`, `IInviteService`, `IPartyStateQuery`). That is
what lets the orchestrator be reasoned about — and tested — without spinning up real UGS sessions, and
what kept the 2,000-line `HostConnectionService` from growing into something unmaintainable.
:::
