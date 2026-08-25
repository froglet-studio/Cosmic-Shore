# Cosmic Shore — UI 9-slice sprite kit

**Task:** UI redesign **T7** · **Spec:** `Docs/STYLE_FOUNDATION.md` §5 (geometry), §10 (components)
**Source art:** not in the repo — `Docs/StyleGuide/README.md` is the transcription of record

The component sprite kit for the redesign's shape language: 26 white + alpha
9-slice sprites under `Assets/_Graphics/UI/SpriteKit/`, a generated test scene,
and the three tools that author and prove them.

**No shipping prefab or scene is touched.** The kit is assets only; wiring it
into the UI is later work, and it is gated on `UIThemeSO` (task T4) existing to
tint from.

---

## 1. The shape

> **The corner sliver is a 45° diagonal cut on TWO OPPOSITE corners, flippable.**
> Not a rounded rect. Not a single chamfer. Border radius is 0 everywhere.
> — Style Foundation §5

Two orientations, named after the studio art's own convention:

| Suffix | Corners cut |
|---|---|
| *(none)* | top-right + bottom-left |
| `_Flipped` | top-left + bottom-right |

§5's sliver sizes: **14 px** on large surfaces, **10 px** on buttons and chips.

**The shipped `Assets/_Graphics/Buttons/Button_Flat_*.png` art is the superseded
shape and is not a reference.** Decoding it (272×72, `spriteBorder 22,0,22,0`)
shows a **single** 20 px top-right chamfer over a half-alpha drop shadow, with
`E6E9FF` baked into every pixel — exactly the v0.1/v0.2 shape §5 corrects, plus
the baked colour this kit exists to remove. Do not pattern-match new sprites off
it.

The banner (§10.9) is deliberately **not** part of the sliver family: §10.9 calls
it an *angled* banner, and it is a parallelogram whose ends rake 2:1 across the
full height, with detached triangular end caps.

---

## 2. The rule that matters: the inset is MEASURED, not chosen

A 9-slice is exact when **every column it stretches horizontally is identical to
its neighbours**, and likewise every row it stretches vertically. So the correct
border is not a design number — it is the largest run of identical columns (and
rows) around the middle of the source. `measure_border()` computes exactly that,
and the kit table carries no border at all.

This is not pedantry; guessing gets it wrong:

> For a **filled** sliver the correct inset is exactly the sliver width. For the
> **same shape outlined**, it is not. Eroding a polygon by the stroke pushes the
> mitre — where the diagonal meets the straight edge — back past the sliver line
> by `stroke × (√2 − 1)`. A 1 px frame on a 10 px sliver therefore needs an
> **11 px** inset, and a 2 px frame on the hexagon's shallower rake needs **16**.

Authored at the nominal sliver, every outline variant in the kit leaked
antialiased pixels into a stretched region. The verifier caught it before it
shipped; nothing about it is visible by eye at the authored size, which is the
whole reason it is measured and re-measured rather than reviewed.

**Corollary:** a sprite's border can differ from its sliver, and `Fill` and
`Border` variants of the same component legitimately carry different borders.
That is correct, not drift.

### Scaling axes

Insets on all four sides mean the shape scales freely in **width and height**.
Buttons only lengthen today (§5), but nothing in the asset depends on that
staying true.

A **hexagon** has no horizontal band to stretch vertically — its two points span
the full height and *are* the shape — so it carries horizontal insets only and
its authored height is fixed. Same for the banner body. A **triangle** has no
scalable interior on either axis: the end caps carry `border 0`, are drawn
`Image.Type.Simple`, and are only ever scaled uniformly.

---

## 3. Inventory

| Sprite | Source | Border (L,B,R,T) | Min size | Scales | Component |
|---|---|---|---|---|---|
| `UIKit_Button_Fill` | 64x48 | 10,10,10,10 | 20x20 | width + height | Sec.10.1 opaque button |
| `UIKit_Button_Border1` | 64x48 | 11,11,11,11 | 22x22 | width + height | Sec.10.1 transparent button |
| `UIKit_ButtonSmall_Fill` | 52x36 | 8,8,8,8 | 16x16 | width + height | Sec.10.1 opaque button, small |
| `UIKit_ButtonSmall_Border1` | 52x36 | 9,9,9,9 | 18x18 | width + height | Sec.10.1 transparent button, small |
| `UIKit_Button_Fill_Flipped` | 64x48 | 10,10,10,10 | 20x20 | width + height | Sec.10.1 opaque button |
| `UIKit_Button_Border1_Flipped` | 64x48 | 11,11,11,11 | 22x22 | width + height | Sec.10.1 transparent button |
| `UIKit_ButtonSmall_Fill_Flipped` | 52x36 | 8,8,8,8 | 16x16 | width + height | Sec.10.1 opaque button, small |
| `UIKit_ButtonSmall_Border1_Flipped` | 52x36 | 9,9,9,9 | 18x18 | width + height | Sec.10.1 transparent button, small |
| `UIKit_Panel_Fill` | 64x64 | 14,14,14,14 | 28x28 | width + height | Sec.10.3 popup body |
| `UIKit_Panel_Border1` | 64x64 | 15,15,15,15 | 30x30 | width + height | Sec.10.3 popup frame |
| `UIKit_Panel_Fill_Flipped` | 64x64 | 14,14,14,14 | 28x28 | width + height | Sec.10.3 popup body |
| `UIKit_Panel_Border1_Flipped` | 64x64 | 15,15,15,15 | 30x30 | width + height | Sec.10.3 popup frame |
| `UIKit_Card_Fill` | 80x80 | 14,14,14,14 | 28x28 | width + height | Sec.10.6 Daily Deals / Arcade Explore |
| `UIKit_Card_Border2` | 80x80 | 15,15,15,15 | 30x30 | width + height | Sec.10.13 selected card frame |
| `UIKit_Card_Fill_Flipped` | 80x80 | 14,14,14,14 | 28x28 | width + height | Sec.10.6 Daily Deals / Arcade Explore |
| `UIKit_Card_Border2_Flipped` | 80x80 | 15,15,15,15 | 30x30 | width + height | Sec.10.13 selected card frame |
| `UIKit_CurrencyPill_Fill` | 56x32 | 10,10,10,10 | 20x20 | width + height | Sec.10.4 currency bar body |
| `UIKit_CurrencyPill_Border1` | 56x32 | 11,11,11,11 | 22x22 | width + height | Sec.10.4 currency bar frame |
| `UIKit_CurrencyPill_Fill_Flipped` | 56x32 | 10,10,10,10 | 20x20 | width + height | Sec.10.4 currency bar body |
| `UIKit_CurrencyPill_Border1_Flipped` | 56x32 | 11,11,11,11 | 22x22 | width + height | Sec.10.4 currency bar frame |
| `UIKit_HexTile_Fill` | 56x48 | 14,0,14,0 | 28x48 | width only | Sec.10.5 tab nav / Sec.10.12 port side nav |
| `UIKit_HexTile_Border2` | 56x48 | 16,0,16,0 | 32x48 | width only | Sec.10.5 / Sec.10.12 active tile |
| `UIKit_HexHandle_Fill` | 28x24 | 7,0,7,0 | 14x24 | width only | Sec.10.7 settings slider handle |
| `UIKit_Banner_Fill` | 128x64 | 32,0,32,0 | 64x64 | width only | Sec.10.9 VICTORY / DEFEAT body |
| `UIKit_BannerCap_Left` | 32x64 | 0,0,0,0 | 32x64 | uniform only | Sec.10.9 left end cap |
| `UIKit_BannerCap_Right` | 32x64 | 0,0,0,0 | 32x64 | uniform only | Sec.10.9 right end cap |

Minimum size is `L+R` × `T+B` — below it Unity overlaps the corner regions and
the sliver *does* distort. The test scene shows every sprite at exactly that
size first, because it is the harshest case.

---

## 4. Using one

- **`Image.Type` = Sliced**, `Fill Center` on, `Pixels Per Unit Multiplier` = 1.
  The end caps are the exception: `Simple`.
- The CanvasScaler's `Reference Pixels Per Unit` must stay **100**, matching the
  sprites' `spritePixelsToUnits`. Otherwise every border is drawn at a scaled
  size and the "1 px" hairline stops being 1 px.
- **Tint at runtime.** Every sprite is RGB `255,255,255` with the shape entirely
  in alpha, so one asset serves every state in §10.1 / §10.6 under an
  `Image.color`. Set that from `UIThemeSO` (task T4) — do not author a
  per-colour copy, which is what the shipped button set did (`Button_Flat_Green`,
  `_Orange`, `_Purple`, `_Gray`, `_White`, each with the colour baked in).
- A **frame plus a fill** is two stacked Images (`_Border1` over `_Fill`), which
  is how §10.3's "`00010A` panel, 1 px `E6E9FF` border" is built.
- **Glow is not geometry.** §10.6's four card states differ by tint and outer
  glow; the card sprite is the same in all four.

---

## 5. Import settings (and why)

| Setting | Value | Why |
|---|---|---|
| `textureCompression` | **0 — uncompressed**, every platform | Block compression chews a 1 px frame and mangles the antialiased diagonal. This is the setting most likely to be "tidied" back to default; don't. |
| `enableMipMap` | 0 | UI draws at one depth. |
| `alphaIsTransparency` | 1 | With white RGB in the transparent pixels, no dark fringe bleeds onto the cut. |
| `filterMode` | 1 (bilinear), `wrapU/V` 1 (clamp) | |
| `spritePixelsToUnits` | 100 | Matches every other sprite in the project and the CanvasScaler. |
| `spriteMeshType` | 1 | What all 1,166 other sprites in the project use, including the shipped 9-sliced buttons. |

The PNGs are 8-bit RGBA, filter type 0 on every row, zlib level 9 — so the bytes
are reproducible and `--check` is meaningful.

---

## 6. Tools

| Script | Does |
|---|---|
| `Tools/Build/ui_sprite_kit_geometry.py` | Convex geometry, exact pixel coverage, the PNG codec, `measure_border` |
| `Tools/Build/author_ui_sprite_kit.py` | The kit table; writes every PNG, `.meta` and the test scene. `--check` verifies the committed bytes, `--table` prints §3 |
| `Tools/Build/ui_sprite_kit_scene.py` | Emits the test scene YAML |
| `Tools/Build/verify_ui_sprite_kit.py` | Proves the **shipped** files, reading them back off disk |

```
python3 Tools/Build/author_ui_sprite_kit.py            # regenerate
python3 Tools/Build/author_ui_sprite_kit.py --check    # CI: bytes match the table
python3 Tools/Build/verify_ui_sprite_kit.py -v         # CI: the assets are correct
```

Pure standard library, no Unity, ~10 s. Run both after any change to the kit.

### What the verifier actually proves

1. Every pixel's RGB is `255,255,255` — no baked colour, checked on the file.
2. The `.meta` carries the settings in §5 and a border matching the pixels.
3. Every partially-transparent pixel of a sliver lies inside an **unstretched**
   corner region.
4. Every stretched region is invariant along the axis it stretches.
5. **End to end:** the shipped sprite, 9-sliced to a target size, is compared
   against the same geometry rasterised natively at that size — at the minimum
   legal size, 1:1, 3× wide, 409 wide, and (where the sprite has vertical
   insets) at minimum, 1:1 and double height. All 26 match at **maxdiff 0/255**.
6. The test scene shows every sprite at 3+ distinct widths, with the right
   `Image.Type`, no dangling `fileID`, and nothing laid out below the 1080 line.

(3)+(4) together mean composition is exact at *any* legal size, which is what
(5) then confirms on a spread of concrete ones.

---

## 7. Test scene

`Assets/_Scenes/Game_TestDesign/UISpriteKitTestScene.unity` — **not in Build
Settings**, no new C#, nothing but declarative UGUI.

Five full-screen pages, **page 1 active and the rest inactive**: toggle the
sibling `Page N` objects in the Hierarchy. Each row is one sprite at its minimum
legal width, 1:1, and 420 px — plus a fourth sample at double height where the
sprite carries vertical insets — and **each sample is tinted a different §2
colour**, which is what demonstrates the asset carries none. Page 5 also shows
the §10.9 header assembled: caps at lower alpha either side of a stretched body.

The scene is generated from the same table as the sprites, so `--check` covers it
and it cannot drift.

---

## 8. Traps recorded

- **Supersampling is translation-dependent on a 45° edge.** The first
  rasteriser sampled 16×16 per pixel; a 45° line passes through sample centres,
  so whether an on-line sample counted as inside was decided by float noise, and
  the same sliver at x=54 and x=118 differed by 1–2/255. Invisible on screen,
  fatal to an exactness proof. Coverage is now the exact clipped-polygon area,
  snapped at 1e-9 before scaling to 8 bits — the 45° bisector lands on exactly
  128 and the corner is byte-identical wherever it sits.
- **A generated scene that reads its table positionally will silently emit
  nothing.** Reordering the kit tuple moved `group` from index 2 to 3 and every
  page came out empty — YAML still valid, scene still opens, just blank. The
  emitter now takes named records, and the verifier counts sprite references.
- **A minimum-size shape may legitimately degenerate.** A hexagon at exactly
  twice its point run is a rhombus; the polygon builder drops coincident
  vertices rather than rejecting them, because the verifier tests that size.
- **Only `PNG` filter type 0.** Any adaptive filter would make the bytes depend
  on the zlib heuristic and break `--check`.

---

## 9. Not done here

- **No prefab or shipping scene is touched** — out of scope by instruction.
- **`UIThemeSO` does not exist yet** (task T4). Until it does, the tint values in
  §11 of the Style Foundation are literals; the test scene inlines them.
- **No sprite atlas.** 26 tiny uncompressed textures are cheap, but if UI draw
  calls become a problem the kit is the natural first atlas.
- **No icons.** §9's glyph set is a separate body of work.
