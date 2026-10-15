# The Shrike — design studio record

The Shrike Studio (`Docs/Studios/VesselStudio/shrike.html`, round 1, 2026-10-15) is a **design studio** (`/vessel-studio`
D2): it flies the Notion class document "Class - Shrike" (Design Documents / Class Design Documents / Sparrow Chain),
not a shipped vessel. It is a page of the one Vessel Studio artifact (https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa),
built with `/labmaker` and `/vessel-studio`. It is published on its own as **https://claude.ai/artifact/JD17sQMeuEUETp6AUPqMmy** (private; its own decision
log), from the repo page by `Docs/Studios/Shrike/build_standalone.py`. Explore there, record decisions, then bring them
to Unity (`/labmaker` §7). This file holds what the document says, what the game ships, how the studio
turned the document's words into numbers, what the scorecard measured, and what is left for the designer.

## 1. What the game ships today (measured 2026-10-15, bleeding-edge)

- `VesselClassType.Shrike = 10`. `Assets/_Prefabs/Spacevessels/Shrike.prefab` is a flat Manta copy
  (`Docs/VESSEL_CONSTRUCTION.md`): `SingleStickVesselTransformer` (DefaultMinimumSpeed 10, DefaultThrottleScaler 50,
  so 60 u/s; PitchScaler 60, YawScaler 40, RollScaler 15, RotationThrottleScaler 0), `VesselStatus.boostMultiplier` 4,
  `FullAutoAction` (40 rounds/s, 2000 u/s, projectileTime 1, ProjectileScale 20) and `BoostAction`.
- No ability map (`element_ability_table.py Shrike`: "no ability map"), no `CameraSettingsSO` (the Manta's 30 u),
  no near- or far-field skimmer, the class asset under `_SO_Assets/_TEMP`, no arcade card.
- So nothing of the design's kit exists in the game. Every kit number in the studio is a **proposal** (§3).

## 2. What the document asks for (its words)

| Piece | The document |
|---|---|
| Genre | One-touch flyer. "Superlative: longest range pin point accurate shooting." |
| Flight | "The ship flies incredibly slowly to allow for better aim. If the player wants to get around the map they will need to utilize their Void Sling." |
| Nest / Docking | "The Shrike will dock to any block it runs into. If you're docked, you can press a button to undock. If you aren't docked and you press that button you'll construct a shrike nest. When docked to a nest, it rotates with the shrike allowing them full directional mobility. The blocks on the nest are super shielded. The nest also has a destructible turret on the top that fires missiles if the shrike paints a target. The turret functions as a flora and fires its own blocks. The turret can also be destroyed, revealing its crystal… The turret will generate blocks faster if the Shrike is docked to its nest." "When stationary the trail silhouette turns into a pip zoomed into the reticle. A zoom slider… controllable by the left and right triggers or shoulders. While docked the shrike gets a cooldown reduction bonus on all of its abilities. This bonus is increased on super shielded blocks. Docking anywhere on the nest should snap the shrike to the correct location in the center." |
| Void Lance | "A long range pin point shot that pierces through everything and destroys any block it hits, even if super shielded. Friendly fire is enabled for this shot. If a vessel is hit with this shot it is slowed, has disrupted steering, and has its trail disabled for a short period. The shot has a short cooldown." |
| Void Sling | "A long range shot that slings the Shrike across the map to its target. The Shrike automatically docks to the target upon arrival. The Void Sling cannot tether to crystals or vessels. Void Sling comes with a moderate cooldown. Implemented as a 1 second fixed boost (very big) pointing at the target." |
| Targeting Array | "The Shrike paints a target and homing missiles are launched from each active Shrike Nest, meaning multiple missiles can be launched at once… Each nest plant spends 1 health block per missile. They will not fire their last health block… The Targeting Array prioritizes locking on to enemies… If no enemy is within the lock-on range then the Targeting Array will query the map for volume density and attempt to path find its way into the center of mass. If there is no blocks or enemy vessels within the lock-on range then no missiles will be fired. Enemy vessels that get locked on will have a vignette… The missile speed should be greater than the base speed of any class… [but] their turn radius does not allow them to follow overly complex paths." |
| Elements | Mass "more blocks in the nest & shorter trail wavelength"; Charge "wider radius on void lance"; Space "larger lock-on radius for targeting array (easier to keep target lock)"; Time "shorter cooldown on void sling". No personas, no level-5 upgrades. |
| Play loop | "Collecting crystals gives each nest 3 health blocks." Ark range "Medium = 1 cell diameter"; at the edge "a countdown, and get gently pulled back into range". |
| Trail | "Thin long blocks that get thicker while boosting with the void sling." |
| Minigames | **Den Defense** (defend a pre-built nest from asteroids and fauna; waves every 20 s; score = time survived plus an accuracy bonus; I1 asteroids, I2 + single fauna as boss waves, I3 asteroid sizes and speeds vary with big ones slower, I4 fauna more aggressive) and **Thread the Needle** (sling through windows to collect every crystal, fast; side objective mass destroyed; I1 windows narrowing, I2 a wall-sized panel hides each window, I3 panels then a mass cluster at the last, I4 a cluster at every window). |

## 3. How the studio reads it (every number lab-only)

- **Two locked rules override the document.** PvP is petals only (`Docs/claude/IMPACT_EFFECTS_AND_AUDIO.md`, Garrett
  2026-10-10): the lance's and the missiles' slow / steering / trail-off on vessels are retired; a rival hull hit is one
  scored petal hit (one per victim per 0.05 s). Friendly fire still breaks your own and allied mass. Den Defense is
  co-operative, so its pilots never hit each other.
- **Flight**: cruise 18 u/s (Vessel Config ▸ Flight numbers: the prefab's 60 is the other set), the prefab's turn rates,
  `VesselTransformer.LERP_AMOUNT` 1.5/s for the nose and speed, the gamepad ease `1 − cos(x·π/2)`.
- **Docking**: a touch (hull radius 3) docks. Docked, cooldowns run ×1.6, ×2.5 on a super-shielded block, a nest or an
  anchor plate. One button: docked → undock (a small push off the surface); flying → build a nest round the hull and dock
  in it (cooldown 8 s, at most 3 nests, a fourth retires the oldest).
- **The nest** is read as a **cup**: super-shielded blocks behind and round the hull (12, 32 at Mass 10), the nose's way
  out open, the whole thing turning with the docked Shrike ("rotates with the shrike allowing full directional
  mobility"). Its turret flora sits on top: 4 health blocks, +1 per 6 s (twice as fast while docked in it), +3 per
  crystal, at most 12; destroyed, it drops a crystal anyone can take. The den is the same nest with 40 blocks wrapping
  further forward.
- **Void Lance**: a hitscan beam 2400 u long, radius 1.5 (5 at Charge 10), 1.2 s cooldown (0.48 s docked in a nest). It
  breaks every block it passes, super-shielded included, except the nest you are docked in (you shoot out of your own
  gimbal) and the course's anchor plates (fixtures, §4).
- **Void Sling**: a tether shot (2400 u/s, 1600 u) anchors on the first block it meets (never a crystal or a vessel);
  then the hull flies straight there in exactly 1 s (speed = distance / 1 s, swept every 2 u so it never tunnels), lays a
  thick trail, and docks where it lands. If it runs into anything on the way, it docks there. Cooldown 6 s (2.5 at
  Time 10), reset by a crystal ("resetting your void sling", Thread the Needle).
- **Targeting Array**: hold to paint. Enemies first (rival vessels in Thread the Needle, asteroids and creatures in Den
  Defense) inside the lock-on cone (5°, 14° at Space 10) and 1500 u; an enemy lock is kept to 1.6× the cone. Otherwise
  the block nearest the paint ray, and the centre of mass within 35 u of it. **One missile per nest per painted target**;
  painting again fires a new salvo (studio reading of "paints a target and homing missiles are launched from each active
  nest"). Missiles: 220 u/s, 140°/s turn, an 18 u blast that strips a shield level or breaks a block, a look-ahead
  swerve round blocks that are not the target's mass, homing only while you keep painting. One health block each,
  never the last.
- **Trail**: 0.6 u blocks (2.6 u while slinging), 7 u long, every 8 u (4 at Mass 10).
- **Ark range**: 1000 u from the arena's centre (grown to fit the course); past it a countdown and a gentle pull back.
- **Scope**: docked, the Chase camera moves to the nose with a reticle and zoom ×1 to ×8 (LT/RT, Z/X, the phone's + / −).
- **Bindings** (lab-only; the game has none): RT lance, B sling, hold X paint, Y dock / nest, LT/LB zoom out, RB in.
- **Den Defense**: asteroids from 650 u at 30 u/s (I3+: radius 4–14, big ones ×0.6 speed, small ×1.6), one every
  3.6 s in wave 1, faster each 20 s wave; a creature on every third wave (I2, I3) or second (I4, faster, biting twice as
  often). A den block takes three hits (two shield levels, then the block). Anything reaching the core breaks 3 of its
  blocks (a creature 6); the den falls with a quarter left. Lab-only: a first cut where one core hit ended the run made
  every cell range 75–296 s on five seeds, so it measured luck.
- **Thread the Needle**: six windows 240 u apart, each a frame of 6 u blocks round a window (I1 narrowing 42 → 12 u,
  else 30 → 18), its crystal in the window, an anchor plate 34 u behind it, and the next window out to the side of the
  plate, so a docked Shrike turns and aims every leg. I2: a single wall-sized panel 4 u in front (one lance anywhere
  breaks it). I3/I4: a 20 u ball of 5 u blocks 30 u in front of the window.
- **AI**: input-only. Hard aims with a proportional controller through the inverse stick ease, slings only when the
  hull-thick line through the window is clear, breaks a panel with one lance, tunnels a cluster with a spiral of lance
  shots while building a nest and painting it, and in Den Defense lances the threat with the least time-to-impact plus
  turning time (creatures first), painting only creatures (one asteroid is the lance's). Easy / Medium: `SkimRaceDifficulty.asset`'s late notice (0.5 / 0.25 s)
  and misjudged target (9.9% / 4.5%), plus a lab-only aim error (0.5° / 0.2°). Play styles (the names are the studio's):
  Balanced (all 3), Lancer (Charge 10, never paints), Nester (Mass 10, plants a forward nest in Den Defense), Painter
  (Space 10, paints crowds), Slinger (Time 10, tighter sling lines), Shipped (all 0).

## 4. Scorecard (measured 2026-10-15, median of seeds 11/22/33/44/55, seconds)

Re-run with `node Docs/Studios/Shrike/shrike_score.cjs [--three <local three.min.js>]` (the page's own
`runScorecard` / `runStyles`; the Score buttons give the same numbers).

**Thread the Needle** (time to take all six crystals; lower is better):

| | Easy | Medium | Hard | Hard, no sling | Hard, no array |
|---|---|---|---|---|---|
| I1 | 32.2 | 30.7 | **29.0** | 82.2 | 29.0 |
| I2 | 31.8 | 30.2 | **28.4** | 82.2 | 28.4 |
| I3 | 33.5 | 31.6 | **30.0** | 86.6 | 34.6 |
| I4 | 57.1 | 61.6 | **54.9** | 93.1 | 106.6 |

**Den Defense** (seconds the den survives; higher is better; 300 is the cap):

| | Easy | Medium | Hard | Hard, no sling | Hard, no array |
|---|---|---|---|---|---|
| I1 | 267.2 | 276.0 | **300.0** | 300.0 | 300.0 |
| I2 | **256.7** | 248.0 | 251.0 | 251.0 | 249.5 |
| I3 | 190.4 | 192.1 | **223.3** | 223.3 | 221.7 |
| I4 | 179.5 | 198.2 | **201.4** | 201.4 | 201.4 |

**Styles, Hard, I4**:

| Style | Thread time | Thread missiles / nests | Den survival | Den accuracy | Den missiles |
|---|---|---|---|---|---|
| Balanced | 54.9 | 28 / 2 | 201.4 | 85% | 5 |
| Lancer | 71.6 | 0 / 0 | **218.5** | 91% | 0 |
| Nester | **43.8** | 40 / 3 | 124.4 | 71% | 20 |
| Painter | 44.0 | 44 / 3 | 202.3 | 81% | 6 |
| Slinger | 43.8 | 42 / 3 | 196.8 | 78% | 3 |
| Shipped | 43.8 | 45 / 3 | 196.8 | 78% | 3 |

Checked: Hard is the fastest level on every Thread intensity and the longest-surviving on Den I1, I3 and I4. **Not met
(D24): Den I2, where Easy outlives Hard by 5.7 s**, inside the noise band; reported, not tuned away. The no-sling control equals Hard to the decimal in Den Defense (a drift control: the sling has
no job there) and is 2.8× slower in Thread at I1 (82.2 s against 29.0 s); the no-array control equals Hard where nothing
needs missiles (Thread I1/I2, Den I1, which has no creatures) and moves where something does. The noise band in Den
Defense is about ±20 s per cell on five seeds.

## 5. Findings

1. **The sling earns its place, by a lot** (Thread: 82 s without it against 29 s with it at I1). At 18 u/s the Shrike
   cannot cover a 240 u leg in under 13 s by flying.
2. **The array earns its place only on mass clusters.** Thread I4 takes 107 s without missiles against 55 s with them;
   I3 (one cluster) costs 4.6 s. Everywhere else the lance does the job.
3. **In Den Defense the array is worth nothing measurable.** The no-array control is within 2 s of Hard at every
   intensity (I2 249.5 s against 251.0). The lance is hitscan and cools in 0.48 s in a nest, while painting holds the
   nose on one target until its missiles land, so the lance stops firing. An earlier Hard pilot that also painted
   asteroids lost to Medium for exactly that reason. The Lancer style (never paints) is the best Den style (218.5 s).
   This is a design question, below.
4. **Mass styles win Thread I4, Charge wins Den Defense.** At I4 every missile-using style (Nester, Painter, Slinger,
   Shipped: 3 nests) finishes in about 44 s; Balanced builds 2 nests and takes 55 s; the Lancer (no missiles, a 5 u lance
   that tunnels in one shot) takes 72 s. Balanced being slower than the all-zero Shipped style is not explained yet: it
   builds one nest fewer, and the AI's nest rule ("no usable nest within 700 u") is the likely cause, not its element
   levels.
5. **A forward nest in Den Defense loses the den** (the Nester style: 124 s). The den's mouth turns only while you are
   docked in it; a Shrike defending from another nest leaves the mouth fixed, and asteroids walk in.
6. **Thread I2 is no harder than I1** (28.4 s against 29.0 s): a wall-sized panel costs one lance, and I1's last
   windows are narrower (12 u against 18 u).
7. Found while scoring: a boss-wave test written `wave % 2 === 2` never fired, so I4 had no creatures and its no-array
   control tied Hard exactly (fixed); and the AI let go of the paint the moment it turned to lance, so all but the first missile of each burst flew out
   its life (fixed in the AI, not the rules).

## 6. Decision needed (designer)

- **Painting and the lance**: should a paint hold its lock without the nose on the target (Space "easier to keep
  target lock"), so the lance can work while missiles fly? Today (studio reading) the lock is kept only within 1.6× the
  cone. Finding 3 says the array is not worth its time in Den Defense unless this changes.
- **Salvo rule**: one missile per nest per painted target (studio reading), or a stream while painted?
- **The nest's shape**: a cup that opens toward the nose (studio reading), or a full shell you shoot out of?
- **Flight speed**: 18 u/s is a proposal. "Incredibly slowly" could be slower still; the sling's worth grows with it.
- **The den's loss rule**: the document says only "den is destroyed". The studio wears it down (finding: one-hit loss
  is luck).
- **Level-5 upgrades** for all four elements: open slots; the document gives none.
- **Asteroids against a docked hull** outside the den, and missiles' real path-finding: not modelled yet.

- **The four element minigames** (`/labmaker` §1.5, the owner's rule 2026-10-15: Time a race, Space destruction, Mass
  creation, Charge a vessel-on-vessel dogfight). Round 1 has the Time race (Thread the Needle) and Den Defense (the
  document's defense game, none of the four). Still to build: Space (the document's Bunker Buster concept), Mass (a
  creation game: nests and trail kept standing) and Charge (a Shrike dogfight, petals only).

## 7. Not modelled

The game's prism pools and growth, elemental crystals and the comeback system, the HyperSea ecology (the creatures and
asteroids are lab stand-ins), networking, sound, an asteroid striking a docked hull outside the den, missile
path-finding beyond a look-ahead swerve. Read every number as relative: between levels, styles and kit settings.

## 8. Files

| File | What |
|---|---|
| `Docs/Studios/VesselStudio/shrike.html` | The studio page (one self-contained page; `window.__lab` and `window.__shrikeStudio`) |
| `Docs/Studios/Shrike/shrike_score.cjs` | The scorecard, headless |
| `Docs/Studios/Shrike/build_standalone.py` | Builds the standalone artifact folder from the repo page (`--self-test`); publish `shrike.html` with the scripts as `files`, capabilities `db` + `user`, to https://claude.ai/artifact/JD17sQMeuEUETp6AUPqMmy |
| `Docs/Studios/VesselStudio/studios.json`, `index.html`, `previews/shrike.png` | Its catalog row, hub card and preview |

Gates run 2026-10-15: `verify_lab.cjs` PASS, `check_studio.cjs --hook __shrikeStudio` PASS, `parity_gate.py` ok,
`game_catalog.py --check` up to date, `domain_colors.py --check` agree.
