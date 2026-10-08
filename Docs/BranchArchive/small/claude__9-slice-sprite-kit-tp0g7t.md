# Branch archive: `claude/9-slice-sprite-kit-tp0g7t`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-08-25 by Claude
- **Unmerged commits:** 1
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/796
- **Forked from:** `0298f1a65` (2026-08-25, Merge remote-tracking branch 'origin/claude/style-foundation-v03-docs-pu91j1' )
- **Tip:** `9291a3017`
- **Files touched (62):**
  - `Assets/_Graphics/UI.meta`
  - `Assets/_Graphics/UI/SpriteKit.meta`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_BannerCap_Left.png`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_BannerCap_Left.png.meta`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_BannerCap_Right.png`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_BannerCap_Right.png.meta`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_Banner_Fill.png`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_Banner_Fill.png.meta`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_ButtonSmall_Border1.png`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_ButtonSmall_Border1.png.meta`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_ButtonSmall_Border1_Flipped.png`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_ButtonSmall_Border1_Flipped.png.meta`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_ButtonSmall_Fill.png`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_ButtonSmall_Fill.png.meta`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_ButtonSmall_Fill_Flipped.png`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_ButtonSmall_Fill_Flipped.png.meta`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_Button_Border1.png`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_Button_Border1.png.meta`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_Button_Border1_Flipped.png`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_Button_Border1_Flipped.png.meta`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_Button_Fill.png`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_Button_Fill.png.meta`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_Button_Fill_Flipped.png`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_Button_Fill_Flipped.png.meta`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_Card_Border2.png`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_Card_Border2.png.meta`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_Card_Border2_Flipped.png`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_Card_Border2_Flipped.png.meta`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_Card_Fill.png`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_Card_Fill.png.meta`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_Card_Fill_Flipped.png`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_Card_Fill_Flipped.png.meta`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_CurrencyPill_Border1.png`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_CurrencyPill_Border1.png.meta`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_CurrencyPill_Border1_Flipped.png`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_CurrencyPill_Border1_Flipped.png.meta`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_CurrencyPill_Fill.png`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_CurrencyPill_Fill.png.meta`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_CurrencyPill_Fill_Flipped.png`
  - `Assets/_Graphics/UI/SpriteKit/UIKit_CurrencyPill_Fill_Flipped.png.meta`
  - … and 22 more

### `9291a3017` — feat(ui): 9-slice sprite kit for the corner-sliver shape language (T7)

_Claude, 2026-08-25 02:54:30 +0000_

```text
26 white+alpha 9-slice sprites under Assets/_Graphics/UI/SpriteKit/ implementing
Style Foundation §5's corner sliver — a 45° diagonal cut on two opposite corners,
flippable — across the §10 components: buttons (two sliver sizes × two
orientations, fill and 1px frame), panel/popup, card, currency pill, hexagon tile,
hexagon slider handle, and the §10.9 angled banner with its triangular end caps.

No colour is baked: every pixel is RGB 255,255,255 with the shape entirely in
alpha, so one asset serves every state under an Image.color tint from UIThemeSO
(T4). No shipping prefab or scene is touched.

The 9-slice border is measured off the rasterised shape, not chosen. A slice is
exact when every column it stretches is identical to its neighbours; the nominal
sliver is the right inset for a filled shape and too small for an outlined one,
because eroding the polygon pushes the mitre past the sliver line by
stroke × (√2 − 1). Authored at the nominal, every outline variant leaked
antialiased pixels into a stretched region.

Assets and the test scene are generated from one table, so --check covers both:

  Tools/Build/ui_sprite_kit_geometry.py  convex geometry, exact coverage, PNG
                                         codec, measure_border
  Tools/Build/author_ui_sprite_kit.py    the kit table; --check, --table
  Tools/Build/ui_sprite_kit_scene.py     test scene YAML
  Tools/Build/verify_ui_sprite_kit.py    proves the shipped bytes

verify reports 26/26 at maxdiff 0/255: each shipped sprite, 9-sliced to a target
size, is byte-identical to the same geometry rasterised natively at that size —
at the minimum legal size, 1:1, 3× wide, 409 wide, and at double height where the
sprite carries vertical insets.

Test scene: Assets/_Scenes/Game_TestDesign/UISpriteKitTestScene.unity, five pages,
each row one sprite at three widths (plus double height), each sample a different
§2 tint. Declarative UGUI, no new C#. Not in Build Settings.

Reference: Docs/UI_SPRITE_KIT.md. Four design questions filed as queue #10-#13.
```

```text
 Assets/_Graphics/UI/SpriteKit/UIKit_CurrencyPill_Border1.png.meta     |   147 +
 Assets/_Graphics/UI/SpriteKit/UIKit_CurrencyPill_Border1_Flipped.png  |   Bin 0 -> 196 bytes
 .../UI/SpriteKit/UIKit_CurrencyPill_Border1_Flipped.png.meta          |   147 +
 Assets/_Graphics/UI/SpriteKit/UIKit_CurrencyPill_Fill.png             |   Bin 0 -> 159 bytes
 Assets/_Graphics/UI/SpriteKit/UIKit_CurrencyPill_Fill.png.meta        |   147 +
 Assets/_Graphics/UI/SpriteKit/UIKit_CurrencyPill_Fill_Flipped.png     |   Bin 0 -> 165 bytes
 .../_Graphics/UI/SpriteKit/UIKit_CurrencyPill_Fill_Flipped.png.meta   |   147 +
 Assets/_Graphics/UI/SpriteKit/UIKit_HexHandle_Fill.png                |   Bin 0 -> 225 bytes
 Assets/_Graphics/UI/SpriteKit/UIKit_HexHandle_Fill.png.meta           |   147 +
 Assets/_Graphics/UI/SpriteKit/UIKit_HexTile_Border2.png               |   Bin 0 -> 440 bytes
 Assets/_Graphics/UI/SpriteKit/UIKit_HexTile_Border2.png.meta          |   147 +
 Assets/_Graphics/UI/SpriteKit/UIKit_HexTile_Fill.png                  |   Bin 0 -> 341 bytes
 Assets/_Graphics/UI/SpriteKit/UIKit_HexTile_Fill.png.meta             |   147 +
 Assets/_Graphics/UI/SpriteKit/UIKit_Panel_Border1.png                 |   Bin 0 -> 264 bytes
 Assets/_Graphics/UI/SpriteKit/UIKit_Panel_Border1.png.meta            |   147 +
 Assets/_Graphics/UI/SpriteKit/UIKit_Panel_Border1_Flipped.png         |   Bin 0 -> 269 bytes
 Assets/_Graphics/UI/SpriteKit/UIKit_Panel_Border1_Flipped.png.meta    |   147 +
 Assets/_Graphics/UI/SpriteKit/UIKit_Panel_Fill.png                    |   Bin 0 -> 221 bytes
 Assets/_Graphics/UI/SpriteKit/UIKit_Panel_Fill.png.meta               |   147 +
 Assets/_Graphics/UI/SpriteKit/UIKit_Panel_Fill_Flipped.png            |   Bin 0 -> 227 bytes
 Assets/_Graphics/UI/SpriteKit/UIKit_Panel_Fill_Flipped.png.meta       |   147 +
 Assets/_Scenes/Game_TestDesign/UISpriteKitTestScene.unity             | 10739 ++++++++++++++++++++++++++++++
 Assets/_Scenes/Game_TestDesign/UISpriteKitTestScene.unity.meta        |     7 +
 Docs/UI_REDESIGN_TASKS.md                                             |    88 +-
 Docs/UI_SPRITE_KIT.md                                                 |   236 +
 Tools/Build/author_ui_sprite_kit.py                                   |   455 ++
 Tools/Build/ui_sprite_kit_geometry.py                                 |   389 ++
 Tools/Build/ui_sprite_kit_scene.py                                    |   585 ++
 Tools/Build/verify_ui_sprite_kit.py                                   |   415 ++
 62 files changed, 16744 insertions(+), 8 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 2224 lines)</summary>

```diff
diff --git a/Docs/UI_REDESIGN_TASKS.md b/Docs/UI_REDESIGN_TASKS.md
index c1fd2e4d5..9c9cc668a 100644
--- a/Docs/UI_REDESIGN_TASKS.md
+++ b/Docs/UI_REDESIGN_TASKS.md
@@ -249,22 +249,90 @@ Acceptance criteria:
 
 ## T7 — Component sprite kit
 
-**Spec:** Style Foundation §5 (geometry) · §10 (components)
+**Spec:** Style Foundation §5 (geometry) · §10 (components) · **Reference: `Docs/UI_SPRITE_KIT.md`**
 
 Acceptance criteria:
-- [ ] 9-slice sprites authored for: button (two sliver sizes + flipped), panel/popup (border and
+- [x] 9-slice sprites authored for: button (two sliver sizes + flipped), panel/popup (border and
       fill variants), card, hexagon tile, hexagon slider handle, currency pill, end-game banner +
-      end caps
-- [ ] Corner sliver is a **diagonal cut on two opposite corners, flippable** — not a rounded rect,
+      end caps — **26 sprites**
+- [x] Corner sliver is a **diagonal cut on two opposite corners, flippable** — not a rounded rect,
       not a single chamfer
-- [ ] Border insets correct — slivers hold their shape at any width
-- [ ] All sprites **white + alpha, no baked colour** — tinted from `UIThemeSO` at runtime
-- [ ] Test scene shows each sprite at three widths
-- [ ] **No shipping prefab touched**
+- [x] Border insets correct — slivers hold their shape at any width
+- [x] All sprites **white + alpha, no baked colour** — tinted from `UIThemeSO` at runtime
+- [x] Test scene shows each sprite at three widths
+- [x] **No shipping prefab touched**
 
 **Deliverables:**
+- `Assets/_Graphics/UI/SpriteKit/` — 26 white+alpha PNGs + `.meta`. Full inventory (source size,
+  border, minimum size, which axes each may scale on, the §10 component it serves) is
+  `Docs/UI_SPRITE_KIT.md` §3.
+- `Assets/_Scenes/Game_TestDesign/UISpriteKitTestScene.unity` — five pages (page 1 active, toggle
+  the siblings), each row one sprite at its **minimum legal width**, 1:1 and 420 px, plus a fourth
+  sample at double height where the sprite carries vertical insets. Each sample is tinted a
+  different §2 colour, which is what demonstrates the asset carries none. Page 5 shows the §10.9
+  header assembled. Not in Build Settings. **No new C#** — declarative UGUI only, so the
+  `/verify-unity` compile gate does not apply to this branch.
+- `Tools/Build/ui_sprite_kit_geometry.py`, `author_ui_sprite_kit.py` (`--check`, `--table`),
+  `ui_sprite_kit_scene.py`, `verify_ui_sprite_kit.py` — pure stdlib, ~10 s, no Unity.
+- `Docs/UI_SPRITE_KIT.md` — the reference.
+
 **Findings:**
+- **The 9-slice border is a MEASURED property of the pixels, not a design number.** A slice is
+  exact when every column it stretches horizontally is identical to its neighbours (and likewise
+  rows). The nominal sliver is the right inset for a *filled* shape and **too small for an
+  outlined one**: eroding the polygon pushes the mitre where the diagonal meets the straight edge
+  back past the sliver line by `stroke × (√2 − 1)`. Authored at the nominal, every `_Border`
+  variant leaked antialiased pixels into a stretched region. A 1 px frame on a 10 px sliver needs
+  an **11 px** inset; a 2 px frame on the hexagon's shallower rake needs **16**. So `Fill` and
+  `Border` variants of one component legitimately carry different borders — that is correct, not
+  drift. Borders are now derived by `measure_border()` and re-measured off the shipped bytes by
+  the verifier.
+- **The shipped button art is the superseded shape and is not a reference.** Decoded,
+  `Button_Flat_White.png` (272×72, `spriteBorder 22,0,22,0`) is a **single** 20 px top-right
+  chamfer over a half-alpha drop shadow with `E6E9FF` baked into every pixel — the v0.1/v0.2 shape
+  §5 explicitly corrects, plus the baked colour this kit removes. Its `_Flipped` sibling mirrors
+  it. Recorded in `UI_SPRITE_KIT.md` §1 so it is not pattern-matched off later.
+- **`textureCompression` must be 0.** Block compression chews a 1 px frame and mangles the
+  antialiased diagonal. It is the setting most likely to be "tidied" back to the project default,
+  so the verifier fails on any platform entry that is not uncompressed.
+- **Supersampled coverage is translation-dependent on a 45° edge.** A 45° line passes through
+  sample centres, so whether an on-line sample counted as inside was decided by float noise: the
+  same sliver rasterised at x=54 and at x=118 differed by 1–2/255. Invisible on screen, fatal to
+  an exactness proof. Coverage is now exact clipped-polygon area, snapped before scaling to 8
+  bits; the 45° bisector lands on exactly 128 and a corner is byte-identical wherever it sits.
+- **A generated scene that reads its table positionally fails silently.** Reordering the kit tuple
+  moved `group` from index 2 to 3 and every page emitted empty — still valid YAML, still opens,
+  just blank. The emitter now takes named records and the verifier counts sprite references per
+  sprite.
+- Verified out of editor: `--check` is byte-clean, and `verify_ui_sprite_kit.py` reports **26/26 at
+  maxdiff 0/255** — every shipped sprite 9-sliced to a target size is *byte-identical* to the same
+  geometry rasterised natively at that size, at the minimum legal size, 1:1, 3× wide, 409 wide and
+  (where applicable) at double height. The scene parses as YAML with zero dangling local `fileID`
+  references, and its four UGUI script GUIDs are the ones `Menu_Main.unity` already uses.
+
 **Deviations from spec:**
+- **The 14 px-text button's sliver is 8 px, which is spec-authored.** §5's geometry table names one
+  button sliver (10 px) while §4's type scale requires two button sizes. 10 × 14/18 = 7.8 → 8,
+  which also lands on the §5 spacing scale. Queue #10.
+- **`_Flipped` variants were authored for panel, card and pill as well as the button.** T7 names
+  the flip only for the button; §5 states flippability as a property of the shape language, and the
+  generator produces both orientations for free. Queue #11.
+- **Sprites are authored minimal, not at a component's footprint.** T7 asks for the card at the
+  "Daily Deals and Arcade Explore footprint"; a 9-slice's footprint is a property of the
+  RectTransform, and authoring a ~400×280 uncompressed RGBA source would cost ~450 KB to say
+  nothing the 80×80 source does not. The footprints are demonstrated in the test scene instead.
+  Consequence: `Card_Fill` and `Panel_Fill` are geometrically congruent under 9-slice, differing
+  only in source size — see queue #12.
+- **The end caps are not 9-slice sprites.** A triangle has no scalable interior on either axis, so
+  they carry `border 0`, are drawn `Image.Type.Simple`, and are only ever scaled uniformly. The
+  test scene shows them at three uniform scales rather than three widths.
+- **Hexagons and the banner body carry horizontal insets only** and their authored height is fixed
+  — the hexagon's two points span the full height, so there is no band to stretch vertically.
+- **The banner's rake is spec-authored at 2:1** (32 px over a 64 px height); §10.9 says "angled"
+  and gives no angle. The caps are the two triangles the rake cuts away, so they tile back into the
+  banner's bounding rect. Queue #13.
+- **The test scene uses legacy `UnityEngine.UI.Text` for labels, not TMP** — deliberately, so a
+  throwaway test scene takes no dependency on font assets task T5 is still replacing.
 
 ---
 
@@ -283,6 +351,10 @@ Anything found during implementation that needs a design decision. The implement
 | 7 | Typography art | T5/T6 | ~~Display, Body small and the three Data roles are spec-authored, not on the source page.~~ **RESOLVED — kept, and marked as such.** §4's table now daggers those five rows with a footnote stating they carry no guide backing and are open to revision in a way the transcribed six are not. | RESOLVED |
 | 8 | Typography art | — | ~~The button caps rule has a documented exception that v0.3 dropped.~~ **RESOLVED — caps is unconditional; the exception is retired with the Port screen** (already cut from the overhaul). §4 records the decision so it is not relitigated. | RESOLVED |
 | 9 | Typography art | T5 | ~~A live countdown renders in the button face, not a Data role.~~ **RESOLVED — `<mspace>` generalised.** It now applies to any live-updating numeric in **any** face, not just the Aldrich Data roles. `X` is per-face, `TabularText` takes the face as a parameter, and T5 reports the digit advance for **both** Aldrich and Chakra Petch SemiBold. | RESOLVED |
+| 10 | T7 impl | T7 | **§5's geometry table names one button sliver (10 px) but §4's type scale requires two button sizes.** The kit authors 8 px for the 14 px-text button (10 × 14/18, and on the §5 spacing scale). Confirm 8, or keep 10 px on both button sizes. | OPEN |
+| 11 | T7 impl | T7 | **Which components get both sliver orientations?** T7 named the flip only for the button; §5 states flippability as a property of the shape language, so the kit also ships `_Flipped` for panel, card and currency pill (free to generate). Keep the full set, or trim to the button? | OPEN |
+| 12 | T7 impl | T7 | **§5 says cards take the sliver "at the same ratio, scaled to the surface", but names only 14 px (large) and 10 px (buttons/chips).** A card is a smaller surface than a popup yet both currently take 14 px, so `Card_Fill` and `Panel_Fill` are congruent under 9-slice. Should the card take an intermediate sliver (12 px), or is one 14 px surface sliver intended? | OPEN |
+| 13 | T7 impl | T7 | **§10.9 specifies an "angled banner with triangular end caps" but gives no angle.** The kit authors a 2:1 rake (32 px over 64 px height), with the caps being the two triangles the rake cuts away — so cap + body + cap tiles back into the bounding rect. Confirm the rake and the cap shape. | OPEN |
 
 ---
 
diff --git a/Docs/UI_SPRITE_KIT.md b/Docs/UI_SPRITE_KIT.md
new file mode 100644
index 000000000..ae8ac976e
--- /dev/null
+++ b/Docs/UI_SPRITE_KIT.md
@@ -0,0 +1,236 @@
+# Cosmic Shore — UI 9-slice sprite kit
+
+**Task:** UI redesign **T7** · **Spec:** `Docs/STYLE_FOUNDATION.md` §5 (geometry), §10 (components)
+**Source art:** not in the repo — `Docs/StyleGuide/README.md` is the transcription of record
+
+The component sprite kit for the redesign's shape language: 26 white + alpha
+9-slice sprites under `Assets/_Graphics/UI/SpriteKit/`, a generated test scene,
+and the three tools that author and prove them.
+
+**No shipping prefab or scene is touched.** The kit is assets only; wiring it
+into the UI is later work, and it is gated on `UIThemeSO` (task T4) existing to
+tint from.
+
+---
+
+## 1. The shape
+
+> **The corner sliver is a 45° diagonal cut on TWO OPPOSITE corners, flippable.**
+> Not a rounded rect. Not a single chamfer. Border radius is 0 everywhere.
+> — Style Foundation §5
+
+Two orientations, named after the studio art's own convention:
+
+| Suffix | Corners cut |
+|---|---|
+| *(none)* | top-right + bottom-left |
+| `_Flipped` | top-left + bottom-right |
+
+§5's sliver sizes: **14 px** on large surfaces, **10 px** on buttons and chips.
+
```

</details>
