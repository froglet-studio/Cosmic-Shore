<div class="sec-eyebrow">Part II · Social</div>

# The friends system

Friends use the UGS Friends SDK and follow the same single-writer / multi-reader SOAP pattern as auth
and party. `FriendsServiceFacade` is the sole writer of `FriendsDataSO`; UI reads four reactive lists
and four events.

## Facade API

Every mutating method calls `SyncAllRelationships()` after the SDK call to keep the SOAP lists honest.

| Method | UGS SDK call | Effect |
|---|---|---|
| `InitializeAsync` | `FriendsService.InitializeAsync` | Wire events, sync lists, raise `OnFriendsServiceReady` |
| `SendFriendRequestByNameAsync` | `AddFriendByNameAsync` | Add to `OutgoingRequests` |
| `SendFriendRequestAsync` | `AddFriendAsync` | Add to `OutgoingRequests` |
| `AcceptFriendRequestAsync` | `AddFriendAsync` | Move incoming → `Friends`, raise `OnFriendAdded` |
| `DeclineFriendRequestAsync` | `DeleteIncomingFriendRequestAsync` | Remove from `IncomingRequests` |
| `RemoveFriendAsync` | `DeleteFriendAsync` | Remove from `Friends`, raise `OnFriendRemoved` |
| `BlockPlayerAsync` / `UnblockPlayerAsync` | `AddBlockAsync` / `DeleteBlockAsync` | Manage `BlockedPlayers` |
| `SetPresenceAsync` | `SetPresenceAsync` | Update rich presence |
| `RefreshAsync` | `ForceRelationshipsRefreshAsync` | Full server refresh |

## Rich presence across scene transitions

`FriendsInitializer` is the only thing that sets presence, so it stays consistent:

| Trigger | Availability | Activity |
|---|---|---|
| Enter menu / sign-in | `Online` | "In Menu" (`Menu_Main`) |
| Enter a game scene | `Busy` | "In Game" (scene, vessel class, party session id) |
| App shutdown | `Offline` | — |

## Two ways to add a friend

These are intentionally different because the caller has different information:

| Entry point | Input | Facade method | Where |
|---|---|---|---|
| `AddFriendPanel` | Player **name** (text) | `SendFriendRequestByNameAsync` | Friends panel → Add tab |
| `OnlinePlayerEntry` | Player **ID** (from presence) | `SendFriendRequestAsync` | Online players panel → per-row "+" |

::: insight Friends vs. party invites are different systems
Adding a friend is a **persistent** relationship via the Friends SDK; a party invite is an
**ephemeral**, session-scoped exchange via lobby player properties. They can appear on the same UI row
but they never share state — and the friend system *feeds* the party system (a friend row's "invite"
button calls `HostConnectionService.SendInviteAsync`), rather than duplicating it.
:::
