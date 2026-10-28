# Shiva's Cell (lab)

The prototype for the Tandava redesign (Shombith, 2026-10-28). It is a voyage location: a squad of three goes into a
barren cell to destroy a creature. The creature is a humongous Shiva in the middle of the cell, shown to the waist,
with eyes and a third eye. The cell is an orrery, with planets wheeling round him.

- **Live page:** published from `shiva-lab.html` (the URL is recorded in `.claude/skills/labmaker/CATALOG.md`).
- **Build:** `python3 Docs/Studios/ShivaLab/build.py` re-reads the nine shipped many-headed serpent plans
  (`Assets/_SO_Assets/Swarm Fauna/Tandava/SwarmPlan_tandava_many_headed_{5,7,10}{,_rear,_strike}.json`) into
  `plans.json`, and writes `shiva-lab.html` beside it. Both outputs are generated and git-ignored.
- **Headless scorecard:** `node Docs/Studios/ShivaLab/check.js 1,2,3` (run `build.py` first). It runs three squads
  (Mixed, Fast, Slow) × three variants on autopilot. The page's **Score the squads** button runs the same with your
  settings, plus a "No clap rip" control.
- **Verify:** `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/ShivaLab/shiva-lab.html`.

## The design the lab plays (round 1)

| Element | In the lab |
|---|---|
| **The shield** | Three concentric shells of danger prisms (620 / 520 / 430 u). Touching the lowest standing shell stings you: half speed for 1.5 s, one element level knocked off as a crystal, and a shove clear. He cannot be touched. |
| **The circuit** | Each form has a ring circuit that spirals in from 1,350 u to just outside its shell (10 / 12 / 12 rings, 72 u mouths, the same size as Headlong at intensity 2). The rings are lit in order, and any pilot threading the lit one moves the squad on. The last ring faces him; threading it cracks the shell. |
| **The clap** | A finished circuit cracks a shell and he claps. A wave sweeps the cell at 1,100 u/s. It wipes every prism the squad laid. It rips every element off a vessel it passes inside 950 u, scattering the levels as crystals. It shoves everyone outward and clears the swarm. Then the cell collapses and grows again. |
| **The forms** | 1. **The Still Shiva** (the reference pose, with four arms spread). 2. **Nataraja**: he dances and the whole circuit wheels round him, each ring bobbing and swaying. 3. **The Devouring Mouth**: his jaw opens on a black hole that pulls vessels and crystals in. Its pull cap is BlackHoleConfig's 90 u/s. A vessel that crosses the horizon is spat out at the membrane, stripped. |
| **The swarm** | It comes out of his hands at each form. Form 1 releases Five-Headed Serpents and sharks; form 2, Seven-Headed Serpents, sharks, piranha and clawfish; form 3, Ten-Headed Serpents and all the rest. Each serpent coils round a ring further along the circuit, across its mouth, and lunges at a pilot that comes within 380 u, rearing every head and then striking (the shipped plans and TandavaDirectorSettings). Its shielded body is solid: you bounce off it, slowed, unless your Charge is 5 or more. Its core can be cut. Sharks ram for a hard slow and lie in wait at the lit ring; piranha nip and stack; a clawfish latches on until you boost for a second. |
| **The cell** | Each form grows a different cellular-automaton cell: a crystal lattice, then coral, then ash blooms. Flying through a prism breaks it; skimming past refills your boost. 60 elemental crystals float in the cell. In this lab, Time raises your speed, Space widens the ring mouths for you, Mass shrugs off slows and pull, and Charge cuts serpent shields. |
| **The clock** | A drum per form (110 / 180 / 170 s). If it runs out, his dance ends the cell: the squad loses. |
| **The win** | The third shell breaks and the god comes apart in light: the figure explodes outward, three shock rings spread, and a burst of gold, blue and magenta fills the cell. |

## Round 1 scorecard (`node check.js 1,2,3`, 2026-10-28)

Mean seconds per form; every run won (3 seeds).

| Squad | Variant | Still | Nataraja | Mouth | Stings | Rams | Nips | Levels ripped |
|---|---|---|---|---|---|---|---|---|
| Mixed (Squirrel, Sparrow, Rhino) | settings | 52 | 65 | 48 | 24 | 15 | 62 | 36 |
| | no swarm (control) | 43 | 56 | 53 | 21 | 0 | 0 | 39 |
| | sloppy squad | 58 | 93 | 83 | 57 | 27 | 33 | 39 |
| Fast (Manta, Dolphin, Squirrel) | settings | 24 | 46 | 46 | 20 | 5 | 58 | 24 |
| | no swarm | 22 | 34 | 41 | 33 | 0 | 0 | 28 |
| Slow (Sparrow, Serpent, Urchin) | settings | 69 | 126 | 108 | 130 | 29 | 58 | 43 |
| | no swarm | 65 | 122 | 107 | 96 | 0 | 0 | 41 |
| | sloppy squad | 77 | 141 | 115 | 127 | 40 | 79 | 37 |

**Found:**
- **The swarm costs the squad only 5-20%.** The squad shares ring credit, so the pilot nearest the lit ring runs
  free while the swarm hits the other two. The levers are more serpents, a ring that needs more than one pilot, or
  stings that cost more. This is **Decision needed (designer)**.
- **One clock cannot fit every squad.** A Fast squad finishes a form in a quarter of the drum; a sloppy Slow squad
  needs most of it. Fast hulls (the Manta's 720 u/s, the Rhino's 1,200 u/s ramp) take the circuit in a few long
  legs. **Decision needed (designer):** do the clocks follow the squad's slowest hull, or does a fast squad simply
  win sooner?
- **The black hole pinned slow hulls to the shell** until two changes: a vessel's own thrust now bleeds the pull, and
  a shell sting now shoves you clear. Without them, stung slow hulls lost every Mouth.
- **The autopilot needed real ring approaches.** It lines up on the ring's axis a turn radius or two out, then flies
  through. It leads a moving ring and leans against the pull. Squadmates pre-position on the next ring only while
  the leader can make the lit one.

## What this lab does NOT model

- Each hull's own boost economy and abilities. A coarse boost stands in: free for the Manta, Sparrow, Rhino (on a
  straight stick) and Scarab; a meter refilled by skimming and crystals for the rest.
- The sort core. A serpent here is its shipped plan moved rigidly; the swarm does not sort itself into it.
- Prism physics, colliders, frame time and the prism budget.
- The game's gyroid colonies. The regrown cell is a 3D cellular automaton standing in for them.
- Multiplayer, latency, the Ark, and the game's `AIPilot` (the autopilot here writes the same inputs, but it is not
  that code).

## Files

| File | What it is |
|---|---|
| `head.html` | the page shell, styles, HUD and brief |
| `sim.js` | `ShivaSim`: the shell, the circuit, the forms, the clap, the swarm, the pilots and the autopilot (no DOM; runs under node) |
| `app.js` | the three.js view (the prism figure, shells, rings, orrery, swarm, wave, black hole, the end), input, settings, scorecard, decision log |
| `build_plans.py` / `build.py` | cut the shipped serpent plans and assemble the page |
| `check.js` | the headless scorecard |

## In the game

Ported 2026-10-28 as `GameModes.Tandava` (Voyage 1): `Assets/_Scripts/Controller/Arcade/TANDAVA.md` §0. The numbers
cross by `Tools/Build/author_shiva_assets.py`, which reads this lab's `SHIPPED` and writes `ShivaSettings.asset`; the
director's C# twin is held to `sim.js` by `bash Tools/Build/swarm_core_harness/run.sh "" shiva` (S1-S9; the fixture is
`parity_fixture.js`). Not yet run in the Unity editor.
