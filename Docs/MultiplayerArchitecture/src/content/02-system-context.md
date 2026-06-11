<div class="sec-eyebrow">Part I · Overview</div>

# System context

Every player runs the same Unity client. Inside it, the game talks to two very different worlds: a
set of **Unity Gaming Services** in the cloud, and a peer-to-peer **Netcode** mesh that flows through
UGS **Relay** for NAT traversal. The party/presence/friends services are the glue that decides *who*
connects to *whom*, and Netcode handles *what* gets replicated once they are connected.

::: figure system-context
The client uses UGS for identity, discovery, and transport provisioning; Netcode for GameObjects then
runs gameplay replication over a Relay-allocated connection to party peers.
:::

## The UGS surface

| Service | What we use it for |
|---|---|
| **Authentication** | Anonymous sign-in; a stable `PlayerId`; cached-session restore. The first thing to complete on boot — everything else waits on it. |
| **Multiplayer Sessions** | The unified session API. We create two *kinds* of session from it: a lobby-only presence session and a Relay-backed party session. |
| **Lobby** | Backs the presence session — player lists and per-player properties (how invites travel). |
| **Relay** | Allocates a relay server so clients behind NAT can connect without port-forwarding. The party session attaches Relay via `WithRelayNetwork()`. |
| **Friends** | Persistent relationships and rich presence ("In Menu", "In Party", "In Game"). |

## Netcode for GameObjects

Once a party session exists, one player is the **host** (server + client) and the others are
**clients**. `NetworkManager` drives connection, scene synchronization, and object replication. The
game's `Player` and vessel objects are `NetworkBehaviour`s; their state replicates through
`NetworkVariable`s and `ClientRpc`/`ServerRpc` calls. Importantly, the presence lobby is **lobby-only
(no Relay)**, so it coexists with a live `NetworkManager` — a player can be discoverable in the lobby
and connected for gameplay at the same time without the two interfering.

::: insight One client, two clocks
A lot of the subtlety in this codebase comes from the fact that UGS calls are cloud round-trips whose
continuations land on the .NET thread pool, while Netcode and all Unity objects live strictly on the
main thread. Bridging those two clocks safely is the subject of the threading model in Part II.
:::
