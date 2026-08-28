<div class="sec-eyebrow">Part II · Cross-cutting</div>

# SOAP data architecture

SOAP (Scriptable Object Architecture Pattern) is how the whole project does cross-system
communication: shared state lives in `ScriptableVariable` assets, notifications travel through
`ScriptableEvent` channels, and UI reacts via inspector-wired listeners. The party system uses it with
one strict rule — **single writer, many readers**.

::: figure soap-dataflow
`HostConnectionService` is the only writer of `HostConnectionDataSO`. Every UI component and the
presence bridge read through its events and lists — none of them call the UGS SDK directly.
:::

## `HostConnectionDataSO` — the party data container

| Kind | Members |
|---|---|
| Lists | `OnlinePlayers`, `PartyMembers` (self at index 0) |
| Events | `OnHostConnectionEstablished`, `OnHostConnectionLost`, `OnPartyMemberJoined`, `OnPartyMemberLeft`, `OnPartyMemberKicked`, `OnInviteReceived`, `OnInviteSent`, `OnPartyJoinCompleted`, `OnInviteResolved` |
| Runtime state | `LocalPlayerId`, `LocalDisplayName`, `LocalAvatarId`, `IsConnected`, `IsPresenceLobbyHost`, `IsPartyHost` |

## `FriendsDataSO` — the friends container

Four reactive lists (`Friends`, `IncomingRequests`, `OutgoingRequests`, `BlockedPlayers`) and four
events (`OnFriendAdded`, `OnFriendRemoved`, `OnFriendRequestReceived`, `OnFriendsServiceReady`).

## The SOAP value types

| Type | Shape |
|---|---|
| `PartyInviteData` | `HostPlayerId`, `PartySessionId`, `HostDisplayName`, `HostAvatarId` |
| `PartyPlayerData` | `PlayerId`, `DisplayName`, `AvatarId`, `PartyMemberCount`, `PartyMaxSlots`, `MatchName` — equality by `PlayerId` only, so party-state fields can update without breaking dedup |
| `FriendData` | `PlayerId`, `DisplayName`, `Availability`, `ActivityStatus` |

::: pitfall Fail loud — no null-guards on SOAP event fields
Project policy forbids `if (evt != null)` guards on serialized `ScriptableEvent` fields. A missing
reference is a wiring error that should crash immediately and obviously, not silently no-op and hide
the bug. The same philosophy runs through the whole stack: loud, traceable failures over quiet
divergence.
:::

::: insight Why single-writer SOAP is load-bearing here
With one writer, "who is in the party is wrong" has exactly one place to investigate. UI is fully
decoupled — it can be tested against a fabricated `HostConnectionDataSO` with no UGS at all — and there
are no two-writer races on shared lists. It is the data-flow analogue of the state machine: one owner,
validated changes, observable everywhere.
:::
