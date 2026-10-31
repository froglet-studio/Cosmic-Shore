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
- **Erasing** removes colours from the family that owns them. Each erased colour goes to the neighbour that scores
  highest there, if that neighbour's grown core still keeps a full band from every other family. Otherwise it becomes
  a flat cut through the colour, facing the old family's core: the smallest cut that keeps a cell convex. A colour
  within one band of its own family's anchors can only become band.
- **Reach** raises a family's score so its borders slide toward its neighbours (every reach is 0 in the adopted rule).

## The adopted definitions

Garrett's saved proposal "convex hulls with forbidden thickness", re-fitted with the gap-free solver above. It moves two
anchors from the game's values:

| family | anchor | game today | adopted |
|---|---|---|---|
| Fire | Engine ember rim | `#6B1C0A` | `#6A361C` |
| Jade | Jade UI | `#0EC0B6` | `#0CA899` |

Of the proposal's 132 erased colours, 45 went to a neighbour (23 of them are corners of the new cores and are kept as
"taken" colours). The rest became 9 cuts or stay band.

Share of the sRGB gamut (a 25³ grid), measured on the artifact:

| | game today (nearest anchor) | adopted (convex cells) |
|---|---|---|
| Fire | 6.6% | 5.8% |
| Gold | 1.7% | 4.2% |
| Lime | 9.4% | 4.6% |
| Jade | 4.3% | 14.9% |
| Blue | 34.5% | 10.8% |
| Ruby | 7.4% | 7.5% |
| Forbidden band | 36.0% | 52.3% |

The convex rule with the shipped anchors and no erasing puts about 38% of the gamut in the band. That is the true cost
of a 0.05 band in OKLab, where the whole screen gamut is about 1 tall and 0.3 wide. The adopted erasing raises the band
to 52%.

## Why the first convex version did not fill in

The first convex version (artifact version 3) put a band in the middle of each pair's gap independently. Fifteen
independent bands do not tile space, so pockets were left that no family owned. Erased colours were then only handed
over if the neighbour's whole hull cleared every other hull. The saved proposal ended with 65.5% of the gamut as band:
its 132 erase cuts handed only 11 colours to a neighbour. The adopted solver fits one partition for all six families
at once, so there are no pockets.

## What has not changed

- **The game still uses the nearest-anchor rule.** `Tools/Build/kite_mote_colors.py` (which derives the four Lime
  element shades) and `COLOR.md` §5b define it. Moving the game to the convex-cell rule means a new version of that
  script, which also re-derives the element shades.
- **No palette asset changed.** The two moved anchors exist only in the definitions. Putting them in the game means
  editing `vessel_colors.py` (the engine ember rim) and `OriginalColorSetSO.JadeColors.TrailHighlightColor` (Jade UI).
  The Jade hue is already an open decision (`COLOR.md` §6).
