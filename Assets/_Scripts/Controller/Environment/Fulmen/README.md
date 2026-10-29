# Fulmen — cosmic thunderbolt (Charge fundamental)

Collecting an omni crystal fires a branching 3D bolt from the collector's **cell centre**. Its streams burn prisms
(through `PrismBurnManager`, server/offline only) and every omni crystal within reach of a branch spawns a smaller child
bolt (struck crystals are not consumed), so crystal clusters chain-react.

| Piece | Role |
|---|---|
| `FulmenMath` | Pure, seeded bolt-shape generator (EditMode tests: `FulmenMathTests`). |
| `FulmenConfigSO` | All tuning: reach, chain caps, growth, per-frame budgets, burn radius, shape, look. Listed in *Environmental Fundamentals*. |
| `FulmenSystem` | Scene singleton (made on demand). Pooled bolts/chains, budgeted segment queries + ignites, child chaining. |
| `FulmenView` | One world-space mesh for all bolts, added by `FulmenSystem`. |
| `FulmenByCrystalEffectSO` | The ability hook (`VesselCrystalEffectSO`). No vessel code. |

Assets: `_SO_Assets/Environment/FulmenConfig.asset`, `_SO_Assets/Effects/Vessel Crystal Effects/FulmenByCrystalEffect.asset`.

## Swapping the ability

* **Give Fulmen to another vessel (e.g. Gareet):** open its `…ImpactorDataContainer` asset
  (`_SO_Assets/Effects/Effect Containers/VesselContainers/`) and add `FulmenByCrystalEffect` to **Vessel Crystal Effects**.
* **Take it off the Stoat:** remove that entry from `StoatImpactorDataContainer`.

**Fulmen is the Stoat's omni-crystal ability (Yash, 2026-10-29: "Fulmen Network as Omni-crystal ability of Stout").**
It replaced the Stoat's Overclock: `StoatOverclockByCrystalEffect` is no longer in the container (the asset and its
code are kept, so it can be added back). The container now holds the haptics and Fulmen only.

The Meerkat had shared `StoatImpactorDataContainer` since it was first built (b8d6ef57a), so it would have fired Fulmen
and Overclock too. It now has its own `MeerkatImpactorDataContainer`: the same prism effects, haptics only for crystals.
Its omni-crystal ability is the Soliton Network, which `MeerkatSolitonExecutor` grows from
`OmniCrystalImpactor.CollectedByVessel` (MEERKAT.md section 9), not a container entry.
* **Different tuning per vessel:** duplicate `FulmenConfig` + `FulmenByCrystalEffect`, point the copy's *Config* at the new
  config, and reference the copy from that vessel's container.

No code changes are needed for any of these.

## Activation, flash and burn (round 2)

- **Activation:** `FulmenConfigSO.crystalsToFire` (default 3). Counted per collector (`VesselImpactor`) in
  `FulmenByCrystalEffectSO`, reset when it fires and on scene unload.
- **Flash timing:** `growSeconds` (0.12) → `holdSeconds` (0.08) → `fadeSeconds` (0.3) with a seeded stepped
  flicker (`flickerRate`, `flickerDepth`). Prisms on the path ignite as the front passes and burn for `burnSeconds` (1.4).
- **Stats:** the config inspector (`FulmenConfigSOEditor`) shows *Prisms Burnt / Crystals Struck / Bolts* for the
  last strike in play mode; the Environmental Fundamentals window shows a `BURNT` pill.
- **Bolt look:** `Assets/_Graphics/Materials/Fulmen/FulmenBolt.mat` (`CosmicShore/FulmenBolt`): thin cool core,
  faint halo, crawling noise pinch, faint flicker, additive. Tune `_Intensity`, `_CoreWidth`, `_HaloGain`, `_Pinch`.
- **Prism burn:** every `PrismBurnManager` burn is drawn by `PrismBurnView` as instanced paper-burn proxies
  (`Assets/_Graphics/Materials/PrismBurn/`, config `Assets/Resources/PrismBurnFxConfig.asset`): NEAR curl /
  MID dissolve / FAR nothing, dithered bands, silent burnouts (`Prism.BurnOut`). Delete the config asset to
  return to the old danger-state + explosion look.

## Network (multiplayer)

The **server** is the only peer that counts charge, decides a strike, strikes crystals and ignites prisms
(`FulmenByCrystalEffectSO`); clients ignore the broadcast crystal dispatch and replay the server
(`FulmenNetSync`). The messages ride the collector's `NetworkVesselImpactor` (already on every vessel
prefab; see `NetworkVesselImpactor.Fulmen.cs`), all reliable on one ordered channel:

| Message | Size | When |
|---|---|---|
| Strike: id, config hash, seed, centre, range, excluded crystal, server time | 40 B | once per strike |
| Child: strike id, crystal id, child seed, generation, origin, range | 28 B | per chained bolt (≤ MaxChildren) |
| Burn batch: strike id, server time, count + 8 B per prism (offset from centre in 1/8 unit, noise seed, tenths of a second) | 11 B + 8 B·n, ≤ 150 a message | once per frame while the strike lights prisms (its spreads too) |

Charge is a server-written `NetworkVariable<byte>` (`NetworkVesselImpactor.FulmenCharge`), so late joiners and
client HUDs read the true count. A solo host / offline session allocates no net id and sends nothing.

**Prism identity.** Trail and environment prisms have no network id (`SpatialIndexId` is per-peer registration
order), so a burn names its prism by position; the client burns the nearest live prism within 0.75 units.
**Destruction.** There is no authoritative destroy sync for trail mass - every peer simulates it. A client's
replicated burn (`PrismBurnManager.IgniteRemote`) never spreads (the server sends its spreads), runs on the
server's timeline (lit `ago` = server-time delta) with the same noise seed, and ends in the quiet
`Prism.BurnOut` - the one destroy on that peer, so no second explosion.
