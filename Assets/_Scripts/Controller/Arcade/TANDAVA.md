# Tandava

`GameModes.Tandava = 62` · ARENA card · Squirrel, Sparrow, Rhino · co-op (every pilot on one domain) · scene
`MinigameTandava` · assets owned by `Tools/Build/author_tandava_assets.py` (`--check`, `--self-test`).

**Status: authored headless, NOT yet run in the Unity editor.** Everything below that describes behaviour is from the
headless harness (`Tools/Build/swarm_core_harness`, mode `tandava`, T1-T15), the reference compile (`unity_refcompile`,
player and editor), a Roslyn type-check of the swarm glue, and the generators. QA: `QA-TANDAVA-9..14` in
`Docs/UNITY_VERIFICATION_CHECKLIST.md` (§ Tandava; `/verify-unity` could not run: no Unity editor or `unity` CLI in the
authoring session). `QA-TANDAVA-1..8` describe the first design and are superseded.

**This is the second design (2026-10-06).** The first - a swarm racing down a long tube cell to an exit, through a young
serpent, two bigger serpents, a bull and a winged lion - was rejected by the prompter as "stupid and boring", for eight
reasons, each answered here:

| The prompter's note | What changed |
|---|---|
| 1. the young serpent and the serpent go; only the Great Serpent remains | four forms; the swarm hatches WHOLE as the Great Serpent (seeded at its full plan, harness T2) |
| 2. cut limbs must not shrink it; once a form is reached it is remembered and regrows, like the automata | the director never steps a form back; regrowth is fast and funded (no kill-lay-hold, a 1.5 s ramp, laid at the wound - T4: a 30% tail cut regrows in 1.1 s) and only feeding holds it |
| 3. no visible form change; at least show it in the goals tab | the top-left goal stack draws the mode's own rows (`IGoalSource`): the form's name and progress, flaring on every change; a gold prism burst from the body; a toast naming the variant |
| 4. no straight path to victory: shut it in the cell like Scarab Scramble, flora dispersed, it eats anywhere, defends while it eats and is most vulnerable then | a CLOSED standard cell, 16 dispersed plants, threat-weighted foraging; the FEED pose puts its plates out round its mouth as DANGER guards and holds its regrowth |
| 5. no bull; a sea creature from the myth - a many-headed serpent; the lion becomes a sea lion; no fire anywhere, use gold prisms | Great Serpent -> Many-Headed Serpent (5, 7 or 10 heads) -> Lord of the Dance -> Sea Lion; the ring of fire is a pearl HALO; the effects are gold prism debris; the dance glows gold |
| 6. every match a different formation, but only four forms | three variants of each form, one drawn per match (T14) |
| 7. faster, and its speed and behaviour set by the threat | calm 60 u/s, wary 90, fleeing 126 (T3, T8), the turn up to 2.5x, the whole body carried round the turn (93% of its shape kept vs 18%) |
| 8. the artifact | the playable lab, rebuilt for this design (§4.1) |

## 1. The pitch

One creature lives in this cell, and it is hungry. It hatches as a **Great Serpent** and goes where it likes to eat,
plant to plant through a closed cell, at a pace and in a manner set by how threatened it feels. Charge it and it turns
wary, then bolts. Leave it alone and it cruises to the nearest good plant. At a plant it settles into its **feed pose**:
its hood comes off its head to orbit its mouth as a ring of **danger guards** (a pilot who touches one is stung), and
it **stops regrowing**. That is the moment to strike: the body, not the head. Hurt it badly enough at the table and the
meal is **broken**: it bolts, hungry.

Fed, it changes. The Great Serpent becomes a **Many-Headed Serpent** whose heads all dip to the food when it eats. Fed
again, it **rises where it stands** into the **Lord of the Dance**: a halo of twelve rings lights round the figure, its
attendant packs patrol the halo, a drum runs, and the cell glows gold. When the drum stops it becomes the **Sea Lion**,
and one last feast completes the cycle.

Every form, once taken, is **remembered**: cut limbs grow back from what it has eaten. So the pilots win by denying it
and by striking it when it cannot heal:
- **shatter it**: cut its body below 35% of its form (the shatter threshold, `EndConditionOverrides`);
- **break the dance**: break nine of the twelve halo rings before the drum stops, past the attendants;
- **hold it off**: the cycle must complete within seven minutes of the go;
- or wipe it out, or starve it to pieces.

They lose when the Sea Lion's last feast is eaten. Only the mode carries the name; the forms, the narrator and every UI
string use plain English names.

## 2. The forms

Every match draws ONE variant of each form (`TandavaDirectorCore.DrawVariants`, replicated as one packed int), so no two
matches meet the same animal. Counts are plan units / in-game tadpoles at `PlanDensity` 3. Every form is at least as big
as every variant of the one before it (`tandava_plans.validate`, harness T1): a commit only ever GROWS the body, paid
from the bank, so no surplus ever crowds a new shape's wells into a blob.

| # | Form | Variants (units / tadpoles) | What it is | Feeding |
|---|---|---|---|---|
| 1 | **Great Serpent** | long and slender 157 / 471 · longer, narrow hood 169 / 507 · short and thick, broad hood 150 / 450 | Mass coils, Space shimmer rods along the back, a Time tail rattle, and a hood of Charge plates (shield tier) | the hood leaves the head to orbit the mouth as danger guards; the body's wave settles |
| 2 | **Many-Headed Serpent** | **Five-Headed** 175 / 525 · **Seven-Headed** 181 / 543 · **Ten-Headed**, two tails 195 / 585 | a serpent's body whose front widens into a collar of Space necks fanned over the top, each with a Mass head and two Charge hood plates | every head DIPS to the food, the heads closing into a ring round the mouth, snouts in; the hoods orbit outside them |
| 3 | **Lord of the Dance** | 204 / 612 each (the mirror pose; the hair flown wider) | the Nataraja as a sculpture of tadpoles: the figure, a Charge gem in the raised hand (no fire), the crown, the halo and four Time attendant packs patrolling outside it | does not eat |
| 4 | **Sea Lion** | crescent fluke, 4 rays 206 / 618 · forked fluke, 5 rays 211 / 633 · long fins, 3 rays 214 / 642 | the heraldic sea-lion: a lion's head and Charge mane on a Mass body, great sculling fore-fins of Space rays, a Time fish's tail with a Charge fluke. No legs: every form flies | the mane and the fluke leave the body to orbit the mouth |

Every eating form has a **feed twin** (`<key>_feed`): the same units in the same order, re-arranged. The commit between
them is a POSE commit (`RequestPose`): the same members re-sorting, no molt, and the lay ease kept (feeding is not a
wound - T6). Every plan is Mass-majority (the director, not the census, picks the form); the plans are 21 JSONs baked
by `Tools/Build/tandava_plans.py`, which also bakes each eating plan's **mouth** (where the director puts the food) and
each dance plan's **halo**.

The ten-headed variant is the prompter's ("a ten-headed or multi-tailed snake"); five and seven are the traditional
counts of a many-hooded naga. The Lord of the Dance is the ascension of the mode's name (Shiva's dance) - the one
reference the mode carries, never named on screen.

## 3. How it works

### 3.1 The swarm: the sort core, scripted, with the director's levers

`SwarmSortParams.Scripted` (phase A) lets a director name the form; the census never does. This design adds five
levers, every one a no-op at its default so every shipped swarm is bit-for-bit unchanged (the sort, round-11d, tick-job,
lineage and LOD suites all pass):

| Lever | What it does | Where |
|---|---|---|
| `SetLevers(cruiseScale, turnScale, holdLaying)` | the body's speed and turn as multiples of the config's, and a laying hold | `IScriptedSwarmCore` / `SwarmSortCore` / `SwarmTickJob` / `SwarmFauna` |
| `RequestPose(plan)` | a plan commit that keeps the lay ease (a re-arrangement, not a new body) | same |
| `SwarmSortParams.TurnCarry` (`SwarmFaunaConfigSO.SortTurnCarry`) | members ride each step's heading turn rigidly, as they already ride the swim. 0 = shipped: a 300 u body's tail, moving at turn x length, strings out behind a sharp turn | `SwarmSortCore.CarryTurn` |
| `SwarmTickSettings.PlanDanger` | a Charge member in a plan well authored DANGER tier wears a danger plate - a designed body part, not a startle. On for scripted plans only: the research plans' tier-1 marks stay unread | `SwarmTickJob.Build` |
| `ISwarmDirector.TryGetSeedForm` | the scripted form a swarm hatches as (the drawn variant) | `SwarmFauna.Seed` |

and three readouts: `SwarmFauna.MembersLost` (monotone, every death), `SampleMemberPositions` (where an effect is thrown
from) and `LifeForm.HealthBlockCount` (a plant's live prisms: what is left to eat).

### 3.2 The closed cell and its food

The standard membrane (`CapsuleMembrane`, radius 1,200) is the creature's wall: the swarm's own member clamp holds every
member inside 0.97 of it, and the director keeps the body's centre within 1,004 u of the cell's centre (the membrane less
the longest body's half-length). The nucleus (radius 392) is glass the creature swims through; fauna eat nothing inside
it. Pilots spawn on a line across the cell from the hatch (x = 950, facing it); the creature hatches at x = -650.

The flora is two forks of the canonical Borromean configs, **dispersed**: 12 Mass and 4 Space plants, spread apart
(`SpreadPlanting`) through the band 470-1,060 u from the centre, each capped at 60 prisms (4,372 volume - a meal is most
of one). A plant regrows when it is left alone and dies when it is eaten down to 4 prisms; the cell's seeder replants it
elsewhere. The director surveys every plant the swarm can eat (`SwarmFauna.CanEat`, the one edibility predicate) twice a
second and weighs it by its live prisms.

**Where it eats** (`TandavaDirectorCore.ChooseFood`): score = sqrt(food) x safety / (distance + 300), where every pilot
within about 350 u of a plant spoils it (by half when the creature is calm, by 90% when it is wary). The plant it is
already heading for scores 1.3x (it commits), and a plant it just left rests 25 s. So it goes wherever the food is good
and the pilots are not - harness T9: with a pilot parked by the nearer of two plants, it takes the far one.

### 3.3 Threat and mood

THREAT (0..1) is the larger of two readings, smoothed (rising in 0.25 s, falling over 2.5 s):
- the pilots' pressure: each pilot within 450 u presses by its nearness, more when it closes fast (80 u/s reads as a
  full charge); two pilots press harder than one (a soft OR);
- the wound rate: losing 4% of its full body a second reads as full threat.

| Mood | Enters at | Speed (x the config's 60 u/s) | Turn | Behaviour |
|---|---|---|---|---|
| Calm | below 0.2 | 1.0 - **60 u/s** | 1.0 | the best plant, a plant spoiled by half by a nearby pilot |
| Wary | 0.3 | 1.5 - **90 u/s** | 1.6 | hurries, and avoids plants near pilots (spoiled by 90%) |
| Fleeing | 0.65 (out below 0.4, at least 4 s) | 2.1 - **126 u/s** | 2.5 | bolts AWAY from the pilots, weighted by how near each is; cornered at the wall, it runs along it |
| Feeding | - | 0.5 (it holds station) | 1.0 | see §3.4. It does not bolt from pilots it merely sees: its guards are out |

Harness T8: a pilot charging from 800 u at 130 u/s turned it wary at 4.0 s and sent it bolting at 5.2 s, always away,
at up to 126 u/s; alone again it calmed in 3.6 s. A Sparrow (top 135 u/s) can just keep up with a bolting creature; a
Squirrel or a Rhino can catch it.

### 3.4 Feeding: the guards, and the one time it cannot heal

When its MOUTH is within 70 u of the chosen plant it begins to eat: the body takes its feed twin (the plates going out to
orbit the mouth as a ring of DANGER plates, 39-40 u in radius - the protectors; T6: every plate on the ring and every
one a danger plate after 10 s), slides so the plant sits at the feed pose's mouth, and stops laying (`holdLaying`). A
pilot who touches a danger plate is stung (`VesselElementalDebuffByDangerPrismEffect`: an opposing-domain hull loses
petals) and slowed. The body behind the ring has no shields left - they are the guards - and cannot regrow: this is
where the pilots strike.

A meal ends when it has eaten one `MealVolume` (4,000) there or all its form needs, when its stomach is full, when the
plant gives it nothing for 6 s (after a 4 s settle), after 30 s, or when it is **broken**: losing 20% of its full body
during one meal sends it bolting (`TandavaMealEnd.Broken`; T10: a 21% cut broke the meal in 1.1 s). A plant it leaves
rests 25 s.

Feeding is round-robin intake (`SwarmFaunaConfigSO.BitersPerStep` 8 members asked a tick, each biting a prism it
touches), so it is the members AT the plant that eat - which is why every form's feed pose brings its head, its snouts
or its jaws to the mouth. Measured in the harness: 260-370 volume/s, a meal in 7-10 s.

### 3.5 A form is remembered

The director never steps a form back. A cut limb regrows: the sort core lays at the wound (`SortBudAtWound`), at up to
24 eggs a step, with no hold after a kill and a 1.5 s ramp - harness T4: a fed Great Serpent with its tail third cut
off grows back to 93% in 1.1 s, the same form, nothing it did not lose dying, and the eggs paid from its stomach.
Laying is held only while it eats (T5).

Regrowth is PAID: every egg costs eaten volume (`SwarmFaunaConfigSO.EggVolume`). So the pilots' cuts cost the creature
food, and food is evolution. A form moves on when its body is at 90% of its full plan AND its stomach holds the form's
**bank**:

| Form | Bank (share of the stomach, 35,903 volume at 500 eggs) |
|---|---|
| Great Serpent | 0.35 (12,566) |
| Many-Headed Serpent | 0.60 (21,542) - the dance's offering |
| Lord of the Dance | none: the drum decides |
| Sea Lion | 0.85 (30,517) - the last feast, which completes the cycle |

The banks RISE, so what one form carries over never skips the next, and all sit under the 0.98 stomach fill at which a
meal ends full (a full stomach stops grazing: a bank above it could never be reached - the harness's first tuning hit
exactly that deadlock). A form is never taken mid-meal.

A creature with an empty stomach cannot heal. That is the pilots' long game: break its meals and its cuts stay cut (T11:
every meal struck from 25 s in, it was shattered at 123 s, never reaching the dance).

### 3.6 The ascension: rising in place, the halo, the drum

When the Many-Headed Serpent is banked it **rises where it stands** into the Lord of the Dance (pulled in from the wall
only as far as the halo needs: `DanceReach` 250 u). The figure assembles for 12 s (`RiseSeconds`), then the **halo** of
twelve `TandavaHaloRing` switch rings lights round it, 167 u in radius, 88 u apart, each with a 24 u mouth, in the
plane the figure stands in. The four attendant packs patrol at 202 u. A ring is **guarded** while six or more
attendants (Time members) are within 40 u of its guard post, and a guarded ring dims and cannot be broken (the switch
law: drawn smaller than its trigger is legal, drawn larger is the lie). Threading an open ring breaks it, scoring the
pilot (`IRoundStats.SwitchesThreaded`, 25 points) and throwing gold where it stood. Break nine before the 30 s drum stops
and the dance is broken (the figure falls back into the serpent it rose from - a molt, never a kill); otherwise, when
the drum stops, it is the Sea Lion. Harness T12: the halo lit 12.0 s after the rise; nobody threading, the Sea Lion
came at the drum's end; threading a ring every 1.5 s broke the dance (10 tries, one held by the attendants); the packs
guard about a fifth of the halo at any moment.

The rings are pearl (`ToySwitchSignal.Halo`, value 3 - it was `Flame`): a fixed warm white, the one colour no playable
domain, no danger rim and no free pickup wears (`ToySwitchVocabularyTests` holds every pair apart).

### 3.7 The HUD: the goals, top left

`TandavaController` is the goal stack's `IGoalSource` on every peer (a new seam: `GoalStack.Source`, mode-worded rows
with `GoalEntry.Progress` - a value the mode words over a hairline it fills). Its rows:

| Row | Roaming / feeding | The dance |
|---|---|---|
| 1 (primary) | the variant's name ("Seven-Headed Serpent") and its progress to the next form, "62%" over a bar; the Sea Lion's reads "Sea Lion - the last feast" | "Halo rings broken 4/9" |
| 2 | what it is doing - Roaming / Wary / Fleeing / **Feeding - strike the body** / Rising - and its body, "Body 86%", over a bar | "Dancing - break the halo", its body |
| 3 | "Time left 4:12" | "Drum 0:12" |

Every form change bumps the source's `Revision`, and the stack flares its primary row the first time it draws a new
revision - so a new form is a new name lighting up where the pilots already look. The narrator toasts it too, naming
the variant drawn ("Its coils split: a Seven-Headed Serpent."), and the creature's old shape shatters into gold.

### 3.8 No fire: the gold burst, and the cell glows for the dance alone

The mode has no fire anywhere (the prompter's call). Its effect is **gold prism debris**: `TandavaGoldBurst` throws the
platform's own prism death shards (`PrismDebris`, the batched entity debris every dying prism throws) in the Gold
domain's prism colours - 64 from the creature's own members at every form change, 18 from a broken halo ring - with a
gold light over the place (`PrismLit.PublishLight`) for 0.6 s. Debris, not mass: no collider, no volume, nothing the cell
counts.

`CellVisualTint` (phase A) recolours the cell's own membrane, nucleus and cytoplasm, never a shared material and never
what they do. The cell keeps its own colours for every form and every moment but one: from the rise into the Lord of
the Dance until the dance ends it blooms and eases into GOLD (`TandavaSettings.AscensionPalette`), and eases back after.

### 3.9 Pilots, stakes and the AI

| Outcome | Pilots | How |
|---|---|---|
| `Shattered` | win | its body below 35% of its form, once it has held 60% (so a body still growing into a new form never reads as shattered) |
| `Starved` | win | the same, while its own unfed clock has run out |
| `Wiped` | win | every member dead |
| `DanceBroken` | win | nine halo rings broken before the drum stops |
| `HeldOff` | win | seven minutes from the go and the cycle is not complete |
| `Completed` | lose | the Sea Lion's bank is full: the cycle is complete |

A pilot's score is the members they culled (`LifeformsKilled`, attributed kills only) plus 25 per halo ring. The free
run (nobody opposing it) completes in 211-245 s over 9-10 meals (T7), well inside the seven-minute clock: the pilots
must take about three minutes off it - every broken meal costs it the meal, the flight and the food to regrow.

AI pilots hunt it (`AIPilot.SetExternalTargetProvider`): at the body led toward where it is going, spread by seat; while
it feeds, at its body 70 u behind its centre, clear of the guards; in the dance, at the nearest open halo ring, aimed
past it so the line threads the mouth. Honest limit (the Tollway rule, restated for speed): the Squirrel's and Sparrow's
autopilots fly at cruise (60 / 35 u/s); a bot catches the creature only at a plant. An all-AI lobby is not expected to win.

### 3.10 Networking

The server runs the director on its own swarm and replicates: the variants (one packed int), the form, the plan the body
wears, the phase and mood (every peer derives the same speed and turn from them - `TandavaDirectorCore.LeversFor`), the
goal, the anchor, the HUD's numbers, the halo's placement and its broken / guarded masks, and the outcome. A client runs
its own swarm (fauna are client-local, the Brood Rush precedent): it takes the replicated plan, levers and goal (written
straight into `Fauna.Goal` every published tick - the fauna's own goal poll is seconds apart, far too slow for a creature
that bolts) and is nudged toward the server's anchor (20% of the gap a tick, at most 8 u, past 25 u). A client that has
not heard the variant draw when its swarm hatches hatches as the first variant and re-sorts into the drawn one on its
first tick. Narration is an index into the settings' lines (`TandavaLine`), so every peer reads its own text.

## 4. What the harness proves (`bash Tools/Build/swarm_core_harness/run.sh <plans> tandava <tandava plans>`)

The shipped sort core (scripted, with the levers) and the shipped director in a closed 1,200 u cell of 16 dispersed,
regrowing plants (`TandavaHarness.cs`, header). The food model keeps the one thing `SwarmFauna.Feed` depends on -
geometry: BitersPerStep members asked round-robin, each biting a prism within reach. Pilots are scripted policies.

| Test | What it shows |
|---|---|
| T1 | 21 plans; every twin is its travel plan's census; every eating plan bakes a mouth, every dance plan a halo; the forms only grow; every feed twin's plates are danger tier |
| T2 | every Great Serpent variant hatches whole (450-507 tadpoles) and holds 93-98% of its shape 15 s later |
| T3 | calm 61 u/s against the config's 60; x2.1 = 127 u/s; the turn carry keeps 93% of the body's shape round a hard fleeing turn (18% without) |
| T4 | a 30% tail cut regrows to 93% in 1.1 s, the same form, no self-inflicted deaths, 3,044 volume paid |
| T5 | held, a 20% cut stays cut; let go, it regrows in 0.3 s |
| T6 | the feed pose: every plate on the guard ring, every one a DANGER plate (and none without `PlanDanger`); the lay ease kept |
| T7 | three free runs complete in 211-245 s, every form in order, no egg laid at a meal, never leaving the cell, rising where it stood |
| T8 | charged: wary, then bolting away at up to 126 u/s; alone again, calm in 3.6 s |
| T9 | it takes the far plant when a pilot sits by the near one |
| T10 | a 21% cut mid-meal breaks it and it bolts; nothing laid while it ate; fed before, it regrows at once afterwards |
| T11 | every meal broken: the pilots win (shattered at 123 s) |
| T12 | the halo lights 12 s after the rise; the drum's end brings the Sea Lion; nine rings broken break the dance; the packs guard ~20% |
| T13 | 30% of its body: shattered (starved if starving); half its body fights on; none: wiped |
| T14 | over 300 seeds every variant of every form is drawn; the draw packs into one int and is the same on every peer |
| T15 | the clock runs out: held off |

What it is not: the game's bites are prism queries against real plants, its pilots are people and its kills are
collisions, so the TIMES above are a model, not a measurement - QA-TANDAVA-9..14 measure them.

### 4.1 What the playable lab flagged

The Tandava Form Lab (the artifact: the 21 shipped plans, a JS port of `TandavaDirectorCore` over the harness's food
model, a simplified flock, and a click-flown dart with two autopilot wingmates) reproduces T7 - unopposed it completes
in 210-237 s over 9-10 meals at 333-384 volume/s - and flagged three things the harness's scripted pilots never tried.
Each is a model result to check in the Editor (QA-TANDAVA-15), not a measured defect:
- **The hatchling cannot heal.** The sort core hatches with an empty stomach (`SwarmSortCore.Seed`), so until its first
  meal (about 20 s in) every cut sticks. A starting stomach of one meal barely helped in the model against a relentless
  chase; whether to give it one is a design call (it is mass the seed would have to pay for).
- **A cut along the spine is forty times a cut across it.** A 6 u pass across a Great Serpent takes about 2% of it; the
  same pass along its spine (a chase from behind) takes about 40%. How hard a pursuer can do that depends on the hull's
  turn at speed: with a point-mass dart that turns on a dime, one relentless pilot starved it in under a minute; capped at
  the hull-like 420 u/s^2 of lateral grip, the same chase cut almost nothing.
- **Two autopilot wingmates broke the dance on their own.** Riding the halo's circle threads ring after ring (§3.6); at
  70 u/s and a tight turn, two bots took nine rings in about 9 s. If the game's `AIPilot` threads as cleanly, an all-AI
  lobby can win through the halo, against §3.9's honest limit.

## 5. Numbers and where they live

| Number | Value | Lives in |
|---|---|---|
| speed, turn, the turn carry | Cruise 3 voxels/step (60 u/s), TurnPerStep 0.03, SortTurnCarry 1 | `TandavaSwarmFaunaConfig` |
| regrowth | SortLayRate 0.084, SortLayMax 8 (x3), KillLayHoldSeconds 0, SortLayRampSeconds 1.5, BudAtWound | same |
| the stomach and intake | StomachEggs 500 (35,903 volume), BitersPerStep 8, SeedMembers 180 (x3, capped at the plan: it hatches whole) | same |
| every director dial (threat, moods, levers, food, feeding, shatter, ascension, clock) | `TandavaDirectorSettings`' C# defaults | `TandavaSettings.Director` (authored from the C# defaults and asserted) |
| the forms, variants, banks, meals, mouths, halo centres | §2, §3.5 | `TandavaSettings.Forms` |
| the halo, the burst, the palette, the lines, the HUD words | §3.6-3.8 | `TandavaSettings` |
| the shatter, the rings to break | 35%, 9 | `EndConditionOverrides` (`tandavaBreakPercent`, `tandavaHaloRingsToBreak`) |

The generator reads the harness's constants back and fails on any disagreement; the settings' director block is the C#
defaults, in declaration order.

## 6. Ecology invariants (the protocol)

| Law | Holds? |
|---|---|
| continuity of existence | yes: it hatches by blooming, re-sorts between forms, its gold shards fly and fade, the cell's colour eases, a client nudge is small |
| no imposed death | yes: the director cannot kill. Starvation is the swarm's own metabolism; the shatter, the clock and a broken dance are OUTCOMES, and a broken dance molts the figure back |
| mass is conserved | yes: every egg is paid from eaten flora; the seed is the spawn; the gold burst is debris, not mass |
| one colour at birth | yes: one colour, the cell's hostile controller (`MultiDomain` off: a lineage could grow members in the pilots' own colour) |
| shielded mass is never food | yes: the swarm eats through the one edibility predicate |
| the Cell owns the environment | yes: the flora is the cell's own forks, the tint recolours the cell's own visuals, the mode adds nothing to the arena but the halo's switch rings |
| endogenous selection | a stated, user-requested exception: the FORM ORDER is scripted. The swarm still grows only by eating, and a form commits only when its eating has banked it |

Colliders: 16 always-on plant hearts + up to 2 x 160 proxies = 336 worst case, under the 1,200 ceiling (asserted).

## 7. Files

| File | Role |
|---|---|
| `Arcade/Tandava/TandavaDirectorCore.cs` | the director (pure C#; the harness runs it): roam, threat and mood, feeding, evolution, the ascension, the outcome, the variant draw |
| `Arcade/Tandava/TandavaController.cs` | the mode: director glue, replication, the levers on every peer, the halo, the HUD's goal rows, the gold burst and light, the cell's dance glow, AI hunters, game end |
| `Arcade/Tandava/TandavaHaloRing.cs` | one halo ring: a `Halo` switch ring, swept-segment crossing test, dims while guarded |
| `Arcade/Tandava/TandavaGoldBurst.cs` | the gold prism debris burst |
| `Arcade/Tandava/TandavaSettingsSO.cs`, `TandavaObjectiveProvider.cs` | settings; the HUD arrow (points at the creature) |
| `Arcade/Scoring/TandavaScoringRuleSO.cs`, `Arcade/TurnMonitors/TandavaTurnMonitor.cs` | the outcome rule (culls + halo rings); the turn ends on the outcome, and the monitor's tick rebuilds the goal stack |
| `UI/Elements/GoalStack.cs`, `GoalRow.cs` | `IGoalSource`, `GoalEntry.Progress`, `GoalRow.ShowProgress` / `Punch` (platform) |
| `Environment/CellVisualTint.cs` | the cell's colour transition (platform, phase A) |
| `Environment/FloraAndFauna/Swarm/*` | the levers (§3.1) |
| `Tools/Build/tandava_plans.py`, `Tools/Build/author_tandava_assets.py` | the plans; every asset (`--check`; `--self-test` runs the scene checks on the donor scene, where all 16 must fire) |
| `Tools/Build/swarm_core_harness/TandavaHarness.cs` | T1-T15 |
| `Assets/_Scenes/Multiplayer Scenes/MinigameTandava.unity` | a one-shot clone of `MinigameBroodRush`: the controller, monitor, cell and four RANDOM-hull AI templates swapped, the pilots' line at x = 950 facing the hatch. When the donor moves on, the generator keeps the committed scene and still validates it |
| `Assets/_SO_Assets/Cell Configs/Tandava Cell/` | the cell config (the standard membrane), spawn profile, the swarm species and the two dispersed Borromean forks |
| `Assets/_SO_Assets/Games/ArcadeGameTandava.asset` | the arena card: Rhino, Squirrel, Sparrow; 1-6 players seated as 3 (one per hull); one domain |
| `Assets/_Graphics/ARCADE/CardBackgrounds/Tandava.png` | the card backdrop, MODEL tier (`render_card_backgrounds.py`): the dispersed flora as Borromean glyphs, the Great Serpent FEEDING (its own feed-twin plan, its danger plates round its mouth on a plant), pilots striking the body, the cell in its own colours |

## 8. Platform changes (and the rule each records)

- **The director's levers** (`SetLevers`, `RequestPose`, `TurnCarry`, `PlanDanger`, `TryGetSeedForm`). A mode may set
  how fast a population moves, how it turns, whether it lays, and which designed pose it wears; it still cannot move,
  feed or kill a member. Every lever's default is the shipped behaviour, proven by the untouched suites.
- **`IGoalSource` / `GoalStack.Source` / `GoalEntry.Progress`.** A mode whose objective is not a metric to race words
  its own goal rows - the seam `GoalStack.SetGoals` was left for, now live. The metric row is untouched for every other
  mode. *A goal that changes what it IS (a new form) flares; a goal that only moves does not.*
- **`ToySwitchSignal.Halo` (was `Flame`) + `ToyFactory.HaloPearl`.** A verb's colour must read as itself and as no
  domain; the value is kept so nothing serialized against it moves.
- **`SwarmFauna.MembersLost` / `SampleMemberPositions`, `LifeForm.HealthBlockCount`.** Readouts only.
- **`EndConditionOverridesSO.tandavaHaloRingsToBreak`** (`FormerlySerializedAs("tandavaFlamesToBreak")`; the generator
  migrates the asset's key).
- Phase A's `SwarmSortParams.Scripted`, `ISwarmDirector`, `CellVisualTint`, `CellConfigDataSO.CytoplasmShardDistance`
  and `SwarmFauna.CountMembersNear` / `BodyForward / BodyUp / BodySide` stand as they were.

## 9. Next phases

1. **Run it in the editor** (QA-TANDAVA-9..14; the Swarm cell has not run in Unity either - QA-SWARM-* first): the
   feeding intake against real Borromean plants, the bolt against real hulls, the HUD rows and the flare, the gold burst,
   the halo, and how a full match plays against the seven-minute clock.
2. **Tune from the playtest**: the moods' thresholds and speeds, the meal-break share, the banks, the clock.
3. **The tactic chooser**: per-form defences beyond the feed pose (a Many-Headed lunge, the Sea Lion's fin sweep) on the
   same queued-lever seam, each with a visible tell.
4. **Severing**: a cut region split into its own `SwarmFauna` (a decoy or a hunter), within the proxy and collider ceilings.
