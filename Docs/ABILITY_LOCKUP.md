# The Ability Lockup (TOTEM) — one system for ability icons + element indicators

**Status:** **fleet-wide and structural.** Every vessel HUD wears the lockup — a vessel that binds
no ability icons yet still gets the four-card row with its open slots drawn LOCKED, so no hull is
left on the old UI while it waits for design. There is nothing per-vessel to author. Design canvas:
the "Ability Lockups" artifact (Totem — shipped page).

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
| 4 | **Is it upgraded** | bloom behind BOTH plates + a lift in their fill (borderless — there is no rim) |
| 5 | **How to fire it** | the device chip below the card. No chip = passive |
| 6 | **How much of the ability is ready** | the card's own linear gauge, rising through the ability plate |
| 7 | **Is it firing right now** | the ability plate lights and decays |
| 8 | **Is it recharging** | a radial veil sweeping off the ability plate, over the icon |

**The gauge rule survives, and now has a home.** The icon remains the vessel's live-gauge channel
(fill · tint · lean). Petals and chip never carry gauge meaning, and the card — not the icon —
carries the upgrade. That is precisely what lets a row of four live gauges show an upgrade at all.

## Shape (why TOTEM)

**Two borderless trapezoids meeting at their wide edges across a small gap** — the element flower in
the upper one, the ability icon in the lower one, the control chip below. The element plate narrows
*upward*, the ability plate narrows *downward*, so the pair mirrors about the seam and the totem has
a waist.

**The two plates are the same height** (88 / 88), which makes the totem a true mirror image about
the gap. Unequal plates — the first pass ran 62 above 104 — stack into a tall lopsided hexagon that
reads as a coffin rather than a totem. `PlateImbalance` is asserted at ≤25% by both the auditor and
the tests, because a single field edit can put the coffin back silently.

**And the two marks are the same size too** (60 / 60). Mirrored plates present each mark with the
same negative space, so an equal mark in each is what makes the pair kern as one object. *This
supersedes an earlier rationale here* — the first mirroring pass kept the flower at 44 "because the
hierarchy lives in the marks, not the plates". That was wrong in practice: against a mirrored plate
a smaller flower does not read as hierarchy, it reads as the upper plate being under-filled. Nothing
was ever telling the two apart by size anyway — they are different *kinds* of mark (a five-petal
meter and a solid glyph) in fixed columns. The guard is now a band, `0.75 ≤ flower/icon ≤ 1.0`:
larger inverts the row, much smaller is the under-filled read. House motif is **soft-hard-soft** — bloom (soft) around flat, radius-0 plates (hard)
carrying the icon.

**The gap is the divider and the silhouette is the frame.** A hairline between two halves of one
plate, and an outline around that plate, were both drawing a boundary the shape can state on its
own — so the divider and the rim are both retired and the plates carry no border at all. The slant
does the work the outline used to: two trapezoids facing wide-edge to wide-edge read as one object,
where two borderless *rectangles* would read as a list. **The upgrade is a bloom behind both plates
plus a lift in their fill** — with no rim, those two are the whole signal, which is why the upgraded
plate now lifts much further from the resting one than it did when the rim carried the change.

**A graded band rides the two sloped sides.** Solid for the *whole length* of each slant, then
**wrapping around both corners** onto the horizontals where it grades to nothing — so it accents the
two edges that carry the shape's identity and **never closes into a border**. That is the
distinction worth keeping: the rim (a closed outline around everything) stays retired; this is an
edge on the slants only, and the plate's core stays transparent behind it.

Wrapping is what makes the corner readable. A gradient that died *on* the slant left the corner
itself unlit, which reads as the shape being unfinished rather than as an accent; putting the whole
grade on the wrap means the slant is solid end to end and the corner is the brightest part of it.

**Antialiasing is baked into the geometry.** A canvas gives a generated diagonal no AA at all, so a
3px diagonal strip is pure stair-steps without help. The band is emitted as three quads across its
width — a zero-alpha feather outside, the solid core, a zero-alpha feather inside — and the
bilinear interpolation between those vertex alphas *is* the antialiasing, at no texture cost. It is
generated into the *same mesh* as the plate, so an edged plate is still one draw call, and its alpha
multiplies the plate's own so a fade takes both together. `slantEdgeThickness + slantEdgeAntialias`
must stay at or below `trapezoidInset` or the band reads as a chamfer.

### Finalizing: the PNG bake

The generated plates cost **~200 vertices per card, ~800 per row** (48 per band side, ×2 sides, ×2
plates, plus 4 for each fill). That is small for UI and it is what makes the shape *tunable* — every
number above is live. Once the numbers are final, swap geometry for texture:

1. Render `LockupPlateElement.png` and `LockupPlateAbility.png` at 2× (208 × 176), each carrying its
   fill, its wrapped band and its baked AA. The aspect is fixed fleet-wide, so neither needs a
   9-slice; a plain `Image` at `Type.Simple` is exact.
2. Replace each plate's `TrapezoidGraphic` with an `Image` — 4 verts, one draw call, no per-frame
   mesh build.
3. **The clips still need the shape, and a PNG serves them too**: `Mask` reads alpha, so the gauge
   and cooldown clips can carry the same ability-plate sprite.
4. Keep `TrapezoidGraphic` in the tree as the generator the PNGs were baked from — it is how the
   next retune is done, and re-deriving it from the sprites would not be possible.

**The plates are generated, not sprited** (`TrapezoidGraphic`, one small `MaskableGraphic` in the
shape of `BlastProfileGraphic`). A trapezoid has no 9-slice — slanted edges do not tile — so a
sprited version would freeze the slant into the art, need a re-export every time the number moved,
and need one asset per direction. Generated, the slant is a single float (`trapezoidInset`) that
both plates read *mirrored*, so the two halves of one totem can never disagree about it. One graphic
type serves the plate, the gauge track, the gauge clip and the press flash; **`bloomSprite` is the
only authored asset the lockup still needs.**

Two properties make it cheap to adopt:

- **The flower is the SHIPPED radial one, unchanged** — same sprites, same 72° fan, same ladder
  colours, same pop-and-shake juice, driven by the same `ElementalBarsView`. Only its *home* moved.
  It is drawn at **44px against the icon's 60px**: the ability is the headline, the element
  qualifies it.
- **The lockup owns the ROW, not just the card.** Position, pitch, cell size, host scale and icon
  size all come from the one style asset and are written onto every vessel at build time. Before
  this the prefabs disagreed on every one of them: the Sparrow and Scarab anchored their row in a
  different container entirely (x 0.076–0.996 of a sub-rect, not the screen), the Squirrel scaled
  its buttons **0.7**, and the Dolphin authored one icon at **96** against its other three at 80.
  None of that is read any more — a prefab cannot make the row diverge, and nobody re-authors an
  icon to match the fleet.
- **Icon size is DERIVED, never authored.** Each icon is scaled by `iconBoxSize / its authored size`
  (`AbilityLockupStyleSO.IconScaleFor`), so an 80, a 96 and a 148 all draw at 60. This is the
  difference between "every vessel was edited to agree" and "disagreeing is impossible".
- **The legacy button plate is retired.** The Sparrow, Squirrel and Scarab each drew a decagon
  `Ability Background Small` behind their icons; the card replaces it, so the lockup disables it and
  hands the button's target graphic to the card's plate — which also means the touch area finally
  matches the shape the player can see. The Dolphin never had one.
- **Both marks are KERNED, not packed.** The card's corner sliver already eats 12 of the 104 cell,
  so an 80px icon ran its corners into the sliver and the card read solid. `iconBoxSize` draws every
  icon at **60**, leaving an even 22 of negative space on every side, and the flower came
  down 50 → 44 with it so the hierarchy the row was approved with (icon clearly larger) is preserved
  rather than quietly inverted. Same principle as optical kerning: equal *apparent* space, not equal
  metrics — the flower is a sparse radial fan and reads lighter than a solid glyph at equal size,
  which is why it keeps a tighter margin.
- **No authored rect moves.** The card is inserted as a SIBLING behind the icon and its lower cell
  is centred on wherever that icon already sits; the upper cell is *added above*. A vessel adopts
  the style without one authored RectTransform changing.

## The gauge — one shape, and it is not a ring

A meter belonged to whatever the vessel happened to draw it as: a radial ring around the Sparrow's
boost icon, another around the Scarab's Ball Forge, a chevron fill on the Squirrel, an undriven
circular heat halo behind the Squirrel's skimming icon. Three different shapes for one idea, all of
them circles wrapped around a square-ish icon inside a square-ish card.

**The fleet's gauge is a linear fill that takes over the ability plate.** A vessel binds its existing
meter `Image` as `AbilityIconBinding.gauge` and the lockup re-homes it into the card and re-forms it
to `Filled` / `Vertical` / origin `Bottom`, behind the icon and above a dim track. **The vessel keeps
writing `fillAmount` on the very same Image** — no gameplay wiring changes, only where and how it
draws. Colour is a default the vessel is free to keep driving where colour carries meaning (the
Squirrel's boost tints to the pilot's domain).

**It is inset inside the slant band, not laid flush over it.** The track is drawn *after* the plate
and, at `gaugeCellFraction 1`, is exactly the same trapezoid — so at full width it painted over the
plate's band and that card silently lost its edge. Only the cards that actually *bind a meter*,
which is why it showed up on the Squirrel's Charge slot alone. Everything laid on a plate now insets
by `PlateEdgeReach` (`slantEdgeThickness + slantEdgeAntialias`), so the band **frames** the meter —
the better read anyway. General shape of the bug worth remembering: *a decoration that exactly
covers the thing beneath it is invisible until something else needs what is underneath.*

**It is masked to the trapezoid, not reshaped into one.** A `Filled` Image is a rectangle, so on a
tapering plate it would overhang by up to `trapezoidInset` per side at the base. The fill is drawn
through a `GaugeClip` — a `TrapezoidGraphic` carrying a `Mask` — which shapes it exactly while
leaving the vessel's `fillAmount` contract untouched. The alternative, mirroring that value onto a
`TrapezoidGraphic`, would need a per-frame poll of somebody else's field: a drive site this style has
no business owning. Cost is two extra draw calls on a card that HAS a meter; three cards fleet-wide
carry one today. The track's own taper is **derived from the plate at the track's top**, not assumed
to be the full width — identical at `gaugeCellFraction 1`, which is why that seam would have stayed
invisible until someone lowered the fraction.

**The lockup DRAWS the chip, from one fleet-wide glyph set.** A vessel authors no control glyphs at
all. Each card resolves its own artwork down a chain that is derived at every step:

> ability → its `ElementalAbilityMapSO` entry's `InputEvents` → `InputHintBindingMap.BindingFor`
> → physical control → `ControlGlyphSetSO` → sprite (pad) or label (keyboard)

Nothing is guessed and nothing is per-vessel, which is what makes a wrong label structurally
impossible. A passive ability (`FullSpeedStraightAction`) has no button and draws nothing; several
pad buttons have no keyboard equivalent and draw nothing there. **Blank is the honest state** — a
pad glyph shown to a keyboard player is misinformation, and that is precisely what the old
per-vessel roots did.

Pad families deliberately share one sprite. The only working hint set in the project — the
Squirrel's — already used the same `L1`/`R1` art for Xbox and PlayStation, and the one HUD that
tried to differ authored a set that did not correspond (Xbox `A`/`B`/`R1`/`R2` against PS
`circle`/`triangle`/`L2`/`square`). Device *detection* still comes from
`InputDeviceIconSetSwitcher`, which `VesselHUDController` now **ensures** the same way it ensures
the lockup — three HUDs never had one, which is exactly why their glyphs were never lit, never
device-matched and never placed.

What the fleet resolves to today, derived from the shipped assets:

| vessel | Charge | Mass | Space | Time |
|---|---|---|---|---|
| Squirrel | — | `L1` / LSHIFT | — | `R1` / RSHIFT |
| Sparrow | `L1` / LSHIFT | `A` | `R1` / RSHIFT | `B` |
| Scarab | — | `A` | — | `R1` / RSHIFT |
| Dolphin | `R1` / RSHIFT | — | — | `L1` / LSHIFT |
| Rhino | — | — | — | — |

*(A dash is a passive ability. The face-button entries are blank on keyboard because
`InputHintBindingMap` authors no keyboard equivalent for `Button1/2/3` — the open item there is the
keyboard map, not the lockup.)*

**The lockup owns the chip's SIZE, not just its position.** The device sets author their glyphs at
wildly different sizes — measured off `Squirrel.prefab`, the pad strips are 269 × 11 px with glyphs
spanning 50 × 50, the PC strip is 366 × 22 with glyphs at 106 × 22. Centring all of them on one 24px
socket therefore gave them *different clearances*: the pad glyph overhung the card by 7px while the
keyboard one sat 7px clear. Switching pad → keyboard → pad looked like the label had moved; it never
moved, the two sets were never the same size.

So `PlaceOnAbilityIcon` takes a **size** as well as a target. With one supplied it collapses the
anchors to a point and states the size outright (plus `preserveAspect` on the image), so every set
renders identically; with none it keeps the old span-preserving behaviour for a HUD with no lockup.

**Supplying the size is safe where reading it is not.** The authored glyphs are pure stretch rects
with `sizeDelta` zero, so their size comes entirely from their anchor span — collapsing the anchors
and re-supplying the size from `rect.size` renders them at ZERO whenever that read happens before a
layout pass or while the set root is inactive, which is every hint on vessel spawn. The size comes
from the chip socket's own `sizeDelta`, which is point-anchored and needs no layout at all.

**Reparenting the hints into the socket was tried and reverted.** It made the position structurally
undriftable, which was appealing — but it took the glyphs out from under the icon-set roots the
switcher activates, and switching device sets stopped working. *A component that shows and hides
things by toggling a parent owns that parenting; anything that re-homes its children has to take
over the showing and hiding too, and that trade was not worth it here.*

*The general trap: two things that look interchangeable at one size are only interchangeable at that
size. Anything centred on a fixed socket inherits the content's height as a variable.*

**A meter is regularly authored on the WRONG card, and the build is ordered around that.** The
Squirrel's boost fill sits under its *skimming* button and the Scarab's ball-energy ring under its
*throttle* button — both inherited layouts. Binding fixes it: the gauge is adopted onto the card its
ability actually owns. That is why `Build` runs three passes — place every host, then adopt every
gauge, then retire chrome. Doing it per-element would deactivate a meter a later element was about
to claim, silently, and only on the vessels whose authoring had drifted.

| vessel | slot | meter |
|---|---|---|
| Squirrel | **Charge** (Skimming) | `boostFill` — the skim-energy meter. It sits under the skimming button and belongs on the skimming card; a first pass put it on Time, which is the Boost Ring |
| Sparrow | Time (Afterburner) | `rollChargeIndicator` — the strafing-roll pip, already on the right card |
| Scarab | Space (Ball Forge) | `energyRing` — authored under the throttle button, re-homed |

## The cooldown — one recharge readout for the fleet

`VesselHUDView.SetAbilityCooldown(element, remaining01)` — 1 the instant the ability fires, 0 when
it is ready. The lockup draws a **radial veil swept over the ability plate**, clipped to the
trapezoid, ending in a one-shot flash the moment the ability comes back.

**Radial where the gauge is linear, and OVER the icon where the gauge is behind it.** Two motions
that cannot be mistaken for each other is what lets one card carry both — which several vessels
need, since an ability can bank a resource *and* have a recharge. A cooldown drawn as another rising
fill would read as the meter running backwards.

**A value, not an `Image` binding.** The gauge is bound because a vessel already authored a meter
worth preserving; a cooldown has no such artwork, so the lockup owns the whole presentation and the
vessel supplies one float. The overlay is built **lazily on first use**, so a card whose ability
never recharges costs no object, no mask and no draw calls.

It is the one piece of the lockup that lives **outside the card**: it has to darken the icon as well
as the plate, and the icon is a later sibling of the card, so a child of the card could only ever
draw behind it. The clip is parented to the host and pushed to the end of the sibling list. The
sweep is sized to the plate's **diagonal**, not its width — a disc inscribed in the rect would leave
the corners permanently lit.

The Squirrel's Boost Ring is the first user, and it **replaced a per-vessel reload animation**: the
icon sank to a seat and rose back as it loaded, breathed on a looping yoyo, wiped a radial fill on
*itself*, and slammed home with a colour flash. Four channels saying one thing, all on the icon, on
one hull. The signature stayed (`SetTubeCooldownReady` still takes the same value from the same
controller); the presentation left, along with six tuning fields and three tweens.

## The press state — the card lights, not a circle behind it

An ability firing used to switch on a per-vessel `highlights` image: a circular glow behind the
icon, a different one per hull. The **ability plate** carries it now — not the whole totem: an
upgrade is a change to the ability-plus-element pair and lights both plates, a press is the ability
firing and lights the one you pressed. Two states lighting the same area would be one signal. It is
driven (`VesselHUDView.SetAbilityPressed` →
`AbilityLockupView`), resolved from the input through the vessel's **own** ability map, so a hull
that rebinds an ability to another control needs no HUD change. It is **held** while the control is
down and **decays** on release — nothing pops out of existence.

The legacy `highlights` list is still written, for a HUD the lockup could not claim. On a lockup
vessel those images are retired chrome, so the write is a deliberate no-op rather than a second,
divergent press glow.

## The row's frame — the HUD root IS the screen

The row anchors to `(1, 0)` of the HUD root with the style's margins, which assumes that root is the
screen. **It was not.** Measured off the prefabs: `RhinoHUDVariant`, `ScarabHUDVariant` and
`SparrowHUDVariant` stretch their root to the full canvas, while `DolphinHUDVariant` and
`VesselHUDPrefab` — and therefore the Squirrel, Serpent and Manta — point-anchor theirs at the
**centre at 100 × 100**. So `(1, 0)` resolved to the screen's corner on one group and to a point
50px from the canvas centre on the other: the margins meant two different things and the vessels
visibly disagreed about where the row sits.

`NormaliseHudRoot` stretches the root to fill its parent and flattens any per-vessel HUD scale. It
is safe precisely because of the sweep below: by the time the lockup is done, everything it owns has
moved into the row and everything else at root level has been retired, so nothing is left whose own
anchors could be disturbed. *A container's rect is part of the contract for everything anchored
inside it — standardising the contents while leaving the frame per-vessel standardises nothing.*

## Retiring the old UI

`RetireLegacyChrome` works per ability **host**, so it only ever reached what sat *around* an icon.
Everything else a vessel drew before the lockup kept drawing beside the new row. The worst case was
the **device-glyph roots**: `SparrowHUDVariant`, `DolphinHUDVariant` and `ScarabHUDVariant` each
carry an `XBOX_Icon_Root` and a `PS_Icon_Root` with **no `InputDeviceIconSetSwitcher` to drive
them** (they are standalone prefabs, not variants of `VesselHUDPrefab`, which is where the switcher
lives). Those glyphs are never lit, never switched for the player's actual device, and never
placed — so when the lockup moved the row, they stayed where the old row used to be.

So the lockup also retires **root-level content that no component on this HUD still references.** By
the time it runs, everything the lockup owns has been moved into the row, so a root-level child that
still draws is either something the vessel actively drives or something nothing drives — and which
one is a *reference* question, asked rather than guessed. Sources are the components on the HUD
root — and only those (see the switcher note below).

Three guards make it safe:

- **Only children that DRAW.** A subtree with no `Graphic` is logic, not UI; switching it off would
  stop behaviour rather than hide a picture.
- **Sources are limited to the HUD root.** A component sitting *inside* a leftover branch would
  reference its own children and spare the branch it belongs to — and a component that *drew* the
  leftover would spare it outright, which is what the switcher did.
- **A reference from `highlights` does not count.** That is the legacy per-vessel press glow the
  card superseded, and the Rhino's entry points inside its old boost container — honouring it would
  spare exactly the chrome the row replaced. *A reference from something the lockup retired is not
  evidence that anything still uses it.*

What that clears, measured from the assets: Sparrow and Scarab lose `Boost Button/display`,
`Ammo Count` (nothing writes to it — ammo is shown by sprite-swapping the missile icon),
the emptied `ActionIconHolder` and both glyph roots; the Dolphin loses a stray `Image` and both
glyph roots. The auditor lists the candidates per vessel.

**The Rhino does NOT lose `BoostContainer`, and that correction matters more than the row it fixes.**
This doc claimed it did, read off `RhinoHUDVariant.prefab` — where the view wires only `highlights`
and `BoostContainer` therefore looks unreferenced. **`Rhino.prefab`'s instance says otherwise.** It
overrides `RhinoVesselHUDView` with six live readouts — `lineIcon`, `debuffIcon`, `crystalIcon`,
`debuffTimerText`, `skimmerSizeIcon`, `slowedCountText` — and adds three root-level branches the HUD
asset does not contain at all (`LaserTargeting`, `Crystal`, `ForceField`). Four of the Rhino's six
root branches are therefore genuinely driven (verified: `RhinoVesselHUDView` writes every one of
those fields), and the sweep spares them exactly as its third guard promises.

So the Rhino keeps a live status cluster beside its four locked cards — and that cluster is anchored
at `(0.93..0.98, 0.04..0.13)`, which is the bottom-right corner the lockup row now occupies. It
reads as "the old UI is back" because it is *in the row's seat*, not because anything failed to
retire.

*A HUD prefab asset does not tell you what a vessel wires.* The vessel's prefab instance can add
GameObjects the asset has never heard of and populate fields the asset leaves empty — so an
assertion about what a HUD contains has to be read from the **vessel**, not the HUD. Same family as
the Scarab's name-override misread: both were confident claims about assets, derived from the wrong
file.

### The switcher's own reference was the last thing propping the old glyphs up

The sweep originally consulted the icon-set switcher too, so that a HUD with a live switcher kept
its glyph roots. That spared exactly the wrong thing. Once the card **draws** its own chip, a second
set of glyphs is a competing display no matter who can still toggle it — and on the Squirrel,
Serpent and Manta (the `VesselHUDPrefab` variants, the only prefab that ever carried an authored
switcher) that reference was the *only* thing keeping `XBOX_Icon_Root` and `PS_Icon_Root` alive.
Worse, `ApplySet` re-activated them on every device change, so switching the branch off could not
have held anyway.

So `InputDeviceIconSetSwitcher` gave up the display entirely and is now a **pure detector** — it
answers "which device is the player holding?" and draws nothing (627 lines → 138). The three root
fields, the per-set hint visuals, the ability-placement pass, `SetHintActive` and `DriveHintVisuals`
are gone; `Current`, `IsKeyboard` and `OnSetChanged` remain. With no reference left to spare them,
those branches retire like any other leftover, and nothing can bring them back. Same rule as the
press glow, reached from the other side: *a reference from something the lockup superseded is not
evidence that anything still uses it* — **including a reference held by the superseded component
itself.**

### Three bugs that were invisible while the old display still drew

Deleting the display half surfaced three defects that had all been masked by authored glyphs:

1. **`OnSetChanged` was declared, subscribed, and never raised.** `ApplySet` set `Current` and
   toggled the roots but never invoked the event, so `VesselHUDController.HandleControlDeviceChanged`
   never fired and a card's chip could never follow a device change. The chips were frozen at
   whatever `SetControlDevice` was seeded with at `Initialize`. *An event nobody raises looks
   identical to an event nobody needs — the subscription compiles either way.*
2. **The keyboard set was unreachable on every vessel.** `KeyboardSet()` read
   `keyboardTextRoot || !showXboxSetWhenNoKeyboardRoot ? KeyboardText : Xbox`, and **no vessel wires
   a keyboard root** — so pressing a key reported *Xbox*, `IsKeyboard` was never true, and the
   LSHIFT/RSHIFT labels could never appear anywhere. That fallback was correct for an authored
   display that might not exist; it is wrong for a drawn one that always does. *A fallback that
   protected an authored display keeps firing after the display stops being authored.*
3. **`padGlyphHeld` and `heldColor` were authored and read by nothing.** The asset wires
   `L1 Active` / `R1 Active`, but the chip always drew the resting sprite in `restColor` — the
   held-state swap regressed when chips replaced hints. The chip now takes it off the card's
   existing press path (`SetPressed`), not a per-frame poll: **the card lights the ability, the chip
   wears the button's own held art, and they are the same press.** A one-shot `PlayPressFlash` is
   explicitly *not* a hold, or the control would read as held for good.

Together those are why "pad → keyboard → pad" could not reach the keyboard: (1) meant no update
ever arrived and (2) meant the answer would have been wrong if one had.

## Locked slots — the row is always four cards

`AbilityDisplayOrder` is four elements, so the row is four cards, always. A slot the vessel binds no
icon for renders **locked**: both plates quieter, a hairline mark where the icon would be, no gauge
track, no chip. Deliberately **not a padlock** — the ability is not locked to the *player*, it does not exist
yet. This is what puts the Rhino (one named ability, three open design slots) on the fleet's UI
today instead of leaving it on the old one until design lands, and its element flowers dock into the
locked cards exactly as they would into live ones.

## Rollout + enforcement (all vessels)

`VesselHUDController.Initialize` — the one method every vessel HUD routes through, on every spawn
path — calls `VesselHUDView.EnsureAbilityLockup()`, which adds and builds the lockup on **every** HUD —
including one that binds no icons at all, which is what gets the Rhino its locked row. So the style
is not opt-in and no prefab has to be edited to adopt it; a NEW vessel inherits it before it has a
single icon. It is added rather than warned about
because the lockup is pure composition over icons that are already authored — there is no
per-vessel art or wiring for a human to supply.

| vessel | row | lockup |
|---|---|---|
| Dolphin | 4/4 | ✅ (component also authored on the prefab — explicit, and equivalent) |
| Scarab | 4/4 | ✅ ensured at runtime; `energyRing` re-homed onto the Space card as its gauge |
| Sparrow | 4/4 | ✅ ensured at runtime; `rollChargeIndicator` becomes the Time card's gauge |
| Squirrel | 4/4 | ✅ ensured at runtime; AUTHORED flowers re-homed, `boostFill` re-homed onto **Charge** as the skim-energy gauge, Boost Ring recharge on the standard cooldown |
| Manta · Rhino · Serpent | 0/4 | ✅ four LOCKED cards — the row exists, the flowers dock, the slots read as undesigned. Blocked on ability DESIGN, not on this style |
| Urchin | 0/4 | — no HUD prefab exists at all, so there is no view to ensure |

**Row ownership is why `EnsureAbilityLockup` runs BEFORE `view.Initialize`.** Per-vessel views
capture their icons' rest scales during Initialize, and those scales are only right once the lockup
has normalised each icon to the fleet's drawn size. Ensure first, initialize second.

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
| The generated plate | `Assets/_Scripts/UI/View/TrapezoidGraphic.cs` |
| Upgrade hook (shared) | `Assets/_Scripts/UI/View/VesselHUDView.cs` — `SetAbilityUpgraded` → `SetUpgraded` |
| Flower socket injection | `Assets/_Scripts/UI/View/ElementalBarsView.cs` — `TrySetPetalRoot` |
| Press state + chip binding (shared init) | `Assets/_Scripts/UI/Controller/VesselHUDController.cs` |
| Control chips land on the card | `Assets/_Scripts/UI/Elements/InputDeviceIconSetSwitcher.cs` — `BindHintsToAbilities` |
| Adoption (no duplicate row) | `Assets/_Scripts/Controller/Vessel/ElementalBarsController.cs` |
| Sprite (white + alpha, 9-sliced) | `Assets/_Graphics/Design Assests/HUD UI/AbilityLockup/LockupBloom.png` |
| Dolphin wiring | `Assets/_Prefabs/UI Elements/VesselHUD/DolphinHUDVariant.prefab` (one component on the root) |
| Gauge bindings | `{Squirrel,Sparrow,Scarab}HUDVariant.prefab` — one `gauge` field per bound slot |
| Fleet audit | `Assets/_Scripts/Editor/AbilityLockupAuditor.cs` |
| Edit-mode tests | `Assets/_Scripts/Tests/Editor/AbilityLockupStyleTests.cs` |

## Tuning knobs — `Assets/Resources/AbilityLockupStyle.asset`

| Knob | Shipped | Meaning |
|---|---|---|
| `plateWidth` | 104 | the plate's WIDE edge — the seam where the two trapezoids face each other |
| `trapezoidInset` | 9 | how far each side pulls in at a plate's NARROW edge. **0 makes both plates rectangles and the totem reads as a list.** Both plates read it mirrored |
| `cellGap` | 6 | gap between the two plates. This IS the divider — a real gap is what lets the plates be borderless |
| `abilityCellHeight` | 88 | lower plate, centred on the existing icon |
| `petalCellHeight` | 88 | upper plate, added ABOVE. **Keep it equal to the ability plate** — unequal plates read as a coffin; the hierarchy lives in the marks, not the plates |
| `slantEdgeThickness` / `slantEdgeWrap` / `slantEdgeAntialias` | 3 / 14 / 1 | the band on the sloped sides only, how far it wraps around each corner (the whole grade lives on that wrap), and the zero-alpha feather baked on both sides — the antialiasing. `thickness + antialias` must stay ≤ `trapezoidInset` |
| `petalFlowerSize` | 60 | element flower — authored EQUAL to `iconBoxSize`, because mirrored plates give both marks the same negative space. Guard band: `0.75 ≤ flower/icon ≤ 1.0` |
| `iconBoxSize` | 60 | the ONE drawn size for every vessel's icons; each icon's scale is derived from it. Multiplies the upgrade bump rather than replacing it |
| `cardPitch` | 116 | centre-to-centre card spacing — one number for the fleet. Authored as `plateWidth + 2 × cellGap`, so the space **between** totems is exactly twice the space **within** one |
| `rowMarginRight` / `rowMarginBottom` | 40 / 44 | where the row sits, from the screen's bottom-right corner. `rowMarginBottom` measures to the **ability plate**, and the chip hangs below it — the row's real bottom margin is `rowMarginBottom − chipGap − chipHeight` (14px) |
| `chipHeight` / `chipGap` | 24 / 6 | the control chip's socket below the card. `chipGap` is authored **equal to `cellGap`** — the chip is a third element in the same stack, so it clears the ability plate by the same distance the element plate does. `chipGap + chipHeight` must stay under `rowMarginBottom` or every label clips off the screen |
| `gaugeCellFraction` | 1 | how much of the ability cell the linear gauge fills |
| `bloomPadding` | 26 | how far the upgraded bloom reaches past the card |
| `plateColor` | `#060810` @0.86 | resting fill. Borderless — there is no resting outline, by design |
| `bloomColor` | `#F5F5FF` @0.30 | alpha carries it — in engine bloom clamps at max-channel 0.5, so glow is bought with lit AREA |
| `upgradedPlateColor` | `#1C1F2B` @0.92 | the plate's lift. Raised well clear of the resting fill because, with the rim retired, this and the bloom are the WHOLE upgrade signal |
| `lockedMarkThickness` | 2 | thickness of the locked slot's placeholder bar |
| `slantEdgeColor` / `upgradedSlantEdgeColor` | `#82879C` @0.85 / `#F5F5FF` | the slant edge at rest and upgraded |
| `cooldownVeilColor` | `#060810` @0.72 | the radial veil swept over a recharging ability. It sweeps **clockwise** — note that this needs `fillClockwise = false` on the Image, because the veil DEPLETES: the flag names the direction the wedge is drawn, and the edge the player watches is its far end travelling the other way |
| `cooldownReadyFlashColor` / `cooldownReadyFlashDuration` | `#F5F5FF` @0.5 / 0.32 | the beat the player is waiting for — must be louder than an ordinary press flash |
| `gaugeTrackColor` / `gaugeFillColor` | `#161822` @0.9 / `#3882FF` @0.55 | the meter. The fill must out-read its own track in luminance, or the gauge is invisible — asserted by both the auditor and the tests |
| `lockedPlateColor` / `lockedMarkColor` | `#060810` @0.55 / `#5C5F70` @0.55 | an undesigned slot. Must stay QUIETER than a live card, or the row advertises abilities that do not exist |
| `pressFlashColor` / `pressFlashDuration` | `#F5F5FF` @0.22 / 0.18 | the card's fire signal, held while the control is down and decayed on release |
| `upgradeTransitionDuration` | 0.2 | states travel; nothing pops |
| `unlockPunchScale` / `unlockPunchDuration` | 1.05 / 0.5 | one-shot ceremony on unlock only, never on re-lock |

`bloomSprite` is the last authored asset — white + alpha, tinted at runtime (the T7 sprite-kit rule),
9-sliced (border 48). `LockupPlate.png` and `LockupPlateRim.png` are **deleted**: the plates are
generated and the rim no longer exists. The gauge fill needs *a* sprite only because Unity's
`Image.Type.Filled` ignores `fillAmount` when the sprite is null; it uses a plain white box built
from `Texture2D.whiteTexture`, so it costs no asset and cannot drift from the style — and it must
stay plain, because the stencil is what shapes the fill and any silhouette in the sprite would punch
notches inside the trapezoid.

## In-editor verification

1. Open a scene with a Dolphin (`MinigameBends` or `MinigameRampage`) and enter play mode.
2. **Row.** Four totems in the lower right, charge → mass → space → time. Each is TWO trapezoids —
   the element flower in the upper one, the Dolphin's existing gauge (Echo Sight profile / crystal /
   jaws / boost ring) in the lower — meeting at their wide edges across a visible gap, with **no
   outline on either plate** and no hairline between them. The gauges must still animate exactly as
   before: the icons were not moved or restyled.
3. **Kerning.** Neither the icon nor the flower should touch the plate's corner sliver — even air
   on every side, and the icon still visibly larger than the flower. Retune with `iconBoxSize`
   / `petalFlowerSize`; nothing here needs a recompile.
4. **Console.** No `[ElementalBarsView] Created N petal(s) … at RUNTIME` warning and no
   `Auto-creating the '…' flower container` warning: the sockets are supplied, so both paths are
   skipped. Also confirm **no second flower row** appears at the fleet-standard position.
5. **Ladder.** Collect elemental crystals and watch a flower fill grey → white → blue → lime; take a
   danger-prism hit and watch it flash and shake down through fire. Same juice as before.
6. **Upgrade.** Drive an element to level 5 (crystals, or the comeback buff in a mode where you are
   behind). **Both** of that totem's plates should lift in fill and a soft bloom come up behind each
   over ~0.2s, with a small one-shot punch — and still no border. Drop below 4 and it should travel
   back, not snap. This is the read most worth judging: the rim used to carry it, so if the upgrade
   is hard to spot, raise `upgradedPlateColor` and `bloomColor` before adding anything back.
7. **Chips.** LT/RT glyphs sit **centred under their own card**, not floating near it — the hint
   lands on the card's `ControlChip` socket at zero offset, so it moves with the totem. Charge = RT,
   Time = LT on the Dolphin; Mass and Space are passive and correctly show none.
8. **Press.** Hold each bound control. The whole CARD lights and decays on release — and no circular
   glow appears anywhere behind an icon.
9. **Gauge (Squirrel / Sparrow / Scarab).** Fly a Squirrel and boost: the Time card fills from the
   bottom in a straight line, inside the icon's cell, over a dim track — no ring anywhere. Sparrow:
   the Time card wipes empty when a strafing roll is spent, refills on re-arm. Scarab: the Space
   card fills with ball energy and goes READY. Confirm each meter is on the card of the ability it
   reports on (boost on Boost Ring, ball energy on Ball Forge), not the one it was authored under.
10. **Rhino.** Fly a Rhino: four cards, Mass live (Trail Slabs) and the other three drawn locked —
    quieter plate, a short hairline where the icon would be, no gauge track — with the element
    flowers docked above all four. No old ability-icon UI left in the corner.
11. **Balance + band.** Both plates the same height AND both marks the same size, mirrored about
    the gap — no coffin, and neither plate looking under-filled. A white band down each sloped side,
    **solid the whole length including the corners**, wrapping a short way onto the top and bottom
    before dissolving; the plate's core stays transparent behind it. Look at the diagonals close up:
    they must be smooth, not stair-stepped. **No band across the middle of the top or bottom edge.**
12. **Cooldown (Squirrel Time).** Fire the Boost Ring: a dark veil sweeps **clockwise** off the
    card, over the icon, and clears with a bright flash when it comes back. If it unwinds
    anticlockwise, `fillClockwise` has been "corrected" back to true. The icon itself must NOT sink,
    rise, breathe, tint or wipe any more — if it does, the old animation is still wired.
13. **Two readouts on one row.** Squirrel Charge shows the linear skim-energy fill; Squirrel Time
    shows the radial cooldown. Confirm they read as different things at a glance, which is the whole
    reason the cooldown is radial.
14. **Vessel swap.** Swap to the Dolphin from another vessel in Menu_Main freestyle and confirm
    exactly one set of cards (Build is idempotent; cards are adopted by name).

## What this retired

Three things existed only because there was no card, and each was a second way to say what the card
now says once:

| retired | why |
|---|---|
| The upgrade **corner badge** (`showUpgradeBadge` + its six tuning fields, and `VesselHUDView`'s whole badge implementation) | The card's own state carries the upgrade. The badge was a petal pinned to an icon corner saying the same thing, and it was already switched off on the Dolphin. |
| The upgrade **icon tint** (`tintIconOnUpgrade`, `upgradeHighlightColor`) | Colour on an ability icon is a GAUGE channel on most vessels, so the tint was unusable on exactly the vessels that needed a signal most. It could never be the fleet's answer. |
| The **decagon button plate** on Sparrow / Squirrel / Scarab | The card is the plate now. Left on, it sat behind the totem as a second, differently-shaped background. |
| The **hairline divider** between the two cells | The cells are two separate plates with a real gap. A line drawn to separate two halves of one shape is redundant once they are two shapes. |
| The **rim** (`rimSprite`, `hairlineColor`, `upgradedRimColor`) and `LockupPlate`/`LockupPlateRim` art | Borderless by request. The trapezoid silhouette is the frame; the upgrade moved onto the bloom plus a bigger plate lift. Both PNGs are deleted — nothing referenced them. The slant edge that arrived later is **not** the rim returning: it rides the sloped sides only and never closes. |
| The Squirrel's **bespoke Boost Ring reload animation** (`tubeCoolingColor`, `tubeReadyColor`, `tubeLoadPulseAmount`, `tubeLoadPulseDuration`, `tubeLoadDropOffset`, `tubeSlamFlashColor`, three tweens, `StartTubeLoadPulse`, `JuiceTubeSlamHome`) | Four channels on one icon on one hull, all saying "recharging". The fleet's standard cooldown says it once, the same way, everywhere. `SetTubeCooldownReady` kept its signature, so the controller did not change. |
| The **ring gauges** (Sparrow roll ring, Scarab energy ring, Squirrel chevron fill and its frame) | Four hulls, four shapes, one idea. The card's linear fill is the fleet's one gauge; the same Images are re-formed rather than replaced, so nothing is re-authored and no drive site changes. |
| The **circular press glow** behind each icon (`highlights`) | The card lights instead. A second shape drawn for a state the card already carries is exactly the divergence the totem removes. |
| The Squirrel's **undriven heat halo** (`overheatHighlight`) | A circular glow left over from the Sparrow's retired overheat mechanic — its driver was deleted in 2026, so it had never moved. Retired positionally with the rest of the host's chrome. |
| The switcher's **whole display half** — three icon-set roots, the per-set `HintVisual` list, the ability-placement pass, `SetHintActive`, `DriveHintVisuals`, `BindHintsToAbilities` (627 lines → 138) | The card draws the chip from one glyph set, so authored glyphs are a competing display and there is nothing left to place or light. `InputDeviceIconSetSwitcher` keeps only what nothing else can do: *which device is the player holding?* |

**Chrome is retired POSITIONALLY, not by name** — a direct child of the host that is not the icon
and not the card is chrome the card supersedes. Naming them would have missed the ones nobody
remembered. The one exception is a **touch target**: a UGUI button raycasts through its
`targetGraphic`, so that graphic is made invisible rather than disabled — an absent graphic does not
raycast, and disabling it would silently delete the on-screen ability control on every touch device.

`VesselAbilityRowWirer` no longer sets the two retired flags, and the stale serialized keys were
stripped from all four HUD prefabs. The remaining icon-level signal is the authored
`upgradedSprite` (optional, still unauthored fleet-wide) and the persistent scale bump.

## Latent bugs this closed

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
- **Urchin.** The only hull the lockup cannot reach: `UrchinHUDVariant.prefab` does not exist, so
  there is no `VesselHUDView` to ensure. Its map is complete — it needs a HUD prefab, not a style.
- **Gauges on the remaining hulls.** Dolphin, Manta, Rhino and Serpent bind no `gauge` yet. The
  Dolphin's four icons are already live gauges in their own right, so it may never want one; the
  other three are waiting on ability design anyway.
- ~~`Scarab.prefab` wires `SparrowHUDVariant`~~ — **not true, and now cleaned up.** It references
  `ScarabHUDVariant` (guid `4f3ce7d760a1e0c76f3bc8c6a6842a92`); what it carried was a stale
  prefab-instance **name override** setting `m_Name: SparrowHUDVariant`, a leftover from when the
  variant was duplicated. The override is removed. General trap: *a prefab-instance name override
  is not a prefab reference* — reading the name is how this was recorded backwards in `CLAUDE.md`
  for months, and it made `ScarabHUDVariant` look orphaned when it was the live asset.
- **Upgraded icon art** (`AbilityIconBinding.upgradedSprite`) is still unauthored fleet-wide; the
  lockup carries the upgrade without it, so it is now optional rather than missing.
