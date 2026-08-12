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
| unshielded *(untouched)* | Jade / Ruby / Gold | 32.2 / 27.1 / 47.6 | 33.6-48.1 / 77.2-138.8 / 51.9-76.5 | 100-100 / 98-100 / 100-91 |
| shielded — original | Jade / Ruby / Gold | 29.3 / **10.1** / **9.8** | 32.1-37.3 / **105.8-111.4** / 45.9-43.3 | — |
| shielded — after the ΔL\* pass | Jade / Ruby / Gold | 29.3 / 29.3 / 29.3 | 32.1-37.3 / 43.3-50.3 / **43.3-50.3** | 82-59 / 65-50 / **90-82** |
| **shielded — current** | Jade / Ruby / Gold | 29.3 / 29.3 / 29.3 | 32.1-37.3 / 43.3-50.3 / **28.5-23.6** | 82-59 / 65-50 / **74-55** |
| supershielded *(untouched)* | Jade / Ruby / Gold | 55.2 / 42.2 / 41.5 | 29.0-18.5 / 70.9-43.0 / 68.2-40.1 | — |

Three defects have been fixed across two passes. The first pass fixed **contrast
collapse** (Ruby and Gold at ~⅓ of Jade's ΔL\*) and **chroma runaway on Ruby** (>3×
Jade's — a near-zero-green violet that blooms harshly). The second pass fixed **Gold
failing to shift to pastel** (§4.1). SuperShielded was measured and left alone: it is
already healthy on all three domains.

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
| **Gold *(current)*** | **13.0 / 17.1** | **+26% / +36%** |

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
composed in `ThemeManager` out of two existing colours:

| | |
|---|---|
| base face (`_DarkColor`) | the domain's `ShieldedOutsideBlockColor` |
| fresnel rim (`_BrightColor`) | the shared `EnvironmentColors.Danger` (one colour, all domains) |

The rim is what says *dangerous* (it is domain-independent by design, since danger is not
safe to its own domain — see CLAUDE.md); the base is what says *whose*. It takes the
**shielded** base rather than the plain one so a danger prism reads as its own frostier
tier of the domain at a glance, instead of as ordinary mass wearing a hot rim.

Measured (linear HDR → CIELAB, §3). Danger rim is `L* = 50.51, C* = 77.85`:

| domain | plain base L\* | shielded base L\* | ΔL\* base→rim with plain *(before)* | with shielded *(as shipped)* |
|---|---|---|---|---|
| Jade | 44.08 | 54.31 | 6.42 | **−3.80** |
| Ruby | 27.41 | 54.31 | 23.09 | **−3.81** |
| Gold | 44.25 | 54.31 | 6.26 | **−3.81** |

**Known trade-off — this pair separates on chroma, not lightness.** The rim now sits
marginally *darker* than the base (sign flip: a frosty body with a dark red rim, where it
used to be a dark body with a hot rim), and |ΔL\*| ≈ 3.8 is far under the 29.34 the
shielded tier holds. Form still reads because the two are miles apart in **hue and
chroma** (a C\* 77.85 red against a C\* 29–43 domain hue), which §3's ΔL\* criterion does
not capture. Jade and Gold barely changed (they were at ~6.3 already); **Ruby gave up a
real 23-point ΔL\***. If a danger prism ever reads flat, the fix is to re-place the danger
rim's `L*` against the 54.31 shielded base — raise it well above, or drop it well below —
**not** to send the base back to the plain colour, which only restored contrast on Ruby.

**Gold is the weakest of the three here and always was** — its hue sits only 44° from the
danger red, against Jade's 132° and Ruby's 84°, so it has the least hue separation to
trade on. The §4.1 desaturation left that essentially unchanged (ΔE00 29.81 → 28.15) while
*widening* the chroma gap this pair actually separates on (34.5 → 49.3). If gold danger
prisms ever read flat, that hue proximity is the cause — not the shielded chroma.

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

### Known trade-off

Gold's *unshielded* rim is anomalously hot (L\* 91.8, vs Jade 76.2), so equalising the
shielded tier across domains puts Gold's shielded rim (83.65) slightly **below** its own
unshielded rim. Its base still rises sharply (44.3 → 54.3) and both now desaturate hard,
so a shielded Gold prism reads frostier overall. If it ever reads as "dimmer when
shielded", the fix is to rebalance Gold's *unshielded* pair — not to re-break the shielded
tier. That rebalance is **deliberately not bundled here**: plain gold is the most-seen
prism of the domain and retuning it is a wider change than the shielded tier warranted.

## 5. Re-deriving after a change

The contract is reproducible from the asset alone; no play mode needed:

1. Parse the four `Shielded*BlockColor` values per domain out of the `.asset`.
2. Convert linear RGB → CIELAB (§3 — no de-gamma).
3. Check `ΔL*` base→rim per domain (must be 29.34), and **screen saturation** per colour
   (§4.1 — clip to [0,1] first), against the §4 table. Checking `C*` alone will pass a
   colour that fails on screen; that is how the Gold defect survived a whole pass.
4. To re-place a domain: take Jade's shielded `L*` pair and the domain's own unshielded
   Lab **hue** for base and rim, then solve `C*` so screen saturation hits the §4.1 target
   (monotonic in `C*` — a bisection converges in a few dozen steps); convert LCh → Lab →
   linear RGB.
5. Assert no channel is negative before writing.
6. Re-check the **danger** tier for that domain (it borrows the shielded base) and the
   plain → shielded `ΔE00` journey, so a chroma move can't quietly flatten either.

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
   Confirm the base→rim separation reads as **hue/chroma**, not brightness — that is the
   danger tier's stated trade-off, and it is what a flat-looking danger prism would be
   failing at.
3. For **Ruby** and **Gold**, confirm: the prism's facets and silhouette read clearly
   (the base→rim separation is visible), and the shielded prism is obviously distinct
   from an unshielded prism of the same domain.
3b. For **Gold** specifically, the question §4.1 was solving is *"does it go pastel like
   the other two?"* — put a shielded gold prism beside a plain one and confirm the shift
   reads as **sand/cream**, the warm counterpart of Jade's mint and Ruby's pink. If it
   still reads as "gold, slightly lighter", the screen-saturation target needs to come
   down further; if it reads **chalky or dead**, it has come down too far.
4. Check bloom: Jade's rim peaks at 1.22, Ruby's at 1.19, Gold's at **0.84**. Bloom
   threshold is **0.2** (`GamePlay PostProcessing Profile`, 0.24 in the menu), so all
   three are well clear of it and none should blow out. Gold's peak is lowest because a
   warm hue reaches a given `L*` with smaller channel values than a blue one — expect it
   to glow a little more softly than the other two, which is inherent, not a defect.
5. Compare all three domains side by side — no domain should read hotter or flatter
   than the others.

## 7. Follow-ups

- The inactive palettes (`CosmicWaveColorSetSO`, `PastelColorSetSO`) still carry the old
  flat shielded values. They are dead assets today; if either is ever wired up, run §5
  against it first.
- `Domains.Blue` (the neutral sentinel) was not measured or tuned; it is not a playable
  domain and its prisms are rarely seen.
- The `Outside`/`Inside` field names are misleading (§2). Renaming them is a broad,
  GUID-safe but wide-reaching refactor across `SO_ColorSet` + `ThemeManager` + every
  colour set asset — worth doing, not worth bundling with a palette tune.
