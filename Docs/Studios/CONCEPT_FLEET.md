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
| Gibbon | 73 / 85 / 79 | 136 / 127 / 111 | 235 / 293 / 254 | 182 / 177 / 123 | 253 | Round 2 (§8.2): two modes. The auto-tethers alone beat the control on I1 and I3 (I3 with zero swings); the AI's swings cost on the twisting courses, where a 1.6 s fling cannot steer. Round 1's swing kind: 159 / 161 / 161, 216 / 212 / 220, 537 / DNF / DNF, 314 / 233 / 218 |
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
(17 is free in origin today), and TETHER.md §12's id note is rewritten; (2) rebase or merge `cece/loving-feynman-hlgf18`
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
