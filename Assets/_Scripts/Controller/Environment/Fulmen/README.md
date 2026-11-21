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

## What the Stoat's omni crystal does (checked 2026-11-20, Yash: "no giant white hole, just fulmen lightning from center, burning any prisms it passes through")

Every omni crystal the Stoat collects (`crystalsToFire` 1) fires one bolt from the centre of the cell it is in: 5 root
streams out to 60% of the cell's radius (at least 200 u), forking to at most 24 streams / 160 segments, drawn in 0.12 s
and gone 0.38 s later. Up to 6 smaller bolts (40% the range, 2 generations deep) chain from omni crystals a stream
passes within 12 u of. The Meerkat fires the same bolt from the same `FulmenConfig`.

**What burns.** `FulmenSystem.BurnAlong` asks the prism index twice for each segment:

| Pass | Radius round the stream | Rule |
|---|---|---|
| On the path | `pathRadius` 16 u | **Always burns.** A spent `ignitesPerFrame` (96) makes the segment wait a frame; `PrismBurnManager`'s `maxConcurrentBurning` (192) never refuses it. |
| The halo | out to `burnRadius` 200 u | Burns while the burn manager has room (192 burning at once, spreads included), then stops. |

Each lit prism burns for `burnSeconds` 12 s (the Laser look) and spreads by `burnSpread`: 4 generations, 50 u, chance
0.73 halving each generation, at most 20 per prism, every 0.8 s, only while the burn manager has room.

Never burnt, by rule: prisms of the collector's own domain ("never burn your own volume", `PrismBurnManager.Ignite`)
and super-shielded prisms.

**Fixed 2026-11-20.** Before, a prism on the path could be skipped three ways: (1) once a frame's 96 ignites were spent,
every further segment that frame was consumed without a burn query, and with a 200 u radius the first segment's query
alone spends them; (2) a segment's hits past the budget were dropped; (3) with 192 prisms already burning (12 s each),
`Ignite` refused the rest and the chain never retried them. The prisms that did burn were the lowest index slots within
200 u, not the ones on the stream. On a client, `IgniteRemote` also applied the 192 cap to burns the server had already
decided, leaving those prisms standing on that peer; it no longer does. The rule is `FulmenMath.Decide`
(`FulmenMathTests`).

## Swapping the ability

* **Give Fulmen to another vessel (e.g. Gareet):** open its `…ImpactorDataContainer` asset
  (`_SO_Assets/Effects/Effect Containers/VesselContainers/`) and add `FulmenByCrystalEffect` to **Vessel Crystal Effects**.
* **Take it off the Stoat:** remove that entry from `StoatImpactorDataContainer`.

**Fulmen is the Stoat's omni-crystal ability (Yash, 2026-10-29: "Fulmen Network as Omni-crystal ability of Stout").**
It replaced the Stoat's Overclock: `StoatOverclockByCrystalEffect` is no longer in the container (the asset and its
code are kept, so it can be added back). The container now holds the haptics and Fulmen only.

The Meerkat had shared `StoatImpactorDataContainer` since it was first built (b8d6ef57a), so it would have fired Fulmen
and Overclock too. It now has its own `MeerkatImpactorDataContainer`: the same prism effects, haptics only for crystals.
Since 2026-11-11 (Yash: "replace meerkat's networked tunnel with this fulmen") its omni-crystal ability is FULMEN:
`FulmenByCrystalEffect` is in `MeerkatImpactorDataContainer`. The soliton's own omni abilities (the Rift and the Soliton
Network) are parked behind `MeerkatSolitonConfig.omniOn` = 0, code and assets kept for reuse (`author_meerkat_assets.py`).
* **Different tuning per vessel:** duplicate `FulmenConfig` + `FulmenByCrystalEffect`, point the copy's *Config* at the new
  config, and reference the copy from that vessel's container.

No code changes are needed for any of these.

## Activation, flash and burn (round 2)

- **Activation:** `FulmenConfigSO.crystalsToFire` (code default 3; `FulmenConfig.asset` 1 = every crystal). Counted per collector (`VesselImpactor`) in
  `FulmenByCrystalEffectSO`, reset when it fires and on scene unload.
- **Flash timing:** `growSeconds` (0.12) → `holdSeconds` (0.08) → `fadeSeconds` (0.3) with a seeded stepped
  flicker (`flickerRate`, `flickerDepth`). Prisms on the path ignite as the front passes and burn for `burnSeconds` (code default 5; asset 12) within `burnRadius` (code default 10 u; asset 200) and spread to neighbours by the bounded,
  decaying `burnSpread` rule (asset: ≤ 20 extra prisms per struck prism). What burns and what a budget can hold back: the section at the top.
- **Stats:** the config inspector (`FulmenConfigSOEditor`) shows *Prisms Burnt / Crystals Struck / Bolts* for the
  last strike in play mode; the Environmental Fundamentals window shows a `BURNT` pill.
- **Bolt look:** `Assets/_Graphics/Materials/Fulmen/FulmenBolt.mat` (`CosmicShore/FulmenBolt`): thin cool core,
  faint halo, crawling noise pinch, faint flicker, additive. Tune `_Intensity`, `_CoreWidth`, `_HaloGain`, `_Pinch`.
- **Prism burn:** every `PrismBurnManager` burn is drawn by `PrismBurnView` as instanced paper-burn proxies
  (`Assets/_Graphics/Materials/PrismBurn/`, config `Assets/Resources/PrismBurnFxConfig.asset`): NEAR curl /
  MID dissolve / FAR nothing, dithered bands, silent burnouts (`Prism.BurnOut`). Delete the config asset to
  return to the old danger-state + explosion look. Fulmen burns use the **Laser** style (`PrismLaserBurn.mat`): the
  skin peels behind a red-hot line into domain-coloured ash flakes, no flame or light, drawn out to `laserDistance`
  (600 u). Tuning map: [`Docs/Shaders/PRISM_EFFECTS.md`](../../../../../Docs/Shaders/PRISM_EFFECTS.md).

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
