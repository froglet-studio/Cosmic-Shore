# Fauna Possession Lab

**Page:** `Docs/Studios/FaunaLab.html` · **Artifact:** https://claude.ai/artifact/PMh24N63DgLuEJphkrbcV4 ·
**Kit:** `Tools/Build/fauna_kit/` · **Skill:** `/fauna-creation` · **Branch:** `cece/peaceful-babbage-tsg0kd`

Garrett, 2026-11-15: "our goal is great fauna, but first our goal is the skill that can make great fauna ... give me a
lab that allows me to control the existing fauna in the game like the jewel wasp does. then get your fauna creation
skill as good as you can make it, and use it to make some new fauna that will broaden the fun a jewel wasp can have. i
should be able to try out each new design in the lab, and give feedback on your fauna to help steer a better tool.
give me a vessel, ark, and prism cluster to attack."

## The brief

- **The decision this lab exists to make:** which creatures are fun to ride as a Jewel Wasp, and what the fauna kit
  must learn next to make better ones. One column per kind of fun (below); a creature is judged by a person riding
  it, and the columns say what the numbers can.
- **Fundamental it serves:** possession (the Jewel Wasp, `JEWEL_WASP.md`): any creature is a vessel for a while.
- **Rules it obeys:** the celestial laws (`/fauna-creation` section 2), PvP is petals only (a strike is a scored hit),
  a game mode never changes a vessel (the wasp's kit is the shipped one; only creatures differ), the ecology's
  invariants (nothing here changes them: nothing here is in the game).
- **The rule it comes closest to breaking:** law L2 (a body is braid spindles). The new creatures keep it. The
  shipped creatures mostly do not (below), and that is theirs to keep until Garrett picks one to redesign.
- **Who judges, and where:** Garrett, on the page, in its Feedback tab.
- **Retire condition for a new creature:** a "cut it" in the feedback log, or a second round in which it still wins
  no column and nobody says it feels good.

## Round 2 (2026-11-16): the Manta, and a silhouette of its own

Garrett: "these seem like variations on what we have ... take inspirations from the shark, brittlestar, and worm by
comparing them to their real counterparts ... give us a manta fauna but really try to build something that belongs in
this world and would be called a manta for its hypnotic resemblance, but is of course not the real thing or the
vessel. the fauna and vessels need to have unique silhouettes so they are all distinguished."

### What changed

1. **Round 1's five are archived.** They were one body plan (a spine, radial limbs, a tail sting: the Bee's) and a
   bare spoke star from ahead. They stay in the lab under "Round 1 archive" so their rides can still be compared.
2. **The study** (`/fauna-creation` section 9): what the real clade, the fauna and the vessel each take from the
   names Brittlestar, Shark, Worm and Manta, and the method that follows from it (name the clade's signature shapes
   and motion, take what the vessel did not, re-derive each under the laws, choose a body PLAN and not a limb count).
3. **A silhouette gate** (`Tools/Build/fauna_kit/silhouette.py`, law L12). Every vessel hull (28) and every creature,
   flattened to an outline from the side and from ahead, compared at every roll, nose to tail and along each axis.
   The bar is measured from the fleet each run: a new species must stand further from everything than three in four
   shipped bodies stand from their nearest look-alike.
4. **Limbs that beat.** A spec's spindle can carry a `beat` (a rotation about its root, with a phase), and the lab
   plays it. The Manta's wingbeat is a wave from shoulder to tip; opposite wings mirror, and the second pair runs a
   quarter beat behind the first.
5. **The Manta** (`specs/manta.py`, all twelve laws pass): four rib-fan wings a quarter turn apart (a swept spar of
   three braids, four ribs, a hem closing the hind edge, three thin plates a wing), a lobe curled round the Etherion
   between each pair, a three-braid whip carrying the sting. 57 u long, 64 u across. For a rider it glides (slow to
   gain and lose speed) and feeds on the wing: holding RT eats what passes between its lobes with no wait.

### Measured

`python3 Tools/Build/fauna_kit/silhouette.py --check` (0 = the same outline, 1 = nothing shared):

| | nearest look-alike | distance |
|---|---|---|
| shipped bodies (28 hulls, 7 creatures) | each other | 0.10 to 0.46, median 0.25; the bar (upper quartile) is **0.30** |
| **Manta** (new) | the Honeyguide hull | **0.42** (passes); 0.53 from the Manta hull |
| Brittlestar fauna against the Brittle Star hull | | 0.53 |
| Marlin (archived) | the Thresher hull | 0.31 |
| Basker (archived) | the Rhino hull | 0.43 |
| Medusa (archived) | the Grizzly hull | 0.32 |
| Nautilus (archived) | the Manta | 0.29 |
| Teredo (archived) | the Angler hull | 0.19 |

- The closest shipped pairs are hulls: several sit under 0.2 of each other. That is a finding about the fleet, not
  about fauna, and it is left for whoever owns those hulls.
- The measure agrees with "variations" for the Teredo and the Nautilus, is lukewarm on the Marlin and the Medusa,
  and disagrees on the Basker (0.43). It sees outlines, not body plans: whether the Basker's body comes back is
  Garrett's call.

The Manta on the scorecard (same games as round 1): duel 11, brawl 4, siege 59.5 s, graze 80.4, sprint 30.5 s,
slalom 34.7 s; a rookie: 3.6, 1.2, 116 s, 65.4, 43.3 s, 39.6 s. It wins no column. Feeding on the wing eats little
in the lab's sparse field (80 prisms against the QuadFish's 456): its meal is a line, and the lab has no trail to
fly down. It was built for its shape and its motion this round, not for a column.

### Checked

- `celestial.py --all` (seven specs celestial) and `--self-test`; `recreate.py --check` (0.00002 u, unchanged);
  `lab_export.py --check`; `verify_lab.cjs` PASS; `silhouette.py --self-test`.
- Read: the Manta's three-view sheet (a diamond with lobes and a whip from the side and from above, a cross with
  four short lobes from ahead), the silhouette sheet of all 41 bodies, and the Manta in the lab mid-turn.

### Found

1. **My first silhouette measure was wrong, and its own control said so.** Scaling each view to fill the grid made a
   dart seen end-on read as a disc. One scale per body fixed it, and several distances moved (the Marlin and the Bee
   went from the closest pair in the game to unremarkable).
2. A hull's travel axis is not always z in its model file, so every axis is tried.

### What only a person can judge

Whether it is hypnotic. The lab draws wires as lines in stand-in colours; the wingbeat is there, the game's look is
not. Nothing is in Unity.

### Decisions needed (Garrett)

1. **Is the four-winged Manta the right reading of "no up"?** Two flat wings with identical faces would be closer to
   the animal and closer to the Manta hull's outline; law L6 asks for three-fold or more, so this one is a cross.
2. **Which clade next** (`/fauna-creation` section 9 lists eight body plans the game does not have).
3. Whether any round 1 body comes back, and round 1's open decisions below.

## Round 1 (2026-11-15)

### What was built

1. **The kit learned the creatures the game already has.**
   - `fbx_model.py` reads a model file as Unity imports it (file scale, handedness, a Z-up file's root turn), and
     `prefab_read.py` flattens a prefab with its nested prefab and model instances. Two things the skills said could
     not be done without the editor are now done: a model's MESH fileID is `xxHash64("Type:Mesh-><name>0")`
     (reproduces two shipped ids), and a prism parented to a bone INSIDE a model instance is placed through the rig's
     constraints, which name the bones (the Brittlestar's ten prisms).
   - `dossier.py` measures the seven creatures a wasp can possess (Tadpole, QuadFish, Clawfish, Shark, Brittlestar,
     Bee, Worm Colony): wires, prisms, heart, the limbs the attack whips (the game's own `FaunaAttackDriver.Scan`
     rule on the measured tree), and the numbers a possession reads.
2. **The kit recreates them, and measures the difference** (`recreate.py`). Each creature's body is built from a spec
   by `body.from_spec`, the function a new species goes through, and held against the prefab measured again.
3. **A new law, L11: what a rider gets.** A spec states its top speed, mouth reach and attack, and the attack must
   be one the body can play (the game whips a body with three limb chains under one hub, and lunges any other).
4. **Five new creatures**, as celestial specs (all ten earlier laws and L11 pass): Marlin, Basker, Medusa, Nautilus,
   Teredo. Each brings one move of its own to the same four controls.
5. **The lab.** Ride all twelve with the wasp's shipped numbers (read from `JewelWaspConfig.asset`), in any of the
   four element forms, at a rival vessel (still, looping, evading or attacking), an Ark (108 sockets of its real
   kinds, regrowing one every 2 s) and a prism cluster. Leave the host with A and sting another. A headless
   scorecard rides every creature with an input-only pilot, and a Feedback tab records a rating, tags and a note per
   creature in the shared decision log.

### Measured: the recreation

`python3 Tools/Build/fauna_kit/recreate.py --check` (largest difference between the spec's body and the shipped
prefab, world units at root scale 1; tolerance 0.002):

| Creature | How | prisms | wires | heart | attack limbs | numbers |
|---|---|---|---|---|---|---|
| Tadpole | transcribed | 0.00000 | 0.00000 | 0.00000 | 0.00000 | 0.00000 |
| QuadFish | transcribed | 0.00000 | 0.00000 | 0.00000 | 0.00000 | 0.00000 |
| Clawfish | transcribed | 0.00000 | 0.00000 | 0.00000 | 0.00000 | 0.00000 |
| Shark | transcribed | 0.00000 | 0.00002 | 0.00000 | 0.00000 | 0.00000 |
| Brittlestar | transcribed | 0.00000 | 0.00001 | 0.00000 | 0.00000 | 0.00000 |
| Bee | **rebuilt from rules** | 0.00000 | 0.00002 | 0.00000 | 0.00000 | 0.00000 |
| Worm Colony (head, body, tail) | transcribed | 0.00000 | 0.00001 | 0.00000 | 0.00000 | 0.00000 |

What this does and does not prove:
- **The Bee is the real test.** Its spec is the generative language (braids from a point to a point, prisms by rule),
  and the body rebuilt from those rules matches the shipped prefab to 0.00002 u, wires and attack limbs included.
- **The other six are transcriptions.** Their spec states the shipped meshes by reference and the prisms, heart and
  limbs by pose. The table proves the kit's language can state them and that nothing is lost between a spec and a
  body. It does not prove the kit can generate a Shark from rules: their bodies are sculpted models.
- The first run of this table was not zero. It found a wrong limb match (several limbs share a root joint), a wrong
  attack-limb joint on the Bee (the game's last joint is the braid's own transform, not the limb's tip), and a
  non-unit quaternion on the QuadFish's sheared fins that made identical boxes read 0.015 apart (a metric bug).
- `recreate.py --self-test` plants nine drifts (a tooth moved 0.02, a fin resized, a kind changed, a model shifted,
  a heart moved, a joint moved, a speed changed, a Bee vane and rib moved in the generative spec): all nine measured.

`python3 Tools/Build/fauna_kit/celestial.py --shipped` lists the laws each shipped body breaks (the redesign list of
`/fauna-creation` section 7.1). Every one but the Bee steers with world up; the QuadFish, Clawfish, Shark,
Brittlestar and Worm wear sculpted models (L2); the Tadpole has one health prism and no stinger.

### Measured: the scorecard

`window.__lab.runBatch()` on the page (the Scorecard tab). An input-only pilot, seeds 1-5 (each starts the host up to
30 u off its mark), every creature at scale 1 and its own base speed, the wasp at element level 0.

| Creature | duel (strikes) | brawl (strikes) | siege (s, low) | graze (prisms) | sprint (s, low) | slalom (s, low) |
|---|---|---|---|---|---|---|
| Tadpole | 0 | 1 | 21.91 | 213.6 | 60 | 61.17 |
| QuadFish | 0 | 0 | **7.37** | 456.2 | 34.17 | 34.62 |
| Clawfish | 11 | 4 | 10.45 | 364.6 | 29.76 | 30.94 |
| Shark | 12.8 | 4 | 29.29 | 146.2 | 27.13 | 28.77 |
| Brittlestar | 0 | 4.4 | 13.69 | 331.8 | 38.4 | 38.22 |
| Bee | 0 | 4 | 80.81 | 52 | 36.88 | 36.92 |
| Worm Colony | 0 | 5 | 12.7 | 346.4 | 36.47 | 35.95 |
| Marlin (new, for sprint) | **17.2** | 4.4 | 120 | 17.2 | **20.26** | **27.19** |
| Basker (new, for graze) | 0 | 5 | 8.56 | **649** | 56.96 | 56.68 |
| Medusa (new, for siege) | 0 | 6 | 8.47 | 474 | 39.54 | 40.05 |
| Nautilus (new, for slalom) | 15.8 | 7.6 | 82.95 | 59.6 | 25.48 | 27.68 |
| Teredo (new, for brawl) | 0 | **12** | 42.26 | 123 | 31.82 | 34.02 |
| Control: RT never pressed | 0 | 0 | 120 | 0 | 60 | 61.9 |

The games: **duel**, strikes in 60 s on a vessel that turns away at 44 u/s; **brawl**, strikes in 60 s on a vessel
strafing the host at 80 u/s from outside the shipped strike reach; **siege**, seconds to strip the Ark from 108
sockets to 30; **graze**, prisms eaten in 30 s from a field of 700; **sprint**, seconds to the first strike on a still
vessel 1500 u away; **slalom**, seconds through ten tight gates.

Reading it:
- **Three new creatures win their column clearly:** the Marlin (sprint 20.3 s against the next 25.5), the Basker
  (graze 649 against 474) and the Teredo (brawl 12 against 7.6).
- **Two do not.** The Nautilus ties the Marlin in the slalom (27.68 against 27.19; the five seeds spread 0.8 and
  1.9 s, so that is no difference). The Medusa ties the shipped QuadFish and the Basker at the Ark (8.47, 7.37, 8.56;
  spreads 1.4 to 3.2 s). Neither earned its place on numbers.
- **The shipped creatures are not equal hosts.** Five of seven cannot land one strike on a vessel that turns away at
  44 u/s (at wasp Time level 0 they swim 22 to 42 u/s). The QuadFish is the best shipped eater (mouth 81 u).
- **A rookie pilot** (the second button) loses most on the Nautilus (brawl 7.6 to 0.6, siege never) and the Shark;
  the Basker barely changes (649 to 648.6): it is the forgiving host.
- Noise: the vessel and slalom games moved 0 to 4 across seeds, graze about 30 to 70 prisms.

### Checked

- `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/FaunaLab.html`: PASS (no console or WebGL error, no page
  scroll on desktop, no sideways scroll on a phone, the hook, every SPEC key in SHIPPED and in range, the manual
  clock, the batch byte-identical twice and across two page loads). The stage is WebGL, so its blank check is the
  screenshots, which were read: a Shark from behind, a Medusa eating the Ark, a Basker inside the cluster.
- `celestial.py --all` (six specs celestial), `--self-test` (every law but L4 and L10 catches a planted defect, L11 three),
  `dossier.py --self-test`, `prefab_read.py --self-test` (51 stripped documents obey source XOR instance),
  `recreate.py --check` and `--self-test`, `lab_export.py --check` (the page's data block is what the kit exports),
  `author_bee_assets.py --check` (the Bee's spec gained a possession block; its assets still match).
- `bash .claude/skills/ship/gates.sh`: 111 checks ok; two fail in files this round did not touch
  (`doc_summaries` on `SwitchbackBlastRunTests.cs:580`, and `colour_grade.json` stale), and the reference compile
  was skipped (no .NET SDK in this container; no C# changed).
- The shared decision log: `ArtifactData list decisions` on the published page reads back (empty). Pressing Save in
  the viewer was not exercised.

### Found

1. **The shipped host camera sits inside a big host.** The stand-back is `hostCameraBase + PossessionBodyRadius x
   hostCameraBodies`, and the body radius is `max(2, the root's scale)`, which is the SCALE and not the size: 2 for
   a Shark that is 56 u long, so the camera stands 42 u behind its heart, inside its tail. The lab's camera stands
   back at least 1.6 body lengths (the Wasp tab's last slider; 0 is the game's formula). Not seen in the Unity
   editor: it is read from `Fauna.Possession.cs` and `JewelWaspExecutor.cs`. **Decision needed (Garrett):** whether
   `PossessionBodyRadius` should be the body's measured reach.
2. **The same radius is the strike's reach.** A strike lands within `attackReach 22 + body radius`, so every
   creature that is not a worm reaches 24 u whatever its size. A Brittlestar's arms span 42 u from its hub and its
   whip animates them, but the hit is still 24 u from its root.
3. **Holding RT wastes attacks.** RT attacks at a vessel within three reaches (72 u), the strike can land only from
   0.21 s to 0.6 s later and only within one reach, and the recharge is spent on the press. A pilot who holds RT
   (the rookie) attacks too early every time. The scorecard's skilled pilot presses when the gap will have closed.
4. **Three seeds were one run three times** in the vessel and slalom games until the host's start was seeded.
5. The first scorecard's Siege and Graze columns sat at a lab limit for nearly every creature (the whole cluster
   eaten, the Ark stripped as fast as it regrew). Each game now pins its own conditions and none saturates.
6. A stand-in vessel that chases a slow host orbits it at its own turn radius and never comes inside reach. It now
   flies committed passes.

### What this lab does not model

Listed on the page's About tab: the game's look, a real rival hull and its petals, the Ark's model, course and danger
contact, collisions, starvation and the stomach, a LightFauna's mouth growing with its girth, the murmuration,
multiplayer. The new creatures' own moves and every `turn_rate` are proposals: nothing in the game has them.

### Decisions needed (Garrett)

1. **Which new creatures are worth another round**, from riding them (the Feedback tab).
2. **May a new limb wear a shipped limb mesh** (a Brittlestar arm, a Shark vane) as well as the braid? L2 says braid
   only, so the five new bodies are plain beside the sculpted ones. Reusing shipped limb meshes as a parts library is
   the cheapest way to raise their look; it is a change to L2 and so it is yours.
3. **The body radius** (Found 1 and 2).
4. **Which shipped creature to redesign first** under the celestial laws (`celestial.py --shipped`).

### Next round (proposed)

- Apply the feedback log to the specs (`ArtifactData list decisions` at the start of the round).
- The murmuration as a host. A possessed plant (the wasp's LT).
- Build the kept creatures in Unity with `fauna_kit/build.py`, and their own moves as species overrides
  (`PlayPossessionAttack`, `PossessedSuction`), after a `who_works_where` look and a design brief each.
