# Colour domains: the convex-cell rule (adopted 2026-10-31)

The six colour families of `COLOR.md` §1 (Fire, Gold, Lime, Jade, Blue, Ruby), drawn as regions of colour space, and
the rule that decides which family a colour belongs to. Explored, shaped and adopted in the **Colour Domains**
artifact (https://claude.ai/artifact/QT7nMWURMQgznfAnAg2FXh). The adopted definitions are in
[`colour_domains.json`](colour_domains.json).

## What was adopted

Garrett, 2026-10-31: "I want these regions to be convex hulls that encroach on each other's territory, but respect
the thickness of the forbidden band. But those bands should separate regions from each other, not themselves, hence
the convex constraint." Then: "save off the proposal as the new rule."

- **The space is OKLab.** The band's thickness (the margin, 0.05, about 2.5 just-noticeable differences) is an OKLab
  distance. Only the OKLab view shows the band as an even slab; RGB, HSV, HSL and CIELAB stretch it.
- **Every family is one convex cell, and the six cells fill OKLab with no gaps.** Each family has a score that rises in
  a straight line across OKLab (`A_f(x) = n_f·x + c_f`). A colour goes to the family that scores highest there.
- **The band** is every colour within half the margin of a border between two cells. It only ever lies between two
  different families, never inside one.
- **The fit.** A family's core is its anchors plus the colours it took over by erasing. Blue's anchors include black
  and white, so it still owns every grey. The scores are fitted so every core colour sits as far from every border of
  its cell as it can, and at least half a band away. Two steps: a multiclass maximum-margin fit (squared hinge, Newton's
  method), then a refinement on the true OKLab distance to each border.
- **The only empty space is the band, and it is the same thickness everywhere.** Nothing else may leave a colour
  unowned (Garrett, 2026-10-31: "i was still able to create large gaps and the starting config has gaps that are
  thicker than others"). Where three or more cells meet, their bands overlap at the corner; that is the only place
  the band looks wider.
- **Erasing** removes colours from the family that owns them: each erased colour becomes a "taken" colour of the
  neighbour that scores highest there, and the cells are refitted. A colour that can't change hands without opening a
  wider gap (too close to its own family's anchors, or where the neighbour would crowd a third family) stays where it
  is. An anchor outweighs a taken colour 30 to 1 in the fit, so erasing never pushes an anchor into the band.
- **Reach** raises a family's score so its borders slide toward its neighbours (every reach is 0 in the adopted rule).

## The adopted definitions

Garrett's saved proposal "convex hulls with forbidden thickness", re-fitted with the gap-free solver above. It moves two
anchors from the game's values:

| family | anchor | game today | adopted |
|---|---|---|---|
| Fire | Engine ember rim | `#6B1C0A` | `#6A361C` |
| Jade | Jade UI | `#0EC0B6` | `#0CA899` |

Of the proposal's 132 erased colours, 37 went to a neighbour and 35 are now band. 52 could not move without opening a
gap, so they stay in their old family. 50 taken colours are kept (only the corners of each family's core matter).

Share of the sRGB gamut (a 25³ grid), measured on the artifact:

| | game today (nearest anchor) | adopted (convex cells) |
|---|---|---|
| Fire | 6.6% | 15.6% |
| Gold | 1.7% | 1.8% |
| Lime | 9.4% | 12.3% |
| Jade | 4.3% | 8.9% |
| Blue | 34.5% | 20.8% |
| Ruby | 7.4% | 4.6% |
| Forbidden band | 36.0% | 36.0% |

About 36–38% of the gamut is band under the convex rule. That is the cost of a 0.05 band in OKLab, where the whole
screen gamut is about 1 tall and 0.3 wide; a narrower band is the lever for more usable colour.

## Why the earlier convex versions left gaps

The first convex version (artifact version 3) put a band in the middle of each pair's gap independently. Fifteen
independent bands do not tile space, so pockets were left that no family owned. Erased colours were then only handed
over if the neighbour's whole hull cleared every other hull. The saved proposal ended with 65.5% of the gamut as band:
its 132 erase cuts handed only 11 colours to a neighbour. Version 4 fitted one partition for all six families at once,
so the pockets were gone, but it still turned every colour no neighbour could take into a flat cut. Behind a cut the
space was empty however deep it went: in the version-4 definitions, 17.1% of the gamut was empty space behind cuts,
and the Jade/Ruby gap was a band plus a cut. Version 5 (adopted) has no cuts.

## What has not changed

- **The game still uses the nearest-anchor rule.** `Tools/Build/kite_mote_colors.py` (which derives the four Lime
  element shades) and `COLOR.md` §5b define it. Moving the game to the convex-cell rule means a new version of that
  script, which also re-derives the element shades.
- **No palette asset changed.** The two moved anchors exist only in the definitions. Putting them in the game means
  editing `vessel_colors.py` (the engine ember rim) and `OriginalColorSetSO.JadeColors.TrailHighlightColor` (Jade UI).
  The Jade hue is already an open decision (`COLOR.md` §6).
