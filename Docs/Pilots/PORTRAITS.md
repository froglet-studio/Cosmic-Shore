# Pilot portraits: the plan, the audit and the rounds

**Status: round 1 built 2026-11-07 on `cece/sweet-goodall-duyp56`** (Garrett: "i tried to get you to make pilot
artwork but that came out terrible. you have illustrations of pilots for reference. perhaps you could try to build
up a skill that could make art like the ones we have but even better."). The skill is **`/pilot-art`**
(`.claude/skills/pilot-art/`), the kit `Tools/Build/pilot_art/`. **Round 2 (same day, Garrett: "continue") gave the
other fourteen callsigns their portraits, so all eighteen show one in the Hangar.** Not yet opened in a Unity
editor: the wiring is a Sprite guid in each pilot asset and the Hangar card's Image has `preserveAspect`, so the
hand test is "open Menu_Main > Hangar > PILOTS and see eighteen portraits" (`Docs/UNITY_VERIFICATION_CHECKLIST.md`).

## 1. What a portrait is

A pilot is a unique person on a callsign ([`INVENTORY.md`](INVENTORY.md) section 3: a name, human plus two
animals, a faction with a domain, a backstory) and the callsign is a pair of eyes ([`CALLSIGNS.md`](CALLSIGNS.md)).
`PilotSO.portrait` (one per callsign today) shows in the Hangar's pilot card at 44x56, on the Squads page and in
the codex at up to 220x280. The portrait is a full-body painting of the PERSON: the first animal's head and
build, ONE feature lent by the second, the faction's gear in the faction's domain colour (Ruby freedom, Gold
commerce, Jade order), the callsign's mark worn on the body, framed and lit like the painted set.

## 2. Art on hand: the painted set (measured 2026-11-07)

32 portraits in `Assets/_Graphics/Pilots/` (24 named `Portrait_<Name>_Placeholder.png` plus 8 spares
`PilotPortrait_*`), 220x280, full-body anthropomorphic animal pilots, painterly, transparent. They were the
40-pilot roster's art (PILOTS_AND_ARTIFACTS.md, thrown out 2026-10-27) and have been unreferenced by any pilot
since; the legacy `SO_Captain` assets, Menu_Main, a dialogue and the FTUE speaker still reference some guids.
**They stay untouched**: they are the REFERENCE SET the kit measures its envelope from
(`Docs/Pilots/art/style_envelope.json`; the audit sheet with every one's gate verdicts:
`Docs/Pilots/art/sheets/references.png`). Thirty pass the kit's gates; Dozer (crouching) and Echo (winged) fail
`height` and `top`, which is right.

What the set does (`.claude/skills/pilot-art/references/STYLE.md`): head at the top edge, boots on the bottom
edge, the body a hair left of centre, dark (median luminance 0.23) with a real highlight, one saturated accent,
soft fur edges, no backdrop.

## 3. The kit (`Tools/Build/pilot_art/`)

| Piece | What |
|---|---|
| `brief.py` | the art direction composed from the game's data: 48 `AnimalSpecies` lines (head / lent feature / colour), 18 callsign marks, 16 faction gear lines with the domain accent hexes copied from `colour_domains.json` (held to it by `--self-test`), the house style, the key colour choice; `--self-test` holds the tables to the enums |
| `specs/<name>.py` | one portrait: the person, callsign, heritage, faction, pose, source, key, exemptions, the pilot it is wired to |
| `matte.py` | the key-colour matte: key estimated from the border ring, the difference key in the edge band, decontamination, specks and pinholes cleaned |
| `finish.py` | the 220x280 sprite framed to the set's stage by measurement (top 0.010, bottom 0.995, anchor 0.49, width <= 0.96), premultiplied Lanczos, edge colour extended, a light unsharp; changes no colour or value |
| `measure.py` | the metrics and the envelope of the painted set |
| `verify.py` | 14 gates calibrated on the set, with planted-defect negative controls and the two reference outliers asserted |
| `sheet.py` | BEFORE at HEAD, AFTER with verdicts, 110x140, the Hangar card at 44x56, the three nearest painted references; `--refs` is the audit |
| `author.py` | spec -> matte -> finish -> verify -> sheet -> write PNG + meta (guid `md5("CosmicShore/PilotArt/<Name>")`) -> `wired.json` -> `author_pilot_assets.py`; `--check` re-finishes every source and is a `/ship` gate |
| `Docs/Pilots/art/sources/` | the captured paintings (1024x1280 over the key colour), the truth `--check` re-finishes from |

The painter is outside the kit (`references/PAINTERS.md`): round 1 used the Figma connector's `generate_image`
(gpt-image-2.5-flare) into the Figma file "Cosmic Shore Pilot Art" (Froglet Studio team), six paintings for four
portraits (Rosh was repainted twice for light), captured at full size through `get_screenshot` because the cloud
environment cannot download from `www.figma.com`.

## 4. Round 1 (2026-11-07): four callsigns get their first person

| Callsign | Person | Heritage | Faction (domain) | File | Gates |
|---|---|---|---|---|---|
| TALON | Wyvendo Waveline (INVENTORY.md's own example) | Human / Lynx / Jackal | Hollowfen Trackers (Hunter, Ruby) | `Portrait_Wyvendo.png` | pass |
| VOLT | Karenth Amberweaver | Human / Axolotl / Koi | Brass Lantern Company (Merchant, Gold) | `Portrait_Karenth.png` | pass |
| SUNDIAL | Delmira Stillwater | Human / Owl / Pangolin | House Verdance (Aristocrat, Jade) | `Portrait_Delmira.png` | pass |
| BANDIT | Rosh Rusthook | Human / Octopus / Raven | Black Tide (Pirate, Ruby) | `Portrait_Rosh.png` | pass (v3; v1 and v2 failed `value`: a dark pilot needs one light garment) |

The sheet: `Docs/Pilots/art/sheets/round.png`. Names are rolled from `PilotGenerator`'s own syllable and family
tables so they read like every other pilot's. What the round taught: `.claude/skills/pilot-art/references/LESSONS.md`.

## 4.5 Round 2 (2026-11-07): the other fourteen callsigns, cast by a panel

Garrett: "continue." Before any painting, a workflow (a) reviewed the kit through four lenses with three refuters
per finding, (b) checked all 48 species lines (33 corrections kept, LESSONS 11), and (c) had three designers cast
the fourteen and two judges score them; the final cast below is the synthesis of the judges' grafts and rejects
(LESSONS 13). New pilots are minted only into the four recruitable faction types, so the fourteen are 4 merchants,
3 hunters, 4 aristocrats and 3 pirates, each animal used once and none of round 1's.

| Callsign | Person | Heritage | Faction (domain) | File | Why this pair |
|---|---|---|---|---|---|
| ENSIGN | Veliane Crestward | Human / Crane / Lemur | Wardens of the Calm (Aristocrat, Jade) | `Portrait_Veliane.png` | a white head reads first; the ringed tail hangs like the pennant |
| MANGER | Tamako Deephook | Human / Ibis / Platypus | Order of the Last Lure (Hunter, Ruby) | `Portrait_Tamako.png` | a wader of the feeding flats; the paddle tail is a shape |
| ANVIL | Brikon Copperwright | Human / Yak / Beetle | Tidewright Exchange (Merchant, Gold) | `Portrait_Brikon.png` | the hauler; wing cases are one shape on the back |
| ZODIAC | Ivelia Starreach | Human / Ocelot / Heron | Saltmark Consortium (Merchant, Gold) | `Portrait_Ivelia.png` | rosettes as a star field under a slate cape |
| OPAL | Quivera Pearlspark | Human / Chameleon / Jellyfish | House of Seven Scales (Merchant, Gold) | `Portrait_Quivera.png` | two colour-changers for Omni; painted over magenta |
| RAMPART | Obelo Stormstone | Human / Badger / Armadillo | Quayside Mutual (Merchant, Gold) | `Portrait_Obelo.png` | a striped face under a wall of plates |
| CITADEL | Nedira Shellward | Human / Hedgehog / Tortoise | House Albrine (Aristocrat, Jade) | `Portrait_Nedira.png` | defence on defence |
| HORNET | Zeko Mirethorn | Human / Mantis / Newt | Deepreed Stalkers (Hunter, Ruby) | `Portrait_Zeko.png` | an insect head says danger; painted over blue |
| IVY | Mathalo Kelpbloom | Human / Orangutan / Sloth | House Verdance (Aristocrat, Jade) | `Portrait_Mathalo.png` | the canopy animals; painted over blue for the leaves |
| DOE | Fenlia Moonbell | Human / Moose / Fennec | Longwake Lodge (Hunter, Ruby) | `Portrait_Fenlia.png` | a moose says herbivore before the name does |
| WINGMAN | Jorin Gullwing | Human / Puffin / Pelican | Gullbone Crew (Pirate, Ruby) | `Portrait_Jorin.png` | white wings, so not a second black-winged pirate |
| NEEDLE | Ilia Silvergate | Human / Narwhal / Seahorse | The Jade Accord (Aristocrat, Jade) | `Portrait_Ilia.png` | the tusk is the needle, the tail curls into the ring |
| GALLEON | Haradek Anchorkeel | Human / Walrus / Bison | The Unmoored (Pirate, Ruby) | `Portrait_Haradek.png` | the walrus captain in an ivory coat |
| FLUME | Ekora Hollowbrook | Human / Otter / Eel | Saltjaw Brethren (Pirate, Ruby) | `Portrait_Ekora.png` | an otter's face a player recruits, an eel's ribbon tail |

Fourteen paintings, fourteen first-pass gate passes (LESSONS 16). The sheet: `Docs/Pilots/art/sheets/round2.png`.
Every portrait then took the stranger test (`/pilot-art` section 3.5: blind readers at 44x56 and at full size, a grader
against the spec); its results are in LESSONS, round 2.

## 5. What comes next, in order

1. **Capture the five repaints** (Mathalo, Fenlia, Ilia, Haradek, Ekora; Figma nodes 6:2-6:6 in "Cosmic Shore Pilot Art")
   once the Figma tool-call limit resets (LESSONS 18), then `author.py <spec>` each and rerun the stranger test.
   They were painted from the corrected species lines after the round-2 stranger test (8 pass, 6 weak, LESSONS 17).
2. **Garrett looks at `round.png` and `round2.png`** and says what is wrong: the animals, the gear, the colours,
   the light, the framing. Each answer goes into `brief.py`'s tables or `STYLE.md`, then the portrait is repainted.
3. **Per-person portraits** (section 6), once the decision is made.
4. **The hand test** in the editor: the Hangar PILOTS column with eighteen portraits at 44x56, the Squads page cards.

## 6. Decisions (defaults used; change any)

1. **A callsign's portrait is its first painted person** (Wyvendo is TALON's face for every crew's TALON) until a
   per-person portrait exists. The Hangar already pairs "the person's name and the callsign's portrait", so
   nothing in the UI changes. Alternatives: an archetype per callsign (no person), or per-person art.
2. **Per-person art for unlimited unique pilots** needs a place a PNG per person cannot be: a cloud-rendered
   portrait on recruit (the record's seed names the heritage and faction, so the brief is deterministic), a
   procedural painter (PAINTERS.md section 4), or a heritage "card" (silhouette + colours) drawn at runtime. Open;
   Garrett's call, Shombith's for anything on the backend.
3. **No firearms in the brief.** The painted set has a rifle and a bow; the kit's `NEVER` line bans firearms and
   allows the callsign's instrument. Open for Garrett.
4. **The painter spends Figma AI credits** on the Froglet Studio team's plan; a round says its count up front.
   An artist's painting goes through the same kit (`key="alpha"`).
5. **Shombith's 2026-10-26 "Dont create art we will wait for an artist"** was about the 40-pilot roster's eight
   missing portraits; that roster is gone and this is Garrett's ask for the callsign pilots. The painted set is
   never overwritten; an artist's portrait of any of these pilots replaces the model's through a spec change.

## 7. The signature busts (2026-11-10/11): every avatar shows all three heritages

Garrett, 2026-11-10: "can you get us pilot images that really show both clades in the avatar bust ... when people see
it they know it is cosmic shore because only we have triple hybrid people". The avatars had been cut from the
full-body portraits, where the second animal lends a tail or wings, so the crop showed one animal. The fix is a new
picture made for the crop: a **signature bust** (`Tools/Build/pilot_art/bust.py`).

**Round 1 (2026-11-10)** gave the first animal the face, the second the crown and a mark across the nape and one
cheek, and the human the eyes, the hair and one hand, on one navy plate with a domain-colour halo. Six samples got
"these are definitely improvements"; then all 50 avatar pilots were painted.

**Round 2 (2026-11-11) repainted all 50.** Garrett: "a good step forward, but the backgrounds need much
more variance. they don't all need a hand ... any creature that already has a hand or tentacle like a frog,
primate, or octopus it doesn't need a hand. the human side is the least important to express when it is wearing
clothes". All 50 were repainted:

- the **first animal** owns the face and skull, and its own eyes where the eye is its tell;
- the **second animal** owns the crown and everything behind the face (the back of the head, the nape, the throat
  and the neck), plus one more piece of its body in the frame or the limb people know it by;
- the **human** is the bearing, the clothes (three outfits per faction type, open at the throat) and the gaze. Of
  the 50, 10 had human hair, 12 showed their animal's own limb, 3 a gloved hand and 35 no hand at all;
- every pilot stands in a **different place** (52 in `BACKDROPS` then; 20 dark, 16 mid and 14 light among the 50),
  and the set wears 37 of the 48 outfits in 8 poses and 14 moods. What makes them one set: the framing, the
  painterly finish, the faction's domain colour as trim, the rim light, the thin glowing ring around each eye and
  the crystal motes.

**Round 3 (2026-11-11) is what the avatars show: six rules, eight repaints.** Garrett on round 2: "These are great.
only a few need changing." Each point became a rule in `bust.py`'s tables, held by its self-test, so every pilot the
skill makes from now on inherits it; only the eight busts a rule touched were repainted, each right on its first
painting.

| Garrett said | The rule now | Repainted |
|---|---|---|
| "please never make crab hands (how would they interact with things?)" | A hand has fingers or is a tentacle (`NOT_A_HAND`, `A_HAND`). The four armoured animals (lobster, scorpion, mantis, mantis shrimp) wear their shell on a hand with four fingers and a thumb, jointed like a gauntlet; a pilot who also has a true hand shows that one. | Comet, Damsel, Dozer, Slingshot, Reaper |
| "butterfly Wings on a head, as ears, is nonsensical" | Wings, tails and tail fans grow from the back and stand behind the clothes. No crown line names wings or tail feathers; the butterfly's crown is its antennae. | Maestro |
| "the turkey tail feathers are in a very weird spot with respect to the dog's hood" | A fan rises from below the frame behind both shoulders, and under a hooded outfit the brief says the hood lies in front of it (`BEHIND_HOOD`, `HOODED`). | Krush |
| "the chameleon transitioning to jellyfish looks a bit abrupt" | A translucent coat meets the face in a written gradient, not at the seam line (`BLEND`). | OPAL |
| "The backgrounds that don't look like they could be earth are the best" | 21 new places (one home ground per faction type, five for anyone): 73 in all, 37 marked `UNEARTHLY` and rolled three times as readily for a new pilot. | none: the 50 keep their places |
| "it is fine to have some that express more of their human side ... I would expand the list vs. change that" | For new pilots only: more hair styles, small human touches (spectacles, a scar, a moustache on a furred face) and a bare hand (`roll_more`). | none: the 50 keep their looks |

The cast after round 3: 10 with human hair, 10 showing their animal's own hand, 3 a gloved human hand, 37 no hand;
15 of the 50 stand in unearthly places. Kept as painted, with the table line fixed for the future (`creator.py
stale` lists them): IVY's sloth claws, Tomahawk's hawk fan, Spare01's peacock fan, and Astrofoil's and Trace's
jellyfish, beside the five kept from round 2 (MANGER, WINGMAN, Tenacious, Tender, Spare08). **The eight were read
blind** by three readers (two at full size, one on the icons): both animals were named in 8 of 8 at full size, and
in the 64 px icon by its reader in 6 of 8 (Reaper's mantis and Slingshot's mantis shrimp are not named at that
size). An emblem sweep of the eight (one reader) found no lettering; the devices it listed are the pilots' own pins
and a racing flag's chequers, and none was repainted.

**It is measured: the blind read** (`Tools/Build/pilot_art/blind.py`, `/pilot-creator` section 4). The busts are
shuffled and unnamed, read by seven readers who are told nothing, and graded by script against the cast. A name
counts as the animal when it is the animal or a name everyday speech does not tell apart from it (crow for a
raven, rabbit for a hare), and as its kind when it is a relative people do tell apart, a look-alike or the
everyday word for its family (a fox for a jackal, a stork for an egret, a lizard for a gecko). "Some bird" or "a
fish" names no kind and counts for nothing. One read is one sample, so each round was read twice by different
readers, and both rounds are graded by the same script.

| What the readers named | Round 1, two reads | Round 2, two reads |
|---|---|---|
| Both animals, full size (sure or likely) | 26 and 27 of 50 | 44 and 47 of 50 |
| Both animals' kind, full size (any confidence) | 42 and 42 | 48 and 50 |
| The second animal, full size (sure or likely) | 29 and 28 | 45 and 47 |
| Both animals in the 64 px icon, either reader | 10 and 12 | 23 and 29 |
| A human hand in the frame | 50 and 49 | 3 and 3 |
| Human hair | 50 and 50 | 11 and 9 (10 are cast) |

Round 2's two reads are of the same 50 pictures, each in its own shuffle. Two of those pictures were replaced
afterwards for emblems (Slingshot and Spare05, below). A check read of the two new ones, beside three unchanged
busts that read at full size as they had, named Spare05's hare and ram at both sizes. Slingshot's mantis shrimp,
"likely" in both reads of the old picture, was a guess behind a likely "lobster" in the new one, so the headline
row of the finished set may stand one lower than the table (its icon was a flamingo with peacock feathers before
and after). An earlier full read gave 45, 49, 46 and 25; four pictures were replaced after
it for flaws a reader does not grade: Longshift's paw had eight claws, and Damsel, BANDIT and Shrapnel carried
emblems nobody asked for (an armband with a ring and a cross, a hand on a banner). The grader was reviewed twice,
and each of its rules was then broken on purpose once: its self-test noticed all 244.

**The emblem sweep** (`/pilot-creator` section 4.5) is the other check: readers who are told nothing look at each
bust at full size and in enlarged tiles and list every drawn sign and what it resembles. Every one of the 50 was
read once. In the finished set the readers list 88 devices, 61 of them with something they resemble, and no legible
lettering; ten busts carry none. Nearly all are a callsign's own mark worn as a small object (ANVIL's anvil pin,
GALLEON's helm brooch, SUNDIAL's sundial brooch), a compass star, or scenery. The sweep found two that the eye had
passed, and both were repainted: Slingshot's sash carried a gold laurel wreath on black, which is a clothing
brand's mark, and Spare05's treaty hall hung a winged sword, a sun and a star on banners the brief had called
plain. A second reading found the new Slingshot clean, as it had the new BANDIT and Shrapnel. Spare05's new hall
has no banner in its brief and still hangs two small ones at the frame's edges, each with a gold tree: scenery,
and kept (LESSONS 44, 52).

- **The skill is `/pilot-creator`** (`.claude/skills/pilot-creator/SKILL.md`); the tool is
  `Tools/Build/pilot_art/creator.py`, which also rolls a player's unique pilot exactly as `PilotGenerator` mints it
  and rolls that pilot's look (place, outfit, pose, mood, hair, limb) from its name.
- **The busts**: `Docs/Pilots/art/busts/<WORD>.jpg`, 1024 square, 50 of them (18 callsign pilots, 32 legacy
  pilots cast as triple hybrids in `creator.LEGACY`, names for the eight spares in `LEGACY_NAMES`). What each was
  painted with is frozen in `creator.CAST`, and the brief it was painted from is `busts/briefs/<WORD>.txt`.
- **The avatars**: `author_pilot_avatar_assets.py` cuts each avatar from its bust at 256 px (was 128 from the
  portrait); ids and guids unchanged, so saved profiles keep their face.
- **Painter**: Figma `generate_image`, `gpt-image-2.5-sunburst`, captured by curl (`www.figma.com` was added to the
  Default environment's allow-list, Garrett 2026-11-10). Round 1 was about 60 paintings (six samples, 50, two
  repaints); round 2 was 70 (50, and 20 repaints of 16 pilots); round 3 was 8.
- **What still misses** (LESSONS 43 and 50): ZODIAC's heron is a secretary bird or a guess after three paintings
  (a heron without its bill); TALON's jackal is a fox in every read and Scales's egret a stork in most; Tomahawk's
  hawk is a turkey to two readers of three (a fan of banded tail feathers; a hawk's other tells are its bill and
  its talons, so talons in the frame are the fix to try) and Shrapnel's newt a lizard to one of two. Slingshot's
  mantis shrimp, a lobster first in round 2 (its clubs came back with a pincer's hook), is named at full size
  since its round-3 repaint without clubs, and not in the icon.
  BANDIT's octopus face reads as a mind flayer and MANGER's ibis wears
  a second bill. At 64 px about half the set shows one animal: a coat on the neck does not survive that size, and
  no icon reader named the second animal of FLUME, Longshift, MANGER, Shrapnel, Strider, WINGMAN or Tender, whose
  narwhal reads at full size and is only a parrot in the icon (LESSONS 48).
- **Not yet**: per-player busts painted on recruit (decision 2 in section 6), and the hand test of the 256 px
  avatars in the profile window and avatar picker (`Docs/UNITY_VERIFICATION_CHECKLIST.md`, "Pilot avatars are
  cut from the signature busts at 256 px").

## 8. The scenes (2026-11-11): the same pilot, photographed in their world. THE STYLE IS NOT LOCKED

Garrett, after round 2 of the busts: "These also look like airbrushed glamour shots, which is fine for avatar
pictures. now lets see how realistic we can get when we take the same character from the avatar picture and make
the art needed for the ark. and elsewhere. make a full scene piece of art that show them in a hyper realistic
setting. they still have their scratches, imperfections, a tear in their clothing, some dirt on their knees, etc.
... it should show them raw in their environment doing something that is appropriate to their background, history,
hobbies, profession, etc. We want to get this second part right so do 4 of these and make stretch yourself in
different directions so we can give feedback on your different approaches and lock in this second style so for
each new character the skill is equipped to make all the needed assets and keep the character consistent across
them."

So this section is four samples and a mechanism, waiting for his choice. The kit is
`Tools/Build/pilot_art/scene.py`; the pictures are `Docs/Pilots/art/scenes/<WORD>-<approach>.jpg`, each with the
brief it was painted from in `scenes/briefs/`. Nothing here is imported by the game yet.

**The four approaches** (`scene.APPROACHES`), each on a different pilot, and what each pilot is doing comes from
who they are:

| Approach | The frame | Pilot | From their | Doing |
|---|---|---|---|---|
| Field documentary (`field`) | 3:2, a long lens from far off, natural light, the whole figure at work, unaware of the camera | Tamako Deephook (MANGER), ibis and platypus | profession (a hunter) | kneeling in the mud of a feeding flat inside a living cell, setting a lure line |
| Cinematic film still (`film`) | 16:9, anamorphic, a lamp and the cold light of the bay, from the knees up, a third of the way in | Haradek Anchorkeel (GALLEON), walrus and bison | hobby: "ties a ribbon on the hull for every friend lost" (the game's `Quirks`) | tying a strip of red cloth beside a dozen older ones under a craft's nose on a hangar deck |
| Candid, available light (`candid`) | 4:5, a crewmate's snapshot in a small cabin, one desk lamp, grain, off level | Echo, mouse and owl | hobby: "keeps a jar of every crystal they have ever missed" (`Quirks`) | on the bunk in socks, holding one crystal up to the lamp in tweezers, the jar between the knees |
| Aftermath portrait (`sitting`) | 4:5, medium format, two steps away, looking into the lens in the first quiet minute after the work | Tenacious, tiger and tortoise | profession (a first responder) | sitting on a gear case on a rescue deck after a storm, soaked and sooty, a steel mug in both hands |

**How a scene keeps the character.** The painter takes words only, never the avatar itself, so the words do it:

1. *The same words.* The person's description is cut from the very brief their avatar was painted from
   (`busts/briefs/<WORD>.txt`), not from today's tables.
2. *What the avatar shows beyond its brief (`seen`), and against it (`swap`).* Whoever writes a scene looks at
   the avatar and writes down what the painter decided (the brooch is brass, the shell cap hides the ears); and
   where the painting departs from its brief, the scene's words follow the painting. A correction replaces the
   phrase and never sits beside it: two colour words for one eye let the painter choose.
3. *The rest of the body is rules, not rolls* (`BUILD`, `hands_of`, `CARRY`, `LOWER`, `GLOVES`): the build, the
   hands, where the second animal's piece grows from and the lower half of the clothes are decided once, the
   same for every picture of that pilot. The clothes cover the body to the wrists and ankles, so no bare human
   skin appears between an animal's head and an animal's hands.
4. *Nothing permanent is added* (`PERMANENT`). Wear is what a day gives and a wash or a needle takes away: mud,
   soot, a tear, a mended seam. Never a scar or a chipped tusk the avatar does not have.

**It is measured: the read** (`scene.py read-prep` and `read-grade`, the readers in
`.claude/skills/pilot-creator/references/scene_read.js`). One reader, told nothing, finds each scene's pilot
among the 50 avatars, which are numbered in a shuffled order. Another puts each scene beside its own avatar and
lists what matches, what differs and how much it matters. Each read was of the pictures as they stood; what a
read found was fixed in the record and repainted, and the new picture was read again.

| | The first four (2 + 2 readers) | Three repainted (1 + 1) | The four samples as kept (1 + 1) |
|---|---|---|---|
| The scene's pilot found among the 50 avatars | 8 of 8, all "sure" | 4 of 4, all "sure" | 4 of 4, all "sure" |
| Side by side, the same character | yes 3, mostly 5, no 0 | yes 1, mostly 3 | yes 2, mostly 2 |
| Differences "a player would notice" | 3 | 4 | 1 |
| The eye's glowing ring seen | 0 of 8 | 1 of 4 | 1 of 4 |
| The trim colour present | 8 of 8 | 4 of 4 | 4 of 4 |
| Called a photograph or a film still | 16 of 16 | 6 of 8 | 8 of 8 |
| Something permanent added | none | none | none |
| Anyone else in the frame | far figures in the hangar, both readers | nobody | nobody |

What the reads changed, each now a line in the kit or a lesson (`LESSONS` 57 to 71):

- **The mouse had bare human-looking hands and wore its lens as a monocle.** An animal's bare-skinned hand is
  painted as a person's. `bust.LIMB["Mouse"]` is now a forepaw furred to the fingertips, and the scene says where
  the lens hangs.
- **The walrus had an ape's brow, then a person's long curls.** "Shaggy" wool was painted as a fur hat and "tight
  springy curls" as ringlets; the record now gives the face from the chin to the top, calls it bare skin, and
  describes the wool as a short dense fleece.
- **The ibis's trim was dull under the mud.** Wear is told where to stop: the piping shows bright above the mud
  line.
- **The tiger's eyes were green.** The avatar's brief asked for hazel and its painter gave amber-gold: `swap`.
  And the ring around the iris is asked for as "the rim of each iris, a thin line of colour where an animal's
  would be dark", which was painted in three rolls of three where "ringed by a thin glowing line" was painted
  in one of three. It still shows only in the close portrait.
- **Blurred figures stood in the far hangar.** A place is emptied by name ("The bay is empty of people", boats
  "empty and unlit"), and the brief ends "nobody else is anywhere in the frame, near or far".
- **A loud unearthly sky reads as a render.** Garrett's "backgrounds that don't look like they could be earth
  are the best" was first painted for the tiger as a violet spiral sky with crystals the size of houses, and
  both readers of that picture called it a 3D render. The quiet version (the far wall of a living cell, veined
  like a leaf, seen through rain: the same world as the ibis's flat) is called a photograph by both. The place
  can be unearthly through distance, weather and what the ground is made of; the camera stays ordinary.

**What still differs in the four as kept** (the last read's own words, shortened): Echo's two ear tufts are thin
single feathers where the avatar's are thick and bushy (the one difference "a player would notice"), its snout
is longer and its waistcoat charcoal, not teal; MANGER has a pale cheek patch the avatar lacks, its hands read as
muddy gloves and its bill shield as a worn cap; GALLEON's epaulette is a fringed board, not coiled rope, its
whiskers are shorter, and the painter gives the bison a small ear under one horn in four rolls of four; Tenacious
wears its teal as a collar lining, not as piping. Two jars on Echo's desk carry labels with marks too small to
read, though the brief asked for blank ones. The eye ring is lost at any distance. Both readers call the candid
picture a film still: its one warm lamp reads as staged.

**The painter**: `gpt-image-2.5-sunburst` through the same Figma file as the busts, a text brief of under 1,000
words (`WORD_CAP`), 19 paintings for the four samples (MANGER 3, GALLEON 5, Echo 4, Tenacious 7). It returns
1536x1024 for 3:2, 1536x864 for 16:9 and 1024x1280 for 4:5. `gemini-3.1-flash-image`, tried once on MANGER, merged
the platypus's bill shield into the beak, drew human hands and a clean cape, at 1264x848.

**Open, and Garrett's to decide:**

1. Which approach, or which mix, is the style; and which frame each place in the game takes (the Ark screen, the
   Hangar, the Squads page, the codex). Until then a scene for any pilot outside the four is a draft: the kit's
   tables cover what the samples need plus one line per faction type.
2. Likeness is words-only today. A model that takes the avatar as a reference image (PAINTERS.md section 2)
   should close most of the "mostly" rows; every run of one needs his approval of its cost.
3. A rolled pilot's story (`PilotGenerator`'s `Homeworlds`, `Upbringings`, `Quirks`, `Wants`) is not ported
   into `creator.roll` yet, so a new pilot's scene cannot yet draw on their own backstory.

