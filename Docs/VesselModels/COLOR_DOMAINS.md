# Colour domains: the source of truth for the six families

**[`colour_domains.json`](colour_domains.json) is the source of truth** for which of the six colour families of
`COLOR.md` §1 (Fire, Gold, Lime, Jade, Blue, Ruby) a colour belongs to, and where the forbidden band between them lies.
Garrett shaped it by eye on 2026-10-31 in the **Colour Domains** lab (https://claude.ai/artifact/QT7nMWURMQgznfAnAg2FXh,
source `Docs/Studios/ColourDomainsLab.html`) and adopted it: "these 'shaped by eye' save is looking pretty good. its not
perfect but it is the best we've ever had. please update everything so that become the source of truth for the six
domains."

```
python3 Tools/Build/colour_domains.py classify '#00D4FF'   # which family, how far inside its border
python3 Tools/Build/colour_domains.py --report             # every anchor, the element shades, the shares
python3 Tools/Build/colour_domains.py --check              # the gate (below)
python3 Tools/Build/colour_domains.py --self-test          # proves the gate names each failure it exists for
```

## The rule

- **The space is OKLab** (`x = [L, a, b]`). The band's thickness, the margin (0.05, about 2.5 just-noticeable
  differences), is an OKLab distance. Only the lab's OKLab view shows the band as an even slab. RGB, HSV, HSL and CIELAB
  stretch it.
- **Every family is one convex cell, and the six cells fill OKLab with no gaps.** Each family has a score that rises in
  a straight line across OKLab, `A_f(x) = n_f · x + c_f` (the `cells` in the JSON). A colour belongs to the family that
  scores highest there.
- **The band** is every colour within half the margin of its cell's border with any other family. That border is the
  plane `A_f = A_g`, and the test is `(A_f − A_g) / |n_f − n_g| < margin / 2`. The band lies only between two families,
  never inside one, and it is the same thickness on every border. Where three or more cells meet, their bands overlap
  at the corner; that is the only place the band looks wider.
- **The cells are frozen.** They are exactly the boundaries the lab had fitted while Garrett shaped them. Nothing refits
  them; changing them means shaping again in the lab and adopting the result.

## The anchors: the game's colours, checked against the truth

Each family's **anchors** are the colours the game already draws for it, read live from the assets. They are listed
in the JSON with their source: the domain UI and signal colours, the CTA pair, the HUD ladder, the kite view's lime,
the vessel body, glass and engine ember, and black and white for Blue (every grey is Blue's, `COLOR.md` rule 1).

**They are the check, not the input.** Every anchor rests inside its own family, clear of the band, with one exception
the JSON names and explains. The engine ember face (`#300A05`) is within one band of black, so no convex border can
give it full clearance. It sits 0.0007 OKLab inside the band's edge. The four Lime element shades (Charge, Mass, Space,
Time, `OriginalColorSetSO.ElementalCrystalColors`) all rest inside Lime, 1.8–3.0 JND clear.

While shaping, two anchors were dragged by accident: the engine ember rim and Jade UI. Garrett, 2026-10-31: "i didn't
mean to move the anchors that was not intentional. the work i liked was the boundary definitions for each region and
the forbidden zones." Both are back at their game values in the truth, and both rest inside their own family.

## The UI's family colours

`Docs/UI_COLOR_RULES.md` gives five UI theme colours a family. Five of the six rest in their family:

| UITheme | family | colour | clearance |
|---|---|---|---|
| `cta` | Lime | `#99FF80` | 0.1 JND (on the edge) |
| `danger` | Fire | `#FF4B3A` | 3.2 JND |
| `controlActive` | Jade | `#00D4FF` | 0.7 JND |
| `purchase` | Gold | `#FFAE00` | 1.7 JND |
| `tooltip` | Ruby | `#A600FF` | 3.9 JND |
| `selection` | Blue | `#3DB2FF` | **in the band**, 0.0077 OKLab from Blue's border with Jade (needs 0.025) |

**Open decision:** move the selection blue into Blue, or reshape Blue in the lab. Until then it is listed as an
exception in the JSON, so any new drift still fails the gate.

## The gate: `colour_domains.py --check`

`--check` exits 1 when any of these is true:

- an anchor's colour in the game no longer matches the JSON (it names the asset), so a palette or HUD change that moves
  a family's colour is caught;
- an anchor is not inside its own family, clear of the band, unless it is listed as an exception;
- a Lime element shade has left Lime;
- a UI theme colour named for a family (`UITheme.asset`: `cta`, `danger`, `controlActive`, `purchase`, `tooltip`,
  `selection`) is not in that family, clear of the band, unless it is listed as an exception;
- the measured shares in the JSON are stale;
- the lab embeds a different truth.

`--self-test` plants each of those failures and confirms every one is named. The tool reads; it writes nothing.

When a palette colour moves on purpose, run `--check`. If the moved colour still rests in its family, update its
`rgb`/`hex` in the JSON. If it doesn't, the family has to be reshaped in the lab first.

## What the truth measures

Share of the sRGB gamut (a 25³ grid, `--report`), next to the old nearest-anchor rule the tools used before:

| | old rule (nearest anchor) | source of truth |
|---|---|---|
| Fire | 6.6% | 16.9% |
| Gold | 1.7% | 1.6% |
| Lime | 9.4% | 14.2% |
| Jade | 4.3% | 7.7% |
| Blue | 34.5% | 18.1% |
| Ruby | 7.4% | 5.5% |
| Forbidden band | 36.0% | 36.0% |

About a third of the gamut is band under either rule. That is the cost of a 0.05 band in OKLab, where the whole screen
gamut is about 1 tall and 0.3 wide; a narrower band is the lever for more usable colour.

Under the truth, the UI style guide's Jade `#00D4FF` (`Docs/STYLE_FOUNDATION.md`) falls inside Jade, 0.7 JND clear.
Under the old rule it sat in the band (the lab's finding 02).

## How the lab got here (what not to repeat)

1. **Nearest anchor** (`kite_mote_colors.py`, `COLOR.md` §5b): regions of any shape, bent by every extra anchor.
2. **Independent pairwise bands** (lab v3): fifteen bands placed pair by pair do not tile space. Pockets were left
   that no family owned; a saved proposal ended with 65.5% of the gamut as band.
3. **One fit for all six, with erase cuts** (lab v4): no pockets, but a colour no neighbour could take became a flat
   cut, and the space behind a cut was empty however deep it went. That was 17.1% of the gamut, and it is why the
   Jade/Ruby gap was thicker than the others.
4. **No cuts** (lab v5): the only empty space is the band. An erased colour goes to the neighbour that scores highest
   there; one that can't move without opening a wider gap stays. Garrett shaped "shaped by eye" on this version.
5. **Frozen** (lab v6, this): the fit is not unique, because different starting points reach different boundaries
   (84% agreement on a 41³ grid). So the truth stores the boundaries themselves, not the inputs that produced them.

## What the truth does not change yet

- **No palette asset changed.** Every anchor is the game's own colour.
- **The Lime element shades follow the truth** (since 2026-10-31): one lime at four lightnesses, picked by Garrett
  inside the truth's Lime (`COLOR.md` §5d). `kite_mote_colors.py --check` and this gate both hold all four inside Lime;
  Charge, the darkest, sits 0.2 JND inside its border.