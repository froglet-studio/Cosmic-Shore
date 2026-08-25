# Cosmic Shore — UI Redesign Task Tracker

**Branch:** `bleeding-edge` · **Companion docs:** `Docs/UI_ARCHITECTURE_AUDIT.md`, `Docs/STYLE_FOUNDATION.md`, `Docs/GAMECANVAS.md`, `Docs/PALETTE.md`

Maintained by the `ui-redesign-tracker` skill. Do not hand-edit the status table — run the skill.

**Status legend:** `TODO` · `IN PROGRESS` · `BLOCKED` · `NEEDS DESIGN` · `DONE`

---

## Status

| ID | Task | Status | Depends on | Branch | PR | Completed |
|---|---|---|---|---|---|---|
| T1 | Safe area component | TODO | — | | | |
| T2 | Finish canvas resolution migration | TODO | — | | | |
| T3 | Unify GameCanvas fork | TODO | T2 | | | |
| T4 | UIThemeSO + literal inventory | TODO | — | | | |
| T5 | Download & install TMP fonts | IN PROGRESS | — | `claude/tmp-font-assets-setup-9z96he` | | |
| T6 | TMP Style Sheet + Aldrich audit | TODO | T5 | | | |

**Critical path:** T2 → T3 is the long pole. T1, T4, T5 are independent and can run in parallel. T6 needs T5's font assets to exist.

---

## T1 — Safe area component

**Spec:** Style Foundation §9 · **Audit ref:** §1.3

Acceptance criteria:
- [ ] `Assets/_Scripts/UI/SafeAreaFitter.cs` exists and compiles
- [ ] Reads `Screen.safeArea`, drives `anchorMin`/`anchorMax`
- [ ] Recalculates on resolution and orientation change
- [ ] Caches last-applied rect — no per-frame work when unchanged
- [ ] Full-screen safeArea (desktop) is a no-op
- [ ] `androidRenderOutsideSafeArea` confirmed still enabled
- [ ] Not yet applied to any shipping prefab
- [ ] Test scene demonstrates the two-layer contract

**Deliverables:**
**Findings:**
**Deviations from spec:**

---

## T2 — Finish canvas resolution migration

**Spec:** Style Foundation §5 · **Audit ref:** §1.3

Acceptance criteria:
- [ ] `_Prefabs/CORE/GameCanvas.prefab` at 1920×1080 / PPU 240
- [ ] `_Prefabs/GameCanvas-HexRace.prefab` at 1920×1080 / PPU 240
- [ ] `_Scenes/Singleplayer Scenes/SplashScreen.unity` migrated
- [ ] `_Prefabs/UI Elements/Loadout Container.prefab` migrated
- [ ] `CanvasUpgraderUpgradedPrefabs.txt` respected — no double pass (×5.76 check)
- [ ] `AdaptiveCanvasScaler` on every scene canvas that lacked it
- [ ] Static `matchWidthOrHeight` overrides removed from those scenes
- [ ] Android max aspect raised 2.1 → 2.4
- [ ] No remaining reference resolution outside 1920×1080 project-wide
- [ ] Project builds; no canvas visibly regressed in a smoke pass

**Deliverables:**
**Findings:**
**Deviations from spec:**

---

## T3 — Unify GameCanvas fork

**Spec:** `Docs/GAMECANVAS.md` · **Audit ref:** §5.1

Acceptance criteria:
- [ ] Prefab Kit Validate run, output recorded
- [ ] Prefab Kit Consolidate run
- [ ] Identical overrides (~1,734) pushed into the prefab
- [ ] **One scene only** re-placed, diff reported, explicit go-ahead received before the rest
- [ ] Override count per canvas instance below 25 in each migrated scene
- [ ] `statsToTrack` preserved per mode (the one real per-mode value)
- [ ] Joust toast feed rect normalised from ~(-1416, -463)
- [ ] 8 cross-asset dangling refs into the CORE prefab resolved
- [ ] Dangling `CountdownDisplay` ref into never-instantiated `MiniGameHUD.prefab` resolved
- [ ] All six modes launch and reach the Ready gate
- [ ] End-game scoreboard renders in each mode

**Deliverables:**
**Findings:**
**Deviations from spec:**

---

## T4 — UIThemeSO + literal inventory

**Spec:** Style Foundation §10 · **Audit ref:** §1.4, §5.4

Acceptance criteria:
- [ ] `UIThemeSO` authored to §10 **verbatim** — 25 fields, no additions
- [ ] Follows `HUDAnimationSettingsSO` pattern with hardcoded fallbacks
- [ ] **No team colour fields** — they stay in `SO_ColorSet`
- [ ] Live asset created and referenced
- [ ] Mapping report covers all 165 literals in `Assets/_Scripts/UI/`
- [ ] Unmapped literals bucketed: (a) missing token, (b) feature-level SO, (c) never designed
- [ ] No call sites changed yet

**Deliverables:**
**Findings:**
**Deviations from spec:**

---

## T5 — Download & install TMP fonts

**Spec:** Style Foundation §4

Acceptance criteria:
- [x] Chakra Petch (400/500/600/700) TTFs in `Assets/_Graphics/Fonts/ChakraPetch/`
- [x] Space Grotesk (300/400/500/600) in `Assets/_Graphics/Fonts/SpaceGrotesk/`
- [x] JetBrains Mono (400/500/700) in `Assets/_Graphics/Fonts/JetBrainsMono/`
- [x] **Not** placed in `Assets/Unity Assests/TextMesh Pro/`
- [x] `OFL.txt` shipped per family; credits attribution added
- [x] TMP font assets generated: SDFAA, sampling 90, padding 9, atlas 1024²
- [x] Charset: ASCII + Latin-1 Supplement + `× · — – ‑ … ← → ↑ ↓ ✕ + −`
- [x] Multi Atlas Textures on; dynamic overflow fallback set
- [x] Fallback chain: Space Grotesk → Chakra Petch → Liberation Sans
- [x] TMP Settings default font asset = Space Grotesk 400
- [x] Base material presets only — no outline, glow, or bevel
- [~] Type-scale test scene screenshot captured at 1920×1080
- [x] Tabular figure check: `0123456789` over `1111111111` columns align

**Deliverables:**
- 11 static TTFs + 3 `OFL.txt` under `Assets/_Graphics/Fonts/{ChakraPetch,SpaceGrotesk,JetBrainsMono}/`.
  Chakra Petch is upstream `google/fonts` static; Space Grotesk and JetBrains Mono ship
  variable-only there, so their per-weight **static** instances come from Google Fonts' own
  gstatic builds (woff wrapper unwrapped to TTF, outlines untouched).
- 11 TMP font assets, one per weight, SDFAA / pointSize 90 / padding 9 / 1024² Alpha8,
  every family fitting a **single** atlas (65–75% fill). ~23 MB total.
- `Tools/Build/tmp_font_lib.py` + `Tools/Build/build_tmp_font_assets.py` — the generator.
  `--fetch` / `--build` / `--check` / `--verify-donor`. The assets are the build; `--check`
  proves the committed output still matches, so the generator cannot silently drift behind it.
- `Tools/Build/render_type_scale.py`, `Tools/Build/render_tabular_proof.py` and the two
  1920×1080 captures in `Docs/Fonts/`.
- `Docs/Legal/THIRD_PARTY_NOTICES.md` — the attribution register, with paste-ready credits copy;
  indexed from `Docs/Legal/README.md`.
- `TMP Settings`: default font asset → `SpaceGrotesk-Regular SDF`; global fallbacks →
  `LiberationSans SDF` then `LiberationSans SDF - Fallback` (the dynamic overflow).

**Findings:**
- **`U+2011` (non-breaking hyphen) and `U+2715` (✕) exist in no font in the chain.** Not in any
  of the three new families, not in the static `LiberationSans SDF`, and not in
  `LiberationSans.ttf` (the dynamic fallback's own source) — so no link can rescue them and
  they will render as nothing. `U+2715` *is* present in JetBrains Mono. Queue entries 1 and 2.
- `U+00B5` (µ, Latin-1 Supplement) is absent from Chakra Petch but present in Liberation Sans,
  so it resolves through the fallback and needs no decision.
- The **current TMP version no longer serializes `hashCode` / `materialHashCode`** and has
  renamed `material` → `m_Material`, adding `m_SourceFontFilePath` and `InternalDynamicOS`.
  A font asset written to the older field names loads with a **null material**. The generator
  is keyed to the newest in-repo asset (`ChakraPetch-Regular SDF`) and asserts key parity.
- TMP's SDF encoding is `alpha = 0.5 + d / (2·(padding+1))`, and the material's
  `_GradientScale` **is** `padding + 1` — measured, not assumed, off the shipped atlas.
- `Assets/Unity Assests/TextMesh Pro/` still contains a **pre-existing** vendored
  `ChakraPetch-Regular` TTF + SDF asset from before this task. Nothing references the new
  assets to it, but it is exactly the package-reimport exposure §4 calls out, and it is now a
  duplicate of a font we own in project space. Left in place — deleting it is outside T5.
- Fonts are not under a `Resources/` folder, so rich-text `<font="…">` tags cannot find them by
  name. Direct references (the default asset, the fallback tables, inspector fields) are
  unaffected. Same as every other font in `Assets/_Graphics/Fonts/`.

**Deviations from spec:**
- **The font assets were generated programmatically, not through the Font Asset Creator.**
  No Unity editor is available in this environment. Mitigation: the generator was validated by
  regenerating the shipped `ChakraPetch-Regular SDF` and diffing it — face info **15/15**,
  glyph table **97/97** on metrics and rect size, character table identical, top-level key
  parity exact — and its SDF pixels match that atlas to a mean **0.054 px**, below the
  0.055 px that one 8-bit alpha step can represent. **A Unity import pass is still owed**
  (see below); this is the one thing offline validation cannot stand in for.
- **The type-scale capture is an offline render, not a Unity test scene** — hence `[~]`.
  `Tools/Build/render_type_scale.py` reads the generated `.asset` files (atlas pixels, glyph
  rects, face metrics) and reproduces `TMP_SDF.shader`'s alpha rule, so it verifies the shipped
  assets rather than the source TTFs — but it does not exercise TMP's own layout code.
- **Fallback chain split across two places.** Space Grotesk → Chakra Petch is per-asset and
  **weight-matched** (SG Light → CP Regular, since Chakra Petch has no 300); the
  Liberation Sans → dynamic tail is global in TMP Settings so Chakra Petch and JetBrains Mono
  inherit it without duplicating the tail on 11 assets. The spec states the chain but not where
  it lives.
- **`m_UsedGlyphRects` / `m_FreeGlyphRects` are emitted empty.** They only matter for dynamic
  population; the shipped static `Rajdhani-*` assets do the same.
- Folder names follow the acceptance criteria (`ChakraPetch/`), not §4's prose spelling
  ("Chakra Petch"). The human-readable names are used in the credits register.

**What still needs the Unity editor:**
1. Open the project and confirm all 11 assets import clean — each shows its atlas, a non-null
   material, and a populated glyph table in the inspector.
2. Confirm `TMP Settings` shows `SpaceGrotesk-Regular SDF` as the default and the two global
   fallbacks in order.
3. Drop a TMP label in a scene at 1920×1080 and eyeball the §4 rows against
   `Docs/Fonts/type-scale-1920x1080.png` — this is what promotes the `[~]` to `[x]`.
4. If anything looks wrong, re-run `python3 Tools/Build/build_tmp_font_assets.py --verify-donor`
   first; it re-proves the model against the shipped TMP asset in ~2 s.

---

## T6 — TMP Style Sheet + Aldrich audit

**Spec:** Style Foundation §4

Acceptance criteria:
- [ ] TMP Style Sheet with all 10 named styles from §4
- [ ] Each style carries family, weight, size @1920, tracking
- [ ] Aldrich migration cost report: reassignable vs per-component
- [ ] Liberation Sans leak list produced (~174 refs)
- [ ] Migration **not** executed — estimate only

**Deliverables:**
**Findings:**
**Deviations from spec:**

---

## Design feedback queue

Anything found during implementation that needs a design decision. The implementer **adds** entries here and does not resolve them or edit `STYLE_FOUNDATION.md` directly.

| # | Raised by | Task | Question | Status |
|---|---|---|---|---|
| 1 | T5 | T5 | `U+2011` (non-breaking hyphen) is in the §4 charset but exists in **none** of the three families, nor in Liberation Sans or the dynamic fallback's source font — it can render nowhere. Drop it from the charset, or substitute `U+2010`/`U+002D`? | OPEN |
| 2 | T5 | T5 | `U+2715` (✕) is in the §4 charset but is present **only** in JetBrains Mono; Chakra Petch, Space Grotesk and Liberation Sans all lack it, so it cannot resolve for UI text. Add JetBrains Mono to the fallback chain for it, use a different close glyph, or make ✕ an icon rather than type? | OPEN |

---

## Style Foundation version log

| Version | Date | Change | Driven by |
|---|---|---|---|
| 0.1 | — | Initial token system, team-colour contract, type scale | Design |

---

## Deferred / out of scope

Decisions taken during the redesign to explicitly not do something. Recorded so they don't get silently relitigated.

| Item | Decision | Rationale |
|---|---|---|
| UI Toolkit migration | Deferred past Steam EA | Framework migration on top of the fork debt risks the EA date |
| Store / ARK screen | Cut from overhaul | Needs a product decision, not a visual one |
| Port / Leaderboards screen | Cut from overhaul | Same — 104 sprites feeding a disabled screen |
