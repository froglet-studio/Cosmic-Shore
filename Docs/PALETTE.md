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

Jade was the healthy domain and is the model. Ruby and Gold now carry Jade's exact
**lightness and contrast**, their own **hue**, and chroma at a fixed allowance above
Jade's:

```
shielded base :  L* = 54.31      C* = Jade C* x 1.35 = 43.34      hue = domain's own unshielded base hue
shielded rim  :  L* = 83.65      C* = Jade C* x 1.35 = 50.31      hue = domain's own unshielded rim hue
                 dL* = 29.34  (identical relative luminance across all domains)
```

Jade itself keeps its authored `C*` (32.10 / 37.27) — it is the reference, unmodified.

Measured state (linear HDR → CIELAB):

| tier | domain | ΔL\* base→rim | C\* base / rim |
|---|---|---|---|
| unshielded *(untouched)* | Jade / Ruby / Gold | 32.2 / 27.1 / 47.6 | 33.6-48.1 / 77.2-138.8 / 51.9-76.5 |
| **shielded — before** | Jade / Ruby / Gold | 29.3 / **10.1** / **9.8** | 32.1-37.3 / **105.8-111.4** / 45.9-43.3 |
| **shielded — after** | Jade / Ruby / Gold | 29.3 / **29.3** / **29.3** | 32.1-37.3 / **43.3-50.3** / **43.3-50.3** |
| supershielded *(untouched)* | Jade / Ruby / Gold | 55.2 / 42.2 / 41.5 | 29.0-18.5 / 70.9-43.0 / 68.2-40.1 |

Two defects were fixed: **contrast collapse** (Ruby and Gold at ~⅓ of Jade's ΔL\*) and
**chroma runaway on Ruby** (>3× Jade's — a near-zero-green violet that blooms harshly).
SuperShielded was measured and left alone: it is already healthy on all three domains.

### Why chroma is Jade × 1.35 and not Jade exactly

Matching Jade's chroma outright is the cleaner rule and was tried first. It fails on the
warm hue: low chroma reads "icy" on a cool hue but **muddy** on a warm one, and Gold came
out khaki. The 1.35 allowance is the one taste knob in this contract — it keeps Gold gold
while keeping Ruby and Gold equal to each other. Raise it for punchier domains, lower it
toward 1.0 for a more uniform frosted family.

### Known trade-off

Gold's *unshielded* rim is anomalously hot (L\* 91.8, vs Jade 76.2), so equalising the
shielded tier across domains puts Gold's shielded rim (83.65) slightly **below** its own
unshielded rim. Its base still rises sharply (44.3 → 54.3), so a shielded Gold prism
still reads brighter and frostier overall. If it ever reads as "dimmer when shielded",
the fix is to rebalance Gold's *unshielded* pair — not to re-break the shielded tier.

## 5. Re-deriving after a change

The contract is reproducible from the asset alone; no play mode needed:

1. Parse the four `Shielded*BlockColor` values per domain out of the `.asset`.
2. Convert linear RGB → CIELAB (§3 — no de-gamma).
3. Check `ΔL*` base→rim per domain, and `C*` per colour, against the §4 table.
4. To re-place a domain: take Jade's shielded `L*` pair, the domain's own unshielded
   Lab **hue** for base and rim, and `C* = Jade C* × allowance`; convert LCh → Lab → linear RGB.
5. Assert no channel is negative before writing.

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
3. For **Ruby** and **Gold**, confirm: the prism's facets and silhouette read clearly
   (the base→rim separation is visible), and the shielded prism is obviously distinct
   from an unshielded prism of the same domain.
4. Check bloom: Ruby's rim peaks at 1.19 and Gold's at 1.05 — both below Jade's 1.22,
   so if Jade doesn't blow out, neither should these.
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
