# Cosmic Shore — Style Foundation

**Version:** 0.3 · **Reference:** 1920×1080, PPU 240 · **Stack:** uGUI + TextMeshPro

**v0.3 supersedes v0.1 and v0.2.** The existing Cosmic Shore style guide — Main Colors, Additional Colors, Typography, Icons, Buttons, UI Elements — is **authoritative**. This document records it, resolves the open items, and adds only what the guide does not cover: spacing, layering, motion, safe area, and the numeric type role.

Direction: Helldivers 2 **discipline only** — the rigour, not the look.

---

## 0. Resolved decisions

| # | Item | Resolution |
|---|---|---|
| A | Team naming | **Jade = Team 1 (cyan `00D4FF`), Ruby = Team 2 (purple `A600FF`), Gold = Team 3 (amber `FFAE00`).** Legacy names, current art — confirmed by the end-of-game victory banners. Green `99FF80` is outside the team gamut and is therefore safe as the interactive hue. |
| B | Type scale | Guide was authored for mobile at 800×450. Launch is PC. Do **not** apply a mechanical ×2.4 — see §4 for the PC scale. |
| C | Numeric type | **Aldrich with TMP `<mspace>`** for tabular figures. JetBrains Mono and Space Grotesk are cancelled. |
| D | Currency vs Gold | **Accepted overlap.** Currency and Gold share amber. They never appear in the same component — currency lives in the Store and Hangar, team score in the HUD. |

---

## 1. Principles

1. **Colour is information, never decoration.**
2. **Form disambiguates before hue does.** Opaque vs transparent, glow vs no glow, and sliver orientation all carry meaning independent of colour.
3. **Numeric data is column-aligned and does not reflow.** Aldrich digits are not tabular; `<mspace>` fixes this without touching the font asset.
4. **Glow and gradient are reserved for state, never for decoration.** Daily Deals glow to signal free vs purchasable; the PLAY button gradients because it is the primary action in the game. Nothing else glows.
5. **The corner sliver is the shape language.** Opposite-corner diagonal cuts, flippable. Nothing is rounded.
6. **Motion is short and functional.** Under 200ms for player-triggered; ceremony rationed to two moments.

---

## 2. Colour

### Main colours

| Name | Hex | RGB | Use |
|---|---|---|---|
| Light | `E6E9FF` | 230, 233, 255 | Active selections; all text except player names, buttons, emphasis; bounding boxes |
| Inactive Light | `5C5F70` | 92, 95, 112 | Inactive selections, buttons, regions |
| Inactive Dark | `25262D` | 37, 38, 45 | Inactive text on inactive buttons |
| CTA (Light) | `99FF80` | 153, 255, 128 | Call to action; player online status |
| Gold / Team 3 | `FFAE00` | 255, 174, 0 | Team identity; currency and purchase affordance |
| Ruby / Team 2 | `A600FF` | 166, 0, 255 | Team identity; tooltip boxes |
| Jade / Team 1 | `00D4FF` | 0, 212, 255 | Team identity; active toggles, sliders, scrollbars |
| "Black" | `00010A` | 0, 1, 10 | Popup background at varying opacity |

### Additional colours

**(Light)** variants may be used for text emphasis, player-name text, and subheadings.

| Name | Hex | Name | Hex |
|---|---|---|---|
| Neutral (Lightest) | `747BAD` | Neutral (Light) | `434C89` |
| Neutral (Dark) | `222645` | Neutral (Very Dark) | `00041F` |
| Jade (Light) | `80EAFF` | Jade (Dark) | `004D81` |
| Ruby (Light) | `D280FF` | Ruby (Dark) | `530080` |
| Gold (Light) | `FFD780` | Gold (Dark) | `805700` |

### Gaps — proposed, needs approval

| Role | Proposal | Rationale |
|---|---|---|
| Destructive / danger | `FF4B3A` | No red in the palette. Red is outside the team gamut. **Full-bleed fill only.** |
| Attention / unclaimed | Reuse CTA `99FF80` | A second novel hue would weaken the CTA. |

---

## 3. The team-colour contract

Three main colours carry both team identity and a UI function. **Green is the interactive hue; team colour is data only.**

| Role | Colour |
|---|---|
| Your team score / total | Team colour |
| Your avatar chip, vessel marker | Team colour, 2px border |
| Your domain panel background | Team (Dark) @ 12%, hairline Team (Light) |
| Objective arrow, owned crystals | Team colour, full saturation *(existing — keep)* |
| End-of-game victory banner | Team **(Light)** variant — see §11.9 |
| **Active toggles, sliders, scrollbars** | **Jade `00D4FF` per the guide — keep.** Jade players see a mild overlap; acceptable because controls and team data never share a component |
| Tooltip boxes | Ruby `A600FF` *(per the guide)* |
| Primary buttons, focus, selection | CTA `99FF80` |
| Player online status | CTA `99FF80` |
| Currency / purchase | Gold `FFAE00` |
| Panel borders, chrome | Neutral (Dark) `222645` |
| Body text | Light `E6E9FF` |
| App shell before a team exists | CTA `99FF80` |

---

## 4. Typography

**Aldrich** for headings and body. **Chakra Petch SemiBold** for buttons, almost always caps.

| Role | Family | Mobile @800 | **PC @1920** |
|---|---|---|---|
| Display | Aldrich | — | **48** |
| H1 | Aldrich | 24 | **36** |
| H2 | Aldrich | 20 | **28** |
| H3 | Aldrich | 16 | **22** |
| Body | Aldrich | 16 | **18** |
| Body small | Aldrich | — | **15** |
| Button | Chakra Petch SemiBold | 16 | **18** caps |
| Button small | Chakra Petch SemiBold | 12 | **14** caps |
| Data (large) | Aldrich + `<mspace>` | — | **44** |
| Data | Aldrich + `<mspace>` | — | **20** |
| Data (small) | Aldrich + `<mspace>` | — | **15** |

Emphasis: colour shift, or Chakra Petch italic.

**Tabular numerics:** wrap score, timer, count, and rank fields in `<mspace=Xem>`, where X is Aldrich's widest digit advance. Use a `TabularText` helper rather than scattering the tag.

---

## 5. Geometry — the corner sliver

**Correction to v0.1/v0.2:** the shape is not a single top-right chamfer.

| Rule | Detail |
|---|---|
| Sliver | A diagonal cut on **two opposite corners** |
| Flippable | Orientation may mirror (top-left/bottom-right ↔ top-right/bottom-left) per the guide |
| Buttons | Sliver on the short ends; may lengthen freely for long text |
| Cards, popups, nav tiles | Sliver at the same ratio, scaled to the surface |
| Border radius | 0 everywhere |
| Hexagons | Reserved for icon-only nav tiles and slider handles |

| Property | Value |
|---|---|
| Sliver (large surfaces) | 14px |
| Sliver (buttons, chips) | 10px |
| Hairline | 1px |
| Emphasis stroke | 2px |
| Accent strip | 3px × 44px |

**Spacing:** 8px base — `4, 8, 12, 16, 24, 32, 48, 64, 96`.

---

## 6. Layering

| Layer | Sort order |
|---|---|
| `transition` | 32767 |
| `consent` | 32766 |
| `veil` | 30000 |
| `overlay` | 10 |
| `modal` | 5 *(new)* |
| `hud` | 1 |
| `base` | 0 |

**Visibility is CanvasGroup alpha, never SetActive** — subscriptions are load-bearing.

---

## 7. Motion

| Token | Duration | Easing | Use |
|---|---|---|---|
| `micro` | 120ms | OutQuad | Hover, tint, focus |
| `std` | 200ms | OutCubic | Press, toggle, tab change |
| `panel` | 320ms | OutQuint | Modal, screen slide, toast |
| `ceremony` | 500ms+ | OutBack | Quest claim, end-game reveal only |

Staggers: 40ms per item, capped at 8.

---

## 8. Safe area

Mobile deferred; `SafeAreaFitter` ships dormant.

Background layer full-bleed; content layer constrained to `Screen.safeArea`; minimum edge inset 24 canvas units @1920 as a **floor**, authored as padding. Test aspects: **16:9 · 16:10 · 21:9**.

---

## 9. Icons

Line-weight monochrome, tinted per context.

Knowledge (XP) · Charge / Mass / Time / Space (elemental crystals) · Omnicrystal (combined; future paid currency) · Intensity (4 bar states) · Players (3 group sizes) · Settings · Clout · Volume Tracker · Train · High Score · Vessel Display · X Button · Locks.

Elemental glyphs match the vessel-HUD petal geometry — one system, not two.

**Missing, needed:** connection lost, error, warning, success, mute, host badge, kick, ready/waiting, favourite star.

---

## 10. Components

### 10.1 Buttons

**Opaque** on popups and wherever a button sits on a line. **Transparent** everywhere else. Exception: the Home screen play button is opaque.

| Variant | Transparent | Opaque | Use |
|---|---|---|---|
| Default | Dark teal tint, Jade border | `E6E9FF` fill, dark text | Standard action |
| Inactive | Dark grey, `5C5F70` border | `5C5F70` fill, `25262D` text | Unavailable |
| Cancel | Dark neutral | `747BAD` fill, dark text | Cancel; "coming soon" in store |
| Purchase | Dark amber tint | `FFAE00` fill, dark text | Any spend — icon + amount |
| CTA | Dark green tint | `99FF80` fill, dark text | Free / primary action |
| Play | Dark blue tint | Jade gradient fill | Launch a game |

Icons may sit before or after the label. Buttons lengthen for long text rather than wrapping.

### 10.2 Text input

Label above in small caps. Field is a slivered rect with a translucent Jade fill and a lighter top-right accent rule. Text in Aldrich, `E6E9FF`.

### 10.3 Popup

`00010A` panel, 1px `E6E9FF` border, slivered corners. Body copy left-aligned. **Action buttons straddle the bottom border**, half in and half out — a distinctive detail worth preserving. Confirm (purchase or CTA) sits left of Cancel.

### 10.4 Currency bar

Dark slivered pill, 1px light border. Icon + tabular number per currency. Single-currency and multi-currency (5-slot) variants both exist.

### 10.5 Secondary tab nav

Hexagonal icon tiles with a label beneath. Active: white border, white icon, white label. Inactive: dim fill, muted icon and label. Exactly one active.

### 10.6 Cards — Daily Deals and Arcade Explore

Slivered rects. Four states:

| State | Treatment |
|---|---|
| Free / CTA | Green outer glow, green button |
| Purchasable | Amber outer glow, amber price button |
| Locked | No glow, grey, lock icon, "Unlocks at clout N" |
| Purchased / owned | No glow, lavender `747BAD` button |

Arcade Explore cards additionally have a dimmed non-focus state for the D-pad grid.

### 10.7 Settings slider

Jade track, unfilled portion at low alpha, **hexagonal handle**. Filled portion left of the handle.

### 10.8 Settings toggle

**Two text labels, not a switch** — matches the shipped implementation. Active label `E6E9FF` with a Jade underline; inactive `5C5F70`, no underline.

### 10.9 End-of-game header

Angled banner with triangular end caps. Neutral `747BAD` for generic VICTORY / DEFEAT; team **(Light)** variant for `{DOMAIN} VICTORY` — Jade `80EAFF`, Ruby `D280FF`, Gold `FFD780`. End-cap triangles pick up the same hue at lower alpha.

### 10.10 Leaderboard

Alternating row fills. Rank medal for the top three, plain numeral below. Avatar, name in caps, tabular score right-aligned. Local player's row marked with `*`. Bottom rows fade out under the list edge.

### 10.11 Game Configure

Video preview panel left. Right: a **branching node tree** of slivered square tiles connected by hairlines — vessel, then intensity, then player count. Selected tile takes a white border and light fill; unselected are dim.

### 10.12 Port side navigation

Vertical stack of hexagonal icon tiles. Active takes a white border and white icon.

### 10.13 Class selection nav

Small slivered square cards with vessel art. Selected takes a white 2px border. Locked shows the lock glyph over dimmed art.

---

## 11. UIThemeSO field map

Chrome only. **Team colours stay in `SO_ColorSet`.**

| Field | Value |
|---|---|
| `textLight` | `E6E9FF` |
| `textInactive` | `25262D` |
| `inactiveLight` | `5C5F70` |
| `surfaceBlack` | `00010A` |
| `surfaceVeryDark` | `00041F` |
| `surfaceDark` | `222645` |
| `surfaceLight` | `434C89` |
| `neutralLightest` | `747BAD` |
| `cta` | `99FF80` |
| `danger` | `FF4B3A` *(proposed)* |
| `spacing[9]` | `4, 8, 12, 16, 24, 32, 48, 64, 96` |
| `sliverLarge` / `sliverSmall` | `14` / `10` |
| `hairline` / `stroke` | `1` / `2` |
| `durMicro` / `Std` / `Panel` / `Ceremony` | `0.12` / `0.20` / `0.32` / `0.50` |
| `staggerStep` / `staggerCap` | `0.04` / `8` |

---

## Version log

| Version | Change |
|---|---|
| 0.1 | Invented token system — **superseded** |
| 0.2 | Rebuilt on the studio palette and typography |
| 0.3 | Team names resolved (Jade cyan / Ruby purple / Gold amber). PC type scale set. Aldrich `<mspace>` for numerics. Chamfer corrected to the flippable corner sliver. Glow/gradient admitted as state carriers. Component library §10 added from the guide. |
