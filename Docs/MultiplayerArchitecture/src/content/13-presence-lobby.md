<div class="sec-eyebrow">Part II · The discovery layer</div>

# The presence lobby

`PresenceLobbyService` owns a UGS **lobby-only** session — no Relay — that every signed-in player
joins. It is the discovery layer: up to 100 players, fast to join, and safe to run alongside a live
`NetworkManager`.

## Join-or-create, and the simultaneous-create race

On sign-in the service queries for an existing lobby tagged `PRESENCE_LOBBY`; if found it joins, else
it creates one. Two players signing in at the same instant can both fail to find a lobby and both
create one — a split. The service heals this:

- After creating, it **re-queries after a ~1500 ms settle** to detect a rival lobby.
- A periodic **converge-to-canonical** pass (every few seconds) migrates everyone onto a single
  deterministic "canonical" lobby, collapsing accidental splits back into one.

```csharp
// resolved fresh at use time — never cached in the constructor (the null-pinning pitfall)
private IMultiplayerService _multiplayerService => MultiplayerService.Instance;
```

## How invites travel — per-player properties

Invites are encoded as **per-player properties**, not session properties, precisely so no host
privilege is needed: any member can write their own properties and thus invite anyone.

| Property | Writer | Reader | Meaning |
|---|---|---|---|
| `displayName` / `avatarId` | Self | Everyone | UI label and art |
| `partyCount` / `partyMax` | Self | Everyone | "In Lobby N/4" badges |
| `invite_target` | Sender | Recipient | Player ID of the recipient |
| `invite_data` | Sender | Recipient | Serialized `PartyInviteData` (sender's session id + identity) |
| `accepted_invite` | Recipient | Sender | "I'm coming to join" handshake |
| `joined_party` | Recipient | Everyone | "I'm in this party session" — host roster reconciliation |

## Refresh cadence

`LobbyRefreshScheduler` drives property reads in two modes: a **base** interval of a few seconds, and
a **boost** (~2 s for ~15 s) fired on invite-receive or party-state change to tighten the window for
the joiner to see the next state change. The cadence is kept comfortably under the UGS read
rate-limit. (The clustering of property writes that boosting can cause is the trigger for the benign
SDK stale-index noise documented as B1.)

## ForceReset — and why it's dangerous

`ForceReset()` clears the cached lobby reference so the next join-or-create actually runs rather than
no-opping. It is called only by the refresh watchdog after the consecutive-error counter trips.

::: pitfall A false ForceReset is the classic failure surface
The historical "YS2" bug was an in-flight refresh that fired `ForceReset` *during* a successful party
transition, dumping the joiner into a private throwaway lobby. The fix added a second catch-guard
inside the refresh catch block (companion to the entry guard) so a transition in progress suppresses
the reset. The lesson: a watchdog that can fire mid-transition must consult "am I transitioning?"
before acting.
:::

Everything else reads presence state through SOAP — `HostConnectionDataSO.OnlinePlayers` and the
member lists — never by reaching into the service directly.
