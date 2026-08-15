# PALETTE.md — the domain colour set (and how to change a prism tier safely)

Scope: `SO_ColorSet` — the per-domain colours that drive vessels, prisms, crystals,
AOE and UI. This doc exists because prism colours are **measurable**, not a matter of
taste alone: the shielded tier was hand-tuned twice by eye and both passes missed the
actual defect. Read §3 before touching any `*BlockColor` field.

## 1. Which asset is live

| | |
|---|---|
| Active asset | `Assets/_SO_Assets/Color Palettes/OriginalColorSetSO.asset` (guid `8d1c1bdcb636a424d9c54fbd1afc952f`) |
| Wired at | `ThemeManagerDataContainer.asset` → `ColorSet` |
| Consumer | `ThemeManager.GenerateDomainMaterialSet` (per-domain material set) + `GameToastAPI.ColorSet` (domain name tinting) |

`CosmicWaveColorSetSO`, `PastelColorSetSO` and `Default Color Palette` exist but are
**not wired to anything** — editing them changes nothing at runtime. The per-domain
`Shielded*SpreadFresnelMaterial.mat` files under `_Graphics/Materials/` are likewise
unreferenced legacy; every live shielded prism gets its material from `ThemeManager`.

## 2. What the prism colour fields actually mean

Traced through `BlockGraph.shadergraph` → `PrismSubGraph` → `DistanceSpreadAndColors`
→ `FresnelColors`:

| Colour set field | Shader property | Role on screen |
|---|---|---|
| `OutsideBlockColor` / `Shielded…` / `SuperShielded…` | `_DarkColor` | the prism's **base face** |
| `InsideBlockColor` / `Shielded…` / `SuperShielded…` | `_BrightColor` | the **fresnel rim** (blended toward the base by distance/`_Spread`) |

So *"Outside/Inside"* is a misnomer inherited from an older shader — they are
**base face** and **rim**. The separation between them is the entire reason a prism
reads as a solid object rather than a flat slab. **Contrast between the pair is the
thing you are tuning.** A prism whose base and rim are near-identical has no form.

Applies to `ShieldedBlockMaterial` and `TransparentShieldedBlockMaterial`, and hence
to the octahedron shield mesh (`PrismOctahedronShield`), which takes the same themed
material.

**The shielded tier carries gameplay meaning, not just style.** Shielded mass is
never food (`Docs/ECOSYSTEM.md §16` — `Prism.Consume` only sheds the shield), and
every flora/fauna **health prism** is shielded. So "is this prism shielded?" is a
question players and creatures both act on, and a shielded prism that reads as a flat
slab is a legibility bug with teeth, not a cosmetic nit. Changing this tier is a
visual change only — no collider, spawn, or consumption behaviour is involved — so it
does not touch the ecology invariants or the collider budget.

### 2.1 One composition, two consumers — a prism and its debris (2026-08-13)

**`SO_ColorSet.GetPrismKindColors(colorSet, PrismKind, out rim, out base)` is the single
definition of what a prism of a given tier is painted with.** Two consumers read it and
must keep reading it:

| consumer | what it paints |
|---|---|
| `ThemeManager.GenerateDomainMaterialSet` → `PaintPrismTier` | the LIVE block materials (opaque + transparent) for all four tiers |
| `PrismFactory.TryGetTeamColors` / `ConfigureForTeam` | the DEATH visuals — explosion debris and consumption suction |

It exists because the two disagreed. The debris palette was resolved from the dying
prism's **domain alone, at the plain tier**, so a danger prism — a frosty shielded base
under the hot domain-independent danger rim — shattered into ordinary domain-coloured
debris and read as a plain prism dying. Shielded and super-shielded mass had the same
defect (visible whenever a devastating hit explodes shielded mass rather than shedding
its shield). The dying prism's tier now travels on the event
(`PrismEventData.Kind`, stamped in `Prism.Explode` / `Prism.Implode` from
`PrismKinds.Of` **before** the destruction pass), and both routes — the batched
pure-entity debris and the pooled fallback — tint from it.

This costs nothing at runtime: debris colour is already a **per-entity** override
(`PrismBrightColorOverride` / `PrismDarkColorOverride`) inside the one
prototype-instantiate batch, so a mixed-tier burst is still one batch and one draw.
The per-domain `SO_MaterialSet.ExplodingBlockMaterial` copies are **not** what debris
draws with (`PrismDebris` reads mesh + material off the pool prefab and overrides the
colours per entity) — do not "fix" a debris colour there.

**Do not re-inline a tier's colour pair** at either consumer. A prism and its own debris
disagreeing is the exact failure this centralisation removes.

Danger additionally detonates **harder** than plain mass — `PrismExplosion.DetonationGain`,
authored as `dangerDetonationMultiplier` on `PrismExplosion.prefab` (1.6). That is a
DYNAMICS knob, not a palette one: it scales the debris speed, the shatter rate and the
clamp band as one quantity (they are one quantity on this contract — see CLAUDE.md ▸ "AOE
blast impulse"). Set it to 1 for palette-only behaviour.

### 2.2 Crystal colour means COLLECTABILITY, and it lives on a property block (2026-08-15)

A crystal's **element is its shape**; its **colour is who may collect it**. One resolver,
`Crystal.ApplyColorSetTint`, reads the live `SO_ColorSet` and paints
`_BrightCrystalColor` / `_DullCrystalColor` per renderer through a `MaterialPropertyBlock`:

| State | Source pair | Reads as |
|---|---|---|
| domain-owned (Jade/Ruby/Gold) | that domain's `BrightCrystalColor` / `DullCrystalColor` | only that domain collects |
| **embedded lifeform heart** (`Crystal.IsEmbedded`) | `BlueColors.BrightCrystalColor` / `DullCrystalColor` | **blue** — nobody collects it, it is alive |
| free pickup (drop / omni / cell) | `EnvironmentColors.BrightCTA` / `DarkCTA` | **lime** — anyone collects it |

So a lifeform's heart is blue while it lives and flips to lime the moment `ActivateCrystal`
drops it. The flip is the pickup affordance, and it is applied explicitly at the tail of
`ActivateCrystal` rather than left to `Start` (which only fires because a heart's `Crystal`
component is authored **disabled**) or to the material lerp's tail (skipped outright when a
model has no target material).

**The authored material is the fallback, and it does not agree with this rule.** Four of
the crystal prefabs sit on materials whose own colours are lime — `ChargeCrystalMaterial` is
literally `BrightCTA` `(0.59, 0.92, 0.16)`, and Time's `FringeMaterial`/`InverseFringeMaterial`
carry a lime dull face — while Mass and Space author the *Blue* material on the renderer. So
whenever the tint is lost, half the ecosystem's hearts (the 21 Charge and 21 Time species) read
as the lime free-pickup while their lifeform is still alive, and the other half look correct
**by accident of which material the prefab happened to author**. Do not "fix" a crystal's
colour by editing its material — the material is what you see when the tint has failed.

**A property block belongs to the RENDERER, not to the component that writes it.** This is the
trap that made the above ship: `FadeIn` drives `_opacity` through the *same* block and used to
write its own instance every frame, then end the bloom with `MaterialPropertyBlock.Clear()` —
which cannot drop a single key, so it took the tint with it a few seconds after every crystal
spawned. `FadeIn` now merges (`GetPropertyBlock` before each write, but only when a co-owner has
registered, so uninvolved renderers keep the cheap path — one prefab can carry thousands of
these blooming at once) and raises `FadeCompleted` after the clear; `Crystal` subscribes in
`Awake` and re-asserts the tint there.
**Any new writer to a shared renderer's block must do the same** — merge on write, and give
co-owners a way back in after a clear.

## 3. The colour-space rule (this is the trap)

The project is **Linear** (`ProjectSettings/ProjectSettings.asset: m_ActiveColorSpace: 1`)
and these fields are `[ColorUsage(true, true)]` (HDR). Therefore the floats serialised
in the `.asset` are **linear intensities**, and:

- Rec.709 luminance (`0.2126 R + 0.7152 G + 0.0722 B`) and CIELAB apply **directly** —
  no sRGB de-gamma step. Do not linearise them again.
- Channel values **above 1.0 are legitimate** (they bloom). Do not "fix" them by clamping.
- **Scaling a pair by a constant changes brightness but NOT contrast.** Halving both the
  base and the rim leaves the ratio between them untouched. This is exactly how the
  first fix attempt (`a8b71b4b`, "shielded color tweaks") failed to fix anything: Gold's
  pair was halved, its ΔL\* stayed at 9.8, and the prisms stayed flat.

Judge prism pairs in **CIELAB**: `L*` for brightness, `ΔL*` between base and rim for
contrast, `C*` for how saturated/harsh the colour is. RGB and HSV both mislead here —
HSV "saturation" is not perceptual chroma, and equal `V` across hues is not equal
brightness.

## 4. The shielded tier contract (as shipped)

Jade was the healthy domain and is the model. Ruby and Gold carry Jade's exact
**lightness and contrast** and their own **hue**; chroma is set per domain by a
**screen-saturation target**, not by a single absolute allowance (see §4.1 — this is
the part that was wrong twice):

```
shielded base :  L* = 54.31      hue = domain's own unshielded base hue
shielded rim  :  L* = 83.65      hue = domain's own unshielded rim hue
                 dL* = 29.34  (identical relative luminance across all domains)

chroma        :  chosen so SCREEN SATURATION lands in the Jade..Ruby band
                 base 65-82%,  rim 50-59%      (see §4.1 for why, and the metric)
```

Jade itself keeps its authored `C*` (32.10 / 37.27) — it is the reference, unmodified.

Measured state (linear HDR → CIELAB; **sat** = screen saturation, §4.1):

| tier | domain | ΔL\* base→rim | C\* base / rim | sat base / rim |
|---|---|---|---|---|
| unshielded — original | Jade / Ruby / Gold | 32.2 / 27.1 / **47.6** | 33.6-48.1 / 77.2-138.8 / 51.9-76.5 | 100-100 / 98-100 / 100-**91** |
| **unshielded — current** | Jade / Ruby / Gold | 32.2 / 27.1 / **32.0** | 33.6-48.1 / 77.2-138.8 / 51.9-76.5 | 100-100 / 98-100 / 100-**98** |
| shielded — original | Jade / Ruby / Gold | 29.3 / **10.1** / **9.8** | 32.1-37.3 / **105.8-111.4** / 45.9-43.3 | — |
| shielded — after the ΔL\* pass | Jade / Ruby / Gold | 29.3 / 29.3 / 29.3 | 32.1-37.3 / 43.3-50.3 / **43.3-50.3** | 82-59 / 65-50 / **90-82** |
| **shielded — current** | Jade / Ruby / Gold | 29.3 / 29.3 / 29.3 | 32.1-37.3 / 43.3-50.3 / **28.5-23.6** | 82-59 / 65-50 / **74-55** |
| supershielded *(untouched)* | Jade / Ruby / Gold | 55.2 / 42.2 / 41.5 | 29.0-18.5 / 70.9-43.0 / 68.2-40.1 | — |
| danger — original | *(all domains)* | **−3.8** | base 32.1 / 43.3 / 28.5, rim **77.9** | rim 97 |
| **danger — current** | *(all domains)* | **+9.3** | base 32.1 / 43.3 / 28.5, rim **116.5** | rim 99 |

Four defects have been fixed across three passes. The first pass fixed **contrast
collapse** (Ruby and Gold at ~⅓ of Jade's ΔL\*) and **chroma runaway on Ruby** (>3×
Jade's — a near-zero-green violet that blooms harshly). The second fixed **Gold failing to
shift to pastel** (§4.1). The third fixed the two structural inconsistencies §4.2 names:
Gold's **anomalous unshielded rim** and the danger tier's **inverted rim**. SuperShielded
was measured and left alone: it is already healthy on all three domains.

### 4.0 The invariant that outranks every per-tier contract

> **In every tier, on every domain, the rim is brighter than the base.**

This is what makes a prism read as a solid object rather than a flat slab (§2), and it
held on nine of the twelve tier×domain pairs by accident rather than by rule — so the
three that broke it were each rationalised individually instead of being recognised as one
defect. Check it first, before any per-tier numbers:

| tier | Jade | Ruby | Gold |
|---|---|---|---|
| unshielded | +32.16 | +27.13 | +31.99 |
| shielded | +29.34 | +29.34 | +29.34 |
| supershielded | +55.16 | +42.19 | +41.47 |
| danger | +9.30 | +9.30 | +9.30 |

A second, weaker corollary: **a shielded prism out-brightens its own plain prism** on both
base and rim, so "shielded" always reads as the brighter state (Jade +10.23/+7.41, Ruby
+26.90/+29.11, Gold +10.06/+7.41).

### 4.1 Absolute C\* is NOT comparable across hues — measure screen saturation

The first pass gave Ruby and Gold the *same* absolute chroma (`Jade C* × 1.35`) on the
assumption that equal `C*` looks equally saturated. **It does not.** Gold came out still
reading as plain gold while Ruby read as a pastel pink, from identical `L*` and `C*`
numbers.

The reason is visible in the linear channels. A shielded prism reads as a pastel when its
**weakest channel is still well lit** — that is what "washed out" means on a screen. At a
cool or magenta hue the weak channel is red or green and `C* ≈ 43` already lifts it well
clear of zero; at a *warm* hue the weak channel is **blue**, and the same `C* ≈ 43` left
Gold's blue at **0.036** (base) and **0.179** (rim) against Ruby's 0.345 / 0.417. Gold was
not a pastel by any measure that reaches the screen — it was a saturated warm colour with
the right `L*`.

So judge this tier by **screen saturation**, defined on the linear channels *after
clipping to the display*:

```
sat = 1 - min(R,G,B) / max(R,G,B)          # clip channels to [0,1] first
```

Clipping is load-bearing and is why raw `C*` (and gamut-fraction) both mislead here: the
rims are HDR and exceed 1.0, and for Jade/Ruby the over-range channel is the *dominant*
one, so clipping pulls them toward white and **desaturates** them. Gold's over-range
channel is red while its blue stays dark, so clipping does not whiten it at all.

Measured plain → shielded journey, which is the thing a player actually perceives:

| domain | ΔE00 base / rim | desaturation base / rim |
|---|---|---|
| Jade | 11.5 / 16.1 | +18% / +41% |
| Ruby | 25.6 / 28.3 | +32% / +50% |
| Gold *(before)* | 10.4 / **8.6** | **+10% / +9%** |
| **Gold *(current)*** | **13.0 / 17.0** | **+26% / +43%** |

Gold's chroma is therefore `28.54 / 23.64` — *below* Jade's, not 1.35× above it — chosen
to land its screen saturation on the **midpoint of Jade and Ruby** (base 74%, rim 55%).
Hue and `L*` are untouched, so the §4 contract is fully intact.

**Hue was considered as a lever and rejected.** Gold's shielded base at `L* 54.31` is a
tan/khaki at *every* hue in the gold family (82.9° → 62° moves it khaki → clay, nothing
more), so warming the hue does not buy a rescue and would cost the "hue = domain's own
unshielded hue" rule. The pastel read lives in the **rim** (`#EDCBA6`, a pale sand — the
warm-hue counterpart of Jade's `#ABD2FF` and Ruby's `#F4BBFF`), exactly as it does for the
other two domains, whose bases are likewise muted mid-tones (`#5386B9`, `#9C71B7`).

### The danger tier borrows the shielded base

A danger prism is painted from a **fourth** pair that has no fields of its own — it is
composed in `SO_ColorSet.GetPrismKindColors` (§2.1) out of two existing colours, and both
the live material and the death debris read it from there:

| | |
|---|---|
| base face (`_DarkColor`) | the domain's `ShieldedOutsideBlockColor` |
| fresnel rim (`_BrightColor`) | the shared `EnvironmentColors.Danger` (one colour, all domains) |

The rim is what says *dangerous* (it is domain-independent by design, since danger is not
safe to its own domain — see CLAUDE.md); the base is what says *whose*. It takes the
**shielded** base rather than the plain one so a danger prism reads as its own frostier
tier of the domain at a glance, instead of as ordinary mass wearing a hot rim.

The danger rim is `L* = 63.61, C* = 116.45, h = 38.8` — linear `(1.4979, 0.0058, 0.0069)`.
Because every shielded base sits at `L* 54.31`, one shared rim gives **identical ΔL\* on
all three domains**, the same property the shielded tier has:

| domain | shielded base L\* | ΔL\* base→rim *(original)* | ΔL\* *(current)* | ΔE00 *(orig → current)* |
|---|---|---|---|---|
| Jade | 54.31 | **−3.80** | **+9.30** | 45.7 → 49.8 |
| Ruby | 54.31 | **−3.81** | **+9.30** | 36.1 → 40.3 |
| Gold | 54.31 | **−3.80** | **+9.30** | 28.2 → 34.2 |

**This tier used to be the only inverted one in the palette** (§4.0) — a dark rim on a
lighter base, and with a peak channel of 0.72 it also had the *dimmest* rim of any tier
(everything else runs 0.84–1.50). It was rationalised as "separates on hue and chroma
instead of lightness", which was true, and which quietly made the tier's legibility depend
entirely on how far a domain's hue sits from red. That is fine for Jade (132° away) and
workable for Ruby (84°), and it fails on **Gold, only 44° away** — the domain that
therefore had nothing much to separate on in *either* dimension.

The fix is the one this section already prescribed: re-place the rim's `L*` against the
54.31 base, upward. **Raising `L*` alone would have made it salmon** — a bright red must
add green and blue, and it stops reading as danger. The way out is that this is an *HDR*
colour: pushing chroma to the positive-channel limit lets the red climb in luminance by
driving `R` above 1.0 while `G`/`B` stay near zero, so it gets brighter *and* more
saturated at once (screen saturation 97% → **99%**, `#DD3D27` → `#FF1214`). The rim now
lands exactly on the palette's authored HDR ceiling of **1.4980**, the value already used
by the Ruby unshielded rim and all three supershielded rims — so danger is as hot as
anything in the game and no hotter.

**Gold still has the least separation of the three (ΔE00 34.2 against Jade's 49.8), and
that part is irreducible.** With one shared danger rim, hue distance is fixed by where
each domain sits on the wheel. What changed is that hue distance is no longer *load
bearing*: every domain now gets the same +9.30 lightness separation and a chroma gap of
73–88, so form reads on Gold without depending on its hue. If Gold's danger prisms ever
read flat again, the remaining lever is to give the danger tier **its own base fields per
domain** (it has none today — see the table above) rather than to push the shared rim
further, which would start distorting Jade and Ruby to solve a Gold problem.

### Why gold's chroma is below Jade's, not 1.35× above it

An earlier revision of this doc set Gold's chroma to `Jade × 1.35` and justified it as
"keeps Gold gold" — the rejected alternative being Jade-parity chroma, which "came out
khaki". Both halves of that reasoning were sound *as far as they went* and still produced
the wrong answer, because both were argued in absolute `C*`, which is not comparable across
hues (§4.1). Keeping Gold gold **was the defect**: the shielded tier's job is to read as a
distinct frosted state, and "still gold" is precisely the failure players reported.

The khaki observation was also real but mislocated. Gold's shielded *base* is a tan/khaki
mid-tone at any chroma low enough to be a pastel — and so are the other two domains' bases
(`#5386B9`, `#9C71B7` are muted, not pastel). Judging the tier by its base alone is what
made khaki look like a dealbreaker. The pastel lives in the **rim**.

The current rule has no free taste knob: chroma is solved from the screen-saturation
target in §4.1. To make the whole family more or less frosted, move that **band** for all
three domains, rather than reintroducing a per-domain multiplier.

### 4.2 Gold's unshielded rim (resolved)

Gold's *unshielded* rim used to sit at `L* 91.80` against Jade's 76.24 and Ruby's 54.54 —
above every other rim in the palette, and above Gold's own **shielded** rim at 83.65. That
inverted the §4.0 corollary: a shielded Gold prism was *dimmer* at the rim than the plain
prism it was supposed to read as a brighter, frostier version of.

The cause was authoring to a peak channel of ~1.5 without checking the resulting `L*`. At
a *blue* hue 1.5 buys little luminance (Ruby's rim lands at `L* 54.54`); at a **warm** hue
it buys a lot, because red and green carry almost all of it — so the same authored peak
put Gold 37 points of lightness above Ruby. This is the same warm-hue asymmetry as §4.1,
reached from the other direction.

Gold's unshielded rim is now `L* 76.24` — Jade's exact rim lightness — with **chroma and
hue untouched** (`C* 76.54`, `h 73.8`), giving linear `(0.9964, 0.4049, 0.0186)`, `#FFAB25`.
That lands ΔL\* at 31.99 against Jade's 32.16, restores 7.41 points of headroom under the
shielded rim (again matching Jade exactly), and drops the peak from 1.50 to 1.00, in family
with Jade's 1.14. Plain Gold prisms are correspondingly less blazing at the rim — the
intended change, since that heat was the defect.

## 5. Re-deriving after a change

The contract is reproducible from the asset alone; no play mode needed:

1. Parse **all** the `*BlockColor` values per domain plus `EnvironmentColors.Danger` out of
   the `.asset` — not just the tier you touched. Two of the three defects in §4 were only
   visible by comparing tiers against each other.
2. Convert linear RGB → CIELAB (§3 — no de-gamma).
3. **Check §4.0 first**: every tier×domain pair must have `ΔL*` base→rim **> 0**, and each
   domain's shielded pair must out-brighten its own plain pair. This is the cheapest check
   and it catches the whole class of defect that survived two passes.
4. Then check `ΔL*` base→rim per domain (shielded must be 29.34) and **screen saturation**
   per colour (§4.1 — clip to [0,1] first) against the §4 table. Checking `C*` alone will
   pass a colour that fails on screen; that is how the Gold defect survived a whole pass.
5. To re-place a domain: take Jade's shielded `L*` pair and the domain's own unshielded
   Lab **hue** for base and rim, then solve `C*` so screen saturation hits the §4.1 target
   (monotonic in `C*` — a bisection converges in a few dozen steps); convert LCh → Lab →
   linear RGB.
6. Assert no channel is negative before writing, and no peak exceeds the palette's HDR
   ceiling of **1.4980** without a stated reason.
7. Re-check the **danger** tier for that domain (it borrows the shielded base) and the
   plain → shielded `ΔE00` journey, so a chroma move can't quietly flatten either.

**Author lightness, never a peak channel.** Both §4.2 and the original danger rim were
authored by picking channel values that looked right in isolation; at a warm hue that
silently overshoots `L*`, and at a red hue it silently undershoots it. Set `L*` against the
other tiers first, then solve the channels.

## 6. In-editor verification (the human gate)

Machine validation covers structure and colorimetry; only a playtest covers *looks right*:

1. Pull, let Unity reimport `OriginalColorSetSO.asset` (if nothing changes visually,
   suspect a stale Library and Reimport the asset before suspecting the values).
2. Get shielded prisms on screen. Verified producers, easiest first:
   - **Any cell with lifeforms** (Menu_Main freestyle) — every flora/fauna **health
     prism** is shielded (`LifeForm.ActivateShield`, `HealthBlockTracker`), so the
     ecosystem is the densest sample of this tier in the game.
   - **The `SegmentSpawner` track** (HexRace / Skim Race) ships prisms with
     `IsShielded`, so the whole course is this tier.
   - **Astro League** (`AstroLeagueBall` shields prisms it touches), AOE block
     creation, and the skimmer overcharge effect.
2b. Get **danger** prisms on screen (§4 "The danger tier borrows the shielded base").
   Verified producers, easiest first:
   - **Ribcage** ("Peel the Cage") — its sparse cage traps are `PrismKind.Danger`,
     and the mode ships the same prism in all three domains.
   - **The worm colony** (Lifeform Matrix toy, Menu_Main freestyle) — its head/tail
     capital segments carry danger prisms (`WormSegmentFauna`).
   - **Dangerous flora** (`AssembledFlora`, `growthInfo.IsDangerous`) and the AOE danger
     hemisphere (`AOEDangerHemisphereBlocks`).
   Confirm the rim now reads as a **bright incandescent red glowing off a frostier body**
   (it was a *dark* red rim before — the only inverted tier in the palette, §4.0). Check
   **Gold** hardest: it has the least hue separation from the danger red, so it is the
   domain where a regression would surface first. Also confirm a gold danger prism is not
   confusable with a gold *plain* prism at speed — both now carry a hot rim, but they sit
   35° apart in hue and 40 apart in chroma (`#FF1214` against `#FFAB25`).
3. For **Ruby** and **Gold**, confirm: the prism's facets and silhouette read clearly
   (the base→rim separation is visible), and the shielded prism is obviously distinct
   from an unshielded prism of the same domain.
3b. For **Gold** specifically, the question §4.1 was solving is *"does it go pastel like
   the other two?"* — put a shielded gold prism beside a plain one and confirm the shift
   reads as **sand/cream**, the warm counterpart of Jade's mint and Ruby's pink. If it
   still reads as "gold, slightly lighter", the screen-saturation target needs to come
   down further; if it reads **chalky or dead**, it has come down too far.
4. Check bloom. Threshold is **0.2** (`GamePlay PostProcessing Profile`, 0.24 in the
   menu), so every rim in the palette clears it comfortably; the peaks are what set how
   hard each one glows:

   | tier | Jade | Ruby | Gold |
   |---|---|---|---|
   | unshielded | 1.14 | 1.50 | 1.00 |
   | shielded | 1.22 | 1.19 | 0.84 |
   | supershielded | 1.50 | 1.50 | 1.50 |
   | danger | 1.50 | 1.50 | 1.50 |

   Nothing exceeds the palette's authored ceiling of 1.4980. Gold's shielded peak is the
   lowest in the table because a warm hue reaches a given `L*` with smaller channel values
   than a blue one — expect it to glow a little more softly, which is inherent, not a
   defect. Danger should now be **among the hottest things on screen**; if it still looks
   dull, suspect a stale Library rather than the values.
5. Compare all three domains side by side — no domain should read hotter or flatter
   than the others. Then compare all four *tiers* within one domain, which is the check
   §4.0 exists for: plain → shielded → supershielded should step visibly brighter, and
   danger should be unmistakably its own thing rather than a dim variant of any of them.

## 7. Follow-ups

- The inactive palettes (`CosmicWaveColorSetSO`, `PastelColorSetSO`) still carry the old
  flat shielded values **and the old inverted danger rim**. They are dead assets today; if
  either is ever wired up, run §5 against it first.
- **The danger tier has no base fields of its own** — it borrows each domain's shielded
  base. That coupling is why Gold's danger separation (ΔE00 34.2) cannot be raised to
  Jade's (49.8) without moving the shared rim and distorting the other two domains. If the
  tier ever needs per-domain control, adding `DangerOutsideBlockColor` to `SO_ColorSet`
  and reading it in `GetPrismKindColors` (§2.1) is the clean way — one edit, and the live
  material and the death debris both follow. It is a structural change, not a tune.
- **The unshielded tier is still not equalised across domains** (ΔL\* 32.2 / 27.1 / 32.0;
  rim `L*` 76.2 / 54.5 / 76.2). §4.2 brought Gold into the band rather than imposing a
  contract, because Ruby's dark rim is load-bearing for its look. If that tier is ever
  given a contract like the shielded one, Ruby is the domain that will move.
- `Domains.Blue` (the neutral sentinel) was not measured or tuned; it is not a playable
  domain and its prisms are rarely seen.
- The `Outside`/`Inside` field names are misleading (§2). Renaming them is a broad,
  GUID-safe but wide-reaching refactor across `SO_ColorSet` + `ThemeManager` + every
  colour set asset — worth doing, not worth bundling with a palette tune.
