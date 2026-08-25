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
| T4 | UIThemeSO + literal inventory | IN PROGRESS | — | `claude/uithemeso-color-audit-0zfqjz` | | |
| T5 | Download & install TMP fonts | TODO | — | | | |
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
- [x] `UIThemeSO` authored to §10 **verbatim** — 25 fields, no additions
- [x] Follows `HUDAnimationSettingsSO` pattern with hardcoded fallbacks
- [x] **No team colour fields** — they stay in `SO_ColorSet`
- [ ] Live asset created and referenced — created, **referenced by nothing** (see Deviations)
- [x] Mapping report covers all 165 literals in `Assets/_Scripts/UI/`
- [x] Unmapped literals bucketed: (a) missing token, (b) feature-level SO, (c) never designed
- [x] No call sites changed yet

**Deliverables:**
- `Assets/_Scripts/UI/UIThemeSO.cs` — 25 serialized fields; names, types and declaration order verified as an exact match against §10's table. Values are hardcoded defaults, so an unassigned reference degrades to the shipped tokens.
- `Assets/_SO_Assets/UI/UITheme.asset` (+ `.meta`) — authored instance, sibling to `HUDAnimationSettings.asset`. All 14 colour fields round-trip to §10's hex.
- `Docs/UI_COLOR_TOKEN_MAP.md` — the mapping report. 184 occurrences classified across 180 sites, every site cited by `file:line`.

**Findings:**
- The literal count is **184 occurrences on 180 lines**, not 165, under the rule `new Color(` · `new Color32(` · `Color.<named>` · `<color=` over `Assets/_Scripts/UI/**/*.cs`. Same population — the audit's own worst-offender figures run approximate in the same direction (`PrivacyConsentOverlay` cited at 14 vs an actual 15; `SquirrelVesselHUDView` at "7–8" vs 16, counting only the authored `[SerializeField]` tints). The `new Color(` + named-`Color.X` subset alone is 170. All 184 are classified and the buckets sum, so nothing is unaccounted for either way.
- Only **58** of the 184 are chrome. The rest: 34 vessel-HUD gauge states, 24 editor-window chrome, 22 team identity, 19 that carry no authored colour at all (alpha re-packs, `Color.clear`, captured-rest initialisers), 14 console rich text, 13 gaps.
- **`danger` and `borderRule` have zero call sites.** Nothing in the UI currently draws a destructive fill or a code-side divider. Both are greenfield rather than consolidations.
- **`attention` has one accidental match**: `IconRotator`'s violet cycle stop is `#A673FF`, four units off the token. Nothing else in the UI is violet.
- **Two live defects, independent of any theming work.** `DomainVolumeHexGraphic.cs:84` hardcodes `{ Color.green, Color.red, Color.yellow }` as the three domain colours and never consults `SO_ColorSet`, so a domain re-colour silently misses that gauge. `ObjectiveArrowGraphic`'s three constants are fixed lime regardless of domain, so §3's "objective arrow … existing behaviour — keep" describes an intent the code does not implement.
- **The census floor is a floor.** `HangarCaptainsView.cs:51-52` carries `"FFF"` / `"888"` as bare 3-digit hex strings, which match no colour-literal pattern. Colours authored on prefabs are invisible to the sweep entirely — and §7 replaces a per-prefab `_pressed`/`_selected`/`_inactive` sprite-swap approach, so the real interactive-state surface is larger than 58.
- **Do not sweep `Color.white` globally.** 17 of the 64 are `textSignal`; the rest are untint resets, domain fallbacks, gauge rests and field initialisers. A blanket replace would dim invisible raycast targets, break domain fallbacks and change gauge semantics.
- **Both companion docs landed mid-task** (`bleeding-edge` `8b3a7692`). T4 was implemented from copies supplied directly, before either was in the repo. The committed `Docs/STYLE_FOUNDATION.md` was diffed against the copy used and is **byte-identical**, and §10 was re-verified against the committed file: 25 fields, names/types/order an exact match, and all 14 authored colours matching. The report's citations now point at the canonical paths.

**Deviations from spec:**
- **"Live asset created and referenced" cannot hold while "no call sites changed yet" holds.** An SO asset can only be referenced by a `[SerializeField] UIThemeSO` on a consumer, which is a call-site change. Verified: 0 references to the asset GUID in any prefab/scene/asset, and 0 scripts declaring a `UIThemeSO` field. The asset is created and authored; it is deliberately unreferenced. **Needs a ruling on which clause wins** — the criterion as written is unsatisfiable.
- **Four non-field members were added beyond the 25 fields.** The serialized surface is exactly 25 and the inspector shows nothing extra, but the class also declares `SpacingSteps` (public const), `DefaultSpacing` (static readonly), `_warnedSpacing` (private bool) and `_fallback` (static), supporting three accessors — `Resolve(theme)`, `Spacing(step)`, `StaggerFor(index)`. Reasons: `spacing[9]` is an array a designer can resize, so a bounds-safe 1-based reader that warns once is needed or a mis-sized asset fails silently; `staggerStep`/`staggerCap` are meaningless apart, and read as two loose floats they reproduce the unbounded-stagger bug the hangar grid already has; and without `Resolve` every call site must restate a hex, which is how `HUDAnimationSettingsSO`'s defaults ended up duplicated inline at `ScoreNumberAnimator.cs:131-132`. **If the criterion is read strictly, all of this moves to a separate static class and `UIThemeSO` becomes 25 fields and nothing else** — a contained change, offered rather than assumed.
- **Report scope is 184, not 165** (see Findings). Superset, fully reconciled in the report's §2.
- **`UIThemeSO.cs` adds one `new Color(` to `Assets/_Scripts/UI/`** — the private `Rgb(int hex)` helper the tokens are built from. A strict cross-cutting grep reads this as +1; the count of *hardcoded* literals is unchanged at 184.
- Report was published as an artifact as well as committed: https://claude.ai/code/artifact/b9c74280-7c9a-4f4d-9b6b-e7e03f65c048 — `Docs/UI_COLOR_TOKEN_MAP.md` is canonical.

---

## T5 — Download & install TMP fonts

**Spec:** Style Foundation §4

Acceptance criteria:
- [ ] Chakra Petch (400/500/600/700) TTFs in `Assets/_Graphics/Fonts/ChakraPetch/`
- [ ] Space Grotesk (300/400/500/600) in `Assets/_Graphics/Fonts/SpaceGrotesk/`
- [ ] JetBrains Mono (400/500/700) in `Assets/_Graphics/Fonts/JetBrainsMono/`
- [ ] **Not** placed in `Assets/Unity Assests/TextMesh Pro/`
- [ ] `OFL.txt` shipped per family; credits attribution added
- [ ] TMP font assets generated: SDFAA, sampling 90, padding 9, atlas 1024²
- [ ] Charset: ASCII + Latin-1 Supplement + `× · — – ‑ … ← → ↑ ↓ ✕ + −`
- [ ] Multi Atlas Textures on; dynamic overflow fallback set
- [ ] Fallback chain: Space Grotesk → Chakra Petch → Liberation Sans
- [ ] TMP Settings default font asset = Space Grotesk 400
- [ ] Base material presets only — no outline, glow, or bevel
- [ ] Type-scale test scene screenshot captured at 1920×1080
- [ ] Tabular figure check: `0123456789` over `1111111111` columns align

**Deliverables:**
**Findings:**
**Deviations from spec:**

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
| 1 | T4 | T4 | No token for **negative / urgent feedback**. §3 pins `danger` to destructive *fill* only, so score-loss text, the countdown-urgent tint and the consent overlay's validation error have nowhere legal to go. Three of the four sites are already the same value (`1, 0.3, 0.2` ≈ `#FF4D33`), a hair off `danger`'s `#FF5C3A`. Add a `warn` token distinct from `danger`, or rule that this is gameplay feedback and belongs in `ElementalBarsConfigSO`? | OPEN |
| 2 | T4 | T4 | No token for **positive feedback** (score gain). §2 has no success hue and green is Jade, so a success token collides with team identity the way §1.2 says Ruby collides with danger. §1.2's own answer — form disambiguates before hue — suggests motion or a glyph rather than a colour. Which? | OPEN |
| 3 | T4 | T4 | **Disabled is defined for chrome controls, not for dimmed content.** §7 gives disabled a transparent surface, `rule` border and `faint` text; `GameCard.lockedTintColor` is a `(0.3,0.3,0.3)` multiply over card art and `DomainInfoData` swaps alpha `1.0`/`0.4`. Add a disabled tint/alpha token, or rule that content dims via `CanvasGroup` alpha? | OPEN |
| 4 | T4 | T4 | **Requirement met / unmet** (`HangarCaptainsView` crystal and XP affordability) maps cleanly onto `textSignal` / `textMuted`, but "can I afford this" is arguably a status rather than a text weight. Confirm the text-ramp reading, or name it? | OPEN |

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
