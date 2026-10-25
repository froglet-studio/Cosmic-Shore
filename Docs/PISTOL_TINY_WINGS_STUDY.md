# The Pistol and Tiny Wings: a study of the game and its genre

**Why this exists.** The Pistol (the sidescroller half of the Goby & Pistol crew vessel) is built on Tiny Wings. Garrett,
2026-10-24: "you need to understand tiny wings better, and really put the effort into making the pistol feel great and
satisfying. it should be properly scaled, have the right sinusoidal curve, speed, accelerations, camera, everything. find
the other games in the genre. what works and what doesn't." Four passes before this one tuned the Pistol from memory of
the game; each was off in a way the next pass corrected. This file is the record of what the game actually does, with
numbers, so the Pistol is tuned from evidence and the next person does not start from memory either.

**How to read the numbers.** Tiny Wings' source is not public. The numbers below come from its MIT-licensed remake
(haqu, 2011, "an attempt to implement key features of the original"), the Kodeco (Ray Wenderlich) tutorial that
rebuilds it, two other clones, and the player guides. Where the sources disagree, both are given. The unit that carries
over to the Pistol is the **bird's length (d)**: everything is measured in it, then rebuilt in the Pistol's length
(`2 x pBodyR` = 1.7 u).

## 1. Tiny Wings, as it is built

### 1.1 The ground: random key points with a half-cosine between

Not a sinusoid. The remake's `generateHillKeyPoints` (and the tutorial's) lays **key points**, alternately a crest and a
trough:

```
minDX = 160, rangeDX = 80      // the next key is 160-240 pt on
minDY = 60,  rangeDY = 60      // and 60-120 pt up or down from the last, the direction alternating
y clamped to [20, screenH]     // the ground stays in a band
first key: a crest (sign = -1: the first half-hill runs DOWN)
```

between each pair the ground is a **half-cosine**: `y = ymid + ampl * cos(pi * t)`, so every crest and trough is level
and the steepest slope of a half-hill is `pi * dy / (2 * dx)` (60 pt over 240 pt: 22 deg; 120 pt over 160 pt: 50 deg).
The hero's radius is 14 pt (the tutorial: 16), so **d = 28 pt**, and:

| | pt | in d |
|---|---|---|
| half-hill, crest to the next trough | 160-240 | 5.7-8.6 |
| hill, crest to crest | 320-480 | **11-17** |
| rise or fall of a half-hill (trough to crest) | 60-120 | **2.1-4.3** |
| the band the ground stays in | 300 | 11 |
| the screen (iPhone 4, 480 x 320 pt) | 480 wide | **17 d wide: 1-1.5 hills; the bird 1/17 of it** |

Every hill is a different length and height. Islands are not modelled by the remake; in the game they are separated
by long jumps, early islands have set patterns, and later islands are taller (the guides: island 3 "is tricky without
knowing where its hills are"; "hitting the most vertical ramps is the key" to cloud touches).

### 1.2 The physics: a frictionless ball, a heavy dive

Box2D, 32 pt to the metre (so d = 0.875 m):

| | remake | tutorial | in d |
|---|---|---|---|
| gravity | 9.8 m/s^2 | 7 m/s^2 | 11.2 d/s^2 |
| the dive, a force while the touch is held | 40 N down | (5, -50) N | 66 m/s^2 = **6.8 g** on the 0.6 kg body |
| drag (Box2D linear damping) | 0.05 /s | 0.1 /s | 5 % a second |
| friction with the ground | **0** | 0.2 | |
| restitution (bounce) | **0** | 0 | |
| the floor on forward speed | 3 m/s | 5 m/s | 3.4 d/s |
| the floor on falling speed | -40 m/s | -40 m/s | 46 d/s (a cap in effect) |
| a cap on speed | none | none | |
| the wake impulse (the first tap) | (1, 2) | (1, 2) | |

What follows from those, and is the whole feel:

- **The dive is the weight.** The air is not light (full gravity); the dive is seven times heavier. Hold it on a
  downslope and the slope's share of 7.8 g drives the ball; hold it on an upslope and the same decelerates it, which is
  why every guide says "release going uphill". With almost no drag, no friction and no cap, **speed is only lost on a
  climb, in the air, or into the ground.**
- **A crest throws you by itself.** The ball leaves the ground when its speed round the crest needs more pull than
  gravity gives (`v^2 * curvature > g cos`). Holding the dive multiplies the available pull by ~8, which keeps you down:
  letting go over a crest IS the launch. There is no jump.
- **A landing loses only the speed INTO the slope.** Frictionless and with no bounce, Box2D kills the normal component
  and keeps the tangential one. Land along the back of a hill and you keep everything; land on the front of one and the
  slope takes most of it. That is the entire landing model: there is no "good landing bonus" in the physics. The
  **perfect slide** is scored, not boosted: the remake counts one when you take off with upward speed above a threshold
  (`kPerfectTakeOffVelocityY`), the guides say it is landing on the back of a hill; three in a row is **fever**, which
  doubles points and lasts while the slides keep coming.
- **The floor speed is the game's forgiveness.** You cannot stall; a player who never taps dawdles along at 3.4 d/s
  until night falls. The run ends by the clock (nightfall), never by a crash.
- Speeds in play: a 3 m (3.4 d) descent under free gravity gives 7.7 m/s; the dive down a 35 deg slope adds ~40 m/s^2
  along it. Play runs at **10-30 m/s = 11-34 d/s = 0.7-2 screens a second**, the fall cap at 46 d/s. A launch at 25 m/s
  and 30 deg flies 2.5 s, 54 m (3.6 screens, 3-5 hills) and peaks 8 m (9 d, three hill heights) up.

### 1.3 The camera

- The bird sits **one eighth of the screen from the left** (`winSize.width/8`), the ground in the lower part.
- **Zoom by height only:** `scale = (0.8 * screenH) / heroY` when the bird is above four fifths of the screen; the view
  zooms out to keep it there and zooms back in as it falls. Nothing zooms with speed. The remake snaps; the game eases.
- The sky scrolls at 0.2 of the ground and zooms at 0.75 of it (parallax: the depth cue a flat game has).

### 1.4 What the game does around the physics (the satisfaction)

- **One input, two meanings:** the same hold is "go faster" on a downslope and "come down" in the air. The whole skill
  is WHEN to let go (the crest) and when to come down (onto a back slope). Reviewers call it rhythm-action.
- **Rewards are legible and chained:** perfect slide (points) -> three in a row (fever, x2, a visible state) -> cloud
  touch (points, only reachable at speed) -> speed coins in the valleys (they also push back nightfall). Each is a
  thing you can see coming.
- **The clock is the only enemy.** Nightfall, a bar in the corner; the bird falls asleep. No deaths, no crashes.
- **Procedural look, fixed play:** the islands' colours and patterns change daily ("to see what the islands look like
  today"); the physics never does. Illiger: the dream of flying, positive emotions, a 1.5-year-old could play it.
- **The lineage:** Illiger credits Nathan McCoy's **Wavespark** (2010, a game-a-week piece: one button manipulates
  gravity so the sprite drops into the concave parts of the ground and speeds through the valley; influences Excite
  Truck and Sonic). Tiny Wings is Wavespark with the art, the perfect slide, fever, the clock and the islands.

## 2. The genre around it

| Game | The loop | What works | What does not, or what to avoid |
|---|---|---|---|
| **Wavespark** (McCoy, 2010) | one button, gravity, concave ground | the mechanic itself: dive into the valley, fly off the crest | no reward structure, no feedback; it was not remembered |
| **Tiny Wings** (Illiger, 2011) | hold = heavy; let go = fly; perfect slides -> fever; nightfall | the physics above; chained, legible rewards; forgiving floor speed; no crash | "something of a one-trick bird": replay rests on score |
| **Dune!** (Voodoo, 2017) | hold to dive, release to launch; land on the DOWNslope for a multiplier | the same loop, stripped; height = score | **landing on an upslope at speed ends the run**: a punishing version; players crash on steep landings |
| **Alto's Adventure** (Snowman, 2015) | tap to jump, hold to flip; chains; procedural slope | "Alto never really loses speed" (deliberately unreal physics); no negative objectives; flow over punishment; tricks add speed | |
| **Ski Safari** (2012) | one tap jumps, hold flips, flips give speed; an avalanche chases | speed as a tug of war; mounts as variety | repetitive once learned |
| **Sonic** (slope physics) | slope factor adds/subtracts speed by the sine of the ground angle | the reference for "use the terrain to go fast" (McCoy's own) | |

The pattern that works: **speed comes from the ground, control is one input with a timing, the physics forgive
(floor speed, speed kept on good landings, no crash), and the rewards for timing are visible and chain.** The pattern
that does not: punishing the landing (Dune!'s crash), adding inputs, or letting speed drain so the run stalls.

## 3. The Pistol, rebuilt from the study (2026-10-24)

The Pistol's length is **d = 1.7 u** (`pBodyR` 0.85). Everything below is the game's number in d, rebuilt in u, on
`GobyPistolConfig.asset`:

| | the game | the Pistol | field |
|---|---|---|---|
| half-hill length | 5.7-8.6 d | 10-15 u | `hillHalfLengthMin/Max` |
| half-hill rise | 2.1-4.3 d | 3.6-7.2 u | `hillHalfHeightMin/Max` |
| band about the dart | 11 d | +-9 u | `hillBand` |
| later islands taller | yes | +8 % an island, to x1.5 | `hillGrowthPerIsland/Max` |
| ground | key points, half-cosine | `PistolPlane.HillTrack` (seeded from the island's two perches, the same on every machine) | |
| lean of the island on screen | level | 0 deg | `slopeDegrees` |
| gravity | 11.2 d/s^2 | 19 u/s^2 | `pGravity` |
| the dive | ~7.8 g in all | x7.5 | `pDiveMul` |
| gravity in the air (letting go) | 1 g | 1 | `pAirGravity` |
| drag | 5 %/s | 0.05 | `pDrag` |
| floor speed | 3.4 d/s | 6 u/s | `pMinSpeed` |
| cap | ~46 d/s (fall) | 90 u/s (53 d/s) | `pMaxSpeed` |
| a landing | keeps the speed along the slope | `pHardKeep` 1 | |
| a perfect slide | points | within 30 deg of the slope: `pSmoothBoost` 1.05 (ours: a touch, so it is felt) | `pGoodLand`, `pSmoothBoost` |
| island (perch to perch) | long jumps between | 900-1800 u, 36-73 hills | `islandMinLength/Max` (the Goby's) |
| screen at rest | 17 d, 1-1.5 hills | 29 u, 1.16 hills | `viewWidth` |
| the bird across the screen | 1/8 from the left | 0.15 from the left | `viewPistolAt` -0.35 |
| zoom | by height, bird under 4/5 | headroom 0.2, to 150 u wide; a little by speed | `viewHeadroom`, `viewMaxWidth`, `viewSpeedWiden` |

**Measured** (the ride code run headless at 60 fps, two seeds; `GobyPistolTests.Ride_IsTinyWings` holds it):

| rider | 900 u island (36 hills) | 1800 u (73 hills) |
|---|---|---|
| dives the slopes, lets go up them (the Pistol AI) | 20-21 s, 1.6-1.8 screens/s, 14 flights (13-14 perfect), the longest 4.3 hills, apex 3 hill heights, speed 23 -> 33 d/s, top 51 | 40 s, 1.7 screens/s, 33-34 flights, top 51-53 d/s |
| wrong a third of the time | 39-48 s | 80-97 s |
| never dives | 93-96 s at the floor speed, never flies | 188 s |
| never lets go | 43 s, 1 flight | 86-88 s |

Against the game: the pace, the hill count per screen, the bird's size on screen, the flight lengths and heights and
the top speed are inside its envelope. Open differences, for Garrett to feel: the Pistol AI spends 77-81 % of a ride
in the air (it dives in the air only when that lands it flatter); a wrong hold costs twice the time (so does the
game's); no fever, cloud or speed coin yet (§4).

## 4. What the study says to build next (not built)

- **Fever for the pair.** Three perfect slides in a row is the game's visible reward. For the Pistol it could feed the
  den (extra prisms at the next perch) or the ring (petals x2): legible, chained, and it ties the ride to the pair's
  purpose. A decision for Garrett.
- **Speed coins in the valleys**: the game puts its boosts where the dive goes. Crystals or the Goby's dropped marks
  could sit in the troughs of the island.
- **Juice on the perfect slide and the launch**: a sound and a world effect (the LOCKED world-not-screen rule). FMOD
  slots, shipped empty.
- **The clock**: the game's only pressure. The pair's is the Goby's lead and the den; nothing presses the Pistol yet.

## Sources

- haqu, *Tiny Wings Remake* (MIT): `Game/Objects/Hero.mm`, `Game/Objects/Terrain.mm`, `Game/Scenes/Game.mm`:
  https://github.com/haqu/tiny-wings
- Kodeco, *How To Create A Game Like Tiny Wings with Cocos2D 2.X*, parts 1-2:
  https://www.kodeco.com/2723-how-to-create-a-game-like-tiny-wings-with-cocos2d-2-x-part-1,
  https://www.kodeco.com/2722-how-to-create-a-game-like-tiny-wings-with-cocos2d-2-x-part-2
- felipegatoloko10, *Tiny-Wings-Android* (a reverse-engineered web clone, two summed sines, a 16 px bird):
  https://github.com/felipegatoloko10/Tiny-Wings-Android; micksolo, *puffwing* (three value-noise layers, G = -24,
  hold x3, cap 55): https://github.com/micksolo/puffwing
- Player guides: Gamezebo walkthrough (https://www.gamezebo.com/walkthroughs/tiny-wings-walkthrough/), TapGamers
  (https://www.tapgamers.com/?p=2537), Pocket Gamer review (https://www.pocketgamer.com/tiny-wings/review/)
- Wavespark: Pocket Gamer (https://www.pocketgamer.com/tiny-wings/iphone-hit-tiny-wings-is-probably-based-on-indie-game-wavespark/),
  Northway Games (https://northwaygames.com/tag/wavespark/), Wikipedia (https://en.wikipedia.org/wiki/Tiny_Wings)
- Dune!: Level Winner guides (https://www.levelwinner.com/dune-ultimate-guide-tips-tricks-everything-need-know/)
- Alto's Adventure / Odyssey: Dice (https://www.dice.com/career-advice/altos-odyssey-winning-mobile-game), Wikipedia
  (https://en.wikipedia.org/wiki/Alto's_Adventure); Ski Safari: TouchArcade, 148Apps reviews
