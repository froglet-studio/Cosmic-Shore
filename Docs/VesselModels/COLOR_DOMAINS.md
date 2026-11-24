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
Time, `PhoticColorSetSO.ElementalCrystalColors`) all rest inside Lime, 1.8–3.0 JND clear.

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

## Colour sets: unlockable palettes with two pigments per domain

[`colour_sets.json`](colour_sets.json) holds the unlockable colour sets: **Nocturne** (2026-11-03, **the game's default
palette**), **Dart Frog** (paid track, 2026-11-02), **Nacre** (the light set, 2026-11-08), **Contrast** and the five
vision sets (2026-11-05), plus four worked **examples** that are never written to the game. A domain there is two
pigments, a rim (the prism's edge, the trail, the HUD) and a face (the prism's body), and a pigment is one paint from
shadow to highlight: a cone segment around a ray from black in OKLab.

**Seven rules hold every set** (2026-11-10; Garrett: the six families "must be distinguishable and have rules that
govern the territory of their regions in color space", and "it must be obvious to the player based on a connection
between their look and their four letter name"). `Tools/Build/colour_sets.py --check` is the gate:

1. **the band**: pigments of different domains at least 0.05 apart;
2. **lighting-proof**: no light or shadow turns one domain's paint into another's;
3. **form**: rim and face 0.12 apart in lightness;
4. **the name**: every rim reads as its own family of this truth, clear of the band, as its swatch, as light (the
   prism's edge) and as paint (the HUD, the score panels and the vessel tails read `TrailHighlightColor` as sRGB, which
   turns an orange Gold into Fire);
5. **communication** (a set made for a vision): as that viewer sees the painted colours, rims (as light and on the
   HUD), faces and each rim against its face stay the set's margin apart;
6. **the voice**: each face is its family's own colour, its rim's own paint, or **quiet**: wholly below the night line,
   L 0.40, where this truth holds only Fire and Blue, so a dark face of any hue reads as night under the rim that carries
   the name;
7. **apart**: every two rims at least 0.10 apart as painted.

No rule is waived (Garrett, 2026-11-10). The five colour-blind sets keep rules 4 and 6 **as their viewer sees them**:
the viewer learned the six names from this truth's anchors through their own eyes (the same vision model), so a rim
must look nearer its own family's anchors than any other's, as light and on the HUD, and a face is its family's, its
rim's paint, or quiet in the viewer's own lightness; rule 5 stands in for rule 7. For a deuteranope and an achromat no
lawful set exists (the anchors crowd one line for them), so those two declare the best name margin the maker found and
the gate holds it there. Three **styles** add rules of their own: **Photic** (one paint per
domain; the original, whose colours are this truth's anchors), **Nocturne** (lanterns over quiet jewel faces in one night
band) and **Nacre** (soft faces of each family's own colour, painted so the gameplay bloom lifts them to pastel).

What the rules taught, measured 2026-11-10:

- **The territories are uneven.** Gold lives only at L 0.70 and above, and a Gold *rim* only at hue 82-96 (the HUD's
  paint reading turns anything more orange into Fire); Ruby lives only at L 0.50-0.70 with chroma 0.20 or more, so no
  pastel Ruby exists; below L 0.40 only Fire and Blue exist. `python3 Tools/Build/colour_set_maker.py atlas` prints
  every family's window.
- **The face decides how a prism reads**, unless it is dark. Dart Frog's mid-dark faces made its Jade prisms read as
  bronze and its Blue as forest green; Nacre's light faces made its Gold read as periwinkle and bloomed to white. Both
  are re-derived: Dart Frog's frogs keep their warning colours as rims over their grounds taken into the night (the
  Azureus blue on blue), Nacre's bodies are soft tones of their own families.
- **Nocturne needed one move**: its Gold rim 4 degrees toward yellow (hue 83 to 87), so the HUD shows Gold instead of
  the Gold/Fire band (the style guide's gold token followed, `#FF8F11` to `#FF9B11`).
- **The colour-blind sets' HUDs were never measured for their own viewers**: an achromat could tell Gold's and Ruby's
  HUD colours apart by 1.5 JND, a deutan Fire's and Gold's by 2.4. Rule 5 now counts the HUD and the sets are
  re-balanced; Contrast, held to every rule, makes every prism its own family at two lightnesses.
- **Photic, measured** (`colour_sets.py --report`, first block), keeps every rule but one: its Jade prism rim is the
  azure §6 of `COLOR.md` defers.

**Making a set** from a photograph, a style, a mood, a film's palette or an animal is the **`/colour-set`** skill
(`.claude/skills/colour-set/SKILL.md`): the atlas, a photograph sampler, `fit` (the nearest set that keeps every rule),
`preview` and `hero` (the sets as the writer paints them, with the gameplay bloom), in `Tools/Build/colour_set_maker.py`.

**Adding or changing a set reaches four writers; run all of them** (each has `--check`): `author_colour_set_assets.py`
(the palette asset), `author_colour_set_catalog.py` (the Profile's catalog entry; its words live in the asset),
`author_editor_themes.py` (the editor windows' themes) and `Tools/Analytics/console/make_themes.py` (the analytics
console's themes); a change to the live set (Nocturne) also reaches `Tools/Build/froglet_style.py` (the style guide's
tokens). Shaped in the Colour Sets lab (https://claude.ai/artifact/7bzYVFEyn4NKJUcT9yyJuf, source
`Docs/Studios/ColourSetsLab.html`), which judges by the same seven rules and shows Photic and the examples. This truth is
unchanged by them.

`Tools/Build/author_colour_set_assets.py` writes each set as an `SO_ColorSet` asset (`<Name>ColorSetSO.asset` in
`Assets/_SO_Assets/Color Palettes/`). Every field takes its hue from its domain's rim or face pigment and keeps
`PhoticColorSetSO`'s alpha and tier whiteness; rims keep the original's HDR brightness (times the set's
`rim_brightness`) and faces take their pigment's own lightness (the face is most of a prism, so it decides how the prism
reads); `--check` fails on drift or on a field in another domain's pigment.

## What the truth does not change yet

- **The anchors are Photic's.** `PhoticColorSetSO` (named `OriginalColorSetSO` until 2026-11-03) was the default when
  the truth was shaped, and the gate still reads the anchors from it. The default is now Nocturne, whose rims rest in
  these families (rule 4 of the colour sets) and whose faces deliberately do not.
- **The Lime element shades follow the truth** (since 2026-10-31): one lime at four lightnesses, picked by Garrett
  inside the truth's Lime (`COLOR.md` §5d). `kite_mote_colors.py --check` and this gate both hold all four inside Lime;
  Charge, the darkest, sits 0.2 JND inside its border.