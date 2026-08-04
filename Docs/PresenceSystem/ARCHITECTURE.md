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
| `displayName` | Self | Everyone | UI label (see "Identity propagation" below) |
| `avatarId` | Self | Everyone | Avatar art |
| `partyCount` | Self | Everyone | "I'm in a party of N" — for "IN PARTY N/4" badges |
| `partyMax` | Self | Everyone | The cap N is referring to |
| `matchName` | Self | Everyone | Game-mode label while in a real multiplayer match ("IN A MATCH — X"); empty on menu scenes incl. lava-lamp/freestyle |
| `invite_payloads` | Sender | Recipients (refresh-loop scan) | Composite: one line per outgoing invite, `targetId\|senderId\|sessionId\|senderName\|senderAvatarId`. `sessionId` is the sender's CURRENT party session — a party MEMBER's invite carries the actual host's session (invite chain, `../PartySystem/INVITE_ENHANCEMENTS.md` Task 4) |
| `accepted_invite` | Recipient | Sender (refresh-loop scan) | "I'm coming to join your session" handshake signal |
| `joined_party` | Recipient | Everyone (host admit-scan) | "I'm now in this party session" — cross-checked against the session's authoritative player list (B8) |

All 8 keys are seeded on every lobby (re)join by
`PresenceLobbyService.BuildLocalPlayerProperties` so no key is ever
absent on first refresh (absent looks identical to empty in UGS).

## State-preserving rejoin & lobby convergence (B4 fix, 2026-07-16)

A lobby **rejoin** (reconnect, or the periodic
`ConvergeToCanonicalAsync` migration that heals simultaneous-create
splits) re-publishes the full property dict. Historically that dict
seeded the stateful keys (`invite_payloads` / `joined_party` /
`accepted_invite` / `matchName`) to **empty**, which is why convergence
had to be *paused* while an invite was outstanding or a party had
formed — and that pause froze lobby splits exactly when a 3rd player
was being invited (bug B4's failure surface).

Now `IPresenceLobbyService.LivePropertySource` — a provider set once by
`HostConnectionService` (`BuildLivePresenceProperties`) — overlays LIVE
values onto the dict at every (re)join:

| Key | Live value carried across the rejoin |
|---|---|
| `invite_payloads` | `InviteService.SerializeAll()` — pending outgoing invites survive a migration |
| `joined_party` | The joined session id when the local player is a **guest** (`!IsPartyHost`) — the host's admit-scan never loses a member mid-migration |
| `matchName` | `ResolveCurrentMatchName()` when non-empty |

`accepted_invite` is deliberately NOT preserved: it is a fast-path hint
the inviter also gets from the session member sync, and carrying it
across rejoins would make stale signals permanent.

With the rejoin state-safe, the convergence pause is **removed** —
`ConvergeToCanonicalAsync` runs on its normal throttle
(`PRESENCE_CONVERGE_INTERVAL_SECONDS`) even mid-invite / mid-party, so
splits self-heal in every phase. Single-writer is preserved: HCS
remains the sole author of the values; `PresenceLobbyService` only
carries them.

## Identity propagation (display name / avatar) — how a rename reaches every surface

**Source of truth:** `PlayerDataService.CurrentProfile` (Cloud Save).
Every rename UI (`ProfileModal`, `ProfileIconSelectView`,
`ArcadeProfileWidget`, the Authentication-scene username panel) routes
through `PlayerDataService.SetDisplayName`, which raises
`OnProfileChanged` — the single fan-out trigger.

**Fan-out on change** (`HostConnectionService.HandleProfileChanged`):

1. `SyncLocalIdentity()` — refresh `HostConnectionDataSO.Local*` from
   the profile (fallback chain: profile → UGS `PlayerName` minus
   `#suffix` → `"Pilot"`).
2. `RepublishLocalIdentityAsync()` — immediate push of
   `displayName`/`avatarId` to the presence lobby (**online lists**).
3. `PartySessionService.UpdateLocalPlayerPropertiesAsync()` — re-publish
   the local player's record on the party SESSION (**party slots** on
   every peer; session player properties are otherwise written only at
   create/join).
4. `RefreshLocalPartyMemberEntry()` — replace the local player's own
   `PartyMembers` entry (seeded as a snapshot) so the local slot
   repaints.

**Guaranteed reconciliation:** the immediate push is best-effort
(`LobbyPropertyWriter.WriteAsync` swallows terminal save failures), so
`displayName`/`avatarId` also ride the change-gated per-tick publish
(`PublishPartyStateIfChangedAsync`, one combined save with
partyCount/partyMax/matchName). Only that success-gated path updates
the `_publishedDisplayName`/`_publishedAvatarId` trackers — a rename
the push missed goes out on the next refresh tick, always.

**Remote ingestion (per surface):**

| Surface | Mechanism |
|---|---|
| Online list rows | `RefreshOnlinePlayersDiff` change-detects DisplayName/AvatarId and RemoveAt+Inserts the entry — the list's item events re-render the row |
| Party slot rows | `PartyMemberService.SyncFromSession` identity refresh: RemoveAt+Insert at the same index — repaints via list item events, WITHOUT raising the member-joined/left SOAP events (identity refresh ≠ membership change; no invite-clear / state-machine side effects) |
| In-game names / scoreboards | Owner writes `Player.NetName` on profile change (`HandleProfileLoadedAfterSpawn`); replication lands in `OnNetNameValueChanged` on every peer, which mirrors into `Player.Name` AND `RoundStats.Name` (server write replicates `RoundStats.n_Name`) — scoreboard identity follows without waiting for the next scene's pair-init re-sync |

**Latency:** the sender's push is immediate; each remote picks it up on
its own refresh tick (base 1.5 s / 0.75 s boosted) — so end-to-end
≈ 1-2 s. True sub-second push is the roadmap "push-based presence"
item (deferred; it rides the same SDK path as the B1/B6 churn).

**Account caveat:** re-tagging an MPPM clone switches it to a NEW UGS
account whose fresh profile gets a generated `Pilot####` default name —
that is account switching, not a sync failure. See
`../PartySystem/TESTS.md` § "MPPM prerequisites".

**Manual test:** `TESTS.md` **P7** (rename propagation).

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
| Local player's party-SESSION player record | `PartySessionService` (seeded at create/join; `UpdateLocalPlayerPropertiesAsync` on profile change) | `PartyMemberService.ReadMemberData` on every peer |
| `HostConnectionDataSO.OnlinePlayers` list | `HostConnectionService.RefreshOnlinePlayersDiff` | UI components (`FriendsListPanel` / `OnlineInfoEntry`, `ArcadeLobbyList`) |
| `HostConnectionDataSO.PartyMembers` list | `HostConnectionService` (member-sync paths incl. identity refresh) | UI |

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
- `../NetworkDiagnostics/ARCHITECTURE.md` — NetDiag overlay used by presence catches
