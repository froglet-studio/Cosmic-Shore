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
`PrismKinds.Of` **before** the destruction pass), and the batched pure-entity
debris path tints from it. Grow (Sparrow ReverseSuction) still uses pooled
`PrismImplosion.ConfigureForTeam` and is not a death tint.

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

### 2.2 The CTA lime — the crystal tier, and why bloom is bought with AREA (2026-08-15)

Crystal COLOUR signals **who may collect it**, never which element it is (element is shape):
domain crystals wear that domain's crystal pair, a living lifeform's heart wears the blue-white
neutral, and a **free pickup wears the lime CTA** (`EnvironmentColorSet.BrightCTA` / `DarkCTA`).
`Crystal.ApplyColorSetTint` drives it per-renderer through a `MaterialPropertyBlock`, and
`Crystal.FindColorPropertyNames` accepts either naming pair, so it reaches all five crystals
across six different shaders (audited 2026-08-15: `ShepardGraph` — omni **and** Mass —,
`ChargeCrystal.shader`, `CrystalGraph` — Space —, `InverseDynamicFresnelGraph` — Time, the one
that actually ships, via `_BrightColor`/`_DullColor`).

**Dull is the body; bright is only the rim.** Every crystal shader composes its colour as
`Blend(Base = Dull, Blend = Bright, Opacity = fresnel)` in **Overwrite** mode — i.e. a straight
`lerp(dull, bright, fresnel)` — and the fresnel is `(1 − N·V)⁴` (`FresnelPower4`). At that power
the rim is **2.5% of the silhouette** and the area-weighted mean fresnel is **0.067**, so
`DarkCTA` paints ~**93%** of the crystal. `BrightCTA` is a hairline. Tune the body, not the rim.
(`TimeCrystalGraph`/`DynamicFresnelGraph` swap the two roles — which is exactly why the elemental
dimming below is a **scalar** rather than a second authored colour pair: a scalar dims correctly
whichever role each colour plays, and cannot drift the hue out of the lime family.)

**Bloom is CLAMPED, so brightness above the clamp is a dead dial.** The gameplay and Commander
profiles override URP's Bloom `clamp` to **0.5** (URP's default is 65472), with `threshold 0.2`,
`knee 0.1`, `intensity 2.5`. URP clamps the bloom SOURCE before thresholding, so the per-pixel
bloom contribution rises with the max channel only up to 0.5 and is **flat above it**:

| max channel | 0.15 | 0.20 | 0.25 | 0.32 | 0.40 | 0.50 | 0.92 | 1.50 | 4.87 |
|---|---|---|---|---|---|---|---|---|---|
| % of bloom ceiling | 2% | 8% | 19% | 40% | 67% | **100%** | 100% | 100% | 100% |

Two consequences, both load-bearing:

1. **§3's "channels above 1.0 bloom" is FALSE in gameplay.** 56 of the 86 colours authored in
   `OriginalColorSetSO` exceed 0.5 (danger rim 1.498, AOE 4.0, supershielded 1.498) and are all
   flattened to the same bloom. Raising the clamp is therefore not a crystal change — it re-lights
   the entire palette — and is deliberately **not** done here. See §7.
2. **Within the clamp, extra bloom is bought with bright AREA, not intensity.** `DarkCTA` was
   `(0.18, 0.32, 0.05)` — max 0.32, only 40% of the ceiling over 93% of the crystal. It is now
   that same colour scaled by **×1.5625**, `(0.28125, 0.5, 0.078125)`, which lands the green
   channel exactly ON the clamp. Pure scalar ⇒ identical hue (§3), and no channel reaches 1.0, so
   nothing clips (tonemapping is **None**, `mode: 0` — channels above 1.0 clip hard and shift the
   lime toward yellow-white, so ≤1.0 is a real constraint here, not a nicety). `BrightCTA` is
   unchanged at `(0.59, 0.92, 0.16)`: at max 0.92 it was already saturating the clamp, so raising
   it would have bought exactly nothing.

**The omni is the hero; the four elementals are the same lime, dimmed.**
`EnvironmentColorSet.ElementalCrystalDimming` (**0.45**) scales the CTA pair for anything
`CrystalProperties.IsElemental`, leaving the omni at full strength. Note the omni and the Mass
crystal wear the *same four materials*, so this scalar is the only thing that distinguishes them.

The scalar is far more sensitive than it looks, because the bloom **threshold (0.2) sits inside
the elemental body's range** — so tune it against the measured curve, not by eye:

| dimming | body max | rim max | body % of ceiling | omni : elemental bloom |
|---|---|---|---|---|
| 0.60 | 0.300 | 0.552 | 33% | 2.6 : 1 |
| 0.55 | 0.275 | 0.506 | 26% | 3.3 : 1 |
| **0.45** | **0.225** | **0.414** | **13%** | **6.2 : 1** |
| 0.40 | 0.200 | 0.368 | 8% | 9.3 : 1 |
| 0.30 | 0.150 | 0.276 | 2% | 32.5 : 1 |

0.45 is chosen so the elementals keep a *live* body glow (body just above the threshold) and a
still-bright rim at 83% of the ceiling, while the omni reads ~6× brighter. Below ~0.40 the body
drops under the threshold entirely and the elementals become rim-only outlines — a legitimate
look, but a different one; decide it deliberately rather than drifting into it.

Note the raise to `DarkCTA` lifts the elementals too (they are scaled *from* the same pair), so
the two knobs are not independent — re-read this table after changing either.

**The tint was invisible until 2026-08-15.** `FadeIn` owned a private `MaterialPropertyBlock`,
pushed it wholesale (wiping the tint at the start of the fade) and `Clear()`ed it at the end
(wiping it permanently) — and every crystal model carries a `FadeIn`, the omni included via the
nested `TrucatedOctahedron.prefab`. So every crystal in the game settled back to its authored
material colour and **no crystal ever showed the CTA lime**: the omni read blue-white/green, Space
blue-white, Time HDR blue, and only Charge was lime (its shader's authored defaults happen to *be*
the CTA pair). `FadeIn` now composes — `GetPropertyBlock` before every write, and it retires the
fade by restoring the material's own `_opacity` instead of clearing the block. Both writers are
now order-independent. **Never push a whole property block onto a renderer another system also
tints**; read-modify-write it.

### 2.3 A crystal's state CHANGE travels — the heart's blue → lime crossing (2026-08-15)

§2.2 defines what each state is painted with. This is what happens **between** two of them.

The change that matters is the lifeform heart: blue while it lives, the lime CTA once
`ActivateCrystal` drops it. That crossing is the pickup affordance — it is the moment the §26
wither reaches the core, or a joust frees the heart — so it **travels** rather than snapping,
running the same shape as a prism domain change
(`MaterialPropertyAnimator.ClockColorTransition`, `Docs/PRISM_ANIMATION.md`):

- the **state goes final at the start** — the crystal is collectable the instant it drops;
  colour is only how it *reads*;
- the start pair is **stamped once** against `PrismClock`;
- every pair in between is computed **analytically** from that stamp rather than accumulated, on
  the same smoothstep the prism lerp uses;
- `PrismTimerManager` fires **one** settle at the analytically-known end, which is what makes the
  final colour independent of the driver;
- an interruption **re-stamps from the analytic current**, so a second state change mid-fade
  departs from what is actually on screen.

Duration is `Crystal.colorTransitionSeconds` (0.8 s, matching the prism transition; 0 snaps).

**Three rules fall out, and each was a bug first.**

1. **Paint the flip explicitly.** `ActivateCrystal` repaints itself rather than leaving it to
   `Start` (which only fires because a heart's `Crystal` component is authored **disabled**) or to
   the material lerp's tail (skipped outright when a model has no target material). Rely on either
   and a collectable crystal keeps wearing heart blue.
2. **Read the start pair BEFORE anything disturbs it.** `ActivateCrystal` captures it on its first
   line: clearing `EmbeddedIn` changes what the state resolves to, and each material lerp drops
   the block on its way in. Read it later and Charge and Time — whose inactive material *is* the
   lime one — would start already-lime and travel nowhere. Exactly the ordering constraint
   `MaterialPropertyAnimator` documents when it reads start colours before binding the end-state
   material.
3. **A cleared block no longer describes the screen.** `ClearColorSetTint` forgets the resting
   pair, or a later cross-fade departs from a colour that has not been displayed since the clear.

`ApplyColorSetTint` is the RE-ASSERT path by contrast (a birth, a domain preview, a material lerp
settling): it writes the current state's pair immediately, and will not snap a live transition to
its end.

One deliberate difference from the prism path: a prism hands the interpolation to the GPU because
thousands animate at once and its graphs carry the clock wiring. The crystal shaders carry none
(§2.2 audits them), so a crystal's pair is pushed from the CPU — bounded by the crystals
actually *transitioning*, and cheaper than the cloned-material lerp it runs alongside.

### 2.4 A crystal colour is NOT a domain's UI colour — `GetDomainSignalColor` (2026-08-17)

**`DullCrystalColor` is authored `(0, 0, 0)` on Jade, Ruby AND Gold in the live
`OriginalColorSetSO`.** Only Blue has a non-black dull. That is deliberate and correct *on a
crystal*: §2.2 established that at the crystal shaders' fresnel power the dull colour paints ~93% of
the surface, so a near-black body with a bright fresnel rim is what makes a faceted domain crystal
read as a dark gem with a lit edge.

It is a trap for anything that is not a crystal shader. The Dolphin's Mass HUD slot announces a
team-locked seed and sampled `DullCrystalColor` on the entirely reasonable theory that the icon
should wear the colour the crystal will wear — and rendered a black square. `BrightCrystalColor` is
no answer either: it tops out at value 0.75 across all four domains.

**The rule.** To say "this belongs to domain X" anywhere that is not a crystal material, use
`SO_ColorSet.GetDomainSignalColor(domain)` — the domain **UI** colour (`TrailHighlightColor`) with
its brightest channel driven to 1, hue and saturation intact:

| domain | `GetDomainUIColor` | → `GetDomainSignalColor` |
|---|---|---|
| Jade | (0.055, 0.753, 0.714) | (0.073, **1.0**, 0.948) |
| Ruby | (0.784, 0.000, 0.765) | (**1.0**, 0.0, 0.976) |
| Gold | (1.000, 0.657, 0.000) | (**1.0**, 0.657, 0.0) — already at peak |

It returns white for an unauthored domain, so a signal can never silently become invisible — which
is the other half of the lesson: a colour accessor that can return black or near-black is a colour
accessor that can make a UI element disappear, and disappearing reads as "not implemented" rather
than as "mis-tinted".

Two consumers today, and they are the intended shape for future ones: the Dolphin's Mass slot, and
the Charge-5 pilot highlight (which needs it *saturated* for the same reason — a marked vessel has to
separate by HUE from the lit prisms around it, and brightness alone cannot do that).

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

### 4.3 Two saturated OPPOSITE hues cannot be blended — they have to be separated (2026-08-24)

Found on the Sparrow's projectile charge shell (`R_VesselActions/SPARROW_SPRAY_ACCURACY.md`
§ Round 4), but it is a composition law, not a projectile fact.

The shell wanted **neutral blue** arcs with a **danger red** hot core, composed the obvious
way: `lerp(blue, red, arcHeat²)`. It rendered magenta. **Any lerp between two saturated hues
on opposite sides of the wheel spends most of its range in a third hue that belongs to
neither** — and on an ADDITIVE surface it also *sums* with whatever is already behind it, so
the third hue appears even where the lerp did not put it.

The fix is a **threshold, not a different pair of colours**: `smoothstep(t, 1, heat)` confines
the second colour to the hot core, so the arc reads blue with a red filament in it. Measured
by hue-bucketing every lit fragment of the shipped shader after tonemapping:

| `_CoreThreshold` | blue | magenta | red |
|---|---|---|---|
| 0 (a plain `heat²` lerp) | 54.5% | **12.6%** | 32.9% |
| 0.75 (shipped) | 77.7% | **7.4%** | 14.9% |

> If a two-colour effect is reading as one muddy colour, reach for a separation dial before
> you reach for new colours. Changing the pair cannot fix a blend that is *supposed* to
> traverse the space between them.

**Two corollaries about how you judge this.**

**§4.1's rule reaches past prism tiers, and ACES is what enforces it.** The same shell's model
wanted a "desaturated whitish blue". Every candidate authored *as* a pale blue — including
this file's own `BlueColors.SpikeLightColor` — rendered at screen saturation **0.03–0.06**,
i.e. white, because ACES compresses highlights and a bright colour desaturates on the way to
the screen. The shipped value was chosen by computing post-tonemap sRGB and hunting for the
0.20–0.30 band. §4.1 says measure screen saturation for the shielded tier; it is true of
**anything** whose linear value is bright, and the inspector swatch will not tell you.

**Judge a candidate at the size it will be judged, and count the pixels.** A 300 px contact
sheet of that shell read as uniformly magenta; the hue census over the same shader said 7%,
and a single large panel proved the census right — at thumbnail scale a blue arc and its red
filament simply average together. A census of the rendered population is evidence; a
downsampled thumbnail is not.

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
   - **The `SegmentSpawner` track** (SkimRace / Skim Race) ships prisms with
     `IsShielded`, so the whole course is this tier.
   - **Astro League** (`AstroLeagueBall` shields prisms it touches), AOE block
     creation, and the skimmer overcharge effect.
2b. Get **danger** prisms on screen (§4 "The danger tier borrows the shielded base").
   Verified producers, easiest first:
   - **PeelTheCage** ("Peel the Cage") — its sparse cage traps are `PrismKind.Danger`,
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
2c. Get all five **crystals** on screen together (§2.2). Easiest producers:
   - **Menu_Main freestyle** — the omni crystal plus whatever the cell's lifeforms drop.
   - **Dog Fight / Rampage** — omni crystals respawn continuously in the nucleus, and
     Rampage's four intensities change how many are out at once.
   - **Wildlife Blitz / Wildlife Liberation** — elemental hearts drop as lifeforms die,
     which is the cheapest way to see all four elementals next to each other.

   Confirm, in this order — the first is the one that has never worked before:
   a. **Every crystal is lime.** Before this change none of them were (the omni read
      blue-white/green, Space blue-white, Time blue, only Charge lime), because `FadeIn`
      wiped the tint. If any crystal still shows its old colour, the fade is winning
      again — check that nothing else pushes a whole `MaterialPropertyBlock` onto that
      renderer.
   b. **The omni is clearly the brightest**, at distance especially — it is the only
      crystal whose *body* sits at the bloom ceiling. Target is roughly 6:1 bloom against
      an elemental; if they read the same, suspect `ElementalCrystalDimming` did not load
      (it is a NEW field — an old serialized copy of the asset simply will not have it,
      and it will silently take the 0.45 C# initializer, which is the intended value
      anyway, so this failing means something overrode it).
   c. **The four elementals are still legible**, not black. They sit just above the bloom
      threshold by design; if they read as dead olive lumps, raise the dimming toward
      0.55 (see the table in §2.2 — the knob is steep, 0.05 is a real step).
   d. **Hue is unchanged across all five.** They must differ only in brightness. Any hue
      drift means something is scaling the pair non-uniformly, or a channel is clipping
      past 1.0 (tonemapping is None — there is no shoulder to absorb it).

2d. Check the **heart crossing** (§2.3) — note it is the one crystal that must NOT be lime:
   a living lifeform's heart wears the blue-white neutral, and only a *free* crystal is lime.
   - Fly a cell with wildlife (Menu\_Main freestyle, Wildlife Blitz). Confirm every living
     lifeform's heart is **blue** — check a **Charge** and a **Time** species specifically
     (`Arbor Flora Charge`, `Tadpole Fauna Time`): their materials are the lime ones, so
     they are the two that fail first if the tint is lost again.
   - Kill one and watch the heart **ease** blue → lime over ~0.8 s rather than flicking.
     Tune on the crystal prefab's **Color Transition Seconds** (0 snaps).
   - Kill one **during its bloom** (within the first ~3 s of the lifeform spawning) to
     confirm the crossing and `FadeIn` coexist — both write the same block.
   - A heart stuck part-way between blue and lime means the settle never fired: check that
     a `PrismTimerManager` exists in the scene.

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
- **The gameplay Bloom `clamp` of 0.5 is the single biggest open question in this document
  (§2.2).** It caps the bloom source below most of the palette, so 56 of 86 authored colours
  bloom identically and every HDR value in this file — the danger rim at 1.498, the AOE
  colours at up to 4.0, `SuperShieldedInsideBlockColor` at 1.498 — is doing nothing that a
  flat 0.5 would not do. §3's rule that "channels above 1.0 are legitimate (they bloom)"
  is, as shipped, false. It is overridden deliberately (`m_OverrideState: 1`) in both the
  GamePlay and Commander profiles, so someone chose it; it is **not** a stray default.
  Raising it is a whole-game relighting, not a tune — do it as its own change, with its own
  playtest, and expect to re-derive the tiers in §4 afterwards. Measured headroom if it were
  raised to 1.0 and the crystal body pushed to a max channel of 1.0: **~3.5×** today's omni
  bloom, versus the **1.38×** available underneath the current clamp.
- The `Outside`/`Inside` field names are misleading (§2). Renaming them is a broad,
  GUID-safe but wide-reaching refactor across `SO_ColorSet` + `ThemeManager` + every
  colour set asset — worth doing, not worth bundling with a palette tune.
- **The crystal crossing (§2.3) interpolates on the CPU because the crystal shaders carry no
  clock wiring.** The prism path hands the same job to the GPU: `_ColorStartTime` +
  `_ColorDuration` + the start pair as per-instance properties, and the graph does
  `lerp(from, to, smoothstep(...))` itself (`MaterialPropertyAnimator.ClockColorTransition`,
  `Docs/PRISM_ANIMATION.md §4.1`). Wiring it into the crystal graphs would delete the CPU
  driver outright and make the crossing free. It is real work — `ShepardGraph` (259 nodes),
  `CrystalGraph` (270), `InverseDynamicFresnelGraph` (135), `DynamicFresnelGraph` (75), plus
  the hand-written `ChargeCrystal.shader`, which is the easy one — and it is
  `/asset-surgery` §2 territory (ShaderGraph JSON synthesis), not hand-editing. Worth its own
  change with its own playtest; the CPU driver is bounded by crystals actually transitioning,
  so there is no urgency.
- **`ChangeDomain`'s theft-decay path still snaps.** `ChangeDomain(Domains.Blue)` lerps the
  MATERIAL back to `defaultMaterial` over 2 s and then the tail `ApplyColorSetTint()` snaps to
  the CTA lime, because the two do not settle on the same colour. Pre-existing, and not fixed
  by §2.3 (which only covers `ActivateCrystal`). The fix is the same mechanism — capture the
  displayed pair before the material lerp starts and cross-fade the tint alongside it — but it
  needs the theft path play-tested, which nothing currently exercises often.
