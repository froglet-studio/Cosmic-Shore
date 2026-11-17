# The signature busts (2026-11-10/11): where every catalog pilot's identity comes from

The busts were the skill's first product: a head-and-shoulders painting of each of the 50 avatar pilots, in an
airbrushed finish, each in a place of its own. Garrett replaced them as the avatar on 2026-11-11 ("the scene close
ups are categorically better than the previous avatar pictures and should be used in their place"), and they stay
in the repository for three reasons:

- **The brief a bust was painted from is the pilot's identity** (`Docs/Pilots/art/busts/briefs/<WORD>.txt`). Every
  later picture of that pilot starts from those words (`scene.identity`), so never edit one by hand.
- **`bust.py`'s tables still compose a NEW pilot's identity lines**: the face, the crown and the coat, the second
  piece, the eyes, the hair, the outfit. A table fix reaches every later pilot with that animal.
- **A bust can be shown to the painter for the one part it got right** (a patch's `bust=`, `refs.py`): strangers
  named the second animal in 44 and 47 of the 50 busts, more than in the scenes' close-ups (SKILL.md section 0).
  Send only the detail that holds the part (`bust_box=`, `refs.py detail <WORD>`): the rest of a bust shows what
  the patch takes away (WINGMAN's plumes, NEEDLE's row of points), and a reference is copied before the words.

Garrett's notes that shaped them are in SKILL.md's opening and in section 5 here; the measurements are in
`Docs/Pilots/PORTRAITS.md` section 7.

## 1. What a bust gives each heritage (bust.py's tables, never a hand prompt)

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

**What every bust shares:** the framing (the head in the upper middle, clear space above everything that grows from
it, cropped at mid-chest, looking at the viewer), the premium painterly finish, the faction's DOMAIN colour (Ruby
#C800C3, Gold #FFA700, Jade #0EC0B6) as the clothes' trim, the rim light and a thin glowing ring around each eye (the
pilot's sight), and a few crystal motes in the air.
**What differs:** the place (`BACKDROPS`: 73 places, each with its light, its value and the faction types whose home
ground it is; 37 of them could not be on Earth, `UNEARTHLY`, and a new pilot rolls those three times as readily), the
outfit, the pose (`POSES`) and the expression (`EXPRESSIONS`). The 50 catalog pilots stand in 50 different places;
what each was painted with is frozen in `creator.CAST`. A new pilot may also show more of the human side
(`roll_more`: 21 hair styles, a human touch such as spectacles, kohl, a scar or a beard, a bare hand).

## 2. Commands

```
python3 Tools/Build/pilot_art/bust.py --callsign TALON --heritage Mole Newt --faction Pirate \
    --name "Lulolo Dawnwhistle"                                # the bust brief of any pilot, from the tables
python3 Tools/Build/pilot_art/bust.py --backdrops              # the places
python3 Tools/Build/pilot_art/creator.py brief <WORD>          # the bust brief of a catalog pilot
python3 Tools/Build/pilot_art/creator.py list                  # the catalog, each pilot's place, which have a bust
python3 Tools/Build/pilot_art/creator.py cast                  # CAST lines for catalog pilots that have none
python3 Tools/Build/pilot_art/creator.py capture <WORD> <url>  # save a painting and the brief it was painted from
python3 Tools/Build/pilot_art/creator.py stale                 # busts today's tables would paint differently
python3 Tools/Build/pilot_art/creator.py sheet out.png [WORD...]   # bust + octagon avatar sizes, for looking
python3 Tools/Build/pilot_art/creator.py tiles <scratch dir> [WORD...]   # whole + four enlarged quarters (4)
```

## 3. The round for a bust

1. **The brief.** `creator.py brief <WORD>` composes it from the pilot's `CAST` line. Read it before spending a
   painting, and ask: does the crown swallow the face (a hood of head feathers makes the head that bird)? Does the
   face line say the animal's tell (the oryx's long horns, the snail's stalk eyes)? If the two animals share a
   texture, does the second have a tell the first cannot have? Is there one pair of ears, and does the brief say
   whose? Would any word summon an emblem or lettering (a medic's tabard, an armband, a crowd's raised fists, a
   banner), or would two phrases do it together (a bandana on an arm beside a crosshair-marked eyepiece came back
   as an armband with a ring and a cross on it)? A wrong or weak animal is fixed in `bust.py`'s TABLES, never in
   the prompt by hand: every pilot with that animal inherits the fix. Then `--self-test`.
2. **Paint**: Figma `generate_image`, model `gpt-image-2.5-sunburst`, 1024x1024, the brief verbatim, into the file
   "Cosmic Shore Pilot Art" (key `BPjqBNIQgtY3L6RvghdWcg`). Each call spends Figma AI credits: say the count up
   front. Paint in batches of five and capture each batch before the next.
3. **Capture** with `creator.py capture <WORD> <image.url>` (curl; `www.figma.com` is on the Default environment's
   allow-list since 2026-11-10). Never `get_screenshot`: the Collab seat's read limit is about 30 calls a day.
   Capture also writes `Docs/Pilots/art/busts/briefs/<WORD>.txt`: capture BEFORE the next table edit.
4. **Look** at the 256 px thumbnail the tool returns, then at the sheet. Obvious misses (the second animal absent,
   a stray emblem, a wrong species) are fixed in the table and repainted now. Then zoom: crop every hand, paw and
   claw at full size and count (a badger's paw came back with eight claws, and two blind readers counted six
   fingers on a glove that has five), and look over the clothes and the place for an emblem nobody asked for.
5. **The blind read** (SKILL.md section 7) for any round of more than a handful, and after a table change that many
   pilots inherit. A pilot whose animal is missed gets its table line fixed and one repaint.

## 4. The emblem sweep

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

A scene is swept for lettering and symbols by the scene sweep (SKILL.md section 7), which asks the same question.

## 5. The table rules of round 3 (Garrett, 2026-11-11)

Each is a table rule that `bust.py --self-test` holds, so every new pilot inherits it:

- **"please never make crab hands (how would they interact with things?)"** A limb in the frame is a hand with
  fingers or a tentacle (`LIMB`, `LIMB_TELL`; the gate is `NOT_A_HAND` and `A_HAND`). The armoured animals
  (`ARMOURED`: lobster, scorpion, mantis, mantis shrimp) wear their shell on a hand with four fingers and a thumb,
  like a gauntlet, and a pilot who also has a true hand (a tree frog with a scorpion's tail) shows the true hand.
  Give the hand something to hold: Comet's lobster-shell hand holds a crystal shard.
- **"butterfly Wings on a head, as ears, is nonsensical."** Wings grow from the back between the shoulder blades
  (`SECOND`), and no `CROWN` line names wings or tail feathers.
- **"the turkey tail feathers are in a very weird spot with respect to the dog's hood."** A tail fan grows from the
  base of the back: its line says "behind the back", never "behind the head", and under a hooded outfit the brief
  says the hood lies in front of it (`BEHIND_HOOD`).
- **"the chameleon transitioning to jellyfish looks a bit abrupt."** A translucent coat has a `BLEND` line: the
  face's skin thins and turns glassy by degrees, with no rim where the bell begins.
- **"The backgrounds that don't look like they could be earth are the best."** `UNEARTHLY` marks those places; every
  faction type has one of its own, and a new pilot's place is rolled three times as readily from them
  (`weighted_backdrops`, `creator.propose_cast`).
- **"it is fine to have some that express more of their human side ... I would expand the list vs. change that."**
  The lists grow and the painted busts stay: `HAIRS`, `HAIRS_LOW`, `TOUCHES`, `TOUCHES_FUR` and a bare human hand
  (`roll_more`, for new pilots only; `compose`'s defaults leave a cast pilot exactly as painted).

And the rules a bust keeps beside them:

- The second animal is never only a tail or wings, and never a patch on a cheek: it owns the crown and the neck.
- The human is the clothes, the bearing and the gaze. No human hand beside human hair, and none on a pilot whose
  animal has a hand, a paw or a tentacle of its own.
- No two catalog pilots stand in the same place; a home ground goes to its own faction type first, and a place
  never swallows its pilot's accent (`BACKDROPS`' avoid column). `creator.py --self-test` holds this for `CAST`,
  with the outfit, pose and mood on their lists and every look composing.
- A bust is painted with its own background (no matte). While a pilot has no painted scene, their avatar is the
  bust's top-centre square: 4% off each side, 8% off the bottom and the top kept (`creator.avatar_cut`).
- `CAST` is frozen: change a line on purpose and repaint that pilot. `stale` is a list to look at, not a gate: a
  bust painted before a later table fix can still be right.
