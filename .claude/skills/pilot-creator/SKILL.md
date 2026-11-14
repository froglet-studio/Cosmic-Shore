---
name: pilot-creator
description: Create a Cosmic Shore PILOT - a unique triple-hybrid person (human + two animals, a faction, a callsign) rolled exactly as the game's PilotGenerator mints them, designed as ONE creature, photographed in one realistic widescreen SCENE from their own life (Docs/Pilots/art/scenes/), with the player avatar cut from the scene's close-up (Assets/_Graphics/Profile/Pilots/Avatar_<NAME>.png) and read blind by strangers who must name both animals. Use on "/pilot-creator", "make a pilot", "give this player a unique pilot", "roll a pilot", "a scene of <pilot>", "repaint <pilot>", "the avatar only shows one animal", "it looks photoshopped / worn / stuck on", "put the game's sea in the picture", "blind read the avatars", "sweep the scenes", or any change to Tools/Build/pilot_art/scene.py, refs.py, bust.py, creator.py, blind.py or Docs/Pilots/art/. Holds both painters and what each costs, and the earlier signature busts (references/BUSTS.md). The full-body Hangar portrait is /pilot-art.
---

# Pilot Creator: a triple-hybrid person, their scene, their avatar

Garrett, 2026-11-10: "can you get us pilot images that really show both clades in the avatar bust when used in that
context. I want to produce a character creator skill that will allow us to give players unique pilots. I want you to
send the quality to its highest levels such that when people see it they know it is cosmic shore because only we have
triple hybrid people with that unique and high quality style." And what the skill is for (2026-11-11): "a large
enough roster that every pilot is unique and each player has their own unique crew. They will all use the same
callsigns which connect to the same vision abilities (not unique) but the art, backstory, species combination, etc.
will all be unique."

Three decisions of his shape everything below, each on 2026-11-11:

- **The scene replaced the bust** (19:58 UTC, on four realistic samples): "All four of these are knocked out of the
  park ... drop what we had before. the scene close ups are categorically better than the previous avatar pictures
  and should be used in their place ... treat each pilot as a new bespoke exercise ... we want something like 2.-
  (cinematic film still) because then we have a fullscreen piece of art we can crop etc. for different platforms and
  presentations. the main use will be a full screen profile for each pilot accessible through the ark app shell
  menu." So a pilot is ONE widescreen scene, and the avatar is its close-up. The 50 busts stay as the source of each
  catalog pilot's identity (`references/BUSTS.md`).
- **The two animals are one creature** (23:01 UTC, on a booby under a jellyfish's bell): "looks like a bad and lazy
  photoshop with a bird head some hair and jellyfish stuff. she does not look like a thoughtfully conceived hybrid
  animal person. we can do a lot better job of expressing the animals together." And on a hare in skunk colours: he
  "should have a bushy skunk tail that wraps around his body and into hit lap like a blanket to keep warm while he
  is waiting at his cold spaceport stop, otherwise he is just a rabbit man with skunk coloring." Section 5.
- **The sea in a picture is the game's sea** (22:49 UTC): "both the hypersea outside and the hypersea in the
  rendering are very different than the hypersea in our gameplay. is there a way to upload screenshots or other
  content so we can keep consistency between the game and the art we are generating?" Section 7.

## 0. The bar

A stranger who is told nothing names BOTH animals from the avatar, and no part of the pilot reads as worn. It is
measured, never felt (sections 4 and 4.5), and one read is one sample.

| What strangers named (2026-11-12) | The 50 scenes | The 50 busts they replaced |
|---|---|---|
| Both animals, from the avatar (sure or likely) | 29 of 50 | 44 and 47 of 50 (two reads) |
| Both animals in the 64 px icon, either reader | 16 | 23 and 29 |
| Both animals, from the whole scene | 40 of 50 | (a bust is its own whole) |
| Taken for a photograph or a film still | 26 of 50 | (painted on purpose) |

Garrett chose the scenes for how they look and was told these numbers on 2026-11-12. They say where the work is: a
scene's close-up shows less of the second animal than a bust built for it, and in 27 of 50 a reader called some part
of the pilot worn (a cap, a hood, a helmet, a wig, a mask). That is his note about the booby, across the roster.
Every pilot's reads are on the roster page (section 8). Before calling a pilot done, read section 5 and read the
avatar blind.

## 1. Read before you touch

- `Tools/Build/pilot_art/scene.py` (its docstring is the mechanism; a pilot's record is `scenes/<WORD>.py`),
  `refs.py` (pictures for a painter that takes them, and patches), `blind.py` (the blind read and its grading),
  `creator.py` (the roll, the catalog) and `bust.py` (the tables every identity line comes from).
- `.claude/skills/pilot-art/references/LESSONS.md`: "the scenes" (57 to 90) before writing a scene, "signature
  busts" (27 to 56) before touching a table. `PAINTERS.md` sections 1 and 2 before spending a painting.
- `Docs/Pilots/PORTRAITS.md` sections 7 and 8 (what was painted, measured and decided), `references/BUSTS.md`.
- The pilot record: `Docs/Pilots/INVENTORY.md` section 3; `Assets/_Scripts/Data/Inventory/` (`AnimalSpecies`,
  `FactionCatalog`, `PilotGenerator`, `SeededRandom`).
- `git fetch origin && python3 Tools/Build/who_works_where.py query Tools/Build/pilot_art Assets/_Graphics/Profile`.
  Pilots and their art are Garrett's; the profile window and avatar picker are Shombith's UI.

## 2. Commands

```
python3 Tools/Build/pilot_art/creator.py roll TALON 0x3039     # the person the game mints for that seed, their story
python3 Tools/Build/pilot_art/creator.py roll TALON            # a fresh seed: a new unique pilot
python3 Tools/Build/pilot_art/creator.py list                  # the catalog (the 50 avatar pilots)
python3 Tools/Build/pilot_art/scene.py list                    # every pilot: treatment, source, painted or not
python3 Tools/Build/pilot_art/scene.py who <WORD>              # a pilot's identity lines and hands, as painted
python3 Tools/Build/pilot_art/scene.py draft <WORD>            # the facts, the treatments taken, an empty record
python3 Tools/Build/pilot_art/scene.py recast <WORD>           # freeze today's words when a pilot's animal changed
python3 Tools/Build/pilot_art/scene.py brief <WORD>            # the brief, verbatim what a words-only painter gets
python3 Tools/Build/pilot_art/scene.py capture <WORD> <url>    # save a painting and the brief it was painted from
python3 Tools/Build/pilot_art/scene.py grid <WORD|file> out.jpg    # the picture under a ruled grid, to set `close`
python3 Tools/Build/pilot_art/scene.py sheet out.png [WORD...]     # scenes beside their avatars, ten a page
python3 Tools/Build/pilot_art/scene.py crops out.png [WORD...]     # one scene cut for other presentations
python3 Tools/Build/pilot_art/scene.py manifest [--check]      # scenes.json: what a screen needs to place a scene
python3 Tools/Build/pilot_art/scene.py stale                   # scenes today's kit would paint differently
python3 Tools/Build/pilot_art/scene.py sweep-prep <scratch dir> [WORD...]   # the readers' pictures, shuffled
python3 Tools/Build/pilot_art/scene.py sweep-grade <scratch dir>
python3 Tools/Build/pilot_art/refs.py list                     # the library of real game frames; who has a likeness
python3 Tools/Build/pilot_art/refs.py likeness <WORD> [l t r b]    # cut a pilot's likeness from their scene
python3 Tools/Build/pilot_art/refs.py mask <WORD> [view.jpg]   # a patch's mask (run after changing edit or keep)
python3 Tools/Build/pilot_art/refs.py prompt <WORD> [--patch]  # the words a painter that takes pictures gets
python3 Tools/Build/pilot_art/refs.py input <WORD> [--patch]   # the run's input with its links (needs a push)
python3 Tools/Build/pilot_art/refs.py apply <WORD> <painted file>  # put a patch's answer into the scene
python3 Tools/Build/pilot_art/blind.py prep <scratch dir> [WORD...]    # shuffle the avatars for the blind readers
python3 Tools/Build/pilot_art/blind.py grade <scratch dir> --rows      # grade what they said, worst first
python3 Tools/Build/author_pilot_avatar_assets.py              # re-cut every avatar (256 px), then --check
python3 Tools/Build/colour_grade.py                            # after a re-cut: the texture record follows
for m in bust creator blind scene refs; do python3 Tools/Build/pilot_art/$m.py --self-test; done
```

## 3. The round for one pilot

1. **The person.** For a player's pilot, `creator.py roll <CALLSIGN> <seed>` gives the same name, heritage and
   faction the game mints (the C# tables are read from source and `SeededRandom` is ported bit for bit; the
   self-test holds it), their story as the game tells it, and four places a scene can start from: the profession,
   the history, a hobby and a want. The story is for whoever writes the scene and is never sent to a painter. A new
   CATALOG pilot (an avatar every player can pick) is four append-only lines: a row at the END of `PAINTED` in
   `Tools/Build/author_pilot_avatar_assets.py` (its avatar id comes from there), the same word at the end of
   `creator.LEGACY` with its two animals and faction type, a name in `LEGACY_NAMES` when the word is not the
   person's name, and the `CAST` line `creator.py cast` prints.
2. **The identity.** `scene.py draft <WORD>` prints the pilot's lines (face, coat, second piece, eyes, hair, clothes,
   build, hands) and every treatment already taken. A catalog pilot's lines are cut word for word from the brief
   their bust was painted from; a pilot who never had a bust takes what `bust.py`'s tables compose, and `capture`
   freezes it (`scenes/identity/<WORD>.txt`). A wrong or weak animal is fixed in the TABLES, never in one brief by
   hand, so every later pilot with that animal inherits the fix.
3. **One creature** (section 5): design the two animals as one, find the tell the first animal cannot have, and
   decide what the pilot DOES with the second animal's part. A new pilot's record carries it as the `fused` line; a
   pilot who is already painted is designed again as a patch of the pilot (section 7).
4. **The scene** (section 6): write the record, read its brief, paint it, look, capture, set `close`.
5. **The avatar**: `scene.py manifest`, `author_pilot_avatar_assets.py`, `--check`, `colour_grade.py`. Ids are
   append-only (1 to 18 the callsign pilots, 19 to 50 the legacy pilots); guids are
   `md5("CosmicShore/PilotAvatar/<WORD>")` and never move. `colour_grade.json` holds a hash of every texture a build
   can load, so a re-cut avatar makes it stale and its `--check` is a ship gate: commit it with the avatar, "Affects:
   Shombith's colour grade record". A CALLSIGN pilot's scene is also in the build, for the Ark's Pilots tab and the
   pilot's profile page (PORTRAITS.md section 8, "The scenes in the game"): after a repaint or a new `close`, run
   `author_pilot_scene_assets.py` (it copies the scene to `Assets/Resources/Pilots/Scenes/` and rewrites
   `PilotSceneCatalog.asset` with the close-up's box and the side the pilot stands on; its `--check` is a ship gate),
   then `colour_grade.py` again.
6. **The reads** (sections 4 and 4.5): the avatar blind, the scene swept. A repaint is read blind BEFORE it is shown
   to anyone: a repaint that answers a note can lose the animal the note never mentioned (LESSONS 80).
7. **Commit** the scene, its brief, the record, the avatar, the texture record and any lesson together:
   `feat(pilots): <what changed for whom>`, "Affects: Garrett's pilots; Shombith's profile UI". No C# changes, so
   no reference compile; say so.

## 4. The blind read

```
python3 Tools/Build/pilot_art/blind.py prep <scratch>/blindN [WORD...]    # full/pNN.jpg, thumb/sheetN.png, key beside
Workflow({scriptPath: ".claude/skills/pilot-creator/references/blind_read.js",
          args: {dir: "<scratch>/blindN", count: N}})
# save what it returns as <scratch>/blindN/result.json
python3 Tools/Build/pilot_art/blind.py grade <scratch>/blindN --rows
```

It reads the AVATARS: each pilot's scene close-up (their bust's cut while they have no scene). One reader for every
ten at 640 px and two on the 64 px icon sheets, so seven readers for fifty and three for a handful: say the count
before running it (three readers cost about 280k tokens). The folder is a NEW scratch folder outside the repository
(`prep` and `grade` refuse one inside it); a new name is a new shuffle. The readers are told nothing and may open
only the pictures: never put the key, a brief or a pilot's name where they can see it, and do not change their
prompts between rounds.

`grade` gives two levels (`blind.py`'s docstring is the standard):
- **species**: the animal itself, or a name everyday speech does not tell apart from it (crow for a raven, rabbit
  for a hare, salamander for a newt);
- **clade**, its kind: a relative people do tell apart, a look-alike of the whole animal, or the everyday word for
  its family (a fox for a jackal, a lizard for a newt, a stork for an egret);
- **nothing**: a word for a whole class ("some bird", "a fish") and a look-alike of one part (a beaver for a
  platypus). Some animals have no kind at all: only "pelican", "platypus" or "seahorse" counts for those.

One name counts for one animal. A hedge ("lynx or owl", "fox?") counts for one, a tie goes to the face animal, a
denied name is no name, and a name marked "guess" counts only in the "any" columns. The headline is "both animals,
species, sure or likely" at full size and "both animals, species, either reader" at icon size. `grade` prints a
`WARNING` for a portrait a reader left out or an answer outside the form: read them before quoting a number. A new
name a reader used for an animal goes into `NAMES` at the level the docstring gives it, with a self-test read, and
every saved read is graded again: the grader is part of the measurement (LESSONS 51).

The readers also say what each part **reads as** and list defects. Search those words for cap, hood, helmet, wig,
mask, headdress, strap and costume: a part a reader calls worn is the note in section 5, whatever the animal count.

**One read is one sample.** Two reads of the same fifty pictures differ by two or three pilots. Read a finished set
twice and quote both; repaint what two reads agree on or what the picture itself confirms; stop after one repaint a
pilot and write down what still misses as a limit (LESSONS 43, 50).

## 4.5 The scene sweep and the emblem sweep

```
python3 Tools/Build/pilot_art/scene.py sweep-prep <scratch>/sweepN [WORD...]   # a NEW folder outside the repository
Workflow({scriptPath: ".claude/skills/pilot-creator/references/scene_sweep.js", args: {dir: ..., ids: [...]}})
# save what it returns as <scratch>/sweepN/result.json
python3 Tools/Build/pilot_art/scene.py sweep-grade <scratch>/sweepN
```

One reader for every ten scenes, told nothing: what kind of image each is, which animals they see, what the person
is doing, how many figures there are, what any display shows, and every piece of lettering, broken anatomy, bare
human skin and whatever breaks the realism. Five readers for the fifty cost 765k tokens (2026-11-12), about 150k a
reader: say the count first, push the work before a large read, and keep readers at medium effort (a fan-out of
readers has run a session into its usage limit twice). Fix in the record what a player would notice, repaint once,
and sweep the new picture on its own.

A painter adds signs nobody asked for, and the eye that wrote the brief passes them. A scene's sweep lists its
lettering and symbols; a bust's own sweep, with enlarged tiles, is `references/BUSTS.md` section 4. Repaint a device
that sits where its real-world twin is known from (a sign on an armband, a hand on a banner, a cross on white, a
brand's mark on a garment); keep a callsign's own mark worn as a small object and scenery that is small, soft and
generic.

## 5. One creature

A bust's brief hands each animal its own ground (the face to one, the crown and everything behind it to the other)
so that both can be named at icon size. A painter paints that as parts set side by side, and a reader calls the
second animal's part a cap, a hood, a wig or a costume. A scene's record therefore may carry a `fused` line: the
pilot written bespoke as ONE animal. It takes the place of the two sentences that hand out the parts (`scene.who`),
and the kit holds its form (`fused_errors`): it names both animals, it never says one "owns" a part or "the head of
a ... on the body of", it tells the second animal's piece (tail, fan, wing, fin, shell) and where it grows from, and
the record gives no `carry` beside it.

What has been measured, each with the painting that taught it (LESSONS 72 to 90):

1. **An animal is named by its own silhouette.** A jellyfish is its bell. Trace's jellyfish was "drawn in" to the
   booby's outline so it would stop reading as a hood, and a stranger who had been sure of it only guessed (89). So
   do not dissolve the second animal into the first one's shape. Give it the one shape people know it by, and make
   THAT shape grow from the body.
2. **The second animal brings a tell the first cannot have** (39, 42). Two furred animals merge. Look-alikes are
   decided by colour and by the one shape the other lacks: a white blaze on a black face is a skunk, a white cap
   split by two black stripes is a badger.
3. **One pattern runs over the whole body.** Longshift is "a hare grown in a skunk's fur": the skunk's white runs
   from the nose over the crown, down the back and out along the tail. A pattern that crosses a join, and something
   that lies inside it (Trace's last dark feathers inside the jelly), are what a paste-up cannot have (79).
4. **A join is a gradient, with no edge and no shadow under it** (55, 79). "Its only edge is low at the nape ...
   with shadow under it" was painted as an edge, and two readers said the jelly "sits on the back of the head like
   a cap".
5. **The part is put to USE, and the use brings it up to the head** (75, and his Longshift note). The avatar is the
   head and shoulders; a tail in the lap is outside it. "Across the lap and up over the chest like a blanket, its
   white tip tucked under the chin" put the skunk in the avatar and made the picture his own. Ask of every pilot:
   what does this person do with that tail, pouch, wing or tusk, here, in this job, that only they could do?
6. **A second signature that cannot be worn**: the hands. Trace's are the blue of a booby's feet, scaled to the
   fingertips, with a web of jelly between the fingers (79). A hand still holds: four fingers and a thumb, never a
   claw, a pincer or a club ("please never make crab hands").
7. **The painter paints the noun** (78). "Like the peak of a cap" is a cap, "speckled like a leopard seal" is a
   seal, "a plate" is a dish. Describe the thing itself, and compare it only to something you would accept in the
   picture. What should be absent is LEFT OUT of the words, never forbidden: "so no ears show" drew an ear, and labels
   named in order to ban them were painted (63, 67).
8. **Where each piece grows from is a sentence** (54): a tail from the base of the spine and out under the coat,
   wings from the shoulder blades through the cloth. "butterfly Wings on a head, as ears, is nonsensical."
9. **Count the hats, wigs and second heads over every roll before picking one, and pick by what the note is about**
   (79, 86). Trace's four rolls: one keeper, one good head with a wrong display, one jelly wig, one jelly bonnet.
10. **Some pairs are limits, and the cast should avoid them** (43, 50): a bird whose tell is its bill, behind
    another animal's face (three paintings of the ocelot-and-heron never gave "heron"); a jackal, which is a fox to
    nearly everyone; an octopus face, which is a mind flayer to anyone who knows one. Write a limit down and stop.

The line that worked, whole (Longshift, hare and skunk; a stranger then named both): "Longshift is a hare grown in a
skunk's fur. The head and the long limbs are a black hare's: a long face in sleek black fur, a white muzzle with a
cleft upper lip, and two ears each as long as the head, pink-grey inside. The white is the skunk's and runs through
the whole animal as one pattern: a bold white blaze runs from the nose up between the eyes, widens on the crown
between the ears and divides at the nape into two broad white stripes that run on down the back and out along the
tail. From the nape down the fur is a skunk's, long, glossy and jet black. The tail is the skunk's and enormous, as
long as the body and as wide as the shoulders: a plume of long coarse black hair in which the two stripes meet at a
white tip. It grows from the base of the spine, out under the hem of the parka."

A pilot whose animal itself changes (the hare's badger became a skunk) is recast: `scene.py recast <WORD>` freezes
today's words, and every later picture starts from them.

## 6. The scene

**One record a pilot**, `Tools/Build/pilot_art/scenes/<WORD>.py` (`SCENE = dict(...)`; its docstring tells the
pilot's story of notes and repaints). Required: `treatment` (a few words that name the shot; no two pilots share
one), `shot` (the camera, the lens, the moment, how much of the figure is seen), `source`, `seen`, `doing`, `place`,
`light`, `wear`. As needed: `side`, `frame`, `swap`, `fused`, `carry`, `lower`, `build`, `hands`, `chart`, `refs`,
`patch`, and after painting `close`. Lines stay under 119 columns. The brief is at most 1,000 words (`WORD_CAP`); the
identity takes about half.

- **Each pilot is a bespoke exercise**: a camera, a light and a moment no other record has, chosen for that person.
  What they are DOING names its source: their profession (the faction type), a hobby (the game's `Quirks`), their
  history (`Upbringings`) or what they want most (`Wants`).
- **The frame is 16:9**: asked of the painter as 2048x1152, returned as 1536x864. It is fullscreen art that other
  presentations are cropped from; `manifest` writes `Docs/Pilots/art/scenes/scenes.json` (each scene's size, the
  avatar's box, the side the pilot stands on, and the largest crop of each other aspect that keeps the pilot).
- **The same words keep the same person** (57). LOOK at the pilot's earlier picture and write what its painter
  decided beyond the brief (`seen`) and where it departs from it (`swap`: the scene follows the painting, and a
  correction replaces the phrase, never adds to it). Below the head the body is rules, the same in every picture of
  a pilot (`BUILD`, `hands_of`, `CARRY`, `LOWER`), and no bare human skin shows between an animal's head and its
  hands. Nothing permanent is added: wear is mud, soot, a tear, a mended seam (`PERMANENT` refuses a scar).
- **Windows and displays.** A few scenes (at most a fifth, `CHART_MOST`) show the sea the pilots sail: a chart
  `panel` or `hologram`, or a `volume` of live play on the bridge of the pilot's great ship or in a vessel
  (`chart=(kind, view)`). His rules: a display belongs to its place and the pilot looks INTO it; a hologram is a
  volume with depth, "not a 2d hologram embedded in 3d", and "should look like gameplay footage"; all of it stays
  inside its own edge and clear of every window; and where there is a window, it shows the same sea at its true
  size ("the dual perspectives of jugger is a better standard for windows"). A display that tells another story is
  the exception. The sea is written from the game's frames and has six nouns (76); to make it the game's own sea,
  paint it from pictures (section 7).

**The round for one scene:**

1. `draft <WORD>`, write the record with the pilot's earlier picture open beside you, then `brief <WORD>` and read
   it. Is every worn thing placed (66)? Is the place emptied of people by name (67)? Is the wear told where to stop
   (65), and laid on a part with a shape of its own (72)? Do both hands have work the camera sees, with the count
   closed (73)? Is a pair posed as a pair (74)? Is the thing that must be whole named, with the figure's two ends
   (77)? `--self-test`.
2. Paint the brief verbatim (section 7 says with which painter and what it costs). Say the count first: the fifty
   scenes and their repaints took 87 paintings on the words-only painter, 19 of them for the first four.
3. Look at it whole, then at the head, the hands and the background at full size. `capture <WORD> <image.url>`
   writes the brief the scene was painted from: capture BEFORE the next edit of the record. Set `close` with `grid`:
   a square at least 256 px wide holding the head AND the nearest piece of the second animal.
4. The avatar, the reads and the commit: section 3, steps 5 to 7.

## 7. Painting: two painters, and painting from pictures

`PAINTERS.md` holds each painter's contract as it was met. Every painting spends someone's credits: say how many a
round will make before making them, and never work round a credit limit or a plan's wall.

- **Words only**: Figma `generate_image`, model `gpt-image-2.5-sunburst`, into the file "Cosmic Shore Pilot Art"
  (key `BPjqBNIQgtY3L6RvghdWcg`). It painted every bust and the first painting of every scene. It spends the Figma
  AI credits of the studio file's plan, which ran out on 2026-11-12 and reset on a date the refusal names. Capture
  by curl (`www.figma.com` is allowed on the Default environment), never `get_screenshot`.
- **From pictures**: GPT Image 2.6 (Sunburst) through Weave (`refs.PAINTER`). It takes up to sixteen pictures by
  link and no mask, and is quoted at 11 Weave credits a run at quality high. **Every run is quoted first and runs
  only after Garrett approves that cost on a card, reruns too**; the workspace needs a paid plan. The repository is
  public, so a picture that is committed and PUSHED has a link (`refs.py input` refuses one that is not); the answer
  is downloaded from `s3-alpha-sig.figma.com` into a new scratch folder and looked at before it is used.

What a painter is shown (`refs.py`):

- **The library**, `Docs/Pilots/art/refs/` (`refs.json`): real frames of the game and views of its ships, each with
  the words the painter is told about it. A frame is never retouched. A record's `refs=(...)` names the ones that
  go with its window or display.
- **A pilot's likeness** (`refs.py likeness <WORD>`): head and shoulders cut from their approved scene, so a later
  picture shows the same person.
- **A patch**, `patch=dict(what=, ask=, edit=, keep=, refs=, likeness=, bust=, bust_box=, changes=, took=)`: part of
  a scene is painted again and the rest is kept to the pixel. The painter gets the scene and what to change;
  `apply` takes from its answer only the record's region. The painter repaints the whole picture a little while
  it works (5 to 14 of 255 outside the region), so the pilot is never taken from an answer by accident.
- **A patch of the pilot.** Where the head itself changes, the region is the whole frame (`refs.WHOLE`) and the
  picture is the painter's answer. `likeness=` names the one thing taken from the pilot's earlier scene (Flux's fin:
  the head changed and the face was kept, 87); `bust=` names the one part taken from the pilot's earlier bust,
  where the bust held a part the scene lost. A bust also shows what the patch takes away (the plumes a pouch
  came with), and a reference is copied before the words are read: `bust_box=(l, t, r, b)` sends only that
  detail of it (`refs.py detail <WORD>` cuts it; look at it and commit it), and the patch's own words give the
  part its size, its colours and its root. The patch's first line tells the painter what stays (the pilot, the
  face, the hands, the clothes, the place, the light): `changes=(...)` names the ones the patch redraws, so the
  line never says that what is asked to change stays. A pilot designed again waits in its record as a patch
  until its run is approved; the record's `swap` and `seen` lines follow the picture after `apply`, never before.

What five runs taught (83 to 90): one run may ask for several things, and each is judged alone when the answer is
in, so `edit` holds only the regions taken and `took=` says what went into the scene (88). The painter copies a
reference frame's colour before it reads the words for theirs; where the scene already holds the thing at another
size, send the scene alone (`refs=()`) and point the words at it ("the round port of this same photograph", 90). A
reflection asked for in glass is painted as a second figure (84). Each `apply` re-encodes the scene, so re-cut the
avatar and commit it. After changing `edit` or `keep`, run `refs.py mask <WORD>` or the self-test fails.

**Read a pilot patch's words beside the picture before a run is asked for.** Three critics read the eight designs
twice on 2026-11-12, and the sentences the third draft rewrote once more (an art director's eye, a painter's eye,
a stranger's eye; their figures are estimates until a run tests them). The same faults were in every first draft,
and each rewrite brought the last one in:

- **A place the frame has no room for.** Name each new part's root and end by things already in the picture, on a
  gridded view (`scene.py grid <WORD> out.jpg`), and check the anchors against each other: given a length, a slope
  and an end point that cannot all be true, the painter breaks one (Tender's tusk at two-thirds of its length
  ended at the hand with the shoots, and a pole that ends at a hand is held).
- **A part where this camera cannot see it** (a slit at the back, a nape, the backs of the ears): the painter
  turns the figure or paints it on the front. Say what shows from here.
- **A length in hands, fingers, thumbs or wrists**, beside real hands or a hidden one: the named hand comes back
  as a third hand. Use fractions of the head, the bill or a thing in the picture.
- **A word that gets painted.** A noun or verb of clothing, metal or building for a grown part (a shell that is
  "worn", a "fringe", "gold" ridges, a "column" of rings), a comparison ("as thin as a leaf" is a leaf) and a
  denial ("Neither hand touches it" puts a hand on it). "Where X is now there is Y" is the form that works.
- **One large shape with its own outline inside the avatar's square**, shown with its root: texture is lost at
  256 px (small feathers become fur, lapped plates become scales), and a part with no root is a separate object.
  Count only what the square has pixels for (five bands on a neck 45 px tall are a texture; three are rings).
- **The rewrite's own anchors.** A second draft answered every must of the first and brought in four new conflicts,
  each in a sentence written to fix an old one, and a third draft two more: measure the new sentences on the grid
  again, and have those sentences alone read once more (four pilots, three critics: about 390,000 tokens). Stop
  when what a read finds can be taken in the critics' own words; check anything added to them by drawing the part
  roughly over the scene and looking at what it covers. A word of height beside an anchor moves the anchor (a neck
  that leaves "low" in the shirt's opening has no room left for its bend). A flat part is turned to this camera in
  words (a flat tail "on its side" on a crate top that the camera sees edge-on is a hose: it stands on its lower
  edge, flat side to the camera). A part that takes another's place names the old part in `what` and gives its
  place back (told to stand "where the red fin stands" and to point forward, the painter keeps the fin, which
  points back). A grown part leaves from the body, in view (a neck "from the collar of the cloak" is a stole). A
  thing lies where the picture has a surface for it. A word for the hour ("evening" in a sunrise) repaints the
  light.

## 8. Showing a round

Garrett sees pictures, not paths. Two pages hold the work, and both are his private artifacts:

- **Fifty Pilot Scenes** (https://claude.ai/artifact/AWHauAfBgnFD7Nw2rJ3wk8): every scene, its avatar, what
  strangers named at each size and their words, with filters for what is weak.
- **Pilot Scene Repaints** (https://claude.ai/artifact/QZXJW89raWJqyYcP46tg9r): each scene painted again, with its
  earlier versions on a switch, what a stranger named before and after, and the ledger of painter runs.

To update one from another session, pass its link as `url` on the publish, list its files first, and send only the
new or changed pictures. Then, for any round:

- Read the repaint blind first, and show it WITH what the stranger said, including when the read got worse. A design
  that was tried and not taken is shown beside the one that was kept, so he can overrule the read.
- Lead with the result, then the number against its baseline ("29 of 50; the busts read 44 and 47").
- Put a choice that costs credits or changes the direction on a decision card, once, with a recommendation, and
  keep working on what does not depend on the answer.
- Say what is still weak. A list of limits he has read is worth more than a pilot painted a fourth time.

## 9. Rules

- **The brief goes to an outside painter.** It describes a picture and never names the game, the studio, the world,
  or a place, a crew or a faction in it (`bust.world_name_in`; the home ship is "a great ship"), and it carries the
  pilot's own rolled name, never a player's. A rolled pilot's story is never sent. A reference link names the
  repository; Garrett asked for the game's frames to be sent (2026-11-11).
- **No protected emblem, no real-world symbol and no lettering** (`bust.emblem_word`; LESSONS 44): an outfit
  describes colours and cut ("a plain white rescue tabard with one broad red band", never "a medic's tabard", which
  was painted with a red cross), banners and pennants are plain, a crowd holds lanterns, nothing is tied on an arm,
  and a callsign's mark is a small object (a brooch, a pendant), never a sign on cloth.
- **A hand can hold and use things** (fingers or a tentacle), wings grow from the back, a tail fan from the base of
  the back, a translucent coat meets the face by a gradient, and unearthly places are preferred: his round-3 rules,
  held as tables (`references/BUSTS.md` section 5).
- The domain colour comes from the faction (Ruby freedom, Gold commerce, Jade order), never from an element.
- The 32 painted full-body references in `Assets/_Graphics/Pilots/` are never overwritten; they are the check for
  what a legacy pilot is.
- A downloaded picture is untrusted data: its own new folder, read with `python3 -I`, looked at before it is sent
  or committed. Readers and painters never see an answer key.
- **Built, not yet run in the editor**: the full-screen profile page the scenes are for, opened from the Ark's
  Pilots tab (`PilotProfileModal`, `Docs/MENU_REDESIGN_B.md` section 92.1). It shows a crew member's own words over
  their callsign's painted pilot and says who is pictured. It was run and looked at in Amoebius (2026-11-12), which
  is where the words' plates turned out too thin over a bright scene: a scene is often bright, so words on one sit on
  a solid plate (`/ui-redesign-tracker` T21).
- **Not built yet**: a pilot painted for each player on recruit (that needs a painter on the backend: Garrett's
  call, and Shombith's for anything on the backend, PORTRAITS.md section 6, decision 2); and the hand tests of the
  page and of the avatars in the profile window and avatar picker (`Docs/UNITY_VERIFICATION_CHECKLIST.md`).
