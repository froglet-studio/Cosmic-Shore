<div class="sec-eyebrow">Part II · Netcode</div>

# The player & vessel spawn pipeline

Spawning is server-authoritative and unified: menu autopilot vessels, AI opponents, and gameplay
vessels all flow through one pipeline. The server owns identity and ownership; clients reactively
resolve pairs as objects replicate.

::: figure spawn-pipeline
The server waits for `Player` NetworkVariables to sync, spawns and DI-injects the vessel, initialises
the pair, then notifies other clients by RPC. Deliberate pre/post-spawn delays choreograph
replication.
:::

## `Player` is a `NetworkBehaviour`

Identity replicates through six `NetworkVariable`s, split by writer:

| Variable | Writer | Purpose |
|---|---|---|
| `NetDefaultVesselType` | Owner | Vessel class selection |
| `NetName` | Owner | Display name (3-tier fallback: profile → cache → UGS) |
| `NetAvatarId` | Owner | Profile avatar |
| `NetDomain` | Server | Team assignment (via `DomainAssigner`) |
| `NetVesselId` | Server | Linked vessel's `NetworkObjectId` |
| `NetIsAI` | Server | AI flag |

Owner-written variables replicate a *tick after* the object spawns, which is why the server waits
`preSpawnDelayMs` (≈200 ms) before reading them, and why a client pair can briefly observe an empty
name / `Random` vessel type before identity arrives (documented as the benign B7).

## The initializer family

| Class | Role |
|---|---|
| `ServerPlayerVesselInitializer` | Base server spawner. Listens for `OnPlayerNetworkSpawnedUlong`, waits for sync, spawns the vessel prefab, DI-injects, then delegates to the client initializer. Tracks processed players by `NetworkObjectId`. |
| `ServerPlayerVesselInitializerWithAI` | Adds AI backfill — pre-spawns server-owned AI before the base subscribes, and balances teams. |
| `MenuServerPlayerVesselInitializer` | Adds autopilot activation for menu vessels. |
| `ClientPlayerVesselInitializer` | Common pair init; on clients, queues `(playerNetId, vesselNetId)` pairs from RPCs and resolves them **reactively** via spawn SOAP events — zero polling. |

## Two subtleties that caused real bugs

::: bug AI vessels destroyed by scene-load tick batching {fixed}
AI players spawn with **`destroyWithScene: false`**. Without it, a client's scene-load message batched
on the same network tick as the AI spawn, and the client destroyed the just-spawned AI NetworkObjects
— surfacing as `[Invalid Destroy]` on the host and invisible AI on clients. Human vessels are
unaffected because the 200 ms `preSpawnDelayMs` pushes them to a later tick. Because AI no longer gets
free scene-unload cleanup, the controllers explicitly despawn AI before a scene reload.
:::

::: pitfall The MPPM connected-client guard
`LaunchGame`, `ReturnToMainMenu`, and `HandleActiveSessionEnd` all check
`if (nm.IsListening && !nm.IsServer) return` *after* the visual setup but *before* `LoadSceneAsync`. In
Multiplayer Play Mode, SOAP events fire on every virtual player; without this guard a client's
`SceneLoader` would race the server's Netcode scene load and destroy AI objects before they replicate.
The guard keeps the smooth visuals on clients while deferring the actual load to the server.
:::
