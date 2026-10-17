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
Finish limit 400 s (DNF beyond it). Measured headless, seed 11, 2026-10-17:

| Vessel | I1 Hard | I1 Easy | I1 No mech | Mech % | Top u/s | Reading |
|---|---|---|---|---|---|---|
| Gibbon | 266 | 225 | 232 | 42% | 170 | The AI's swings are detours on a gentle course: it swings for turns only now and still trails its own control. The mechanic is for a human to judge; the AI cannot make it pay on I1 |
| Flea | 294 | 326 | 326 | 56% | 258 | Bounces pay (+10% over the control) once the skim and the hull kept separate cooldowns; the chain is the skill |
| Albatross | 140 (one run) / DNF | DNF | DNF | 100% | 118-300 | Finishes only when it stays in the ribbon's updraft; a misjudged crystal takes Easy out of the lift and it never climbs back. Unforgiving by design, and the AI is the weakest of the ten |
| Remora | 80 | 168 | 222 | 92% | 240 | The draft is enormous; **found:** two Remoras leapfrog each other's wakes up to the cap in 40 s (a slipstream chain). A design question |
| Dragonfly | 156 | 150 | 240 | 20% | 190 | Dashes pay; Easy and Hard within noise (Hard dashes more and hits more) |
| Mole | 63 | 63 | 351 | 87% | 217 | The ribbon is mass, so the Mole is a 150 u/s hull on any Skim Race course; switch the ribbon off (Scene Config) to test a bare course |
| Marlin | 147 | 149 | DNF | 55% | 121 | The AI thrusts in bursts and drifts the corners; the control cannot move at all (the mechanic IS the engine) |
| Eel | 201 | 201 | 280 | 21% | 79 | Coil and shed pay; shed segments now fall to the sides, after the AI hit its own wall 120 times a race |
| Octopus | 88 | DNF | DNF | 100% | 254 | Hard keeps the beat and flies; Easy's late presses break the chain and it crawls. The sharpest difficulty ladder of the ten |
| Gecko | 160 | 197 | 262 | 28% | 200 | Runs and hops pay; a stationary loop past a segment's end was found and fixed |

The four-course table (I1-I4, all levels) is appended in section 4.1 when the headless run finishes. Read everything as
relative, between a vessel's own columns: the AI is a line-following pilot plus one rule per mechanic, not a player.

**The difficulty ladder is not clean yet.** D24 asks for Hard fastest on every course. The shared belief mistakes (late
notice, misjudged crystal, from `SkimRaceDifficulty.asset`) cost these fast, tight-turning movers little, and the lab-only
press delay (Easy 0.2 s, Medium 0.1 s) cuts the other way on tempo mechanics (the Octopus). This is a round-1 lab for
FEEL; the AI ladders get their round once the five are picked.

### 4.1 Four courses

(appended by the headless run; see the Scorecard tab of each page for the live numbers)

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
