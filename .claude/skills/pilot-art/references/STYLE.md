# The house style: what the 32 painted portraits do, measured, and what "better" means

Measured 2026-11-07 with `Tools/Build/pilot_art/measure.py --refs` over `Assets/_Graphics/Pilots/*.png`
(the envelope: `Docs/Pilots/art/style_envelope.json`; the audit sheet: `Docs/Pilots/art/sheets/references.png`).

## 1. The envelope (fractions of the 220x280 frame unless said)

| metric | min | p10 | median | p90 | max | what it is |
|---|---|---|---|---|---|---|
| top | 0.000 | 0.004 | 0.009 | 0.126 | 0.318 | the head's top: the set starts at the top edge |
| bottom | 0.989 | 0.993 | 1.000 | 1.000 | 1.000 | the boots: on the bottom edge |
| height | 0.671 | 0.874 | 0.988 | 0.996 | 1.000 | Dozer (crouching frog) and Echo (winged) are the short ones |
| width | 0.400 | 0.512 | 0.673 | 0.958 | 0.991 | wings, tails and bows make the wide ones |
| head_cx | 0.320 | 0.358 | 0.478 | 0.550 | 0.618 | the head sits a hair left: pilots turn left, tails hang right |
| torso_cx | 0.309 | 0.371 | 0.482 | 0.531 | 0.568 | |
| coverage | 0.167 | 0.223 | 0.279 | 0.372 | 0.531 | silhouette pixels over the frame |
| soft | 1.14 | 1.17 | 1.29 | 2.28 | 2.50 | semi-transparent pixels per edge pixel: a soft matte, not a cut-out |
| halo | 0.009 | 0.011 | 0.028 | 0.074 | 0.111 | no coloured fringe |
| lum_p50 | 0.135 | 0.171 | 0.231 | 0.365 | 0.534 | DARK: the median pixel is a deep mid-tone |
| lum_p95 | 0.544 | 0.589 | 0.756 | 0.887 | 0.963 | but every one has a real highlight |
| sat | 0.116 | 0.234 | 0.383 | 0.562 | 0.687 | |
| accent | 0.016 | 0.048 | 0.170 | 0.446 | 0.659 | share of strongly saturated pixels: one accent, a few glow lines |
| thumb_keep | 0.979 | 0.986 | 1.000 | 1.009 | 1.016 | the silhouette survives 44x56 |

The finish (`finish.py`) puts every new portrait on the set's stage: top 0.010, bottom 0.995, the head/torso
anchor at 0.49, width at most 0.96. The gates (`verify.py --rules`) sit just outside the envelope.

## 2. The language, in words

- **Subject**: one anthropomorphic animal pilot, full body, head to boots, humanoid build and hands (a few keep
  claws or paws), the animal's head and its signature parts (tail, wings, shell, fins, crest).
- **Pose**: standing, three-quarter, turned a little to the left, weight on one leg, a hand on the belt or a prop,
  looking at the viewer. Relaxed confidence. (Dozer crouches, Slingshot and Strider stand like birds; they are the
  exceptions the gates name.)
- **Gear**: rugged flight and expedition wear - jackets with fur or quilted collars, harness straps, belts and
  pouches, knee guards, heavy boots; the newer ones armoured flight suits with thin glowing seam lights (Comet,
  Damsel, Strider, Shrapnel, Trace). Props: a bow, a tablet, a rifle, a sword - the set is mixed; the kit's brief
  bans firearms (an open decision for Garrett, `PORTRAITS.md` section 6).
- **Palette**: muted earth tones (umber, olive, tan, grey) with ONE saturated accent (cyan, orange, magenta) and a
  few glowing lines. Dark overall (median luminance 0.23) with real highlights.
- **Light**: one soft key from the upper left, a faint cool rim at the right; no cast shadow, no floor.
- **Rendering**: painterly digital, semi-realistic, visible soft brushwork, crisp at the face and the gear, soft at
  the fur edges. Crisp at 220 px (the set looks slightly sharpened).
- **Background**: none. Transparent, with soft edges (1.1-2.5 semi-transparent pixels per edge pixel).

## 3. What "better" means (each one is measured or gated)

1. **It is THIS pilot.** The brief is composed from the record: the first animal's head and build (`SPECIES[a].head`),
   ONE feature lent by the second (`SPECIES[b].lend`), the faction type's gear (`GEAR`) trimmed in the faction's
   domain colour (`DOMAIN_OF`, `ACCENT`: Ruby #C800C3 freedom, Gold #FFA700 commerce, Jade #0EC0B6 order), the
   callsign's one mark (`MOTIFS`). The set's pilots belong to nothing; the kit's wear their faction and their sight.
2. **One stage.** Every portrait is framed by measurement to the set's medians, so a column of them in the Hangar
   lines up: heads at the top, boots on the edge, the body on the same vertical. The set itself drifts from
   0.67 to 1.00 in height and 0.32 to 0.62 in head position.
3. **One light.** The brief fixes the key light and the rim; the set's light wanders.
4. **Lit for the plate.** The brief says the figure reads against a dark navy screen; the `value` gate holds the
   set's highlight floor (p95 >= 0.50) and shadow ceiling. A dark animal in dark gear needs one large light
   garment in the spec's `extra` (Rosh took three paintings to learn it: 0.43, 0.47, 0.59).
5. **A clean matte.** The difference key with decontamination leaves no fringe (`no_halo`), no specks and no holes
   (`alpha_clean`), and the edge stays soft like the set's.
6. **The game's colours.** The accent is a colour-domain anchor the game already draws for that team
   (`Docs/VesselModels/colour_domains.json`), so a Ruby pirate's trim is the Ruby the UI uses.
7. **Reproducible.** The source is kept; `--check` re-finishes it; a hand edit reads as drift.

## 4. The brief's anatomy (`brief.py compose`)

```
Full-body character portrait of one anthropomorphic pilot, <Person> (<CALLSIGN>), <faction type> of the <faction>:
a human body and build with <first animal's head>, fur and skin <first's colour>, and <second animal's lent feature>.
<Pose>. Wearing <faction gear>, trimmed in <domain word> (<hex>) with thin glowing <domain word> seam lights, and
<callsign mark>. [<extra>.] <STYLE> <FRAMING> <NEVER> <background: flat solid <key> edge to edge>
```

Change a species, a motif or a gear line in the TABLE; every later pilot inherits it. The key colour is
`key_for(animals, domain, callsign)`: green, or blue for a green coat or IVY; never magenta on its own.
