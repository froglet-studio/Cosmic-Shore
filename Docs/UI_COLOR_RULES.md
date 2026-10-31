# UI colour rules (Garrett, 2026-10-24)

> **Which family a colour belongs to** (and the forbidden band between families) is decided by the source of truth,
> `Docs/VesselModels/colour_domains.json` (`COLOR_DOMAINS.md`). `Tools/Build/colour_domains.py --check` holds this
> page's family colours to it (`UITheme.asset` cta, danger, controlActive, purchase, tooltip, selection); the selection
> blue `#3DB2FF` sits in the band between Blue and Jade, an open decision recorded there.

Garrett's design feedback on the style-B menu and HUD, relayed by Shombith on 2026-10-24, and what it means in the
assets. These rules hold for every UI surface: Menu_Main, the in-match HUD and canvases, the start flow, Maelstrom.
`python3 Tools/Build/check_ui_colors.py` (`--self-test`) is the gate for the parts the assets can show.

## The six colour families

| Family | Tokens | Means | Never |
|---|---|---|---|
| Domain | Jade, Ruby, Gold (the domain palette) | a team or a domain | anything else |
| Neutral (blue) | `NeutralLightest`, `SurfaceLight`, `SurfaceDark`, `TextLight`, `ControlActive` | low-priority information and structure | a call to action |
| Call to action (lime) | `Cta`, the `Play` / `Cta` button variants | categorically good, no caution: launch, join, accept, confirm, earned rewards, "online", a done tick | an eyebrow, a heading or decoration |
| Danger (fire) | `Danger`, the `Danger` button variant | danger, caution, a problem: delete my data, a guest-account warning, why a game cannot launch | anything merely important |
| Spend (gold) | `Purchase`, the `Purchase` button variant | spending something: a purchase, an upgrade, any button that confirms using a resource; the crystal glyph (the currency) | a first place, a highlight, a reward amount |
| Selection (sky blue) | `Selection`, the SelectionRim material | the one thing that is actually selected (a picked tile, the open tab's still underline) and the keyboard / pad focus ring (decided 2026-10-24: focus keeps the selection blue, no look of its own) | a playing track, your own row, a favourite, a spinner, a "featured" card |

Domain colour notes from Garrett: Ruby can mean risk-taking. Jade is open and may trend toward order, control and
arrangement.

## Elements carry no colour

The issue Garrett raised is not colour in general: it is tying elements (Charge, Mass, Space, Time) to fixed colours,
which confuses them with the families above. **An element is told by its glyph's shape, by animation and by effects,
never by a fixed colour.**

- The element tokens (`ElementCharge` 13 … `ElementTime` 16) stay in `UIColorToken` only so serialized values keep
  their meaning; nothing draws in them.
- The element rims (`ElementRim_*.mat`, a comet of an element's light running round a panel) are deleted; the guids are
  kept so the gates can catch a stray reference. The rim shader stays because the selection's two materials run on it.
- The `Mass` / `Space` / `Time` button variants are retired and draw as `Default`.
- The in-match ability lockup's `elementColour` and `lavaMotion` default to off (`AbilityLockupStyle.asset`).
- The Hangar pages, the vessel info card and the pilot record read a serialized neutral token instead of
  `AbilityLockupStyleSO.ColourFor`.

Still on element colours, and not changed here because they are other people's HUD code (ask before re-tuning):
`StarlingHUDView`, `GobyPistolButtons`, `OrcaHUDController`, `TouchControlsHud`, `AbilityLockupView` call `ColourFor`.
With `elementColour` off the lockup itself is neutral; those vessel HUDs still tint by element.

Hub windows: the strip's four words (Toy Box, Arcade, Arena, Voyage) all read in the one neutral blue
(`NeutralLightest`), the shown one included; its still underline alone marks it (Shombith, 2026-10-24).

## Attention economy

"Every pixel is important." Nothing in the UI moves for its own sake while a player is reading or playing.

- **Still:** every tab underline (the top nav's Home / Hangar / Ark bar ran the selection sweep and read as frozen next
  to the hub strip's bar; both are plain bars now), the panel lattice (no drift, no element pulses, no twinkles), the presence orb (no orbiting dots, no
  spinning ring), the party bonds (solid), the Hangar bay (no breathing rings, no scan line, no motes), floating
  avatars and art, the settings gear, element halos (`KitElementHalo.breathe` off).
- **The one background motion: the sea lines** behind a whole window (Arcade, Arena, Mission, Toy Box, Configure,
  Profile, Leaderboard; `Docs/MENU_REDESIGN_B.md` §67). Shombith chose it on 2026-10-25 from the canvas's Rev 12 board
  ("keep it a bit slow"): thin wave lines over a 95 % veil, three layers sliding one 480-unit loop per 90 / 68 / 45 s.
  Rev 13 (2026-10-25, "make the blue a little bit brighter and use gradients"): the lines shade sky to periwinkle
  across the window and a deep-blue wash fades from the top. `author_ui_lattice.py` fails any layer faster than 40 s a
  loop and a sea that has gone back to one flat colour. Since §70 the sea is the only background anywhere (the crystal
  lattice is retired).
- **The card pictures drift** (`Docs/MENU_REDESIGN_B.md` §72; Shombith 2026-10-25: "make it have some animation ... for
  all of the gamemode toy cards", then "implement a" on the Rev 18 board). A game card's art moves at most 5 x 3 canvas
  units on a 40 s Lissajous path inside its overscan (`Card_ArtDrift.mat`); a toy's portrait bobs 4 units over 8 s
  (`Card_PortraitBob.mat`). It is the cards' one motion and stays this small: the generator fails a faster loop, and
  `author_menu_arcade.check_card_a` fails an overscan the drift could cross. This is an explicit exception to "nothing
  moves for its own sake" - raise it with Garrett if the cards ever read as busy.
- **Kept, because they mean something:** progress spinners while something loads or connects, the music EQ while a
  track plays, the hint that teaches a control, the START sweep, the current quest segment's spark, window entrances
  and the lattice crystallising in (transitions are meaningful moments; Shombith proposed putting the dramatic visuals
  there and Garrett agreed it could work).
- **Strong juice is reserved** for upgrades, powered states and the call to action.

## Where this is enforced

| What | Where |
|---|---|
| No element tokens, rims, literal element colours or element button variants in any UI prefab or scene; the lockup's element colour off | `Tools/Build/check_ui_colors.py` |
| The kit palette (Play / Cta lime, Purchase gold, Danger fire, retired variants = Default) | `author_ui_kit_prefabs.py` validation |
| The lattice / orb / bond materials still; no rim materials written; the sea lines slow (40 s a loop or more) | `author_ui_lattice.py` validation |
| Period 0 really holds the shaders still; the sea lines move a fraction of a line per frame and loop without a seam; the card drift stays inside its reach and loops | `lattice_preview.py --check` |
| The card art drifts on `Card_ArtDrift.mat` inside a band with enough overscan; the hull on a pale plate; the toy portrait on `Card_PortraitBob.mat` | `author_menu_arcade.py`, `author_menu_arena.py`, `author_menu_toybox.py` validation |
| Each screen's colour choices | the screen's `author_*.py` generator and review against this page |
