---
name: pilot-creator
description: Create a Cosmic Shore PILOT as a signature triple-hybrid bust - a unique person (human + two animals, a faction, a callsign) rolled exactly as the game's PilotGenerator mints them, painted as a head-and-shoulders bust where the first animal owns the face, the second owns the crown and everything behind the face, and the human is the bearing, the clothes and the gaze, standing in one of many places, then cut into the player avatar (Assets/_Graphics/Profile/Pilots/Avatar_<NAME>.png). Use on "/pilot-creator", "make a pilot", "give this player a unique pilot", "roll a pilot", "paint a bust / avatar for <pilot>", "the avatar only shows one animal", "repaint <pilot>'s avatar", "add a pilot to the avatars", "blind read the busts", "do both animals read", "sweep the busts for emblems", or any change to Tools/Build/pilot_art/bust.py, creator.py, blind.py or Docs/Pilots/art/busts/. Also holds the realistic SCENE of the same pilot (Tools/Build/pilot_art/scene.py, Docs/Pilots/art/scenes/; four sample approaches, the style NOT locked until Garrett picks): "a scene of <pilot>", "the realistic art", "art for the Ark", "does the scene look like the avatar". The full-body Hangar portrait is /pilot-art.
---

# Pilot Creator: the signature triple-hybrid bust

Garrett, 2026-11-10: "can you get us pilot images that really show both clades in the avatar bust when used in that
context. I want to produce a character creator skill that will allow us to give players unique pilots. I want you to
send the quality to its highest levels such that when people see it they know it is cosmic shore because only we have
triple hybrid people with that unique and high quality style." Round 1 painted all 50 avatar pilots on one navy plate
with a hand in every frame. Garrett, 2026-11-11: "a good step forward, but the backgrounds need much more variance.
they don't all need a hand ... any creature that already has a hand or tentacle like a frog, primate, or octopus it
doesn't need a hand. the human side is the least important to express when it is wearing clothes". Round 2 is what
this skill paints (Docs/Pilots/PORTRAITS.md section 7). Garrett on round 2, 2026-11-11: "These are great. only a few
need changing", with five rules that are now in the tables (section 5, "Round 3") and eight busts repainted. He also
named what the skill is for: "a large enough roster that every pilot is unique and each player has their own unique
crew. They will all use the same callsigns which connect to the same vision abilities (not unique) but the art,
backstory, species combination, etc. will all be unique." And he asked for a second style: the same character in a
hyper-realistic scene, "raw in their environment". Section 6 holds the four samples and the mechanism; that style
is not locked.

## 0. The bar

A stranger who is told nothing names BOTH animals from the bust. It is measured, never felt: the **blind read**
(section 4) shows the busts shuffled and unnamed to readers who may open only the pictures. Each round was read
twice. Round 1: both animals named in 26 and 27 of 50. Round 2: 44 and 47, and their kind (a fox for a jackal) in
48 and 50; at 64 px icon size 23 and 29, from 10 and 12.

**Each heritage has its own ground inside the crop (bust.py's tables, never a hand prompt):**
- **First animal: the FACE and skull** (`FACE[a]`), and its own eyes where the eye is the animal's tell (`EYES[a]`:
  a tree frog's red eyes; human eyes in a frog's face read as a goblin). A beak stays rigid (`BEAKED`). A tell that
  grows from the head stays on the face (`KEEPS`: a hare's ears, an oryx's horns). Ears that are only part of the
  look appear when the crown brings none (`FACE_EARS`: a cat's, a badger's, an opossum's).
- **Second animal: the CROWN** (`CROWN[b]`: what grows from the head) **and everything behind the face**: its coat
  (`MARK[b]`) over the back of the head, the nape, the throat and the neck, blending into the face along the jaw;
  **plus one more piece of its body in the frame** (`SECOND[b]`: a tail, folded wings, a shell rim) or the hand
  people know it by (`LIMB_TELL[b]`: an otter's paws around a pebble). Wings, a tail and a tail fan grow from the
  BACK and stand behind the clothes, never from the head. A translucent coat meets the face in a written gradient
  (`BLEND`), not at a seam. When the face keeps the crown's organ the crown is `ALT[b]` instead of a second pair;
  `EARS_TOO[b]` adds small ears to a crown that is not ears.
- **Human: the bearing, the CLOTHES and the gaze** (`GEAR`: three outfits per faction type, all open at the throat
  so the second animal's neck shows). Human hair for some (`HAIR_ODDS`; never over a crown that covers the scalp,
  never beside an animal's own mane, `OWN_HAIR`). **A human hand almost never** (`HAND_ODDS`, gloved): never beside
  hair, and never on a pilot whose animal has a hand, a paw or a tentacle of its own (`LIMB`), which shows that
  instead about half the time. **A hand can always hold and use things**: fingers or a tentacle, never a claw, a
  pincer, a club or a hook. Of the 50: 10 have human hair, 10 show their animal's own hand, 3 a gloved hand.

**What every bust shares, so the set reads as one:** the framing (the head in the upper middle, clear space above
everything that grows from it, cropped at mid-chest, looking at the viewer), the premium painterly finish, the
faction's DOMAIN colour (Ruby #C800C3, Gold #FFA700, Jade #0EC0B6) as the clothes' trim, the rim light and a thin
glowing ring around each eye (the pilot's sight), and a few crystal motes in the air.
**What differs:** the place (`BACKDROPS`: 73 places, each with its light, its value and the faction types whose home
ground it is; 37 of them could not be on Earth, `UNEARTHLY`, and a new pilot rolls those three times as readily), the
outfit, the pose (`POSES`) and the expression (`EXPRESSIONS`). The 50 catalog pilots stand in 50 different places;
what each was painted with is frozen in `creator.CAST`. A new pilot may also show more of the human side (`roll_more`:
21 hair styles, a human touch such as spectacles, kohl, a scar or a beard, a bare hand instead of a gloved one).

## 1. Read before you touch

- `Tools/Build/pilot_art/bust.py` (the tables and the brief), `creator.py` (the roll, the catalog, the cast,
  capture, stale, sheet, tiles) and `blind.py` (the blind read's shuffle and grading). For a scene, `scene.py`
  (its docstring is the mechanism) and `Docs/Pilots/PORTRAITS.md` section 8.
- `.claude/skills/pilot-art/references/LESSONS.md` sections "signature busts" (rounds 1 to 3) and "the scenes"
  (57 to 71), and `PAINTERS.md` section 1.
- The pilot record: `Docs/Pilots/INVENTORY.md` section 3; `Assets/_Scripts/Data/Inventory/` (`AnimalSpecies`,
  `FactionCatalog`, `PilotGenerator`, `SeededRandom`).
- `git fetch origin && python3 Tools/Build/who_works_where.py query Tools/Build/pilot_art Assets/_Graphics/Profile`.
  Pilots and their art are Garrett's; the profile window and avatar picker are Shombith's UI.

## 2. Commands

```
python3 Tools/Build/pilot_art/creator.py roll TALON 0x3039     # the person the game mints for that seed
python3 Tools/Build/pilot_art/creator.py roll TALON            # a fresh seed: a new unique pilot
python3 Tools/Build/pilot_art/bust.py --callsign TALON --heritage Mole Newt --faction Pirate --name "Lulolo Dawnwhistle"
python3 Tools/Build/pilot_art/bust.py --backdrops              # the places
python3 Tools/Build/pilot_art/creator.py brief <WORD>          # the brief of a catalog pilot (the 50 avatars)
python3 Tools/Build/pilot_art/creator.py list                  # the catalog, each pilot's place, which have a bust
python3 Tools/Build/pilot_art/creator.py cast                  # CAST lines for catalog pilots that have none
python3 Tools/Build/pilot_art/creator.py capture <WORD> <url>  # save a painting and the brief it was painted from
python3 Tools/Build/pilot_art/creator.py stale                 # busts today's tables would paint differently
python3 Tools/Build/pilot_art/creator.py sheet out.png [WORD...]   # bust + octagon avatar sizes, for looking
python3 Tools/Build/pilot_art/creator.py tiles <scratch dir> [WORD...]   # whole + four enlarged quarters (4.5)
python3 Tools/Build/pilot_art/blind.py prep <scratch dir> [WORD...]   # shuffle the busts for the blind readers
python3 Tools/Build/pilot_art/blind.py grade <scratch dir> --rows     # grade what they said, worst first
python3 Tools/Build/author_pilot_avatar_assets.py              # re-cut every avatar (256 px) from its bust
python3 Tools/Build/author_pilot_avatar_assets.py --check      # the drift gate
python3 Tools/Build/colour_grade.py                            # after a re-cut: the texture record follows
python3 Tools/Build/pilot_art/scene.py list                    # the scene samples and their approaches
python3 Tools/Build/pilot_art/scene.py who <WORD>              # an avatar pilot's identity lines, as painted
python3 Tools/Build/pilot_art/scene.py brief <WORD> [approach] # field | film | candid | sitting
python3 Tools/Build/pilot_art/scene.py capture <WORD> <url> [approach]
python3 Tools/Build/pilot_art/scene.py sheet out.png           # each scene beside its avatar, for looking
python3 Tools/Build/pilot_art/scene.py stale                   # scenes today's kit would paint differently
python3 Tools/Build/pilot_art/scene.py read-prep <scratch dir> # the readers' pictures, shuffled; key beside it
python3 Tools/Build/pilot_art/scene.py read-grade <scratch dir>
for m in bust creator blind scene; do python3 Tools/Build/pilot_art/$m.py --self-test; done
```

## 3. The round

1. **The person.** For a player's pilot, `roll <CALLSIGN> <seed>` gives the same name, heritage and faction the game
   mints (the C# tables are read from source and `SeededRandom` is ported bit for bit; the self-test holds it), and
   its look (place, outfit, pose, mood, hair, limb) is rolled from the name. For a catalog pilot, `brief <WORD>`
   composes from its `CAST` line. A new catalog pilot is four lines, all append-only: a row at the END of `PAINTED`
   in `Tools/Build/author_pilot_avatar_assets.py` (its avatar id comes from there), the same word at the end of
   `creator.LEGACY` with its two animals and faction type, a name in `LEGACY_NAMES` when the word is not the
   person's name, and the `CAST` line `creator.py cast` prints. When `cast` says every place that pilot may stand
   in is taken, add a place to `bust.BACKDROPS` first: no two catalog pilots share one, and the self-test holds it.
2. **The brief.** Read it before spending a painting, and ask: does the crown swallow the face (a hood of head
   feathers makes the head that bird)? Does the face line say the animal's tell (the oryx's long horns, the snail's
   stalk eyes)? If the two animals share a texture, does the second have a tell the first cannot have? Is there one
   pair of ears, and does the brief say whose? Would any word summon an emblem or lettering (a medic's tabard, an
   armband, a crowd's raised fists, a banner), or would two phrases do it together (a bandana on an arm beside a
   crosshair-marked eyepiece came back as an armband with a ring and a cross on it)? A wrong or weak animal is
   fixed in `bust.py`'s TABLES, never in the prompt by hand: every pilot with that animal inherits the fix. Then
   `--self-test`.
3. **Paint**: Figma `generate_image`, model `gpt-image-2.5-sunburst`, 1024x1024, the brief verbatim, into the file
   "Cosmic Shore Pilot Art" (key `BPjqBNIQgtY3L6RvghdWcg`). Each call spends Figma AI credits: say the count up
   front. Paint in batches of five and capture each batch before the next.
4. **Capture** with `creator.py capture <WORD> <image.url>` (curl; `www.figma.com` is on the Default environment's
   allow-list since 2026-11-10). Never `get_screenshot`: the Collab seat's read limit is about 30 calls a day.
   Capture also writes `Docs/Pilots/art/busts/briefs/<WORD>.txt`, the brief the bust was painted from: capture
   BEFORE the next table edit, and never edit that file by hand.
5. **Look** at the 256 px thumbnail the tool returns, then at the sheet. Obvious misses (the second animal absent,
   a stray emblem, a wrong species) are fixed in the table and repainted now. Then zoom: crop every hand, paw and
   claw at full size and count (a badger's paw came back with eight claws, and two blind readers counted six
   fingers on a glove that has five), and look over the clothes and the place for an emblem nobody asked for.
   A whole round gets the emblem sweep (section 4.5): the eye that wrote the brief passes what it did not ask for.
6. **The blind read** (section 4) for any round of more than a handful, and after a table change that many pilots
   inherit. A pilot whose animal is missed gets its table line fixed and one repaint; read the final set again.
7. **Write** the avatars: `author_pilot_avatar_assets.py` then `--check`. Ids are append-only (1-18 the callsign
   pilots, 19-50 the legacy painted pilots); guids are `md5("CosmicShore/PilotAvatar/<WORD>")` and never move.
   Then `python3 Tools/Build/colour_grade.py`: its record (`Docs/VesselModels/colour_grade.json`) holds a hash of
   every texture a build can load, so a re-cut avatar makes it stale and its `--check` is a ship gate. Only the
   re-cut avatars' entries should change.
8. **Commit** the busts, their briefs, the avatars, any table change and the lessons together:
   `feat(pilots): signature bust of <Person> (<WORD>)`, "Affects: Garrett's pilots; Shombith's profile UI".
   No C# changes, so no reference compile; say so.

## 4. The blind read

```
python3 Tools/Build/pilot_art/blind.py prep <scratch>/blindN          # full/pNN.jpg, thumb/sheetN.png, key beside
Workflow({scriptPath: ".claude/skills/pilot-creator/references/blind_read.js", args: {dir: "<scratch>/blindN", count: 50}})
# save what it returns as <scratch>/blindN/result.json
python3 Tools/Build/pilot_art/blind.py grade <scratch>/blindN --rows
```

Seven readers (one for every ten portraits at 640 px, two on the 64 px icon sheets): say so before running it.
The folder is a scratch folder outside the repository (`prep` and `grade` refuse one inside it); a new folder name
is a new shuffle. The readers are told nothing and may open only the pictures, so do not put the key, a brief or a
pilot's name where they can see it, and do not change their prompts between rounds.

`grade` gives two levels. `blind.py`'s docstring is the standard, and every round is graded by it:
- **species**: the animal itself, or a name everyday speech does not tell apart from it (turtle for a tortoise,
  crow for a raven, rabbit for a hare, heron for an egret);
- **clade**, its kind: a relative people do tell apart, a look-alike of the whole animal, or the everyday word for
  its family (a fox for a jackal, a cat for a lynx, a lizard for a gecko, a stork for an egret);
- **nothing**: a word for a whole class ("some bird", "a fish") and a look-alike of one part (a beaver for a
  platypus).

One name counts for one animal. A hedge ("lynx or owl", "fox?") counts for one, and a tie goes to the face animal.
A name the reader denies ("a dog, not a fox") or takes back ("fox? no, a dog") is no name, "fox-like" is the kind
at most, and a name used as a colour or a look ("salmon pink", "leopard print") is no animal.
`blind.py --self-test` holds each rule with a read that fails when the rule is broken (183 reads, each also with
its names in the other order). The headline is "both animals, species, sure or likely" at full size, and "both
animals, species, any reader" at icon size. `grade` prints a `WARNING` line for a portrait a reader left out, an
id that is not in the key, an answer outside the form, and a portrait where one name hedges between the pilot's own
two animals: read them before quoting a number. A new name a reader used for an animal goes into `NAMES` at the
level the docstring gives it, with a self-test read, and every saved read is graded again: the grader is part of
the measurement.
The readers also report hands, hair, defects and anything that reads as a mythical creature: `--rows` prints them
under each portrait (human hair is counted from the human features a reader listed, since "none; shaggy fur" is
also a hair answer).

**One read is one sample.** Round 1's 50 pictures, read twice by different readers, gave 26 and 27 on the headline
row and 10 and 12 at icon size, and a pilot or three flips between reads (an armadillo was an armadillo, then a
pangolin). Read a finished set twice and quote both. Repaint what two reads agree on or what the picture itself
confirms, fix the table line first, and stop after one repaint per pilot: what still misses is a limit to write
down (LESSONS 43, 50).

## 4.5 The emblem sweep

A painter adds signs nobody asked for, and the eye that wrote the brief passes them: BANDIT's first round-2 bust
went through two looks and a blind read wearing a red armband with a black ring and cross on it. Before a round
ships, readers who are told nothing look at every bust whole and in four enlarged quarters, and list each drawn
device (an emblem, insignia, symbol, a hand or fist shape, a cross, a star, a figure in a disc), what carries it,
how large it is and which real-world emblem it resembles, plus any lettering:

```
python3 Tools/Build/pilot_art/creator.py tiles <scratch>/tiles [WORD...]
Workflow({scriptPath: ".claude/skills/pilot-creator/references/emblem_sweep.js",
          args: {tiles: "<scratch>/tiles", jobs: [{reader: "A1", words: ["ANVIL", "Astrofoil", ...]}, ...]}})
```

About ten busts a reader; say the count before running it. Then read every "resembles" that is not "none" and
look at that bust yourself. Round 2's sweep read all 50: in the finished set the readers list 88 devices, 61 with
something they resemble, and no legible lettering. Most are what a brief asked for or harmless (an anvil pin, a
helm brooch, compass stars, wreck timbers that cross). Two were what the eye had passed: a gold laurel wreath on
Slingshot's black sash, which is a clothing brand's mark, and a winged sword, a sun and a star on three hall
banners that Spare05's brief had called plain.

- **Repaint** a device that sits on the carrier its real-world twin is known from (a sign on an armband, a hand
  on a banner, a cross on white, a brand's mark on a garment), and heraldry where the brief asked for plain. Fix
  the table line first so the words cannot come back (`bust.EMBLEM`), and have the repaint read again: Spare05's
  second hall has no banner in its brief and still hangs two small ones at the frame's edges.
- **Keep** a callsign's own mark worn as a small object, a compass star, a plain block of colour, and scenery that
  is small, soft and generic (a foliate crest on a far banner).
- **Keep the readers cheap.** The tiles are cut before the readers start, the readers only open pictures, and
  their effort is medium: about 100k tokens a reader before its first picture and about 6k a bust after it, so
  three readers read 29 busts in under three minutes. The first design, in which each reader cut its own tiles at
  full effort, took about 35 minutes and 0.8M tokens a reader, and eight of those at once ran the session into its
  usage limit for eight hours.

## 5. Rules

**Round 3 (Garrett, 2026-11-11). Each is a table rule that `bust.py --self-test` holds, so every new pilot inherits it:**
- **"please never make crab hands (how would they interact with things?)"** A limb in the frame is a hand with
  fingers or a tentacle (`LIMB`, `LIMB_TELL`; the gate is `NOT_A_HAND` and `A_HAND`). The armoured animals (`ARMOURED`:
  lobster, scorpion, mantis, mantis shrimp) wear their shell on a hand with four fingers and a thumb, like a gauntlet,
  and a pilot who also has a true hand (a tree frog with a scorpion's tail) shows the true hand. Give the hand
  something to hold: Comet's lobster-shell hand holds a crystal shard.
- **"butterfly Wings on a head, as ears, is nonsensical."** Wings grow from the back between the shoulder blades
  (`SECOND`), and no `CROWN` line names wings or tail feathers.
- **"the turkey tail feathers are in a very weird spot with respect to the dog's hood."** A tail fan grows from the
  base of the back: its line says "behind the back", never "behind the head", and under a hooded outfit the brief
  says the hood lies in front of it (`BEHIND_HOOD`).
- **"the chameleon transitioning to jellyfish looks a bit abrupt."** A translucent coat has a `BLEND` line: the face's
  skin thins and turns glassy by degrees, with no rim where the bell begins.
- **"The backgrounds that don't look like they could be earth are the best."** `UNEARTHLY` marks those places; every
  faction type has one of its own, and a new pilot's place is rolled three times as readily from them
  (`weighted_backdrops`, `creator.propose_cast`).
- **"it is fine to have some that express more of their human side ... I would expand the list vs. change that."** The
  lists grow and the painted busts stay: `HAIRS`, `HAIRS_LOW`, `TOUCHES`, `TOUCHES_FUR` and a bare human hand
  (`roll_more`, for new pilots only; `compose`'s defaults leave a cast pilot exactly as painted).

- The second animal is never only a tail or wings, and never a patch on a cheek: it owns the crown and the neck.
- The human is the clothes, the bearing and the gaze. No human hand beside human hair, and none on a pilot whose
  animal has a hand, a paw or a tentacle of its own.
- No two catalog pilots stand in the same place; a home ground goes to its own faction type first, and a place
  never swallows its pilot's accent (`BACKDROPS`' avoid column). `creator.py --self-test` holds this for `CAST`,
  with the outfit, pose and mood on their lists and every look composing.
- **The brief goes to an outside painter**: it describes a picture and never names the game, the studio, the
  world, or a place, a crew or a faction in it, and it carries the pilot's own rolled name, never a player's. The
  self-test holds this: it reads the world's names from the game (`bust.world_names`, from
  `Docs/Voyages/voyage_slice.json` and `FactionCatalog.cs`) and refuses them, as names, in every table line.
- No protected emblem, no real-world symbol and no lettering: an outfit describes colours and cut ("a plain white
  rescue tabard with one broad red band", never "a medic's tabard", which was painted with a red cross; "a strip of
  bright plain cloth knotted around one sleeve", never "an armband", which was painted with a disc and a star), and a
  place has lights and plain shapes (a crowd "holding lanterns high", never "raised fists", which came back as
  fist emblems on banners; "murals of waves", never "open hands", which came back as a hand on a banner). A
  callsign's mark is a small object (a brooch, a pendant), never a sign on cloth; nothing is tied on an arm, and a
  band of cloth on a sleeve says plain. A sash is "a broad plain satin sash", never "a champion's ribbon sash",
  which was painted with a laurel wreath; and a hall has curtains, never banners: "three plain hanging banners"
  came back with a winged sword, a sun and a star on them. The self-test holds the words (`bust.emblem_word`),
  and the emblem sweep (section 4.5) reads the finished pictures.
- The domain colour comes from the faction (Ruby freedom, Gold commerce, Jade order), never from an element.
- The 32 painted full-body references in `Assets/_Graphics/Pilots/` are never overwritten; they are the check for
  what a legacy pilot is.
- A bust is painted with its own background (no matte); the avatar is the top-centre square of the frame: 4% off
  each side, 8% off the bottom and the top kept (`creator.avatar_cut`).
- `CAST` is frozen: change a line on purpose and repaint that pilot. `stale` is a list to look at, not a gate: a
  bust painted before a later table fix can still be right.
- Per-player busts painted on recruit are not built yet: that needs a backend painter and is Garrett's call,
  Shombith's for anything on the backend (PORTRAITS.md section 6, decision 2).

## 6. The scene: the same pilot in a realistic picture (THE STYLE IS NOT LOCKED)

Garrett, 2026-11-11: "now lets see how realistic we can get when we take the same character from the avatar picture
and make the art needed for the ark. and elsewhere. make a full scene piece of art that show them in a hyper
realistic setting ... it shouldn't look like a pristine commercial set, it should show them raw in their environment
doing something that is appropriate to their background, history, hobbies, profession, etc. ... do 4 of these and
make stretch yourself in different directions so we can give feedback on your different approaches and lock in this
second style". Four samples exist, one per approach (`scene.APPROACHES`: `field` a long-lens documentary, `film` a
cinematic still, `candid` a crewmate's snapshot, `sitting` a portrait in the minute after the work). **Until he
picks, a scene for any other pilot is a draft, and nothing here is imported by the game.** PORTRAITS.md section 8
has the samples, the reads and what is open.

**How a scene keeps the character** (the painter takes words only):

1. The person's lines come word for word from the brief the avatar was painted from (`identity`), never from
   today's tables.
2. LOOK at the avatar and write down what its painter decided beyond the brief (`seen`) and where the painting
   departs from it (`swap`: the scene follows the painting, and a correction replaces the phrase, never adds to it).
3. Below the avatar's crop the body is rules, the same for every picture of that pilot: `BUILD`, `hands_of` (the
   hand the avatar shows, else the animal's own true hand, else gloves), `CARRY`, `LOWER`. No bare human skin.
4. Nothing permanent is added: wear is mud, soot, a tear, a mended seam (`PERMANENT` refuses a scar or a chipped
   tusk).
5. What the pilot is doing names its source: their profession (the faction type), a hobby (the game's `Quirks`) or
   their history (`Upbringings`).

**The round for one scene:**

1. Write the record in `scene.SAMPLES` (approach, source, `seen`, `swap`, doing, place, light, wear) with the
   avatar open beside you. `brief <WORD>` and read it: is every worn thing placed (lesson 66)? Is the place emptied
   of people by name (67)? Is the wear told where to stop (65)? Is the place strange through distance and weather,
   with an ordinary camera (68)? `--self-test`.
2. Paint the brief verbatim on `gpt-image-2.5-sunburst`: 1536x1024 (`field`), 2048x1152 (`film`), 1024x1280
   (`candid`, `sitting`). Say the count first; a sample took three to seven paintings.
3. Look at it whole, then at the head, the hands and the background at full size, beside the avatar. `capture`
   the one you keep (it writes the brief it was painted from), set its `CLOSE` box, and `sheet`.
4. **The read** for anything that will be shown:

   ```
   python3 Tools/Build/pilot_art/scene.py read-prep <scratch>/readN       # a NEW folder outside the repository
   Workflow({scriptPath: ".claude/skills/pilot-creator/references/scene_read.js",
             args: {dir: "<scratch>/readN", letters: ["A", "B", "C", "D"]}})
   # save what it returns as <scratch>/readN/result.json
   python3 Tools/Build/pilot_art/scene.py read-grade <scratch>/readN
   ```

   Two readers (say so before running it): one finds each scene's pilot among the 50 avatars, told nothing; one
   puts each scene beside its avatar and lists what differs. About 220k tokens and two minutes for four scenes.
   The key lives beside the folder, never in it. Fix what "a player would notice" in the record, repaint once,
   and read the new picture again: fixing one thing can move another.
5. Commit the scene, its brief, the record and any lesson together. No C# changes, so no reference compile; say so.

**What a text brief does not hold yet** (write it down, do not chase it): the eye ring at any distance, an ear on
a horned animal, small costume details (an epaulette's make, the count of a necklace's pieces). A model that takes
the avatar as a reference image is the next thing to try, and every run of one needs Garrett's approval of its cost
(PAINTERS.md section 2).

