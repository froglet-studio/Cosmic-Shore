# The sound language: what Cosmic Shore sounds like (DRAFT, 2026-11-07)

**Drafted from the record, for Charles (the audio owner, `aradia1` in git) and the four to confirm. Nothing here is
decided.** It is the sound twin of the icon language (`.claude/skills/ability-icon/references/ICON_LANGUAGE.md`) and the
colour language (`Docs/VesselModels/COLOR.md`): short rules, measured where the repo can measure, one decision to say yes
to before briefs are written at scale (section 6). It is step 3.5 R0 of `Docs/SKILL_TRAINING_PLAN.md`; the kit it
belongs with (`Tools/Build/sound_slots.py`, the brief generator) is not built yet, so every number here was taken with the
commands in section 7.

Who works here (`who_works_where.py query Docs/AudioSystem "Cosmic Shore"`, 2026-11-07): Charles on `Charles-Land`
("Thresher ability audio", 2026-11-06, unmerged; his "Thresher" landed in #1766 on 2026-11-07), Shombith (merged it),
Yash (the Meerkat's sounds, the boost bed fix). The FMOD Studio project itself (`Cosmic Shore/Cosmic Shore.fspro`) is
Charles's: this page describes it and proposes; it changes nothing in it.

The owner words this draft is built on:

- Garrett, 2026-10-20, on silence: "Let's have all charge based abilities that strip/steal elements also silence charge
  and space attacks for a duration proportional to the elements stripped/stolen ... We want vessels to still use their
  abilities, but those abilities can not harm vessels, fauna, or prisms while silenced." And 2026-10-21, on feeling it:
  "a very subtle camera shake on the onset", and the perimeter that "read reality trying to pull you somewhere you
  don't want to be".
- Garrett, 2026-10-24, LOCKED: "jade is not time. gold is not charge. mass is not blue. space is not ruby."
- Yash, 2026-10-28, on the Meerkat's sounds: "sound effects to the Meerkat's abilities ... one by one ... I will test
  and confirm", then "it's so bad right now ... professional space sounds, not childish ones ... give some common good
  sounds ... options to turn the sound on and off" (`Docs/Studios/VesselStudio/studio-audio.js`).
- The LOCKED FMOD convention (`Docs/claude/IMPACT_EFFECTS_AND_AUDIO.md`): every sound an exposed `EventReference`,
  shipped empty, never a temp event; an empty slot is a visible TODO.

## 0. The bar, in one paragraph

A sound in Cosmic Shore names three things and nothing else: **the act** (what just happened: a skim, a hit, a shot, a
tunnel forming), **the element** it ran on (Mass, Charge, Space or Time, the four the game is made of), and **the
domain** only where the picture already shows it, which is to say never on its own (section 2). A player with their eyes
on the road should know, from the sound alone, what they did and which element it cost or paid; they should never be
told by a sound which team did it, because every peer hears the same hit and the team is a colour. **Silence is a
mechanic, not a gap**: the game already takes a pilot's teeth for 0.4 to 8 seconds on every scored hit
(`Docs/ElementalAbilitySystem/SILENCE.md`), and the sound of that window (its onset, its drain, every refused hit
deflecting) is a sound the game owes and does not have. A sound that cannot be found in the inspector of the thing that
makes it does not exist (section 4). Professional space sound, Yash's bar: sub weight, air, long tails, nothing childish.

## 1. What exists (measured 2026-11-07 on `cece/affectionate-fermi-x1lp81`; section 7 regenerates every number)

### 1.1 The counts

| What | Number | Where it was read |
|---|---|---|
| Events authored in the FMOD project | **72** XML files (24 loops, 48 one-shots; 18 carry a spatialiser, so 18 are 3D and 54 are 2D) | `Cosmic Shore/Metadata/Event/*.xml` |
| Events in the built banks | **70** of the 72 (the two missing: `Base sounds/New Event`, an empty placeholder, and `Gameplay sfx/Rhino one shot`); the bank also holds **one event the repo has no XML for**, `SFX/Loops/Morning Star Right trigger`, whose XML is on `origin/Charles-Land` | GUID bytes in `Cosmic Shore/Build/Desktop/SFX.bank` (37.3 MB) and `Master.bank`, both written 2026-11-06 by Charles's "Thresher" (#1766) |
| `Build/GUIDs.txt` | lists **61** and is a stale export (last written 2026-10-28, #1597): one of its 61, `Loops/Mass brittle star`, is no longer in the bank (`mass birttle`, the same source file, took its place); the plan's "61 banked" came from it | `git log -1 -- "Cosmic Shore/Build/GUIDs.txt"` |
| Source audio | **108** files (98 `.wav` at 44.1 or 48 kHz, 7 `.ogg`, 3 `.mp3`), 0.08 s to 199 s, 470 MB on disk; nine of them are the music's songs (the three measured wavs run 132 to 199 s) | `Cosmic Shore/Assets/` |
| Serialised `EventReference` slots in prefabs, SOs and scenes | **279** (the plan's `Path:` grep counts 277 and 18 distinct paths); **238 empty (85 %)**, 41 filled, pointing at **17 distinct events**, one of them (`Loops/Morning Star Right trigger`, the Thresher's RT) a GUID the repo's XML does not have | the GUID blocks under every `Path:` line in `Assets/**/*.{prefab,asset,unity}` |
| The `AudioSystem`'s shared category slots | wired as Bootstrap scene overrides: 46 categories (12 menu, 34 gameplay) plus the music; **3 empty**: `driftStartEvent`, `driftEndEvent`, `creatureBlockHitEvent` (`CHARLES_TASKS.md` C3) | `Assets/_Scenes/Bootstrap.unity`, `propertyPath: *Event.Path` |
| Banked events the game can reach from some slot or override | **60** of 70; **11 are banked and reachable from nothing**: the four element engine beds `Base sounds/Charge`, `/Mass`, `/Space`, `/Time`, plus `/Layer 2`, `/Stress`, `Loops/Boost Activate 2`, `Loops/Drift sparrow`, `Loops/Layer 2 rhino boost`, `Loops/Mass brittle star 2`, `Loops/Space Tadpole` | the union of the two columns above |
| Events whose FMOD XML holds **no sound at all** | **2**: `Gameplay sfx/Gun Fire` and `Gameplay sfx/Explosion` (no `SingleSound`, no `audioFile`). Both are wired in Bootstrap and fired from code (3 sites each), so every gun and every blast plays an empty event. Charles confirms in FMOD Studio whether the bank disagrees with the XML | the two XMLs |
| Call sites of a shared category in code | 36 of the 37 `GameplaySFXCategory` members are fired somewhere; `CrystalSkim` is fired by nothing; `BoostActivate` by 11 sites (the held bed), `BlockDestroy` 4, `VesselImpact` / `SpeedBurst` / `GunFire` / `Explosion` / `CrystalCollect` / each `Element*Received` 3 | `grep -rn 'GameplaySFXCategory\.'` |

### 1.2 The 70 banked events, by what they are

| Family | Events (name as authored; L = loop, 3D = spatialised) | Source material (length) | Used by |
|---|---|---|---|
| **The engine bed** | `Engine stuff/space ship engine main` (L, 2D; parameters Speed, Energy, Tilt Acel), `... main Rhino` (L, same) | `space ship ambience.wav` 27 s | **21 hulls** carry `ShipAudioController`; 16 wire an event (14 the main, Rhino and Jacks the Rhino twin) and **five fly with an empty `engineEvent`: Manta, Scarab, Serpent, TigerMoth (the Butterfly), Urchin**; the local pilot's hull only |
| **The element beds** | `Base sounds/Charge`, `/Mass`, `/Space`, `/Time` (L, 2D; each takes Speed, Tilt Acel and its element's parameter 0..1; the Mass bed also takes Space); `/Layer 2` and `/Stress` (L; Energy) | Charles's composed beds: Charge 1/2/3 ship (22 s each), Mass ship 1-5 (12-16 s), space ship 2/3/4 (32 s), time 1 / time ship 1/2 (24 s); Layer 2 and Stress from library files (`AEROJet`, `AIRBrst`, `VEHInt` S6k, `Chimey Can Rattle`) | **nothing**: every hull's `additionalEngineLayers` is `[]`, so the four element voices Charles already made never play. `ShipAudioController` pushes Charge/Mass/Space/Time 0..1 from the pilot's bars every frame (`driveElementalParams` on) to the main event, which has no such parameters |
| **Boost** | `Gameplay sfx/Boost Activate` (L despite its folder; Speed), `Loops/Boost Activate 2` (L), `Loops/Layer 2 rhino boost` (L), `Rhino one shot` (not banked) | `Cosmic shore time ship 2.wav` 24 s, `boost rhino cosmic shore.wav` 12 s | `Boost Activate` is the `BoostActivate` category, held as a bed per boost since Yash's fix (`HeldVesselSfx`, 11 sites); the other three nothing |
| **Drift** | `SFX/Drift` (L; Drift amount, Trigger let go), `Loops/Drift dolphin` (L; same), `Loops/Drift sparrow` (L), `Gameplay sfx/trigger off`, `Gameplay sfx/Squirel boost` (Drift amount) | drift hold 3.25 s loop (+ layer 2), `drift let go` 4 s tail, `Cosmic shore Boost.wav` 0.75 s; Squirel boost = `time joust.wav` + a bell | `DriftAudioController` on Squirrel, Rhino, Jacks (`SFX/Drift`) and Dolphin (`Drift dolphin`); `trigger off` is the Dolphin's release, `Squirel boost` the Squirrel's; Drift sparrow nothing |
| **Skim** | `Gameplay sfx/Skim` (3D; a multi-sound of 11 takes: one `skim 3.wav` 0.39 s and ten `cosmic shore skim 3.wav` 1.9 s) | | `boostTickEvent` AND `boostLoopEvent` on Squirrel, Goby, Thresher (C4: a one-shot in the loop slot); the `CrystalSkim` category (fired by nothing) |
| **Creatures** | `Loops/Charge tadpole`, `/Mass Tadpole`, `/Space Tadpole`, `/Time Tadpole`, `/Mass shark`, `/mass birttle`, `/Mass brittle star 2` (all L, 3D), `Gameplay sfx/Massshark spawn` (3D) | tadpoles 16-32 s, shark 16 s, brittle star 16 s, spawn 4 s | `StudioEventEmitter`s on `TadPoleFauna`, `TadpoleSpindle`, `MassSharkFauna`, `MassBrittlestarFauna`, `BeeFauna`; `AudioLoopEvent` on the Mass and Time tadpole lifeform assets. Space Tadpole and brittle star 2 nothing |
| **Crystals and prisms** | `Loops/Goal` (L, 3D, 18 s, 91 % of its energy under 150 Hz), `Gameplay sfx/Crystal Collect` (3D, `block break` 0.62 s), `Creature colide` (3D), `Block Destroy Clip` (3D, `Crystal_Collision_Amplified`), `Energy Gain` | | `Goal` on `TeamCrystal` and `BigCrystalVariant`; `Creature colide` twice on `CrystalTime` (C7) and as the `FloraCollision` category; the rest are categories |
| **The element one-shots** | `Charge Joust`, `Mass joust`, `Space Joust`, `Time Joust` (2D, 1.5-2 s); `Charge comeback`, `masss comeback'`, `space comeback`, `time comeback` (2.5-4 s); `charge Joust friend 2`, `mass Joust friend`, `Space Joust friend`, `time Joust friend` (3.5-4 s) | Charles's per-element stings | the names say joust; the game plays `X Joust` on **an elemental crystal pickup** (`ElementXReceived`, local pilot only), `X comeback` on the comeback band, `X Joust friend` when an ally is overtaken and buffed |
| **Hits** | `Vessel impact` (3D, `ship impact` 0.38 s, 89 % under 150 Hz), `Track collide` (3D), `Creature Death`, `Mine Explode` (`END GAME crash.mp3`), `Explosion` (**empty**), `Gun Fire` (**empty**), `Projectile Launch` (3D, `laser 2` 1.57 s), `Shield Activate` / `Deactivate`, `Speed burst` | | all categories: `VesselImpactor` (vessel-prism and track contact, positional), `AOEExplosion` (every blast prefab), `FireGunActionExecutor` and the Grizzly's two guns, `Projectile`, `PrismStateManager`, `Mine`, `LifeForm` |
| **The Thresher** | `Loops/Morningstar loop` (L, 3D, 12 s, 96 % under 150 Hz), `Morning Star Right trigger` (in the bank; XML on Charles-Land with `Morning Star Left Trigger` and three `morning star ... release/swinging` wavs) | | `ThresherAudioController.morningStarLoop` and `rightTriggerEvent` on `Thresher.prefab`: the fleet's first ability voice, 2026-11-06 |
| **Match and menu** | `Game End`, `Score Reveal`, `Pause Open` / `Close`; `Menu/Option Click`, `OpenView`, `SwitchView`, `Close View`, `SmallReward`, `BigReward`, `Upgrade`, `Denied`, `Confirmed`, `LetsGo`, `SwitchScreen`, `RedeemTicket` | 0.12 s (click) to 5.35 s (reward) | the 12 `MenuAudioCategory` members, 37 call sites (`OptionClick` 13, `Confirmed` 8, `LetsGo` 5, `Denied` 5 ...) |
| **Music** | `Music/Music` (L, a multi-sound of nine tracks) | nine songs (the three measured wavs 132-199 s) | `AudioSystem.musicEvent` (Bootstrap override); `Soundtrack.asset` has nine per-track slots, all empty, so the player's playlist runs in LEGACY mode (`SoundtrackPlayer`) |

### 1.3 The empty families (the 238), by owner

| Owner | Empty slots | What they are |
|---|---|---|
| Honeyguide (`HoneyguideConfig.asset`) | 25 | the whole crew kit: flit, jump, slip, spin, hive enter/leave/sting/colour/chain, pattern made, reveal, upgrade |
| Rhino sword combos (`SwordfishSwordComboLibrary.asset`) | 24 | one `Sound` per tap string, RR through LRR, plain and energized |
| Callsigns (`Resources/Pilots/CallsignSet.asset`) | 18 | `openSound` per callsign |
| Brittle Star (`BrittleStarConfig.asset`) | 14 | fire, explosive round, saber throw, ring launch/blast/rebuild, cord slice, the three modes on/off, overheat, charge stored |
| Shrike (`ShrikeConfig.asset`) | 11 | lance, lock, dock, undock, nest, sling, tether, missile launch/blast, mode, crystal |
| Thresher (`Thresher.prefab`) | 9 | LT press, LT release, RT release (the controller); smash, crush, bounce, slice, plant, yank (the executor) |
| Soundtrack (`Soundtrack.asset`) | 9 | the per-track events the playlist waits on |
| Meerkat (`MeerkatSolitonConfig.asset`) | 8 | form, collapse, network, portal, prism fired, sentinel placed/lost, zap: Yash's "one by one" (section 3, wormhole) |
| Orca crew (`OrcaCrewConfig.asset`) | 8 | missile launch/burst, bomb throw/burst, sniper shot, shield raise, omni refill, empty magazine |
| Wrasse kite (`KiteNavigatorConfig.asset`) | 8 | charge, loop closed/fizzled, prism popped, mass cluster, steal, fauna struck, time boost |
| Stoat (`StoatSkim/Sling/Dipole/RippleConfig` + the two AI-policy copies of each) | 17 + 12 | skip, graze, plunk, capture, launch, chain break, sweet spot; hold start, sling, trampoline touch/bounce; open, annihilate, warp; ripple, strip |
| UI (`Menu_Main` 7, `Profile`, `ModalWindows`, `CountdownTimer`) | 10 | the four C9 sounds: countdown beat, typing tick, currency burst fired/landed |
| Swarm fauna (6 configs) and bees | 12 + 6 | `MorphEvent`, `SwarmLoopEvent` per swarm; pollinate, sting, hive grow; the bee lifeforms' `AudioLoopEvent` |
| Lifeform and cell overrides (`AudioLoopEvent`) | 19 | the Charge and Space tadpole assets, 16 per-mode tadpole/fauna config copies, `ArkReactor` |
| Payload, artifacts | 5 + 8 | grab, release, refused, artifact taken/stowed; each artifact's load/release |
| Jacks, Grizzly, Serpent, Scarab, Urchin, TigerMoth, Manta, Rhino, MorningStar | 4, 2, 2, 2, 1, 1, 1, 1, 1 | call/decoy/teleport and the drift release; bomb fire/detonate; the engine and the sniper's `fireEvent`; the engine and the juke whoosh; the engine; the engine; the engine; the drift release; the ball's emitter |

Slots that do not exist yet, so cannot be empty: the Sparrow's **skyburst launch** (DT-004, `Docs/QA/DEV_TASKS.md`: no file under `R_VesselActions/` with skyburst in its name declares an `EventReference`), **silence** (`SilenceScreenFeedback` and `VesselSilenceConfig` carry a shake and a perimeter, no sound), the **petal take** itself (strip, steal, eject and burn all settle in `ElementalTransfer` with no voice), the Butterfly's **dust** and wings (five `ButterflyDustField` slots and `wingbeatEvent` in code; `TigerMoth.prefab` serialises one).

### 1.4 The four classes of mis-wiring, where each stands

| Class (`CHARLES_TASKS.md`) | Then | Now (2026-11-07) |
|---|---|---|
| C2 a looping event fired as a one-shot (boost silent) | every boost logged NOT PLAYED | the code half landed (Yash, 2026-10-29: `AudioSystem.StartHeldGameplaySFX`, `HeldVesselSfx`, one owned 2D bed per boost, local pilot only, stopped with fade-out on every end path). Charles's half stands: `Boost Activate` stays a Speed-driven bed, or becomes a sting; `CHARLES_TASKS.md` C2 is not updated yet |
| C3 three empty `AudioSystem` slots | drift start/end, creature block hit | unchanged (the drift's own controller covers four hulls; `CreatureBlockHit` fires from `Prism.cs` into an empty slot) |
| C4 a one-shot in a loop slot | Squirrel `boostLoopEvent` = `Skim` | unchanged; Goby and Thresher carry the same wiring |
| C7 two `Creature colide` emitters on `CrystalTime` | | unchanged |
| new: two empty FMOD shells | | `Gun Fire` and `Explosion` have no sound in their XML (1.1) |
| new: the bank and the repo disagree | | the bank is one commit ahead of the XML (the Thresher's RT), `GUIDs.txt` is ten days behind both; `/sound_slots.py --check` should read the bank bytes, never the export |
| licences (`Docs/LAUNCH/READINESS.md` #10) | | the `Stress` and `Layer 2` engine beds are built from Sound Ideas library files (`S6k` names) in the public tree; the FMOD tier is undecided |

## 2. One meaning per family

### 2.1 The four elements each get a sonic signature

The colour language gives each hue family one meaning. The sound language does the same with the four elements, and it
starts from a fact the audit turned up: **Charles already composed one engine bed per element** (`Base sounds/Charge`,
`/Mass`, `/Space`, `/Time`, 12 to 32 s each, gated by that element's own 0..1 parameter) and nothing in the game plays
them. The signatures below were **measured from those files, not heard** (section 7 has the script; centroid is the
median spectral centre, bass is the share of energy under 150 Hz, onsets are transients per second). Each reading is a
proposal until Charles and the four say it is the intent, and a listening session beats the table.

| Element | What it is in the game | Measured in Charles's material | Proposed signature (what a designer does) | Reading |
|---|---|---|---|---|
| **Mass** | substance: the trail, the prisms laid and grown, armour, what a round is made of (`Docs/ELEMENTAL_ECONOMY.md`; "MASS owns the SUBSTANCE of what you fire") | the beds sit at 74-266 Hz centroid (four of five), 13-61 % of their energy under 150 Hz, **no** energy from 2 kHz up; the Mass stings are the heaviest sounds in the project (98-119 Hz, 66-92 % bass) | **weight**: sub and low-mid, slow attack, a thud or a press, no air. Mass grows: more Mass is more low end and a longer tail, never a higher pitch | supported by the files |
| **Charge** | the attack verb: strip, steal, the sniper, the joust; and silence rides on it ("all charge based abilities that strip/steal elements also silence") | three of its four beds are the brightest in the set (1313-1900 Hz centroid; the fourth sits at 519 Hz), 3-6 transients a second, pulsing with silences between; the Charge tadpole is the brightest loop at 4.1 kHz with a third of its energy above 6 kHz | **crackle**: arcs, bolts, a static bed that pulses; short bright transients on a hit. More Charge is denser crackle, not louder | supported by the files |
| **Space** | reach and the field: the skimmer's radius, pierce, the scope, the wormhole, the soliton (Yash's tunnels are Space) | the smoothest beds (0-1.3 transients a second, level steady), 283-1015 Hz; the Space tadpole is the smoothest loop in the project (0.2 a second) | **hollow sustain**: whoosh, wind through a bore, a pad with no attack, slow risers and falls. More Space is wider (stereo, reverb), not louder | supported by the files |
| **Time** | speed and the clock: boost, dash, the drift, cooldown, the fuel pellets, the countdown; 1 s per petal of silence | mixed beds (256-1284 Hz, 0.3-4.5 transients a second); its joust and ally stings are among the spikiest in the set (crest 12-14), and the Time tadpole ticks (2.3 a second, a quarter of its energy at 2-6 kHz) | **tick**: a clock, a shutter, a metronome under the boost; a sound with a RATE the game drives. More Time is faster, never higher | proposal: the one-shots say tick, the beds do not yet agree |

Two rules that follow:

- **The element is the texture, never the pitch or the colour.** A level is told by density (Charge), weight (Mass),
  width (Space) or rate (Time), so the four scale without meeting. `ShipAudioController` already drives each element
  0..1 from the bar with 6/s smoothing (`elementSmoothing`): wire the four beds as `additionalEngineLayers` on one hull
  and the fleet has its first element voice for free. Which hull first is the owner's call (section 6).
- **Every act sounds like its element.** A Mass skim is a thud-skim, a Time boost ticks, a Space tunnel breathes, a
  Charge hit crackles. The act (section 3) gives the shape; the element gives the texture. `ElementXReceived`,
  `ComebackX` and `JoustBuffX` are already four events each: the first place the rule is testable.

### 2.2 The six colour families are NOT sound families

The colour language (`COLOR.md` §1, `COLOR_DOMAINS.md`) has six families with one meaning each: **Blue** is neutral,
**Fire** is danger, **Lime** is the call to action, **Ruby, Gold and Jade** are the three domains. None of them is a
sound, for three reasons:

1. **Colour says WHO, sound says WHAT.** Three of the six are teams. Every peer in a match hears the same hit, and a
   pilot's own team changes from match to match; a sound that said Ruby would be a different sound for the same act on
   another day. The team is told by the picture (the trail, the plates, the flower above the card) and never by the
   speaker. So: **no event is ever named for, or voiced for, a domain.** There are 12 element stings in the project and
   zero domain stings, and that is right.
2. **Elements have no colour (LOCKED, Garrett 2026-10-24)**, so sound is the only channel besides shape that can name
   the element. Giving the colour families a sound would spend that channel on something the picture already says.
3. **Two of the six are roles, and roles ARE acts.** Fire means danger: the danger prism's burn, the deflection of a
   refused hit, the onset of silence. Lime means the call to action: a crystal to collect, a ready button. Those two
   meet the sound language as acts (section 3: hit, crystal, silence), and the proposal is that they keep one voice each
   across the game the way the colour keeps one hue, so a player learns "that is danger" and "that is reward" once.
   Blue (neutral) is the engine bed under everything: it has no act, so it has no sting.

## 3. The act vocabulary

The acts a player hears first, in order (the plan's brief order): the engine, boost, skim, drift, hit, then the ability
acts. Each row: what fires it in code, how long it is (measured from the executors and the files), the parameters the
code already drives, who hears it, which hulls share it, and what plays today.

| Act | Trigger (code) | Duration | Parameters the code drives | Who hears it | Hulls | Today |
|---|---|---|---|---|---|---|
| **Engine** (the bed) | `ShipAudioController` creates the instance when the hull is the local pilot's; positioned before `start()` | the whole flight; `space ship ambience.wav` loops at 27.4 s | `Speed` 40..100 from 0..100 u/s (`paramAtZeroVelocity` 40, `paramAtMaxVelocity` 100, smoothing 6/s), `Tilt Acel` -100..100 from pitch+yaw rate up to 180 deg/s, a drift slowdown x0.6, `Charge`/`Mass`/`Space`/`Time` 0..1 from the bars; `extraParameters` for static values such as `Energy` (empty on every hull) | the local pilot only (`onlyAudibleToController`), 2D | 21 hulls carry it, 16 wire it | plays on 16 hulls; five engines are empty (1.2); the element layers are authored and unwired (2.1) |
| **Boost** | `HeldVesselSfx.Begin` on engage, `End` on every end path (natural end, cancel, turn end, `OnDisable`); five boost kinds: Boost, ChargeBoost, RampBoost, ConsumeBoost (the Serpent's pellets), the Manta's analog turn boost | as long as the boost: ConsumeBoost 6 s x the Time multiplier, the others while held; the bed file is 24 s | `Speed` (the engine's), re-trigger replaces | the local pilot only, 2D, max-instances 1 | the fleet | the `Boost Activate` bed since 2026-10-29; the proposal is a Time tick on top (2.1) |
| **Skim** | `ProximityBoostAudioController` fires `boostTickEvent` on every rising edge of `BoostMultiplier` (every prism or crystal the skimmer passes), never closer than `minTickInterval` 0.07 s (about 14 a second), attached to the ship; `boostLoopEvent` is created on the first rising edge and released when boost decays to base | one-shot 0.39-1.9 s; dense skimming is a train | `Boost Amount` 0..1 on the loop (base..max multiplier, smoothing 12/s) | 3D at the hull | Squirrel, Goby, Thresher carry it; every other skimmer is silent | the `Skim` one-shot in both slots (C4); its haptic twin is the 70 ms skim pulse (`Docs/HAPTICS.md`), a separate effect on the same contact |
| **Drift** | `DriftAudioController`: start the loop when `IsDrifting` goes true; push `Drift amount` 0 (single) .. 1 (double) each frame; on release push 1 (the "let go" stage), hold `releaseHoldSeconds` 0.4 s, then stop; `releaseEvent` one-shot | held while the trigger is held; hold loop 3.25 s, let-go tail 4 s | `Drift amount`, `Trigger let go`; volume follows the slip angle 0..180 deg (quiet at 0, full at 180) | the local pilot only | Squirrel, Rhino, Jacks (`SFX/Drift`), Dolphin (`Drift dolphin`) | plays on four hulls; the legacy `DriftStart`/`DriftEnd` categories (`DriftActionSO`, 2 sites) are empty; the drift rumble haptic (0..0.3 by slip) is driven by the same component |
| **Hit** (petals: PvP is petals only) | every scored hit admits through `CombatHitDrain.TryAdmit`; today's sounds are at the contact instead: `VesselImpactor` plays `VesselImpact` / `TrackImpact` on body contact (positional), the Squirrel's overtake plays `JoustScored` / `JoustReceived` | `ship impact` 0.38 s; the joust stings 1.5-2 s | none | 3D at the contact: everyone nearby hears a body hit; the joust stings are 2D | the fleet | **no sound says "I took petals" or "I lost petals"**: the take settles in `ElementalTransfer` with no slot; the Thresher's six impact slots and the Stoat's strip slot are empty. Proposal: one `petalsTaken` / `petalsLost` pair on the gate, per element (2.1), and the Fire voice for a burn. Haptic twin: the 200 ms punish thud on body-prism contact |
| **Shot** | `FireGunActionExecutor` and the Grizzly's two guns play `GunFire`; `Projectile` plays `ProjectileLaunch`; the Sparrow's full auto runs a 30 s heat ladder (six 5 s phases, `SPARROW_SPRAY_ACCURACY.md`) | `laser 2` 1.57 s per round | none; the heat is on the haptic (the spray buzz 0.15..1.0) and not on the sound | 2D for the category, 3D for the launch | Sparrow, Grizzly, Brittle Star, Serpent, Orca, Shrike, Urchin | `Gun Fire` is an empty shell (1.1); the skyburst has no slot (DT-004); the Serpent's rifle, the Brittle Star's 14, the Orca's 8 are empty |
| **Blast** | `AOEExplosion` plays `Explosion` at the blast (a per-prefab toggle); `Mine` plays `MineExplode`; the Grizzly bomb, Manta sting, Shrike missile have their own slots | the blast's `ExplosionDuration` 2 s; `explosion.wav` 4 s (69 % under 150 Hz) | none; the blast's radius and tier are known to the prefab and could be | 3D at the blast | every blast prefab (the Dolphin cone, the Scarab plate, the skyburst warhead, the Grizzly, the Manta bloom ...) | `Explosion` is an empty shell (1.1); `Mine Explode` plays `END GAME crash.mp3` |
| **Crystal** | `VesselImpactor` and `Crystal.cs` play `CrystalCollect` at the crystal; the pickup effect plays `ElementXReceived` for the local pilot only; `TeamCrystal` / `BigCrystal` carry the `Goal` loop; the omni crystal lights the omni card (`VesselHUDView.PlayOmniCrystalCollected`) | `block break` 0.62 s + a 1.5-2 s element sting; `Goal` 18 s loop | none | collect 3D, the sting 2D local | the fleet | plays, twice per elemental crystal (a generic collect plus the element sting): the first act that already follows 2.1. The omni crystal has no sound |
| **Spawn** (life) | `StudioEventEmitter` on each creature prefab (loop, `ObjectDestroy` stop), `Massshark spawn` on spawn; `LifeForm` plays `CreatureDeath`; `HealthPrism` plays `FloraCollision`; `Prism.cs` plays `CreatureBlockHit` (empty) | the loops live with the creature (16-32 s files); spawn 4 s | none; `StopEventsOutsideMaxDistance` is on, so only creatures inside each event's max distance hold a voice (C5), and builds inherit 32 real channels (C6) | 3D | tadpoles (one loop per element), shark, brittle star, bees; the six swarm configs and the Space tadpole are silent | the per-element tadpole loops are the second place 2.1 is already true |
| **Silence** | `VesselSilence.OnHitAdmitted` on the machine that decides the hit; `SilenceScreenFeedback` shakes the camera 0.45 for 0.35 s on onset and 0.2 for 0.15 s on each extending hit; the perimeter's depth = seconds left / 8 | **0.4 s** (a Sparrow bullet) to **8 s** (the cap); a Strike 3.2 s, a missile blast 8 s | the seconds left (the perimeter already follows them within 0.25 s); each hit's extension | the silenced local pilot only; a rival's silence shows and sounds nothing on your screen | the fleet | **no slot**. Proposal: three slots on `VesselSilenceConfig.asset` under Feedback: `onsetEvent`, a held `windowEvent` whose one parameter is the depth 0..1 (drains with the seconds, like the perimeter), `liftEvent`; plus the deflection: a refused hit already plays the super-shield wobble, and the Fire voice belongs on it. Garrett's words for the picture ("reality trying to pull you somewhere you don't want to be") are the brief for the bed |
| **Wormhole** (Space) | the Butterfly's mouth plays `TransitEvent` at the exit of a transit (`WormholeMouth`); the Meerkat's executor plays `FormEvent` when a tunnel forms, `CollapseEvent` when it collapses, `NetworkEvent` on the omni network, `PortalEvent`, `PrismFiredEvent`, `SentinelPlaced` / `Lost`, `ZapEvent` (rate-limited, about 5 a second); the Stoat's `OpenEvent`, `AnnihilateEvent`, `WarpEvent` | a mouth blooms over `BloomSeconds` and retires over 0.5 s; a tunnel lives for the ride | none in Unity yet; the studio brief has a reticle hum that rises with the bend and a ride rush that follows speed (loops), the rest one-shots | 3D at the mouth, the tunnel, the sentinel | Butterfly (TigerMoth), Meerkat, Stoat | all empty. Yash's studio (`Docs/Studios/MEERKAT.md` § sounds, `studio-audio.js`) is the brief: sub impacts and swells, airy whooshes and risers, soft bells with long tails, pads, one shared 4 s reverb, "no square-wave beeps or arpeggios", the ride "deep bass with a rare high glint" |
| **Menu and match** | `PlayMenuAudio` (37 sites), `CountdownTimer` one beat per second (3, 2, 1, GO), `ProfileModal` one tick per character about 75 ms apart, `IconEmitter` fired/landed | 0.12-5.35 s | none | 2D | | the 12 menu events play; the four C9 slots are empty |

What the table says about the kit to come: a brief for an empty slot is one row of this table filled in for that slot,
plus the element (2.1), plus the sibling events it has to sit beside. The Meerkat's eight slots (R1) have every column
already.

## 4. The slot rules (the LOCKED convention, restated for sound design)

1. **Every sound is an exposed field on the thing that makes it**, an `EventReference` in the inspector of that prefab,
   component or SO. A sound an audio designer cannot find in the component view of the thing that produces it does not
   exist. A shared category (`GameplaySFXCategory`, `MenuAudioCategory`) is only for a sound that must be identical
   everywhere (prism destroy, crystal collect, the menu's click); one vessel's one ability never goes through a category.
2. **Shipped empty, never a temp event.** An empty slot is silent and warns once; a borrowed event is an invisible TODO
   that survives to release. The 238 empties are the inventory, not the shame; the `Skim` one-shot sitting in three loop
   slots (C4) is the thing to remove.
3. **A looping event belongs in an instance slot, a one-shot in a one-shot slot.** The code tells the two apart and
   refuses the wrong one (`FMODOneShotVolumeHelper.RejectLoopingOneShot`): a loop fired as a one-shot leaks or is refused
   (C2, the silent boost), a one-shot in a loop slot plays twice (C4). Instance slots today: the engine, the drift, the
   proximity boost loop, the held boost bed, the Thresher's morning star and trigger presses, every creature emitter, the
   `Goal` loop, the music. Everything else is a one-shot. A new ability with a held state gets a press slot and a release
   slot (the Thresher's pattern); a one-shot that should tail may be authored either way on a press slot.
4. **The event's parameters are the game's numbers, driven by code, named on the slot.** `Speed`, `Tilt Acel`, `Drift
   amount`, `Trigger let go`, `Boost Amount`, `Ball Speed`, `Ball Hot`, `Trigger Depth`, and the four elements 0..1.
   A parameter the code does not drive is an unused knob, and a parameter name the event does not expose is silently
   ignored (`setParameterByID` resolves once and gives up), so the slot's tooltip names the parameter and the brief names
   its range.
5. **Who hears it is a rule of the slot, not of the mix.** A vessel's own loops (engine, drift, boost, proximity, the
   Thresher's) play for the local pilot only and 2D; a contact one-shot plays 3D at the contact for everyone nearby; a
   reward sting (`ElementXReceived`) plays 2D for the pilot it rewards. A remote or AI hull is heard through its 3D
   one-shots and never through its loops.
6. **Three sliders, one mapping.** Music, SFX and Haptics (`OptionsMenuContent`, 0..1, `GameSetting`, roaming,
   last-writer-wins). Every instance the code creates resolves its volume through `AudioSystem.ResolveSfxInstanceVolume`
   / `ResolveMusicInstanceVolume`; an emitter authored on a prefab is covered only once Charles routes the buses through
   the VCAs (C1) and `driveFmodVcas` is switched on, after which every resolver collapses to its trim. Haptics ride the
   same contacts as the sounds (skim pulse / punish thud / drift rumble) on their own slider and their own priority order.
7. **Create, attach, release through `FmodSafe`; play through `AudioSystem` or `FMODOneShotVolumeHelper`;** never
   `RuntimeManager.PlayOneShot`, never a string event path, never a fallback to another event.
8. **A log channel, not the console.** A slot that warns does so once; nothing per frame or per contact logs.

## 5. Mobile (what is known, what is not)

- **One platform in FMOD Studio: Desktop** (`Cosmic Shore/Metadata/Platform/`, one file). Unity's FMOD settings list
  twelve platform entries (Windows, Mac, Linux, Android, iOS, tvOS, visionOS, WebGL, the Mobile High and Low groups,
  Play In Editor, default) and all of them inherit `default` with `BuildDirectory` unset, whose default in the plugin is
  `"Desktop"` (`Platform.cs`). So **every platform loads the Desktop banks**: `SFX.bank` 37.3 MB raw (no per-platform
  encoding, no smaller mobile bank), `Master.bank` 1.9 KB.
- **The web Toybox already pays for it**: `Docs/LAUNCH/READINESS.md` measures a 114 MiB load with the 37 MB bank
  uncompressed and "Loading sound..." waiting up to 90 s; banks are served raw while the rest is Brotli.
- **Channels**: Play In Editor is 1024 virtual / 256 real; every other platform inherits FMOD's default 32 real channels
  (C6), so a phone mixes a quarter of what the editor does, and Wildlife Liberation's 500-1200 creatures lean on
  `StopEventsOutsideMaxDistance` and the per-event max distances Charles has not yet checked (C5).
- **Haptics on a phone are silent by design** (`Docs/HAPTICS.md`): a pad on a phone rumbles, the phone itself does not.
- **Unknown, and nobody has decided**: a mobile bank size budget; whether a Mobile platform (Vorbis or FADPCM, a lower
  sample rate, a smaller max-distance table) is wanted in FMOD Studio before the mobile launch; a memory budget for the
  banks on a low phone; whether the music ships in the bank at all on mobile (nine 132-199 s tracks); the FMOD licence
  tier and the sound-library files in the public tree (`READINESS.md` #10). This is D5's second half.

## 6. The one decision to confirm

Before a single brief is written at scale, Charles and the four say yes or no to one sentence:

> **A sound names the act and the element, never the domain: the act gives its shape (section 3), the element gives its
> texture (section 2.1: Mass is weight, Charge is crackle, Space is hollow sustain, Time is a tick), the four element beds
> Charles already composed are the fleet's element voices, and no event is ever named for or voiced for Ruby, Gold or
> Jade.**

If yes, three smaller calls ride on it, each a line to answer:

1. **Time's reading.** Tick (a rate the game drives) or something else: the one-shots say tick, the beds do not yet.
2. **Silence gets a sound** (three slots on `VesselSilenceConfig.asset`, the depth as its one parameter, the Fire voice
   on every deflected hit), and the petal take gets one pair per element. Garrett decides; it is his mechanic.
3. **Briefs only, or synthesised reference stems too** (D5, Charles with Garrett and Shombith), and which hull wires the
   element beds first (the Squirrel, whose drift and skim already play, is the cheapest test).

If no, the draft comes back with the words that changed it, and section 2 is rewritten before anything else.

## 7. Regenerate (every number above)

```
# the authored events, loops, 3D
ls "Cosmic Shore/Metadata/Event/"*.xml | wc -l
grep -l 'class="LoopRegion"' "Cosmic Shore/Metadata/Event/"*.xml | wc -l
grep -l 'SpatialiserEffect' "Cosmic Shore/Metadata/Event/"*.xml | wc -l
# an event's name, sounds and parameters (names are on the line after <property name="name">)
grep -A1 'name="name"' "Cosmic Shore/Metadata/Event/{59f0cd35-6a01-4476-9879-318f40186599}.xml" | head -2
# the stale export against the bank: GUIDs.txt and its date
grep -c 'event:/' "Cosmic Shore/Build/GUIDs.txt"; git log -1 --format=%cd -- "Cosmic Shore/Build/GUIDs.txt"
git log -1 --format='%cd %s' -- "Cosmic Shore/Build/Desktop/SFX.bank"
# is an event in the built bank: its GUID as FMOD writes it (<IHH + the last 8 bytes) must be in the bank
python3 -c "import struct;h='14b0c00aa3d1471a9a66e4601e702e07';b=struct.pack('<IHH',int(h[:8],16),int(h[8:12],16),int(h[12:16],16))+bytes.fromhex(h[16:]);print(b in open('Cosmic Shore/Build/Desktop/SFX.bank','rb').read())"
# the slots: all, empty, filled by event, distinct
grep -rhE '^\s+Path: ' --include=*.prefab --include=*.asset --include=*.unity Assets | wc -l
grep -rhE '^\s+Path: *$' --include=*.prefab --include=*.asset --include=*.unity Assets | wc -l
grep -rhE '^\s+Path: event:/' --include=*.prefab --include=*.asset --include=*.unity Assets | sort | uniq -c | sort -rn
# the AudioSystem's category wiring (Bootstrap overrides) and its empties
grep -n 'propertyPath: \w*Event\.Path' -A1 Assets/_Scenes/Bootstrap.unity | grep 'value:' | sort
# which categories the code fires
grep -rhoE 'GameplaySFXCategory\.\w+' Assets/_Scripts --include=*.cs | sort | uniq -c | sort -rn
# the field names (305 distinct) and the files that declare them
grep -rhoE 'EventReference\s+\w+' Assets/_Scripts --include=*.cs | sort | uniq -c | sort -rn
grep -rn 'EventReference' Assets/_Scripts --include=*.cs | grep 'SerializeField\|public'
# which prefabs carry a controller: its script guid, then the prefabs that reference it
g=$(grep -o 'guid: [0-9a-f]*' Assets/_Scripts/Controller/FX/DriftAudioController.cs.meta | cut -d' ' -f2); grep -rl "guid: $g" --include=*.prefab Assets
# the timings quoted in section 3
grep -n 'minTickInterval\|boostAmountSmoothing' Assets/_Scripts/Controller/FX/ProximityBoostAudioController.cs
grep -n 'releaseHoldSeconds\|fullSlipDegrees' Assets/_Scripts/Controller/FX/DriftAudioController.cs
grep -n 'paramAt\|maxShipVelocity\|maxTiltRate\|elementSmoothing' Assets/_Scripts/Controller/Vessel/Audio/ShipAudioController.cs
grep -n 'secondsPerPetal\|maxSeconds' Docs/ElementalAbilitySystem/SILENCE.md | head -3
# the source files and their lengths (python's wave module; 24-bit files need the 3-byte branch)
python3 -c "import wave,glob;[print('%-50s %6.2f s'%(f[20:],wave.open(f).getnframes()/wave.open(f).getframerate())) for f in sorted(glob.glob('Cosmic Shore/Assets/*.wav'))]"
# the spectral table in 2.1: median centroid, share under 150 Hz and 2-6 kHz, transients a second (numpy, 2048-point
# frames, hop 1024, flux peaks over 3x the median); the script is in the 2026-11-07 session's notes and belongs in
# Tools/Build/sound_slots.py when that lands
# who else works here
python3 Tools/Build/who_works_where.py query Docs/AudioSystem "Cosmic Shore" Assets/_Scripts/System/Audio
```
