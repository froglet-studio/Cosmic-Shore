<div class="sec-eyebrow">Part II · Netcode</div>

# Multiplayer game flow

Once a party launches a game, the same server-authoritative spine drives the match. The base
controller synchronises configuration and the turn/round lifecycle; scoring aggregates by **team
domain** so AI and human teammates finish together.

## Config sync and AI backfill

`MultiplayerMiniGameControllerBase.OnNetworkSpawn` runs `SyncGameConfigToClients_ClientRpc` so every
client receives the same intensity, player count, AI-backfill count, and mode. The player-count
pipeline is fully data-driven — a party of 2 humans can launch a 12-player game with 10 AI, with no
hardcoded caps:

```text
SO_ArcadeGame (min/max) → stepper UI → GameDataSO.ConfigurePlayerCounts(total, humans)
   → RequestedAIBackfillCount = max(0, total − humans)
   → ServerPlayerVesselInitializerWithAI.SpawnAIs() with team balancing
```

`GetBalancedDomain` assigns each AI to the team with the fewest players, breaking ties by domain enum
order (Jade → Ruby → Gold) so identical inputs produce identical AI distributions on every machine —
**no shared RNG seed required**.

## Domain-aggregated scoring

SkimRace, Joust, and Crystal Capture all end on a **per-domain** sum rather than per-player. At most
three scores ever exist (Jade / Ruby / Gold); teammates contribute to the same domain total. The turn
monitor ends the turn when any active domain's summed total reaches the target, so a human and their
AI teammates cross the line together.

::: insight Determinism without a shared seed
Tie-breaking by a fixed enum order instead of RNG is a small decision with an outsized payoff: every
client computes the same team assignment from the same inputs, so there is no "AI distribution
desync" to debug across machines. It is the same instinct as making sessions eager — remove the
nondeterminism rather than synchronise it.
:::

Cross-client domain identity is itself driven by one server-written `NetworkVariable`
(`Player.NetDomain`), whose replication callback fans the change out to the local mirror, the
server-side round stats, and the vessel materials — so scoreboards, end-game UIs, and team colours
stay live across re-picks and AI fills. (Full detail is in the project's domain-sync notes; it is
called out here because it is the same single-writer-then-fan-out pattern the party system uses.)
