# UI colour rules (Garrett, 2026-10-24)

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
| Selection (sky blue) | `Selection`, the SelectionRim / SelectionBar materials | the one thing that is actually selected (a picked tile, the open tab) | a playing track, your own row, a favourite, a spinner, a "featured" card |

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

## Attention economy

"Every pixel is important." Nothing in the UI moves for its own sake while a player is reading or playing.

- **Still:** the lattice backdrop (no drift, no element pulses, no twinkles), the presence orb (no orbiting dots, no
  spinning ring), the party bonds (solid), the Hangar bay (no breathing rings, no scan line, no motes), floating
  avatars and art, the settings gear, element halos (`KitElementHalo.breathe` off).
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
| The lattice / orb / bond materials still; no rim materials written | `author_ui_lattice.py` validation |
| Period 0 really holds the shaders still | `lattice_preview.py --check` |
| Each screen's colour choices | the screen's `author_*.py` generator and review against this page |
