# The Ability Lockup (TOTEM) — one system for ability icons + element indicators

**Status:** **fleet-wide and structural.** Every vessel HUD that binds an ability row wears the
lockup; there is nothing per-vessel to author. Design canvas: the "Ability Lockups" artifact (Totem — shipped page).

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
  It is drawn at **44px against the icon's 60px**: the ability is the headline, the element
  qualifies it.
- **Both marks are KERNED, not packed.** The card's corner sliver already eats 12 of the 104 cell,
  so the shipped 80px icon ran its corners into the sliver and the card read solid. `iconContentScale
  0.75` draws it at **60**, leaving an even 22 of negative space on every side, and the flower came
  down 50 → 44 with it so the hierarchy the row was approved with (icon clearly larger) is preserved
  rather than quietly inverted. Same principle as optical kerning: equal *apparent* space, not equal
  metrics — the flower is a sparse radial fan and reads lighter than a solid glyph at equal size,
  which is why it keeps a tighter margin.
- **No authored rect moves.** The card is inserted as a SIBLING behind the icon and its lower cell
  is centred on wherever that icon already sits; the upper cell is *added above*. A vessel adopts
  the style without one authored RectTransform changing.

## Rollout + enforcement (all vessels)

`VesselHUDController.Initialize` — the one method every vessel HUD routes through, on every spawn
path — calls `VesselHUDView.EnsureAbilityLockup()`, which adds and builds the lockup whenever the
HUD binds an ability row. So the style is not opt-in and no prefab has to be edited to adopt it;
a NEW vessel inherits it the moment it binds its four icons. It is added rather than warned about
because the lockup is pure composition over icons that are already authored — there is no
per-vessel art or wiring for a human to supply.

| vessel | row | lockup |
|---|---|---|
| Dolphin | 4/4 | ✅ (component also authored on the prefab — explicit, and equivalent) |
| Scarab | 4/4 | ✅ ensured at runtime |
| Sparrow | 4/4 | ✅ ensured at runtime |
| Squirrel | 4/4 | ✅ ensured at runtime; its AUTHORED flowers are re-homed, not replaced |
| Manta · Rhino · Serpent | 0/4 | — nothing to lock up; blocked on ability DESIGN, not on this style |
| Urchin | 0/4 | — no HUD prefab exists |

**A vessel that authored its own flowers keeps them.** The Squirrel authors all four containers with
their petals in the prefab. Docking RE-HOMES that container into the card (a reparent) rather than
pointing the bars view at a new socket — otherwise the authored flowers would be left rendering at
the old row position while a second set was built at runtime, warnings and all. Re-homing is also
order-independent: it works whether or not the bars view has already built.

**Three guards, per the contract's enforcement ladder:**

1. **Single source** — `Resources/AbilityLockupStyle`; no per-prefab geometry fields exist.
2. **Runtime** — `EnsureAbilityLockup` on the shared init path (above).
3. **Fleet audit** — **FrogletTools > Vessels > Audit Ability Lockups**: asset-only, no play mode.
   It checks the shared style is sane AND the one thing a single shared style *cannot* absorb —
   **per-vessel icon fit**. The card is a fixed 104 wide while icon size is per-prefab, so a vessel
   authored much larger than the fleet's 80 would overflow after kerning, invisibly, until someone
   flew it.
4. **Edit-mode tests** — `AbilityLockupStyleTests` asserts the RELATIONSHIPS (kerning leaves air,
   flower stays under the drawn icon, the two cells stack exactly, states travel), so retuning is
   free and only a change that breaks the composition fails.

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
| `petalFlowerSize` | 44 | element flower; keep BELOW the icon's DRAWN size (60) |
| `iconContentScale` | 0.75 | the card's KERNING — draws an 80 icon at 60. Multiplies the upgrade bump rather than replacing it |
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
3. **Kerning.** Neither the icon nor the flower should touch the plate's corner sliver — even air
   on every side, and the icon still visibly larger than the flower. Retune with `iconContentScale`
   / `petalFlowerSize`; nothing here needs a recompile.
4. **Console.** No `[ElementalBarsView] Created N petal(s) … at RUNTIME` warning and no
   `Auto-creating the '…' flower container` warning: the sockets are supplied, so both paths are
   skipped. Also confirm **no second flower row** appears at the fleet-standard position.
5. **Ladder.** Collect elemental crystals and watch a flower fill grey → white → blue → lime; take a
   danger-prism hit and watch it flash and shake down through fire. Same juice as before.
6. **Upgrade.** Drive an element to level 5 (crystals, or the comeback buff in a mode where you are
   behind). That card's rim should cross to white and a soft bloom come up behind the plate over
   ~0.2s, with a small one-shot punch. Drop below 4 and it should travel back, not snap.
7. **Chips.** LT/RT glyphs sit below the cards (Charge = RT, Time = LT on the Dolphin); Mass and
   Space are passive and correctly show none.
8. **Vessel swap.** Swap to the Dolphin from another vessel in Menu_Main freestyle and confirm
   exactly one set of cards (Build is idempotent; cards are adopted by name).

## Two latent bugs this closed

`blastProfile` and the jaw pair are **children** of the Charge and Space ability icons, so they
already inherit the icon's scale — and `DolphinVesselHUDView` was *also* resting them at
`AbilityIconRestScale`, squaring it. Invisible while rest was 1 (1×1), a quiet 1.15² = **1.32** when
upgraded, and it would have become 0.75² = **0.56** the moment kerning arrived. Nested graphics now
rest at `Vector3.one`; only the slot whose gauge IS the icon (Mass) re-anchors.

**The rule:** `AbilityIconRestScale` is for the ICON's own transform. Anything nested inside an
ability icon inherits it by being a child, and must not re-apply it.

**And its mirror, on the Scarab.** `ScarabHUDView.OnDisable` reset `ballIcon` and `blastIcon` — both
BOUND ability icons — to `Vector3.one`. That silently wiped the upgrade bump on the first hide and
never restored it (nothing re-applies until the next `Initialize`), and it would have wiped the
kerning too. They now rest at `AbilityIconRestScale`. `energyRing` is this view's own gauge image,
not a bound icon, so it correctly stays at one.

**The general shape of both:** any write to a bound ability icon's `localScale` must go through
`AbilityIconRestScale`. A literal `Vector3.one` is only correct for something that is *not* a bound
icon. The fleet auditor cannot see this — it is a code rule, and it is why the sweep for it is part
of rolling this style onto any further vessel.

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
