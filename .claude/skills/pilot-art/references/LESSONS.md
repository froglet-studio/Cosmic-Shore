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
   height (13 px at 1280) of a background pixel and interior pixels keep alpha 1; Delmira passed (soft 1.7).
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
9. **The image model follows the brief closely enough that the TABLES are the art direction.** Round 1's six
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
17. **The stranger test, round 2: 8 pass, 6 weak, 0 fail.** Every first animal was named at full size; six were
    missed at the Hangar's 44x56. The causes were all in the brief: a small head over a big full-length figure
    (Nedira's hedgehog read as brown hair), a long mane that made the orangutan a lion, the fennec's tall ears
    out-silhouetting the moose's antlers (a lent HEAD feature again: the moose was the first animal), a thin tusk
    on a slender head that read as a heron's bill, walrus tusks lost in the head mass, and a thick scaly eel tail
    on the ground that read as a crocodile. Five table lines changed (Orangutan, Moose and Narwhal heads, Walrus
    head, Fennec and Eel lends): name the thumbnail silhouette (a disc face, antlers wider than the shoulders, a
    whale head with no neck and a thick tusk, tusks to the chest), and a lent tail is slender and held up. Nedira's
    fix (a knees-up crop) breaks the full-body house style, so she ships weak.
18. **The Figma connector has a per-seat tool-call limit.** After 34 calls in a day the Froglet Studio Collab seat
    refused `get_screenshot`, so five repaints (Figma nodes 6:2 Mathalo, 6:3 Fenlia, 6:4 Ilia, 6:5 Haradek,
    6:6 Ekora) were painted but not captured. A round costs two calls per portrait (generate + capture): budget
    them, and capture each painting right after it is generated so a cut-off leaves nothing uncaptured.

## 2026-11-07 - the kit review (four lenses, three refuters per finding)

Garrett: "finish and merge." A workflow reviewed the kit through four lenses (numerics, pipeline, gates, docs);
each finding had to survive three refuters. What held, and what changed:

19. **Pillow already premultiplies an RGBA resize.** `finish.py` premultiplied the colour itself before
    `Image.resize`, so every soft edge was weighted by alpha squared and came out with a pale rim (a fur edge
    measured 127 mean luminance against 81 after the fix, alpha identical). Feed Pillow straight colour.
20. **A pinhole the matte fills keeps the key's colour** unless something repaints it: `clean()` set alpha to 1
    and the decontamination only touches semi-transparent pixels. Filled holes now take their neighbours' colour.
21. **The fringe cap must not touch opaque pixels**: jade trim is green-rich, and capping opaque edge pixels dulled
    it. The cap now acts on semi-transparent pixels only, and on both channels of a two-channel key (magenta).
22. **The reference folder holds the kit's own output too.** `measure.references()` took every PNG in
    `Assets/_Graphics/Pilots`, so after round 1 the envelope, the audit sheet and the nearest-reference picker
    would have judged new portraits against earlier ones. The painted 32 are named by `is_reference()`.
23. **A ship gate must not need packages a collaborator lacks.** `author.py --check` needed numpy and Pillow;
    without them it now prints SKIP (like the reference compile) instead of failing every branch.
24. **Byte equality is the wrong drift test for float maths across machines.** `--check` now allows a handful of
    one-or-two-level differences and still fails a real edit (both proved in its self-test).
25. **One spec wires one pilot.** Moving or clearing a spec's `pilot` now removes its old wire, and `--check`
    audits `wired.json` as a whole (a spec that no longer exists, a pilot it no longer names, a missing PNG).
    `author_pilot_assets.py` refuses a wired id that is no callsign's.
26. **Smaller fixes**: a Preservationist's green jacket now moves the key to blue; the pose keeps its capitals and
    "BountyHunter" reads "bounty hunter"; an artist's alpha delivery asks for no key colour; an exemption must name a
    real gate and give a reason; a kept detached piece (a thrown rope) is no longer counted as a speck; the sheet's
    font keeps its size without DejaVu; ACCENT is held to `colour_domains.json` by the brief's self-test; the docs
    no longer claim the portrait shows in the codex or the voyage card, or that an unwired card shows a word.

## 2026-11-10/11 - signature busts (`/pilot-creator`)

Garrett: pilot images that "really show both clades in the avatar bust". 50 busts with gpt-image-2.5-sunburst.

27. **An avatar cut from a full-body portrait shows one animal.** The second animal's ONE feature (a tail, wings)
    lives below the crop. A bust is painted as a bust and gives each heritage a zone on the head: face, crown and
    mark, eyes/hair/hand.
28. **Name the human's parts explicitly**: "white sclera", "human eyebrows", "five slender fingers, its skin matching
    the <animal>'s". Without them the human vanishes into an animal head.
29. **A face animal whose tell grows from the head keeps a small one.** Fenlia's moose with the crown given to the
    fennec read as a horse; "two short velvet antler stubs on the brow" fixed it.
30. **The sunburst model follows a long structured brief** better than flare: all 50 kept the halo, rim light and
    domain colour, and only Flux lost its crown (the orca fin), which came back on one repaint of the same brief.
31. **Weak reads to fix in the tables next**: a platypus face (MANGER) reads as a duck-billed human; a clownfish
    face (Comet) reads as orange human skin; a jackal crown on a lynx face (Wyvendo) is subtle. Push the FACE line
    harder (snout length, coat on the whole face) before repainting.
32. **Capture by curl, not `get_screenshot`.** The Collab seat's read limit runs out after about 30 calls a day;
    `www.figma.com` is now on the Default environment's allow-list, so `creator.py capture` downloads `image.url`.
    Capture each batch of five before painting the next (the asset URLs last 7 days).

## 2026-11-11 - signature busts, round 2: places, almost no hands, and the blind read

Garrett on round 1: "a good step forward, but the backgrounds need much more variance. they don't all need a hand
... any creature that already has a hand or tentacle like a frog, primate, or octopus it doesn't need a hand. the
human side is the least important to express when it is wearing clothes". All 50 were repainted: 70 paintings, 20 of
them repaints of 16 pilots. The blind read (`Tools/Build/pilot_art/blind.py`, `/pilot-creator` section 4), taken
twice for each round, named both animals in 26 and 27 of 50 before and in 44 and 47 after, and in 10 and 12, then
23 and 29, of the 64 px icons.

33. **Have the set read blind before calling it good.** Round 1 was passed by eye, by someone who knew each
    pilot's animals. Seven readers who were told nothing named both in 26 of 50, the second animal in 29, and
    found a human hand and human hair in all 50. Grade by script, too: a hand count of the same reads said 27,
    and 15 at icon size, where the script says 26 and 10.
34. **The second animal needs ground, not a patch.** A crown and a mark on one cheek was named in 29 and 28 of 50.
    The crown, the whole back of the head, the nape, the throat and the neck, plus one more piece of its body in
    the frame (a tail, wings, a shell rim, its claw) is named in 45 and 47.
35. **The clothes must leave that ground open.** Every outfit ends "the collar sits low and open so the
    <animal>'s throat and neck show above it". A high collar or a hood hides the second animal.
36. **The human is the clothes, the bearing and the gaze.** The hand was round 1's commonest painting flaw and
    said nothing the outfit does not. Now 3 of 50 keep one, gloved, and 12 show their animal's own limb instead (a
    lobster's claw, an otter's paw, a sloth's claws), which is a second tell. Human hair never over a crown that
    covers the scalp (there it shows only below the crown: 4 of the 10) and never beside an animal's own mane:
    10 of 50.
37. **A crown is what grows from the head, never a hood.** Jugger's eagle "hood of head feathers" and brow ridge
    made the orca's head a bird's. The face animal keeps its skull; the crown animal starts behind it.
38. **A face tell that is not on the face has to be said.** A snail without its stalk eyes was a blue alien
    (Astrofoil), and an oryx with short horns was a goat (Flux: "two very long slender ringed horns ... each as long
    as the head").
39. **Two animals of one texture merge unless the second brings a tell the first cannot have.** A hedgehog's
    spines on a horned lizard read as one spiny lizard until the hedgehog brought its furry ears (Bushy, `EARS_TOO`).
40. **One pair of ears, and the brief says whose.** `KEEPS` (the face's ears are its tell), `FACE_EARS` (the
    face's ears show when the crown brings none), `EARS_TOO`, and "the only ears on the head". An opossum under a
    lynx crown was once told both things, and a cat and a badger were painted with no ears at all.
41. **A tail curled over the shoulder is a squirrel's.** TALON's jackal read as a red squirrel twice. A dog's tail
    is "held up straight behind one shoulder like a fox's brush, not curled".
42. **Look-alikes are decided by colour and by the one shape the other lacks.** Brown overlapping plates are a
    pangolin; a grey mosaic cap and narrow grey hoops are an armadillo (RAMPART). Short curved horns on a dark
    shaggy head are a bison or a wildebeest; long slender horns sweeping wide over long straight hair are a yak
    (ANVIL, Spare03). A white blaze on a black face is a skunk; a white cap split by two black stripes, with the
    digging claws in frame, is a badger (Longshift).
43. **A bird whose tell is its bill is a weak second animal.** The face is the first animal's, so a heron arrives
    as a long neck, grey wings and head plumes. ZODIAC's was a secretary bird in two reads (a spray of long black
    plumes); with two short plumes and the white crown it is "some bird" and its neck "a stoat". Three paintings,
    still not a heron: a limit, and the pair to avoid when casting. Tomahawk's hawk is the same case: streaked
    feathers and a fan of banded tail feathers are a turkey to two readers of three. A raptor's other tell is its
    talons, so talons in the frame (`LIMB_TELL`) are the fix to try.
44. **A word can summon an emblem, and two words can summon a worse one.** "A medic's tabard" was painted with a
    red cross, which is a protected emblem. "A bright armband" was painted as a band with a disc and a star on it.
    "Raised fists in far silhouette" came back as fist emblems on banners, and "murals of waves and open hands" as
    a yellow hand on a banner and a hand print on the poncho (Shrapnel). BANDIT's "bandana knotted around one upper
    arm" beside its "crosshair-marked eyepiece" was painted as a red armband carrying a black ring and cross: each
    phrase was harmless, the pair was not, and the picture passed two looks and a blind read before a zoom caught
    it. Outfits describe colours and cut, places describe lights and plain shapes, a callsign's mark is a small
    object (a brooch, a pendant) and never a sign on cloth, nothing is tied on an arm, and a band of cloth on a
    sleeve says plain. "Plain" does not keep a carrier plain, though: "a champion's ribbon sash" was painted with
    a gold laurel wreath on black, which is a clothing brand's mark (Slingshot), and "three plain hanging banners"
    in a treaty hall with a winged sword, a sun and a star (Spare05). A sash is now "a broad plain satin sash" and
    a hall has curtains. Even so, the hall came back with two small banners at its edges, each carrying the tree
    the painter had put on the pilot's "signet brooch": a signet is a crest, and a hall of state hangs it up. The
    self-test refuses the words (`bust.emblem_word`); the emblem sweep (`/pilot-creator` section 4.5) reads the
    finished pictures, because a word list only knows the words that have already failed.
45. **The brief goes to an outside painter, so it describes a picture and nothing else.** It never names the game,
    the studio, the world, or a place, a crew or a faction in it (a backdrop is "thick dark water the colour of old
    wine", never the place's name), and it carries the pilot's own rolled name, never a player's. The self-test
    reads the world's names from the game (`bust.world_names`) and refuses them in every table line.
46. **Fifty places need a table, not adjectives.** `BACKDROPS` gives each place its light, its value (20 dark, 16
    mid and 14 light among the 50) and the domain colours it must not sit beside, so the accent is never
    swallowed; a home ground goes to its faction type first and no two catalog pilots share a place. The place
    stays soft and lower in contrast than the head, and the rim light keeps the silhouette off it.
47. **Record what each bust was painted from.** `capture` writes `briefs/<WORD>.txt`, and `stale` lists the busts
    today's tables would paint differently. Capture before the next table edit, or the record is of a brief that
    painted nothing.
48. **At 64 px only the silhouette and big colour blocks survive.** Both animals are named in 23 and 29 of 50
    icons (10 and 12 before), and by all four icon readers of the two reads in 16. What survives changes the
    outline or brings its own block of colour: antlers, a fin, a claw in the frame, a jellyfish bell, a peacock's
    fan, a tiger's stripes. A coat on the neck does not: no icon reader named FLUME's eel, Shrapnel's newt,
    Strider's crocodile, MANGER's platypus, Longshift's badger, WINGMAN's pelican or Tender's narwhal, whose tusk
    at the jaw is a hummingbird's bill at that size.
49. **One reader is not a verdict, and neither is one look.** With the pictures unchanged, two or three pilots
    flip between reads: RAMPART's armadillo was an armadillo, then a pangolin. Each round read twice by different
    readers gave 26 and 27, then 44 and 47, of 50 at full size, and 10 and 12, then 23 and 29, at icon size: quote
    both reads, and trust a difference only when it is much larger than that. Two readers counted six fingers on
    Krush's glove, which has five; and nobody counted the eight claws on Longshift's first paw until a zoom did
    ("five toes and one long pale digging claw on each toe" painted five). Zoom into every hand, paw and claw,
    and repaint what two reads agree on or what the picture confirms.
50. **Limits that held.** An octopus face is a mind flayer to anyone who knows one (BANDIT); a hare with horns is
    a jackalope (Spare05, Spare07); a bull-headed person in a coat is a minotaur at first glance (ANVIL, Spare03);
    an ibis with a platypus's bill has two bills (MANGER). A stranger says fox for a jackal, stork for an egret
    and bearded dragon for a horned lizard: the grader counts those as the animal's kind (its clade level), and
    crow for a raven, rabbit for a hare or heron for an egret as the animal, since everyday speech does not tell
    those apart. "Some bird" counts for nothing.
51. **The grader is part of the measurement, so test it like code.** Two reviewers who tried to break `blind.py`
    found 15 ways a plain answer was misgraded ("fox? no, a dog" counted as a fox; "monarchs" as no butterfly; a
    hedge between a pilot's own two animals counted for both). Each is now a rule with a read in the self-test,
    and each rule was then broken on purpose once: the self-test noticed all 244. Regrading every saved read with
    the fixed grader moved no row by more than one (round 1's kind row 41 to 42, its icon row 9 to 10; round 2's
    headline 44 to 45 on the read of the time), and each move is a named answer: rabbit for a hare, lizard for a
    newt. Keep every read's `result.json`, so a grader fix can be applied to all of them.
52. **Have strangers look for signs, and keep them cheap.** The eye that wrote the brief looks for what it asked
    for: the laurel on Slingshot's sash and the crests on Spare05's banners passed every look, the blind reads and
    a review, and readers who were told nothing listed both at once (`/pilot-creator` section 4.5). In the
    finished 50 they list 88 devices, 61 with something they resemble; reading those 61 lines and looking at each
    bust is the work, and most are a pilot's own mark or scenery. The first design gave each reader the busts and
    let it cut its own enlargements at full effort: 35 minutes and 0.8M tokens a reader, and eight at once stopped
    the session at its usage limit for eight hours. Cut the tiles first (`creator.py tiles`), let the readers
    only open pictures, at medium effort: 29 busts, three readers, under three minutes, 480k tokens. And a
    repaint is a new picture: read it again. Slingshot's came back without the laurel and with clubs that have a
    pincer's hook, so its one reader said lobster before mantis shrimp.

## 2026-11-11 - signature busts, round 3: six rules from one review

Garrett on round 2: "These are great. only a few need changing." Eight busts were repainted, each right on its
first painting, because each fix was a table line and not a prompt edit (`Docs/Pilots/PORTRAITS.md` section 7).

53. **A hand is something that can hold.** "please never make crab hands (how would they interact with
    things?)" A pincer, a striking club, a mantis's foreleg and a hook are an animal's tell and no hand, and a
    pilot has to work a ship. The armoured animals wear their shell ON a hand with four fingers and a thumb,
    jointed like a gauntlet, and a pilot who also has a true hand shows that one. Give the hand something to hold
    (Comet's holds a shard): a hand that holds is read as a hand. The gate is two patterns, `NOT_A_HAND` and
    `A_HAND`, on every limb line.
54. **Where a piece grows from is a sentence of its own.** "butterfly Wings on a head, as ears, is nonsensical"
    and "the turkey tail feathers are in a very weird spot with respect to the dog's hood": the brief had named
    the piece and left its root to the painter. Wings, tails and fans now say the back, a fan "rises from below
    the frame behind both shoulders", and under a hooded outfit the brief adds that the hood lies in front of
    it. What is worn and what is grown both sit behind the head, so say which is in front.
55. **Two materials meet by a written gradient.** "the chameleon transitioning to jellyfish looks a bit abrupt":
    the standard seam line ("meeting the face along the jawline") draws a rim when one side is translucent. A
    translucent coat has a `BLEND` line instead: the skin thins and turns glassy by degrees over a hand's width.
56. **A review that likes the set grows the lists; it does not repaint the set.** "The backgrounds that don't
    look like they could be earth are the best" became 21 new places and a mark (`UNEARTHLY`, rolled three times
    as readily), and "it is fine to have some that express more of their human side ... I would expand the list
    vs. change that" became `roll_more` for new pilots only. The 50 kept their places and looks; `creator.py
    stale` lists the ten whose table line moved on after they were painted.

## 2026-11-11 - the scenes: the same pilot in a realistic picture (the style is not locked)

Four samples, 19 paintings, three reads (`Docs/Pilots/PORTRAITS.md` section 8; `Tools/Build/pilot_art/scene.py`).

57. **The same words keep the same person.** A text painter never sees the avatar. A scene whose description of
    the person is cut word for word from the brief the avatar was painted from was matched to the right avatar
    among 50, by readers told nothing, in 16 answers of 16, every one "sure". Composing the person again from
    today's tables would describe someone slightly different: the tables move on, the painting does not.
58. **Follow the painting, not the request.** The painter decided things no brief asked for, and departed from
    some it did. Look at the avatar and write both down: `seen` for what it added (the brooch is brass, the shell
    cap hides the ears), `swap` for where it differs (hazel was asked and amber-gold painted; "shaggy" wool was
    painted as tight curls). A correction REPLACES the phrase: "warm hazel irises" beside "amber-gold" gave pale
    green eyes, because two colour words for one thing let the painter choose a third.
59. **Below the avatar's crop nothing was decided, so decide it once.** The build, the hands, where the second
    animal's piece grows from and the lower half of the clothes are tables (`BUILD`, `hands_of`, `CARRY`,
    `LOWER`), the same for every picture of a pilot. And cover the body: the first candid picture had bare pink
    human forearms between an owl's neck and a mouse's face, which its reader marked as a thing a player would
    notice. The brief now says the clothes cover everything from the collar to the wrists and ankles, and the
    wrist wears the hand's own coat.
60. **An animal's bare-skinned hand is painted as a person's.** "a mouse's small hand, four thin bare pink
    fingers" came back as small bare human hands, and a reader said they made the head look like a costume head.
    Fur to the fingertips and bare skin only at the pads reads as the animal's.
61. **Hair words mean something else in a photograph.** "Shaggy" wool was a fur hat sitting on the head. "Tight
    springy curls like a lamb's fleece" was a person's long ringlets. "A deep dense fleece of short tight curls,
    packed like a sheepskin and nowhere longer than a finger" was the mound the avatar has, and in the one roll
    without "the face is bare skin from the brow to the chin" the fleece ran over the face. Say the length, what
    it is packed like, and where it stops.
62. **A face needs its whole outline.** "A walrus's broad heavy face" under a mound of wool was painted with an
    ape's brow and nose: the painter filled the top of the face with the nearest heavy face it knew. "The face is
    a walrus's from the chin to the top: a smooth rounded forehead sloping straight into a broad flat muzzle of
    two round whisker pads" gave the walrus's own muzzle in three rolls of three.
63. **A horned animal gets its ears.** The bison's small ear under one horn was painted in four rolls of four,
    whatever the wool was told to cover ("where ears would be", "so that only wool shows there", "closing round
    the base of each horn in an unbroken fleece"). A hard cap does hide ears when it is described as covering the
    place ("fitting close over the whole top of the head ... down over the temples where ears would be"), and "so
    no ears show" drew one. Not solved for wool.
64. **The eye ring is a close-up's detail.** "Ringed by a thin glowing line" was painted in one roll of three;
    "the rim of each iris, a thin line of colour where an animal's would be dark" in three of three. Readers
    still saw it only in the portrait taken from two steps away. A scene that must carry the pilot's sight has to
    be near the face.
65. **Tell the wear where to stop.** Mud to the knees also buried the trim, and a reader said the faction colour
    was dull. "Above the mud line the magenta piping still shows bright along the collar" kept both. Wear that
    has a cause reads truer than wear that is spread: "soot is ground into one shoulder, one side and both knees,
    where they went against a wall and a floor" against "streaked with soot". The last reader still found the
    tiger's mud "evenly applied, like costume breakdown".
66. **A prop stays where the avatar wears it.** The lens that hangs on a cord in the avatar was painted in the
    scene as a monocle over one eye, and a reader called it a difference a player would notice. Say in the action
    where every worn thing is ("the brass lens hangs on its cord against the chest").
67. **Empty the place by name.** A hangar "with a row of craft" came back with blurred crew in the distance (both
    readers counted four or five figures), and two rescue boats with someone in each. "The bay is empty of
    people", boats "empty and unlit, their hulls plain orange", and "nobody else is anywhere in the frame, near
    or far" held. Naming labels in order to ban them ("every label ... is blank") still drew two jar labels with
    marks too small to read: leave out the thing, do not ask for it blank.
68. **Unearthly and photographic at once means a quiet strangeness.** A violet spiral sky with crystals the size
    of houses made both readers call the portrait a 3D render. The far wall of a living cell, veined like a leaf
    and seen through rain, with flat milky-green water under it, was called a photograph by both, and one still
    listed "a faint giant lattice in the misty sky" as the thing that breaks the realism. Make the place strange
    through distance, weather and what the ground is made of, and keep the camera, the light and the foreground
    ordinary.
69. **Each approach reads as what it copies.** Readers called the long-lens field picture "a real telephoto
    wildlife or documentary shot" and the close portrait a photograph; they called the film still a film still,
    and the candid snapshot a film still too ("warm single-lamp lighting and tidy composition feel staged").
    A candid picture needs light that nobody would choose.
70. **The painter and its frames.** `gpt-image-2.5-sunburst` returns 1536x1024 for 3:2, 1536x864 for 16:9 (ask
    for 2048x1152) and 1024x1280 for 4:5, from a brief of up to about 1,000 words. `gemini-3.1-flash-image`,
    tried once on the same brief, merged the platypus's bill shield into the beak, drew human hands and a clean
    cape, at 1264x848.
71. **The read is two commands and two readers.** `scene.py read-prep` cuts and shuffles the pictures and keeps
    the key outside the folder; the workflow's two readers only open pictures, at medium effort: about 220k
    tokens and under two minutes for four scenes. Each repaint is a new picture and is read again; three reads
    took the "differences a player would notice" from 3 to 1, and the read after the first repaints found four,
    because fixing one thing moved another (the walrus's fur hat became an ape's brow).
72. **Weather laid over a whole thing replaces the thing.** "trail boots caked with snow" came back with a white
    furry paw where a toe cap should be. "tall laced trail boots, snow packed in the treads" came back as boots
    in four rolls of four. Put the snow, the mud or the rust on a part that has a shape of its own (the treads,
    the laces, one sleeve), never over all of it.
73. **A hand that is hidden comes back as a third one.** "One hand is buried to the wrist in the tail's fur; the
    other holds a steaming tin cup" was painted with two hands on the cup and a third paw at the knee. Give both
    hands work the camera sees ("both hands are wrapped round a steaming tin cup") and close the count ("and no
    other hand is anywhere"): two hands in three rolls of three.
74. **A pair posed two ways makes three.** "one ear straight up and the other turned to the wind" was painted as
    two ears up and a third out sideways. "the two ears standing straight up side by side" held in two rolls of
    two. Pose a pair as a pair, with its number in the sentence.
75. **Bring the second animal up to the head.** The avatar is the scene's head and shoulders, and a tail lying in
    the lap is outside that square: the hare with the skunk's tail across his lap cut to an avatar of a black
    rabbit. The same scene with the tail drawn "across the lap and up over the chest like a blanket, its white tip
    tucked under the chin" put the skunk in the avatar, and made the picture more his own. When the second
    animal's piece can be worn, held or wrapped, pose it where the avatar's square will catch it.

## 2026-11-12 - the scenes, round 2: the game's own sea, one creature, a painter with a wall

Garrett's notes of 2026-11-11 (22:49 and 23:01 UTC) on the scenes with displays: the sea in the art was not the
game's, Flux's scene was to start again, Trace was "a bad and lazy photoshop with a bird head some hair and
jellyfish stuff", ZODIAC's windows were to follow Jugger's ("the dual perspectives of jugger is a better standard
for windows"). Four repaints: Longshift, Flux (five paintings), Trace (four) and ZODIAC (three).

76. **The sea is written from the game's frames, and it has six nouns.** "both the hypersea outside and the
    hypersea in the rendering are very different than the hypersea in our gameplay." The old words were a
    guess (islands, plankton, a galaxy). The new ones were written with six real frames open (five from the
    studio site, one flown in the browser build; `Docs/Pilots/art/refs/`): black space sown with fine white
    dust; the wall of a vast hollow sphere, a net of thin dim blue lines in six-sided panes; bricks of light,
    flat-sided blocks with deep clear faces and thin bright edges, a trail being a ribbon of them; a coral of
    bricks set edge to edge in rings round open holes; a crystal shaped like a twelve-sided die; fauna as swarms
    of tiny violet shards. `scene.SEA` holds them once, the display views are built from them, and a record's
    own words for a window use the same nouns. The six-sided wall was painted in three scenes of three.
77. **A crop is named by the thing that must be whole and by the figure's two ends.** Flux's horn tips were cut
    by the top of the frame in four paintings of four, whatever the shot said about them: "both horns are
    whole", a height in fractions of the picture, the eyes at mid-height, a seat laid back. "Seen from the waist
    up" names one end of the figure, and the painter spends the other. The fifth says "the whole arch of the
    canopy is in the picture, from rim to rim" and the figure "from the tips of the horns down to the gloved
    hands on the stick", and drops the sentence that puts the figure a third of the way in: both tips whole,
    with glass above them. Name both ends of whatever must not be cut, and the frame that holds them.
78. **The painter paints the noun.** ZODIAC's windows asked twice for "a reef of coral-orange bricks of light
    with violet edges" and got a reef: glowing rock, once as a spiral with boulders, once as a lava plain under
    a horizon. "A wall of bricks of light, each as big as a door, set edge to edge in rings round open holes,
    coral orange deepening to violet" got bricks on the first roll, and the chart on the table drew the same
    rings of bricks small without being asked. In Trace's port "a spray of white needles in every hole" became
    sea-urchin puffs. Where the thing fills a window, call it by what it is built of and size it by something
    the painter knows (a door); a likeness from nature (reef, coral, island, needles) is painted as that nature.
    Inside a display the size of a hatbox "a coral" is safe, because nothing that small can be a reef.
79. **A fused head is one outline with states along it, and no edge to sit on.** Trace was designed by a panel
    (five designers, each from one lens; a director, a stranger reading blind and the painter's habits as
    judges): one booby head whose back turns to jelly (dry feathers, wet feathers, jelly), a pattern that
    crosses the join (the white flecks), something lying inside it (the last dark feathers), and a second
    signature that cannot be worn (hands the blue of a booby's feet, scaled, "every web is jelly"). The parts of
    the old picture were LEFT OUT of the words, never forbidden (63, 67), and none came back in four rolls. The
    four: one keeper, one good head with the display inside a glass case, one jelly wig, one jelly bonnet.
    "Smooth" jelly and "slim" frills moved it off the wig; "hangs in the open air with nothing round it" took
    the case away. What is still wrong was written by the brief: "its only edge is low at the nape ... with
    shadow under it" is an edge, and both blind readers who named booby and jellyfish also said the jelly "sits
    on the back of the head like a cap", with "a visible edge where it meets the feathers". The next brief
    gives the join no edge and no shadow: the feathers thin, go clear and are the jelly. Count the hats, wigs
    and second heads over all the rolls before picking one.
80. **A repaint that answers the note can lose what the note never mentioned.** Flux's fifth painting answers
    every line of his note (the tail inside, the display over the panel, the canopy shut over the horns), and
    one of two blind readers no longer found the orca: "horse or zebra (stiff upright mane)". The picture he
    rejected had a glossy hooked fin and a fluke; the new one hides the fluke by design and paints the fin as a
    matte crest with no white eye patch. When a repaint takes the second animal's biggest piece out of the
    frame, the brief owes the head a second sign of it (75), and the avatar is read blind again BEFORE the
    picture is shown: "is it still both animals" is a measurement, and it was taken after the push.
81. **The painter's credits are a wall, and it arrives mid-pair.** The second of two ZODIAC rolls came back
    "Insufficient Figma AI credits ... resets at 2026-11-14T01:41:00Z". Twelve paintings had gone on three pilots.
    Roll changed words only: never a second pair of words that have just failed the same way, and never a roll
    "to see" (PAINTERS.md section 2 has the pools: Figma AI credits for the words-only painter, Weave credits,
    approved run by run on a card, for the painter that takes pictures).
82. **A critic told to object will object, so give each critic that picture's notes.** The review workflow gave
    three critics every note Garrett had written on any scene. They applied all of them to each picture
    (Longshift's field was faulted for not showing the game's sea, which he asked of Jugger and ZODIAC) and
    all three said "unlikely". What held up was what they measured: the names the blind readers gave,
    an ear tip on the top edge, the share of vivid pixels (1.5 percent in Flux's sky against about 42 in the
    sheet of game frames). Brief a critic with the notes on THAT picture, the standing rules under their own
    heading, and ask for measurements first.

## 2026-11-12 - the game's frames: a painter that takes pictures, and the wall of its plan

Garrett approved, on a card, up to six runs of the painter that takes pictures (GPT Image 2.6 through Weave, 11
credits a run, each quoted first). Two ran: Jugger's windows and display, and Scales's display. The third was
quoted, and refused by the plan.

83. **A painter shown the game paints the game, and repaints the room while it does.** Given the scene, four real
    frames and "Edit this photograph, picture 1. Change only ...", it gave Jugger's windows and dome and Scales's
    dome the game's own look on the first run each: bricks with bright edges and red rims, needle sprays, the
    six-sided net, twelve-sided crystals, shard swarms, brick trails, and the ship itself from its four views.
    Six nouns of words (76) had not done that in twelve paintings. It takes no mask, and its answer is a new
    picture of the whole room: Scales's moved 6.09 of 255 outside the region and Jugger's 14.36 (the figure 40 px
    to the right, the windows taller). So `refs.py apply` takes only the record's region (edit less keep,
    feathered 2 px inward) and the pilot stays the pilot of the first painting. Both seams were clean, because
    each region ends on something drawn: a window frame, a sill, a brass rail. Draw a region to such edges. The
    ask with one part moved the room less than the ask with two.
84. **A reflection is a second figure.** Jugger's ask said the glass "carries faint reflections of the bridge".
    The answer hung a second orca head in a window. It lay above the old frames, outside the region, and was
    dropped with the rest of the answer; the sentence is gone from the record. In an edit as in a brief (78),
    what is named is painted: leave reflections, the shadows of people and glimpses out of an ask.
85. **A free plan has walls its price list does not show.** Every run was quoted at 11 credits and the workspace
    held 150. The third run was quoted, was inside what the card had approved, and came back "You've used your 2
    free Weave model runs. Upgrade to a paid Weave plan": two model runs for an agent on the Free plan, whatever
    credits remain. Neither the quote nor the pricing page says so, so the card that asked for six runs asked
    for four that could not be had. A sign had been there: the same plan had refused another model outright
    ("not available on the Free plan"). On a free plan, say on the card that its limits are not known until
    met, and order the runs so that the first ones are worth having alone. They were: the two scenes that
    needed only a region went first. Nothing was spent on the refused run. The wall is the plan's and is the
    person's to move (PAINTERS.md section 2).
86. **Look through the rolls before paying for an edit, and pick a roll by what the note is about.** Trace's kept
    roll had the free-hanging display and wore the jelly as a hood with a rim (79), and a run was planned to edit
    the hood away. Roll A, set aside for its glass cylinder, already had the head: the feathers thin into the
    bell with no edge and the bell is the back of the skull. The pick had been made on the display, and his note
    is about the creature ("a bad and lazy photoshop with a bird head some hair and jellyfish stuff"). A place
    can be patched from frames (83); a head cannot be patched without the painter redrawing the pilot. So the
    scene is roll A, with the cylinder and the port prepared as a patch. It also corrects 79: the brief without
    "smooth" and "slim" painted both the best head and the wig, so those two words did not make the join.

## 2026-11-12 - the paid plan: a head changed by an edit, a head that lost its read, one thing at three sizes

Garrett moved the workspace to a paid plan (04:09 UTC) and three more of the six runs he had approved were made:
Flux (the whole frame, with his likeness), Trace (four things in one ask) and Trace's cylinder again (one thing,
and no frame of the game). 55 of the 66 credits are spent and one run is unused.

87. **A whole-frame edit that is shown the pilot's likeness changes the head and keeps the face.** Flux's patch
    names the whole frame as its region, because the head itself changes, and shows the painter his likeness
    (head and shoulders, cut from his first scene) as picture 2: "an orca's dorsal fin: the fin Flux has in
    picture 2". One run gave the hooked fin of wet black skin, an orca's skin from the temples back, a display
    with depth and the course outside from three frames, and the face, the horns, the canopy and the hands of the
    fifth painting were where they had been. Read blind, a stranger named a gemsbok and an orca, where one of two
    had said "horse or zebra" (80). What the ask wanted kept and also used as a landmark was lost: "swept back
    toward the ear" put the white patch where the ear was, no ear shows, and the reader took the patch for "one
    small white ear". Place a new part by what cannot be painted over (the eye, the base of a horn), never by a
    part the new one could cover. Afterwards the record follows the picture: the fin and the patch are `swap`
    pairs against the identity's own phrases, because three more `seen` lines put the brief over its word cap.
88. **One run may be asked for several things, and each is judged alone: asked and taken.** Trace's run asked for
    four: the back of her head drawn in to a booby's outline, the cylinder, the port, and a blank plate on the
    microscope. A run costs the same whatever it asks. The answer was looked at part by part, and only the port
    and the cylinder went into the scene: the record's `edit` holds those two regions, its `took` says in a
    phrase what was taken and what was left, and the note beside the mask keeps the ask as it was sent
    (`refs.py`, ASKED AND TAKEN). Part by part: the port came right on the first run. The cylinder came back
    orange, the colour of one reference frame, over the ask's "jade teal ... indigo and violet", and wider than
    it had been over "stay exactly where they are". "The small plate on the microscope's head is ... blank" was
    painted as a white dish on its stage: what is named is painted (84), and a plate is a dish. The seams of
    regions cut through feathers, scales and metal could not be found at four times, because the painter keeps
    the picture's alignment to the pixel while it repaints it (the whole moved 13.48 of 255, the face 7.82, fine
    detail a little softer). A region need not end on a drawn edge (83) when the answer is that well aligned;
    look at the seam at four times either way.
89. **Drawing the second animal in to the first one's outline loses it.** Two readers of Trace's bell had said it
    was worn "like a hood", so the run drew the jelly in: "no wider and no higher than a booby's own head", the
    feathers going clear into a close mantle down the nape. It came back as asked, and to the eye that had asked
    for it, it was one head at last. Read blind BEFORE it was shown (80), the jellyfish went from sure
    ("translucent glowing bell and frilly tentacles") to a guess ("translucent purple frilled veil"), and the
    reader still said "worn as hair; could also read as a gauzy headscarf". At icon size the bird read better
    ("long-beaked seabird (gannet or booby)" from both readers, where the bell gave "bird") and the jellyfish not
    at all. So the scene keeps the bell, and the head that was tried is on the gallery page beside it with both
    reads. An animal is named by its own silhouette, and a jellyfish is its bell. And jelly that hangs where hair
    hangs is read as hair whatever its outline: "worn" is about where a part sits and what it does, which an
    outline does not change. What is left to try is the part put to use, as Longshift's tail is his blanket
    (Garrett's note on him): a frill that holds, lights or reaches.
90. **Make the scene its own reference where it already holds the thing, and send no frame in another colour.**
    The second run for Trace's cylinder sent the scene alone (the patch's `refs=()`): "a small model of the
    growth that fills the round port of this same photograph ... in the same colours and the same glow as the
    port shows it". It came back in the port's jade teal and violet on the first run, in the same place and at
    the same size, and the answer moved the room about half as far as the four-part ask had (7.09 of 255 against
    13.48; 83 found the same for one part against two). A frame is copied for its colour before the words are
    read for theirs: send only frames in the colours the scene wants, and where the scene has a window, a port
    or a second display, point the words at it. A scene patched twice keeps both notes (`earlier`). Each `apply`
    encodes the picture again: the avatar's cut moved by 2 of 255 at most, so it is re-cut and committed with
    the scene.
