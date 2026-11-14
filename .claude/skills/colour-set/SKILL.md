---
name: colour-set
description: Make or fix a Cosmic Shore COLOUR SET - an unlockable palette where every domain is a rim pigment (the prism's edge, the trail, the HUD) and a face pigment (the prism's body) - from ANY inspiration (a photograph, an animal, a painting style, a mood, a film's palette) so that Fire, Gold, Lime, Jade, Blue and Ruby stay distinguishable and each still looks like its four-letter name. Loads the seven rules (Tools/Build/colour_sets.py), the territory atlas (where each family can live), the three voices a face may have, the three fundamental styles (Photic - one paint; Nocturne - lanterns; Nacre - pastel), the worked examples (Dart Frog from a photo, Blue-ringed Octopus, Neon Noir, Ukiyo-e, Dusk), the maker (Tools/Build/colour_set_maker.py - fit, sample, preview, atlas) and the writers that take a set into the game. Use on "/colour-set", "make a colour set / palette from <X>", "a colour set inspired by <photo / animal / film / style / mood>", "fix <set>", "why does <domain> look like <other domain>", "is this set legal", or before touching Docs/VesselModels/colour_sets.json, Docs/Studios/ColourSetsLab.html, an SO_ColorSet palette asset or the colour-set writers.
---

# Colour Set Protocol

Garrett, 2026-11-10: "you should be able to make new color sets from a variety of inspirations from a photograph,
style, mood, movie palette, an octopus, anything. however it should do its best to obey some rules: 1. Lime, Fire,
Blue, Jade, Ruby, Gold, those must be distinguishable and have rules that govern the territory of their regions in
color space 2. it must be obvious to the player based on a connection between their look and their four letter name.
The Photic color set does the best to define the spaces. the others should use this as an example to follow in
pursuit of as much variety as these rules afford." And: "nocturne is among the best color sets looking we have. lets
try to make sure it has some defined rules so it can be used as an example not an exception."

So a set has two promises, in this order: **every domain reads as its name**, and **within that, as much variety as
the rules allow**. An inspiration never outranks the name. When the two fight, the inspiration bends.

Owner: Garrett (colour, `Docs/VesselModels/COLOR_DOMAINS.md`). The Profile's colour-set rail, the editor themes and
the analytics console's themes are Shombith's: a set that changes reaches them, so queue it for his `/approve`.

## 0. What a set is, and where each pigment is painted

`Docs/VesselModels/colour_sets.json` holds every set; `Tools/Build/colour_sets.py` is the judge (`--check`).
A **pigment** is one paint from shadow to highlight: a hue, a `sat` (chroma divided by lightness: its direction from
black in OKLab), an `L` range and a spread `rho`. A **domain** is two pigments:

| pigment | the asset fields it paints (`author_colour_set_assets.py`) | where the player sees it |
|---|---|---|
| **rim** | `InsideBlockColor` (+ tiers), `TrailHighlightColor`, `ShipColor2`, `UIAccentColor`, `BrightCrystalColor`, spikes, skimmer, AOE; Fire's rim is `Danger`, Lime's rim is `BrightCTA` and `DarkCTA` | the prism's fresnel edge, the trail and vessel tail, the HUD and score panels, the danger edge, the free crystal |
| **face** | `OutsideBlockColor` (+ tiers), `ShipColor1`, `TrailCoreColor`, `DullCrystalColor`, spike dark, AOE fresnel | the prism's body (about 93 % of what a player sees of a prism), the tail's far end, the HUD panel backdrop |

Rims keep Photic's HDR brightness (times the set's `rim_brightness`); faces take their pigment's own mid L. Fire's
and Lime's faces have no field yet (`colour_sets.json` `open`). Photic (`PhoticColorSetSO`) is the hand-tuned
original every field's brightness, alpha and tier whiteness come from; it is measured, never written
(`colour_sets.py photic_reference`, first block of `--report`).

## 1. The seven rules (read `colour_sets.py`'s docstring for the exact maths)

| # | rule | what it protects |
|---|---|---|
| 1 | **band**: pigments of different domains at least `margin` (0.05 OKLab, 2.5 JND) apart | distinguishable |
| 2 | **lighting-proof**: their directions at least `pigment_gap` apart beyond both spreads | no light or shadow turns one domain's paint into another's (dark green IS shaded lime) |
| 3 | **form**: rim and face at least 0.12 apart in mid L (a set or style may add a hue contrast) | a prism reads as a solid |
| 4 | **the name**: every rim reads as its family in the six-family truth, clear of the band, as swatch, as LIGHT (prism edge) and as PAINT (the same numbers read as sRGB: the HUD and tails); Lime also at the free crystal's body brightness | look ↔ name, on every screen |
| 5 | **communication** (sets with a `vision` only): as that viewer sees the painted colours, rims (as light AND as the HUD paints them), faces and rim/face pairs stay `vision_margin` apart; it stands in for rule 7 | the colour-blind sets |
| 6 | **the voice**: each face is its **family**'s colour, its rim's own **paint**, or **quiet** (whole pigment at or below the night line, L 0.40) | the body never names another domain |
| 7 | **apart**: every two rims at least 0.10 apart as painted, as light and as paint | distinguishable edges, trails and HUD colours |

**No rule is waived** (Garrett, 2026-11-10: "we want the colour-blind sets to fulfill their version of the same
criteria to the best of our ability without someone with each type to iteratively confirm"). A set made for another
vision (protan, deutan, tritan, achromat, low vision) keeps every rule, and rules 4 and 6 are judged **as its viewer
sees them**:

- **4V, the name:** the viewer has learned the six names from the game's own colours, so the truth's anchors are seen
  through the same vision model (`vision_prototypes`: Machado 2009 for the three dichromats, Rec. 709 luminance for
  the achromat, half the chroma for low vision). Every rim, as LIGHT and as the HUD paints it, must look nearer its own
  family's anchors than any other family's by `name_margin` (default 0, OKLab).
- **6V, the voice:** a face is *family* (nearer its own family's anchors, as seen), its rim's *paint* (the cones
  overlap; no vision changes that) or *quiet* (its top lightness, as seen, at or below the night line).
- **Rule 5 is the vision set's rule 7:** its rims apart as light and on the HUD, for its viewer.

Where no lawful set exists for those eyes (a deuteranope sees Fire's and Gold's rims on one yellow-blue line; an
achromat has six names in one dimension), the maker's `"objective": "name"` finds the set whose rims look as much like
their names as the viewer allows, and the set declares the margin it reached as its `name_margin` (deuteranopia
-0.021, achromatopsia -0.045: how far short the worst rim falls). The gate holds it there, so a later change can only
make it better. `--report` prints every rim's margin and which family it would be mistaken for. These are models,
not people: a player with that vision is the only real test (`Docs/UNITY_VERIFICATION_CHECKLIST.md`).

## 2. The territories: where each family can live (run `python3 Tools/Build/colour_set_maker.py atlas`)

The six-family truth (`colour_domains.json`, Garrett shaped it by eye) is not six even slices. Its hard facts:

- **Below L 0.40 only Fire and Blue exist.** That is the night line. A dark face of any other hue cannot name a
  domain, so it reads as night under its rim (the voice *quiet*).
- **Gold exists only at L 0.70 and above**, hue 66-108, chroma 0.09-0.16. There is no dark gold (dark gold is brown:
  Fire or the band). A Gold *rim* must sit at **hue 82-96, sat 0.10-0.19**: the HUD reads the rim's numbers as sRGB,
  which turns any orange Gold into Fire (every vision set shipped that way until 2026-11-10, and Nocturne's honey sat in
  the band).
- **Ruby exists only at L 0.50-0.70 with chroma 0.20 or more**, hue 282-340. There is no pastel Ruby: the truth gives
  every pale violet to Blue (the glass) and every pale pink to Fire. In a light set Ruby is the one jewel.
- **Jade** lives at L 0.40-0.90, hue 150-222, chroma 0.085 or more; its rim at hue 164-210, sat 0.11-0.19.
- **Lime** lives at L 0.40-0.95, hue 106-146, chroma 0.095-0.14 or more (more as it gets lighter).
- **Fire** lives at L 0.20-0.80, hue 322-68, chroma 0.07 or more; nothing above L 0.85. Rims at hue 344-46.
- **Blue** holds every grey at every L, plus the blues (hue about 210-330 in the light, 218-320 dark).

## 3. A face's voice: the three ways a face may look (rule 6)

The face is most of a prism, so it decides how the prism reads unless it is dark. Pick one voice per face:

| voice | test | model | when an inspiration wants it |
|---|---|---|---|
| **family** | its swatch rests in its own family | Photic's Blue (a deeper blue under a pale blue rim); Dart Frog's Azureus | the inspiration's body colour already is the family's colour |
| **paint** | the face's and the rim's pigments share a direction (their cones overlap): the rim's own paint in shadow (or light) | Photic's Jade, Ruby, Gold (one paint, two lights); Ukiyo-e's bokashi | one-colour subjects; graded printing; any domain whose body must stay its rim's hue |
| **quiet** | its whole pigment at L 0.40 or below | every Nocturne face; Dart Frog's grounds | a contrasting second colour: it may be ANY hue, as long as it stays in the night |

A face that is light, other-hued and not its family's is **loud** and breaks rule 6: that was Dart Frog's bronze,
indigo and forest bodies (a Jade prism that read as Gold) and every Nacre face (a Gold prism that read as Blue).

## 4. The three fundamental styles (`colour_sets.json` `styles`)

Photic, Nocturne and Nacre are one family: day, night and pearl. A set may name one and keep its rules on top of the
seven; or name none and mix voices domain by domain (Dart Frog, the Octopus).

- **Photic - one paint.** Faces *family* or *paint*. Every domain is one hue at two lightnesses; the clearest
  definition of the six territories (its colours are the truth's anchors). Example sets: Photic (the asset),
  Contrast, Ukiyo-e.
- **Nocturne - lanterns.** Faces *quiet*, at least 90 degrees of hue from their rim, all six within 0.10 of each other
  in mid L (one night band, no body outshines another), rims at mid L 0.60 or more. The rim glows in its family's
  hue and carries the name; the body is a dark jewel of the complementary side. Example sets: Nocturne (the default),
  Neon Noir.
- **Nacre - pastel.** Faces *family* or *paint*, mid L 0.50-0.74 (the gameplay bloom lifts them to pastel: a face
  painted much lighter washes out to white - Nacre's first cut did), `rim_brightness` 0.5-0.85 so the rims are a
  sheen, not a glow. Each body is a soft tone of its own family with a small iridescent hue shift to its rim; Ruby
  keeps a clear amethyst because no pastel Ruby exists. Example set: Nacre.

## 5. Making a set from an inspiration

1. **Collect the inspiration's colours.**
   - A photograph: `python3 Tools/Build/colour_set_maker.py sample <image> --box x0,y0,x1,y1 --k 10` (crop to the
     subject; a busy background swamps the clusters). It prints hue, sat and L range per cluster and its family.
   - An animal, a film, a style, a mood: name its colours in words first (the blue-ringed octopus: sand skin,
     electric rings, black outlines, a rust alarm flush; neon noir: red, sodium, toxic lime, cyan, electric blue,
     magenta over wet asphalt), then turn each word into a hue, sat and L with the atlas beside you.
2. **Give each domain its colours by the NAME, not by the order the inspiration shows them.** The atlas says which
   inspiration colour can be which domain's rim. A subject with fewer than six colours borrows: what it shows when it
   changes (a flush, an iridescence), its shadows, its surroundings.
3. **Choose each face's voice** (section 3). A contrasting second colour goes quiet; a body of the domain's own colour
   is family or paint.
4. **Write the draft** in `colour_sets.json`'s shape (one set, swatches optional; `"pin": true` keeps a pigment
   fixed; a style key adds its rules) in your scratchpad.
5. **Fit it:** `python3 Tools/Build/colour_set_maker.py fit draft.json --out fit.json`. It moves each pigment as
   little as it can until every rule holds (the asset writer's field check included: no whitened tier may land in
   another domain's pigment), rounds to the repo's precision and re-checks with the judge. Read what
   moved (a pigment the solver squeezes to `rho` 0.01 or less is fighting a neighbour: give one of them another hue
   or lightness and fit again). `"objective": "vision"` maximises rule 5 instead (the vision sets).
6. **Look at it:** `python3 Tools/Build/colour_set_maker.py preview photic nocturne --draft fit.json --out sheet.png`
   draws every set as the writer paints it (rims HDR, faces at their L, the gameplay bloom, no tonemapping): rows
   Jade, Ruby, Gold, Blue (plain, shielded, super-shielded), then danger, a free crystal and Blue super-shielded.
   Read it: does every row read as its name? does any tier wash out to white? does danger still warn? Send the sheet
   with `SendUserFile`; say that the final look, bloom and feel are judged in the editor.
7. **Land it.** Add the set to `colour_sets.json` (`sets` for a set the game ships, `examples` for a worked example
   that is never written), paste it into the lab's `SETS_RAW` / `EXAMPLES_RAW`
   (`Docs/Studios/ColourSetsLab.html`; `colour_sets.py --check` compares them), give the lab its picture
   (`colour_set_maker.py hero <key> --out <key>.jpg`, embedded in the lab's `HERO` as a data URI), then run the writers
   (section 7) and republish the lab artifact.

## 6. The worked examples (all in `colour_sets.json`, all held to the seven rules by `--check`)

| set | inspiration kind | style / voices | the move that made it lawful |
|---|---|---|---|
| Photic | the original | one paint | the model (measured, never written): keeps every rule but one, its Jade prism rim is an azure in the band (COLOR.md §6, deferred) |
| Nocturne | a feeling (night) | lanterns | its Gold rim moved 4 degrees toward yellow (hue 83 to 87) so the HUD gold reads Gold |
| Nacre | a material (mother of pearl) | pastel | faces moved into their own families and painted at L 0.5-0.7, so the bloom lifts them to pastel instead of white |
| Dart Frog | a photograph (Garrett's green-and-bronze frog) and four frogs | quiet grounds, Azureus in family | every frog's second colour taken into the night; the Azureus is blue on blue |
| Blue-ringed Octopus (in the game since 2026-11-11) | an animal | mixed: sand skin as one paint, black for the quiet faces | an animal with three colours borrows its alarm flush and its iridescence |
| Neon Noir (in the game since 2026-11-11) | a film genre's palette | lanterns | each neon's puddle is its complement in the night; sodium amber stays in Gold's window |
| Ukiyo-e (in the game since 2026-11-11) | a painting style | one paint | bokashi is the paint voice: the face is the rim's pigment printed deeper |
| Dusk (in the game since 2026-11-11) | a mood | muted rims, quiet faces | rims stop at their families' chroma floors; Ruby is never dusty; the greys had to spread apart; taken into the game, its Lime face (Heather) moved 1 degree so the writer's whitened Ruby fields stay out of it |
| Contrast | a purpose (typical vision, as far apart as the rules allow) | one paint | its faces were other families' colours; held to rule 6 every body is its own family, and counting the HUD it is further apart than before (11.0 JND against 6.5) |
| the five colour-blind sets | a viewer's eyes | rules 4 and 6 as the viewer sees them (4V, 6V), rule 5 for rule 7 | their waivers are gone: every rim now looks like its own name to its viewer (protan, tritan, low vision) or as nearly as those eyes allow (deutan, achromat, declared `name_margin`); the faces went quiet in the viewer's own lightness; rule 5 counts the HUD; re-balanced with `"objective": "vision"` and `"name"` |

## 7. Taking a set into the game (each writer has `--check`)

```
python3 Tools/Build/colour_sets.py --check                 # the seven rules, the swatches, the lab's copy
python3 Tools/Build/colour_trace.py --build                # the traced colours (every colour outside the blocks)
python3 Tools/Build/author_colour_set_assets.py            # Assets/_SO_Assets/Color Palettes/<Name>ColorSetSO.asset (+ TracedColors)
python3 Tools/Build/colour_trace.py --write-photic         # Photic's TracedColors (the baked values)
python3 Tools/Build/colour_trace.py --check                # every colour the game draws is in every set
python3 Tools/Build/author_colour_set_catalog.py           # the Profile's catalog entry (its words are authored in the asset)
python3 Tools/Build/author_editor_themes.py                # the editor windows' themes
python3 Tools/Analytics/console/make_themes.py             # the analytics console's themes
```

To take a worked example into the game, move it from `examples` to the END of `sets` (the writer keeps every value of
every set apart in that order, so appending moves no shipped set's colours), give it a `track`, then run the writers
above (2026-11-11: Blue-ringed Octopus, Neon Noir, Ukiyo-e, Dusk). A new set needs a catalog row in `author_colour_set_catalog.py` `SETS` (and its words in the catalog asset); a
changed pigment name needs the asset's `pigments` words updated by hand (the writer stands down on words). Then
`--self-test` on `colour_sets.py` and `colour_set_maker.py`, the `/ship` gates, and an entry in
`Docs/UNITY_VERIFICATION_CHECKLIST.md`: no palette has been judged in the editor until someone flies it.

## 8. Every other colour the game draws: the trace (`Docs/VesselModels/COLOR_TRACE.md`)

A set is not only its 12 pigments. Every colour the game draws outside the palette blocks (the HyperSea sky, materials,
particles, the UI's own colours, configs and code) is a `TracedColor` on every set (Garrett, 2026-11-10: "no pixel
anywhere should ever receive a color that can be traced somewhere but a colorset"). Photic holds the baked values;
every other set computes its own from its pigments: greys keep, UI grounds take the set's Jade face hue, everything
else of family F wears the set's F **rim** at the original's brightness and whiteness, palette-baked materials take the
set's own field. A new set gets all of them from the writer; to retune one for a set, put it in that set's `traced`
(`{"<id>": "#RRGGBB"}`) and run the writer. The writer then keeps every value of every set naming one colour (the
runtime finds a colour by its value, from whichever set painted it) and UI values clear of every set's UI theme
tokens, moving a clash to the nearest free 8-bit value; so **a new set is written with every other set in one run**
(`author_colour_set_assets.py`, then `colour_trace.py --write-photic`, then `colour_grade.py`, which writes the set's
texture grade: the same rule on every texel of every picture, a 33^3 table per role), and `colour_trace.py --check`
and `colour_grade.py --check` fail if any value of any set could be misread or a table drifted. The runtime painters are `UIThemeLive` (the UI) and `WorldColorTrace` (the world); code
and configs read `ColorTrace.Retrace(role, colour)`. No colour is left in shader code (each is a material colour,
saved at the sRGB encoding of the old constant; `/asset-surgery` `references/SHADERGRAPH.md`), every texture follows
the set through the grade (section 1.7 of that doc), and what is still outside the trace (caches copied before a
wear, particles alive at one, baked light probes) is its section 2.

## 9. Traps (each one cost a set)

- **A loud face names the wrong family.** The face is 93 % of a prism. Dart Frog's mid-dark bronze made Jade read as
  Gold; Nacre's lavender made Jade read as Ruby. Rule 6.
- **The HUD reads the rim's numbers as sRGB.** `TrailHighlightColor` is painted in linear light, but
  `GetDomainUIColor`, the score panels and the vessel tails (`TailGradient`) read it as sRGB, which darkens the middle
  channel and turns orange gold into Fire. Rule 4's PAINT reading. (`ColorSetUITheme` reads it as light; both are on
  screen.)
- **Bloom turns light faces white.** The gameplay profile blooms from max channel 0.2, clamped at 0.5, intensity
  2.5: a face painted above about L 0.72 washes out, and its shielded tiers (brighter, whitened) go pure white, so
  every domain's shielded prism looks the same. The preview shows it.
- **Dark green beside a lime rim is one paint** (rule 2). So is dark red beside a fire rim, dark orange beside gold.
  Put a contrasting face on the other side of the circle, or far from every rim's direction.
- **Near-black faces collide.** Blacks of different hues are a few JND apart; give the quiet faces different
  lightnesses or real chroma, or the band squeezes them (the solver will, and tells you by shrinking `rho`).
- **Ruby cannot be pale, Gold cannot be dark or orange, Fire cannot be near-white.** Section 2.
- **A light set weakens danger.** Danger is the Fire rim over the domain's shielded face; over a pale body the edge
  is all that warns. Give a light set's Fire rim real chroma (Nacre's coral is sat 0.26).
- **Fire's and Lime's faces are never painted** (no field yet): design them, but do not lean on them.
- **A whitened rim walks toward grey along its own hue.** The writer paints the brighter tiers with Photic's
  whiteness, so a rim's fields sweep from its saturation down toward grey on its own hue line, and a quiet face of the
  same hue and less chroma catches them: Dusk's Heather (Lime face, hue 316) took Wild Plum's `AOEConicEdgeColor`
  (Ruby rim, hue 318). The seven rules compare pigments only; `author_colour_set_assets.py --check` compares every
  field, for the examples too since 2026-11-11.
