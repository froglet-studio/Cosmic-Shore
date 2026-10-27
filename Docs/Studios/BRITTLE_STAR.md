# The Brittle Star (the Falcon, reintroduced)

> **Fly it first:** the Brittle Star has a design studio in the Vessel Studio (`Docs/Studios/VesselStudio/brittlestar.html`,
> card "Brittle Star" in the hub, published into https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa). Decide its numbers there,
> record them, then bring them into the game with `/artifact-to-unity` (CLAUDE.md: the artifact is the one source of truth).
>
> **Two records, one vessel.** This file is the research and the studio: what was found, the design document, both rounds of
> the studio and their scorecards. The Unity build's contract (how it flies, every number, how it was made, in-editor
> verification) is `Assets/_Scripts/Controller/Vessel/R_VesselActions/BRITTLE_STAR.md`. The two sessions that built them
> merged on `cece/gallant-lamport-sc3khe` (2026-10-14); the handoff between them is `BRITTLE_STAR_HANDOFF.md`.

This file collects everything that was found about the vessel on 2026-10-14: the design document, what exists on
bleeding-edge, what the never-merged branches built, what the studio models, what its scorecard says, and what it would
take to put the vessel in the game. The team wants it called **the Brittle Star**; `VesselClassType.Falcon = 9` is the same
hull under its old name.

## 1. Status in one paragraph

**Redesigned 2026-10-16 (round 4, §5.4)**: four modes (Speed, Creation, Space's five analog guns with heat, Charge's five
whip cords that stiffen into sabers), one aimed turret in the other modes, and a spare-ring stock instead of the charge
and Overdrive.

**In the game since 2026-10-14 as `VesselClassType.BrittleStar = 16`** (built from the design document with Garrett's
decisions: x6 Speed, Creation the slowest, the five auto-firing arms, a trail per ring corner, a 2.5x ring, the nucleus-wide
ring explosion, a level camera; contract `R_VesselActions/BRITTLE_STAR.md`). **The studio flies that build by default**
(round 2, §5.2) and its numbers reach the game through `studio_to_unity.py`. What follows was written before the build, and
stays as the record of what existed.

**Before 2026-10-14: not in the game.** `Falcon.prefab` on bleeding-edge is a flat copy of an old Manta that cannot spawn (§3). The last
real Brittle Star is `BrittleStar.prefab` on the never-merged branch `origin/claude/falcon-brittlestar-reintro-oyx1q1`
(Philip Appoh, May to July 2026), and it is a Sparrow clone with a five-gun ring (§4). The design document (§2) describes
a different, richer vessel than either. **The studio is the first place the designed vessel exists**: three modes, the
five-turret gun, Ring Detonation, the four element personas and both minigames, flown on the branch's authored flight
numbers (§5).

## 2. The design: "Class - Brittle Star" (Notion)

Source: Notion, Design Documents / Game Design Docs / Class Design Documents / In Frogress / Sparrow Chain / **Class - Brittle
Star** (https://curved-class-545.notion.site/Class-Brittle-Star-d6ee454eebfb42c8a5cf1e017c6146d4, last edited 2026-04-02).
Concept art saved beside this file in `BrittleStar/`.

- **Genre: one-touch flyer.** "A complex one-touch flyer. With different modes concentrated on destruction, creation, and
  maneuverability, the Brittle Star is an all in one tutorial on the more advanced systems of the game." Superlative: *best
  at short-range barrages for pinpoint destruction with minimal investment.*
- **Look** (`BrittleStar/concept_ring_interceptor.jpg`): a ring-mounted interceptor, "but a pentagon instead of a circle.
  Each point of the pentagon is an engine in the back and a gun in the front." The support ring's energy is *suspended,
  not physically connected* (`speed_mode_on.jpg`).
- **Change Modes** (ability icon 1-3): one of three modes at a time.
  - **Speed mode** — "Ignite all thrusters for maximum speed." Raises base speed. Off: only the cockpit thruster burns,
    the outer ones only as the vessel banks; on: all of them at once. It stretches the trail's wavelength so output per
    second does not rise.
  - **Creation mode** — "Engage mass production drives to increase trail size." The trail changes from single small
    blocks flowing from the cockpit to a large pentagon of blocks flowing from the support ring, rotating with the firing
    rotation (`creation_mode_trail.jpg`).
  - **Destruction mode** — "Reroute energy to the vessel's main turrets." Off: projectiles from the cockpit destroy mass
    and knock enemy vessels back, reducing their momentum, each further hit within a brief period knocking further. On:
    all five turrets fire, and hits also disable the enemy's mass production for a while. **Friendly fire is on.**
- **Shooting**: the off-hand button fires. Normal mode fires one turret at a time, chosen by where the finger is dragged
  (with a support ring); Destruction fires all of them. Dragging away from the button's centre widens the spread. On a
  controller: a fire button, and the right stick controls spread.
- **Ring Detonation** (icon 4): eject the support ring forward at high velocity; on impact, or after a time with no
  impact, it explodes, destroying enemy and allied mass in a medium area, then a further blast continues out in the ring's
  plane. Vessels caught are knocked back, slowed and have their trail disabled (*retired by the LOCKED "PvP is petals only"
  rule, which postdates the document: in the studio and the Unity build a vessel hit takes petals only*). A crystal adds one charge (only one held);
  with a charge the ring rebuilds at once; without, the vessel has **no ring and none of its three modes** until a crystal.
  A crystal taken while a charge is held activates **all three modes at once for a short time**. No cooldown: two rings in
  quick succession with a stored charge.
- **Elements** (parameter + persona + AI behaviour; *no level-5 upgrades are designed*):
  | Element | Persona | Parameter | Look | AI behaviour |
  |---|---|---|---|---|
  | Mass | Fabricator (robot) | larger blocks in Creation | larger cockpit | builds mass, clears enemy volume |
  | Charge | Rolling Thunder (Fox / Falco) | larger projectiles, larger ring blast | larger thrusters / guns | destroys clumps of enemy mass, takes close crystals |
  | Space | Hunter (bounty hunter) | longer shot range, harder nudge | longer barrels | shoots players |
  | Time | Zephyr (adrenaline junkie) | bigger Speed-mode bonus | the ring curves into a circle | chases crystals, detonates where there is little allied volume |
- **Ark maximum range**: medium, one cell diameter; at the edge a countdown and a gentle pull back into range.
- **Minigames**:
  - **Demolition Derby** — dodge or destroy obstacles on the way to a crystal; time scores, mass destroyed breaks ties;
    rewards Time and Charge. I1 obstacles only (fly-through), I2 walls and obstacles, I3 more of both plus mass clusters
    past the crystal for a last ring detonation, I4 walls and obstacles super-shielded.
  - **Fauna Frenzy** — build a trail and defend it from fauna; reach the target mass fast, kills break ties; rewards Mass
    and Space. I1 low presence / low aggression ... I4 medium presence / high aggression.
  - Concepts only (not for development): Target Practice (Charge), Bounty Hunt (Space).

## 3. What bleeding-edge has (`VesselClassType.Falcon = 9`)

`Assets/_Prefabs/Spacevessels/Falcon.prefab` is **not a spawnable vessel**:

- It has no `VesselController`, `ActionExecutorRegistry`, impactors, audio or telemetry.
- `VesselStatus.vesselType` is 0 (Random).
- It is absent from `Vessel Prefab Container.asset` and `ToyVesselRoster.Default`, though it is in `DefaultNetworkPrefabs`.
- It has no input bindings. Its four actions are legacy `ShipAction` MonoBehaviours that the current handler cannot bind: `FullAutoAction`, `BoostAction`, `ToggleGyroAction` and `SeedAssemblerConfigurator`.
- The gun's `projectileFactory` is null, and the trail's prism channel is null.
- No skimmer is assigned, so crystals credit nobody.
- There is no camera asset, HUD or ability map.
- The hull is the Manta mesh. Its hull-fusion bakes key the inactive mantis mesh.

The only usable design data is the flight: `SingleStickVesselTransformer` with:

- cruise 10 + 50 = 60 u/s
- boost ×4 → 210 u/s
- pitch / yaw / bank 60 / 40 / 15 °/s, with no speed term

The other Falcon assets are placeholders:

- `_SO_Assets/_TEMP/FalconClassSO.asset` (Class 9, empty description, `isLocked`, UnlockCost 4000; listed in `SO_Classlist_All`)
- four `*FalconTrainingSO` and `SO_Captain_Arcade_Falcon_Freestyle`
- the unused `FalconProjectile.prefab` and `FalconBlock.prefab`

Docs place it as "Planned" (`ABOUT_PROJECT.md`) and "not a candidate" for a studio (`VESSEL_STUDIO_ROLLOUT.md`, now superseded by this studio).

## 4. What the branches built

| Branch | Last commit | What it holds |
|---|---|---|
| `Reintroduce-Falcon` | 2026-04-22, Philip Appoh | 2025-11 to 2026-01 Falcon revival. First `BrittleStar = 12` and a Sparrow-copy BrittleStar prefab (`3e0f4733e`); gun spin transforms, gun-ring prototype, mode-switching fire, Falcon boost |
| `Reintroduce-Falcon-2` | 2026-04-22 | `FalconModeSwitchingFireSO`, `FalconsTrailExecutor` (logs only), `FalconBoostActionExecutor` (broken), empty `FalconHUDVessel`, `FalconCameraSettingsSO` (offset (0,10,−30)); 6-gun ring radius 20 |
| `claude/align-brittlestar-falcon-ZY8cd` | 2026-05-15, Claude | `f677230f1`: repaired `BrittleStarBoostActionExecutor` init and lifecycle (Philip adopted it in `d212749cb`) |
| `Bleeding-Edge-Reintroduce-Falcon` | 2026-07-15, Philip Appoh | **The BrittleStar line**: `506ae8352` "Added BrittleStar", `c57e264c6` "First pass at BrittleSar" (the three BrittleStar scripts, `GunRingTransformer`, registrations), `c244936230` ring-fire asset, `76e9aa413` "Tried fix guns" |
| `claude/falcon-brittlestar-reintro-oyx1q1` | 2026-07-03, Claude | Same BrittleStar code and assets as above; `f012a1b9c` enum test for `BrittleStar = 12`, `ea615857b` `GunRingTransformer` iterates direct children only |

The BrittleStar on those branches (`BrittleStar.prefab`, guid `12ad9964…`):

- **A Sparrow clone.** It has the Sparrow model (`SparrowModel1.fbx`, not the fauna brittlestar mesh), `SparrowPrismController`, `SparrowCameraSettingsSO`, Sparrow's HUD and Sparrow's AI ability list.
- **Flight**: `SingleStickVesselTransformer`.
  - Speed: 10 + 25 = **35 u/s** cruise. `boostMultiplier` 4 gives **110 u/s**.
  - Turning: pitch / yaw **80 °/s**, bank **30 °/s**, each **+0.1 × speed** (`RotationThrottleScaler`).
- **Guns**: five guns on `GunRingTransformer`. The ring spins 20 °/s, and every gun `LookAt`s a focus 70 + 300 |right stick|² u ahead.
- **Rounds**: `FalconProjectile.prefab`, hit radius 0.5, 0.3 s life. Speed is `speedValue` 1500 → 4000 with **Space**, so range is v·2T/π = 286 → 764 u.
- **Input 7 (Button 2)**: `BrittleStarModeSwitchingFire` cycles three behaviours on each press:
  1. ring fire, 2 volleys/s from all five guns, gated to once per 5 s;
  2. "creation", which is null and does nothing;
  3. Speed, which is `BoostAction`.
- **Input 8 (Button 3)**: Sparrow's `FullAutoAction`, 30 rounds/s from all five guns.
- `BrittleStarBoostSO` is wired to nothing. If it were, it would add a velocity shift lasting 1000 s on every press.
- **Trail**: a (2, 2, 5) block every 7 u, 4 u behind.
- **Camera**: (0, 10, −50).
- **Known defects**:
  - The mode-switching SO keeps its cycle on the shared asset (AUDIT §2 #4, still true there).
  - Button 3 and the AI use Sparrow's assets.
  - The branch lacks the audio listener gate every other vessel gained.
  - The pool-injection crash (AUDIT §2 #1) is fixed on HEAD (`ProjectilePoolManager.OnInstanceCreated`).
  - The "`maxSyncPrewarm=8` cap" the AUDIT and BACKLOG rows describe **does not exist**: `git grep maxSyncPrewarm` finds nothing on either branch or on HEAD. Those rows are corrected in place.
- **Registrations made there**:
  - `BrittleStar = 12` (**taken by Scarab on HEAD**: use the next free id, 16)
  - `DefaultNetworkPrefabs` and `Vessel Prefab Container` entries
  - the AppManager / MainMenu dev defaults
  - No class SO, class-list entry, ability map, HUD or telemetry. A fuller "parity" pass (`6cb2a87a8`, branch `intelligent-cannon-2uqj0`) was never merged.

The fauna brittle star on HEAD (`MassBrittlestarFauna.prefab`, `LightFauna` herbivore grazer) is a different object.
`Brittlestar_Export.fbx` is a central disc with five 5-joint arms, about 58 × 56 × 4.6 u span. The arms whip behind the body
through 26 Animation-Rigging damped-transform constraints. It could become a hull, but no vessel uses it.

## 5. The studio (`Docs/Studios/VesselStudio/brittlestar.html`, round 1)

A **design studio** (`/vessel-studio` D2): it models the design document's kit on authored flight numbers.

- **Flight numbers**: a dropdown in Vessel Config switches between two sets. The default is the branch's `BrittleStar.prefab`: cruise 35, Speed 110, turns 80 / 80 / 30 °/s + 0.1·speed, gun 30/s at 1500–4000 u/s, 0.3 s. The other is `Falcon.prefab`. Every number names its source in the page.
- **The kit**:
  - **Three modes**, one at a time, on the gamepad's X / Y / B or keys 1 / 2 / 3.
  - **The gun** fires on RT or A. With the aim stick centred, the cockpit fires; pushed out, the ring turret nearest it fires; in Destruction all five fire, with ×2.5 rounds. **A hit on a vessel takes petals and nothing else** (PvP is petals only, LOCKED: `Docs/claude/IMPACT_EFFECTS_AND_AUDIO.md`); the design's knockback, slow and trail-off on vessels are retired, as on the Unity build.
  - **The aim stick** has two readings, picked in Input:
    - *Spread*: the design's drag-to-spread.
    - *Convergence*: the branch's `GunRingTransformer` focus.
  - **Ring Detonation** on LT: launch, fuse, ball plus planar wave, the crystal charge, overdrive, and no modes without a ring.
- **The hull**:
  - A cockpit dart inside a pentagon of five engine-and-gun pods, held by energy links.
  - The thrusters behave as the design describes.
  - Element looks: Mass cockpit, Charge pods, Space barrels, Time pentagon → circle.
  - **Creation's trail is a hollow pentagonal frame**: one block per edge, turning with the ring.
- **The games** (Game Config ▸ Minigame) are the design's two, at the design's four intensities:
  - **Demolition Derby**: five crystals, each at the end of a corridor (the design's "Ark maximum range" pull-back). The walls span the corridor. I4 walls are super-shielded with one hole to thread, or the ring's blast core strips the shield.
  - **Fauna Frenzy**: lay a target mass while creature brittle stars eat it. You shoot them off it, and crystals charge the ring.
- **AI**: input-only (one stick, aim stick, buttons).
  - Easy and Medium = Hard plus `SkimRaceDifficulty.asset`'s two belief mistakes.
  - Two lab-only Brittle Star mistakes: reading a barricade later (170 / 240 u against 320) and changing mode later (0.7 / 0.35 s).
  - Each persona is a style with its own brain (`STYLES[].brain`): when to Speed, when to clear, when to ring, when to defend.
- **Universal kit**: six tabs, a Players list with + / −, domains, difficulty, play style and View, three cameras, the transport, phone play (left thumb steers, right thumb is the fire button), and AI holds my idle hull.
- **Records**: the Scorecard (levels × intensities + two controls; styles × modes), Runs, About.
- **Lab-only numbers** live in Play Style Config ▸ The kit and Scene Config ▸ Barricades and fauna, each tooltip quoting the design sentence it stands for.

### 5.1 What the scorecard says (2026-10-14, median of seeds 11/22/33, seconds)

Demolition Derby:

| | Easy | Medium | Hard | Hard, no ring | Hard, no modes |
|---|---|---|---|---|---|
| I1 | 29.1 | 27.8 | 28.5 | 28.5 | 94.3 |
| I2 | 30.0 | 29.0 | 29.3 | 29.3 | 95.4 |
| I3 | 30.9 | 31.4 | 30.7 | 30.7 | 95.9 |
| I4 | 55.3 | 59.2 | 54.4 | 54.4 | 107.9 |

Fauna Frenzy (target mass 500 / 650 / 800 / 950):

| | Easy | Medium | Hard | Hard, no ring | Hard, no modes |
|---|---|---|---|---|---|
| I1 | 19.1 | 19.0 | 19.9 | 19.9 | 150.8 |
| I2 | 25.1 | 25.0 | 25.9 | 25.9 | DNF |
| I3 | 32.1 | 33.4 | 33.5 | 33.5 | DNF |
| I4 | 85.0 | 66.2 | 63.8 | 63.8 | DNF |

Styles at Hard, intensity 3 (time · destroyed or killed · rings · share of the run in Speed / Creation / Destruction / overdrive):

| Style | Derby | Frenzy |
|---|---|---|
| Balanced | 30.7 s · 571 · 0 · 33 / 0 / 14 / 53% | 33.5 s · 0 · 0 · 0 / 100 / 0 / 0% |
| Fabricator (Mass) | 56.7 s · 616 · 0 · 0 / 52 / 16 / 32% | 35.3 s · 0 · 0 · 0 / 100 / 0 / 0% |
| Rolling Thunder (Charge) | 47.0 s · 921 · 4 · 48 / 0 / 32 / 0% | 105.7 s · 7 · 0 · 0 / 39 / 61 / 0% |
| Hunter (Space) | 33.5 s · 599 · 0 · 34 / 0 / 12 / 53% | 41.5 s · 2 · 0 · 0 / 78 / 15 / 8% |
| Zephyr (Time) | **25.7 s** · 478 · 0 · 36 / 0 / 12 / 52% | 39.5 s · 2 · 1 · 0 / 83 / 9 / 0% |

**Findings for the designer** (each a question the studio can now answer by playing):

1. **The three modes earn their place by a mile.** "No modes" (the old Falcon: one gun, one trail) is 3× slower in the
   Derby and cannot reach the Frenzy's target past I1.
2. **Overdrive is worth more than Ring Detonation.** A crystal taken while charged gives all three modes at once (6 s,
   lab-only), and the best Derby pilots spend ~half the run in it. Spending the charge on a ring turns the next crystal
   into a plain charge instead, so the Hard pilot never detonates. "Hard, no ring" equals Hard everywhere. The persona
   that rings the most, Rolling Thunder, is the slowest but one. The ring needs a reason to fire: a shorter overdrive, a
   ring that refunds itself on a big burst, or a mode that only the ring opens (the design's I4 sealed walls already do,
   but threading their hole is faster).
3. **Destruction clears a Derby wall faster than the hull reaches it.** 30 rounds/s from five turrets at 1500 u/s erase a
   corridor wall in under a second, so I1-I3 finish within 2 s of each other and the AI levels do not separate there (D24
   is not met on I1-I3; it is on I4 and on the Frenzy's I4). Bigger walls, tougher blocks or a slower Destruction fire rate
   would make the ladder bite.
4. **The personas read as four different pilots**: Zephyr is the fastest Derby pilot and destroys least, Fabricator wins
   the Frenzy, Rolling Thunder destroys the most and finishes late, Hunter turns on rivals. That is the design's intent.
5. **Speed-mode momentum carries into Creation.** The design's "Speed mode does not raise output" rule holds only while in
   Speed; switching to Creation at 110 u/s lays the pentagon frame 3× faster until the speed eases back (1.5/s). Easy, which
   changes mode late, beats Hard on Frenzy I1-I2 for exactly this reason. Decide whether that is a skill or a hole.
6. **Friendly fire bites in the Frenzy**: creatures feed ON your trail, so firing at them shoots your own mass. The AI holds
   fire when its own trail is in the line.

### 5.2 Round 2: the studio flies the Unity build (2026-10-14)

Vessel Config > Flight numbers gained a third set, **Unity build**, and it is the default (the saved-settings key moved to
`v2` so every tester gets it). It ports `BrittleStarExecutor` / `BrittleStarRules` line for line behind one switch
(`SHIP.unity`), and reads every number from the page's `SHIPPED` block, keyed by `BrittleStarConfigSO`'s field names:

- **Modes**: Cruise at the start; A / B / X select Speed / Creation / Destruction and the lit one again returns to Cruise
  (`BrittleStarRules.Press`); a ringless hull keeps its pick, switched off. **Speed** is cruise x6 (x8 at Time 10) on the
  throttle target, **Creation** x0.5, Overdrive both. Trail spacing follows the hull's actual speed over cruise.
- **The ring** turns to the stick's direction in every mode (nearest point, the short way, 540 deg/s). **Destruction**: the
  five arms fire on their own (90 rounds/s each, 1350 u/s; since slowed to 135 u/s for 3 s, the same range), meeting 120 u ahead with the stick in and splaying 25 deg with it
  out (`BrittleStarRules.ArmDirection`). **RT** outside it: 12 rounds/s at 700 u/s (since 70 u/s for 3 s) from the aimed turret or the cockpit, spread
  to 16 deg. **Creation**: five corner trails, the cockpit trail paused.
- **Ring Detonation**: +240 u/s, 1.1 s fuse, contact after 0.06 s; a flat disk in the ring's plane, half-thickness 0.22 x
  Charge (x1 to x4), swept from the ring's radius to the nucleus's (392 u here, lab-only: the game reads the cell) over
  0.7 s in ten sweeps, breaking every prism whose slab it overlaps (own trail under 1 s old spared; shields pop first;
  super-shields refuse) and scoring one petal hit per rival hull. A charge or a crystal rebuilds the ring over 0.5 s;
  Overdrive adds 6 s. Camera (0, 0, -38).
- **Checked**: `check_studio.cjs` passes; a hook-driven check confirms Cruise at start, 45 / 270 / 22.5 u/s, press-again
  Cruise, the ring's 18 deg turn to the nearest point in Destruction, the arms crossing the axis at exactly 120 u, 450 arm
  rounds/s, RT 12/s and none without the trigger, the disk breaking prisms in its plane out to 392 u while sparing ones 5-8 u
  off it and past its edge, and the rebuild landing between 0.3 and 0.6 s. `studio_to_unity.py --vessel brittlestar
  --check`: 35 mapped, 0 drift (a changed `fireRate` reports drift and exits 1).
- **The studio pilot** (lab-only) needed one change for x6: near a crystal it now drops to Cruise (presses the lit mode)
  inside 250 u or its stopping distance, where round 1 rode Speed in. At 297 u/s the 25 u pickup sits inside the turn radius
  and every Hard Derby I4 run orbited a crystal until the 300 s limit.

Scorecard at the Unity build (median of seeds 11/22/33, seconds; `brittlestar_score.cjs`), re-measured 2026-10-15 with the
slow Sparrow rounds (135 / 70 u/s for 3 s; the first round-2 run, at 1350 / 700 u/s, differed only in the Frenzy):

| | Easy | Medium | Hard | Hard, no ring | Hard, no modes |
|---|---|---|---|---|---|
| Derby I1 | 20.2 | 20.1 | 21.1 | 21.1 | 73.9 |
| Derby I2 | 21.6 | 24.1 | 21.3 | 21.3 | 75.0 |
| Derby I3 | 26.4 | 22.5 | 22.0 | 21.6 | 77.8 |
| Derby I4 | 64.7 | 59.7 | 69.8 | 69.8 | 84.7 |
| Frenzy I1 | 26.4 | 26.4 | 26.0 | 26.0 | 78.1 |
| Frenzy I2 | 34.4 | 34.4 | 34.0 | 34.0 | 163.7 |
| Frenzy I3 | 81.0 | 68.9 | 60.9 | 60.7 | DNF |
| Frenzy I4 | DNF | 135.2 | 153.6 | DNF | DNF |

Styles at Hard I3, Derby: Zephyr 18.6 s, Balanced 22.0, Hunter 25.0, Shipped 25.0, Rolling Thunder 36.8 (5 rings), Fabricator
58.3. Frenzy: Shipped 51.3, Hunter 53.9, Fabricator 55.8, Zephyr 58.4, Balanced 60.9, Rolling Thunder 66.4.

**Findings for Garrett** (round 2):

1. **The ring explosion breaks your own work.** The nucleus-wide disk destroys every prism in its plane, your own trail
   included once it is a second old. In the Frenzy that is the target mass: the ring never pays there ("Hard, no ring"
   60.7 against Hard 60.9 on I3; at the first round-2 speeds it beat Hard outright, 61.7 against 76.9). A Domain-Safe
   Ring (the Charge L5 proposal B) or sparing the pilot's own mass would change it.
2. **Creation at half speed slows the Frenzy a lot**: Hard I4 is 154 s against round 1's 64, and Easy does not finish
   it. That is the decision working (Creation is now the deliberate mode), but the Frenzy's targets were set for the old
   pace.
3. **Overdrive still out-pays the ring** in the Derby: Balanced spends 42% of the run in Overdrive and rings once; only
   Rolling Thunder rings often (5 times), and it is the second-slowest style.
4. **The arms make Derby I1-I3 a level race**: walls vanish in a blink at 450 rounds/s, so Easy, Medium and Hard finish
   within 1 to 4 s of each other and the ladder only separates them at I4 (round 1's finding 3, stronger).
5. **Speed momentum into Creation is fixed** (round 1's finding 5): output during the coast-down is capped at the cruise
   rate.
6. **The studio pilot is not the fastest level on Derby I1 / I4 and Frenzy I4** (D24 unmet there; Medium beats Hard on
   both I4s). That is the lab-only
   pilot, not the vessel; the next studio round should re-tune its Speed-in and threading for x6.

### 5.3 Round 3: the five cords (2026-10-15)

Garrett: *"instead of a ton of projectiles lets create 5 cords similar to the thresher's cord but without the weight at
the end. so the 5 cords are still controlled by the right thumbstick and slice through prisms."* Destruction Mode's five
streams of rounds are gone; one cord hangs from each turret (`BrittleStarCord`, contract
`R_VesselActions/BRITTLE_STAR.md` §3.4). The studio's Unity build steps the same model line for line (`stepCords`:
pose spring stiff at the root and soft at the tip, follow-the-leader length cap, 1/120 s sub-steps) and draws each link
as a thin cylinder in the pilot's colour; Vessel Config's Unity sliders gained Cord length / width / root / tip / drag.
In the studio a cord also hits a creature (2 hit points, once per `cordHitCooldown`) — lab-only, the game's fauna die
by their prisms.

Scorecard with the cords (median of seeds 11/22/33, seconds; `brittlestar_score.cjs`, 2026-10-15; round 2's arms in
brackets where they differ):

| | Easy | Medium | Hard | Hard, no ring | Hard, no modes |
|---|---|---|---|---|---|
| Derby I1 | 20.2 | 20.1 | 21.1 | 21.1 | 73.9 |
| Derby I2 | 21.6 | 24.1 | 22.8 (21.3) | 22.8 (21.3) | 75.0 |
| Derby I3 | 24.5 (26.4) | 22.3 (22.5) | 22.0 | 21.6 | 77.8 |
| Derby I4 | 64.7 | 59.7 | 69.8 | 69.8 | 84.7 |
| Frenzy I1 | 26.4 | 26.4 | 26.0 | 26.0 | 78.1 |
| Frenzy I2 | 34.4 | 34.4 | 34.0 | 34.0 | 163.7 |
| Frenzy I3 | 66.8 (81.0) | 78.2 (68.9) | 57.1 (60.9) | 57.1 (60.7) | DNF |
| Frenzy I4 | 148.7 (DNF) | 100.7 (135.2) | 106.8 (153.6) | 74.5 (DNF) | DNF |

Styles at Hard I3, Derby: Balanced 22.0, Zephyr 22.0 (18.6), Hunter 25.6, Shipped 27.0, Rolling Thunder 42.3 (36.8, 5
rings), Fabricator 50.2. Frenzy: Zephyr 55.5, Fabricator 55.8, Balanced 57.1, Hunter 57.9, Shipped 64.0, Rolling Thunder 71.7.

**Findings** (round 3):

1. **The Derby barely moves.** Walls in a corridor fall as fast to five 80 u cords flown through them as to 450 rounds a
   second: I1, I4 and "no modes" are unchanged to the tenth, I2/I3 within 2 s. Round 2's finding 4 (a level race on
   I1-I3) still stands; the cords did not make the ladder bite.
2. **The Frenzy got easier at I3-I4**: Easy now finishes I4 (148.7 s, was DNF) and Hard I4 drops 154 → 107 s. The cords
   kill creatures that cross them without the pilot aiming, and they only cut what they touch, so they no longer pour
   rounds down a line that runs through the pilot's own target mass.
3. **The ring still does not pay in the Frenzy** ("no ring" 74.5 against Hard 106.8 on I4: round 2's finding 1).
4. **Zephyr lost its Derby lead** (18.6 → 22.0 s): at x6 in Overdrive the arms' rounds used to reach far ahead; the cords
   are 80 u, so the fastest pilot now meets walls with less ahead of it. Cord length is the Space dial (x1.6 at Space 10);
   the Hunter's 25.6 s is where a longer reach would show.

### 5.4 Round 4: four modes, analog guns with heat, whip cords, the ring stock (2026-10-16)

Garrett's redesign, built in Unity and in the studio's Unity set in the same change (contract
`R_VesselActions/BRITTLE_STAR.md` §1, §3):

- **Charge Mode** (Y in the game, RB until 2026-10-27; key 4 / the CHARGE touch button in the studio): the five cords become **whips** — a wave equation on
  the link directions (`BrittleStarCord`), inextensible, heavy, 150% long (120 u) — that RT, analog, shortens to 100% (80 u)
  and stiffens into **five sabers**. Using them cools the guns.
- **Space Mode** (X, `Mode.Destruction` in code): the five projectile streams are back, RT's pull setting their rate (up to
  90/s each). **Every other mode** fires the one turret the ring has turned to the aim, no spread.
- **Heat**: each round heats the guns (heat/s = pull × guns); overheated they stop, and in Space Mode the trail lays
  **danger prisms** (drawn red-tinted in the studio). Base gun range 387 u (150%), ×2 at Space 10.
- **Speed Mode**'s trail prisms stretch with their spacing; **every trail lays one smaller block** (1.5 × 1.5 × 3).
- **The ring**: thrown only with a **spare** (one per crystal, two at most); the mounted ring regrows on its own. Overdrive
  and the ringless state are retired.

Checked headlessly on the page's hook (`makeWorld` / `stepWorld`): LT without a spare does nothing; half a pull fires
half the rounds of a full one (225 vs 455 in 1 s); five guns at a full pull overheat in ~3 s, fire nothing while hot and
lay 15 danger prisms in the next 1.5 s; Charge Mode cools them in 0.72 s; the whip is 120 u and the saber 80 u; Charge Mode
fires no rounds; Cruise fires one gun at 88/s with zero spread; three crystals hold two spares; a throw spends one and the
ring regrows in 0.5 s. `check_studio.cjs` passes (no console errors on desktop, 400 px and a phone held sideways).

Scorecard (median of seeds 11/22/33, seconds; `brittlestar_score.cjs`, 2026-10-16; round 3 in brackets):

| | Easy | Medium | Hard | Hard, no ring | Hard, no modes |
|---|---|---|---|---|---|
| Derby I1 | 18.4 (20.2) | 19.3 (20.1) | 18.6 (21.1) | 18.6 | 74.0 |
| Derby I2 | 21.9 (21.6) | 20.4 (24.1) | 22.9 (22.8) | 22.9 | 75.3 |
| Derby I3 | 23.1 (24.5) | 27.6 (22.3) | 28.2 (22.0) | 28.2 | 78.1 |
| Derby I4 | 72.6 (64.7) | 77.5 (59.7) | 84.5 (69.8) | 84.5 | 84.7 |
| Frenzy I1 | 26.4 | 26.4 | 26.0 | 26.0 | 78.3 |
| Frenzy I2 | 34.4 | 34.4 | 34.0 | 34.0 | 157.9 |
| Frenzy I3 | 79.0 (66.8) | 70.6 (78.2) | 54.8 (57.1) | 54.8 | DNF |
| Frenzy I4 | 215.2 (148.7) | 163.6 (100.7) | 110.6 (106.8) | 134.5 (74.5) | DNF |

Styles at Hard I3 (time · destroyed or killed · rings · Speed / Creation / Space / Charge share): Derby — Zephyr 21.9,
Balanced 28.2, Hunter 29.6, Shipped 30.5, Rolling Thunder 40.0 (4 rings), Fabricator 99.7. Frenzy — Balanced 54.8,
Fabricator 56.4, Hunter 57.8, Shipped 57.8, Zephyr 58.7, Rolling Thunder 156.0 (51% in Charge Mode).

**Findings** (round 4):

1. **The ring is rarely thrown now.** It needs a spare, the studio pilot starts with none and spends crystals on the
   course, so "Hard, no ring" equals Hard on every Derby row (only Rolling Thunder throws, 4 times). Starting with one
   spare, or a cheaper first ring, would bring it back into play — Garrett's call.
2. **Derby I4 is now as slow with the modes as without** (84.5 vs 84.7): the sealed walls are super-shielded, neither guns
   nor cords break them, and the ring that strips their shield needs a spare. Round 3's Derby I4 (69.8) leaned on the ring.
3. **Medium and Easy beat Hard on Derby I3 / I4** (D24 unmet there): the studio pilot (lab-only) has not learnt heat — it
   holds a full pull in Space Mode, overheats, and only then swaps to Charge. Teaching it to feather the trigger is the
   next pilot round.
4. **Charge Mode is the strong melee answer in the Frenzy**: Hard I3 54.8 s (57.1); Rolling Thunder, which lives in
   Charge Mode, is slowest because its sabers cut its own target mass (friendly fire) — the cords spare only trail under
   a second old.

## 6. Bringing it into the game (done 2026-10-14)

Built as `VesselClassType.BrittleStar = 16` on `cece/gallant-lamport-sc3khe` (`R_VesselActions/BRITTLE_STAR.md`). What this
section planned, for the record:

- **Enum:** `Falcon = 9` already exists; rename the display to "Brittle Star", or add a new member `16`. Never reuse 12.
- **Prefab:** rebuild from HEAD's Sparrow with a generator, the Stoat / Thresher way (`author_*_assets.py`, with a fresh
  `GlobalObjectIdHash`).
- **Abilities:** per-vessel stateless action SOs. The mode state lives in an executor, never on the SO.
- **Ability map and HUD:** the design maps the four icons to the three modes and Ring Detonation, but **elements → abilities and the
  level-5 upgrades are not designed** (vessel skill §3: do not invent them).
- **Code to port:** `GunRingTransformer` (with the direct-children fix) and `FullAutoActionExecutor.SetGuns`. Its signature
  changed on HEAD, so merge by hand. `FullAutoActionSO.speedValue` is a plain float on HEAD now, so re-author the branch's
  ElementalFloat by hand.
- **Registrations:** the class SO and class lists, `ToyVesselRoster`, Vessel Prefab Container, DefaultNetworkPrefabs, a camera asset, and the tests that enumerate `VesselClassType`.
- **Hull model:** the studio's procedural pentagon hull is ready to port, or the fauna `Brittlestar_Export.fbx` with its damped arm rig could serve.
