# Lessons (append one block per round; the owner's words first)

## 2026-11-07 - round 1 (Wyvendo TALON, Karenth VOLT, Delmira SUNDIAL, Rosh BANDIT)

Garrett: "i tried to get you to make pilot artwork but that came out terrible. you have illustrations of pilots
for reference. perhaps you could try to build up a skill that could make art like the ones we have but even
better."

1. **A chroma key is a DIFFERENCE key, not a colour distance.** The first matte ramped alpha on OKLab distance
   from the key; a half-blended fur edge (50 % coat, 50 % green) sits 0.216 from pure green in OKLab and came
   out opaque (alpha 0.99), a hard cut-out with a lime rim. A pixel blended over a pure key carries that much
   excess of the key's channel over the others: alpha = 1 - excess / the key's own excess gives 0.5 for that edge
   and 1 for the coat. The OKLab ring spread (0.005 on every round-1 painting) only gates the background cluster.
2. **A dark animal in dark gear sinks below the painted set's highlight floor.** Rosh v1 (black raven wings,
   dark sea coat, deep purple) measured lum_p95 0.426 against the set's minimum 0.544 and failed `value`. The
   fix is the BRIEF, not the finish: the STYLE paragraph now says the figure is lit to read against a dark navy
   screen, with pale highlights on the face and the nearest gear and the darkest fur, feathers or cloth kept in
   cool grey light. That alone was not enough: Rosh v2 (feather-edge light, "warm mid-brown where the light
   falls") measured lum_p95 0.474, still under the floor, because a highlight sentence lights edges and a p95 needs
   AREA. Rosh v3 gave one large garment a light value ("a weathered tan-brown coat rather than dark, a cream linen
   shirt at the chest") and passed at 0.587. The rule: a dark animal with dark faction gear gets one big light
   garment in `extra`. The finish changes no colour or value on purpose, so the source stays the truth `--check`
   re-finishes from.
3. **The difference key must act only in the edge band.** Delmira's jade-teal seam lights and brocade have a
   little green excess (#0EC0B6: g 0.75 over b 0.71), so the whole coat interior came out 4 % see-through:
   `soft` 4.54 against the set's 2.50 maximum and a `halo` of 0.167. The key now applies within 1 % of the image
   height (13 px at 1280) of a background pixel and interior pixels keep alpha 1; Delmira passed (soft 1.5).
4. **The cloud environment cannot download the painting's URL.** `www.figma.com` is not on the environment's
   allow-list (the proxy answers 403 to the CONNECT). `get_screenshot` with `enableBase64Response: true` and
   `maxDimension: 1280` returns the full 1024 x 1280 PNG inline and the harness saves it under `tool-results/`;
   that file is the source. The generate call's own inline preview is a 205 x 256 JPEG and is never the source.
5. **A magenta key eats Ruby trim** (#C800C3 is magenta with the green channel at zero: full excess, alpha
   0.24 on the edge), so `key_for` never chooses magenta on its own: green by default, blue for a green coat
   (Mantis, Chameleon, Sloth, Firefly, Hummingbird, Gecko) or for IVY's leaves. A green key keeps Jade teal
   (edge alpha 0.96, interior 1.0) and Gold.
6. **The painted set is darker than it looks.** Median luminance 0.23, the brightest 5 % at 0.76; saturation
   0.38; one accent. A model's first instinct is brighter and cleaner; the brief's "muted earthy base palette with
   one saturated accent" and the `value` / `saturation` / `accent` gates hold it to the set.
7. **Thirty of the thirty-two references pass the gates, and the two that fail are the right two**: Dozer (the
   crouching frog, height 0.671) and Echo (the winged mouse, 0.739) fail `height` and `top`. `verify.py --self-test`
   asserts exactly that, so a gate that drifts away from the set is caught.
8. **The sheet's nearest references are chosen by measurement** (width, coverage, median luminance, saturation,
   accent), which puts a lynx hunter beside Comet, Fiest and Tenacious: the same darkness and the same bulk. That
   is the right neighbour for judging whether a new portrait belongs in the column, better than the same species.
9. **The image model follows the brief closely enough that the TABLES are the art direction.** Round 1's five
   paintings carried the exact accent hex as trim and seam lights, the callsign mark (the fang cord and clawed
   gauntlet, the crackling monocle, the forearm sundial, the crosshair eyepiece), the lent feature (the jackal's
   tail, the koi fins and patches, the pangolin scales and tail, the raven wings). So a species line, a motif or a
   gear line in `brief.py` is what to fix when a portrait is wrong, never the prompt by hand.
10. **The Figma file is the round's gallery.** Every painting is a frame in "Cosmic Shore Pilot Art"; a round's
    rejected candidates stay there without entering the repo. Only the shipped source is committed.

## 2026-11-07 - round 2 (the fourteen remaining callsigns), prepared by a panel

Garrett: "continue." The round was prepared by a workflow: four review lenses over the kit with three refuters per
finding, four groups of twelve species lines checked by a zoologist-and-art-director pass with one refuter per
proposed fix, and three cast designers (legibility first, character first, the sight first) scored by two judges
(the rules; the art) before the fourteen specs were written.

11. **A species line is checked by someone who was not the one who wrote it.** 56 corrections were proposed and 33
    survived refutation; the kept ones were facts (a narwhal's tusk grows from the upper jaw, not the brow; a
    red-crowned crane's face is black, so the white-headed line is a whooping crane; a scarlet ibis's face is bare
    pink skin; fireflies glow from the abdomen, not the wing cases; an orangutan's face is dark leather inside the
    orange mane; a lobster's carapace is one shield) and legibility calls (name the thumbnail tell first: the moose's
    antlers, the hedgehog's spines, the mole's snout; a lend is ONE feature with a body placement, never a second
    head feature such as a puffin's bill or a crane's crown on another animal's head, never a whole-body coat that
    collides with the first animal's "fur and skin" clause). The 23 dropped ones were refuted for inventing anatomy,
    dropping a true fact, or fixing a line no portrait had shown to be wrong.
12. **The composer's grammar is data too.** "aristocrat of the House Verdance" read wrong to a designer;
    `crew_phrase` now writes "of House Verdance", "of the Rusted Grin", "of the House of Seven Scales".
13. **Casting rules the judges enforced, now rules here**: a capybara head cannot be named at 44 px, so it is
    never a first animal; a predator head never goes on the herbivore sight (a wolf on DOE reads as TALON's cousin);
    a dark head with a head-feature lend (a mole with walrus tusks) names the lend, not the animal; two pilots in one
    faction colour with the same silhouette (two winged Ruby pirates) read as one; a translucent dome head painted
    over a key colour keeps the key through its interior, where the matte never reaches; a lend that is light rather
    than shape (a firefly glow) vanishes at the thumbnail; claws for hands read as a chimera.
14. **New pilots are minted only into Merchant, Hunter, Aristocrat and Pirate** (`FactionCatalog.Recruitable`), so
    every cast pilot is one of those four, spread across them (round 2: 4, 3, 4, 3), with a founding faction name.
15. **The key colour per spec**: Quivera (Chameleon head, blue-lilac jellyfish tentacles) could take neither green
    nor blue, so her spec names magenta; as a Gold merchant that is safe (matte.py's negative control is a Ruby
    pilot on magenta). The brief's own key choice covered the other thirteen (green, with blue for the Mantis and
    for IVY's leaves).
16. **Fourteen paintings, fourteen first-pass gate passes.** With the light-garment rule in every dark pilot's
    `extra` (Brikon, Obelo, Fenlia, Haradek, Ekora, Zeko) no portrait sank: lum_p95 0.52-0.83, lum_p50 0.21-0.36,
    every matte spread 0.004-0.010. The ring spread on the magenta key (0.010) is twice the green's; still flat.
