# Elemental Bars (per-vessel buff/debuff display) - Reference

> Extracted verbatim from `CLAUDE.md` (2026-07-23) so the root file stays a lean
> rules-and-routing dictionary. This is the canonical home of this content now -
> update it here, and keep the corresponding CLAUDE.md digest in sync.

### Elemental Bars (per-vessel buff/debuff display)

`ElementalBarsView` (`_Scripts/UI/View/ElementalBarsView.cs`) is the shared HUD widget every vessel uses to convey its dynamic and meta-earned elemental buffs/debuffs. Each of the four elements (Charge, Mass, Space, Time) renders as a **5-fold-symmetric "flower"**: five copies of one crisp white petal sprite, pivot-centred and rotated 72°·n. The petal shape differs per element (charge = irregular pentagon, mass = triangle, space = kite, time = rhombus), all sharing an inward-pointing 72° apex so adjacent inner edges stay parallel and form the negative-space gaps.

**Level → colour mapping.** `ResourceSystem.GetLevel(element)` returns `floor(normalizedLevel × 10)` with `normalizedLevel ∈ [-0.5, 1.5]` → an integer in **[-5, 15]**. `ElementalBarsConfigSO.DistributePetalValues` spreads that total round-robin across the five petals; each petal value lands in `{-1,0,1,2,3}` → `{fire, grey, white, blue, lime}`:

| Level | -5 | 0 | +5 | +10 | +15 |
|---|---|---|---|---|---|
| Petals | all fire | all grey | all white | all blue | all lime |

At any total at most two adjacent colours show (e.g. +8 → 3 blue + 2 white). Petals are pure white, so a single multiply-tint reproduces every colour exactly — **never hue-shift** (a low-saturation source can't reach grey/white or vivid colours). Each petal recolours and scale-pops about the flower centre (outward bloom) on upgrade, flash+shakes on downgrade.

**Single source of truth — `ElementalBarsConfigSO`** (`_Scripts/ScriptableObjects/`, asset at `Resources/ElementalBarsConfig.asset`). Per CLAUDE.md Config Separation, all shared look/feel lives here: the 5 tick colours, per-element petal sprites, and every juice timing/haptic. All vessels reference the one asset, so the spec can't drift between prefabs. Holds the petal math (`DistributePetalValues`, `ColorForTick`) and constants (`PetalCount=5`, `MinLevel=-5`, `MaxLevel=15`, `PetalSpacing=72`).

**Per-vessel integration.** `ElementalBarsController` (on all 11 vessel prefabs — formerly named `SilhouetteController` before the vessel silhouette/trail-display HUD element it also drove was removed; the leftover `Silhouette` GameObjects were finally excised from all 13 vessel + HUD-variant prefabs in 2026-08, along with the dead `silhouette`/`silhouetteContainer`/`trailContainer` keys — do not re-add a vessel silhouette to a HUD) is the driver: `InitializeElementBars()` calls `elementBars.Build()`, seeds levels, and subscribes to `ResourceSystem.OnElementLevelChange`. The `elementBars` reference is null-safe — vessels without the view wired simply show no bars (opt-in rollout). `SquirrelVesselHUDView` routes drift/joust/crystal juice into the view.

**Zero-wire by default.** With no config or petalRoot assigned, the view loads `Resources/ElementalBarsConfig`, auto-creates a centred flower container per element, and loads petal sprites from `Resources/ElementPetals/{element}_petal`. To author explicitly (recommended for real positioning), run **FrogletTools > Vessels > Wire Elemental Petal Bars** (assigns config + creates `*_Flower` containers), then position the containers. A petal authored in-prefab as `Petal{0..4}` under a container is reused (not duplicated) and normalised via `ElementalBarsView.ConfigurePetal`.

**Patterns to follow:**
- **Spec changes go in the config asset**, never per-vessel SerializeFields — that's the whole point of the shared system.
- **Petal sprites are pure-white silhouettes** tinted at runtime. Add a new element by adding its sprite to the config's `petals` list and `Resources/ElementPetals/`.
- **Rolling out to another vessel**: add an `ElementalBarsView` to that vessel's HUD (or run the wirer), then assign it to the vessel's `ElementalBarsController.elementBars`. No code changes.
- **Performance**: petals render at ~88px — keep `maxTextureSize` small (128). One `Image` per petal (20 total), `raycastTarget` off, event-driven (no `Update`), `SetLevel`/`RefreshBar` early-out when nothing changed, tweens `SetLink`ed and killed + snapped to rest on `OnDisable` for pooled/toggled HUDs.
