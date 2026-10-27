# The concept fleet: ten new movement vessels, round 1

**What this is.** Ten vessel concepts, each a different way of MOVING through the HyperSea, each playable today in the
Vessel Studio (the one artifact, https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa; the hub's "New vessels" row) on a
phone or a PC, with an AI at Easy / Medium / Hard, the three cameras, a scorecard and the shared decision log. The owner
asked for ten (2026-10-17: "ten vessels that will enable me to play ten different gameplays ... I will choose five, and
then finally I will approve three ... I need really good movement vessels"). This file is the design record: what each
one is, why it is here (what players are loving right now, with the evidence), what the lab measured, and what is open.

**How they were built** (the vision's B3, `Docs/Studios/VESSEL_STUDIO_VISION.md`): a vessel is a RECIPE
(`Docs/Studios/VesselStudio/vessels/<id>.json`: its movement kind, its numbers, its four abilities over the four elements,
its play styles, its words), a KIND is one way of moving written once (`studio-kinds.js`), and one shared ENGINE
(`studio-vessel.js`) runs any recipe on any kind as a full studio page. `vessel_pages.py` turns the recipes into the ten
pages, the hub's cards and the catalog entries (`--check` is the drift gate). Nothing here ships: every mechanic number is
a lab-only proposal, shown with its recipe value in each page's Vessel Config > Mechanic. The only shipped numbers are the
game's own Skim Race courses, the course every concept is raced on.

## 0. Building vessels together, asynchronously (the rule for every vessel and supervessel session)

Several sessions build vessels at once, on different branches, for different people. They stay out of each other's
way by reading and writing the SAME records, in this order, at the start and the end of every vessel session:

1. **The id ledger below** (and `VesselClassType.cs` on `bleeding-edge` AND every in-flight branch: `git fetch origin`,
   then `git grep -n "= <id>," origin/<branch> -- Assets/_Scripts/Data/Enums/VesselClassType.cs`). Claim an id by adding
   the enum row AND the ledger row in the same commit, on your branch, before anything else is built on it. Never reuse
   an id another branch claimed, merged or not (enum 14 was taken twice on 2026-10-13: 110 conflicts).
2. **This file**: one section per vessel or crew (the design, what the lab measured, Found, Decisions needed, the Unity
   clone). Add yours; update the status line of a vessel you touched. The report for people is built from it:
   `python3 Tools/Build/team_report.py area concept-fleet --write` (`Docs/Reports/CONCEPT_FLEET.md`).
3. **The Vessel Studio artifact** (https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa): one page per vessel, published
   into the ONE artifact from the repo copy (`/vessel-studio` §4); its Decisions tab is where a person records Keep /
   Drop, and `/artifact-to-unity` reads them. Never a second artifact.
4. **Unity**: a `Tools/Build/author_<vessel>_assets.py` generator that owns the prefab and every registration
   (`--check`, `--self-test`), a design doc beside the code (`R_VesselActions/<VESSEL>.md` or `Crew/<CREW>.md`), the hand
   test in `Docs/UNITY_VERIFICATION_CHECKLIST.md`, `studio_to_unity/clones.json`. Say in the commit who else works there
   (`Affects:`; `python3 Tools/Build/who_works_where.py query <paths>`), and build on their pattern rather than a new one
   (the Flea and the Omega crews are Stoat clones; the crews use the Orca's seat plumbing).
5. **Hand-offs**: a test, a decision or content for someone else goes on Froglet Tasks through `/team-tasks suggest`
   (it waits for the person's approval; never filed on its own, never assigned). One ticket per branch.

### The vessel id ledger (VesselClassType; re-check the branches before claiming)

| Id | Vessel | Where | Since |
|---|---|---|---|
| -1, 0 | Any, Random | meta | |
| 1-17 | Manta, Dolphin, Rhino, Urchin, Grizzly, Squirrel, Serpent, Termite, Falcon, Shrike, Sparrow, Scarab (12), Butterfly, Thresher, Stoat, Brittle Star, Angler | `bleeding-edge` | `Docs/claude/ABOUT_PROJECT.md` |
| 18 | Goby (Goby and Pistol) | `bleeding-edge` | 2026-10-15 |
| 19 | Wrasse (Wrasse and Grouper) | `bleeding-edge` | 2026-10-17 |
| 20 | Orca (4 seats) | `bleeding-edge` | 2026-10-20 |
| 21 | Starling | `bleeding-edge` | 2026-10-21 |
| 22 | Jacks | `bleeding-edge` | 2026-10-21 |
| 23 | Honeyguide (Honeyguide and Ratel) | `bleeding-edge` | 2026-10-21 |
| 24 | Flea | `claude/flea-vessel` (Garrett's branch, unmerged) | 2026-10-24 |
| 25-27 | free again: the Omega crews (Warden, Bulwark, Tempest) claimed them on 2026-10-26 and were removed on 2026-10-27 (§10.5), before any merge to `bleeding-edge` | | |
| **25** | **next free** (the Siphonophore, the Tether / Gibbon, the Dragonfly, the Marlin and the Albatross have none yet) | | |

An id a branch claims and then abandons is released only when that branch is deleted; say so here.

## 1. How to playtest (the owner's round)

1. Open the artifact (phone or PC). Each new vessel has a card in the hub under **New vessels - the concept fleet**.
2. Fly it: gamepad (both sticks steer the way the game does; LT / RT / A / B are the mechanic), keys (W S A D Q E, Space =
   LT, F = RT, G = A, H = B) or the phone's thumbs. The start card names the controls; the Input tab repeats them.
3. Watch its AI: Game Config > Players, **+** adds an AI rival (its own level and play style); **View** follows it; Scene
   Config picks Chase / Follow / Free. AI Config > **Show AI thinking** draws where it steers (violet while it works the
   mechanic).
4. Tune it: Vessel Config > Mechanic (every number, outlined when changed), Play Style Config (named styles over the four
   element levels). Scorecard > Run races Easy / Medium / Hard and a **no-mechanic control** on all four courses.
5. **Decide**: Decisions > Record (Keep / Drop / Feel / Numbers ...). The log is shared; the next session reads it first.
   Course mass (Scene Config) is the stand-in for a cell's flora: many of these vessels need mass near them; turn the
   density up or down and switch the ribbon off to see a bare course.

The ask is: pick five, then three. Record "Keep" or "Drop" on each page, with a line of why.

## 2. The ten

Each row: the fantasy, the mechanic (controls), the four elements, and the research reason. Full recipe: `vessels/<id>.json`.

| # | Vessel | Kind | The mechanic (controls) | Elements (Charge / Mass / Space / Time) | Why now (evidence, `research` below) |
|---|---|---|---|---|---|
| 1 | **Gibbon** | swing | RT tethers to the best prism in reach (150 u, 60 deg cone, 0.5 s target memory) and the hull pendulums round it; LT reels in, trading rope for speed; release flings out, x1.3 after a 70 deg swing | Fling kick / Heft (shorter rope; L5 Harvest converts the anchor) / Long Arm (reach) / Quick Reel | Grapple and swing: Spider-Man 2 (11M+), Marvel Rivals' swingers, Echo Point Nova (98%); forgiveness rules from EPN |
| 2 | **Flea** | ricochet | Crawls at 50; a hull contact REFLECTS it off the prism's axis instead of resetting; RT inside a 180 ms window round the contact = perfect bounce x1.35 and a chain; LT crouch, release to hop | Spring (perfect gain) / Momentum (cap) / Big Hop / Wide Window | Earned, protected speed (ULTRAKILL 97% on 132k reviews; Haste's perfect landing) and the timing window with a tell (Expedition 33's parry) |
| 3 | **Albatross** | soar | No engine: its own ballast (g 10) makes it fall, wings turn speed into lift, an updraft rises over every prism (the ribbon is a thermal corridor); LT spreads (lift + drag), RT folds (dive) | Thermal Sense / Ballast / Wingspan / Clean Glide | Dive-to-glide (Tears of the Kingdom 20.6M, Sky 300M downloads, Exo One); nothing in the fleet is an energy vessel |
| 4 | **Remora** | slipstream | Alone 55 u/s; in a rival's wake the cap is their speed x1.3; LT latches within 60 u behind a rival (towed, draft meter, petals at Mass); release / RT slings x1.7 their speed + the draft | Sling / Leech (petals per second towed; PvP is petals only) / Latch Reach / Deep Draft | Racing's draft: new skill verbs win (Mario Kart World 14M on grind and wall-ride; CrossWorlds missed at 84 Metacritic) |
| 5 | **Dragonfly** | hover | Velocity has nothing to do with the nose: left stick thrusts any way, right stick looks, A / B climb / sink, LT stops dead; RT dashes 190 u/s for 0.25 s with i-frames, three pips that regrow | Thrust / Anchor (brake) / Long Dash / Quick Pips | Stamina dash with i-frames is 2025's most-shipped movement feature (Hades II 95, Silksong 7M, ULTRAKILL's pips); the fleet has no strafer |
| 6 | **Mole** | burrow | Slow (40) and clumsy in the open; within 16 u of any prism it is fast (150), turns 220 deg/s and passes through; LT digs a tunnel of its own mass; RT as it leaves mass = a timed breach x1.4 | Breach / Wide Tunnel / Sense (in-mass reach) / Burrow Speed | "The wall is the fast way" (Titanfall's 2x rule, MKW's wall-ride meta) on the game's own conserved prisms; the Urchin rides ON, the Mole goes THROUGH |
| 7 | **Marlin** | momentum | The nose is free (180 deg/s) while the velocity persists; RT burns along the nose, LT retro-burns, B flips 180 deg in 0.3 s; a drift assist (1.2/s) bends the velocity toward the nose, 0 = pure Newtonian | Main Burn / Momentum (cap) / Retro / Agility | Arcade flight wants tight, drifting, momentum-preserving ships, not sims (Squadrons; Chorus's Drift Trance; Jett panned for fiddly controls) |
| 8 | **Eel** | body | A head leading a train of segments on its exact path: the body is its trail, shielded mass a rival bounces off; longer = slower (72 at 8, 45 at 30); LT coils (turn x2.2, body bunches), release whips the tail straight and cuts; RT sheds five segments for a burst | Shed Burst / Girth / Whip Reach / Tight Coil | Snake and trail games are still mass-market on mobile (Slither, Snake.io, Hole.io in 2025's top ten); cutting someone off is legible |
| 9 | **Octopus** | pulse | LT inhales (slows, charges in 0.7 s), release jets 55-205 u/s, fading to a 20 u/s drift; a release at the same interval as the last two chains x1.1 each, to x1.8; RT spends a pulse on an ink cloud of danger prisms | Jet / Ink / Wide Beat / Quick Breath | Charge-and-release (Kirby Air Riders, MKW's charge jump) with an optional rhythm layer you set yourself (Hi-Fi Rush 2M+) |
| 10 | **Gecko** | surface | LT near any mass sticks and runs along the structure (the chain of prisms guides the course, speed climbs to 130, a rival's rail it runs becomes yours); release / RT hops off x1.4 with 3 s of carried momentum | Hop / Claim (converted rails grow) / Grip Reach / Run Speed | Wall-running (Titanfall 2, Ghostrunner 2.5M, MKW): geometry into speed, chained once the wall is trusted |

Three ideas were weighed and left out, with the reason: a **swarm** vessel (a flock you contract and spread: no 2024-26
hit uses swarm control; it may be the right design for the never-built Termite, "send out drones"); a **portal** vessel
(the Butterfly's fold already is one, and Splitgate 2 is the cautionary tale of diluting it); a **slingshot** (the Stoat).
Nearest shipped relatives, so nothing here duplicates the fleet: the Thresher's chain (Gibbon), the Urchin's rail grind
(Gecko, Mole), the Dolphin's and Squirrel's drifts (Marlin), the Scarab's juke (Dragonfly), the Stoat's sling, the
Shrike design's Void Sling (a zip to a block, not a swing). The fleet's own movement inventory is in the `/vessel-studio`
skill's fleet sweep (2026-10-17) and `Docs/claude/ABOUT_PROJECT.md`.

## 3. The research behind the picks (October 2026)

Web research for this round (sources at the end of this section), ranked by heat and fit for a two-stick, two-trigger
3D arcade flyer:

1. **Momentum you earn and visibly protect** (ULTRAKILL 97% positive on 132k reviews, 70k CCU on a 2026 free update;
   Haste; Echo Point Nova 98%; Neon White): speed from chaining flowing actions, lost by abrupt ones. -> Flea, Gecko, Gibbon.
2. **Grapple / tether / swing** (Spider-Man 2 11M official; Marvel Rivals 40M, its most-loved heroes swing; Echo Point
   Nova's target memory, 1 s velocity blend and auto-release; GDC 2026 "Grappling with Success"). -> Gibbon.
3. **Drift in 3D** (Chorus's Drift Trance "car drifting in three dimensions and zero gravity, feels incredible"; Mario
   Kart World 14M; Kirby Air Riders' one-button brake-drift-charge). -> Marlin (the Squirrel and Dolphin already drift).
4. **Charge-and-release with a visible gauge** (Kirby Air Riders, MKW's charge jump). -> Octopus.
5. **Gliding / dive-to-glide** (Tears of the Kingdom 20.6M, Sky 300M downloads, Spider-Man 2's web wings, Exo One). -> Albatross.
6. **Stamina-gated dash with i-frames** (Hades II Metacritic 95, 113k Steam peak; Silksong 7M in three months). -> Dragonfly.
7. **Rail-grind / wall-ride / wall-run** (MKW's meta; Titanfall 2's "2x sprint speed on the wall"; Ghostrunner 2.5M). -> Gecko, Mole.
8. **Snake / trail / territory** (Hole.io in AppMagic's 2025 global top ten; hypercasual the only mobile segment still
   growing downloads, Sensor Tower). -> Eel.
9. **Racing: new skill verbs beat more tracks** (MKW vs CrossWorlds). -> Remora (the draft).
10. **Rhythm-synced movement** (Hi-Fi Rush 2M+, a sequel in development): a devoted audience, no 2025 breakout, so it is a
    LAYER here (the Octopus's own tempo), never a gate.

What players want in general, and how the fleet answers it: two inputs with physics depth (Rocket League's rule: force-based
inputs, mastery is timing; every concept here is a force or a window on a trigger); forgiveness that is invisible (the
Gibbon's target memory, the Flea's coyote press); speed sold by camera and audio (the hull banks onto the Gecko's surface,
the Octopus's beat ring; sound is the Unity port's job); 3-8 minute rounds (a Skim Race course); small groups of 2-4
(the Remora is nothing without rivals). Arcade flight wants tight, drifting ships, not sims; the Marlin's assist dial is
there for that reason, and Jett's reception is the warning.

Sources: Steam Best of 2025 (fullcleared.com); Newzoo 2025 market update (gamesbeat.com); Sensor Tower State of Gaming
2026; AppMagic's most-downloaded 2025 (pocketgamer.biz); GDC 2026 State of the Industry and "Grappling with Success:
Smooth Movement on an Indie Budget" (gdcvault.com); Game Developer's Echo Point Nova and Titanfall controls deep dives;
Psyonix's rocket-jumping deep dive; the Celeste game-feel thread (gamespot.com); PC Gamer, "2026 is a low-key
revolutionary year for player movement"; Shacknews on Mario Kart World 14M and Kirby Air Riders 1.76M; Nintendo Life on
CrossWorlds below expectations; Dexerto on Marvel Rivals; Gematsu on Spider-Man 2; PCGamesN and Notebookcheck on
Splitgate 2; NME on Chorus; PC Gamer on Squadrons; Slant on Jett; the Hades II, Silksong, Expedition 33 and ULTRAKILL
figures as cited in the `/vessel-studio` research sweep of 2026-10-17.

## 4. What the lab measured (round 1)

The engine's scorecard races the studio's AI alone (the Remora with two Hard rivals) on each Skim Race course, three seeds,
at Easy / Medium / Hard, plus **the Hard pilot forbidden its mechanic** ("No mech"), so the table can say NO: a mechanic
that does not beat its own control is decoration. "Mech %" is the share of Hard's race spent in the mechanic. Course I1
is the flat octagon, 3 laps, 24 crystals; the Squirrel's shipped pilot runs it in 64 s, the Squirrel studio's in 69 s.
Finish limit 540 s (DNF beyond it). Measured headless, seed 11, 2026-10-17 (`smoke.cjs`, the engine's `raceHeadless`):

| Vessel | I1 Hard | I1 Easy | I1 No mech | Mech % | Top u/s | Reading |
|---|---|---|---|---|---|---|
| Gibbon | 159 | 161 | 161 | 2% | 198 | On the Tether's numbers (§8.1) the AI barely swings on the flat octagon (2% of the race) and ties its own control; before the merge, on the recipe's guesses, turn-swings paid a little (217 against 232) and straight-line swings lost (266). The mechanic is for a human to judge |
| Flea | 294 | 326 | 326 | 56% | 258 | Bounces pay (+10% over the control) once the skim and the hull kept separate cooldowns; the chain is the skill |
| Albatross | 266 | 266 | DNF | 100% | 144 | Finishes I1 once its physics became a glider's: lift perpendicular to the flight path and following the angle of attack (lift along hull-up put ~90 u/s² against the velocity in a pull-up), a glide ratio of 8 (it was 1.6, a brick), ballast 25 so a dive is a 3 s plunge, an updraft weaker than the ballast at neutral wings. The AI's dive is an ATTITUDE held by the aim controller (a held stick is a pitch rate, and it looped). Its control (no wings, no updraft) cannot fly at all, like the Marlin's. Still DNF on I2-I4: the climbing courses need an energy plan the round-1 pilot does not have. The weakest AI of the ten; fly it yourself |
| Remora | 80 | 168 | 222 | 92% | 240 | The draft is enormous; **found:** two Remoras leapfrog each other's wakes up to the cap in 40 s (a slipstream chain). A design question |
| Dragonfly | 156 | 150 | 240 | 20% | 190 | Dashes pay; Easy and Hard within noise (Hard dashes more and hits more) |
| Mole | 63 | 63 | 351 | 87% | 217 | The ribbon is mass, so the Mole is a 150 u/s hull on any Skim Race course; switch the ribbon off (Scene Config) to test a bare course |
| Marlin | 147 | 149 | DNF | 55% | 121 | The AI thrusts in bursts and drifts the corners; the control cannot move at all (the mechanic IS the engine) |
| Eel | 201 | 201 | 280 | 21% | 79 | Coil and shed pay; shed segments now fall to the sides, after the AI hit its own wall 120 times a race |
| Octopus | 94 | DNF | DNF | 100% | 254 | Hard keeps the beat and flies; Easy's late presses break the chain and it crawls. The sharpest difficulty ladder of the ten |
| Gecko | 160 | 197 | 262 | 28% | 200 | Runs and hops pay; a stationary loop past a segment's end was found and fixed |

The four-course table (I1-I4) is section 4.1. Read everything as relative, between a vessel's own columns: the AI is a
line-following pilot plus one rule per mechanic, not a player.

**The difficulty ladder is not clean yet.** D24 asks for Hard fastest on every course. The shared belief mistakes (late
notice, misjudged crystal, from `SkimRaceDifficulty.asset`) cost these fast, tight-turning movers little, and the lab-only
press delay (Easy 0.2 s, Medium 0.1 s) cuts the other way on tempo mechanics (the Octopus). This is a round-1 lab for
FEEL; the AI ladders get their round once the five are picked.

### 4.1 Four courses

Finish time in seconds as **Hard / Easy / No mech** per course, finish limit 540 s, seed 11, 2026-10-17 (`smoke.cjs
--all-courses` on the committed engine; the Albatross row from its final tuning run). I1 is the flat octagon (3 laps, 24
crystals), I2 the longer flat course (30), I3 the long climbing course (56), I4 the twisting course (54). Each page's
Scorecard tab re-runs its own row live.

| Vessel | I1 | I2 | I3 | I4 | Top u/s | Reading |
|---|---|---|---|---|---|---|
| Gibbon | DNF / DNF / DNF | DNF / DNF / DNF | DNF / DNF / DNF | DNF / DNF / DNF | 260 | Round 2.2, the web-slinger (§8.3): no engine, so its control cannot move at all, and the AI moves (32-41 u/s, up to 4 crystals) but finishes nothing yet; fly it yourself. Round 2.1 (§8.2, two modes): 73 / 85 / 79, 136 / 127 / 111, 235 / 293 / 254, 182 / 177 / 123 |
| Flea | 294 / 326 / 326 | 388 / 458 / 458 | DNF / DNF / DNF | 496 / 475 / 475 | 258 | The bounce chain pays 10-15% on the flat courses, loses on I4; the AI never hits the rail (0 resets) |
| Albatross | 266 / 266 / DNF | DNF / DNF / DNF | DNF / DNF / DNF | DNF / DNF / DNF | 144 | Finishes the flat octagon only; the climbing and twisting courses need an energy plan the round-1 pilot lacks. Fly it yourself |
| Remora | 80 / 168 / 222 | 118 / 256 / 297 | 394 / DNF / DNF | 260 / 264 / 282 | 240 | Two Hard rivals to draft (its scorecard seats them); the draft is worth 2-3x on the flat courses. The slipstream chain finding stands |
| Dragonfly | 156 / 150 / 240 | 200 / 202 / 318 | 513 / 506 / DNF | 218 / 215 / 318 | 190 | Dashes pay about a third everywhere; Easy and Hard within noise (no ladder yet) |
| Mole | 63 / 63 / 351 | 117 / 118 / 485 | 233 / 261 / DNF | 83 / 130 / 458 | 220 | The ribbon is mass, so it burrows the whole course; a bare course (ribbon off) is the real test |
| Marlin | 147 / 149 / DNF | 194 / 200 / DNF | 479 / 483 / DNF | 216 / 231 / DNF | 140 | The mechanic is the engine, so the control cannot move; finishes every course |
| Eel | 201 / 201 / 280 | 272 / 274 / 382 | DNF / DNF / DNF | 266 / 274 / 374 | 81 | Coil and shed pay about 30%; the slowest top speed of the ten by design, and the long course beats it |
| Octopus | 94 / DNF / DNF | 214 / 145 / DNF | 319 / DNF / DNF | DNF / DNF / DNF | 254 | Hard keeps the beat; Easy's late presses break the chain (I2 is the one course where Easy beat Hard: a seed effect to read with more seeds). The twisting course breaks even Hard's rhythm |
| Gecko | 160 / 197 / 262 | 226 / 267 / 354 | 475 / DNF / DNF | 313 / 337 / 346 | 207 | Runs and hops pay 10-40%; the cleanest Easy-to-Hard ladder after the Octopus |

**What the table says about round 1 as a whole.** Seven of the ten AIs finish three or four courses; the long climbing
course I3 beats five of them inside the 540 s limit, which is a pilot limit (a line follower that resets on the rail), not a
vessel verdict. Every mechanic but the Gibbon's and the Flea's on I4 beats its own control where the control can fly at all.
The Easy / Hard ladder is clean only for the Gecko, the Gibbon, the Flea and the Octopus; the Remora's is inverted by its
rivals (Easy drafts less), and the rest sit within noise (section 4's note on the ladder).

## 5. Found (bugs and design holes the lab exposed)

- **One cooldown for the skim and the hull hid every hull contact.** The platform baseline (the Squirrel studio's) cooled
  a prism for 0.6 s on a skim, 12 u out, so the hull never registered reaching it. The Flea never bounced; every other
  concept's contact count was zero for the same reason. The engine keeps two cooldowns now (`p.cool` skim, `p.hull`).
  Worth checking in the game's `SkimmerImpactor` / `VesselImpactor` pair, which are separate components there.
- **A chain walker needs "ahead" and "not where I came from"**, or a cluster is a treadmill (the Gecko ran in place for
  60 s, pulled back by its own clearance past a segment's end).
- **Spread wings are lift first and drag second**: an airbrake rule that spread on a fast approach looped the Albatross.
- **Slipstream compounds**: two drafters ratchet each other to the cap. Decide whether a draft should be relative to the
  host's UNASSISTED speed.
- **Hull-level auto-roll needs the sign checked**: the Dragonfly's self-levelling doubled its roll until the sign flipped.
- **A swing on a straight is a detour** on a gentle course; swings pay on corners and against a human's judgement.

## 6. Decisions needed (designer)

1. Which five, then which three (record Keep / Drop on each page).
2. The Albatross's ballast: a vessel that falls is a fundamentals question (the HyperSea has no gravity).
3. The Remora's slipstream chain (compounding drafts), and whether a tow may take petals at all (PvP is petals only, so it may).
4. The Dragonfly's and Marlin's input: both need a second input strategy (left stick = translation / RT = throttle), the
   first since the one-thumb hulls.
5. The Eel's body as a moving prismscape: the mass pipeline has no mover but the Ark.
6. Names (the owner's; the animals are placeholders), and which one becomes the Termite (the fleet's unbuilt slot).

## 7. Files

| File | What |
|---|---|
| `Docs/Studios/VesselStudio/vessels/<id>.json` | The ten recipes (the source) |
| `Docs/Studios/VesselStudio/studio-kinds.js` | The ten movement kinds: physics, contact, AI rule, HUD meters, hull, visuals, hub preview |
| `Docs/Studios/VesselStudio/studio-vessel.js` | The generic studio engine (D40) |
| `.claude/skills/vessel-studio/vessel_page_template.html` | The page skeleton every recipe is poured into |
| `.claude/skills/vessel-studio/vessel_pages.py` | The generator (`--check`, `--self-test`): pages, hub bays, previews, catalog |
| `Docs/Studios/VesselStudio/<id>.html` | The generated pages (never edited by hand) |

Gates run 2026-10-17: `vessel_pages.py --check`, `check_studio.cjs` on every page (desktop, 400 px, phone), `verify_lab.cjs`
(the `__lab` contract: SPEC/SHIPPED, manual clock, deterministic `runBatch`), `parity_gate.py`, `game_catalog.py --check`,
`domain_colors.py --check`, `bake_previews.cjs`, and the headless race smoke (every vessel, every level, with and without
its mechanic, four courses).

## 8. Round 2: one vessel per session (the owner, 2026-10-17)

Round 1 made ten vessels in one session so the owner could pick. Round 2 takes **one vessel per session**, in the
order Garrett's review set (§9: Gibbon, Flea, Dragonfly, Marlin, Albatross), each session on `Ys-bleeding-edge`, each ending with a vessel the owner has flown and recorded a
decision on. A round-2 session, in order:

1. **Read first**: this file (the vessel's row in §2 and §4.1, §5, §6), its recipe `vessels/<id>.json`, its kind in
   `studio-kinds.js`, the live decision log (`ArtifactData list decisions`, filtered by `vessel`), and
   `python3 Tools/Build/who_works_where.py query` on the files it will touch. Say who else works there.
2. **The studio round(s)**: refine the kind and recipe until the mechanic reads on a phone and a PC: the AI finishes every
   course it is meant to, Hard is fastest (D24), the mechanic beats its own no-mechanic control (L-GEN-15), and the HUD
   says what the triggers do. Every round: `vessel_pages.py` → `check_studio.cjs <id>.html --hook __vesselStudio` →
   the headless smoke on all four courses → `build_artifact.py` → publish into the one artifact → `amoebius_artifacts.py
   import` → `parity_gate.py --artifact`. Write the measured table into §4.1's row and the round's Found into §5.
3. **The owner flies it** and records **Keep / Drop** (and what to change) on the page's Decisions tab. The session
   applies the recorded decisions at the start of its next round and says "your decisions applied".
4. **Kept → the game**: `/artifact-to-unity` ports the settled numbers and the mechanic, line for line with tests, under
   `/vessel`'s contract (four abilities over the four elements are proposals until Garrett signs them off, §11 of each
   design doc). A vessel id is claimed only after checking `bleeding-edge` AND the in-flight branches.
5. **Ship**: `/ship` (§2.5 and §3.55 never skipped), "Affects: Yash's Vessel Studio" in the commit, the team tasks the
   session leaves on `Docs/TEAM_TASKS.md`.

What round 1 leaves every vessel's session (from §4-§6): a clean Easy / Medium / Hard ladder, the I3 climbing course
(five AIs DNF inside 540 s), a human-facing name, and the design decisions in §6 that belong to that vessel.

### 8.1 Gibbon + Tether: one concept, two halves already built

`cece/loving-feynman-hlgf18` (a Claude Code session, 2026-10-08, merged mostly by Garrett) carries the **Tether**
prototype in Unity: `VesselClassType.Tether`, a Squirrel-hull clone whose long tether is the Gibbon's swing in the
real flight model, with pure maths (`TetherMath.cs`, 12 edit-mode tests, a headless runner), a config asset
(`TetherConfig.asset`) and its design doc `Assets/_Scripts/Controller/Vessel/R_VesselActions/TETHER.md`. The two
concepts are the same vessel seen from two sides, so round 2 **merges them**: the Gibbon is the studio side (where the
owner flies and decides), the Tether is the Unity side (where the decided numbers land), and from now on they share one
set of numbers and one design record (this section, and TETHER.md for the engine detail).

| | Gibbon (studio, round 1) | Tether (Unity prototype) | Merged |
|---|---|---|---|
| Swing | RT tether to the best prism, reel with LT, release flings; kick ×1.3 past 70° | LT / RT each hook the best target on that side; stick up reels, down pays out; +20% past a half turn | **The Tether's**: two triggers, one per side; reel on the stick; the half-turn boost |
| Rope | point constraint, no gravity; reel trades rope for speed, cap 170 u/s | rigid rope, angular momentum conserved; the reel stops at the **spin limit** 0.6 rev/s; cap 358 u/s | the Tether's rope and spin limit (the studio kind gains both) |
| Aiming | 60° cone ahead, target memory 0.5 s | the **search plane** (forward × right; roll tilts it); ghost rings; a held trigger hooks the moment something is in reach | the Tether's plane and ghosts, the Gibbon's 0.5 s memory kept |
| Straight flight | plain cruise 55 u/s | **auto-anchors**: it plants its own prisms 0.8 s ahead, 20° off the nose, alternating sides every 0.35 s, and yanks itself along them hand over hand at 1.15× cruise | the Tether's auto-anchors (new in the studio; the owner's top tuning question) |
| The tether itself | inert | a **light sword**: cuts hostile prisms it sweeps, pops shields, binds on super-shields (the Rhino sword's rules) | the Tether's blade |
| Numbers | the recipe's guesses | `TetherConfig.asset`: cruise 80, range 93, minHook 27, reel 40 + auto-reel 7.4, minLength 19, maxSpin 0.6, maxSpeed 358, releaseBoost 0.2, glide 1.6 s × 0.15 | **the asset's** (D1: the studio reads the shipped numbers); the recipe's `params` now carry them where the round-1 kind has the knob |
| Elements | Charge Fling · Mass Heft (L5 Harvest) · Space Long Arm · Time Quick Reel | four OPEN slots; proposals Space reach, Time swing rate, Mass anchor size, Charge the blade | the two proposals agree on Space and Time; Charge and Mass stay for Garrett (TETHER.md §11, the design-approval gate) |
| Measured | I1 Hard 217 s vs 232 control (turn-swings only) | one swing from cruise: 1× → 2.1× (2.4× with the boost) in 1.4 s, hooked at 93 u | both stand; the studio re-measures after the merge |

**The Gibbon session's first steps**, in order: (1) the vessel id: `Tether = 14` on that branch collides with
`Thresher = 14` on `Ys-bleeding-edge`; the Tether takes the next free value after checking every in-flight branch
(17 went to the Angler and the Kite's branch claims 19: 18 as of 2026-10-18; the Goby then took 18 and 19-23 are taken: 24 as of 2026-10-24, then the Flea claimed 24 on `claude/flea-vessel` the same day: **25**, re-check before claiming), and TETHER.md §12's id note is rewritten; (2) rebase or merge `cece/loving-feynman-hlgf18`
onto `Ys-bleeding-edge` (its `VesselTransformer` seams default to the expressions they replaced, so the fleet is
bit-identical; `check_network_prefab_hashes.py` on the merge); (3) teach the studio's `swing` kind the Tether's three
additions, auto-anchors, the spin limit, the blade, and the search plane, each as a `TetherConfig` knob by the asset's
field name; (4) re-run §4.1's row and §2's reading; (5) when the owner records Keep, `/artifact-to-unity` writes the
decided values into `TetherConfig.asset`, never into the C#. The AI note stands from both sides: nothing drives the
long tether in Unity yet, and the studio's pilot swings for turns only.

## 9. Garrett's review (2026-10-17, The Lounge; Garrett, Yash, Joseph Zhou)

Garrett reviewed the ten in the artifact. His verdicts decide §6's first question and set round 2's order. **Qualitative
first**: "fix the qualitative before you fiddle with the quantitative"; he declined the parameter panels. Recorded in the
artifact's decision log (`decisions`, `by: Garrett`) so each vessel's session reads them on open.

| Vessel | Verdict | What Garrett asked for |
|---|---|---|
| **Gibbon / Tether** (the notes spell it "Given": the transcriber's spelling of "Gibbon", the page Garrett was reviewing) | **Build next**, from `cece/loving-feynman-hlgf18` | Two modes. **Time mode**: feels like flying, is the branch's auto-tethers (Spider-Man swinging off the blocks its tethers plant). **Space mode**: the slicing mode; two sticks + two triggers, one pair per tether; slicing is what the two tethers cut through; movement differs with 0 / 1 / 2 tethers attached (both held = two, one held = one, none = flinging through space until back in time mode). Charge and mass modes later. The transition time ↔ space must feel natural before anything else is added |
| **Flea** | **Keep**: "seems really good", no feedback | Give Garrett a branch to fly it. If the artifact version is good, bring it into the game (out of the artifact, playable), make a small racing game for it, push to bleeding-edge as the Unity version |
| **Albatross** | **Keep, rework the frame** | Up/down from the **nearest point on the nucleus** (not the membrane), in the cytoplasm and in the nucleus: toward the nucleus is diving, away is falling. **Small thermals** with strong lift that shoot it away from the nucleus and make a large explosion. Hitting the membrane has a consequence (maybe mass just inside it; incentive undecided). Crossing the membrane is not free spam: the longer you avoid crossing, the bigger the mass explosion when you do, growing faster than linear ("a very shallow exponential"). Feel before numbers |
| **Dragonfly** | **Keep**: "I like the name, what it does and how it flies" | Drop the stop function. **Control swap**: holding LT turns the look stick from pitch/yaw to roll/elevation, so A/B go. Solid baseline throttle; boost on a **long cooldown**, fired along the current motion, **stored bursts** like the Serpent's (3 or 5 banked). The precision needs a reason: a **grasping joust** (latch onto a vessel, bite, steal its element: "definitely a jouster") and a **precision mine kit**: mines placed exactly while hovering, hands off the sticks, whose geometry decides the effect (equilateral triangle blows up its inside; square makes mass inside; kite / pentagon / rhombus for mass / charge / space / time). "Should definitely be a miner of some sort" |
| **Marlin** | **Keep**: "I definitely want this character" | A pure 3D drifter, "all drift all the time". Tune for fun, not real. **Never let him get grip**: for the others drifting is part of the kit, here drifting IS the kit; the rest of the kit must support that identity. Less drift assist is more fun (lean in) |
| **Octopus (pulse)** | **Not a vessel**: a mode or a piece of a kit | Fun for seconds, repetitive as a whole kit. Use the pulse as a weapon the player steers briefly, a temporary mode on a long cooldown, or one piece of a kit. Watch for mechanics of this scope |
| **Gecko (trail runner)** | **Keep the mechanic** (read, not flown) | More trail runners besides the Urchin: this becomes a gecko or merges into another trail runner in development, and may update the Urchin so it sucks itself into the trail too |
| **Remora (draft)** | **Put down** | Not a failure, not a priority, "not that interesting" beside the stronger ideas |
| **Mole (burrow)** | **Put down**: "not interesting yet" | Could look good (mass parting round the hull and reforming behind), but too close to the Urchin; could be mixed into the Urchin |
| **Eel (body)** | **Put down** | Overlaps other vessels; coil-and-release is what the Thresher does better |

Other decisions from the meeting: Yash reviews Joseph Zhou's pull request when it lands (approval required before
merge); Garrett reviews the next R&D round with Yash after the next iteration.

**Round 2's order (§8), as decided:** 1 Gibbon (+ the Tether, from `loving-feynman`), 2 Flea (to the game), 3
Dragonfly, 4 Marlin, 5 Albatross; Gecko's mechanic folds into a trail-runner session; the Octopus pulse waits for a kit
that wants a mode; Remora, Mole and Eel are archived (pages stay in the hub, marked archived, for the record). §6's
questions 1 and 2 are answered above; 3 and 5 fall with their vessels; 4 (the Dragonfly's second input strategy) is
now the control swap; 6 (names) is open (the notes' "Given" is the Gibbon, misheard).

### 8.2 Session 1: the Gibbon, two modes (2026-10-18)

Built from Garrett's review (§9) on the Tether prototype (§8.1). The Gibbon's kind is now `tether` in `studio-kinds.js`
(round 1's `swing` stays in the file for the record); the recipe reads `TetherConfig.asset`'s numbers.

**What it is.** **Time mode** (no trigger): the sticks fly; every 0.35 s an anchor prism is planted 0.8 s ahead, 20°
off the nose, alternating sides, and a short elastic tether yanks the hull along it (AutoTetherRig and TetherMath ported
function for function: the plant cadence that drops owed time, the spring that pulls only past rest and damps only
stretching, the abeam release, the 1.15× cruise cap). **Space mode** (a trigger held): each trigger owns one tether and
its stick owns that tether. The press hooks what the trigger's ghost ring showed; after that the stick aims an unhooked
tether and reels a hooked one (up in, down out). **One tether** is the Tether's swing (rigid rope, v·L kept, the spin
limit stops a reel's gain, +20% past a half turn). **Two tethers** are a bolas: the hull turns about the line between
the anchors, exactly (a rotation, so both ropes stay rigid). **No trigger after a swing** is the fling: ballistic, the
sticks only aim, and after 1.6 s (TetherConfig.glide) time mode eases the sticks back to steering over 0.5 s (`handBack`,
lab-only; 0 = a snap). Squeezing during the fling chains. **Every live tether cuts**: hostile prisms it sweeps are cut,
a shield pops and survives that contact, own and own-domain prisms, the hooked anchor and the course ribbon never.

**Measured** (`probe_given.cjs`, free space far from the course, and `smoke.cjs --all-courses`):

| | Studio | TETHER.md |
|---|---|---|
| Time mode, hands off 6 s | 92 u/s = 1.150× cruise, heading ≤ 0.54°, 18 anchors | 1.10-1.15×, ≤ 0.63° |
| Its control (no auto-tethers) | 80 u/s flat | |
| One tether, hooked ~40-60 u, full reel | 87 → 140 u/s in 0.5 s, held there by the spin limit (0.6 rev/s at 37 u) | 60 u: 1.68× at 0.5 s |
| Release past a half turn | 140 → 164 (+17%: the boost shrinks toward the 358 cap) | +20% × (1 − (v/cap)²) |
| Fling → time mode | 1.60 s, then the anchors resume | |
| Bolas | rope error 0.000 u, two turns in 3 s with the auto-reel only | (new) |
| Blade | hostile cut; shielded: shield popped, survived; own-domain and the anchor intact | the Rhino sword's rules |
| Race, Hard / Easy / No mech | I1 73 / 85 / 79 · I2 136 / 127 / 111 · I3 235 / 293 / 254 · I4 182 / 177 / 123 | |

**Found.**
- **A mode switch must not read the input of the mode it is leaving.** In space mode a stick aims its tether, but at the
  press the thumbs are still steering, so the first version hooked wherever the steering had pushed the aim (a held
  pitch-up aimed 45° up and hooked nothing). The press now hooks what the ring showed, and the stick aims only from the
  next frame.
- **Two rigid ropes cannot be solved one rope at a time.** Projecting the velocity onto each rope's sphere in turn stalled
  the bolas (1° swept in 3 s): a hull flying between two anchors ahead has almost no velocity along the circle the two
  ropes allow. Solved as a rotation about the anchors' line, with the speed redirected onto the circle (hooking never
  costs speed; a hull flying straight at the axis turns over its own top).
- **The studio's neutral throttle was half the Tether's cruise** (the engine's convention puts neutral at half
  `cruise`): the Gibbon now maps throttle as Tether.prefab does (20 pinched, 80 neutral, 140 flat out).
- **An AI that hooks on every turn lives in space mode.** The first rule (any turn over 25°) spent 96% of I1 swinging
  and DNF'd: a fling at the cap cannot steer and carries the hull 500 u past its turn. The AI now swings only from time
  mode with the sticks back, on turns over 35°, under 1.8× cruise, and lets go leading by the swing's turn rate.

**Decisions needed (Garrett, by flying it).**
1. Does time → space → fling → time feel natural? The knobs that decide it: `glide` (fling time), `handBack` (how the
   sticks come back), the ring opacity in time mode (what a press will hook).
2. Should the fling steer? The race says an unsteerable 1.6 s fling is expensive on twisting courses (I2, I4).
3. The stick aim in space mode is the studio's proposal (neutral = ahead and out on its own side); the Tether aims with a
   search plane that roll tilts. Which one?
4. What should the bolas do? It is here because Garrett said movement differs with two tethers attached; its
   behaviour (an orbit about the anchors' line) is the studio's guess.
5. Charge and mass modes: later (Garrett). The element rows are proposals.

**Not done this session:** the Tether branch is not merged into `Ys-bleeding-edge` (its id 14 collides with the
Thresher; T-20261017-e64b), so nothing reached Unity; the AI never holds two tethers.

### 8.3 Session 1, second pass: the Gibbon as a web-slinger (2026-10-18)

Yash flew §8.2 and redirected it: "like Spider-Man. Remove the auto moving: it should never auto move. No swing like that.
Make it stationary; it should move only when it shoots a web. Exactly how a Spider-Man web works, just without gravity.
Latch on to whatever we find: prisms, crystals. The effect wavy, like spindles. Very smooth, very elastic and springy."
(The "Given" in the notes and in this request is the Gibbon.) The Gibbon's kind is now `web`; `tether` (§8.2) and `swing`
(round 1) stay in `studio-kinds.js` for the record.

**What it is.** No engine and no throttle: it starts at rest and the sticks only turn it (they aim). LT shoots the left
hand's web, RT the right's, at the best thing near the crosshair (any prism, any crystal; each hand prefers its own side,
long webs preferred, a 22° cone as the Spider-Man games give). A web flies out at 900 u/s and latches. It is an **elastic
bungee**: it pulls only past its rest length (born at 75% of the distance, so it yanks at once), the rest length reels in
at 70 u/s while the trigger is held, and the damping is light and acts on stretching only, so the hull overshoots and
bounces. A spring is a central pull, so a web latched off your line of travel makes you **swing round the anchor and spin
up** as it shortens (angular momentum is kept by the physics, not scripted). Arriving at the anchor, a zip **perches with a
bounce**: it brakes from about 0.15 s out and its closing speed reflects at 50% at the shortest web (10 u), so it bobs a
few times and settles. Let go and you fly on your momentum (space drag 0.12/s lets a coasting hull come to rest; 0 is true
space). A wall bounces you, never resets you. The idle AI takeover is off for this page (it would look like auto-moving).
The web is drawn as a twisted two-strand line, after the flora's spindles: loose and waving while it flies or hangs slack,
pulled straight as it tightens.

**Measured** (`probe_web.cjs`, free space):

| | |
|---|---|
| Idle 3 s, then 3 s of full stick | 0.000 u moved, speed 0 |
| Zip to a prism 150 u ahead | latched in 0.18 s, peak 177 u/s, lands at 1.3 s, bobs 4 times, never closer than 9.8 u |
| Swing (120 u/s, anchor 20° off the path, held 3 s) | 328° round the anchor, peaks at 147 u/s |
| Miss (nothing in the cone) | the web flies out and comes back; the hull does not move |
| Two webs, left and right | both latch, pulled straight between them (0.0 u sideways drift) |
| Hitting a prism at 150 u/s | bounces back at 73 u/s, no reset |
| A crystal 100 u ahead | webbed and caught at 1.08 s |

**Found.** A spring cannot land a fast body: it flies through or, with a pad spring, punches through; the landing is a
restitution (L-STU-104). And a thrustless AI strands itself in several distinct ways (L-STU-105): it is not raced yet.

**Decisions needed (Yash, by flying it).** The feel knobs are all in Play Style Config: Springiness, Bounce damping, Latch
slack, Pull, Landing bounce, Perch, Prefer far, Aim cone, Web speed, Space drag. And: should a web pass through what it hits
(a prism between you and the anchor) or latch the first thing on its line? Today it latches what the crosshair picked.

## 10. Round 3: the Omega crews, combiner crew vessels (Yash, 2026-10-26)

**The ask** (Yash, 2026-10-26, after the Flea branch): *"now I wanna make a crew vessel, that stoat will be a part of. ...
crew [vessels] contained around 2-3 pilots or we can swap between each seats and control the vessel in different way to
make the actual vessel work. ... like Power Rangers SPD ... each vessel will have their own megaforms and then finally
they will have their omegaforms. They can combine. ... Prototype me some artifacts. By combining all these now we have 21
vessels inside game and here in my prototype somehow combine and make three crew vessels or the bigger versions."*

**What was built:** one page in the Vessel Studio artifact, **Omega Crews** (`Docs/Studios/VesselStudio/omega.html`,
engine `omega-studio.js`, hub card, catalog row), with the three crews as data so they can be compared by flying:

| Crew | Members (Helm, Arms, Core) | Mega moves (RT, lab approximations of the shipped kit) | Omega weapon (Arms) | Core: shield / finisher |
|---|---|---|---|---|
| **Warden**, the field crew | **Stoat**, Thresher, Butterfly | dipole sling (boost arc), wrecking ball (sweep), fold gate (gate pair) | Ripple whip: a 60 u sweep, a wall in two hits | Fold ward / **Annihilation** (180 u cone) |
| **Bulwark**, the heavy crew | Rhino, Grizzly, Scarab | plough (shove + ramp boost), charged cannon, cavitation dash | Siege cannon: a full charge breaks a wall | Switch wall / **Breaker** |
| **Tempest**, the fast crew | Squirrel, Sparrow, Manta | skim boost, pulsefire, boost tunnel | Pulsefire battery: a wall under fire 2.5 s | Afterburner veil (tunnel rings behind) / **Kabloom** |

**The rules the page tests (every number a proposal, Vessel Config > Crew numbers):**
- **Mega forms.** The three members fly as themselves; you fly one, the AI the other two (the Orca's rule). Each has ONE
  move on RT. A hull contact costs as the platform's does; the moves smash neutral mass.
- **Combine.** Hold LT with every member inside the gather range (140 u) and the meter at or above 0.3: the two others
  dock onto the caller in 1.6 s and the crew is the **omega form**, one body, the members' average flight x crew speed
  (Warden 1.15, Bulwark 1.1, Tempest 1.3) and x crew turn (0.6 / 0.5 / 0.75). It ploughs mass it touches; a wall bounces it.
- **Three seats.** Helm flies (hold LT 0.5 s to split). Arms aims a turret with the right stick and fires on RT. Core holds
  the shield on LT (-0.06/s more meter) and fires the **finisher** on RT once the meter is at 0.9: a cone ahead,
  everything in it including walls, costing 0.7 and splitting the crew (the Megazord's finishing move ends the fight).
- **The omega meter.** +0.22 per crystal, +0.004 per skim, +0.02/s while apart; -0.035/s combined. Empty = forced split.
- **One swap.** D-pad left / right, the arrow keys, a two-finger swipe: the arena's gesture (`Docs/CREW_STATIONS.md`) steps
  you round the three seats; the AI takes the seat you leave, in both forms.
- **The Omega Run (the mini-game, B6 of the vision).** The Skim Race course's crystals, three lit at a time from the
  first untaken, so flying apart collects faster; **every third crystal is locked inside a shielded wall** across the
  course that only the omega weapon (or the finisher) breaks, and the window does not pass a locked crystal. Apart for
  the crystals, together for the walls: the meter couples the two.
- **AI, input-only (D4).** Each member flies for the lit crystal nobody else claimed; when none is open it flies to the
  wall's approach point. The crew calls a gather when a locked crystal is near (320 u) or is all that is lit, and the
  meter allows; the caller holds back at half cruise while the others close. Omega Helm flies for the wall's approach
  point (90 u out) then faces it; Arms turns the turret onto the nearest wall in range and fires; Core shields when mass
  is about to hit and fires the finisher when a wall is in the cone. Easy / Medium = Hard plus `SkimRaceDifficulty.asset`'s
  late notice and misjudged crystal, plus a lab-only late press.
- **Elements, proposals:** Charge = weapon power (x1 to x1.6), Mass = ward cost (x1 to x0.4), Space = finisher reach
  (x1 to x1.5), Time = combine speed (x1 to x0.5). L5 names: Overload, Bulwark, Horizon, Instant. Nothing signed off.

**Scorecard columns (D5):** per intensity, the crew on AI at Easy / Medium / Hard, **Never combine** (cannot open a locked
crystal: DNF is the expected answer) and **Always combined** (one body collects one crystal at a time). If the crew
is not faster than both controls, combining and splitting are decoration. Measured below.

### 10.1 Measured (round 1, 2026-10-26; headless, three seeds, median seconds; `omega_score.cjs` in the session's scratch, the page's own `runHeadless`)

| Crew | Course | Easy | Medium | Hard | Never combine | Always combined | Hard combined % | Combines |
|---|---|---|---|---|---|---|---|---|
| Warden | I1 | 291 | 270 | 271 | DNF (0 walls) | **255** | 22% | 7 |
| Warden | I2 | 371 | 363 | 361 | DNF | **339** | 30% | 9 |
| Bulwark | I1 | 268 | 250 | 254 | DNF | **229** | 15% | 7 |
| Bulwark | I2 | 319 | 350 | **328** | DNF | 353 | 21% | 9 |
| Tempest | I1 | 230 | 220 | **212** | DNF | 262 | 29% | 5-6 |
| Tempest | I2 | 286 | 306 | **297** | DNF | 385 | 38% | 6-8 |

Every crew finishes every course with every wall broken (7 on I1, 9 on I2); **Never combine never opens a wall**: the
control that proves the walls need the omega. Economy at the lab values: +0.22 per crystal apart (half combined),
+0.02/s apart, -0.05/s combined, finisher 0.7.

**Found (the lab's honest answers):**
- **The Tempest is the crew the design wants**: its crew beats both controls on both courses (212 vs 262 always; 297
  vs 385). Fast members collect apart, the omega's battery opens walls, and it must split to refill.
- **The Warden's omega has no reason to split**: always combined beats its crew on both courses (255 vs 271; 339 vs
  361). Its whip opens a wall in one pass and the omega ploughs mass, so the only cost of staying combined is the meter,
  which crystals half-refill. **Decision needed (designer):** either the omega pays more for staying combined (drain,
  Combined crystal pay: both sliders in Vessel Config), or the Warden's members get a reason to be apart (the Butterfly's
  gate pair and the Thresher's ball are worth little on this course).
- **The Bulwark splits the difference**: always combined wins I1, the crew wins I2 (the longer, tilted course; the slow
  omega loses more time lapping back for a crystal it skipped).
- **The difficulty ladder barely separates a crew** (Medium is fastest in three of six rows, all within a few percent):
  late notice and a misjudged crystal are one member's mistakes, and three members cover for each other. A crew's levels
  need a crew mistake (a late or early combine call, a wasted finisher) before Hard can be shown fastest (D24).
- **Fixed on the way** (the lab would have measured nothing without them): a gather that chases a moving leader at its own
  speed never docks (members circled "gathering" for 500 s; the dock is kinematic now, 1.6 s); an objective window that
  skips past a locked crystal lets the crew collect every open crystal first and leaves eight walls for one long omega
  run (the window is anchored at the first untaken crystal: a wall gates the course); a cannon AI that waited for a turret
  angle inside a cone it could not reach held its shot forever; a locked finale with an empty meter was a dead end (the
  last crystal is never locked and the meter trickles back apart).

**Next rounds (the owner flies it first):** Yash flies the three crews (Game Config > Crew) and records Keep / Drop per
crew and the feel of the swap, the dock and the finisher on the Decisions tab. Then: a crew mistake for the levels, a
body for each omega (today the three member bodies on struts), the Warden's reason to split, a second mini-game
(a wall-breaking race against a rival crew), and the Unity port by `/artifact-to-unity` with the mini-game (`/arcadegame`)
and the four abilities, per CLAUDE.md's artifact-to-Unity pipeline.

### 10.2 The Unity clone (2026-10-26, `Ys-bleeding-edge`; Yash: "make this prototype in Ys-bleeding-edge, I wanna test there")

Three hull classes, `Warden = 25`, `Bulwark = 26`, `Tempest = 27`, each a Stoat clone by `Tools/Build/author_omega_assets.py`
carrying `OmegaCrew` (the forms, the seats, the meter, the members' moves, the crew weapon, the finisher, input-only AI
seats), `OmegaVesselTransformer` (the page's cruise / turn / roll per form), `OmegaCrewView` (placeholder bodies and
struts) and `OmegaCrystalEffectSO` (crystals fill the meter). `OmegaMath` is the page's part B line for line, held by
`OmegaMathTests` to goldens from the page's own functions (`omegaFlight`, `slotOf`, the dock ease, the meter, `mults`,
the `omegaCore` cone). In the Toybox's Vessel Changer, as every port must be (CLAUDE.md "Bring it to Unity"). What the
clone folds (nine member moves into boost / smash / guns / cannon; a cannon that bursts at range instead of flying) and
what is round 2 (the Omega Run mode via `/arcadegame`, member and omega bodies via `/vessel-model`, skims into the
meter, rider seats, an omega camera) is written in `Assets/_Scripts/Controller/Vessel/Crew/OMEGA_CREW.md` §3 and §6;
the hand test is `Docs/UNITY_VERIFICATION_CHECKLIST.md`. Not editor-verified: the Unity CLI was not available.

### 10.3 Round 2 (2026-10-26, Yash's first Unity test): the march formation with three roles

Yash flew the round-1 clone and saw the Stoat alone with a black cube moving beside it on the D-pad, and asked for a
different shape: "an aligned flock of three, group of three ... when I swap, my camera should just go to that particular
vessel, and the other two will be controlled by AI ... the relative distance between each vessel will be the same ...
the manoeuvre will be done by the Stoat, the shooting and everything by the right vessel, and the left one will protect
... every big vessel needs a defence, attack and agility ... defender, midfielder and attacker ... one controlled by the
player and two by AI, where the player can swap; later also swappable in multiplayer. For now, just make this much."

Done the same day on `Ys-bleeding-edge`: every crew is a MOBILITY vessel (member 0, it leads and the formation flies with
its numbers), an ATTACKER (member 1, to its right) and a DEFENDER (member 2, to its left): Warden = Stoat / Sparrow /
Rhino, Bulwark = Scarab / Grizzly / Butterfly, Tempest = Squirrel / Thresher / Manta (proposals from the fleet's
strengths; rearranged in `OmegaMath.Crews` and the page's CREWS). The Unity crew is a rigid formation: the swap moves
the hull (and the camera) to the chosen member's place, the AI holds the other seats; the attack seat aims a turret
and fires, the defence seat shields on LT and fires the ward on RT; the black bodies were the view's colour not reaching
the instanced material (fixed: the colour is set on the material). The page's CREWS were recomposed the same way and
the artifact republished; the page keeps the round-1 combine (mega / omega forms), which the game does not have yet.
`OMEGA_CREW.md` §1-2, §6 and the checklist's Omega section are the record.

### 10.4 Decision (design principle, 2026-10-27): a Voltron vessel is designed from the ground up, never assembled

"It's generally better to build a Voltron type character that is designed from the ground up to be both many separate
vessels and one big vessel at alternating parts of its game loop than it is to build a mechanic for combining any
characters that have been designed for a complete game loop as an autonomous entity."

What it means for the Omega crews:
- **Round 1 measured exactly this.** The Warden's omega never had a reason to split (§10.1): the Stoat, the Thresher
  and the Butterfly each already have a complete loop of their own, so being apart buys the crew nothing, and being
  combined costs only a meter. A combine mechanic bolted onto autonomous vessels has no loop of its own.
- **The three crews as composed (§10.3) are a test bed, not the design.** The formation, the seat swap, the AI seats,
  the attacker's turret and the defender's shield are plumbing that any combiner vessel uses; the Stoat / Sparrow /
  Rhino compositions are placeholders standing in for parts that do not exist yet.
- **The next round designs ONE supervessel from the ground up**, in the artifact first (`/vessel-studio`, then
  `/artifact-to-unity`): three PARTS, none of which is a complete vessel on its own. Each part's solo loop is a piece
  of the whole's (the scout that can only position, the gun that can only fire where the scout put it, the shield
  that only holds while the gun works), and the game loop ALTERNATES on its own terms: an apart phase that only the
  parts can do, a combined phase that only the whole can do, and a reason to go back (the Omega Run's walls were the
  first sketch of that pressure). Its mini-game and its four abilities come with it, as every new vessel's do.
- Until that design exists, nothing more is built on the assembled crews beyond what the test bed needs; no new crew
  is composed from shipped vessels.

### 10.5 The three Unity crews removed (2026-10-27, Yash: "remove the three mega vessels we just created")

Warden, Bulwark and Tempest (25-27), their prefabs, configs, maps, cards, the Omega C# and the generator are gone from
`Ys-bleeding-edge` (commit after 6350dfc87d); the enum, the roster and `EnumIntegrityTests` are back to 25 members and
ids 25-27 are free again (the ledger in §0). The Omega Crews lab page stays as the superseded round-1 record (its hub
card says so). Nothing of the Omega crews reaches `bleeding-edge`.

## 11. The Siphonophore: a Voltron vessel designed from the ground up (round 1, 2026-10-27)

**Page:** `Docs/Studios/VesselStudio/siphonophore.html` + `siphonophore-studio.js` (the "Siphonophore" bay of the one
artifact). **Why this animal:** a siphonophore (the Portuguese man o' war is one) is one animal made of zooids that are
not animals on their own, which is exactly the principle of §10.4: many separate vessels and one big vessel at
alternating parts of one game loop, designed that way from the start.

### 11.1 The parts (none a complete vessel)

| Zooid | Role | Its ONE verb | Flight (proposal) | RT move | LT |
|---|---|---|---|---|---|
| **Bell** (the swimming bell) | Mobility | **lights** a DARK crystal within the lantern's 90 u (it stays lit) | cruise 78, turn 130, roll 150 | Pulse: x1.8 for 1 s and the lantern at twice its range, cool 3 s | hold = call the fuse |
| **Sting** (the stinging tentacle) | Attack | **opens** a LIT crystal's husk (6 shielded prisms), one per venom shot (range 160, every 0.25 s); smashes mass | cruise 60, turn 85, roll 100 | Venom shot | hold = call the fuse |
| **Bract** (the shield leaf) | Defence | **nets** a FREED crystal within 24 u into its hold (3), unscored | cruise 66, turn 100, roll 110 | Cast the net | ward (mass within 18 u is shed); with a hold, the call |

No part scores. A dark crystal cannot be opened (the Sting cannot see it), an unopened one cannot be netted, and a
crystal behind a wall is LOCKED (dark, unopenable, unnettable) until the colony opens the wall.

### 11.2 The colony (the fused form) and the loop

Hold LT with every zooid within 120 u of the caller (the person's zooid, else the Bract): they dock in 1.2 s into the
colony (the zooids' average flight x1.25 speed, x0.55 turn), three seats on the same gesture: **Helm** (the Bell) flies
and rams on RT, **Lance** (the Sting) aims the lance (right stick) and fires at walls (range 140, every 0.3 s; a wall
takes 3 hits or rams), **Ward** (the Bract) holds the shield on LT. The colony **digests** the hold into the score, one
crystal per 4 s, ploughs mass, and cannot light, open or catch anything. The hold empty and no wall to open, it
**splits itself** (Helm holds LT 0.5 s to split early, the hold kept). So the loop is LIGHT, OPEN, NET, FUSE, DIGEST
(and RAM), SPLIT, and neither phase can do the other's work: that is the design, not a meter.

The Tide Run (the lab's mini-game): every crystal of the Skim Race course dark in a husk, every third behind a wall only
the colony opens; finish = every crystal digested.

Elements (proposals, one scaled parameter each): Charge = Venom (husk prisms per shot x1 to x3; L5 Venom opens a husk
in one shot), Mass = Trawl (net radius x1 to x1.6; L5 the hold takes one more), Space = Lantern (range x1 to x1.8; L5
Beacon: the pulse lights the whole window), Time = Digest (x1 to x0.5; L5 Surge: the zooids keep the colony's speed
3 s after a split).

### 11.3 Measured (round 1, 2026-10-27; headless, two seeds 11 / 22, seconds; `siph_score.cjs` in the session's scratch, the page's own `runHeadless`)

| Course | Easy | Medium | Hard | Never fuse | Always fused | Colony share (Hard) | Fuses |
|---|---|---|---|---|---|---|---|
| I1 (24 crystals, 7 walls) | 439 / 380 | 357 / 380 | 384 / 419 | DNF (0 digested) | DNF (0 digested, 1 wall) | 25% | 8 |
| I2 (30 crystals, 9 walls) | 443 / DNF | DNF (29) / DNF | 484 / 485 | DNF (0) | DNF (0, 9 walls) | 25% | 10 |
| I3 | - | - | DNF (17 / 15 digested in 600 s) | DNF (0) | DNF (0, 14 walls) | 12% | 5-6 |

**Found:**
- **The two controls fail for the designed reason**: never fusing scores nothing (the hold is never digested); always
  fused lights, opens and nets nothing (0 digested on every course, even with every wall rammed open). The alternation
  is forced by what each form CAN do, which is what §10.4 asked for; the Omega lab could only force it with a meter.
- **The levels do not separate yet** (Medium is fastest on I1; I2's Medium seeds hit the time limit): the belief
  mistakes cost a three-step relay little, and the relay's own stalls (below) are bigger than a mistake. A crew mistake
  (a late call, a net cast early) is the lever, as the Omega lab found (D24).
- **I3 and I4 exceed the lab's 600 s limit** at the lab's digest time (4 s a crystal, 48 crystals on I3 = 192 s of
  digesting alone). `digestSeconds` and `holdCap` are the pacing levers; a course meant for this vessel is shorter or
  lights more than one crystal per anchor.
- **Fixed on the way** (the lab would have measured nothing without them): the Sting circled its crystal inside its
  own turn radius and never lined a shot up (it now stands off to 95 u and attacks straight); a colony that fused for a
  wall and split at once because the wall was "not ahead" (it keeps the window's wall as its goal until it is open);
  an idle zooid flew to the far wall instead of staying with the crew (each idles behind the zooid before it in the
  loop); a lit crystal with no husk left could never be freed (walled crystals are now locked until their wall opens).

**Decisions needed (designer):** the hold's size and the digest time (the rhythm: how long apart, how long fused);
whether the Bell's lantern should reveal husks too (today a lit crystal shows its husk; a dark one is a dim shape);
whether the Ward seat earns something in the colony phase beyond the shield (today it is the quiet seat); names.

**Next rounds (the owner flies it first):** Yash flies the three zooids and the colony (Game Config > Seats) and
records Keep / Drop and the feel of the relay, the fuse and the digest on the Decisions tab; then a crew mistake for
the levels, a course of its own, bodies via `/vessel-model`, and only then the Unity clone by `/artifact-to-unity`
with the Tide Run (`/arcadegame`) and the four abilities.


### 11.4 The ability kit (round 2 proposal, 2026-10-27, Yash: "get inspired from our other 21 vessels in game, and my 10 prototypes")

Read from the 21 shipped element maps (`Assets/Resources/ElementalAbilityMaps/*.asset`) and the ten concept prototypes
(Gibbon, Flea, Albatross, Remora, Dragonfly, Mole, Marlin, Eel, Octopus, Gecko). **Each zooid owns one element**, so
the vessel still has the fleet's four: the Bell Space, the Sting Charge, the Bract Mass, the colony Time. Each zooid has
a passive and two triggers; B calls the colony from any zooid (proposal; today LT calls it). The page carries the same
table as data (`KIT` in `siphonophore-studio.js`, Vessel Config > Ability kit), each row marked **in the sim**, **in
part** or **proposal**.

| Part | Input | Ability | What it does | From | State |
|---|---|---|---|---|---|
| Bell (Space) | passive | Lantern | Lights every dark crystal in reach; Space x1 to x1.8 | Dolphin Echo Sight, Angler Paint | in the sim |
| | RT | Pulse Jet | Hold to inhale, release to jet; on the beat of the last two pulses the chain grows x1.1 a pulse to x1.8; a pulse doubles the lantern for 1 s | Octopus | in the sim (round 2) |
| | LT | Slipstream | Hold to lead: the Sting and the Bract form up in its wake, one, two, three, drafting at its speed x1.3 | Remora, Jacks The Call | in the sim (round 2) |
| | L5 | Beacon | A pulse lights the whole window | Dolphin Pilot Echo | in the sim |
| Sting (Charge) | RT | Nematocyst Volley | Aimed venom to 160 u; breaks a lit crystal's husk (Charge x1 to x3 prisms a round), else smashes mass | Sparrow Pulsefire, Urchin Chain Spikes | in the sim |
| | LT | Tentacle Lash | The Sting streams a tentacle of its trail; hold to coil (turn x2), let go to whip it straight, cutting mass; a rival hull struck loses a petal, which spills as a crystal | Eel, Thresher, Wrasse Kite Ram | in the sim (round 2) |
| | passive | Reel | A hit on a target over 120 u away pulls the Sting to it like a bungee: the slowest zooid catches up by hitting things | Gibbon, Shrike Void Sling | proposal |
| | L5 | Contagion | A husk the venom opens poisons the next husk in the window | Manta Contagion, Urchin Overcharge | proposal |
| Bract (Mass) | RT | Net Cast | Catches freed crystals in reach into the hold (3); Mass x1 to x1.6 | Jacks netting, Angler Lure | in the sim |
| | LT | Bract Shell | Super-shielded plates, open ahead, clear the mass they meet; let go and they drop as danger prisms behind the hold | Orca Danger Shield, Rhino afterimages | in part (clears mass) |
| | passive | Anchor | Stopped on mass it digs in: net reach x2, the others dock in half the time; the colony forms where it sits | Grizzly Dig In, Shrike Nest, Gecko | in part (gather at the Bract) |
| | L5 | Trawl | The hold takes one more, and the net catches crystals a lashed rival sheds | Wrasse Crystal Steal, Squirrel Steal | in the sim (round 2) |
| Colony (Time) | passive | Digest | One held crystal into the score every 4 s; Time x1 to x0.5 | its own | in the sim |
| | Helm RT | Ram Spool | Hold to wind up; at full spool it runs red and a ram opens a wall cell and breaks shielded mass in one hit | Rhino Ramp Spool and Breaker, Thresher | in part (tap pulse) |
| | Helm LT | Scatter | Hold to split; let go and the three are flung x1.4 along the stick, each to its own next crystal | Remora sling, Gecko hop, Butterfly Fold | in part (hold to split) |
| | Lance RT | Siphon Lance | Aimed hitscan to 140 u, cuts mass and wall cells | Shrike Void Lance, Serpent Sniper | in the sim |
| | Ward LT | Colony Shell | The hull ploughs the mass it meets | Grizzly Rush, Brittle Star Speed | in the sim |
| | Ward RT | Chain Wake | The colony lays its body behind it as a short chain of shielded prisms | Eel body, Brittle Star corner trails | proposal |
| | L5 | Surge | After a split the zooids keep the colony's speed for 3 s | Remora sling, Urchin Slipstream | in the sim |

**Why it is one vessel (the combos no single zooid finishes):** light, venom, net, digest (each verb is the next one's
condition); lash, Trawl, digest (the Sting knocks a rival's petals loose as crystals, the Bract nets them, the colony
digests them); Slipstream carries the slow pair (the march is a Bell ability); Anchor sets the fuse point and the
Shell's danger drop covers the docking; the Bell's pulse chain carries into the colony's spool; Scatter lands the three
on the window's three crystals.

**Rules it keeps:** PvP is petals only (the lash takes petals, and every scored hit silences per `SILENCE.md`); one
scaled parameter per element; the AI writes inputs only. **Decision needed (designer):** which proposals to fly next
(suggested order: Slipstream, Tentacle Lash with Trawl, Pulse Jet's beat, Ram Spool), and whether B as the shared
fuse call frees LT well enough on a phone.

### 11.5 Round 2: the kit flown, one ability at a time (2026-10-27, Yash: "do all recommended 1 by 1")

Measured with the page's own `runHeadless` on seeds 11 / 22 / 33 (seconds to digest every crystal; DNF(n) = n digested
when the 600 s limit hit). Every row's "off" column is the same build with that one ability switched off, so the
difference is the ability. Both controls (never fuse, always fused) still digest nothing on every course and level.

**1. Slipstream (the Bell's LT).** While the Bell holds LT it leads: a zooid in its wake (140 u long, 26 u wide), behind
its place in the line (22 u x its number), may fly at the Bell's speed x1.3; at its place it holds the Bell's speed. A
leading Bell turns at x0.7 (step 3 lets it jet while it leads). The AI: a zooid whose next job is over 220 u away takes its place in the
line; the Bell leads while one does, and with nothing of its own to light it carries the line to the Sting's lit
crystal (else the Bract's freed one). The fuse call moved from LT to **B** (keys H) for every zooid.

| Course, level | Slipstream off | Slipstream on |
|---|---|---|
| I1 Hard | 398 / 433 / 430 | 389 / 378 / 385 |
| I1 Medium | 405 / 440 / 415 | 450 / 469 / 438 |
| I1 Easy | 445 / 448 / 443 | 499 / 455 / 492 |
| I2 Hard | 484 / 485 / 481 | 484 / 402 / 476 |
| I2 Medium | 498 / 470 / 485 | 448 / DNF(29) / 501 |
| I2 Easy | 519 / 525 / 512 | 525 / DNF(27) / 505 |

Found: Slipstream is a skill ability. It takes Hard about 35 s off I1, and it is the first thing that makes Hard clearly
the fastest level (off, Hard and Medium tie on I1). Easy and Medium lose time with it: their late notice and misjudged
crystals break the line. A rule where the Bell leads only with nothing of its own to light was tried and dropped (it
sat inside the noise on every level). Also found and fixed: the AI Bract held RT down after a cast that missed, and the
net only casts on a fresh press, so it never cast again (it stalled one Easy run at 6 crystals).

**2. Tentacle Lash (the Sting's LT) and Trawl (Mass level 5).** The lab gained **rivals**: Game Config > Rivals (0 to 3,
default 2), stand-ins in the Ruby, Gold and Blue domains that fly the course at 85 u/s carrying 8 petals each (a ring of
small crystals round the hull, one per petal). They take no crystals, have no abilities and never strike back. The
Sting streams a 60 u tentacle (its own path). Holding LT coils it: the turn doubles and the tentacle bunches to half.
Letting go after at least 30% of a 0.6 s coil whips it straight from the hull toward its tip (60% to 100% of its length
with the coil), cutting the neutral mass within 9 u and taking one petal from a rival within 13 u. The petal spills as
a crystal that drifts for 25 s. With Trawl the Bract's net takes it too, and the colony digests it as a point toward the
finish. The AI coils while a rival with petals is within 96 u, and lets go when the rival is on the tentacle's line.

Also fixed in this step, because Slipstream made it four times worse: the AI flew straight at a crystal behind a closed
wall and rammed the wall (244 wall contacts in the first 90 s of I1 Hard before this round, 1008 with Slipstream). A
zooid whose path crosses a closed wall now aims just past the wall's nearest edge; a crystal lying in a wall's own
plane (an earlier lap's crystal at the same anchor) is reached from its side. **These numbers supersede the Slipstream
table above**, which was measured before the detour (8 seeds, mean and standard deviation):

| Course, level | Old AI, no new abilities | Detour only | Slipstream + lash + detour |
|---|---|---|---|
| I1 Hard | 419 (sd 15) | 404 (2) | 344 (14) |
| I1 Medium | 423 (17) | 412 (10) | 397 (16) |
| I1 Easy | 458 (16) | 431 (14) | 430 (16) |
| I2 Hard | 481 (7) | 479 (8) | 442 (34) |
| I2 Medium | 492 (11) | 496 (11) | 491 (23), 1 run over the limit |
| I2 Easy | 521 (15) | 521 (8) | 512 (31), 4 over the limit (24 to 29 of 30) |

| Lash and Trawl (8 seeds) | I1 Hard | I1 Easy | I2 Hard |
|---|---|---|---|
| Mass 0, lash off | 336 | 436 | 443 |
| Mass 0, lash on | 344 (0.5 hits a run) | 430 (1.1 hits) | 442 (0 hits) |
| Mass 5, lash off | 350 | 418 | 473 |
| Mass 5 (Trawl), lash on | 344 (0.6 stolen a run) | 421 (0.6 stolen) | 470 (0.1 stolen) |

Found: the lash works, and costs nothing once the AI halves its stick while coiled (a coiled Sting turns twice as fast,
and the AI's steering overshot: before that it cost about 50 s). But the AI lands about one lash in eight, so the
Lash, Trawl, digest steal adds at most one crystal a run: rivals in a race rarely pass within the tentacle's reach.
It is a person's ability for now, and a better AI rule (hunt a rival while in transit) is the next step if the steal is
kept. **Mass level 5 is slower on I2** (473 against 443 for Hard, every Easy run over the limit): its extra hold slot lets
the crew work past a walled crystal and fly back for it later. Fusing at a walled crystal whenever the hold carries
anything was tried and was slower nearly everywhere, so it was dropped. **Decision needed (designer):** whether Trawl's
+1 hold stays, since it slows the loop it belongs to.

**3. Pulse Jet (the Bell's RT).** The Octopus's breath on a zooid that has a cruise: RT held inhales (the Bell slows to
x0.6 and charges over 0.7 s, the bell visibly swelling), letting go jets (boost 1 + 0.8 x charge x chain, held 1 s,
then fading as every boost does) and flashes the lantern at twice its range for 1 s. A release within 15% of the last
interval is on the beat: the chain grows x0.1 a pulse, to x1.8. The old tap pulse (x1.8 for 1 s every 3 s) is gone. The
AI breathes while it travels (no crystal within 1.2 lantern ranges) on a 1.6 s tempo, inhaling for 72% of it; Easy and
Medium let go up to their lab-only late press early or late (0.2 s and 0.1 s), which breaks the beat. **A leading Bell
jets too**, so the line drafts on its beat: with the jet blocked while leading the AI Bell jetted 8 times a run and the
jet changed nothing; allowed, it jets about 115 times and the whole line rides it.

| 8 seeds | Tap pulse (step 2) | Pulse Jet, not while leading | Pulse Jet, leading too (shipped) |
|---|---|---|---|
| I1 Hard | 344 (sd 14) | 343 (11) | 333 (19); 42% of jets on the beat, chain up to 6 |
| I1 Medium | 397 (16) | 408 (14) | 397 (11); 38% on the beat |
| I1 Easy | 430 (16) | 441 (12) | 428 (16); 24% on the beat |
| I2 Hard | 442 (34) | 445 (37) | 430 (31) |
| I2 Medium | 491 (23), 1 over | 492 (19), 4 over | 479 (38), 1 over |
| I2 Easy | 512 (31), 4 over | 488 (3), 6 over | 533, 6 over the limit |

Found: the jet is worth about 10 s on I1 and I2 for Hard and is level with the tap pulse below that; its value is the
skill it asks (the beat), not raw speed. The beat separates the levels as designed (Hard 42%, Easy 24% on the beat).
I2 Easy sits on the 540 s limit with or without it.
