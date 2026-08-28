<div class="sec-eyebrow">Appendix C</div>

# UGS SDK call map

Which service method maps to which Unity Gaming Services SDK call. This is the surface the whole
party/presence/friends stack is built on.

## Sessions & Relay

| Layer · method | UGS SDK call |
|---|---|
| `PresenceLobbyService.JoinOrCreateAsync` | `QuerySessionsAsync` (filter `PRESENCE_LOBBY`) → `CreateSessionAsync` |
| `PresenceLobbyService.ConvergeToCanonicalAsync` | `QuerySessionsAsync` → `JoinSessionByIdAsync` |
| `PresenceLobbyService.RefreshAsync` | `ISession.RefreshAsync` |
| `PresenceLobbyService.LeaveAsync` | `ISession.AsHost().DeleteAsync` / `LeaveAsync` |
| `PartySessionService.CreateAsync` | `CreateSessionAsync(SessionOptions.WithRelayNetwork())` |
| `PartySessionService.JoinByIdAsync` | `JoinSessionByIdAsync` (with retry) |
| `PartySessionService.RefreshAsync` | `ISession.RefreshAsync` |
| `PartySessionService.LeaveAsync` | `ISession.AsHost().DeleteAsync` / `LeaveAsync` |
| `HostConnectionService.KickPartyMemberAsync` | `ISession.AsHost().RemovePlayerAsync` |

## Lobby properties (the invite channel)

| Layer · method | UGS SDK call |
|---|---|
| `LobbyPropertyWriter.SaveWithRetryAsync` | set player properties + `ISession.RefreshAsync` (mutex + retry) |
| `HostConnectionService.SendInviteAsync` | write `invite_target` / `invite_data` player properties |
| `HostConnectionService` presence scan | read each lobby player's properties each refresh tick |

## Authentication

| Layer · method | UGS SDK call |
|---|---|
| `AuthenticationServiceFacade.EnsureSignedInAnonymouslyAsync` | `AuthenticationService.SignInAnonymouslyAsync` |
| `AuthenticationServiceFacade.TrySignInCachedAsync` | cached-session restore |
| `AppManager` startup | `UnityServices.InitializeAsync` |

## Friends

| Layer · method | UGS SDK call |
|---|---|
| `FriendsServiceFacade.InitializeAsync` | `FriendsService.InitializeAsync` |
| `SendFriendRequestByNameAsync` / `SendFriendRequestAsync` | `AddFriendByNameAsync` / `AddFriendAsync` |
| `AcceptFriendRequestAsync` | `AddFriendAsync` |
| `DeclineFriendRequestAsync` / `CancelFriendRequestAsync` | `DeleteIncomingFriendRequestAsync` / `DeleteOutgoingFriendRequestAsync` |
| `RemoveFriendAsync` | `DeleteFriendAsync` |
| `BlockPlayerAsync` / `UnblockPlayerAsync` | `AddBlockAsync` / `DeleteBlockAsync` |
| `SetPresenceAsync` / `SetAvailabilityAsync` | `SetPresenceAsync` / `SetPresenceAvailabilityAsync` |
| `RefreshAsync` | `ForceRelationshipsRefreshAsync` |

::: insight Every one of these awaits `.AsMainThread()`
Without exception, each SDK call above is awaited through the `.AsMainThread()` boundary helper so its
continuation — and any SOAP event it raises — resumes on Unity's main thread. That single, uniform
discipline is what keeps the entire cloud surface from leaking ThreadPool continuations into Unity
state. It is the connective tissue under every row in this table.
:::

---

*End of document. Synthesised from the canonical engineering docs under `Docs/` and the party,
multiplayer, and player source under `Assets/_Scripts/`. — Cosmic Shore · Froglet Inc.*
