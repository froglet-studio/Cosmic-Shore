# Cosmic Shore — Style Foundation

**Version:** 0.1 (draft for review) · **Reference:** 1920×1080, PPU 240 · **Stack:** uGUI + TextMeshPro

Direction: Helldivers 2 **discipline only** — the rigour, not the look. UI accent is **contextual team colour**, scoped by the contract in §3.

This document is the vocabulary every screen mockup and every `UIThemeSO` field derives from. It is not a screen.

---

## 1. Principles

1. **Colour is information, never decoration.** If a pixel is coloured, it is telling the player something. Chrome is neutral.
2. **Form disambiguates before hue does.** The interface must be readable with colour drained out. Team identity = accent strip + numeral. Destructive = filled surface. Different shapes, so colourblind modes and the Ruby-vs-danger collision both resolve without a special case.
3. **Data is monospaced and column-aligned.** Digits must not reflow while scores punch and roll.
4. **Hairlines and negative space, not plates and glows.** The HUD sits over a bright moving arena; legibility comes from the type ramp, not opaque backing.
5. **One chamfer, always the same corner.** Every surface cuts its top-right corner at 45°. Nothing else is rounded.
6. **Motion is short and functional.** Under 200ms for anything player-triggered. Ceremony is reserved for the quest claim and the end-game reveal.

---

## 2. Colour

### Surface ramp

| Token | Hex | Use |
|---|---|---|
| `void` | `#07090F` | Scrims, modal backdrop, deepest field |
| `hull` | `#0E131C` | Default panel surface |
| `plate` | `#171E2A` | Raised surface, card, button rest |
| `raise` | `#212B3A` | Hover surface, active row |
| `rule` | `#2A3444` | Hairline border, divider |
| `rule-hi` | `#3D4A5E` | Emphasised border, table head |

### Text ramp

| Token | Hex | Use |
|---|---|---|
| `signal` | `#E8EDF5` | Headings, primary values |
| `body` | `#B9C4D2` | Body copy, descriptions |
| `muted` | `#7C8899` | Labels, secondary, captions |
| `faint` | `#4E5A6B` | Disabled, placeholder, metadata |

Never pure white — it buzzes on a dark HUD over a moving arena.

### System and reserved hues

| Token | Hex | Use |
|---|---|---|
| `sys` | `#4FD5E8` | Focus, selection, links, chrome accent, **all pre-team UI** |
| `sys-dim` | `#2A8A99` | Inactive tab, unfilled track |
| `attn` | `#A67CFF` | New / unclaimed / CTA badge only |
| `danger` | `#FF5C3A` | Destructive **fill** only — never a tint or a border |

Cyan is the system hue because **Blue is already the codebase's neutral, non-playable sentinel**. Violet and vermilion sit outside the Jade/Ruby/Gold gamut deliberately.

### Team identity

Indicative only — read live values from `Assets/_SO_Assets/Color Palettes/OriginalColorSetSO.asset` and reconcile against `Docs/PALETTE.md` before committing.

| Domain | Indicative |
|---|---|
| Jade | `#35D6A0` |
| Ruby | `#FF4D63` |
| Gold | `#FFC44D` |
| Blue (no team) | `#4FD5E8` — and therefore the system hue |

**Traps in that asset:** `BrightCTA` / `DarkCTA` are crystal colours, not UI call-to-action colours. `DullCrystalColor` is authored black on all three teams. None of the three is usable in UI.

---

## 3. The team-colour contract

**The most load-bearing section.** Contextual team accent only works scoped to named roles. Unscoped, a Ruby player's interface reads as a permanent error state and a Jade player's as permanent success.

| Role | Team colour? | Form |
|---|---|---|
| Your team's score / total | **YES** | Numeral fill + 3px accent strip above |
| Your avatar chip / vessel marker | **YES** | 2px border |
| Your domain panel background | **YES** | Team colour @ 12% alpha, hairline @ 60% |
| Objective arrow, owned crystals | **YES** | Full-saturation fill (existing behaviour — keep) |
| Selection / focus ring | NO | Always `sys` — focus must be one learnable colour |
| Buttons, tabs, sliders, steppers | NO | Always `sys` |
| Panel borders, dividers, chrome | NO | Always `rule` |
| Body copy, labels, headings | NO | Text ramp only |
| Destructive confirm (kick, leave, delete data) | NO | `danger` as full-bleed fill |
| New / unclaimed indicators | NO | `attn` violet dot |
| Entire app shell before a team exists | NO | `sys` — the menu's resting state |

**Why the menu is cyan:** team assignment happens on Screen 2 of the Arcade configure modal. Before that the player has no domain. Team colour entering the interface at the moment they pick a tile turns a styling decision into feedback.

---

## 4. Typography

Three families, three jobs. Replaces the six currently in live use. Sizes at the 1920 reference map directly to TMP point sizes.

| Role | Family | Weight | Size | Tracking | Used for |
|---|---|---|---|---|---|
| Display | Chakra Petch | 600 | 48 | +0.01em | Victory banner, screen titles |
| H1 | Chakra Petch | 600 | 32 | 0 | Screen headers |
| H2 | Chakra Petch | 500 | 24 | 0 | Panel headers, modal titles |
| H3 | Chakra Petch | 500 | 18 | 0 | Card titles, tab labels |
| Body | Space Grotesk | 400 | 16 | 0 | Descriptions, dialogue |
| BodySm | Space Grotesk | 400 | 14 | 0 | Secondary copy, hints |
| Label | JetBrains Mono | 500 | 12 | +0.10em | Field labels, eyebrows, status |
| DataLg | JetBrains Mono | 700 | 44 | −0.01em | Team totals, timers, scores |
| Data | JetBrains Mono | 500 | 20 | 0 | Counters, balances, stat rows |
| DataSm | JetBrains Mono | 400 | 13 | +0.04em | Table cells, ranks |

**All numeric type uses tabular figures.** Labels are uppercase; headings and body are sentence case.

**Aldrich migration:** Aldrich has ~1,670 references (≈9× the runner-up) and is the de-facto brand font; Chakra Petch is already present at ~180. Retiring it is a font-asset reassignment plus a material-preset script, not 1,670 manual edits — but budget it as a task. Also move font assets out of `Assets/Unity Assests/TextMesh Pro/` into project space; a TMP package reimport can take them.

---

## 5. Space and geometry

8px base unit at the 1920 reference. Every margin, padding, and gap is a step on this scale.

| Token | Value | Use |
|---|---|---|
| `s1` | 4 | Icon-to-label gap, tight inline |
| `s2` | 8 | Inner element gaps, chip padding |
| `s3` | 12 | Button padding-y, list row gap |
| `s4` | 16 | Card padding, default gap |
| `s5` | 24 | Panel padding, section gap |
| `s6` | 32 | Modal padding, group separation |
| `s7` | 48 | Screen gutter, major sections |
| `s8` | 64 | Screen top/bottom margin |
| `s9` | 96 | Hero spacing, end-game layout |

| Property | Value | Note |
|---|---|---|
| Chamfer (large) | 14px, top-right only | Panels, cards, modals — one 9-slice sprite serves all |
| Chamfer (small) | 10px, top-right only | Buttons, chips, badges |
| Border radius | 0 | Nothing is rounded; the chamfer is the only corner treatment |
| Hairline | 1px | All borders and dividers |
| Emphasis stroke | 2px | Focus ring, selected state, own-chip border |
| Accent strip | 3px × 44px | Team-identity marker, fixed size everywhere |

---

## 6. Layering

Named layers over the sort-order stack already running. Use the names in code; keep the numbers.

| Layer | Sort order | Occupants |
|---|---|---|
| `transition` | 32767 | Scene fade / adopted splash veil |
| `consent` | 32766 | Privacy overlay (first run) |
| `veil` | 30000 | Environment load veil |
| `overlay` | 10 | Duel stats, splash |
| `modal` | 5 *(new)* | Modal stack — currently shares the base canvas |
| `hud` | 1 | Game canvas, FTUE |
| `base` | 0 | Menu, auth, vessel HUD |

**Visibility is CanvasGroup alpha, never SetActive.** Several components stay active at alpha 0 so their SOAP subscriptions survive. A mockup showing a screen "removed" still means alpha 0.

---

## 7. Interactive states

Replaces the per-prefab sprite-swap approach (`_pressed` / `_selected` / `_inactive` PNGs) with tint and stroke changes on one sprite.

| State | Surface | Border | Text | Motion |
|---|---|---|---|---|
| Rest | `plate` | `rule-hi` 1px | `signal` | — |
| Hover / highlight | `raise` | `sys` 1px | `signal` | 120ms tint |
| Pressed | `hull` | `sys` 2px | `sys` | 0.98× scale, 80ms |
| Selected | `sys` @ 14% | `sys` 2px | `sys` | Strip grows in, 200ms |
| Disabled | transparent | `rule` 1px | `faint` | None — no hover response |
| Focus (gamepad) | inherits | `sys` 2px, 2px offset | inherits | None — must be instant |

**Disabled must look disabled.** Two live cases to fix: the ARK and PORT nav tabs are tappable and do nothing; the Spend Crystals confirm button is *hidden* when unaffordable rather than disabled. Show it dimmed with the reason.

---

## 8. Motion

| Token | Duration | Easing | Use |
|---|---|---|---|
| `micro` | 120ms | OutQuad | Hover, tint, focus move |
| `std` | 200ms | OutCubic | Button press, toggle, tab change |
| `panel` | 320ms | OutQuint | Modal in/out, screen slide, toast entry |
| `ceremony` | 500ms+ | OutBack | Quest claim, end-game reveal — nothing else |

Staggers: **40ms** per item, capped at **8**. Current hangar grid uses 80ms across an unbounded list.

---

## 9. Safe area contract

No safe-area handling currently exists anywhere in the project, and `androidRenderOutsideSafeArea` is enabled.

| Rule | Detail |
|---|---|
| Background layer | Art, gradients, vignettes, scrims. Full bleed. Deliberately extends under notch and gesture bar. |
| Content layer | Every button, label, gauge, readout. Constrained by `SafeAreaFitter` to `Screen.safeArea`. |
| Minimum edge inset | 24px |
| Android max aspect | Raise 2.1 → **2.4** (at 2.1, 20:9 and 21:9 phones letterbox or crop per OEM) |
| Test aspects | 16:9 · 20:9 · 4:3 |

---

## 10. UIThemeSO field map

Author to this list verbatim. Follow the `HUDAnimationSettingsSO` pattern: ScriptableObject with graceful hardcoded fallbacks when unassigned.

| Field | Type | Value |
|---|---|---|
| `surfaceVoid` | Color | `#07090F` |
| `surfaceHull` | Color | `#0E131C` |
| `surfacePlate` | Color | `#171E2A` |
| `surfaceRaise` | Color | `#212B3A` |
| `borderRule` | Color | `#2A3444` |
| `borderRuleHigh` | Color | `#3D4A5E` |
| `textSignal` | Color | `#E8EDF5` |
| `textBody` | Color | `#B9C4D2` |
| `textMuted` | Color | `#7C8899` |
| `textFaint` | Color | `#4E5A6B` |
| `systemAccent` | Color | `#4FD5E8` |
| `systemDim` | Color | `#2A8A99` |
| `attention` | Color | `#A67CFF` |
| `danger` | Color | `#FF5C3A` |
| `spacing[9]` | float[] | `4, 8, 12, 16, 24, 32, 48, 64, 96` |
| `chamferLarge` | float | `14` |
| `chamferSmall` | float | `10` |
| `hairline` | float | `1` |
| `stroke` | float | `2` |
| `durMicro` | float | `0.12` |
| `durStd` | float | `0.20` |
| `durPanel` | float | `0.32` |
| `durCeremony` | float | `0.50` |
| `staggerStep` | float | `0.04` |
| `staggerCap` | int | `8` |

**Team colours are not in this asset.** They stay in `SO_ColorSet`. `UIThemeSO` is chrome only — that separation is what enforces the contract in §3.

---

*Supersedes nothing until approved. Companion to `Docs/UI_ARCHITECTURE_AUDIT.md` and `Docs/PALETTE.md`.*
