<div class="sec-eyebrow">Part I · Overview</div>

# The two-level session model

The single most important architectural choice is to split "being discoverable" from "being
connected for gameplay" into two distinct UGS sessions that run side by side.

::: figure two-level-model
Two UGS sessions per player: a lobby-only Presence Lobby for discovery and invite exchange, and a
Relay-backed Party Session for gameplay. The invite payload is the bridge between them.
:::

| Layer | Purpose | Relay? | Max players | Service |
|---|---|---|---|---|
| **Presence Lobby** | Player discovery, invite property exchange | No (lobby-only) | 100 | `PresenceLobbyService` |
| **Party Session** | Actual gameplay networking via Relay | Yes (`WithRelayNetwork()`) | 4 (configurable) | `PartySessionService` |

## Why separate them

A single Relay-backed session for "everyone online" would be wrong on every axis: Relay allocations
are a finite, billed resource, joining one is a slow handshake, and you would not want global
discovery traffic flowing over your gameplay transport. Splitting the two means:

- **Discovery is cheap and global.** The presence lobby holds up to 100 players with no Relay cost,
  and joining it is fast and never disturbs an active `NetworkManager`.
- **Invites need no host privilege.** Invites travel as **per-player properties** on the presence
  lobby — any member can write their own properties, so any player can invite any other without the
  lobby host mediating.
- **Gameplay stays small and hot.** The party session is capped at a handful of players and exists
  only for the people actually flying together.

::: pitfall They are not the same thing
A common confusion is to treat "in the lobby" and "in the party" as one state. They are independent:
joining the presence lobby does **not** join a party session, and vice-versa. The presence lobby is a
*discovery signal*; the party session is the *authoritative truth* of who is connected. When the two
disagree, the session always wins — a rule that directly fixed a real host-side bug (B8 in Part II).
:::

## How an invite crosses the gap

The presence lobby carries a small set of per-player properties. Two of them are the invite channel,
and two more complete the handshake and roster reconciliation:

| Property | Writer | Meaning |
|---|---|---|
| `invite_target` | Sender | Player ID of the intended recipient |
| `invite_data` | Sender | Serialized invite payload (sender's **party session id**, display name, avatar) |
| `accepted_invite` | Recipient | "I'm coming to join your session" handshake |
| `joined_party` | Recipient | "I'm now in this party session" — for host roster reconciliation |

Because every player already hosts a party session (next section), the `invite_data` payload always
carries a **real** session id. The recipient simply leaves their own session and joins the sender's.
