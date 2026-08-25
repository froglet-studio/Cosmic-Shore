# The Ability Lockup (TOTEM) — one system for ability icons + element indicators

**Status:** shipped on the **Dolphin**. Fleet-wide capability; opt-in per vessel by adding one
component. Design canvas: the "Ability Lockups" artifact (Totem — shipped page).

## The problem it closes

The four-icon ability row and the four element flowers were **two separate displays** that a pilot
had to read together: the row said *which ability*, the flower row above said *how much element*,
and the only thing linking them was column position. The upgrade signal was a third language again
(a corner badge petal, an icon tint, a scale bump) — and on a vessel whose icons are all live
gauges, colour was already spoken for, so the Dolphin ran with `tintIconOnUpgrade` **and**
`showUpgradeBadge` both off and the upgrade was carried by a 1.15× scale bump alone.

The lockup fuses them into one card per ability, so a glance answers four questions at once.

## The glance contract (five reads)

| # | Read | Carried by |
|---|---|---|
| 1 | **Which element upgrades this** | the COLUMN — charge → mass → space → time, locked fleet-wide |
| 2 | **How much of that element** | the five petals, on the shipped ladder fire → grey → white → blue → lime |
| 3 | **Which ability** | the vessel's own icon, in the lower cell — never redrawn |
| 4 | **Is it upgraded** | the card's rim to the level-5 white + bloom behind the plate |
| 5 | **How to fire it** | the device chip below the card. No chip = passive |

**The gauge rule survives.** The icon remains the vessel's live-gauge channel (fill · tint · lean).
Petals, rim and chip never carry gauge meaning, and the card — not the icon — carries the upgrade.
That is precisely what lets a row of four live gauges show an upgrade at all.

## Shape (why TOTEM)

One silhouette, two stacked cells: the element flower in the upper cell, the ability icon in the
lower one, a hairline divider between, the control chip below. House motif is **soft-hard-soft** —
bloom (soft) around a flat, radius-0, corner-slivered plate (hard) carrying the icon.

Two properties make it cheap to adopt:

- **The flower is the SHIPPED radial one, unchanged** — same sprites, same 72° fan, same ladder
  colours, same pop-and-shake juice, driven by the same `ElementalBarsView`. Only its *home* moved.
  It is drawn at **50px against the icon's 80px**: the ability is the headline, the element
  qualifies it.
- **No authored rect moves.** The card is inserted as a SIBLING behind the icon and its lower cell
  is centred on wherever that icon already sits; the upper cell is *added above*. A vessel adopts
  the style without one authored RectTransform changing.

## How it composes (and why it is not authored per vessel)

The chrome is pure style read from one asset and is identical on every vessel; only the icon inside
it is per-vessel, and that is already authored. So `AbilityLockupView` builds the chrome around the
four authored icons at runtime — the same shape `VesselHUDView` already uses for the upgrade badge,
and the enforcement ladder this codebase uses for fleet-wide requirements (single config asset →
runtime warn-and-degrade → auditor). Rolling out to another vessel is **one component**, no art.

**Ordering is not relied upon.** `ElementalBarsController.InitializeElementBars` asks the lockup
first and calls `Build()` itself (idempotent), because a HUD that starts inactive does not `Awake`
until it is shown — which is *after* the controller runs. The controller then adopts the view the
lockup docked into rather than creating the fleet-standard row on top of it.

**The standard-placement stamp stands down correctly.** `ElementalBarsView.Build` skips the shared
config's placement stamp when every flower root is supplied — which is exactly what docking does —
so `enforceStandardPlacement` stays `1` fleet-wide and no other vessel is affected.

## Files

| Role | File |
|---|---|
| Style tokens (single source of truth) | `Assets/_Scripts/ScriptableObjects/AbilityLockupStyleSO.cs` |
| Style asset | `Assets/Resources/AbilityLockupStyle.asset` |
| The composer | `Assets/_Scripts/UI/View/AbilityLockupView.cs` |
| Upgrade hook (shared) | `Assets/_Scripts/UI/View/VesselHUDView.cs` — `SetAbilityUpgraded` → `SetUpgraded` |
| Flower socket injection | `Assets/_Scripts/UI/View/ElementalBarsView.cs` — `TrySetPetalRoot` |
| Adoption (no duplicate row) | `Assets/_Scripts/Controller/Vessel/ElementalBarsController.cs` |
| Sprites (white + alpha, 9-sliced) | `Assets/_Graphics/Design Assests/HUD UI/AbilityLockup/Lockup{Plate,PlateRim,Bloom}.png` |
| Dolphin wiring | `Assets/_Prefabs/UI Elements/VesselHUD/DolphinHUDVariant.prefab` (one component on the root) |

## Tuning knobs — `Assets/Resources/AbilityLockupStyle.asset`

| Knob | Shipped | Meaning |
|---|---|---|
| `plateWidth` | 104 | card width; sits inside the shipped 150px ability cell |
| `abilityCellHeight` | 104 | lower cell, centred on the existing icon |
| `petalCellHeight` | 62 | upper cell, added ABOVE — this is what makes it a totem |
| `petalFlowerSize` | 50 | element flower; keep BELOW the vessel's icon size (80) |
| `dividerInset` / `dividerThickness` | 8 / 1 | the hairline between cells |
| `bloomPadding` | 26 | how far the upgraded bloom reaches past the card |
| `plateColor` / `hairlineColor` | `#060810` @0.86 / `#5C5F70` @0.9 | resting fill + outline |
| `upgradedRimColor` | `#F5F5FF` | the level-5 white the flowers already speak |
| `bloomColor` | `#F5F5FF` @0.24 | alpha carries it — in engine bloom clamps at max-channel 0.5, so glow is bought with lit AREA |
| `upgradeTransitionDuration` | 0.2 | states travel; nothing pops |
| `unlockPunchScale` / `unlockPunchDuration` | 1.05 / 0.5 | one-shot ceremony on unlock only, never on re-lock |

Sprites are white + alpha and tinted at runtime (the T7 sprite-kit rule), 9-sliced (border 16 on
plate/rim, 48 on bloom), so one asset set serves every vessel and every size.

## In-editor verification

1. Open a scene with a Dolphin (`MinigameBends` or `MinigameRampage`) and enter play mode.
2. **Row.** Four cards in the lower right, charge → mass → space → time. Each shows a small element
   flower above the Dolphin's existing gauge (Echo Sight profile / crystal / jaws / boost ring).
   The gauges must still animate exactly as before — the icons were not moved or restyled.
3. **Console.** No `[ElementalBarsView] Created N petal(s) … at RUNTIME` warning and no
   `Auto-creating the '…' flower container` warning: the sockets are supplied, so both paths are
   skipped. Also confirm **no second flower row** appears at the fleet-standard position.
4. **Ladder.** Collect elemental crystals and watch a flower fill grey → white → blue → lime; take a
   danger-prism hit and watch it flash and shake down through fire. Same juice as before.
5. **Upgrade.** Drive an element to level 5 (crystals, or the comeback buff in a mode where you are
   behind). That card's rim should cross to white and a soft bloom come up behind the plate over
   ~0.2s, with a small one-shot punch. Drop below 4 and it should travel back, not snap.
6. **Chips.** LT/RT glyphs sit below the cards (Charge = RT, Time = LT on the Dolphin); Mass and
   Space are passive and correctly show none.
7. **Vessel swap.** Swap to the Dolphin from another vessel in Menu_Main freestyle and confirm
   exactly one set of cards (Build is idempotent; cards are adopted by name).

## Follow-ups

- **Bake tool.** A `FrogletTools > Vessels > Bake Ability Lockups` that writes the composed chrome
  into the prefab (the pattern `Bake Elemental Petal Bars Into All Vessel HUDs` already sets), so
  the authored state is inspectable. Runtime composition is the shipping path today.
- **Fleet auditor.** An asset-only `Audit Ability Lockups` reusing the runtime discovery, per the
  contract's enforcement ladder step 4.
- **Roll out** to Squirrel / Sparrow / Urchin (maps complete). Manta / Rhino / Serpent are blocked
  on ability DESIGN, not on this style.
- **Upgraded icon art** (`AbilityIconBinding.upgradedSprite`) is still unauthored fleet-wide; the
  lockup carries the upgrade without it, so it is now optional rather than missing.
