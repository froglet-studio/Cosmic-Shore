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
   nothing in the UI changes. Alternatives: an archetype per callsign (no person), or per-person art. The scenes
   follow the same rule (2026-11-12): a crew member's profile page shows their own words over their callsign's
   scene and says who is pictured (section 8, "The scenes in the game").
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

**Since 2026-11-11 the avatar is the close-up of the pilot's scene (section 8)**, by Garrett's decision on the
four scene samples ("drop what we had before"). The busts stay in the repository: a bust's brief is the frozen
source of its pilot's identity lines, `bust.py`'s tables compose a new pilot's lines, and a patch of a scene may
show the painter the pilot's bust for the one part it got right (`refs.py`, a patch's `bust=`, or only the detail
that holds it, `bust_box=`). The bust
procedure is `.claude/skills/pilot-creator/references/BUSTS.md`. What follows is the record of the three rounds.

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

**The emblem sweep** (`/pilot-creator` section 4.5; a bust's own, with enlarged tiles, `references/BUSTS.md`
section 4) is the other check: readers who are told nothing look at each
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
- **The avatars**: `author_pilot_avatar_assets.py` cut each avatar from its bust at 256 px (was 128 from the
  portrait); ids and guids unchanged, so saved profiles keep their face. Since 2026-11-11 it cuts the scene's
  close-up, and the bust's cut is the fallback for a pilot with no scene (section 8).
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
- **Not yet**: per-player pilots painted on recruit (decision 2 in section 6), and the hand test of the 256 px
  avatars in the profile window and avatar picker (`Docs/UNITY_VERIFICATION_CHECKLIST.md`, "Pilot avatars are
  the close-ups of the pilots' scenes at 256 px").

## 8. The scenes (2026-11-11/12): every pilot photographed in their world, and the close-up is the avatar

Garrett, after round 2 of the busts: "These also look like airbrushed glamour shots, which is fine for avatar
pictures. now lets see how realistic we can get when we take the same character from the avatar picture and make
the art needed for the ark. and elsewhere. make a full scene piece of art that show them in a hyper realistic
setting. they still have their scratches, imperfections, a tear in their clothing, some dirt on their knees, etc.
... it should show them raw in their environment doing something that is appropriate to their background, history,
hobbies, profession, etc. We want to get this second part right so do 4 of these and make stretch yourself in
different directions so we can give feedback on your different approaches and lock in this second style so for
each new character the skill is equipped to make all the needed assets and keep the character consistent across
them."

**His decision on the four samples (2026-11-11, 19:58 UTC):** "All four of these are knocked out of the park. This
is an excellent direction. drop what we had before. the scene close ups are categorically better than the previous
avatar pictures and should be used in their place. All four scenes are excellent choices and I like that each
choice suits the personality of the pilot. SO I would continue to extend your reach and treat each pilot as a new
bespoke exercise in that regard. As far as the sizing and aspect ratio, we want something like 2.- (cinematic film
still) because then we have a fullscreen piece of art we can crop etc. for different platforms and presentations.
the main use will be a full screen profile for each pilot accessible through the ark app shell menu."

**What exists now.** All 50 catalog pilots have one scene: `Docs/Pilots/art/scenes/<WORD>.jpg` (1536x864), the
brief it was painted from in `scenes/briefs/`, its record in `Tools/Build/pilot_art/scenes/<WORD>.py`, and
`scenes.json` (what a screen needs to place a scene: its size, the avatar's box, the side the pilot stands on, the
crops for other aspects). All 50 player avatars (`Assets/_Graphics/Profile/Pilots/Avatar_<WORD>.png`, ids and guids
unchanged) are cut from the scenes' close-ups. The busts of section 7 stay in the repository: a bust's brief is the
frozen source of its pilot's identity lines, and a bust can be shown to a painter for the one part it got right.
The full-screen pilot profile is not built, and the scenes are not imported by the game yet. Every scene, its
avatar and what strangers named in each are on one page (https://claude.ai/artifact/AWHauAfBgnFD7Nw2rJ3wk8); the
scenes painted again, with their earlier versions, are on another
(https://claude.ai/artifact/QZXJW89raWJqyYcP46tg9r). How a scene is made is `/pilot-creator`.

**Each pilot is a bespoke exercise.** No two records share a treatment, and what the pilot is doing names its
source: their profession (31 of the 50), a hobby from the game's `Quirks` (9), their history from its
`Upbringings` (4) or what they want most from its `Wants` (6). `python3 Tools/Build/pilot_art/scene.py list` prints
all 50. A few, to show the reach:

| Pilot | Treatment | From their |
|---|---|---|
| Tamako Deephook (MANGER), ibis and platypus | field documentary, a long lens from far off | profession: a hunter sets a lure line on a feeding flat |
| Ekora Hollowbrook (FLUME), otter and eel | split-level waterline shot, half above and half below | history: "grew up on a salvage barge" |
| Dozer, tree frog and scorpion | through a new pane from inside, the glazier dozing on the outside of the glass | profession: a contractor glazes a great canopy |
| Lunari Hollowreach (Spare02), opossum and lynx | one frame lit by a lightning bolt, the shutter left open in the dark | profession: a hunter carries the season's decoys home ahead of a storm |
| Krush, coyote and turkey | flash snapshot by whoever came down the cellar stairs | hobby: "cannot resist a dare" |
| Bellomo Stripewright (Spare03), zebra and yak | across a trading counter, thin hard light at altitude | want: "to pay back an old debt, with interest" |
| Tomahawk, basilisk and hawk | panned at 1/30 s from a skiff keeping pace | profession: a hunter runs across a lagoon's skin after a wake |
| Jugger, orca and eagle | 65mm epic film still, a bridge lit by its display | history: "grew up in the shadow of a dead Ark" |

**How a scene keeps the character.** The first painter takes words only, never the earlier picture, so the words
do it:

1. *The same words.* The person's description is cut from the very brief their bust was painted from
   (`busts/briefs/<WORD>.txt`), not from today's tables. A new pilot without a bust takes the lines today's tables
   compose, frozen at capture (`scenes/identity/<WORD>.txt`).
2. *What the earlier painting shows beyond its brief (`seen`), and against it (`swap`).* Whoever writes a scene
   looks at the pilot's picture and writes down what the painter decided (the brooch is brass, the shell cap hides
   the ears); and where the painting departs from its brief, the scene's words follow the painting. A correction
   replaces the phrase and never sits beside it: two colour words for one eye let the painter choose.
3. *The rest of the body is rules, not rolls* (`BUILD`, `hands_of`, `CARRY`, `LOWER`, `GLOVES`): the build, the
   hands, where the second animal's piece grows from and the lower half of the clothes are decided once, the
   same for every picture of that pilot. The clothes cover the body to the wrists and ankles, so no bare human
   skin appears between an animal's head and an animal's hands.
4. *Nothing permanent is added* (`PERMANENT`). Wear is what a day gives and a wash or a needle takes away: mud,
   soot, a tear, a mended seam. Never a scar or a chipped tusk the pilot does not have.

**One creature.** Garrett on Trace (2026-11-11, 23:01 UTC): "trace looks like a bad and lazy photoshop with a bird
head some hair and jellyfish stuff. she does not look like a thoughtfully conceived hybrid animal person. we can do
a lot better job of expressing the animals together." And on Longshift: he "should have a bushy skunk tail that
wraps around his body and into hit lap like a blanket to keep warm while he is waiting at his cold spaceport stop,
otherwise he is just a rabbit man with skunk coloring." A bust's brief hands each animal its own ground, and a
painter paints that as parts set side by side. So a record may carry a `fused` line, the pilot written as ONE
animal (`scene.fused_errors` holds its form), and the second animal's part is put to USE in the scene. Longshift
was recast as hare and skunk and wrapped in his own tail; a stranger then named both animals. Trace is a booby
whose skull ends in a jellyfish's bell; Flux got an orca's fin, skin and eye patch by an edit that kept his face.
The ten rules that came out of it, each with the painting that taught it, are `/pilot-creator` section 5 and
`LESSONS` 72 to 90.

**Displays and windows** (seven scenes, `chart=`; never more than a fifth of the cast). Garrett, 20:22 UTC: "Make
a few of them render the hypersea in their scene on either a panel or holographic display". On the first four
(21:34 UTC): "zodiac's feathers look too rendered. halven's hologram looks out of place in that scene and not
aligned to his perspective. the others look good, but these are all just view of the galactic scale hypersea. I
want to see some where the pilot's are on the bridge of the ark or in a vessel. and they are looking at a 3D
holographic display (not a 2d hologram embedded in 3d) that 3d rendering should look like gameplay footage. prisms,
trails, flora, fauna, gyroid flora, crystals, cells, etc." Three scenes show the sea the pilots sail as a chart
(ZODIAC in a chart of light, NEEDLE and Longshift on a panel) and four show live play in a hologram with depth
(`volume`): Jugger (a siege of the great ship, on its bridge), Scales (a whole cell, on a bridge), Flux (a race,
in a cockpit) and Trace (a cell's garden, in a survey cabin). Halven's tower lost its hologram and kept its scene ("I
like the choice you made to just remove the hologram from halven"). His standard for a window (23:01 UTC): "the
dual perspectives of jugger is a better standard for windows, which applies to zodiac as well": the window shows
the same sea at its true size, and a display that tells another story is the exception.

**The game's own sea, painted from the game's frames.** On Jugger (22:49 UTC): "both the hypersea outside and the
hypersea in the rendering are very different than the hypersea in our gameplay. is there a way to upload
screenshots or other content so we can keep consistency between the game and the art we are generating? it seems
like we will need something like this if we want the same character in more than one piece of art as well". Then
(23:03 UTC): "i connected weave. use amoebius etc. to make and send screenshots. you can also use froglet.games".
The kit's answer is `Tools/Build/pilot_art/refs.py`: a library of seven real pictures of the game
(`Docs/Pilots/art/refs/`, `refs.json` says where each came from), a pilot's likeness cut from their approved scene,
and a **patch**: a painter that takes pictures is given the finished scene and the frames and asked to change named
things, and `refs.py apply` takes from its answer only the record's region. Five runs on Weave (GPT Image 2.6, 11
credits each, each quoted first and approved by Garrett on a card) painted Jugger's windows and hologram and
Scales's display from the frames, Flux's orca and the course outside his canopy, and Trace's port and cylinder
(twice). The contract, the costs and the wall of the free plan are `PAINTERS.md` section 2.

**It is measured, and the scenes read worse than the busts they replaced.** Two reads, both by readers who are
told nothing (2026-11-12; one read is one sample):

| What strangers named | The 50 scenes | The 50 busts (section 7) |
|---|---|---|
| Both animals, from the avatar at 640 px (sure or likely) | 29 of 50 | 44 and 47 of 50 (two reads) |
| Both animals, from the avatar, counting guesses | 35 | |
| Both animals in the 64 px icon, either reader | 16 | 23 and 29 |
| Both animals, from the whole scene (the sweep) | 40 of 50, and 47 at least by kind | |
| The scene taken for a photograph or a film still | 26 of 50 (24 a 3D render or digital painting) | |

Garrett chose the scenes for how they look, and was told these numbers on 2026-11-12. A scene's close-up shows
less of the second animal than a bust that was built to show it, and in 27 of the 50 a reader called some part of
the pilot worn (a cap, a hood, a helmet, a wig, a mask, a headdress): his note about Trace, across the roster. The
eight weakest, in each of which every stranger names the first animal and the second goes unnamed:

| Pilot | The second animal | What a stranger said of the avatar |
|---|---|---|
| ZODIAC, ocelot | heron | "some bird (owl or hawk)"; "cat ears, with a feather-like crest between them" |
| WINGMAN, puffin | pelican | "some white bird (gull or swan)"; the wing "appears to hang from a strap or harness" |
| MANGER, ibis | platypus | "beaver (or otter)"; the brow plate "a leather cap or helmet" |
| Shrapnel, toucan | newt | "lizard"; the crest "looks like a hood or hat" |
| Slingshot, flamingo | mantis shrimp | "mahi-mahi (dolphinfish)", "lobster or shrimp"; "glossy plastic-looking hands and shell" |
| TALON, lynx | jackal | "squirrel"; the tail "stands behind the shoulder rather than grows from the body" |
| NEEDLE, narwhal | seahorse | "lionfish"; the tusk "comes straight out of the tip of the snout like a beak" |
| Tender, macaw | narwhal | "seal"; "like a hood over a parrot face"; the tusk "looks stuck onto the beak" |

Several of these were named in their busts (WINGMAN's pouch, MANGER's bill, NEEDLE's ringed neck and spiral tail),
which is why a patch may show the painter the pilot's bust for one part (`bust=`), cut to that part (`bust_box=`),
since the same busts also show what a patch takes away (WINGMAN's plumes, NEEDLE's row of points).

**The four samples' read** (before the decision, kept as the record of how the mechanism was proved: one reader
found each scene's pilot among the 50 busts, another put each scene beside its bust):

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

What those reads changed is in `LESSONS` 57 to 71 (an animal's bare-skinned hand is painted as a person's; hair
words mean something else in a photograph; a face needs its whole outline; tell the wear where to stop; empty the
place by name; a loud unearthly sky reads as a render). What the 46 scenes after them taught is in `LESSONS` 72 on.

**The painters.** Words only: `gpt-image-2.5-sunburst` through the same Figma file as the busts, a brief of at most
1,000 words (`WORD_CAP`), asked for 2048x1152 and returned at 1536x864. It made 87 paintings for the fifty scenes
and their repaints (19 of them for the four samples), and its plan's credits ran out on 2026-11-12. From pictures:
GPT Image 2.6 through Weave, five runs, 55 credits. Both are in `PAINTERS.md`.

### The scenes in the game (2026-11-12)

Garrett, 2026-11-12 13:51 UTC: "as you have pointed out there are many little changes and tweaks we could make to
polish these and fix oddities. But we will do that on the next pass. for now these are an excellent start. Lets get
them into the game. update the avatars, and use the full size art for each pilot's profile page in the ark."

**What is in the build.** The eighteen callsign scenes, copied by `Tools/Build/author_pilot_scene_assets.py`
(`--check` is a ship gate; `--self-test` plants its faults) from `Docs/Pilots/art/scenes/<WORD>.jpg` to
`Assets/Resources/Pilots/Scenes/<WORD>.jpg`, and one catalog, `Assets/_SO_Assets/Pilots/PilotSceneCatalog.asset`
(`PilotSceneCatalogSO`): for each callsign the painted pilot's name, the scene's resource name, the close-up sprite
(the avatar, `Assets/_Graphics/Profile/Pilots/Avatar_<WORD>.png`), the close-up's box in the scene and the side the
pilot stands on, all read from `scenes.json`. The other thirty-two scenes stay under `Docs/`: those pilots are in
no crew, so nothing in the game can show them yet. The fifty avatars were already the scenes' close-ups.

**Where a player sees them.** The Ark's PILOTS tab (`ArkPilotsPage`, `author_menu_ark.py`) lists the crew, one cell
a pilot with the close-up; pressing one opens the pilot's PROFILE PAGE (`PilotProfileModal`,
`Resources/Pilots/PilotProfileView.prefab`, `author_menu_pilot_profile.py`, `ScreenSwitcher.OpenPilotProfile`):
the scene across the whole screen and the words in a panel on the side the pilot does not stand on.
`Docs/MENU_REDESIGN_B.md` section 92.1 is the page's record.

**The cut** is `PilotSceneImage.Frame`, the kit's `crop_box` in C#: the largest window of the screen's shape with
the face a little above the middle; a wider screen moves up until a tall close-up shows whole; then the window
moves the least it takes for the face to stand clear of the words. With no words it equals the manifest's recorded
crop within a pixel (90 scene and shape pairs). With the panel in its place, on all eighteen scenes at 21:9,
19.5:9, 16:9, 16:10, 3:2 and 4:3, every face is inside the view and clear of the words.

**Memory and size.** A scene is loaded by name when its pilot is drawn and unloaded when the next is shown or the
closed page has faded: one 1536x864 texture at a time, never eighteen. The import is a Sprite-typed texture, BC7
on Standalone, the platform default (ASTC) on Android and iOS, and 32 px on WebGL, where the browser Toybox never
opens the Ark (`ScreenSwitcher.IsModalInWebToybox` keeps the page out). 9.0 MB of JPG joins the repository.

**Whose words, whose picture.** The starter crew ARE the painted pilots (Garrett, 2026-11-12: "the stories and the
art got mixed up because none of the stories matched the art"). Each painted pilot is a person in
`Docs/Pilots/art/scenes/people.json`: the name the scene carries, the two animals it shows, a faction and home that
fit its place, and a story in three short paragraphs. The story does not explain the callsign's sight (the page has
its own Sight row); it opens the wider world and says what work the pilot's faction brings the crew (Garrett: "more
about exposing the player to the wider world and about what kind of jobs they will be finding through that pilot's
faction connection"; the jobs follow `Docs/Voyages/DEPTH.md` section 4). `author_pilot_scene_assets.py` writes them
into `Resources/Pilots/PaintedCrew` (`PaintedCrewSO`), and `AccountInventoryStore` paints every starter record from
it on each read, so a crew saved before this, and every later edit of a story, catches up; id, callsign, origin and
chronicle are kept, and voyage jobs follow the painted faction. A recruit is still a rolled person of their own, and
only then does the page's last line (`picturedTemplate`; blank it to drop the line) say who is pictured.
`author_menu_pilot_profile.py --check` fails a painted story that does not fit the Story row with its paragraph
breaks (9 to 12 of 14 lines today).

**Open:**

1. **The eight weakest are designed again as one creature each**, in words, as a pending `patch` in each record;
   painting them waits for the next pass (Garrett, 2026-11-12: "we will do that on the next pass"; it would take
   up to 16 runs on Weave, 176 credits at most). Three of the
   pairs are limits whatever is painted: a heron behind an ocelot's face, a platypus behind an ibis's bill, and a
   jackal, which is a fox to nearly everyone (`LESSONS` 43, 50). The words are a third draft: three critics read
   the first beside the pictures (an art director's eye, a painter's, a stranger's) and none of the eight was
   ready to send; the same three read the second against their notes and would send six as written, change one
   sentence of Slingshot's, and two of them still said no wording gets ZODIAC a heron in her pose. They then
   read only the sentences the third draft had rewritten, in four pilots: they would send Slingshot and Tender
   as written, and one clause each of Shrapnel's tail and ZODIAC's neck was changed as they asked. What they
   found is in each record's docstring and, as rules, in the skill's section 7. Their estimate of a stranger
   naming the second animal from the avatar, with nothing painted or tested: about one in two for WINGMAN,
   NEEDLE, Tender, Slingshot, TALON and Shrapnel (the last three by kind: a shrimp or lobster, a fox, a lizard),
   one in three for MANGER, and one in four or five for ZODIAC, which is the run to leave till last or leave out.
2. **What is still weak in the six painted again**: Trace's bell is still called worn by a reader who is sure it
   is a jellyfish; ZODIAC's heron is missed; the white patch on Flux sits where his ear was; Longshift's fingers
   read as fused.
3. **Twenty-four scenes read as a render or a digital painting**, and the icon-size read is 16 of 50. Neither has
   been worked on.
4. **The full-screen pilot profile** is built and not yet run in the editor ("The scenes in the game" above;
   the hand test is in `Docs/UNITY_VERIFICATION_CHECKLIST.md`). It shows a crew member's words over their
   callsign's painted pilot; art for each person, or crews drawn from the painted pilots, is the next decision.
   An upright phone has no pose of its own for the page.
5. **Size.** The painter returns 1536x864. A 4K screen needs an upscale or a painter that returns more.
6. **The hand test** of the avatars in the profile window and avatar picker
   (`Docs/UNITY_VERIFICATION_CHECKLIST.md`, "Pilot avatars are the close-ups of the pilots' scenes").
