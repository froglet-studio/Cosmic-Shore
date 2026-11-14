# The next skills to train: what we redo, what falls short, and the rounds that would fix it

Garrett, 2026-11-07: "we do a lot of work that we have to redo or does not meet the highest standards. if you were to
train or learn or research and develop new skills that would benefit the whole team what would you do to increase your
competency. consider how you might spend cycles developing those skills and capturing everything in now or existing
skills. i did this before and you got much better at modeling vessels, and much better at making icons for vessel
abilities. now consider what is missing or what could use more improvements."

This is the plan. Nothing in `Assets/` changes with it. Every number below was measured on 2026-11-07 from the repo
(section 9 says how to regenerate each one; `python3 Tools/Build/rework_ledger.py --skills` prints the counters that
matter most, so the same measurement can be made next month and the month after, which is how the team will know
whether a training round paid).

**The short version.** Two things cause most of the redoing, and neither is a craft:

1. **Work ships unseen.** 169 of the 242 sections of `Docs/UNITY_VERIFICATION_CHECKLIST.md` are red (never verified
   in the editor); 159 of them since October. 60 sections of the menu redesign were never run in the editor. The
   checklist's own status line reads "No Unity compile since 2026-10-06". A session cannot see the real renderer, so
   every look is judged by a person, in rounds, after the commit. Vessel models and icons got better because each got a
   SHEET a session could look at before anyone else did; most of the game still has no sheet. Of the 150 most recent
   hand-off tickets, 82 are tests only a person can run, and 59 % of those were never taken.
2. **Things are built, then retired for a rule nobody had written.** 64 commits since September retire something;
   13 revert work. Six LOCKED rules were written in the three weeks after a build broke them (nothing flat 10-18, a
   hovering assembly 10-21, in the world not on the screen 10-21, moving prisms 10-28, a mode never changes the vessel
   10-31, a drift never moves the hull 11-02). The owners' intentions exist as 101 dated quotes spread over 30 docs.

Then the craft areas with the most measured rework, in order: **the look of things in the world** (shaders, effects:
one wormhole look took 26 rounds and had eight looks removed), **UI screens** (43 mockup rounds, 16 "I do not like"
lines, the Hangar rebuilt at least seven times), **sound** (86% of the game's sound slots are empty), and
**multiplayer** (the owner's eight hand tests are run by hand). Section 3 ranks them with the evidence; section 4 says
how to spend the cycles; section 5 lists the decisions this plan waits on.

## 1. What made the last two rounds work (so the next ones start there)

`/vessel-model` (Garrett, 2026-10-17: "procedural vessel models are terrible even as placeholders"; 2026-10-18 on the
round-8 Squirrel: "I can't tell it isn't the original") and `/ability-icon` (2026-10-24: "the current icons range from
decent to terrible"; the Manta's cards merged 2026-11-07, #1805) share one shape. The shape is the skill; the drawing
engine and the Blender specs are only its hands:

| Part | What it is | Where it is in the two skills |
|---|---|---|
| **An audit sheet, measured** | Everything that exists, looked at side by side, with what "terrible" means written down as failure shapes | `Docs/Icons/ability_icons_audit_2026-10-24.png`, `ABILITY_ICON_SKILL_PLAN.md` §2; the vessel dossiers |
| **The intention, in the owner's words** | One short language doc the owner confirmed, which every round is judged against | `references/ICON_LANGUAGE.md`, `references/FLEET_CONVENTION.md` |
| **A contract the game binds** | What a replacement must keep (names, guids, bones, sizes) so the game never notices the swap | `references/CONTRACT.md` in both |
| **One kit, one command** | A spec per unit; render, verify, sheet, write, all deterministic, guids kept | `Tools/Build/icon_kit/author.py <hull>`, `Tools/Build/vessel_model/build.py` |
| **A sheet LOOKED AT before anything is written** | The unit at the size and on the plate the game shows it, beside the yardstick set | the 128 / 60 / 40 px lockup sheet; the Cycles contact sheet |
| **Gates that fire, each with a negative control** | Measured minimums (stroke, gap, coverage; rest bounds, take parity), proven to fail on a bad input | `verify.py --self-test`, `build.py --check`, `take_parity.py` |
| **One unit per round, the owner judges in the editor** | Never a bulk rewrite; the owner's words are the result, the gates only say what he will not have to point out | `/vessel-model` §4 (the loop that passed), `/ability-icon` §3 |
| **LESSONS appended before the next unit** | A new rule goes to the language doc, a check that could have caught it becomes a gate | `references/LESSONS.md` (55 and 59 entries), the plan's round table |

What it cost, so the next area can budget: the Squirrel passed on round 8 and the Dolphin on round 4 (six and three
from-scratch rounds were a cheap remake, LESSONS 38); later hulls took 1 to 5 rounds; the first icon hulls took 2 to 3
rounds each, with Garrett looking at every one. A kit session is one or two cloud sessions. A round is a few hours of
session time and a few minutes of an owner's time in the editor. The rounds ride on real work: the icon kit matured on
real hull requests, never on a study exercise.

Two things the shape depends on and most of the game lacks today: **something to look at** (a sheet from the real
renderer) and **something to measure** (a gate with a number). Section 3's first entry is about the first; every
other entry names its sheet and its gates.

## 2. The audit (measured 2026-11-07)

### 2.1 The rework ledger (`python3 Tools/Build/rework_ledger.py`)

Commits since 2026-09-01 on every fetched branch, one per subject: **5,015** (feat 1,656, fix 983, docs 932).

| Signal | Count | What it is |
|---|---|---|
| Reverts of work | **13** | `Revert "Merge pull request #1620 …tunnel-shader-watery-glass"` (10-29); `revert(camera): remove the drift slide` (11-02); `revert(vessel-model): the artist's seven hulls go back in place` (10-18); `revert(prism): the erosion rework was the wrong system` (09-17) … |
| Retirements | **64** | the Butterfly (10-29), the Waystation Prototype (10-31, "76 retired"), Serpent Time's round economy and wall cost (10-31), the lattice backdrop (10-25), Ark concept A (10-28), the small view's double tap (10-25) … |
| "again" | **37** | a thing that had worked, made to work again: "a restored prism grows again", "Serpentine's card loads again", "the score bar sits top-centre again", "the lava lamp spawns the Squirrel by default again" |
| Compile fixes | **21** | a CSxxxx or "compiles again" in the subject (`KitRectMask2D … CS0117`, "the Unity stub gains Vector3.Min/Max - charge_arcs_harness builds again") |

### 2.2 What ships unseen

- `Docs/UNITY_VERIFICATION_CHECKLIST.md`: **242** `##` sections, **169 red**, **2 verified**; by month, red: 2026-08 2,
  2026-09 2, **2026-10 136**, 2026-11 23. Counting `###` headers too, 597 carry 🔴 and 559 say "NOT EDITOR-VERIFIED"
  or "unverified". The checklist's own status (`Docs/LAUNCH/READINESS.md:36`): "No Unity compile since 2026-10-06; 48
  EditMode failures; 181 open QA items". `unity-ci.yml`'s Unity job has been skipped since 2026-10-13 (no runner).
- `Docs/MENU_REDESIGN_B.md`: 126 sections, **43** mockup rounds (Rev 1 to Rev 66), **60** sections never run in the
  editor; only the top nav and the Hangar were ever "Approved in editor" (§60-61).
- `/vessel-model` §4: six hulls "awaiting the owner's flight" or "not wired yet" (Butterfly, Goby, Grizzly, Orca,
  Pistol, Meerkat).
- `Docs/Reports/ANALYTICS.md`: "No match has yet been played in the Unity editor against the live server."
- What a session CAN see today: Amoebius renders the real modes, AIs, HUD and replays with its own OpenGL renderer
  (`Docs/claude/VIDEOS_AND_PREVIEWS.md`: "the final look … differ from Unity's"); Blender renders a hull; Playwright
  renders an artifact. Nothing renders a Unity frame. The headless real editor exists for compiling and EditMode
  tests (`Tools/Build/verify_unity.sh`, a full checkout and `/opt/unity/<ver>`), and Froglet Compute's VM runs the
  real game headless for the AI trainer (`Docs/Compute/README.md` §16: `Unity -batchmode -executeMethod
  …FrogletComputeTraining.Run`, and a windowed run "camera and sound on"). The pieces of a real-renderer sheet exist;
  nothing joins them.

### 2.3 Built, then retired for a rule written afterwards

| Built | Retired | The rule that was missing, and when it was written |
|---|---|---|
| the camera's drift slide (10-25, Yash's "a little bit alive") | 11-02 | "A drift never moves the hull on screen" (Garrett, LOCKED 11-02: "disruptive to the players sense of their reference frame") |
| Serpent Time's crystal-bought rounds and walls that cost speed (10-27) | 10-31 | "A game mode never changes the vessel" (Shombith, LOCKED 10-31) |
| the Omega crews in Unity, ids 25-27 (10-26) | 10-27 | superseded by the Siphonophore; then "no more new vessels" (Yash, 11-01) parked all ten concepts |
| the ring explosion, the spindle facets, the black hole's vortex ring (flat) | each redrawn | "Nothing in the world looks flat" (Garrett, LOCKED 10-18); "In the world, not on the screen" (LOCKED 10-21) |
| seven from-scratch vessel hulls (10-17) | 10-18 | "the shipped heading is right by definition"; a hull is a hovering assembly (10-21) |
| the Rev 11 frosted lattice (10-24) | the same day | "Scrap the whole lattice design. The old bg was far better." (Shombith) |
| the Vessel Studio's collaboration machinery, ~1.8k lines (10-14) | unused | one shared decision, no requests, no sync job (`Docs/TOOLING_DECISIONS.md` §2) |
| the Stoat, twice (Unity #1076 and studio rounds 1-15) | merged by hand | `who_works_where.py` and the TEAM rules (10-13, ~110 conflicts) |

The intentions these rules state were held before the builds. They were not written anywhere a session reads first:
101 dated quotes from the four owners are spread over 30 docs (section 9 has the grep), and
`Docs/claude/DESIGN_PHILOSOPHY_EMERGENCE.md` holds the fundamentals but not this ledger.

### 2.4 The craft areas, measured

| Area | Rework recorded | Content without a kit | Skill today |
|---|---|---|---|
| **The world's look** (shaders, materials, effects, trails, blasts) | `Docs/Shaders/WORMHOLE_SHADERS.md`: 984 lines, 16 sections, **round 26**, eight looks removed or replaced between 10-27 and 11-01, one PR reverted; "looks terrible" on the Urchin cradle (`PRISM_ANIMATION.md:1935`: "two rounds replaced, both proven by a harness and both wrong on screen") and on crystal capture (`ECOSYSTEM.md:4485`); the Orca missiles "feel cheap" (10-21); 22 red checklist headers, 10 open QA items | 60 shader graphs, 55 project shaders (about 11 generated), 28 particle systems in 19 prefabs, 2 VFX graphs; **no effect generator, no gate, no sheet** | none (`/asset-surgery` wires graph nodes; `/prism-morph` is prisms only) |
| **UI screens** | 43 mockup rounds; the Hangar rebuilt ≥ 7 times, Configure ≥ 5, Codex ≥ 4, Squads 3, the friends tray 4, the Voyage window 4; 16 "do not like" lines (Shombith: "I do not like this UI" 10-17, "I do not like this style at all … Too much noise" 10-27, "Up your game bhai" 10-28, "I don't like the design, make it modern" 11-02, "I do not love these" 11-05); the five worst screens carry 43 / 39 / 34 typed-in colours (`UI_STYLE_GUIDE.md` §12) | 180 UI prefabs, 64 with no generator; 232 red checklist headers | `/frogma` (mockup rounds, picks), `/ui-redesign-tracker`, the style guide and tokens (11-03), `check_ui_pr.py` |
| **Sound** | boost is silent (a looping event fired as a one-shot, `CHARLES_TASKS.md` C2); three empty `AudioSystem` slots (C3); the Squirrel's loop slot wired to a one-shot (C4); the Butterfly has no sounds, Rhino combos none, the skyburst missiles no launch sound (DT-004); Yash (10-28): the Meerkat's ability sounds "one by one … I will test and confirm" | **238 of 277 serialised FMOD references are empty (86 %)**; 18 distinct events in use of 61 banked and 73 authored; banks for Desktop only; the FMOD Studio project is in the repo (`Cosmic Shore/Cosmic Shore.fspro`, 73 event XMLs, 108 source files); **0 generators, 0 gates** | none (the LOCKED convention: every sound an exposed field, shipped empty, never a temp event) |
| **Multiplayer** | the owner's table of eight hand tests plus the live UGS check (`MULTIPLAYER_START_HERE.md` §5); bugs B20-B47; "B31 fixed again"; the party-sync crash seen by three testers (11-01); 7 of the 42 anti-patterns; 42 red checklist headers; 21 QA items never run; launch blocker #20 | fake-SDK EditMode tests (B34), `net_logs.py` timelines, Amoebius START ONLINE + PLAYER 2 with a network simulator and faults, `--ugs-relay-check` | none (six doc folders, no skill) |
| **Ecology, flora, fauna** | "for the third time" twice in `ECOSYSTEM.md` (:4159, :10353); 129 red headers; 42 QA items; the swarm "feels like a small pack, not a murmuration" (Garrett, 10-21) | `fauna_kit` has one species spec (the bee) against 9 fauna models; `flora` fits and harnesses | `/flora` A-, `/ecology`, `/fauna`, `/fauna-creation` B+ (each lacks a negative control and a round table) |
| **Launch content** | `Docs/LAUNCH/READINESS.md` §store assets: capsules, screenshots, trailer, store copy all "Not found"; iOS icon slots empty; no localisation; an unlicensed font ships | 91 files named placeholder (31 pilot portraits, 32 icons); 0 string tables; 4 dialogue assets, 0 lore | `/cardart` (in-game cards only) |

### 2.5 The skills, by maturity

55 skills (`.claude/skills/*/SKILL.md`), hand-audited against the shape in section 1 (appendix A has the table; the
ledger's `--skills` prints a mechanical proxy):

- **A** (the whole shape): `vessel-model`, `ability-icon`. **A-** (five of six): `vessel-studio`, `flora`, `labmaker`.
- **B+** (kit, gate and a measured loop; no negative control or round table): `prism-morph`, `fauna-creation`,
  `fauna`, `cardart`, `ecology`, `vessel`, `vessel-ai`, `duo-vessel`, `perf-hunt`, `compute-job`, `frogma`.
- **B** (kit and gate, no critique loop): `arcadegame`, `arenagame`, `EndGameConditions`, `artifact-to-unity`,
  `amoebius-artifact`, `refactor`, `ship` (three lanes), `ui-redesign-tracker`, `froglet-skills`, `launch-flow`,
  `regatta-balance`, `compute`, `fleet-codex`, `bug-ledger`.
- **C** (a recipe in prose): `element-ability-table`, `compute-scout`, `team-tasks`, `phone-hunt`, `qa-backlog`,
  `bug-closeout`, `bioform-forge` (names `Tools/NCA/`, which is on the research branch, not here), `asset-surgery`,
  `amoebius`, `androidbuild`, `ab-test`, `reorient`, `ship-tools`, `frogma-new`.
- **D** (thin by design): `frogma-show`, `to-bleeding-edge`, the seven `launch-flow-<platform>` wrappers.
- **No skill at all**: the world's look, sound, music, multiplayer, narrative and dialogue, localisation, analytics,
  store and marketing. Partly covered inside other skills: animation, camera, shaders.

### 2.6 What the team hands off (the 150 most recent `team-task` tickets, #1375-#1774, and the 80 most recent PRs)

| Kind of ticket | Count | Never taken in 14 days (archived) |
|---|---|---|
| a hand test | **82** | 59 % |
| a decision | 20 | 70 % |
| a content gap | 5 | 60 % |
| a setup step | 8 | 37 % |
| a fix | 32 | 34 % |

- **Half of everything is a vessel flown by a person**: 75 of 150 tickets are vessel work (45 tests, 11 decisions), 44
  of them the crew vessels ("the Orca, rounds 5-9", "every round in one sitting"). Of the 82 hand tests, 46 need the
  Unity editor by hand, 18 a phone or a device build, 7 a gamepad, 6 two machines, 5 a Release capture.
- **Decisions do not travel by ticket** (70 % archived). The ones that land are taken in chat or on Frogma: a
  decision competency is "draw the options and ask the owner", which is what section 3.3's brief does.
- **Of the 80 PRs, 18 were redone, reverted or fixed after a previous PR.** UI has the most (five: the Showroom's
  missing background, the colour-set apply that did nothing until a restart, tab words out of line, all found by
  Shombith in the editor; the phone-UI stack #1739 / #1741 / #1744 closed unmerged after one look in the Device
  Simulator); analytics four fixes within a day of #1761 (the VM could only be checked live); the Manta's bomb (#1804,
  eight minutes after #1803), the Dolphin morph that covered the blast (#1726), the Thresher's Time genome that "must be
  learned again" (#1791).
- The ticket form for AI is "race every AI in one sitting"; nine AI PRs are proposals waiting on a person's race.

## 3. The ranked list

Each entry: the evidence, the bar in the owner's words, the kit, the gates, the sheet, the rounds and what each one
proves, where the lessons are captured, and whom to ask (an owner is who is ASKED, never who is assigned; the TEAM
rules). Names of skills are suggestions; the team names them.

### 3.1 See the real game without a person (the multiplier)

**Evidence.** Sections 2.2 and 2.6: 169 red checklist sections, 159 since October; 60 UI sections never run; six hulls
waiting for a flight; 82 of 150 hand-off tickets are hand tests, 46 of them in the editor, 18 on a phone, and 59 %
were never taken; no Unity compile in CI since 10-06; the Urchin cradle's two rounds "proven by a harness and both wrong on
screen". Every craft loop in this plan ends with "the owner judges in the editor", and today the owner is also the
first person to see the thing at all.

**The bar.** Garrett, 2026-10-18, on the Pistol's view: "render … as true to the game as possible; err on the side of
being too true to the game." `/vessel-model` LESSONS 38: "no offline render will tell you", said of Blender; it is the
real renderer or nothing.

**What exists.** Two in-flight branches found on 2026-11-07 evening, which this entry builds on rather than beside:
`claude/ci-hosted-unity-compile` (2026-11-01: the bleeding-edge guard's Unity compile on GitHub-hosted runners in
GameCI's image, with licence activation in `Tools/CI/unity_compile_in_container.sh`; it recorded its first hosted
runs and waits on a licence in the `unity-ci` environment and a merge) and `claude/launch-mode-smoke` (PR #1754:
`ModeSmokeLauncher` / `ModeSmokeDriver` play every rostered card from Bootstrap with the AI on every seat, an
`-executeMethod` runner; built, not yet run in Unity). Also `Tools/Build/verify_unity.sh` (the real editor, headless:
compile and EditMode tests; refuses a sparse checkout); `Tools/Build/unity_refcompile` (the Roslyn reference compile every C# commit runs);
`unity-ci.yml`'s Unity job, skipped while `UNITY_RUNNER_LABEL` is unset; the Compute VM's licensed Unity that runs
the trainer with and without a window; `ScreenCapture` in two editor tools (`QASessionWindow`,
`PrismOcclusionDitherLab`); Amoebius's `screenshot` and the clip recipe; `Time.captureFramerate` and a seed on every
mode.

**The kit** (`Tools/Build/unity_look/`, an editor-assembly `[CliCommand]`-style entry under `Editor/`):
- `look.py menu <screen> --pc --phone`: open Menu_Main, reach the screen the way a player does (the ScreenSwitcher),
  write a PNG at 1920x1080 and at the phone's 800x450 scale with the safe area drawn.
- `look.py vessel <Hull> --mode <mode> --intensity 2 --frames 6 --seed 45501`: the mode scene with the hull on seat 1
  flown by its AI, six frames at fixed game times, from the game's own camera.
- `look.py effect <prefab> --at near,game,far`: the effect spawned in a lit cell, at the three distances the game
  shows it, four frames of its motion, on the PC tier and the Mobile-low tier.
- `sheet.py`: the contact sheet, labelled with the commit, the scene, the seed; for a UI screen, the picked Frogma
  board beside it.
- It runs where a Unity is: the Compute VM (Xvfb and Mesa when the VM has no GPU: seconds a frame, fine for a sheet)
  or a CI runner. A cloud session calls it the way it calls Amoebius today, through a job, and reads back the sheet.

**The gates** (each with a negative control in `--self-test`): the compile is clean (the existing script); the PNG
is not empty, black, or the skybox alone (a scene that renders nothing fails); a UI sheet's element boxes match the
picked board's within a tolerance (`ui-redesign-tracker`); an existing approved sheet is diffed on every later run,
so a shader or prefab change that moves a screen is seen before a person sees it.

**Rounds.**
- R0: **the real editor compiles every PR.** Not the VM: `claude/ci-hosted-unity-compile` already does it on hosted
  runners and needs only the licence secrets (`UNITY_EMAIL`, `UNITY_PASSWORD`; optionally a serial) in the `unity-ci`
  environment and a merge. Closes the "no Unity compile since 10-06" hole. Proof: the guard's negative-control
  commit (11-01) fails it.
- R1: one headless screenshot of Menu_Main's Home at PC and phone sizes, from the VM, read by a cloud session.
- R2: the Manta in Manta Time flown by its AI, six frames; the sheet attached to the next icon or model round instead
  of "awaiting the owner's flight".
- R3: the UI parity sheet for the Hangar (the most rebuilt screen) beside its picked board.
- R3b: the same sheet from an Android development build on a named phone through `adb` (18 of the 82 hand tests
  are a phone; the Android build skill and the Play upload exist; the capture is the missing step).
- R4: a sheet is a `/ship` gate for any PR touching `Assets/_Prefabs/UI*`, `Assets/_Models/Vessel Models/**`, a
  shader or a world effect; the checklist entry carries the sheet instead of "NOT RUN IN EDITOR".

**Captured in:** a new skill (`/unity-look`); `/ship`'s verify rule, `/vessel-model` §4, `/ability-icon` §3,
`/frogma` and `/ui-redesign-tracker` point at it. **Ask:** Shombith (tooling, CI, the VM; Froglet Compute is paused
for analytics and this is not a compute job, so it is his call how the VM is shared). **Cost:** R0 a day; R1-R2 one
or two sessions; the rest ride on real rounds. **Payback measured by:** red checklist sections per month; rounds to
pass per hull, screen and effect.

### 3.2 The world's look: shaders, materials and effects (`/world-look`)

**Evidence.** Section 2.4 row 1. One wormhole look: 26 rounds, eight looks removed, a PR reverted, and the doc that
records how to bring each one back is longer than most skills. Three flat effects rejected before the rule was LOCKED,
two "looks terrible", one "feel cheap". 60 shader graphs and 28 particle systems made by hand; nothing measures them
and nothing renders them for a session.

**The bar.** Garrett: "Nothing in the environment should ever look flat like this, that is reserved for ui" (10-18);
"always go for the 3d in world solution over the 2d flat text solution" (10-21); the bomb "should not have a ring that
says burst"; Yash on the wormhole (11-01): "plain worm hole with wider mouth looking cool from outside and inside,
bending and warping of space time".

**The kit** (`Tools/Build/look_kit/`, the 3D twin of the icon kit):
- `references/LOOK_LANGUAGE.md`: a world effect is a lit, faceted volume in the domain materials, scaled with the thing
  it belongs to, prism-cut like the hulls and crystals; the comfort limits (`WORMHOLE_SHADERS.md` §14.4); what has to
  read at the three distances; the phone tier. Drafted from the LOCKED rules and the owners' quotes, confirmed by
  Garrett before the kit is built (the icon plan's one decision, §4).
- `references/VOCABULARY.md`: the effect primitives the game already owns, each drawn once with a tile: the prism
  shard, the domain band, the ember, the jelly lens, the trail chain, the bloom petal, the ward, the lance. The
  Starlings' element marks stay the element marks.
- The yardstick set: the effects the owners accepted (the calm jelly wormhole §16, the Ark's ember, the prism bloom,
  the Manta's Squirrel-style trail of 11-07) on one audit sheet beside the ones they rejected, with the failure shapes
  named the way `ABILITY_ICON_SKILL_PLAN.md` §2 names them (flat sheet, billboard, screen ring, unlit additive, out of
  scale, hairline at distance, generic).
- `effect.py`: an effect is a SPEC (geometry, material slots, motion curve, size relative to its owner); builders for
  the common shapes (shard burst, a ring as a volume, lance, trail, veil) write meshes, materials and prefabs with
  stable guids through the `/asset-surgery` techniques; a shader is written only when a vocabulary material cannot do
  it.
- `verify.py`: no Unlit, additive-sheet or billboard shader on a world effect unless the spec declares why; the mesh
  has normals and depth (its thinnest extent against its longest: the flatness gate); size to scale with the owner,
  measured from the prefab; the phone budget (triangles, overdraw estimate, shader variants, no material instantiation
  per spawn: the perf gates already here); the comfort numbers; each with a negative control.
- The sheet: until 3.1 lands, Blender Cycles with a lit, domain-tinted setup for geometry and motion (the hull sheets
  already render this way) and Amoebius for the effect in play; the real renderer the day 3.1's R2 works.

**Rounds.** R0 the language, the vocabulary tiles and the audit sheet (one session, Garrett confirms the language);
R1 the Orca's missile and bomb ("feel cheap"); R2 crystal capture ("looks terrible"); R3 the black hole's ring; R4 a
trail. One effect per round, judged in the editor by its owner, LESSONS appended; `WORMHOLE_SHADERS.md` §1-16 and
`PRISM_ANIMATION.md` seed the LESSONS file.

**Captured in:** a new skill; `/prism-morph`, `/asset-surgery` and `Docs/claude/SHADERS_AND_PERFORMANCE.md` are
referenced, not replaced. **Ask:** Garrett (the look), Yash (the Meerkat's wormhole), Shombith (UI-side shaders).
**Cost:** by the icon precedent, two kit sessions, then 3-5 rounds for the first effect and 1-2 for each later one.

### 3.3 Decide before building: the principles ledger and the design brief

**Evidence.** Section 2.3. Eight things built and retired in five weeks, six of them for a rule written after the
build; 64 retire commits; the intentions existed as quotes. This is the cheapest entry and the one that saves the
most, because a retired feature cost a build AND an undo.

**The bar.** `Docs/claude/DESIGN_PHILOSOPHY_EMERGENCE.md` ("favor emergent systems over bespoke solutions"; "one
HyperSea, one rule set"); Garrett (11-02): "we do things to help players, but we can't do things like drift
displacement that hinder them"; Shombith (10-31): "The vessel kit as designed always stays the same, we just make the
game mode interesting."

**The kit.**
- `Docs/claude/PRINCIPLES.md` and `principles.json`: one entry per rule: id, owner, date, the words, the rule in one
  line, what it retired, and how a design is tested against it. Mined by `Tools/Build/principles.py` from the 101
  dated quotes and the LOCKED headings; `--check` fails when a LOCKED rule in `CLAUDE.md` or `Docs/claude/` has no
  entry, or an entry's source line no longer says it.
- `Docs/claude/DESIGN_BRIEF.md`, one page a session writes BEFORE building a mechanic, a camera behaviour, a mode rule
  or a screen: the fundamental it serves, the principles it obeys by id, the one it comes closest to breaking, the
  cheapest prototype (a `/labmaker` lab, the Vessel Studio page for a vessel, an `/ab-test` for a choice), what the
  owner will judge and where, and the condition under which it is retired. `/arcadegame`, `/vessel`, `/labmaker`,
  `/frogma` and camera work get the brief as their first step.
- The retrospective rule: when a thing is retired, its brief gets one line and the ledger gets the principle that was
  missing. The ledger grows the way LESSONS files grow.
- Negative control: run the ledger against the eight retirements in 2.3; a useful ledger names the principle each
  broke before the build, not after.

**Rounds.** R0 mine and write (one session, docs only). R1 each owner strikes or confirms the entries in their area
(a decision list, nobody assigned). R2 the brief on the next three features that reach `/ship`. R3 the check joins
`gates.sh`.

**Captured in:** `Docs/claude/DESIGN_PHILOSOPHY_EMERGENCE.md` (points at the ledger), the five skills above; no new
skill unless the team wants `/design-brief` as a thin entry. **Ask:** all four; Garrett holds the philosophy doc.
**Cost:** one session, then minutes per feature.

### 3.4 UI: first drafts that land, and a build that matches the board

**Evidence.** Section 2.4 row 2. The pipeline exists and works (mockups on Frogma, a pick, a build by
`author_menu_*.py`, the style guide and tokens since 11-03); what repeats is the first draft Shombith rejects, and
the build nobody puts beside the board. 43 rounds of picks with his reasons are a dataset of his taste that no doc
distils, so first drafts repeat what he already rejected.

**The bar.** Shombith, 11-02: "I don't like the design, make it modern"; 10-27: "Too much noise"; the style guide
(Lime is always the call to action; Jade the choice; Ruby no way back; phones on their own 800x450 scale).

**The kit** (inside `/frogma` and `/ui-redesign-tracker`, no new skill):
- `references/UI_LESSONS.md`, mined from `MENU_REDESIGN_B.md` by `Tools/Build/ui_lessons.py`: per screen, what was
  picked and what was rejected, in Shombith's words, with the board; `--check` that every new round section records
  its pick.
- `check_mockup.py`, a gate on a mockup BEFORE he sees it: colours only from `froglet.css` tokens; type from the
  scale; contrast at least 4.5; the phone frame with the safe area; one Lime call to action per screen; a noise budget
  (distinct elements on the screen against the median of the picked screens). Proven on three rejected rounds (Rev 29
  "too much noise", Rev 34 FTUE, Rev 48 Codex) and three picked ones.
- The parity sheet: the built screen rendered (Amoebius today for layout; the real renderer through 3.1) beside the
  picked board, with the element boxes diffed.

**Rounds.** R0 UI_LESSONS (docs only). R1 the mockup gate. R2 the Hangar's parity sheet. R3 the five worst screens
through the kit with their boards.

**Captured in:** `/frogma`, `/ui-redesign-tracker`, `Docs/UI_STYLE_GUIDE.md` §13 (the open decisions). **Ask:**
Shombith (UI code owner, the picks). **Cost:** R0 one session; the rest ride on real rounds.

### 3.5 Sound: every slot audited, every empty slot briefed (`/sound-brief`)

**Evidence.** Section 2.4 row 3: 238 of 277 slots empty, four classes of mis-wiring, a mobile launch with Desktop-only
banks, an audio owner (Charles, `Docs/AudioSystem/CHARLES_TASKS.md`; "Thresher ability audio" landed 11-06 from
`Charles-Land`) who gets his tasks as prose.

**The bar.** The LOCKED convention (every sound an exposed, editable field; shipped empty; never a temp event; an
unwired slot is a visible TODO); `Docs/ElementalAbilitySystem/SILENCE.md`.

**The kit.**
- `Tools/Build/sound_slots.py`: every `EventReference` in prefabs, SOs and scenes with its owner (hull, ability,
  system), filled or empty; the event's loop or one-shot nature read from the FMOD metadata XML; the slot's expectation
  read from the code (fired once, or started and stopped). `--check` fails on a loop fired as a one-shot or a one-shot
  in a loop slot (the C2 and C4 classes), and on a platform in the build profiles with no bank.
- `Docs/AudioSystem/SOUND_LANGUAGE.md`: what the game sounds like, per element, domain, hull family and ability act;
  durations measured from the takes and executors; the parameters the code drives (`Boost Amount` 0..1 and the rest).
- The brief generator: for each empty slot, one paragraph (trigger, duration, layers, parameters, the sibling events
  to match, the reference events), written into `CHARLES_TASKS.md` as a table the audio owner works down, ordered by
  what a player hears first (boost, skim, hit, the ability acts).
- A later round, only if Garrett wants it: synthesised reference stems (numpy) kept under `Docs/AudioSystem/refs/`,
  never in a bank.

**Rounds.** R0 the audit and the check (one session). R1 the Meerkat's four abilities briefed (Yash's ask). R2
Charles's feedback folded into the language. R3 the fleet's empties briefed in priority order.

**Captured in:** a new, small skill; `Docs/AudioSystem/`. **Ask:** Charles (FMOD Studio), Yash (the Meerkat),
Shombith (licences, `READINESS.md` #10). **Cost:** one session, then one per vessel.

### 3.6 Multiplayer: the owner's hand tests run by machines (`/multiplayer`)

**Evidence.** Section 2.4 row 4. The most documented area with no skill; the eight tests the owner runs by hand are
exactly the shape a machine runs; the bugs are numbered and recur; ticket #1530 (P1, open two weeks) asks a person to run eleven QA-NET hand tests in
order, and "B38 again" is in this week's commits.

**The bar.** `MULTIPLAYER_START_HERE.md` §1-2 (what the owner wants, the standing rules); the table in §5 with the
result each test expects.

**The kit.** The skill itself (START_HERE, the B ledger, `net_logs.py`, the rules, `UgsRequestPolicy`);
`Tools/Build/net_scenarios.py`: rows 2-5 of the table as scripted Amoebius two-player runs with injected faults (a
host drop then an invite, two joins on a 3/4 party, a leave at the ready gate, a guest leaving mid-match), judged from
the merged timeline against the marks each row expects; a `--self-test` that runs the five-process party scenarios in
the parity CI; the live UGS relay check on the VM as a scheduled task, when Shombith says so (it touches the real UGS
project).

**Rounds.** R0 the skill (docs). R1 rows 2-3. R2 rows 4-5. R3 the live check scheduled.

**Captured in:** a new skill. **Ask:** Yash (multiplayer), Shombith (UGS and the backend). **Cost:** one session,
then one per scenario pair.

### 3.7 Promote the near-A craft skills

`fauna-creation` (one species through the kit against nine fauna models; Garrett 10-21: "the swarm feels like a small
pack not … a murmuration"), `cardart`, `prism-morph`, `flora`, `ecology`: each has a kit, a gate and a measured loop
and lacks a negative control and a round table. One session each: `--self-test` negative controls, a
`*_SKILL_PLAN.md` round table in the icon plan's shape, LESSONS seeded from their docs, and the next unit through the
kit (the next creature; the next card). Filler sessions between the entries above.

### 3.8 Later, built on 3.1

- **Store assets and the trailer** (`READINESS.md`: all "Not found"; Steam by March 2027): scripted flights captured in
  the real renderer, capsules laid out from the style guide, the Amoebius clip recipe for order of events.
- **Pilot portraits**: started on bleeding-edge 2026-11-07 by `/pilot-art` (`Tools/Build/pilot_art/`,
  `Docs/Pilots/PORTRAITS.md`: all eighteen callsigns, not yet run in the editor). The 32 placeholder icons remain.
- **Motion**: Shombith (10-28): "Then we need some new hover animations"; vessel takes are `/vessel-model`'s, UI and
  fauna motion have no sheet; a clip is the sheet (`VIDEOS_AND_PREVIEWS.md`).
- **The voice of the game and localisation**: 4 dialogue assets, 0 lore, 0 string tables; the authored-text rule is
  the precondition; a `VOICE.md` when the FTUE copy is written (Shombith leads activation, Caleb heads FTUE).
- **The AI race in the real game**: nine AI PRs wait on "race every AI in one sitting"; the per-mode smoke test
  (#1754) is the seed of a harness that races every AI hull at I1 and I3 on the VM and writes the scorecard,
  which is `Docs/AI_SYSTEM/AI_PLAN.md`'s evidence instead of a person's evening. Served by 3.1; Garrett's plan.
- **Analytics**: Shombith drives it; a skill when the console settles.

Coordination (the 110-conflict merge) is covered by `who_works_where.py` and the TEAM rules; the ledger counts reverts
and "again" every month so a repeat is seen.

## 4. How to spend the cycles

1. **The unit of training is a round on real work.** Each entry gets ONE kit session (plan, language, kit, gates,
   audit sheet), then every real task in that area is a round through the kit: one unit, the sheet looked at, the
   gates green, the owner judges in the editor, LESSONS appended before the next unit. No study exercises, no bulk
   rewrites.
2. **Order.** 3.1 R0 (the VM compiles every PR) and 3.3 R0 (the principles ledger) first: a day and a session, both
   docs or infrastructure, both multiply everything after. Then 3.2 and 3.4, the two craft areas with the most rounds,
   whose sheets get real the day 3.1 R2 works. 3.5 and 3.6 when their owners say go. 3.7 fills the gaps. 3.8 after
   3.1.
3. **Budget**, calibrated on the two precedents: a kit session is one or two cloud sessions; the first unit in an area
   takes 4-8 rounds (the Squirrel 8, the Dolphin 4, the first icon hulls 3); later units 1-3 (the Meerkat 5 as a new
   hull, later icon hulls 1-2). A round costs hours of session time and minutes of an owner's.
4. **Capture.** A NEW skill only when the area has its own language, kit, gates and lessons (3.1, 3.2, 3.5, 3.6);
   otherwise the lessons go into the existing skill (3.3 into the philosophy doc and the five skills; 3.4 into
   `/frogma` and `/ui-redesign-tracker`; 3.7 into each). Every area has a `*_SKILL_PLAN.md` with a round table
   (`ABILITY_ICON_SKILL_PLAN.md`'s shape) that is updated in every change that moves a step; a new rule goes to the
   language doc; a check that could have caught it becomes a gate with a negative control.
5. **Measure the payback.** `python3 Tools/Build/rework_ledger.py --skills` once a month, the numbers into section 6:
   red checklist sections per month, reverts, "again", compile fixes, mockup rounds per screen, rounds to pass per
   unit, and the skill grades. The owner's words stay the bar ("I can't tell it isn't the original"); the counters say
   whether the bar is being reached in fewer rounds.

## 5. Decisions this plan waits on

| # | Decision | Whose |
|---|---|---|
| D1 | The Compute VM's Unity as the PR compile runner (3.1 R0) and the look-sheet machine (3.1 R1-R2), or a CI runner instead | Shombith |
| D2 | The look language's draft (3.2 R0), and the first three effects to take through the rounds | Garrett |
| D3 | Confirm or strike the mined principles in your area (3.3 R1) | Garrett, Caleb, Shombith, Yash, each their own |
| D4 | `UI_LESSONS.md` from the picks, the mockup gate, and which screen's parity sheet first (3.4) | Shombith |
| D5 | Briefs only, or synthesised reference stems too; mobile banks before the mobile launch (3.5) | Charles, Garrett, Shombith |
| D6 | Which hand-test rows to script first, and whether the live UGS check may be scheduled (3.6) | Yash, Shombith |
| D7 | The order in section 4, or another | the four |
| D8 | The skills' names | the four |

## 6. Status (update in every change that moves a step)

| Entry | Step | Status (date) |
|---|---|---|
| 3.1 See the real game | R0 the real editor compiles every PR | `claude/ci-hosted-unity-compile` exists (2026-11-01); waits on the licence secrets and a merge (D1) |
| 3.1 See the real game | R1-R2 the look-sheet kit | **built 2026-11-07 night**, not yet run in Unity: `Docs/LOOK_SHEET.md`, `/unity-look`, the CI job `look-sheet.yml`; the first run is a checklist entry |
| 3.2 The world's look | R0 the audit, the baseline gate, the language, the vocabulary, the lessons, the skill | **built 2026-11-07 night**: `/world-look`, `Tools/Build/look_kit/audit.py` (67 effects, 72 signals on 25 as the baseline), `Docs/Shaders/WORLD_LOOK_SKILL_PLAN.md`; the language is a draft for Garrett (D2) |
| 3.3 Decide before building | R0 the ledger, the check, the brief, the brief step in four skills | **built 2026-11-07 night**: `Docs/claude/PRINCIPLES.md` (271 entries, every one verified at its source line by `principles.py --check`), `DESIGN_BRIEF.md`; every entry drafted, waiting on its owner (D3) |
| 3.4 UI | R0 `UI_LESSONS.md`; R1 the mockup gate | **built 2026-11-07 night**: `.claude/skills/frogma/references/UI_LESSONS.md` + `ui_lessons.py`, `check_mockup.py` (measured on five boards: the noise gate tells a rejected round from a picked one in four cases of five); the parity sheet (R2) waits on 3.1 (D4) |
| 3.5 Sound | R0 the audit and the language; R1 the Meerkat briefs | **R0 built 2026-11-07 night**: `sound_slots.py` (13 real failures on the tree: three one-shots in loop slots, seven unbanked events, one renamed, no mobile banks), `SOUND_LANGUAGE.md` (draft), `/sound-brief`; the tool's self-test has one open assertion; the Meerkat briefs are owed (D5) |
| 3.6 Multiplayer | R0 the skill; R1 the scenario scaffold | **built 2026-11-07 night**: `/multiplayer` + `references/HAND_TESTS.md`, `net_scenarios.py` (rows 2-5 as data, a judge over `net_logs.py`, `--run` unverified: no port build here); B48 is claimed twice, next free B50 (D6) |
| 3.7 Near-A skills | the measurement and the owed negative controls | **measured 2026-11-07 night** for fauna-creation, cardart and prism-morph (a 'Round status and what each gate still owes' section each); flora and ecology not reached |
| ledger | `Tools/Build/rework_ledger.py` | built 2026-11-07, the numbers in section 2 |
| elemental loop | scaffolding 1-2: the one-minute fun card and the per-rung bars | **built 2026-11-12** (Garrett picked each design choice on a card): `FunCard.cs` after every arcade match, `fun_cards.py` (sign-offs), `rung_bars.py` + `rung_bars.json` (defaults, then learned); the card has not been answered in the editor yet |

## 7. What each step taught (append; newest last)

- 2026-11-07, the audit: the two areas that got better had a sheet and a gate; the areas that keep being redone have
  neither, and the owner is the first to see the work. The redoing is not a craft problem first; it is a seeing
  problem and a deciding problem.
- 2026-11-07 night, the build: (1) **look before you build was worth a night by itself**: the real editor already
  compiles on hosted runners (`claude/ci-hosted-unity-compile`) and the per-mode smoke test (PR #1754) already plays
  every card headless; the look-sheet kit was built on both in hours instead of rebuilding them. (2) **A ledger
  catches the second violation, never the first**: checked against the eight retirements, the principles ledger would
  have stopped one and flagged one late; the brief, which asks the owner before the build, is what covers the first.
  (3) **A gate that cannot run in a cloud session is not a gate**: the verify rule's reference compile needed a .NET
  root the proxy refused, so one was assembled from nuget packages and made a repo tool; the same for the two GLSL
  harnesses of /prism-morph (`glslangValidator` absent here) is still owed. (4) **The writer stages of a workflow are
  the ones that hit the account's model limit**, after the miners and auditors had spent most of the budget; the
  mined data survived in the journals and the writing was done in the session itself. Next time: fewer, larger
  agents, the writer first on a small sample, and a budget per workflow. (5) **Every area had a measurable
  first finding the same night** (13 mis-wired or unbanked sound slots, 72 flat signals on 25 effects, one undecided
  UI round, two B48s): the audit tools pay on their first run; the craft rounds need the owners' eyes.

- 2026-11-12, the fun card: (1) **ride what already sends**: the card is three taps because the Bug Hunt send
  already pushes flight records from every editor to a branch, so the answer goes the same way and the board reads
  it with no new service. (2) **The recorded flights answered a question nobody had asked**: 521 races were already on
  the bugledger branches, and the first run of the rung bars found seven rungs whose human finish rate misses its bar.
  (3) **Count only races run to the end**: 175 of the 521 were left early (Play stopped), which made Switchback I1 look
  like a 39% finish instead of 47%. (4) **One question per card worked**: three decision cards answered in about ten
  minutes settled the design.

## 8. Appendix A: the skills, hand-audited (2026-11-07)

Grade: A = the six parts of section 1 (kit, gate, sheet or measured loop, lessons, negative control, a plan with
rounds); A- five; B+ kit, gate and a measured loop; B kit and gate; C prose; D thin by design.

| Skill | Purpose | Kit | Gate | Measured loop | Lessons | Grade |
|---|---|---|---|---|---|---|
| vessel-model | hulls from Blender specs | `vessel_model/` | `build.py --check` | Cycles sheet, dossiers | LESSONS 55 | A |
| ability-icon | the ability-row glyphs | `icon_kit/` | `verify.py`, `--check` | 128/60/40 sheet | LESSONS 59 | A |
| vessel-studio | the one studio artifact | `build_artifact.py`, `parity_gate.py` | `check_studio.cjs` | scorecard, AI race panel | D1-D38 | A- |
| flora | plants | fits, harnesses | `--check`, `--self-test` | measured roster | §9 traps, 3 negative controls | A- |
| labmaker | prototype labs | lab harnesses | `--self-test` | "a scorecard that can say NO" | rounds, traps | A- |
| prism-morph | prism deformation | `verify_prism_*.py`, clang harness | `--check`, budget gate | convergence proofs | partial | B+ |
| fauna-creation | new creatures | `fauna_kit/` | `celestial.py` says no | `preview.py` | §5 traps | B+ |
| fauna | the alive contract | heart-seat gate | `--check` | measured roster | §9 | B+ |
| cardart | card backgrounds | `card_art_harness/` | `--check` | renders looked at | §3 traps | B+ |
| ecology | the cell ecosystem | fits, bakes | collider budget | baselines | §2.6 traps | B+ |
| vessel | every vessel class | `author_*_assets.py`, checks | many | measurements | "rules relearned" | B+ |
| vessel-ai | AI rules and test kit | sims, `learn_from_flights.py` | `check_ai_no_state_writes.py` | sims, Third Eye | §5 traps | B+ |
| duo-vessel | crew vessels | `check_crew_vessels.py` | `--self-test` | lab-first | §6 traps | B+ |
| perf-hunt | the perf loop | `perf_hunt.py` | `check_mathf_params_alloc.py` | before/after | partial | B+ |
| compute-job | a search from a problem | evaluators | self-test, negative control | the loss | none | B+ |
| frogma | the design board | `frogma.py` | `--self-test` | rounds and picks | §3, §5 | B+ |
| arcadegame, arenagame, EndGameConditions, artifact-to-unity, amoebius-artifact, refactor, ship (3), ui-redesign-tracker, froglet-skills, launch-flow, regatta-balance, compute, fleet-codex, bug-ledger | | kit | gate | none or partial | some | B |
| element-ability-table, compute-scout, team-tasks, phone-hunt, qa-backlog, bug-closeout, bioform-forge, asset-surgery, amoebius, androidbuild, ab-test, reorient, ship-tools, frogma-new | | | | | | C |
| frogma-show, to-bleeding-edge, launch-flow-<platform> x7 | | | | | | D |

Of the C skills, the ones that MAKE content and would gain from the shape are `bioform-forge`, `asset-surgery`,
`ab-test` and `element-ability-table`; the rest are process skills, where a kit does not fit.

## 9. Appendix B: regenerate any number

- The ledger (commits, reverts, retirements, "again", compile fixes; the checklist; the menu rounds; skill grades):
  `git fetch origin && python3 Tools/Build/rework_ledger.py --skills` (`--json` for the rest; `--self-test`).
- Red checklist headers at both levels: `grep -cE '^#{2,3} .*🔴' Docs/UNITY_VERIFICATION_CHECKLIST.md`.
- Empty sound slots: `grep -rhE '^\s+Path: ' --include=*.prefab --include=*.asset --include=*.unity Assets | wc -l`
  against the same with `'^\s+Path: *$'`; events in the banks: `grep -c 'event:/' "Cosmic Shore/Build/GUIDs.txt"`.
- Shaders and effects: `find Assets -name '*.shadergraph' | wc -l`; `.shader` outside `Plugins/` and TextMesh Pro;
  `grep -rh --include=*.prefab '^ParticleSystem:' Assets/_Prefabs | wc -l`.
- Placeholders: `find Assets -iname '*placeholder*' -not -name '*.meta' | wc -l`.
- Modes against generators: the `= N` members of `Assets/_Scripts/Data/Enums/GameModes.cs` against
  `ls Tools/Build/author_*_assets.py`.
- The owners' dated quotes: `grep -rhoE "(Garrett|Shombith|Yash|Caleb), 20[0-9]{2}-[0-9]{2}-[0-9]{2}[^\"]{0,25}\"[^\"]{8,}\"" Docs .claude CLAUDE.md | sort -u`.
- Wormhole rounds and removals: `grep -o -i 'round [0-9]\+' Docs/Shaders/WORMHOLE_SHADERS.md | sort -u`;
  its `## ` headings say which looks were removed.
- The hand-offs: the `team-task` issues with their `kind:` and `area:` labels (`python3 Tools/Build/team_tasks.py list`,
  or the GitHub API), the last 80 PRs' titles and bodies for revert / again / fix-after / follow-up.
- The skills' line counts and reference files: `wc -l .claude/skills/*/SKILL.md`; `ls .claude/skills/*/references`.
