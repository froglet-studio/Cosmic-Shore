# Presence System — Architecture Snapshot

The presence lobby is the **discovery layer** of the party system.
Players use it to find each other and exchange invite payloads. It's
distinct from the party (Relay) session that hosts actual gameplay.

For the party (Relay) layer see `../PartySystem/ARCHITECTURE.md`.

## What it is

A UGS Multiplayer **lobby-only** session (no Relay transport). Up to
100 players per lobby. Every authenticated player joins this lobby on
sign-in via `HostConnectionService.JoinOrCreatePresenceLobbyAsync`,
which delegates to `PresenceLobbyService.JoinOrCreateAsync`.

| Property | Value |
|---|---|
| Type | UGS Multiplayer Session, lobby-only |
| Max players | 100 |
| Relay transport | None — coexists safely with a NetworkManager |
| Session-property game mode tag | `PRESENCE_LOBBY` |
| Discovery model | Query for existing lobby with the `PRESENCE_LOBBY` tag; if found, join; else create. Re-query after a 1500 ms settle to detect simultaneous-creation races. |

## Why it's separate from the party session

| Layer | Role | Why separate |
|---|---|---|
| **Presence Lobby** | "I exist, here are my properties, here are the invites I'm sending" | Lobby-only means it can host every signed-in player without exhausting Relay allocations. Joining is fast (no Relay handshake) and joining doesn't disrupt any existing NetworkManager. |
| **Party Session** | "These players are actively connected via Relay for gameplay" | Has Relay; capped at small player count; comes and goes per party formation. |

A common confusion is treating these as the same thing. They aren't.
Joining the presence lobby does NOT join the party session, and vice
versa.

## How invites travel

Invites are encoded as **per-player properties on the presence lobby**,
not as session properties. This is intentional:

- **Per-player properties** can be written by the player themselves —
  no host privilege needed. Any lobby member can invite any other lobby
  member.
- **Session properties** would require the host to mediate every
  invite, which doesn't fit the discovery model (the host might not
  even know the recipient exists).

| Property key | Writer | Reader | Meaning |
|---|---|---|---|
| `displayName` | Self | Everyone | UI label |
| `avatarId` | Self | Everyone | Avatar art |
| `partyCount` | Self | Everyone | "I'm in a party of N" — for "In Lobby N/4" badges |
| `partyMax` | Self | Everyone | The cap N is referring to |
| `invite_target` | Sender | Recipient (via refresh-loop scan) | Player ID of the recipient |
| `invite_data` | Sender | Recipient | Serialized `PartyInviteData` (sender's session ID + display + avatar) |
| `accepted_invite` | Recipient | Sender (via refresh-loop scan) | "I'm coming to join your session" handshake signal |
| `joined_party` | Recipient | Everyone | "I'm now in this party session" — for host roster reconciliation |

The full property list and write semantics live in
`PresenceLobbyService.cs:60-72`.

## Refresh cadence

`LobbyRefreshScheduler` controls the polling interval that drives
property reads. Two modes:

| Mode | Interval | Trigger |
|---|---|---|
| **Base** | 3-5 s | Default; runs as long as the lobby is active |
| **Boost** | ~2 s for 15 s | Fired by `Boost()` on invite-receive or party-state change; tightens the window for the joiner to see the next state change |

Boosting on invite-receive is the reason the lobby property writes can
cluster — see `BUGS.md` B1.

## Single-writer pattern

| What | Writer | Readers |
|---|---|---|
| Presence-lobby active session reference | `PresenceLobbyService` only | `HostConnectionService` (via `IPresenceLobbyService.ActiveLobby`) |
| Local player's lobby properties | `LobbyPropertyWriter.SaveWithRetryAsync` (mutex-protected) | All party services that need to publish |
| `HostConnectionDataSO.OnlinePlayers` list | `HostConnectionService.RefreshOnlinePlayersDiff` | UI components (`OnlinePlayersPanel`, `PartyAreaPanel`, `PartyArcadeView`) |
| `HostConnectionDataSO.PartyMembers` list | `HostConnectionService` (member-sync paths) | UI |

Anything that wants to know the presence-lobby state reads through SOAP
events / lists on `HostConnectionDataSO`. Nothing else reaches into
`PresenceLobbyService` directly.

## ForceReset semantics

`PresenceLobbyService.ForceReset()` clears the internal `_activeLobby`
reference so the next `JoinOrCreateAsync` will proceed (rather than
no-op). Called only by `HostConnectionService.RefreshAsync` when the
consecutive-error counter exceeds the reconnect threshold (and the
catch-guard at the top of `RefreshAsync` did not fire).

**False ForceReset is the main historical failure surface** — the YS2
bug (commit `a1a8eb9`) was an in-flight refresh that fired ForceReset
during a successful party transition, leaving the joiner in a private
throwaway lobby. The fix added a second catch-guard inside the catch
block (companion to the existing entry guard at the top of
`RefreshAsync`).

## Key files

| Role | File |
|---|---|
| Service implementation | `Assets/_Scripts/Controller/Party/Services/PresenceLobbyService.cs` |
| Interface | `Assets/_Scripts/Controller/Party/Interfaces/IPresenceLobbyService.cs` |
| Property writer (mutex + retry) | `Assets/_Scripts/Controller/Party/Services/LobbyPropertyWriter.cs` |
| Refresh cadence | `Assets/_Scripts/Controller/Party/Services/LobbyRefreshScheduler.cs` |
| Invite-receive detection | `Assets/_Scripts/Controller/Party/Services/InviteService.cs` |
| Acceptance signal | `Assets/_Scripts/Controller/Party/Services/AcceptanceSignalService.cs` |
| Benign log filter | `Assets/_Scripts/Utility/BenignLobbyLogFilter.cs` |

## Related docs

- `REFACTOR.md` — `PresenceLobbyService` refactor backlog
- `BUGS.md` — open presence-side bugs (B1, B4, B6)
- `TESTS.md` — presence-specific manual test procedures
- `TODOS.md` — minor parking-lot items
- `../PartySystem/ARCHITECTURE.md` — party (Relay) layer
- `../NetworkDiagnostics/README.md` — NetDiag overlay used by presence catches
