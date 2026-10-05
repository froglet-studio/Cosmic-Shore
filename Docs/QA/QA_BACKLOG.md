# QA Backlog — untested development on `bleeding-edge`

**Generated:** 2026-08-13 · **Scan covers:** merges up to `50b563f7` (PRs #583–#710
plus the direct branch merges: Dog Fight, Wildlife Liberation, Astro League
improvements, Cleave scoring, game-data JSON schema, profile/ads, quit button,
menu camera, pause-menu perf, display-name validation, Windows build failures)
· **Owner of this file:** the `/qa-backlog` skill — do not hand-edit.

Every item below landed on a shared branch **without ever being opened in Unity**
by its author (or was play-tested only in part). Work top-down: P0 first.

**How to report:** copy `RESULTS/TEMPLATE.md` → `RESULTS/<date>-<tester>.md`,
fill the table, commit. Full workflow: `Docs/QA/README.md`.

**Status key:** ⬜ never run · 🟡 partially confirmed · 🔴 failed, awaiting fix
· ⛔ blocked. Items that PASS leave this file (→ `ARCHIVE.md`).

**Standing preconditions for every item** (do these once per session):
- Get the build being tested (branch + commit), then let Unity **finish importing
  it completely** before you judge anything — a leftover `Library/` folder from an
  older build hides the changes and is the most common reason a test looks broken
  when it is not.
- Keep Unity's **Console** window open, with **Error Pause off** and *Clear on Play*
  off, so nothing scrolls away or halts the game mid-test.
- Unless an item says otherwise, **"freestyle"** means: start the game, wait for the
  main menu, then take control of the ship — **click the centre of the screen**, or
  press **Y** on a gamepad. (Press it again to hand control back.)

---

## Priority 0 — gates. Nothing below matters if these fail.

### QA-BUILD-WINDOWS-PLAYER ⬜ — a Windows IL2CPP player reaches the main menu
**Source:** PRs #688, #690, #692, #693, #698, #699. **Why P0 and why it is separate
from QA-BUILD-COMPILE:** every defect in this chain was **invisible in the Editor**.
The edit-mode tests were compiling into `Assembly-CSharp` and shipping into the
player, killing the UnityLinker (`IL1005` / `nunit.framework`); once that was fixed,
the player crashed on **every** login inside `PauseMenu.Prewarm`; the root of that
was a type-punned `pauseMenuPanel` reference in `Pause_Menu_Panel.prefab`. None of it
can be judged from a running Editor — it needs a player build.

1. Produce a **Windows IL2CPP player build** (or take the one CI produced from this
   branch). Record whether the build itself completes — the linker stage is the gate.
2. Launch the player. Sign in and reach `Menu_Main`.
3. Open and close the **pause menu** in a game round.
4. Read `Player.log` end to end afterwards.

**PASS:** the build completes with no `IL1005` / `Mono.Cecil` resolution failure; the
player launches, signs in, and reaches `Menu_Main` without the crash handler firing;
the pause menu opens and closes; `Player.log` contains no `Couldn't fetch Ads Service
game Ids` (Unity Ads was removed in #694) and no `GetComponent` crash frame under
`PauseMenu.Prewarm`.
**FAIL:** a build that dies in the linker · a player that closes on reaching the menu
· any managed exception in `Player.log` that does not appear in the Editor. Attach the
last 100 lines of `Player.log` for any failure.

### QA-MENU-CAMERA-RIG ⬜ — Menu_Main's camera is no longer Cinemachine
**Source:** PR #671 (`4245cf8f` — 335 lines of vCam orchestration deleted, replaced by
a direct-transform rig driven by four `MenuCameraConfigSO` assets in
`_SO_Assets/Camera/`). **Why P0:** the menu camera is the surface almost every other
item on this list is observed through, and both freestyle transitions were rewritten.

1. Enter `Menu_Main`. Watch the idle menu shot for ~30 s. Confirm there is **no**
   Cinemachine brain/vCam driving it (the scene camera moves under
   `MainMenuCameraController`).
2. Cycle the four rig kinds (**OrbitVessel / CinematicTrail / ChaseTight / TopDownPan**)
   however the scene exposes them, and watch each config **glide** into the next.
3. **Enter freestyle** (click the centre of the screen, or press **Y** on a gamepad)
   and watch the whole transition.
4. **Exit freestyle** back to the menu and watch the whole transition.
5. Do steps 3–4 five times in a row, including while the AI vessel is turning hard.
6. Swap vessel (Vessel Changer toy), then enter and exit freestyle again.
7. Trigger a teleport / respawn while in the menu shot if you can reach one.

**PASS:** the menu shot always frames the local vessel; entering freestyle blends
seamlessly from the menu framing to the gameplay camera with **no snap at either end**
and no swoop through world space while the vessel is moving; exiting is seamless the
same way; config switches glide rather than cut; a vessel swap leaves the rig framing
the new hull; no `MainMenuCameraController` / `NullReferenceException` in the Console.
**FAIL:** a visible snap, jump or swoop at either end of a freestyle transition · the
camera losing the vessel (framing empty space) · a camera left stuck in the gameplay
pose after returning to the menu · any exception from the camera controller.

### QA-SCORING-CLIENT-MIRROR ⬜ — non-host players no longer start with the last game's score
**Source:** Cleave second merge (`a6066b54`), logged as `Docs/ScoringSystem/BUGS.md`
**B17**. **Why P0:** this was reproduced *every time* by the reporter and it corrupts
the scoreboard of **every multiplayer mode** — so any score you read while testing
another item is untrustworthy until this passes. Fix is
`RoundStats.SyncLocalMirrorsFromNetwork()`, called from `Player.InitializeForMultiplayerMode`
at every scene entry. Needs **MPPM with at least 2 virtual players** (host + client).

1. Host + one client. Play a multiplayer game (any mode) to the end so scores are
   non-zero. Note the client's final score.
2. Return to the menu. Launch a **second** game. At the countdown, read the score on
   **both** machines before anyone scores anything.
3. Return to the menu again and launch a **third** game. Read both scores at the
   countdown again — this is the launch the reporter saw fail.
4. Repeat once with the client **exiting a game mid-way** before the next launch.
5. Repeat once using **Play Again** (replay path) rather than returning to the menu.

**PASS:** at the countdown of every game, **every** player reads 0 on **every**
machine — the client's score is not carried over from the previous game, and the host
and client agree throughout the round.
**FAIL:** any non-host player starting a game with a non-zero score · host and client
disagreeing on a score at any point · the score of a player who left mid-game
reappearing in the next round. Record the exact game number (2nd, 3rd…) at which the
drift first appears.

### QA-PRISM-OCCLUSION ⬜ — camera↔vessel prism corridor (shader, magenta risk)
**Source:** PRs #661, #677 (kernel goes hard-edged — ships as **SHATTER**), #702
(screen-door dither becomes **all** prism transparency; the corridor now stops short
of the nose; debris gains an erosion wipe). Platform law, hand-authored HLSL, no
compiler. Reference: `Docs/PRISM_CLOCK_WIRING_CHECKLIST.md` § occlusion corridor.

1. Load any scene with prisms. **Look at the prisms first** — an HLSL compile failure
   turns every prism magenta on load. If magenta: reimport once (stale Library), and
   if it persists, stop, FAIL, attach the shader error.
2. Freestyle: lay a wall of trail, then fly so the wall sits between the camera and
   your ship.
3. Study the stipple pattern in the fading band. It should read as a **cracked lattice
   of walls** (SHATTER). Round flecks mean the kernel reverted to Worley; triangles
   mean it reverted to SHARD — report which you see.
4. **Ram a prism.** The corridor deliberately stops short of the nose so impacts read.
5. Fly away from the wall so nothing occludes you; then hold still with prisms in the
   corridor for ~10 s and watch for strobing.
6. **Fly at speed** through dense mass and watch for a beat/flicker on the fade.
7. **Shoot something and watch the debris** — each face should wipe with one hard
   erosion front that finishes before the piece retires.
8. Swap to a much larger/smaller vessel (Vessel Changer toy) and repeat step 2.
9. Run **FrogletTools ▸ Vessels ▸ Audit Corridor Vessel Radii** and check the Console
   for `[PrismOcclusion]` messages.

**PASS:** prisms between camera and ship dissolve so the ship stays visible; they snap
back opaque as you leave; no hard seam or banding at the boundary; the stipple flows
rather than strobes, including at speed; **mass at contact range does not hide the
ship when you ram** and impacts still read; debris erodes with a clean front; the
corridor rescales with vessel size; the radii audit reports ship-sized hulls for every
vessel; zero `[PrismOcclusion]` errors.
**FAIL:** magenta prisms · ship hidden behind prisms (especially at ram range) ·
visible flicker/strobe at speed · hard-edged rectangle or ring at the boundary ·
debris that never erodes or erodes after it retires · corridor obviously the wrong
size on one vessel · any `[PrismOcclusion]` error.
**Judgement call to report:** interiors read as thinner shells mid-fade (the cost of
the back-face separation at power 3.0). Say whether that is acceptable.

### QA-SPEED-TUNNEL ⬜ — the speed tunnel as a fleet-wide law
**Source:** PR #668 (deleted the per-vessel `SpeedTunnelEffectController`; a single
static driver now covers all 11 vessels). Reference: `Docs/SPEED_TUNNEL.md` §5.

1. Fly **Rhino, Manta, Dolphin, Squirrel, Sparrow, Serpent** in turn (Vessel Changer
   toy in freestyle). For each: accelerate to top speed, then drop to cruise.
2. Boost a **Dolphin** and a **Serpent** (both top out ≈210) and compare the effect.
   *(Note: the Dolphin's boost was retuned in #681 — see QA-DOLPHIN-SPEED-TUNE. If its
   peak now reads ≈357, this comparison moves; record what you see rather than forcing
   the old equality.)*
3. Swap vessels *while the tunnel is engaged* (boost → open Vessel Changer → swap).
4. Play **Astro League** and score a goal — watch the replay camera.
5. Change the FOV setting in Settings mid-session, then boost again.

**PASS:** every vessel narrows FOV + relaxes Panini purely as a function of its own
speed and returns *exactly* to its pre-boost framing; two vessels at the same speed
look the same; a mid-effect vessel swap leaves the new vessel with a correct,
non-stuck view; the goal replay camera is **not** tunnelled; after a FOV setting
change the effect anchors to the new home value.
**FAIL:** any vessel with no effect · FOV stuck narrow after release or after a swap
· the replay shot visibly zooming · a snap to a foreign FOV when the setting changes.

### QA-PRISM-CLOCK-ENV-SNAP 🟡 — environment-lay prisms snap (known defect, confirm scope)
**Source:** PR #642 item C13. `SegmentSpawner`-instantiated prisms get no companion
entity, log `[PrismClock] STRICT MODE` and pop into existence instead of blooming.
Strict mode is working as designed; QA's job is to bound the blast radius.

1. Launch **Skim Race / SkimRace** (any intensity) and watch the track build.
2. Read the Console for `[PrismClock] STRICT MODE` errors; note the count and whether
   it is bounded (one burst at build) or continuous.
3. Fly the **Wander** toy in freestyle, choose **Without Ark**, and watch scenes arrive.
4. Note every *other* place prisms appear to snap rather than bloom (cell environments,
   trails, flora, fauna, cage bars, the new Boneyard and Wildlife Liberation cages).

**PASS (for this pass):** the snap and the STRICT MODE errors occur **only** on
`SegmentSpawner` tracks and Wanderway scenes, are bounded to build time, and nothing
else in the game snaps.
**FAIL:** snapping/errors anywhere else (especially vessel trails, cell environments or
lifeforms), errors continuing every frame, or the errors accompanied by prisms that
never appear at all.

### QA-DOGFIGHT-MODE ⬜ — "Dog Fight" has never been opened
**Source:** PR #703 + the `claude/dog-fight-game-mode-it9xgy` merges. Whole new game
mode (`GameModes.DogFight = 41`), Sparrow-only, authored headless — **the platform's
first mode scored on vessel-vs-vessel combat**. Arena is the **Boneyard**. Reference:
`_Scripts/Controller/Arcade/DOGFIGHT.md` § In-editor verification.

1. Open `MinigameDogFight.unity`. Confirm no `Missing (Mono Script)`, the controller
   shows `rule = DogFightScoringRule` with milestone fractions 0.25 / 0.5, AI fields
   1.5 / 0.6 / 120, and the **Cell lists four configs with Cell Type Choice =
   Intensity Wise**.
2. Launch at intensity 1. The arena must build: a bowl of crust with 6 hulks, 9 leaning
   spires, 4 girder cages, 3 broken overpasses and a central reactor.
3. **Fly INTO a hulk** through its torn-open side and sit there. This is the headline
   check — the ribs must leave gaps you can slip between and you must not be visible
   from outside.
4. Launch intensity 1 and intensity 4 back to back. Confirm 4 is **not** Atlantis
   (Scurry's drowned garden-city with world-tree and terraces) — if it is, the
   intensity-4 config points at the wrong prefab. Watch frame time at 4 (34,654 prisms,
   the heaviest arena of any party game).
5. Run **FrogletTools ▸ Ecology ▸ Measure Cell Environment Baselines**; expect
   **9,043 / 16,100 / 24,807 / 34,654** prisms for intensities 1–4.
6. Check spawns: everyone starts ~700 u out, spread over a **sphere**, facing the
   arena, nobody inside it.
7. **Bullets score.** Shoot an opponent with full-auto: **+1 per hit**. Shoot a hulk,
   the crust or a scavenger: **no** score movement.
8. **Missiles score 50, once.** Hit an opponent dead-on with a skyburst: **+50, not
   +100**. Then detonate one *near* an opponent without touching them: also **+50**.
9. **A client's hits score.** Host + at least one client; have the **client** do all
   the shooting for 30 s. Their score must rise on **both** machines. (The reverse test
   is not equivalent — the host records directly.)
10. In a 2v2, shoot and splash a **teammate**: no damage, no points, scoreboard flat.
11. Play a full round to the point target (default **120**) and watch the scoreboard.

**PASS:** the scene opens clean; four visibly different intensities all reading as the
Boneyard; hulks are hollow and hideable; baselines within a few hundred of the expected
counts; spherical outside spawn; bullets = 1, missiles = 50 exactly once, scenery = 0;
a client's hits register on both machines; friendly fire scores nothing; the round ends
on the domain point target and the scoreboard shows domains.
**FAIL:** every intensity looking the same (Cell not on `IntensityWise`, or configs out
of order) · intensity 4 rendering Atlantis · solid, un-enterable hulks · baselines off
by thousands · spawning inside the arena · a missile scoring 100 · scenery scoring ·
a client's hits appearing only on the client · teammates damaging or scoring off each
other · a non-Sparrow vessel spawning.

### QA-WILDLIFE-LIBERATION ⬜ — "Wildlife Liberation" has never been opened
**Source:** PR #678 + the `claude/wildlifeliberation-game-mode-j410ej` merges. Whole
new game mode (`GameModes.WildlifeLiberation = 40`), Sparrow-only hunt, authored
headless. Reference: `_Scripts/Controller/Arcade/WILDLIFE_LIBERATION.md` § In-editor
verification.

1. Open `MinigameWildlifeLiberation.unity`. No `Missing (Mono Script)`; controller
   shows `rule = WildlifeLiberationScoringRule`, milestones 0.25 / 0.5, and the **Cell
   lists four configs with Cell Type Choice = Intensity Wise**.
2. Launch at intensity 1: **three concentric cages at 1050 / 600 / 200** with big empty
   rooms between them. Headline check — one cage, or layers that look adjacent, means
   the Cell is not on `IntensityWise` or the configs are out of order.
3. Confirm the cage openings are **triangles** at intensity 1–2, with no dense polar
   cap (fly a full orbit — the weave should look the same from every angle).
4. Relaunch at intensity 3 → the **outer** cage is a BOX with square openings and heavy
   corner posts. Intensity 4 → the **middle** cage is a box too and the core is the
   tightest weave.
5. Run **Measure Cell Environment Baselines**; expect **9,206 / 11,456 / 11,680 /
   12,870** prisms for intensities 1–4.
6. Check spawns: everyone on **one horizontal circle** ~1150 u out, facing the jail,
   nobody inside.
7. **The kill path.** Shoot a tadpole (1 body prism): it must **die** — wither/suction
   out and drop an elemental crystal — not keep swimming. Then a brittlestar (10
   prisms): ten hits, dies on the last. The counter ticks **once per creature**, not
   once per prism.
8. **Only your kills count.** Watch a shark eat a tadpole; watch one starve. Neither
   moves any score. Shoot a cage bar: no score.
9. Fly a full lap of each room. Each tier must stay in its room and be **spread around
   it** — not clumped, and above all not clumped at the arena centre. Nothing swims
   between rooms; nothing chews a cage bar.
10. Note the rough headcount at the countdown (~610) and again three minutes in — it
    must be visibly denser (heading toward ~1,409). That is reproduction, and it is
    what makes the target reachable.
11. **Sparrow-only, solo:** pick a different vessel in an earlier game, then launch
    this — you should spawn a Sparrow with a `clamping selected vessel` log line.
12. **Sparrow-only, multiplayer:** have the client pick a Dolphin — they must still get
    a Sparrow.
13. Play a round toward the kill target (default **250**).

**PASS:** three well-separated cages whose shape changes with intensity; baselines
within a few hundred; equatorial outside spawn; a 1-prism creature dies to one shot and
drops a crystal; kills counted per creature and only when you caused them; tiers stay
spread in their own rooms; the population visibly grows; both players get a Sparrow
regardless of their pick.
**FAIL:** one cage / adjacent layers · a creature that survives losing all its prisms ·
a counter ticking per prism · score moving for a starvation or a shark kill · creatures
clumped at the arena centre or wandering between rooms · a flat population three minutes
in · a non-Sparrow vessel spawning on either machine.

### QA-CLEAVE-MODE ⬜ — "Cleave" has never been opened
**Source:** PR #662 + later tuning + the second `claude/rhino-cage-destruction-mode-1t9e3q`
merge (`a6066b54`, which carried the B17 scoring fix — see QA-SCORING-CLIENT-MIRROR),
then the **four-arena rework** (three of the four nested-shell intensities replaced with
unrelated arenas), the **2× scale-up** of all four, then a **3× further spread of
intensities 1 and 2** (envelope 720 → 2160, the Panes at triple rib spacing, their own
3600-radius membrane), and finally a **re-author of intensity 2** from corrugated wave
SHEETS into five wide wavy ROADS, and finally a **PRISM-SIZE pass**: prism size came off the
envelope scale onto its own dial pinned at 2 on all four rungs, so the two 6× arenas are now
built from small pieces (a pane plank 34 long, not 102; a road plate 44, not 132) and their
counts tripled to **15,380 / 14,277**, with the Swell's five roads also re-spread across the
whole shell (spines 780…1,728). The ladder is targeted **1200 / 1200 / 1500 / 1500** and the
comeback rate is **0.0125**. Whole game mode (`GameModes.Cleave = 39`), authored headless and
never run. **Intensity 1 was flown and approved BEFORE the prism pass**, so how it reads has
deliberately changed — same arena, finer grain. Reference:
`_Scripts/Controller/Arcade/CLEAVE.md` § In-editor verification (29 steps; this is the short
form).

1. Open `MinigameCleave.unity`. Confirm no `Missing (Mono Script)`, the controller
   shows `rule = CleaveScoringRule` with milestone fractions 0.25 / 0.5 and a field named
   **`aiArenaRadiusOverride`** (a stray `aiCageRadiusOverride` means the scene is stale),
   and the Cell lists **four** configs with **Cell Type Choice = Intensity Wise**.
2. Launch each of intensities **1 → 4** in turn and look at the arena, not the score.
3. Run **FrogletTools ▸ Ecology ▸ Measure Cell Environment Baselines**; expect
   **15,380 / 14,277 / 14,731 / 16,423** prisms for intensities 1–4.
4. **Scale:** from the spawn ring, does the arena fill the view? Time a boosted
   straight-line run across the whole thing, at intensity 1 AND at intensity 4.
5. Sight-check the arena's far side against the membrane shell — nothing may poke through.
   At intensities 1–2 the shell must be the big one (3600), well outside the mass.
6. Look closely at a pane rib / a Swell road deck / a twistband deck plate. **Do intensities
   1 and 2 read as LOTS OF SMALL PRISMS rather than a few big slabs?** That is what the
   prism pass exists for. **Intensity 2 is the one to scrutinise** — it is brand-new geometry
   nobody has flown, twice over.
6a. **Swell only:** find a road from the spawn ring (can you even see one?), put the blade
   on its deck and follow it through a full lap. Then deliberately run wide on a tight
   corner, and separately fly the INSIDE verge of the tightest corner on each of the five
   roads.
7. Select one laid arena prism at intensity 1 and read its transform scale.
8. Ram a plain prism. Then find a **danger** prism (distinct material) and ram it.
9. Watch an AI Rhino for a minute at intensity 1 AND at intensity 4.
10. Play a full round to the target at intensity **1** and again at **4**, and watch the
    goal row and the scoreboard count.
11. **Arcade card preview (new):** open the Cleave card in the Arcade screen WITHOUT
    launching, and step the intensity row 1 → 4. The preview vessel must open OUTSIDE the
    arena on every rung, and the arena in the window must visibly CHANGE between rungs
    (panes → roads → shells → ribbons), not just resize.

**PASS:** four arenas that look nothing like each other — angled **slabs**, wide wavy
**roads**, three nested **shells**, twisted **ribbons** · baselines within a few hundred
of 15,380 / 14,277 / 14,731 / 16,423 · the arena spans most of the view from spawn, and a
boosted crossing takes **~3.6 s** at intensities 1–2 and **~1.2 s** at 3–4 · the far side
sits well inside the membrane at every rung · ribs, troughs and deck plates read as
**continuous surfaces**, not as beads, and every rung reads as MANY SMALL pieces rather than
a handful of slabs · a pane rib prism at intensity 1 measures roughly **6.8 × 6.8 × 34**, a
mullion **10.4 × 10.4 × 44** and a Swell deck plate **44 × 5.2 × 44** · a Swell road reads as a carriageway
(gold crown, blue shoulders, jade verges), holds an unbroken cut through a whole lap, bites
only on the OUTER verge of tight corners, and never doubles back over itself on the inside
of one · a plain prism shatters on **one** hit with no shield to shed · the danger prism
also one-hits but full-stops you, debuffs all four elements ~4 s and resets boost · the AI
orbits **outside** and cuts on transits at both rungs · **no fauna hatch at any point** ·
the goal row counts to **1,200** at intensities 1–2 and **1,500** at 3–4, and the round ends
there.
**FAIL:** every intensity looking the same (Cell not on `IntensityWise`, or configs out of
order) · the card preview opening the vessel INSIDE the arena, or showing the same arena on
all four rungs, or throwing on a missing cell config (`PreviewCellsByIntensity` must hold
exactly FOUR entries) · baselines off by thousands · the arena reading as a small ball in the middle of an
empty cell, or an intensity-1 crossing taking ~1.2 s (the 3× did not land) · spawning
INSIDE the arena at intensity 1–2 (`spawnRingRadiusFloorByIntensity` is stale) · mass
outside the membrane, or the small 1200 membrane cutting through a big arena · intensity 1
or 2 still reading as a few enormous slabs (a prism dimension is still on `LengthScale`) ·
any surface reading as a dotted line of separated prisms (an along-grain STEP went to the
prism dial while its prism LENGTH did not, or the reverse) · a prism whose long axis measures
exactly **10** (the arena is laying through the wrong prefab) · a goal row reading 1,500 at intensity 1 (the
per-intensity target did not replicate) · two-hit/shielded prisms · an AI that settles
inside the arena, or one parked 936u out in a 2160 arena · any fauna · **intensity 2
specific:** a road you cannot find from spawn, a cut that keeps dropping while you follow
one, a road that ROLLS about its own travel direction (that is the Twistbands' job, not
this rung's), danger on an inside verge or on a straight, or a road folding through itself
at the inside of a bend.

### QA-DOLPHIN-SKIM ⬜ — nobody has ever seen a Dolphin skim work
**Source:** PR #660 + #695 (15× skim-energy nerf → exactly **150 skims / 50 danger
skims** to fill; lime jaw CTA at full energy). The Dolphin's `VesselStatus` pointed at
a **disabled** legacy skimmer, so every contact was dropped silently. The fix is
unconfirmed. Reference: `DOLPHIN_ENERGY_ECONOMY.md` §5–6.

1. Run **FrogletTools ▸ Vessels ▸ Audit Vessel Skimmers**. This is the gate.
2. Freestyle as the Dolphin: fly through cell mass so prisms pass through the skimmer.
   Watch the Console — a single `[DolphinVesselHUDView]` warning means the shared bars
   config is missing or the jaw refs carry no `Graphic`, and the lime CTA is silently
   dead.
3. Skim continuously and count roughly: the meter should take on the order of **150**
   skims to fill (50 on danger prisms), not a handful.
4. Skim to full and watch the **jaws blend to lime**; then ram a prism and confirm they
   go back to white.
5. Hold drift until the ring steps up, then release. Then fly **straight** for 10 s.
   Then drift → release → drift again.
6. Hit a crystal.
7. Raise Charge to level 5 (elemental crystals) and plant two team crystals back to back.
8. Run **Audit Vessel Ability Rows** — Dolphin should read 4/4, order ✅.
9. With a second client (MPPM), confirm both peers agree on the level-5 upgrades.

**PASS:** audit reports `Dolphin NearFieldSkimmer: 'EnergySkimmer' OK`; crackle arcs
sweep the skimmer sphere per prism and the HUD jaw icon punches per skim; energy takes
~150 skims to fill; jaws go lime at full and white on a prism ram; the gape widens as
energy fills; drift fills the ring and flying straight does **not**; speed returns to
normal after an interrupted discharge; the crystal fires the cone, empties energy and
flashes the Space icon; two crystals plantable at Charge L5, preview tinted your domain
and blooming (not popping); no `[DolphinVesselHUDView]` warning; both peers agree.
**FAIL:** audit reports anything else for Dolphin · no visible/audible skim feedback ·
a meter that fills in a few skims · jaws that never go lime · ring fills while flying
straight · speed stuck high after drift→release→drift · peers disagree on upgrade state.
*(Serpent is expected to FAIL the same audit — that is a known, separate item.)*

### QA-SHELL-COLLISION ⬜ — shape-precise shielded-prism collision (Burst shell tier)
**Source:** PR #627. Reference: `Docs/SPATIAL_INDEX.md` § "Shell view — in-editor
verification". Touches every skim and every shield pop in the game.

1. **Squirrel on Skim Race:** skim the super-shielded track lining along its length,
   including grazing passes at the spike tips and passes aimed at the gaps *between*
   spikes.
2. **Rhino:** swipe a shielded prism and note at what distance the shield pops.
3. Fly a dense trail while crystals auto-shield prisms around you.
4. Profile a SkimRace round: watch `ShellContact.Build` / `ShellContact.Query` and
   `Physics.SendEvents`.
5. Toggle the runtime A/B switch off and back on.

**PASS:** skims register at the **stella surface** (≈3× the box), spike-tip grazes
hit, aimed-at-the-gap passes do **not**; boost is granted per shell touch; the Rhino
pops at octahedron reach rather than point-blank; no prism becomes untouchable and no
double-fire (pop *and* destroy in one contact); the two markers stay sub-ms and
`Physics.SendEvents` is flat vs. the previous build; the A/B toggle reverts cleanly.
**FAIL:** skims only at the box · gap false-positives · pop-then-destroy · any prism
that cannot be hit at all · marker spikes or a rising `Physics.SendEvents` train.

### QA-EDITMODE-TESTS ⬜ — run the test suites that were written but never executed
**Source:** PRs #659, #639, #627, #641, #668, #651, #673 + **#688/#690** (all 61 NUnit
files were **moved under `Editor/` folders** so they stop shipping into the player, and
`Assembly-CSharp-Editor` was given `InternalsVisibleTo`). That move touched 105 files
and has never been run — a suite that silently stops compiling now looks like "no
failures".

1. **Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All.**
2. Record the **total test count** as well as pass/fail. The move preserved 762 `[Test]`
   methods — a total far below that means suites are missing, not passing.
3. Record every failing test by name and assertion message.
4. Specifically confirm these suites are present and green: `CellSpawnFormationTests`,
   `SkimmerSwingKinematicsTests`, `ShieldShellMathTests`, `VesselElementalMorphTests`,
   `VesselRigPartResolutionTests`, `SpeedTunnelLawTests`, `SettingsAutoDetectorTests`,
   `GeometryUtilsTests`, `PrismOcclusionCoverageTests`, `DisplayNameValidatorTests`,
   `ShipModifierTests`.

**PASS:** all EditMode tests green, the total is ≈762, and all eleven suites appear.
**FAIL:** any red test (record the name + assertion message), a suite that does not
appear at all (it did not compile into the editor assembly), or a total materially
below 762.

### QA-AUDIT-TOOLS ⬜ — run every FrogletTools auditor and record its verdict
**Source:** PRs #637, #641, #653, #659, #661, #668, #646, #650, #702. Each auditor is a
cheap, asset-only check that encodes a contract; several have never been run.

Run each and paste its report into your results file:
1. **Vessels ▸ Audit Vessel Skimmers**
2. **Vessels ▸ Audit Vessel Ability Rows**
3. **Vessels ▸ Audit Vessel Elemental Morphs**
4. **Vessels ▸ Validate Speed Tunnel Law**
5. **Vessels ▸ Audit Corridor Vessel Radii** *(new — re-run after the skimmer-exclusion
   fix; every hull radius must read ship-sized)*
6. **Ecology ▸ Audit Cell-Owned Visuals**
7. **Ecology ▸ Validate Lifeform Crystals**
8. **Ecology ▸ Prism Animation** (validator) **▸ Validate Occlusion Corridor**
9. **Ecology ▸ Measure Cell Environment Baselines**
10. **Game Modes ▸ End Game Conditions** — confirm it lists Wildlife Liberation **250**
    and Dog Fight **120**
11. **Game Modes ▸ Game Mode Prefab Kit ▸ Validate**
12. **Build ▸ Pending Tool Changes** (should list nothing unexpected)

**PASS:** every tool runs without throwing, and each reports either clean or *only*
the known exceptions: Serpent fails the skimmer audit; Manta/Rhino/Serpent are listed
as design-blocked in the ability-row audit; Dolphin/Urchin/Rhino/Grizzly lack elemental
morphs; `SkyboxModel` entries listed under OK in the cell-visual audit.
**FAIL:** any tool that throws, or any *new* failure beyond the known exceptions above
— especially "SCENE-PLACED DUPLICATES" or "DEAD CELL OVERRIDES" being non-empty, or a
corridor radius that is not ship-sized.

---

## Priority 1 — merged features that have never been played

### QA-SWARM-FAUNA ⬜ — swarm fauna + the Swarm cell have never been opened
**Source:** branch `cece/swarm-fauna-game` (authored headless; never compiled in Unity). Full
reference and exact steps: `Docs/SWARM_FAUNA.md` §5. **Why it matters:** a new fauna family
(population-of-tadpoles creatures that morph by majority element), a new cell in the Cell
Selector, and an edit to `Menu_Main`'s `CellConfigs`. The pure-C# sim core is proven headless;
the Unity glue, prefabs and on-screen behaviour are not.

1. Let Unity import; confirm the Console has **no compile errors** naming a `Swarm*` file.
2. Menu_Main → freestyle → **Cell Selector** toy → fly into the **Swarm** station. Enable the
   **Ecology** log channel first (FrogletTools > Toolbox > Logging).
3. After ~6 s, 24 swarms hatch — eight per band (inner whales, middle dragonflies, outer
   pufferfish) — with ~190 plants seeded around them (OVERTUNE pass: ~5,000 always-on hearts at
   the caps, 4.6× the Lattice cell). Watch one feed and grow.
4. Fly through a swarm without firing (scatter + startle ripple; pufferfish plates turn danger).
   Hover still beside the dragonfly (its Time members mob you).
5. Vessel Changer → Sparrow. Shoot tadpoles. Then kill ~36 of the dragonfly's Time members in a
   burst and watch it morph into a jellyfish.
6. Profiler: `SwarmFauna.Update` cost per frame and total frame time with the swarms grown.

**PASS:** no compile/import errors; the three swarms hatch in their bands and swim as recognisable
creatures; tadpoles grow in (never pop); feeding visibly suctions flora prisms; every kill drops a
collectable crystal and the husk shrinks away; the dragonfly morphs (log line
`[Swarm] … morphs time -> space`) with members re-forming in motion; no exceptions in the Console;
`SwarmFauna.Update` under ~2 ms per swarm, and record the total frame time (the overtune is unprofiled).
**FAIL:** any `Swarm*` compile error · the Swarm station missing from the Cell Selector · swarms
not hatching · tadpoles appearing at scale 1 or vanishing instantly · a kill with no crystal ·
a `NullReferenceException` from `SwarmFauna`/`SwarmTadpoleFauna` · a morph that never happens
after the kills. Record frame cost either way, and the cell's live volume after ~5 min (the volume
ladder is modelled, not measured — `Docs/SWARM_FAUNA.md` §4).

### QA-SWARM-GRID ⬜ — the grid swarm (second swarm species) has never been opened

**Source:** branch `cece/swarm-fauna-game` (authored headless; never compiled in Unity). Full
reference: `Docs/SWARM_FAUNA.md` §8, exact steps §5.1. **Why it matters:** a second simulation
core (`SwarmGridCore`, the research's hgrid2 grid morphogen) drives 8 of the Swarm cell's 24
swarms (the inner whales); the middle dragonflies stay on the field core and the outer pufferfish
moved to the third, sort core (`QA-SWARM-SORT`), so all three can be compared in one flight. Headless proof: the core compiles, runs, and scores within
hgrid2's own range under the research's unchanged scorer; nothing about Unity is proven. Run
`QA-SWARM-FAUNA` first (it covers import, the Cell Selector and the shared glue).

1. Let Unity import; confirm no compile errors naming `SwarmGridCore`, `ISwarmCore`, or
   `SwarmFauna`. Select `Assets/_SO_Assets/Swarm Fauna/SwarmGridFaunaConfig.asset`: **Model = Grid**
   and the Grid model fields are populated; `Assets/_Prefabs/FloraAndFauna/SwarmGridFauna.prefab`'s
   `config` points at it.
2. Enable the **Ecology** log channel. Enter the Swarm cell (Cell Selector → Swarm). Check the
   hatch lines: whales say `(Grid)`, dragonflies `(Field)`, pufferfish `(Sort)`.
3. Fly inward from the membrane past the sort pufferfish, park beside a field dragonfly (past
   ~840 u from the centre), then a grid whale (past ~600 u). 20–30 s each.
4. Morph a grid whale: kill its majority in a burst (~100 Mass). Then morph a field dragonfly
   (~36 Time).
5. Hover still beside a grazing grid whale for 30 s.
6. Profiler: with the cell grown, record `SwarmFauna.Update` per frame and total frame time; note
   how many swarms are grown.

**PASS:** no compile errors; the grid swarms grow from a knot into recognisable creatures (a whale
reads as a whale at gameplay distance) and visibly JOSTLE inside (members trading places, interior
shuffling) where the field dragonfly sits crisper; a grid morph commits on the tipping kill and
re-forms by members swimming to new places (no molting, no flicker back within 3 s); a grazing grid
body does not spin while it hovers (step 5); no exceptions. Record the frame cost, and the lead's
verdict on which model reads as more alive.
**FAIL:** any compile error · a grid swarm that stays a formless cloud after a minute of feeding ·
a grid swarm whose body rotates continuously while it grazes · a morph that flips back and forth ·
a `NullReferenceException` from `SwarmGridCore`/`SwarmFauna` · grid tadpoles popping in or vanishing
(they share the field swarm's bloom and wither — any pop is a glue bug) · a frame-time cost that
makes the cell unplayable (record it; the dial is `SWARMS_PER_BAND` in `author_swarm_fauna.py`).

### QA-SWARM-SORT ⬜ — the sort swarm (third swarm species) has never been opened

**Source:** branch `cece/swarm-fauna-game` (authored headless; never compiled in Unity). Full
reference: `Docs/SWARM_FAUNA.md` §9, exact steps §5.1. **Why it matters:** a third simulation core
(`SwarmSortCore`, the research's emergent cell sorting) now drives the Swarm cell's 8 OUTER swarms
(the pufferfish, 900–1120 u from the centre). It is the swarm with a LOSSLESS composition corrector:
after a morph, members of the old majority MOLT into the new body's missing elements instead of
lingering as debris (the grid swarm's known flaw) or dying. Headless proof: the core compiles, runs,
scores at Python sort's own accuracy under the research's unchanged scorer, and never kills a
member by itself; nothing about Unity is proven. Run `QA-SWARM-FAUNA` first.

1. Let Unity import; confirm no compile errors naming `SwarmSortCore`, `SwarmCoreShared`,
   `ISwarmCore` or `SwarmFauna`. Select `Assets/_SO_Assets/Swarm Fauna/SwarmSortFaunaConfig.asset`:
   **Model = Sort** and the Sort model fields are populated;
   `Assets/_Prefabs/FloraAndFauna/SwarmSortFauna.prefab`'s `config` points at it, and
   `Swarm Outer Charge Swarm Fauna Config Data` points at that prefab.
2. Enable the **Ecology** log channel. Enter the Swarm cell. The pufferfish hatch lines say `(Sort)`.
3. From the Cell Selector (near the membrane) the first swarms you meet flying inward are the sort
   pufferfish. Park beside one for 30 s while it grazes. Then fly on and park beside a field
   dragonfly and a grid whale for comparison.
4. Fly straight through a sort pufferfish without firing: members scatter, its Charge members puff
   up and their plates turn danger.
5. Morph a sort pufferfish: switch to a Sparrow (Vessel Changer) and kill ~95 of its Charge members
   in a burst (it becomes a dragonfly). Watch the 30 s after the morph.
6. Hover still beside a grazing sort swarm for 30 s.
7. Profiler: `SwarmFauna.Update` for a grown sort pufferfish vs a grown grid whale; total frame time
   with the cell grown.

**PASS:** no compile errors; a sort pufferfish grows from a knot into a recognisable pufferfish
whose ELEMENTS sit in clean tissues (the shielded/danger Charge plates on the shell, the other
elements in their own patches) and whose body animates; newborns visibly swim across the body to
their place; after the morph (step 5) the body re-forms as a dragonfly and surplus members visibly
MOLT (heart shrinks away and re-forms as another element, over ~1 s) until the surplus is gone, with
no member dying that you did not shoot; the body does not spin while it hovers (step 6); no
exceptions. Record the frame cost (headless: ~0.13 ms/step for a pufferfish, ~1/8 of a grid whale)
and the lead's verdict comparing all three models.
**FAIL:** any compile error · a sort swarm that stays a formless knot after a minute of feeding ·
members that freeze in place while the body moves · the old majority's surplus still clinging to the
new body 30 s after a morph without molting · a member that dies with no shot fired and the swarm
fed (a self-inflicted death - the model is meant to have none) · a morph that flips back · a
`NullReferenceException` from `SwarmSortCore`/`SwarmFauna` · tadpoles popping in or out.

### QA-SWARM-ROUND5 ⬜ — the lossless grid swarm and the evolved-rule (evofate) swarm have never been opened

**Source:** branch `cece/swarm-fauna-game` (authored headless; never compiled in Unity). Full
reference: `Docs/SWARM_FAUNA.md` §10 (lossless grid), §11 (evofate), exact steps §5.2. **Why it
matters:** two changes nobody has seen. (1) The GRID swarm now runs the research's `combo` - a member
of a surplus element MOLTS into a missing one instead of lingering, so a grid morph no longer leaves
the old majority clinging to the new body as debris (finding 17) - on an 8^3 grid by default (half the
CPU of 16^3, same measured accuracy). (2) A FOURTH species: the EVOFATE swarm, whose every tadpole is
moved by a trained neural network (the research's evolved G2 rule) with a small designed pull that
sorts it - the most "alive" texture of the four, and the most expensive. It lives in a new outermost
band (the rim, 970-1120 u); the cell still holds 24 swarms (grid 6, field 8, sort 7, evofate 3).
Headless proof: both cores compile, run and score under the research's unchanged scorer; the
network reproduces Python to float32 rounding; zero self-inflicted deaths; nothing about Unity is
proven. Run `QA-SWARM-FAUNA` first.

1. Let Unity import; confirm no compile errors naming `SwarmEvoFateCore`, `SwarmEvoRule`,
   `SwarmGridCore`, `SwarmCoreShared` or `SwarmFauna`. Select
   `Assets/_SO_Assets/Swarm Fauna/SwarmEvoFateFaunaConfig.asset`: **Model = EvoFate** and **Evo Rule**
   points at `SwarmEvoFateRule.json` (a ~470 KB text asset); `SwarmEvoFateFauna.prefab`'s `config`
   points at the config, and `Swarm Rim Space Swarm Fauna Config Data` points at that prefab.
   `SwarmGridFaunaConfig.asset`: **Grid Size 8, Grid Cell 12, Grid Lossless on**.
2. Enable the **Ecology** log channel. Enter the Swarm cell (Cell Selector -> Swarm). Hatch lines:
   jellyfish `(EvoFate)` x3, pufferfish `(Sort)` x7, dragonflies `(Field)` x8, whales `(Grid)` x6. No
   line saying an EvoFate swarm "needs SwarmFaunaConfigSO.EvoRule" (that is the missing-asset fallback).
3. The first swarms you meet flying inward from the Cell Selector (~980 u out) are the evofate
   jellyfish. Park beside one for 30 s while it grazes the Reed plants. Then fly on past ~920 u (sort
   pufferfish), ~740 u (field dragonflies) and ~560 u (grid whales) and park beside one of each.
4. Morph an evofate jellyfish: switch to a Sparrow (Vessel Changer) and kill ~30 of its Space
   members in a burst (it becomes a pufferfish if Charge then leads). Watch the 30 s after.
5. Morph a grid whale (~100 Mass in a burst). Watch the 30 s after: the old Mass surplus should
   MOLT into the new body's elements (heart shrinks away, re-forms, ~1 s each), not linger at the edge.
6. Fly straight through an evofate jellyfish without firing.
7. Profiler: `SwarmFauna.Update` for a grown evofate jellyfish vs a grown grid whale; total frame
   time with the cell grown. Headless CoreCLR: an evofate jellyfish ~0.7 ms/step (SIMD) / ~1.6
   (scalar - what Unity's Mono runs); a grid whale ~0.6.

**PASS:** no compile errors; an evofate jellyfish grows from a knot into a recognisable jellyfish
whose members visibly SWARM (they keep moving on their own inside the body - not riding fixed places
like the field/sort bodies) while its elements stay sorted; after its morph (step 4) it re-forms as
the new creature and surplus members molt, with nothing dying that you did not shoot; after the grid
morph (step 5) the surplus molts away within ~30 s and no debris clings to the new whale; both
react to a ship (step 6); no exceptions. Record the frame cost and the lead's verdict on which of the
four reads as most alive.
**FAIL:** any compile error · the missing-rule error in step 2 · an evofate swarm that stays a
formless cloud after a minute of feeding · evofate members jittering in place every other step (the
"jerky" signature the research removed with `sync`) · grid debris still clinging 30 s after a morph ·
a member dying with no shot fired and the swarm fed · a morph that flips back · a
`NullReferenceException` from `SwarmEvoFateCore`/`SwarmGridCore`/`SwarmFauna` · tadpoles popping in or
out · a frame cost that makes the cell unplayable (record it; the dial is the `swarms` column of
`REGIONS` in `Tools/Build/author_swarm_fauna.py` - keep the total at 24).

### QA-SWARM-ROUND6 ⬜ — the sort swarm's round-6 motion (flat wells, wander, 1-in-8 update) has never been seen

**Source:** branch `cece/swarm-fauna-game` (authored headless; never compiled in Unity). Full reference:
`Docs/SWARM_FAUNA.md` §12. **Why it matters:** the SORT species (the pufferfish band of the Swarm cell, 790-920 u)
changed how every member moves: its wells are flat-bottomed (a tissue fills its region like a liquid instead of
packing into flat crystal sheets), every member drifts on its own slow wander, and only 1 member in 8 re-steers
each step (the rest coast on their last velocity). Headless it is 52/52 on the research's yardstick, inside the
organic band (it was OUTSIDE before: planar), smoother (0.751 -> 0.917, three seeds), zero self-inflicted deaths,
and 2.8x cheaper on CoreCLR (0.155 -> 0.055 ms per swarm-step). Nothing about Unity is
proven: not the compile, not the import, not the look, not the Mono frame cost. Run `QA-SWARM-FAUNA` and
`QA-SWARM-SORT` first.

1. Let Unity import; confirm no compile errors naming `SwarmSortCore`, `SwarmSortParams`, `SwarmFaunaConfigSO`
   or `SwarmFauna`.
2. Select `Assets/_SO_Assets/Swarm Fauna/SwarmSortFaunaConfig.asset`: **Sort Well Dead 0.7, Sort Well Dead
   Time 0, Sort Wander 0.05, Sort Wander Tau 12, Sort Update Fraction 8** (the last five of the Sort block;
   Sort Noise stays 0.1). If they are missing, the import is stale - reimport the asset before judging.
3. Enter the Swarm cell (Cell Selector -> Swarm). Fly to the outer band (~850 u) and park beside a grown sort
   pufferfish for 60 s while it grazes.
4. **The look call (the reason this item exists).** Compare against a pufferfish built the old way: set **Sort
   Update Fraction 1, Sort Well Dead 0, Sort Wander 0** on the config, re-enter the cell, park
   beside one again, then restore the shipped values. Judge: (a) does the new body read as a soft, softly
   churning cloud holding a banded shape, where the old one read as flat plates / a mosaic? (b) at the game's
   10 Hz a member re-steers every 0.8 s - does anything look like it is sliding on rails, or snapping when it
   re-steers? (c) does it fizz (too gas-like)? If it fizzes, try **Sort Noise 0** first (measured twice as
   coherent, slightly less smooth), then **Sort Wander 0.03**; if members slide, try **Sort Update Fraction 4**.
5. Morph one: switch to a Sparrow and kill ~40 of its Charge members in a burst. Watch the 30 s after - the change
   should ease in (no jolt), surplus members molt, nothing you did not shoot dies.
6. Fly straight through a grown pufferfish without firing: the members near the ship must flinch IMMEDIATELY
   (a threatened member re-steers every step whatever its phase), and the body must inflate (threat).
7. Profiler (Mono, the editor): `SwarmFauna.Update` for a grown sort pufferfish, frac 8 vs frac 1 (step 4's
   toggle). Headless CoreCLR: ~0.07 vs ~0.19 ms per step for one pufferfish; Mono is expected 2-3x slower.

**PASS:** no compile errors; the step-2 values are present; the lead (or tester) judges the new body as at
least as alive as the old and not sliding/snapping (record the verdict and any dial changed); the morph eases in
with no deaths; the flinch is immediate; the Mono cost per sort swarm is recorded and frac 8 is cheaper than
frac 1.
**FAIL:** any compile error · stale or missing fields after a reimport · members visibly sliding in straight lines
or snapping every 0.8 s · a body that dissolves into fizz with no readable shape · a member dying with no shot
fired and the swarm fed · a flinch that starts late · a `NullReferenceException` from `SwarmSortCore` ·
tadpoles popping in or out.

### QA-SWARM-ROUND7 ⬜ — three ~1,000-tadpole swarms, simulated off-thread and drawn on the GPU, have never been opened

**Source:** branch `cece/swarm-fauna-game` (authored headless; never compiled in Unity). Full reference:
`Docs/SWARM_FAUNA.md` §14. **Why it matters:** the Swarm cell was climbing back to 15-30 FPS. It is now
three SORT swarms (whale / pufferfish / jellyfish, ~5x the old bodies) over Borromean feeding grounds;
the simulation runs on a worker thread, every living member is drawn from one GPU buffer, and only
members near a vessel are real GameObjects ("proxies"). Always-on colliders 5,008 -> 18. Headless:
64/64 on the shape yardstick at the new size, smoothness 0.882, 1.08 ms of worker CPU per tick for all
three swarms and 0.002 ms on the main thread (CoreCLR). Nothing about Unity is proven: not the shader
compile, not the instanced draw, not the proxy hand-off, not Mono timings. Run `QA-SWARM-FAUNA` first.

1. Let Unity import. Confirm no compile errors naming `SwarmTickJob`, `SwarmMemberRenderer`,
   `SwarmFauna`, `SwarmTadpoleFauna` or `Prism`, and no shader errors on
   `CosmicShore/SwarmMemberInstanced`.
2. Select `Assets/_SO_Assets/Swarm Fauna/SwarmSortFaunaConfig.asset`: **Plan Density 5, Simulate Off
   Main Thread on, Draw Members On Gpu on, Member Shader = SwarmMemberInstanced, Theme set, Engage
   Radius 160, Max Proxies 160**. If they are missing, reimport the asset before judging.
3. Turn on the `Ecology` log channel (FrogletTools > Toolbox > Logging). Enter the Swarm cell (Cell
   Selector -> Swarm). Expect exactly THREE swarms (inner ~545 u, middle ~765 u, outer ~995 u) and
   Borromean plants (closed knotted membranes), no phyllotactic forest. If the console says
   `GPU member drawing is OFF`, record the reason it names - every member then falls back to a
   GameObject and the rest of this item measures the wrong thing.
4. **The look.** Park beside each swarm for 60 s. Each must read as its creature (whale, pufferfish,
   jellyfish), members must glide between ticks (no 10 Hz stepping), newborns must bloom in, nothing pops.
   Hearts sit at each member's nose; Charge members spike to the danger colour when you fly close.
5. **Proxies.** Fly into a swarm. Members within ~160 u should look IDENTICAL as they become proxies
   (there must be no visible swap, double image or flicker at the boundary) and again when you leave.
6. **Kills.** Shoot members (Sparrow), ram them, joust one (Squirrel): each death withers / suctions /
   leaves a skeleton as before and drops its crystal; the killed member must not reappear. Fly away
   and back: leaving range must NOT drop crystals.
7. **Morph.** A full body needs ~5x the kills of round 6 to flip. Kill a big share of the whale's Mass
   members; it must morph (record roughly how many kills it took and whether it felt like a slog).
8. **Prism occlusion corridor:** with members between the camera and your ship, they dissolve like
   prisms do.
9. **Profiler (the reason this item exists).** Let the bodies grow for 5+ minutes, then capture:
   `SwarmFauna.Frame.Draw` and `SwarmFauna.Tick.*` together should be well under 1 ms per frame with
   no vessel near a swarm; `PhyllotacticFlora.Update` must be gone. Read the `[Swarm] ... worker X
   ms/tick` lines (the Profiler cannot see the worker thread): record Mono's figure (CoreCLR 0.3-0.4 ms
   per swarm). Confirm the CPU line does NOT climb over minutes. Then fly into a swarm and record the
   cost with proxies live (`Tick.Proxies`, `Frame.PoseProxies`).
10. Starvation: in a cell with its plants grazed out (or `StarvationSeconds` set low on a copy of the
    config), a starving swarm sheds members that wither to crystals - none simply vanish.

**PASS:** no compile or shader errors; the step-2 values present; three swarms that read as their
creatures and move smoothly; no visible proxy hand-off; every kill drops a crystal and leaving range drops
none; the whale morphs; members respect the occlusion corridor; frame cost with no vessel near is under
~1 ms and does not climb; the Mono worker cost is recorded.
**FAIL:** any compile/shader error · `GPU member drawing is OFF` on a desktop GPU · members invisible,
black, stretched or stepping at 10 Hz · a double image or pop at the 160 u boundary · a killed member
reappearing · a crystal dropped by a member nobody killed · no morph after the majority is cut down ·
a CPU line that still climbs over minutes · a `NullReferenceException` from `SwarmFauna` or the renderer.
**Known costs to judge, not fail:** a member beyond 160 u of any vessel has no collider, so a long-range
weapon (Serpent sniper, Dolphin cone, distant rocket) passes through it; other fauna cannot prey on
members; member bodies do not move the cell's phase ladder (`Docs/SWARM_FAUNA.md` §14.4).

### QA-SWARM-ROUND8-1 ⬜ — weapons reach a GPU-drawn swarm member at range

**Source:** branch `cece/swarm-fauna-game` (authored headless; **never run in the editor**). Full reference:
`Docs/SWARM_FAUNA.md` §16.1-§16.2. **Why it matters:** before round 8 a member with no proxy (> 160 u from a
vessel) had no collider, so a rocket, the Dolphin's cone, the Serpent's rifle and gunfire passed straight
through a whale. Now each weapon queries the members with the SAME volume it uses for prisms, and a hit
member is materialised at its slot and killed by the weapon's own code. Headless proof: the five volumes
match the shipped Burst predicates on 1M points each (0 disagreements), and the grid never misses a member.
Nothing about Unity is proven. Run `QA-SWARM-ROUND7` first.

1. Let Unity import. Confirm there are no compile errors naming `SwarmMemberQuery`, `SwarmTargets`,
   `SwarmFauna`, `ExplosionImpactor`, `SniperShotActionExecutor`, `Projectile` or `Prism`.
2. Enter the Swarm cell. Park a Dolphin ~600 u from the whale (well outside 160 u), bank energy, collect a
   crystal, and fire the cone through the whale. Members inside the cone must die where they are drawn:
   each one dies the way a proxied member shot up close dies, and drops its crystal. Nothing should pop and
   nothing should jump sideways at the moment of death.
3. As a Serpent, scope from ~1,500 u and fire through a dense part of a swarm. The tracer must stop at, or
   pierce through, members exactly as it does through prisms, and the members it passed through must die.
4. As a Sparrow, fire full-auto at a swarm from ~400 u, then fire a rocket. Rounds that cross a member must
   hit it. A rocket's blast must kill the members inside its sphere and spare members of the pilot's own
   domain, exactly as it spares own-domain prisms.
5. In a multiplayer session, kill members from a client. Confirm the kills show on the scoreboard for that
   client (the ordinary `ReportFaunaKill_ServerRpc` path).
6. Profiler: look for a frame spike on a big blast. Explosion member work is capped at 48 materialisations
   per frame; record the worst frame time.

**PASS:** no compile errors; every weapon kills members at range, where they are drawn; own-domain members
are spared; crystals drop; client kills score; no frame worse than the same blast fired into a forest.
**FAIL:** any compile error · a weapon passing through a member at range · a death that pops or jumps · a
member killed twice (two crystals) · an own-domain member killed by a non-friendly-fire blast · a client
kill that never scores.

### QA-SWARM-ROUND8-2 ⬜ — predators hunt and eat GPU-drawn members

**Source:** branch `cece/swarm-fauna-game` (headless only; **never run in the editor**). Full reference:
`Docs/SWARM_FAUNA.md` §16.3. **Why it matters:** proxies were never in the cell's live-fauna registry, so no
predator could see a swarm member. Now `LightFauna` and the worm colony ask the swarms for prey through the
same member query, respecting diet, band and predation immunity measured from the member's real age.

1. Spawn a predator `LightFauna` species and a worm colony into the Swarm cell from the Spawn Matrix.
2. Watch a predator near a swarm for 2 minutes. It must chase a member, which becomes a proxy and must not
   flicker away mid-chase, then eat it. The member must suction into the predator's mouth (the predation
   death, not a wither), and the predator's starvation clock must reset.
3. Confirm that a just-born member (blooming in) is not eaten in its first moments, i.e. predation immunity
   holds.
4. Confirm a predator does not chase members outside its own band.

**PASS:** predators chase and eat members through the predation path; immunity and band hold; no pop.
**FAIL:** any compile error · predators ignore swarms · a member is eaten while still blooming in · a chased
member blinks out and reappears · a predated member withers in place instead of suctioning into the mouth.

### QA-SWARM-ROUND8-3 ⬜ — member bodies move the Swarm cell's volume ladder

**Source:** branch `cece/swarm-fauna-game` (headless only; **never run in the editor**). Full reference:
`Docs/SWARM_FAUNA.md` §16.3. **Why it matters:** "volume is the spine". The three swarms' bodies (~62,600
volume grown full) now count toward `Cell.LiveVolume` per domain, never twice for a member that has a
proxy. The cell's ladder was re-derived for it: Restless 55,000 / 41,000, Frenzy 390,000 / 343,000
(volume; counts unchanged).

1. Select `Swarm Cell Config.asset`. **Restless Enter/Exit Volume 63000/47000, Frenzy Enter/Exit Volume
   448000/394000** (round 11c added the substrate agents' bodies, ~23,300 volume at full pools, to the ladder:
   `Docs/SUBSTRATE_FAUNA.md` §7; round 8 alone was 55000/41000 and 390000/343000). If not, reimport.
2. Enter the Swarm cell with a debug readout of `Cell.LiveVolume` (or the cell phase HUD). With the swarms
   seeded, live volume must exceed the forest alone. Let them grow for 5 minutes: it should climb as they
   grow.
3. Fly a vessel into a swarm so dozens of proxies form, then leave. Live volume must NOT jump when proxies
   appear or retire. A step there is a double count or a gap.
4. Kill ~100 members. Live volume must fall by roughly their bodies, with no spike.
5. Record which phase the cell sits in once the swarms are grown (expected Calm or Restless; Frenzy only
   with heavy trail).

**PASS:** the config values are present; bodies raise live volume; proxies forming or retiring cause no
jump; kills lower it. **FAIL:** volume jumps when you approach or leave a swarm · the cell pins at Frenzy
with no trail · live volume never moves as the swarms grow.

### QA-SWARM-ROUND8-4 ⛔ — superseded by QA-SWARM-ROUND9-2

Round 8's diet colouring (newborns wearing the colour of what they ate) was removed on playtest. See
`Docs/SWARM_FAUNA.md` §17.

### QA-SWARM-ROUND11-4 ⬜ — the sort swarm heals without a jolt; lurkers never bite a rush

**Source:** branch `overnight/feel` (headless only: harness R11d-J/T/B, the research's yardstick and smoothness).
Reference: `Docs/SWARM_FAUNA.md` §22. **Why it matters:** right after a strike the body used to flood with hatchlings
that raced across it (a jolt Garrett would read as a bug), and a rushed lurker plated up after it had bolted.

1. Select `SwarmSortFaunaConfig.asset`: **Sort Bud At Wound on**, **Sort Fate Near on**, **Sort Lay Ramp Seconds 12**.
2. Enter the Swarm cell and carve a third out of one sort swarm in one pass (a blast or a fast fly-through).
   Watch the next ~15 s: the hole should fill from its own edge, gradually; nothing should streak across the body.
3. Set **Sort Update Fraction 4** and repeat step 2 (the dial QA-SWARM-ROUND6 offers); then set it back to 8.
4. Mass lurkers: rush one at full speed. It must bolt with no danger plates, then or as it settles. Then creep up on
   another slowly: plates rise once and stay up while you hover, with no flicker at the edge of its notice.
5. Time pack hunters: hover at the distance where they first turn on you. Plates must not flicker.
6. Profiler: the swarm tick marker for a sort swarm should be within ~5% of round 10 (the wound bud scans the body
   per egg, at most LayMax eggs a step).

**PASS:** heals ease in from the wound; no flood of newborns the moment the 2 s kill hold lifts; a rushed lurker never
plates; no plate flicker. **FAIL:** members streak across the body after a strike · a swarm under steady grazing stops
regrowing · a lurker plates after bolting · plates flicker on and off at one distance.

### QA-SWARM-ROUND11-13 ⬜ — the demo cell starts hostile, and claiming the nucleus takes it

**Source:** branch `overnight/hostile`. Only the headless resolver harness (`Tools/Build/cell_control_harness/run.sh`),
the reference compile and the authoring gates have run; it has never been opened in the editor. Reference:
`Docs/SWARM_CELL_PLAY_GUIDE.md` §2, `CellConfigDataSO.initialControllingDomain`, `CellControlRules`.

**Why it matters:** every creature wears the cell's controlling domain. With the nucleus empty, the old fallback
handed control to the local pilot, so solo freestyle seeded the Swarm cell in **your** colour: every hit was a
friendly sting and the demo had no stakes. The Swarm cell now authors `OpposingLocalPilot`: until somebody holds
the nucleus, it is controlled by the next domain after yours (Jade → Ruby → Gold → Jade).

1. **Hostile start.** Collect crystals to 5+ petals per element. Then enter the Swarm cell through the Cell Selector
   as Jade, and wait for the seed (about 6 s).
   - Creatures should wear **Ruby**.
   - A landed contact should burn 1 petal per element for good (QA-SWARM-ROUND11-7), not dip and recover.
2. **Trail outside the nucleus does not flip it.** Lay trail freely outside the nucleus (r 392) for a minute. The next
   waves must still hatch Ruby.
3. **Claiming the nucleus takes the cell.** Lay trail inside the nucleus until you lead it. The next wave should
   hatch in **Jade** (yours, friendly). Live creatures keep the colour they hatched in.
4. **Other domains.** Repeat step 1 as Ruby (expect Gold) and as Gold (expect Jade).
5. **Domain Changer.** Inside the unclaimed cell, flip your domain with the Domain Changer. The next wave should
   again be a colour that is not yours: the start is not latched.
6. **Every other cell is unchanged.** Pick another Cell Selector station (e.g. Yggdra) and confirm the old behaviour:
   its fauna seed in your colour.
7. **Multiplayer (host + one client), if a networked cell is available.** Use a cell that authors a start.
   - Both peers must see the same hatch colour: the next domain after the **host's**.
   - Note it if that colour is the client's own. That is the stated trade-off: the server decides.

### QA-SWARM-ROUND11-12 ⬜ — the cell's emotional range, and a puffed shield member stays shielded

**Source:** branch `overnight/feel2`. Only headless harness exports and the research probe have run. Reference:
`Docs/SWARM_FAUNA.md` §27.

**Why it matters:** the probe reads motion and size only. A human has to confirm that the cell feels like its reads,
from cute to terrifying.

1. **Cute.** Fly slowly at a sparse substrate locust swarm. It should feel cute or harmless.
2. **Terrifying.** Fly at a dense, hungry locust storm. It should feel terrifying.
3. **The pack (round 11-12 ring hold, `overnight/packhold`; `Docs/SUBSTRATE_FAUNA.md` §7.7).** Approach the
   substrate pack directly and slowly.
   - Once the six hunters surround you, the ring should circle slowly and tighten for about 6 s. No one should strike
     during that time, and nothing should freeze.
   - Then every hunter should dive in at once.
   - Repeat, but sprint out through the gap halfway through the hold. Nobody should strike on the way out, and the
     ring should re-form around you.
   - Note whether the held ring reads as menace (the probe: eerie at cruise, mostly terror at hover).
4. **Swarm bodies.** Fly through a whale and a dragonfly body. Expect awe at range and dread inside them.
5. **Shield.** Hound a pufferfish (Charge) shield member until it puffs.
   - It must still deflect shots.
   - Grazers and hunters must not eat it or steer at it while it is puffed.

### QA-SWARM-ROUND11-6 ⬜ — a far swarm collapses and comes back without a pop; creatures starve on a stomach

**Source:** branch `overnight/lod`; steps 9-11 come from `overnight/lod2` (round 11f-2). Only headless gates,
type-checks and authoring gates have run; it has never been opened in the editor. Reference: `Docs/ECOLOGY_LOD.md`.

**Why it matters:** the swarm now stops simulating when no one is near.
- If the collapse or expansion is wrong, a swarm teleports, pops, loses members, or changes the cell's phase.
- If the stomach migration is wrong, every Boid, LightFauna and Worm starves early or never.

1. **Set up.** Open the Swarm cell in freestyle. Turn on the **Ecology** log channel. Open the Profiler and find:
   - `CellEcologyLod.Guard`
   - `CellEcologyLod.MacroTick`
   - `SwarmFauna.MacroTick`
2. **Collapse.** Fly about 600 u away from a swarm and look away from it for 5 s.
   - `SwarmFauna.Tick.*` markers for that swarm should stop.
   - `SwarmFauna.MacroTick` should appear once per second.
   - The cell's phase readout and live volume must not move when the swarm collapses.
3. **Distant view.** From about 400-500 u, turn the camera toward a collapsed swarm.
   - It should already be drawn. It may move in small once-per-second steps at that distance.
   - It must never vanish, and never re-form from a knot.
4. **Approach.** Fly straight at a collapsed swarm at boost speed. Before you are within about 300 u it must already
   be swimming and morphing again: smooth motion, no jump, no member popping in or out.
5. **Hit it from range.** Fire at a collapsed swarm from as far as your weapon reaches. The hit member must react like
   any member (proxy, death, one crystal), and the swarm must resume swimming.
6. **Long absence.** Leave a swarm for 3+ minutes, then come back. Check that:
   - its member count (`MemberCount` in the inspector) is the same or grew;
   - if it was grazing, it has laid eggs on expansion;
   - it is roughly where its goal led it, not where you left it.
7. **Stomachs** (Boid, LightFauna, Worm cells). Play 5 minutes as before.
   - Starvation should look the same as before: creatures that find food survive, and creatures in a stripped area
     thin out after about their `starvationSeconds`.
   - A creature eaten by a predator keeps the predator alive for the same time as before.
8. **Profile** 60 s with all swarms far. Record `CellEcologyLod.*` and `SwarmFauna.MacroTick`: expect well under
   0.1 ms per frame together.
9. **Substrate bands** (round 11f-2, `overnight/lod2`). In a cell with locusts, pack hunters and lurkers, fly
   600 u away and look away for 10 s.
   - `SubstrateCellHost.*` cost should drop to almost nothing once every band is frozen.
   - Come back: the bands are where you left them and swim on with no jump.
   - Fire at a far band: it wakes and the hit member reacts normally.
   - Watch a pack near a frozen locust band: the hunt wakes the locusts.
10. **Builder colonies** (fortress, thief nest, wearers). Leave each for 2+ minutes, then return.
    - Within a few seconds of leaving, the fortress's workers stop fetching and the carriers finish their deposits;
      then `BuilderColonyFauna.Tick` stops and `BuilderColonyFauna.Roost` appears once per second.
    - On return: the structure is unchanged, member counts are unchanged, nothing pops, and every carried or worn
      prism is exactly where it was.
    - A wearer that was rearing when you left should not stay frozen mid-rear.
    - Shoot a roosting colony's member from range: it wakes and resolves the hit normally.
11. **Threat grove far cadence.** Fly 800+ u from the grove for 30 s. `ThreatGrove.Physarum` should cost about a
    quarter of what it did up close. Fly back: the network is continuous, no tube pops, beats resume at full rate.

**PASS:**
- A far swarm stops its tick and costs only the macro tick.
- LiveVolume and the phase readout are unchanged across collapse and expansion.
- No pop, jump or knot on approach or on a long-range hit.
- Counts are conserved over a long absence.
- Substrate bands, builder colonies and wearers come back exactly as left (count, structure, carried and worn
  prisms), and the grove's network is continuous across the far cadence.
- Starvation timing in the classic fauna cells is unchanged on leaf-sized meals.

**FAIL:**
- A swarm teleports or re-forms on approach.
- Members vanish or appear when it collapses.
- The phase readout steps when a swarm collapses or expands.
- A collapsed swarm cannot be hit.
- A swarm keeps its 10 Hz tick while far and unseen.
- Boids, lights or worms starve noticeably faster or never starve.
- Any `[EcologyLod]` warning about a full sense buffer.
- A substrate band, builder colony or wearer jumps, loses or gains members, or moves a prism while far.
- A builder member starves while its colony roosts, instead of the colony waking first.
- The grove's network pops tubes when you return.

### QA-SWARM-ROUND11-5 ⬜ — creatures that steal and build: the fortress colony and the thief nest

**Source:** branch `overnight/builders` (headless harness, type-check and authoring gates only, not run in the
editor). Reference: `Docs/BUILDERS_AND_THIEVES.md`. **Why it matters:** these are the first creatures that move
other prisms. If the mover contract, the registry or the settle flight is wrong, prisms pop, double-count, or are
stolen from shields.

1. Open the Swarm cell in freestyle. In the Console (with the **Ecology** log channel on), confirm two
   `[Builders] ... founded` lines: a Fortress colony near r 845-905 and a Thieves nest near r 1085-1140, both in
   the cell's controlling domain. Each line should say `bodies prism entities`. Member bodies should look like
   ordinary prisms of their tier in that domain, with crystal hearts in front.
2. **Fortress build-up.** Fly a long trail loop through the 800-950 u shell, then hold off for 2-3 minutes.
   Workers (small prism-bodied creatures with hearts) should carry your trail prisms to a hollow shell about 40 u
   across. Check the details:
   - each placed prism eases into place (no teleport, no pop);
   - shielded prisms in the area are never taken.
3. **Wound knit.** Ram straight through the wall, then back off about 150 u and watch with a stopwatch. Most of the
   hole (about half the sites) should refill in about 5-10 s, mostly with your own trail prisms re-coloured to the
   colony's domain. Cut the same line two more times: the wall there should grow visibly thicker.
4. **Defence.** Hover inside about 110 u of the colony. A screen of workers should gather between you and the core
   (the telegraph), then some should turn into danger plates. As an opposing domain, touching a striker burns
   petals that do not come back as crystals.
5. **Worker deaths.** Ram a worker carrying a prism. The worker drops ONE crystal, and the prism falls loose where
   it was (it does not vanish).
6. **Thieves.** Fly at cruise within about 700 u of the nest's plant. A few thieves should fall in behind you and
   snatch prisms from the last moment of your trail. Watch them fly home slowly to a cluster at the plant (the
   hoard).
7. **Turn back.** Turn on a laden thief: you should always catch it. Knock it down, and its prism should change
   back to YOUR domain and stay where it fell. The thief drops one crystal.
8. **Raid.** Fly through the hoard: the prisms you touch become yours.
9. **Opening.** Start a fresh session and stay away from the nest for 5 minutes. When you come back, the nest should
   still be alive with at least its 6 founders (no opening die-off).
10. **Profile** 60 s near both colonies. Record `BuilderColonyFauna.Tick` (expect well under 0.2 ms per tick) and
    `.Frame`, and the physics collider count (proxies at most 24 + 18, two colliders each).

**PASS:**
- The colonies found in the right bands and colour.
- The wall rebuilds a cut in seconds from the cutter's trail.
- No pops: placements ease in, and every death leaves exactly one crystal.
- Shields are never stolen.
- Thieves tail, snatch fresh trail, fly slowly when laden, and return the prism on knock-down.
- The hoard can be raided.
- No opening die-off.

**FAIL:**
- A prism vanishes when picked up, carried or placed.
- A prism snaps into place, or a placed prism is invisible or culled.
- A shielded prism is carried.
- A cut never refills, or refills only after a minute or more.
- A worker or thief death drops no crystal, or more than one.
- A laden thief outruns a turning ship.
- The knocked-down prism keeps the thief's colour.
- The nest dies out with no ship around.
- A frame-time regression above about 0.5 ms.
### QA-SWARM-ROUND11-7 ⬜ — the petal-burn switch: the demo cell burns 1 petal per element, everywhere else 5

**Source:** branch `overnight/burn` (round 11g). It is proven by the elemental transfer harness (T8 and the
switch type-check) and by `check_elemental_economy.py` §5. Not run in the editor. Reference:
`Docs/ELEMENTAL_ECONOMY.md` §4.1.

**Why it matters:** the shipped burn strips a careless pilot in 5-37 s. The lab's tuned rule costs a skilled pilot
0.22 petals/min. Garrett has not chosen yet. The demo cell plays Tuned so the two can be felt side by side.

**Step 0: give yourself petals to lose.** Collect crystals until every element on your HUD flower holds at least 5
petals (ELEMENTAL_ECONOMY.md §8 step 0).

1. **Inspect.** Select `Assets/_SO_Assets/Effects/Vessel Prism Effects/VesselElementalDebuffByDangerPrismEffect.asset`.
   It should show Debuff Magnitude **-0.5** and Tuned Debuff Magnitude **-0.1**, and Stakes Switch ▸ Cell Data =
   **Runtime Cell Data**. Then select `Assets/_SO_Assets/Cell Configs/Swarm Cell/Swarm Cell Config.asset`: Stakes ▸
   Petal Burn Rule = **Tuned**. Any other Cell Config should read **Shipped**.
2. **Tuned, hostile.** Open `Menu_Main`, pick the **Swarm** cell and start freestyle. Fly as a domain that does NOT
   control the cell. Fly into one striking creature: a lit pufferfish plate, a pack hunter mid-dive, or a lurker's
   snap. Each of the four flowers should step down **exactly one** petal and stay down. Wait 1 s, the per-vessel
   cooldown, and touch again: one more petal each.
3. **Tuned, own domain.** Repeat as the cell's controlling domain. Each flower dips by one petal and recovers over
   ~4 s. Nothing is lost.
4. **Shipped, for contrast.** Pick any other cell with danger prisms (an opposing pilot's danger trail works in any
   mode), then repeat step 2. Each flower should drop **five** petals per contact, so a full element is gone in two.
5. **Last petal.** In the Swarm cell, take repeated hostile contacts until an element shows 1 petal. The next
   contact must take it to **0**. Further contacts take nothing and never go negative.
6. **Flip it.** Set the Swarm Cell Config's Petal Burn Rule to **Shipped**. For the permanent version, set
   `PETAL_BURN_RULE = 0` in `Tools/Build/author_swarm_fauna.py`, run it, and update `TUNED_CELLS` in
   `check_elemental_economy.py`. Replay step 2: five petals per contact. Then set it back to Tuned.

**PASS:**
- The Swarm cell burns 1 petal per element per hostile contact.
- Other cells burn 5.
- The own-domain sting matches the burn's size and recovers.
- The last petal can be burned.
- Flipping the field changes the size with no code change.

**FAIL:**
- The Swarm cell burns 5. Likely cause: Cell Data is unwired on the effect asset, or the cell config reads Shipped.
- Another cell burns 1.
- A contact burns a fraction or an uneven count across the four elements.
- An element sticks at 1 petal under repeated hostile contact.
- The own-domain dip is a different size from the burn.

### QA-SWARM-ROUND11-2 ⬜ — the Living Ecology substrate: a pack that surrounds you and dives in at once

**Source:** branch `overnight/substrate`. Proven by the headless harness and Roslyn type-check only; not run in the
editor. Reference: `Docs/SUBSTRATE_FAUNA.md`.

**Why it matters:** this is the first fauna whose threat is coordinated. Six hunters ring the pilot and strike
together. If it does not read in the editor, the round's design target is missed: *"being surrounded by them and
having them all dive in at once will be scary once the stakes are felt."*

1. **Load.** Open `Menu_Main`, pick the **Swarm** cell in the Cell Selector, and start freestyle. In the Console, turn
   on the **Ecology** channel (FrogletTools > Toolbox > Logging). Within ~10 s you should see three
   `[Substrate] … population … seeded` lines: pack (6 agents), locust (40) and lurker (8). Each line says its bodies
   are `prism entities` or `instanced`. You should see NO `[Substrate] GPU agent drawing is OFF` warning.
2. **Look.**
   - **Locusts** (outer shell, 910-1080 u): small Space-heart creatures bobbing near the Space flora.
   - **Pack** (middle shell, 690-1080 u): six long, low Time-heart bodies.
   - **Lurkers** (inner shell): they should be hard to tell from the Mass flora crystals. Look for a crystal whose
     sliver of body grows when it moves.
3. **Pack, careless.** Fly as a domain that does NOT control the cell, with petals on your HUD flower. Cruise in a
   straight line through the middle shell near the pack. Within ~10-20 s you should see:
   - the hunters spread onto a ring around your line (about 110 u out) and HOLD it, stalking, for ~10 s or more
     without striking (round 11c: the menace before the terror);
   - the ring close: a ~0.4-0.9 s beat where they all turn toward you;
   - **all of them** light up as danger prisms within ~1 s of each other and dive.
   Being hit burns petals. Afterwards they fall back, slower and unlit, for ~3 s (winded). Hitting them now is safe.
4. **Pack, counterplay.** Repeat, but when the ring forms, turn hard toward the widest gap between hunters. You should
   escape most strikes (headless: 1 bite vs 20 for the straight flyer).
5. **Pack, own domain.** Repeat once as the cell's controlling domain. Contact only stings; no petal is burned.
6. **Locusts.** Fly slowly past a sparse cloud: they stay unlit and drift curiously. Then hover in the outer shell
   until the cloud has grown dense, or herd it with your vessel. A dense, hungry cloud should tighten, align and turn
   dangerous as a storm.
7. **Lurkers.** Point your nose AWAY from a lurker-crystal ~300 u off for 3 s, then look back. It should have crept
   toward your line. While you look straight at it, it must not move. Fly past within ~60 u: it gapes (swells), then
   snaps (lit, ~0.5 s later), then hangs slack for ~3 s.
8. **Kills and crystals.** Kill one of each species with a gun, a ram and an AOE weapon. To test the AOE, fire it at a
   locust cloud more than 160 u away, which tests the virtual entries. Each death drops exactly one crystal. A
   shot-dead agent leaves its body as a skeleton prism; nothing pops in or out.
9. **Food web.** Watch the pack for 2-3 min with no vessel near it. It should drift into the locust shell and catch
   locusts. Each catch is a locust crystal drop plus the body suctioned into the hunter, and the hunter's body grows.
10. **Mass.** On the cell's phase/aggression readout, live volume should rise as locusts breed and fall as they die.
    It must not jump when you fly close (proxies forming) or leave (proxies retiring).
11. **Cost.** Profile 60 s with all three populations alive. Record `SubstrateCellHost.Collect`, `.Publish`, `.Sense`
    and `SubstrateFauna.Tick.*` / `.Frame.*`. The worker tick itself is not sampled by the Profiler (pool thread).

**PASS:**
- All three populations seed.
- The pack rings, closes and strikes together, then is winded.
- An opposing-domain hit burns petals; an own-domain hit does not.
- The gap counterplay works.
- Locusts flip under density + hunger.
- Lurkers creep only when unwatched.
- Every death drops one crystal.
- Far agents are hittable by AOE.
- No volume jumps.
- Main-thread substrate cost is under ~0.5 ms per frame.

**FAIL:**
- A population never appears, or appears with no bodies or no hearts.
- The pack strikes one at a time, or never strikes.
- Strike contact burns no petals.
- A lurker moves while watched.
- A death with no crystal, or an agent that pops out without dying.
- The ladder jumps near the pack.
- A worker error: `[Substrate] the cell's substrate tick threw`.

### QA-SWARM-ROUND11-9 ⬜ — the whole Swarm cell at once: whales at full size, lean frames, wearers steal at the rim

**Source:** branch `overnight/cell`. The showcase-cell harness (`Tools/Build/showcase_cell_harness/run.sh all`) runs
every creature core together in the authored layout with three scripted pilots. Results:
- 1,192/1,200 collider worst case;
- 0.84-0.97 ms per frame combined;
- the mass ledger closes to 4e-7;
- the LOD is never seen collapsed.

It fixed six cross-system bugs in the cores. **Never run in the editor.** Reference: `Docs/SWARM_FAUNA.md` §25.

**Why it matters:** two of the fixes change what a player sees.
- The swarm's plan library now upsamples to `PlanDensity`, so whales hold up to 960 members, not 192.
- Wearers now steal the trail pilots lay along the membrane.

A third fix (a subnormal flush in the substrate fields) removes a frame-time cliff that appears after minutes of
hovering.

1. **Whale size.** Open the Swarm cell and find the biggest swarm body. In the inspector or debug overlay, its member
   count should climb past 192. A full whale reads clearly denser than before.
2. **Hover cost.** Hover near a substrate population for 5+ minutes and profile. The substrate tick's fields stage
   should stay flat, about 2 ms per tick off-thread, and must not creep up over time.
3. **Wearers at the rim.** Fly through the inner gap (400-465 u) so the wearers find your trail, then fly loops near
   the membrane (r ~ 1,150). Within a minute or two, white hearts should be pulling YOUR rim trail into bodies. They must not hover motionless
   at the wall.
4. **Substrate engagement.** Near the locust cloud, many locusts should engage you at once, up to the authored
   per-population cap, not just 2.
5. **A long session.** Fly 30 minutes with friends or bots. Note which creature classes die out and when; the harness
   predicts thieves by minute 1 and packs and locusts by minutes 2-4. Note whether the game's spawner re-seeds them.

**PASS:**
- Steps 1-4 read as described.
- No frame-time growth over the session.
- No crystal missing at a death.

**FAIL:**
- Whale capped at 192.
- Substrate fields stage creeping past 10 ms.
- Wearer hearts stuck at the membrane holding still.
- Only 2 locusts ever engage.
- A creature that pops out without dying.

**Report also:** the burn rate per pilot style. The harness found that the skilled pilot burns MORE than the careless
one (3.3 vs 1.8/min), mostly on the charge swarm's danger plates. Is that true for a human dodging?

### QA-SWARM-ROUND11-11 ⬜ — the rest of the bestiary: stampede, mobber, leech, leviathan

**Source:** branch `overnight/more-species`. Proven headless (substrate harness S, T, C, V, Q, M2):
- the bulls trample a wanderer 10.9/min (research range 5.3-10.9) after a 1.9 s head-down;
- the mobbers peck a hovering pilot 44.7/min, in turn (≤ 3 diving), and leave a 140 u/s flyer alone;
- the leeches latch, sip 15.8/min and are shaken off by a hard turn;
- the leviathan assembles, holds its manta and gulps a pilot ahead of its mouth;
- the 39 substrate proxies are re-divided with each cap still engaging, and the ledger closes with all seven species.

**Never run in the editor.** Reference: `Docs/SUBSTRATE_FAUNA.md` §9.

**Why it matters:** this is new glue that has only been type-checked:
- the danger weight on `PrismProperties`;
- the danger effect's collision-free `ApplyContact` (a sip);
- sector pens and cluster seeding;
- per-species proxy caps;
- riders retired off their proxies.

1. **Find them.** Open the Swarm cell.
   - **Mobbers:** roosts in the 625-685 u gap.
   - **Stampede:** herds in the middle shell, 690-840 u, on the +X side.
   - **Leeches:** puddles at the flora, 690-840 u, 120° round from the herds.
   - **Leviathan:** a school, 690-840 u, 120° the other way.
2. **Stampede.** Fly past a herd at ~120 u/s. It should spook as one. The cows run away, and every 4th animal (a bull)
   lowers its head (slows, faces you) for about a second, then charges. A charge that lands burns like a bite. Dodge
   sideways during the head-down and it should miss.
3. **Mobber.** Hover by a roost, or fly slower than ~100 u/s near one. The birds should swirl round the hull, and
   one at a time pulls up and dives. Each peck is a SMALL burn: about a quarter of a bite, at most one per second.
   Speed up past ~140 u/s: they should fall behind. Point your nose at one close in: it should jink aside.
4. **Leech.** Fly within ~140 u of a puddle.
   - Leeches should leap onto the hull and ride it: visible on the ship, at most six.
   - Every 1.5 s, each one drains a quarter-bite (same 1 s cooldown).
   - Fly straight and they stay. Turn hard and they are flung off.
   - Ram a free one: it should die and drop one crystal.
5. **Leviathan.** Watch the school. Fed, it should pull into one 120 u manta shape and cruise. Touching the body
   burns. Sit ~150 u in front of its mouth: the jaws flare for about a second, then it surges at you. Hungry later, it
   should fall apart into a harmless shoal.
6. **Budget.** With all four engaged at once, record:
   - the live collider count, which must stay ≤ 1,200;
   - `SubstrateFauna` proxy counts: stampede ≤ 6, mobber ≤ 4, leech ≤ 2, leviathan ≤ 4, locust ≤ 12, lurker ≤ 4.

**PASS:**
- Steps 2-5 read as described.
- No rider keeps a proxy, so nothing collides inside your own hull.
- Pecks and sips burn less than bites (`PrismProperties.DangerWeight` 0.25).
- No `MissingReferenceException` when a vessel carrying riders is destroyed or the cell unloads.
- The demo cell's ladder still steps (its volume thresholds rose with the new bodies: RestlessEnterVolume 69,000).

**FAIL:**
- Leeches seeded outside their sector, or herds overlapping the leviathan's school.
- A peck or a sip that burns a full bite.
- A sip with no `contactEffect` (one warning naming the asset).
- A collider count over 1,200.
- The manta drawn as a loose cloud while its members burn.

### QA-SWARM-ROUND11-10 ⬜ — the wearer: a creature made of your stolen trail

**Source:** branch `overnight/wearers`. Proven headless (builders harness W1-W7: bodies form from stolen trail, fuse,
rear 1.00 s and lunge at 2.40 hits/min; moults return mass; audit 0). **Never run in the editor.** Reference:
`Docs/BUILDERS_AND_THIEVES.md` §10.

**Why it matters:** worn prisms are re-parented under one container per creature. Their render matrices and index
points move in one batched pass a frame, and their colliders ride the hierarchy. None of that has run in Unity.

1. **Find it.** Open the Swarm cell. Fly a long loop through the inner gap (400-465 u). Within a minute or two,
   small white hearts should be pulling prisms out of YOUR trail into lumpy bodies behind you.
2. **Growth.** Keep flying. Bodies should grow from where they fed (a head with a tail, not a ball) and merge when they
   touch.
3. **The hunt.** Once a body is big (~60 prisms), it turns. Watch for the rear (the body contracts for one second),
   then the lunge with its prisms in the danger look. Dodge sideways during the rear and it should miss.
4. **Strip it.** Fly through a body. Your prisms should come back in your colour and fall loose. Strip a lot quickly:
   it should moult, dropping a shell of loose prisms that go back to their owners, and flee.
5. **Kill a heart.** Ram a bare or small heart. It should die and drop one crystal.
6. **Lair.** Let a creature reach 150 prisms while it is not hunting. It should drop a static clump (the lair) and a
   new heart should appear there.
7. **Cost.** Profile 60 s with a 150-prism creature following you. Record:
   - `BuilderColonyFauna.Frame`;
   - `Physics.SyncColliderTransform` / `Physics.Simulate` with and without the creature;
   - `PrismRenderService` batch time.

**PASS:**
- Steps 1-6 read as described.
- No prism is left floating where a body was when the creature dies.
- No `MissingReferenceException` when exiting Play with a creature alive.
- The Frame marker is under ~0.3 ms at 300 worn prisms.

**FAIL:**
- Worn prisms drawn at their old place (the render matrix is not following).
- Weapons or AOE missing a body (the index is not following).
- Prisms destroyed when the cell unloads mid-hunt.
- The danger look stuck on after a lunge.
- Physics sync above ~1 ms a frame. If so, try the kinematic-Rigidbody container in §10.4.

### QA-SWARM-ROUND11-8 ⬜ — the substrate's agent pass is a Burst job

**Source:** branch `overnight/substrate2`. Proven headless: the kernel is bit-identical to the managed step it
replaced, it passes a textual Burst gate, and the split tick publishes exactly the plain tick. **Burst itself has never
compiled it.** Reference: `Docs/SUBSTRATE_FAUNA.md` §7.

**Why it matters:** the agent step now runs as `SubstrateAgentJob` (`[BurstCompile] IJobParallelFor`) scheduled by
`SubstrateCellHost` between the worker's two halves of a tick. If Burst rejects it, the job runs as slow managed
code, or the safety system throws.

1. **Compile.** Open `Jobs > Burst > Open Inspector`, find `SubstrateAgentJob`, and compile it. There should be no
   Burst error and no "managed code" fallback warning in the Console on entering Play.
2. **Safety.** With `Jobs > Burst > Safety Checks` ON and the Jobs Debugger on, play QA-SWARM-ROUND11-2 steps 1-3.
   There should be no `InvalidOperationException` about the parallel-for range, a missing dependency, or
   `[ReadOnly]` writes.
3. **Behaviour.** QA-SWARM-ROUND11-2 steps 3, 6 and 7 read the same as before: the pack rings and holds, then strikes
   together; locusts flip; lurkers creep only unwatched.
4. **Lifetime.** Exit Play mode. The Console must not show "A Native Collection has not been disposed".
5. **Cost.** Profile 60 s. Record `SubstrateCellHost.AgentPass` (main-thread copies and scheduling) and the
   `SubstrateAgentJob` worker bars in the Timeline view.

**PASS:**
- Burst compiles the job.
- No safety exceptions.
- No leak on exit.
- Behaviour unchanged.
- `SubstrateCellHost.AgentPass` is under ~0.2 ms per frame at the Swarm cell's population.

**FAIL:**
- A Burst compile error, or the job running managed.
- Any safety exception.
- A leak warning.
- Agents frozen in place: the worker stayed parked.
- `[Substrate] the cell's substrate tick threw`.

### QA-SWARM-ROUND11-1 ⬜ — one prism system: members are index entries and prism entities

**Source:** branches `overnight/prism` and `overnight/prism2` (round 11a-2: Burst pose and index jobs) - headless
gates and type-check only, not run in the editor. Reference:
`Docs/SWARM_FAUNA.md` §19. **Why it matters:** members are now found by the platform's own prism queries and drawn
as ordinary prism entities. If either half is wrong, swarms become unhittable or invisible, or they double-count
in the phase ladder.

1. Open the Swarm cell in freestyle. Confirm `SwarmFaunaConfigSO` **Unified Prism Bodies** is on (default). Play
   and watch the Console: there should be NO `[Swarm] ... fall back to the instanced draw` warning.
2. From a distance, check the members:
   - **Looks.** Every member body looks like a prism of its tier in its domain: plain, a danger plate (round-10
     strike), or shielded.
   - **Spread.** The body opens with distance like any prism (§15).
   - **Hearts.** Hearts still sit at the front and re-form while molting.
   - **Newborns.** Newborns grow from a point at the heart.
3. With **Multi Domain** on (`SwarmSortFaunaConfig.asset`), the back and belly regions show their own domain's
   prism material.
4. Fire at members **more than 160 u away**, once with each:
   - a rocket or other AOE: members inside the blast die, and kills roll over a few frames for a dense hit;
   - a projectile vessel: the shot stops at, or pierces, members as it does prisms;
   - the sniper: own-domain members are passed through.
   Each dead member drops one crystal.
5. Fire a Sparrow missile into a far swarm. Members whose hearts are in the blast should be jousted (lifeform-crystal
   effects).
6. Leave a predator (`LightFauna`) and a worm in the cell. They should hunt and eat members from range.
7. Open the cell's phase/aggression readout. It should be the same as on the round-10 build with the same seed: no
   jump when proxies appear near your vessel.
8. Profile 60 s in the Swarm cell with all three swarms grown, with Burst compilation ON (Jobs > Burst > Enable
   Compilation). Record the following against §19.4's round 11a-2 estimates (main thread ~0.03-0.07 ms per frame,
   ~0.1 ms per tick):
   - `SwarmFauna.Frame.Bodies` (the pose job schedule);
   - `SwarmFauna.Frame.BodiesWrite` (the wait and the transform write);
   - `SwarmFauna.Tick.Index` and `SwarmFauna.Tick.Entities`.
   In the Timeline view, check that `SwarmPoseJob` runs on the worker threads, and that `ResolveHandlesJob` and
   `UpdatePositionsJob` are marked Burst.
9. Turn **Unified Prism Bodies** off and re-enter. The round-7 instanced draw returns, and weapons and predators
   still work.

**PASS:**
- Members look like platform prisms.
- Members are hittable at range by all three weapon types.
- Predators eat them.
- The ladder is unchanged.
- No fallback warning.
- Costs are within ~2x of §19.4.
- The Burst jobs compile with no Burst errors in the Console.

**FAIL:**
- Invisible members, or members drawn at the wrong place or size (pose mismatch with hearts).
- Members flicker at death or respawn, or a dead member stays visible.
- A far member cannot be hit.
- The ladder jumps when proxies appear (double-count).
- An own-domain sniper kill.
- A frame-time regression above ~1 ms per frame.

### QA-SWARM-ROUND11-3 ⬜ — threat flora: bait a snap trap, read a physarum pulse

**Source:** branch `overnight/flora` (headless harness and glue type-check only, not run in the editor). Reference:
`Docs/THREAT_FLORA.md` and `Docs/SWARM_FAUNA.md` §21. **Why it matters:** these are the first plants that can
cost you petals. If the telegraph is unreadable the danger feels unfair; if it never fires the grove is scenery.

1. **Find the grove.** Open the Swarm cell in freestyle and fly as a domain that does NOT control the cell, with a
   few petals on your HUD flower. From the cell centre, head along (1, 0.3, 0): +X, tilted slightly up. Fly out past
   the outer swarm band (ends ~1,080 u) toward the membrane (1,200 u). The grove sits at radius 1,095-1,192 u in an
   18° cone: three clumps of snap traps (stalk, two plate lobes, teeth, a crystal in the jaws) and five sclerotia
   (crystals in a six-prism shell) joined by cables of tube prisms.
2. **Bait a snap trap.** Approach one trap's mouth (it faces the cell centre). Within ~146 u its lobes should glow
   in its domain's danger colour and gape wider for ~0.7 s (PRIMING), then hold open (ARMED).
   - Back off before it arms: the glow goes out and it never fires.
   - Fly a path straight across the open mouth: the lobes snap shut in ~0.5 s. Be between them and you are hit;
     the closing lobes and the teeth burn petals. Dodge sideways the moment you see the glow.
   - Touch a tooth on a resting trap: it is always a danger prism.
   - Shoot or ram the lobes and teeth until 13 or more of the 24 are gone: the trap glows no more and cannot fire.
   - Joust the crystal in the jaws while the trap is shut (~7-9 s): the trap dies and leaves a skeleton.
   - Fly loops past a clump for ~1 min: the mouths turn toward your path (slowly, 7°/s, at most 60°).
3. **Read a physarum pulse.** Hover ~100 u from a long cable and watch it:
   - A band of danger-coloured tubes runs along the cable at ~65 u/s (about 1.2 s per 80 u). Each tube is lit for
     ~0.5 s, then dark and refractory for ~1 s; cross a cable just behind a passing pulse.
   - A sclerotium's shell glows for 0.8 s, then beats danger for 0.6 s, about every 3 s. Dive for its crystal
     right after a beat ends.
   - Cut a cable by ramming or shooting through it. Within ~1 minute the cables re-find the gap. Leaving a trail
     near the grove should NOT attract the cables (wake repels).
4. Repeat one trap hit and one cable hit as the cell's controlling domain: it should only sting, with no petal
   loss.
   As an opposing domain, a hit should take ONE petal per element (the Swarm cell plays the Tuned burn,
   QA-SWARM-ROUND11-7), not five.
5. Profile 60 s near the grove and record `ThreatGrove.SnapTraps` and `ThreatGrove.Physarum` (headless estimate:
   well under 0.1 ms for the traps, ~1.4 ms per physarum step at 10 Hz under Mono). Note any hitch in the first
   ~1.5 s while the network warms up.

**PASS:**
- The grove is where step 1 says, outside every swarm band, and no swarm grazes it.
- Every snap is preceded by a visible glow; a trap whose vessel leaves relaxes without firing.
- An opposing-domain hit by a closing lobe, tooth, pulse or beat takes petals.
- A trap missing 13+ lobe/tooth prisms never fires.
- Pulses visibly travel along cables; a cut cable re-forms.
- Nothing pops in or out: prisms fly in from and back to the plant.
- Over a 10 min session, the fortress colony and thief nest (QA-SWARM-ROUND11-5) never carry off a tube, and the
  thief nest is not inside the grove's sector.

**FAIL:**
- A snap with no glow first, or a glow that never resolves.
- Danger with no petal loss, or burned petals that come back as crystals.
- A trap whose jaws reach into the swarm band or through the membrane.
- Cables that never pulse, never re-form, or that chase your trail.
- Tubes or trap prisms appearing or vanishing without a flight.
- A fortress wall or thief hoard containing a physarum tube; a hoard shrinking because a trap or cable ate it;
  a thief nest sitting inside the grove.
- A frame-time regression above ~1 ms per frame near the grove, or a warm-up hitch over ~10 ms.

### QA-COMPILE-ROUND11H-1 ⬜ — the project opens clean and Entities prisms still render

**Source:** branch `overnight/compile` (compiled headless against real package sources + Unity 6000.0.75 engine
references by `Tools/Build/unity_refcompile/run.sh`; not opened in the editor). Reference:
`Docs/SWARM_FAUNA.md` §25. **Why it matters:** the real-reference compile found two errors from tonight that would
have stopped the whole project compiling on open (`Random` ambiguous in three fauna/flora files;
`EntityManager.GetComponentLookup` is internal in Entities 1.4.2). PrismRenderService now takes its lookups from a
never-updated `PrismRenderLookupSystem`.

1. Open the project; wait for the compile. The Console must show no CS errors.
2. Enter Play in a scene with many prisms (any arcade mode, or the Swarm cell). Read the PrismRenderService status
   line (Entities ON, ents > 0).
3. Let a swarm or a trail grow; watch newly created prisms appear at their positions (batched create path) and move
   (batched transform path).
4. Exit Play, enter Play again (world rebuilt): prisms render again.

**PASS:** clean compile; prisms render, appear in place and follow their owners across a Play restart.
**FAIL:** any compile error · prisms at the origin or frozen · an exception mentioning `PrismRenderLookupSystem` or
`ComponentLookup` · prisms missing after the second Play.

### QA-SWARM-ROUND10-1 ⬜ — four creatures, and their strikes burn petals

**Source:** branch `cece/swarm-fauna-game` (type-checked only, not run in the editor). Reference:
`Docs/SWARM_FAUNA.md` §18. **Why it matters:** before round 10 only the pufferfish could hurt you, so most swarms
had no stakes.

1. Fly into the Swarm cell as a domain that does NOT control it, with a few petals on your HUD flower.
2. Mass lurkers: creep slowly toward one until it shows danger plates, then touch one. Next, rush another at speed.
3. Space locusts: from a distance, watch the cloud; a quarter should glow dangerous, shifting about every 2 s.
   Fly through, once hitting a lit member and once threading the gaps.
4. Time pack hunters: approach; they should turn dangerous sooner than the Charge pufferfish do.
5. Repeat one hit as the cell's controlling domain.

**PASS:** each creature is dangerous in its own pattern; an opposing-domain hit takes petals that do not reappear as
crystals; a rushed lurker bolts without plates; an own-domain hit only stings.
**FAIL:** no plates on Mass/Space/Time · plates but no petal loss · burned petals drop as crystals · a whole swarm
stays lit permanently · frame rate drops versus round 9.

### QA-SWARM-ROUND9-1 ⬜ — swarms graze and move on

**Source:** branch `cece/swarm-fauna-game` (headless only). Reference: `Docs/SWARM_FAUNA.md` §17.1. **Why it
matters:** in round 8 a swarm parked on the nearest plant's heart crystal and never left.

1. Enter the Swarm cell and watch one swarm for 3-5 minutes from a distance (no vessel near it).
2. While it is growing (hungry) it should travel to a plant, graze, then leave for ANOTHER plant or roam.
3. Once full-grown it should roam its band rather than sit on a plant.
4. Kill a chunk of it; it should go back to grazing, and leave the plant once sated.

**PASS:** no swarm stays on one plant for more than ~1 minute while sated; hungry swarms visit plants in turn.
**FAIL:** a swarm hovers on a plant's crystal indefinitely · a swarm never goes to food while it is growing.

### QA-SWARM-ROUND9-2 ⬜ — one colour at birth, a second lineage owns a region

**Source:** branch `cece/swarm-fauna-game` (headless only). Reference: `Docs/SWARM_FAUNA.md` §17.2 and the CLAUDE.md
exception beside "No domain asymmetry". Headless: 8/8 whales grew back and belly in different domains (R9b).

1. Select `SwarmSortFaunaConfig.asset`: **Multi Domain on**, **Lineage Drift 0.01**.
2. Enter the Swarm cell. Every swarm must hatch ONE colour (the controlling domain). No shader errors on
   `CosmicShore/SwarmMemberInstanced`.
3. Watch the bodies fill out. In most of them a second colour should appear as a few members, then fill one
   region (whale: back OR belly; pufferfish: top OR bottom) rather than speckle the body.
4. Graze-test: grazing other domains' mass must NOT add their colours (food colours nothing).
5. Across several sessions, the second colour and the region it takes should both vary.
6. Weapons: own-colour members are spared, others die. Proxies (fly close) match their member's colour.

**PASS:** births one colour; a second colour grows as a region; colour and region vary between swarms; food does
not recolour. **FAIL:** a newborn in a food's colour · random speckle instead of a region · every swarm picks the
same colour or the same region · shader error.

### QA-PALETTE-SHIELDED ⬜ — the four prism tiers across all three domains
**Source:** PRs #644, #705 (danger prisms now paint on the domain's **shielded base
face**), #707 (gold's shielded prism brought into the pastel family; the danger tier
un-inverted). Colorimetry verified by simulation only — the engine has never rendered
any of it. Reference: `Docs/PALETTE.md` §6.

1. Pull and let `OriginalColorSetSO.asset` **reimport**. If nothing looks different,
   suspect a stale Library and Reimport before suspecting the values.
2. **Shielded prisms** — any cell with lifeforms in Menu_Main freestyle (every
   flora/fauna health prism is shielded — the densest sample in the game). Confirm
   **gold shifts to sand/cream**, the warm counterpart of Jade's mint and Ruby's pink.
3. **Danger prisms** — Cleave ships the same trap in all three
   domains; the worm colony (Spawn Matrix toy) and dangerous flora also work.
   Confirm the rim reads as a **bright incandescent red glowing off a frostier body**,
   not a dark edge.
4. Compare a **gold danger** prism against a **gold plain** prism at speed.
5. Compare a danger prism against a **shielded** prism of the same domain — they now
   share a base face, so the danger rim is the only separator.
6. Within one domain, compare all four tiers: plain → shielded → supershielded should
   step visibly brighter, and danger should be unmistakably its own thing.
7. Check **plain gold** still reads as gold and not amber-brown (its rim peak dropped
   1.50 → 1.00).

**PASS:** gold shielded reads sand/cream and not "gold, slightly lighter"; no domain
blooms hotter or flatter than the others; danger reads as hue/chroma separation rather
than pure brightness; gold danger is not confusable with gold plain at speed; danger is
clearly distinct from shielded despite the shared base; the four tiers step visibly.
**FAIL:** a domain blowing out under bloom · shielded reading *dimmer* than unshielded
· gold shielded reading chalky/dead (came down too far) or barely changed (not far
enough) · a flat-looking danger prism · gold danger confusable with gold plain.
**Judgement call to report:** danger now sits at the palette's brightest tier alongside
supershielded — a deliberate call, but a bigger jump than the numbers convey. Say
whether it reads as alarming or as noise.
**Known, do not fail on:** explosion/implosion debris is painted from the *plain*
domain pair regardless of the source prism's tier, so danger and shielded prisms shed
plain-coloured debris (pre-existing, `Docs/PALETTE.md` §7).

### QA-ECOLOGY-SKELETON ⬜ — a joust takes the heart, starvation exposes it, both leave a skeleton
**Source:** PR #709. Reference: `Docs/ECOSYSTEM.md` §26.8. Touches every lifeform death
in the game, so a defect here is fleet-wide.
1. **Joust a fauna** in `Menu_Main` freestyle (Squirrel is the menu vessel).
2. **Joust a flora** — watch specifically for a detonation.
3. **Starve a fauna**: lower `starvationSeconds` on the `LightFaunaDataSO` and watch a
   creature run out.
4. **Devour**: let a predator eat a creature at the jaws.
5. Watch a herbivore approach the leftover **skeleton** prisms.
6. Watch the Console throughout.

**PASS:** a jousted creature does **not** explode — the crystal flies to *your* vessel,
arms/fins evaporate **from the body outward**, and a skeleton is left hanging; a jousted
flora likewise does not detonate; starvation is the mirror (extremities first, heart
collectable only at the end, skeleton left); a devoured creature suctions into the mouth
with **no** skeleton; herbivores eat skeleton prisms; no crystal-invariant errors.
**FAIL:** an explosion on a joust · a creature vanishing instead of withering · no
crystal, or a crystal that does not fly to you · a skeleton after a devour · herbivores
ignoring skeleton prisms (the re-file did not land) · any crystal-invariant error.
**Known, do not fail on:** the worm colony is deliberately excluded from the skeleton,
and flora deaths *other than* the joust still detonate.
**Report:** whether skeletons accumulate to a visually or performance-troubling degree
over a long round — only a playtest can answer that.

### QA-ASTROLEAGUE-REWORK ⬜ — blade strikes, the fauna pen, the smaller court, the settling ball
**Source:** PRs #704 and #706 (#706 is a playtest follow-up to #704, itself unflown).
Reference: `_Scripts/Controller/Arcade/ASTROLEAGUE.md`, `Docs/ECOSYSTEM.md`.
1. Play **Astro League**. Judge the court size first — #706 cut it **40 %** linearly.
2. **Strike the ball with a swung sword tip**, then with a **parked** sword at the same
   closing speed.
3. Strike the ball hard and let it run. Time roughly how long until it comes to rest.
4. Bounce the ball off a **wall** repeatedly and watch the carom lose energy.
5. Fly the pitch and watch the **fauna**: the nucleus is play geometry here, not a
   control zone, so creatures must be edible inside it.
6. Check the **sphere cage cover** renders on every intensity.
7. Score a goal and watch the replay (also covered by QA-SPEED-TUNNEL step 4).
8. Watch/listen for the new strike feedback: pop, shake, burst.

**PASS:** a swung tip sends the ball dramatically harder than a parked sword's deflect;
a struck ball loses roughly 65 % of its speed in ~3 s and genuinely reaches **rest**
rather than creeping; wall bounces cost energy while a vessel strike stays fully
elastic; fauna are eaten inside the court (no arena-wide sanctuary); the cage renders at
every intensity; strike feedback fires on contact.
**FAIL:** a parked sword firing the ball · a ball that never settles or that stops dead
· fauna untouchable anywhere on the pitch · a missing cage at some intensity · no strike
feedback.
**Judgement calls to report (each a one-field edit on `AstroLeagueSettings.asset`):**
`ballDrag = 0.35` (sluggish → drop toward 0.2; won't die → push toward 0.5),
`wallRestitution = 0.72` (a billiards-ish guess), and the 40 % court cut (if it now
reads too small, the right size is between this and #704).

### QA-SPARROW-BOOST-WARD ⬜ — overheat removed, strafing roll freed, Elemental Ward added
**Source:** PR #675. **Highest risk on the branch: two vessel prefabs were hand-edited
as YAML** (a removed GameObject, a removed resource slot, a new component on each,
renamed serialized fields). Reference: `SPARROW_AFTERBURNER.md` § In-editor verification.
1. Open the **Sparrow** and **Serpent** prefabs. Confirm: no missing-script rows;
   `OverheatingBoostActionExecutor` is **gone**; `ResourceSystem` reads Missiles /
   FullAuto / ExhaustBarrage (**3 slots, no Heat**); `SparrowHUDController.barrelRollController`
   is wired; `VesselElementalImmunity` sits on each root with the right condition.
2. Hold boost for **60 s** continuously.
3. At Time level 0: boost + full stick deflection.
4. Watch the boost icon ring across a press → roll → release cycle.
5. At Time ≥ 5 (set `TimeTestHarness = 0.5`): fly into a **danger prism while boosting**,
   then into one **while not boosting**. Watch the elemental flowers each time.
6. Repeat step 5 on a **stopped Serpent**.
7. MPPM, two clients, one Sparrow at Time 5 — both machines must agree on who resists.

**PASS:** both prefabs inspect cleanly; 60 s of boost produces no force-release, no
danger trail and no self-slam; one press = exactly one roll; the boost ring is full on
press, wipes empty on the roll, and stays empty until the next press (never a partial
fill); at Time 5 a danger prism **while boosting** leaves the flowers undipped and
**not boosting** dips them (the slow and input-mute still land either way, by design);
the stopped Serpent never dips at any Time level; both peers agree.
**FAIL:** a missing script or a Heat resource still present · boost force-releasing or
laying a danger trail · more than one roll per press · a partially-filled boost ring ·
the ward applying in the wrong boost state · peers disagreeing.
**Open design question to report:** the ward holds `WhileBoosting` (mirrors the
Serpent). With boost now unbounded, a pilot flying permanently full-throttle is
permanently warded. Say whether it should be `Always` — it is one inspector field.

### QA-SPARROW-STOPPED-ROLL ⬜ — the strafing roll works stopped, and the stance turns 3× faster
**Source:** PR #679. Reference: `SPARROW_AFTERBURNER.md` §2.1–2.2 + items 4b–4e.
1. **Regression first:** fly the Sparrow normally and confirm the **flying** roll is
   unchanged — the shared modifier path was touched.
2. **Stopped:** enter the stance, then boost + full left stick.
3. Stopped, aim well away from the heading you had when you stopped, then dodge.
4. Stopped, take a knockback (no movement), then release the stance.
5. Time a **180° yaw** in the stance vs. out of it, then release the stance and time
   again.
6. **Serpent:** take a knockback and release — confirms the exemption is scoped to the
   roll.
7. MPPM two clients: a stopped roll must replicate like the flying one.

**PASS:** the flying roll is unchanged; stopped, boost + full stick rolls **and**
strafes, exactly one roll per press, and you are still stopped afterwards (stationary
fire, no trail laid); the strafe follows **current facing**, not the heading you stopped
with; releasing the stance produces no lurch; a 180° yaw takes about a third as long in
the stance and the rate drops straight back on release; the Serpent holds knockback with
no lurch; the stopped roll replicates.
**FAIL:** a changed flying roll · no roll or no strafe when stopped · a strafe skewed
toward the old heading · a banked lurch on release · a turn rate that stays fast after
release (`TurnScalar` got cached) · a roll that does not replicate.

### QA-SPARROW-TURRET-STANCE 🟡 — Turret Stance fires real prisms
**Source:** PR #696. **Play-tested by the prompter through the final commit** (six
rounds; the ShaderGraphs have been imported and rendered), so this item is only about
the two things that report did not cover. Reference: `SPARROW_TURRET_STANCE.md`.
1. **MPPM, two clients.** Enter Turret Stance and fire for 30 s while the other peer
   watches. Turret prisms come from a local `blockFactory` and `ProjectileImmuneUntil`
   is local `Time.time` state — nothing here is networked.
2. **The CHARGE-5 skyburst flip.** Below Charge 5, confirm the skyburst blast
   **friendly-fires**. At Charge 5, confirm the blast goes **domain-safe** while the
   direct hit behaves as before.
3. While at it, note whether `placementImmunitySeconds` still reads too long (you should
   not be able to shoot your own just-placed prism, but the window should not feel like
   a dead zone).

**PASS:** both peers see a coherent turret volley with no desync or duplicated prisms
and no exceptions on either machine; the blast friendly-fires below Charge 5 and is
domain-safe at Charge 5.
**FAIL:** prisms appearing on one machine only, or in different places · the blast
friendly-firing at Charge 5, or going domain-safe below it · any exception during a
sustained volley.
**Known, do not fail on:** transform-moved triggers at high speed can tunnel through
thin prisms — bullets do it too, and parity is the point.

### QA-SPARROW-MISSILE-BAY ⬜ — bay-animated skyburst launch with the real missile model
**Source:** PR #708. Authored without a Unity compile or play-test. Reference:
`Docs/UNITY_VERIFICATION_CHECKLIST.md` 🔴 "Sparrow Skyburst Missile Bay".
1. Fire a skyburst and **watch the hull** — this is the one thing only the editor can
   prove (a **cross-FBX clip binding**).
2. Fire several in a row and watch which side launches each time.
3. Fire during a hard maneuver (roll + pitch together).
4. Watch the launch seam: the bay opens, then the projectile leaves ~0.2 s later.
5. Look at the exhaust particles against the missile's ~1.7 u visual.

**PASS:** the bay physically opens on the hull before launch; sides alternate
**right-then-left**; no puppetry fight (bay animation vs flight animation) during hard
maneuvers; the handoff reads as one motion, not two; exhaust particles are sized for
the missile.
**FAIL:** a bay that never animates (the clip binding failed — the projectile still
spawns at the bay-bone rest pose, so this fails quietly) · both missiles from the same
side · the hull visibly fighting itself mid-maneuver · a visible gap or double-motion at
the handoff.
**Tuning to report:** `launchDelaySeconds` (0.2; useful range 0.16–0.26).
**Known, flagged, do not fail on:** the skyburst's direct-hit sphere (world radius 8.5)
now visibly dwarfs the missile — a Dog Fight balance call, already recorded.

### QA-VESSEL-SPARROW-ROLL ⬜ — Sparrow rolls on prism hit
**Source:** PR #669 (two hand-authored assets, never imported; 60° is a guess).
1. Inspect the Sparrow's prism-effect container: `VesselRollByPrismEffect` in **slot 0**,
   inspecting cleanly (no `Missing (Mono Script)`).
2. Fly the Sparrow into prisms at a few angles and speeds.

**PASS:** the asset inspects cleanly and every prism hit **rolls** the vessel rather
than redirecting its course; control is recoverable.
**FAIL:** missing script · no roll · the vessel still being deflected off-course · a
roll so violent it reads as a loss of control.
**Report the feel:** `rollDegrees` (60) is a single inspector value — say whether it
wants to be larger or smaller.

### QA-DOLPHIN-SPEED-TUNE ⬜ — +30 % cruise, +70 % charged boost
**Source:** PR #681 (in-place scalar edits; the arithmetic is machine-checked, the
*feel* is entirely unflown). Reference: `Docs/UNITY_VERIFICATION_CHECKLIST.md` 🔴
"Dolphin speed + charged-boost retune".
1. Menu_Main → freestyle → **Dolphin**. Full throttle, no boost — read
   `VesselStatus.Speed`.
2. Hold drift from an empty meter and time the boost ring filling.
3. Release a full meter and read the peak speed, then time the fall back to cruise.
4. Drift → release → drift again.
5. Fly any other vessel as a regression check.

**PASS:** cruise settles at **≈78** (was 60); the ring fills in **≈3.6 s** (was 4); the
peak reads **≈357** and takes ~2.5 s to fall back (was 210 over 2 s); speed returns to
normal with no stuck multiplier; no other vessel changed.
**FAIL:** numbers materially off the targets above · a stuck multiplier after
drift→release→drift · another vessel's speed changing.
**Judgement call to report — this is the point of the item:** 357 is a big jump and the
speed tunnel amplifies how it reads. Say plainly whether it is too much.

### QA-VESSEL-AOE-IMPULSE ⬜ — explosion inertia, the Dolphin capsule cone, and debris spin
**Source:** PRs #652, #632, #643, **#680** (the blast's collider was hand-swapped from a
Sphere to a **Capsule** at the YAML class-id level — `!u!135` → `!u!136` — and aligned
with the jaw gape).
1. Select `_Prefabs/Projectile/AOEConicExplosion.prefab`. The root must show a
   **Capsule Collider** (Is Trigger ✓, Radius 0.0667, Height 1, Direction **Z-Axis**,
   Center 0/-0.5/0) — **not "Missing", not still a Sphere**. This is the riskiest edit
   on the branch and shows up nowhere else. Also confirm **Inertia 1.8 / Proportional
   Debris ✓ / Debris Restitution 0.333**.
2. After a SkimRace/Skim track spawn (so pools have cycled super-shielded prisms), lay a
   Squirrel overheat **danger** trail, then detonate a Dolphin crystal blast into it.
3. Dolphin + crystal in open space: watch the cone's reach and where destruction ends.
   **Roll 90° and fire again** — the fan must roll with the ship (ship-up, not world-up).
4. Watch the jaws at **zero energy** (slightly open) and at every charge step — they
   must agree with the HUD icon.
5. Watch the direction struck prisms fly.
6. Fire one **spherical** AOE (e.g. Rhino) as a regression check, and check the Manta /
   Rhino / Serpent / Squirrel crystal blasts and the Sparrow skyburst are unchanged.
7. Blow up prisms at a range of impact speeds and watch debris **tumble**.

**PASS:** the capsule collider is present and correct; the blast expands through danger
and regular-shielded prisms (shields pop, danger takes damage) and stops **only** on
stellated super-shielded prisms; the charged blast reads as a **fan** that rolls with
the ship; the cone mesh and its destruction both reach ≈2400 units with a travelling
wavefront; struck prisms fly radially from the **apex**, not from the wavefront; jaws
agree with the HUD at every charge step; the spherical AOE and the other four vessels'
blasts are unchanged; debris tumbles noticeably more than before **at the same flight
speed and shatter timing**.
**FAIL:** a Missing or still-Sphere collider · the blast stopping on a danger prism ·
destruction falling short of the cone mesh · a fan bound to world-up · debris flying
from the moving wavefront · flight speed or shatter pace changing with the spin tune.
**Known cosmetic gaps (report, do not fail on):** the conic VFX spawn flash does not
scale with the tripled height; the rendered cone widens with the capsule's length by
construction, so full charge draws wider than it destroys off-axis.

### QA-CRYSTAL-CHARGE-SHADER ⬜ — the dedicated charge-crystal shader
**Source:** PR #710 (new `.hlsl` + ShaderGraph + `CrystalEdgeArcs` component, compiled
and rendered offline with clang but never by Unity).
1. Open `CrystalCharge.prefab`. Confirm `CrystalEdgeArcs` sits on `chargeShell` with no
   *Missing (Mono Script)* row.
2. Enter play in a scene that spawns **charge** crystals (freestyle with elemental
   crystals is easiest).
3. Watch one crystal for ~20 s at gameplay distance and again up close.
4. Watch a crystal **appear**.
5. Look at the crystal against a bright background / with bloom on.

**PASS:** the crystal is a **static faceted solid** (not spreading or spinning its
geometry); bolts crackle along the prism **edges only**, never across face interiors;
no permanent starbursts at the vertices; the crystal **blooms in** rather than popping;
nothing blows out against bloom.
**FAIL:** a magenta crystal (stale Library — reimport the shader before failing) · bolts
drawn straight across face interiors · always-on vertex starbursts · a crystal that pops
into existence instead of blooming (that breaks the platform-wide continuity law) ·
blowout against bloom.
**Judgement call to report:** the screen-door emergence replaced an alpha blend — say
whether it reads better or worse at gameplay distance.
**Tuning lives on `ChargeCrystalMaterial`:** `_ArcWidth` / `_ArcIntensity` / `_ArcJitter`
/ `_ArcDuty` / `_ArcSpeed` for the discharge; `_RimStrength` / `_FacetAmbient` /
`_EmissionStrength` for the body.

### QA-MENU-VEIL-PAUSE ⬜ — the menu-return veil hold and the prewarmed pause menu
**Source:** PRs #672, #693, #698. The prewarm crashed the **Windows player** on every
login (#693) and the root was a type-punned `pauseMenuPanel` reference (#698) — so this
item is the Editor half; the player half is QA-BUILD-WINDOWS-PLAYER.
1. Play any arcade game and **return to the menu**. Watch the transition closely.
2. Do it three more times, from different modes.
3. Reach `Menu_Main` from a **cold boot** and confirm the veil does *not* linger.
4. In a game round, open the pause menu for the **first** time and watch for a hitch.
5. Open and close it several more times.
6. Open `Pause_Menu_Panel.prefab` and `R_Pause_Menu_Panel.prefab` and confirm both point
   `pauseMenuPanel` at their own root GameObject.

**PASS:** the game→menu teardown (vessel despawns, pooled-prism churn, GC) happens
**behind** the opaque splash and the fade-in reveals a settled menu; cold boot and
auth→menu are unaffected by the ~1.5 s settle hold; the first pause opens with no
visible hitch; both prefabs reference their own root.
**FAIL:** watching vessels despawn or prisms churn during a menu return · a veil that
lingers on cold boot · a hitch on first pause · a pause menu that fails to open · any
exception from `PauseMenu.Prewarm`.

### QA-DISPLAY-NAME-VALIDATION ⬜ — display-name rules and global uniqueness
**Source:** PRs #673, #674. New `DisplayNameValidator` (local rules + profanity/leetspeak
handling) plus `DisplayNameRegistry` (UGS Cloud Save uniqueness). #674 was a *namespace
collision fix on the Cloud Save API* — i.e. the registry path has never compiled in
Unity until now. Surfaces: `AuthenticationSceneController`, `ProfileModal`,
`ArcadeProfileWidget`, `ProfileIconSelectView`.
1. Sign in as a **new** user and set a display name through the auth-scene username panel.
2. Try each rejection class and read the message shown: too short, too long, illegal
   characters, a reserved name, a plain profanity, a **leetspeak** profanity (`f4ck`), a
   **separated** one (`f.u.c.k`), and a **repeated-letter** one.
3. Try a name a second account already holds — expect a uniqueness rejection.
4. Change your name from the **Profile modal** and confirm the new name appears on the
   arcade profile widget, the party/online list and in a game round's scoreboard.
5. Watch the Console for Cloud Save exceptions on every attempt.

**PASS:** every rejection class is caught with a human-readable message; an accepted
name is saved, is unique, and propagates to every surface listed in step 4; no Cloud
Save exception anywhere.
**FAIL:** a blocked term getting through (record it) · a legitimate name rejected ·
a name that saves but does not propagate · **any** Cloud Save exception (that is the
namespace-fix regression this item exists for) · a uniqueness check that never fires.

### QA-PROFILE-ADS-REMOVAL ⬜ — Unity Ads gone, and the profile double-submit closed
**Source:** PR #694. Ads were enabled with **Android/iOS game ids only**, so every
desktop launch threw `Couldn't fetch Ads Service game Ids`.
**⚠ This item carries a two-minute editor task:** the `AdButton` GameObject still exists
in `Menu_Main.unity` and `Screens.prefab` and is only hidden at `Start`. **Delete it
from both**, then report that you did — the code field can then be removed.
1. Boot the game. Confirm no `Couldn't fetch Ads Service game Ids` in the Console/log.
2. Open the **daily reward** card. It must run **free claim → clock** — there is no
   ad-watch second claim, and no ad button visible in any state.
3. Delete the `AdButton` object from `Menu_Main.unity` and `Screens.prefab`, save both,
   and confirm nothing else in the layout shifts.
4. **Double-submit:** in the profile modal, hit Save twice quickly (and mash it).
5. Change the profile, back out without saving, and reopen.

**PASS:** no ads exception on any platform; the daily reward is free-claim only with no
stray ad button; deleting `AdButton` leaves both layouts intact; a double-tapped Save
submits **once** with no duplicate write or error; profile state is consistent after a
cancel-and-reopen.
**FAIL:** the ads exception still appearing · a visible ad button or ad-mode claim ·
a double Save producing two writes or an exception · a layout that breaks when
`AdButton` is deleted (report it and revert rather than fighting it).

### QA-ECOLOGY-WORM-KAIJU ⬜ — the worm colony boss
**Source:** PR #667. Reference: `Docs/ECOSYSTEM.md` §23.6 (spawn steps + dials).
1. Freestyle → **Spawn Matrix** toy → "Worm Colony" → any element station.
2. Watch it move, feed (prism mass, other creatures, and you), and grow.
3. Kill a **mid-body** segment and watch what happens to the colony.
4. Kill the head; kill the tail. Watch each death sequence to completion.

**PASS:** the colony slithers follow-the-leader; it grazes mass, devours creatures at
the jaws and pursues pilots; growth is funded by feeding; a mid-body kill **splits**
the colony into two viable colonies; head/tail (capital) deaths each drop exactly one
elemental crystal and body segments drop none; every death withers (extremities first)
rather than vanishing.
**FAIL:** any segment popping out of existence · a split that strands a headless or
tailless remnant · a body segment dropping a crystal, or a capital dropping none ·
the colony feeding on nothing / never growing · exceptions in the Console.
**Known, do not fail on:** the worm is deliberately excluded from the #709 skeleton.

### QA-ECOLOGY-HESPERIDES ⬜ — the garden cell
**Source:** PR #646 (9 hand-authored prefabs + 56 SO assets, first import).
Reference: `Docs/ECOSYSTEM.md` §21.7.
1. Freestyle → Cell Selector toy → **Hesperides**. Watch it build.
2. Run **Measure Cell Environment Baselines** on `SpawnableHesperides` — expect
   ≈ **12,060 prisms / ≈ 507k volume**.
3. Run **Validate Lifeform Crystals** — the eight new flora prefabs must pass.
4. Stay in the cell and let flora grow through several waves. Watch the eight forms
   (Arbor/Rosette/Frond/Coral/Spire/Tendril/Reed/Lantern) plant on their site kinds
   (beds, climbs, baskets, water, ledges).
5. Check the phase readout over time (it must not boot straight to Frenzy).
6. Plant-test the three **repaired** flora (Pine, Nerve, Wall) — they had a dangling
   `cellData` GUID and have not been planted since the repair.

**PASS:** imports clean; baseline within a few hundred prisms / few thousand volume;
crystal validator green; flora actually grow on the authored sites and the garden
thickens toward the mature planting; phase ladder behaves; the three repaired species
plant without throwing.
**FAIL:** import errors or `None` references (a null `prism` builds a silent, empty
cell) · baseline off by more than a few hundred (PhaseThresholds must be re-authored)
· flora planting inside each other / floating / not planting at all · an exception
from `Flora.Plant()`.

### QA-ECOLOGY-CALDERA-OUROBOR ⬜ — the two nucleus-aware cells
**Source:** PR #645. Reference: `Docs/ECOSYSTEM.md` §18.3.
1. Cell Selector → **Caldera**. Then → **Ourobor**. Confirm each imports and builds.
2. Run **Measure Cell Environment Baselines**: expect **Caldera 41,353 / 1,210,753**
   and **Ourobor 37,889 / 751,449**.
3. Caldera: confirm four inward-aimed massifs in tetrahedral symmetry, no ground plane,
   and **nothing laid inside the nucleus radius**.
4. Ourobor: fly a full lap of a band — confirm countryside + cityscape on **both**
   faces and that no global "up" survives the lap.
5. In both: sanity-check danger-prism density (does it play hot or cold?).

**PASS:** both import with no missing scripts and no `None` refs; baselines within a
few hundred of the expected counts; Caldera's nucleus interior is empty; Ourobor's
bands read as continuous two-sided worlds.
**FAIL:** a cell that builds zero prisms · baselines off by >few hundred · any prism
inside Caldera's nucleus · a band that reads as flat/one-sided or disorienting to the
point of unplayable (note it as PARTIAL + a note rather than FAIL if it is a taste call).

### QA-ECOLOGY-FREESTYLE-SIX ⬜ — the prepopulated cells + the deferred menu build
**Source:** PR #636 (gated minigame loads already field-verified; the rest is not).
1. Launch to `Menu_Main` repeatedly until a non-Blob cell rolls, if the boot still
   rolls worlds; otherwise pick each of the six via the Cell Selector.
2. Watch **when** the veil appears relative to the menu settling, and listen to audio
   during the build.
3. Watch the prism counter run to completion; then confirm the veil fades into a
   fully-grown world.
4. Fly through each cell: phase ladder behaviour, clearance pads keeping spawns and
   crystals clear, shielded/danger accents reading correctly.
5. Run **Measure Cell Environment Baselines** and compare with: Yggdra 34,340 ·
   Daedala 33,858 · Orrery 34,573 · Zephyr 36,069 · Caldera 31,194 · Geode 34,365 ·
   Atlantis 69,078.

**PASS:** the veil appears **after** the menu settles, audio stays clean, the counter
completes and the world is fully grown when the veil lifts; no cell sits in Frenzy at
rest; baselines match.
**FAIL:** a build that wedges (look for the `CloneBatchAsync` watchdog warning in the
log) · audio underruns/stutter during the build · the veil lifting on a half-built
world · a cell permanently in Frenzy.

### QA-TOYS-CELL-SELECTOR ⬜ — opt-in worlds, and the freestyle reset
**Source:** PR #638. Reference: `Docs/ToySystem/BACKLOG.md`.
1. **The headline:** enter `Menu_Main` cold — no veil, no "GROWING…" hold. Launch an
   arcade game and return — same. Console should log the Cell assigning **Blob**.
2. Fly the Cell Selector (≈300° around the membrane ring), pick e.g. Yggdra: old world
   suctions away, veil raises with the prism/percent readout, Yggdra grows in. Check the
   cell then reads **Calm**, not Frenzy.
3. **The riskiest path — the reset.** With a world loaded and a long trail laid, fly the
   toy and pick the **same** cell. Do this **on the Squirrel** specifically.
4. Repeat the reset several times, then lay fresh trail.
5. Run the Wanderway conveyor, then reset the cell.

**PASS:** cold boot and game-return are veil-free; a pick suctions the old world and
blooms the new one behind one veil; picking the current cell resets freestyle cleanly;
**no `Trail`/`TrailFollower` NullReferenceExceptions** on the Squirrel; after several
resets pooled trail prisms still spawn at **full size**; the Wanderway belt survives a
reset untouched.
**FAIL:** a veil on cold boot or on return from a game · any NRE during a reset ·
shrunken/zero-scale trail prisms after a reset (suction scale baked into the pool) ·
the conveyor's scenes vanishing or duplicating.

### QA-TOYS-EMBLEMS ⬜ — every toy is an icon of what it selects
**Source:** PR #655 (~2,400 lines, nothing compiled or run).
1. Enter freestyle — watch for any emblem visibly assembling during the bloom. Check
   this on a **return from an arcade game**, not just cold boot.
2. Compare Load Time Insights before/after: no new Environment-category span.
3. Fly the whole membrane ring. For each toy, ask: identifiable **without reading the
   label**? Record each failure.
4. Fly Wanderway → orbit spins up over ~0.8 s; leave freestyle → drops to a dormant
   crawl; fly again → stops. While doing this, watch **other** toys' colours.
5. Fly the domain changer → the vessel-changer emblem hulls re-tint within 0.5 s. Swap
   ship → the emblem core becomes the new hull and keeps spinning.
6. Cell Selector emblem: at boot it is a small bare core; pick a world → after the veil
   it blooms as that world; pick the environment-free cell again → the placeholder
   returns (not an invisible station).
7. At the Lifeform bench, look at the seven flora icons.

**PASS:** no emblem assembles in view; no new load span; the Wanderway spin states
behave and **no other toy changes colour**; the vessel/domain emblems re-tint and
re-shape; the Cell Selector emblem tracks the loaded world; flora icons read as
branch/lattice/surface forms, not spheres.
**FAIL:** an emblem building in view · another toy changing colour when Wanderway spins
(shared-material bug) · an invisible station · spheres where flora forms should be.
*(Ring-distance legibility is a judgement call — report failures as notes, not FAIL,
unless a toy is genuinely unidentifiable at ring distance.)*

### QA-TOYS-WANDERWAY-RUN ⬜ — grand scale, the tether, and the way home
**Source:** PR #654. Reference: `Docs/ToySystem/BACKLOG.md` ▸ "Wanderway — the run".
1. Fly the toy → the cell suctions away and returns as bare Blob behind **one** veil.
2. Fly outward and watch your trail length settle.
3. Turn around → the return station should be riding the tail of your tether. Fly it.
4. Wander again → the belt resumes with **no** second veiled build (watch the prism count).
5. Exit a run via the **overview button** and via **gamepad Start**.
6. Repeat step 2 on the **Squirrel**, riding your own tether.

**PASS:** one veil, one build (30k prisms) ever; the trail stabilises at ~100 prisms and
**stays** there; the station sits one tether-length behind and glides (not snaps); flying
it returns you home with speed intact; both alternate exits end the run; the Squirrel
rider stays put as the tail recycles.
**FAIL:** trail length climbing without bound (a ribbon is not rolling) · a doubled prism
count on the second wander · the station snapping or unreachable · a Squirrel thrown off
its own tether · scenes visibly popping in or out of existence.

### QA-ECOLOGY-ELEMENTAL-VARIATIONS ⬜ — four elemental variations, and a heart sized to its lifeform
**Source:** PR #635 (the element spread), then the levels-retired pass. Reference:
`Docs/ECOSYSTEM.md` §40. **Levels are gone**: a lifeform is its species and its element and
nothing else, and each element authors its own heart size — so this item is no longer about
finding giants, it is about the four elements being real and the heart sizes being right.
1. Open a spread-enabled spawn config in the inspector: confirm `Spread Elements` and a
   4-entry `Element Palette`. There must be **no** `Levels` block, `Initial Level`,
   `Body Scale Per Level` or `Leaf Scale Per Level` field anywhere on it — if you see one,
   the asset did not migrate. (If a field reads default, re-save the asset from the inspector.)
2. `Menu_Main`, freestyle, watch a few fauna waves.
3. Kill a **tadpole**, a **brittlestar** and a **shark** in one session and compare the
   crystals they drop. Roughly 1.6 / 2.7 / 4.6 world scale — the shark's should read as
   clearly the biggest prize; they used to be identical.
4. **The size trap.** Spawn a **Mass or Time tadpole** and a **Charge or Space** one from the
   Spawn Matrix bench, kill both, and compare their hearts. They should be close — **1.56
   and 2.07** world scale, a 1.33× difference. If a creature's heart is being shrunk by its own
   body scale the two drop at **0.63 and 1.45** instead (a 2.5× and 1.43× cut), which reads as
   a **2.3× gap** between them and as two conspicuously tiny crystals. Either signal — report
   it; it is the regression this pass exists to prevent.
5. Follow one grazer through several feeds and one plant through a birth: **nothing may change
   size mid-life** — not body, not leaf, not heart. A visible step is a level surface that
   survived.
6. Let a brood reproduce and compare the offspring's element with the parent's.
7. Spawn Matrix toy: open a species. The variant layer must be **four stations, one per
   element** — no level rows — and each station's crystal drawn at that variant's own heart
   size.
8. Play Skim Race and Nucleus Rush briefly and judge whether cadence still feels right.
9. **Squirrel Shepherd** (Space level 5): joust an OWN-domain creature. It must **not** grow —
   watch its brood instead; a nourished creature should reproduce sooner. Joust an own-domain
   plant and expect an offspring, not an inflating plant.

**PASS:** one species' brood shows all four crystal **models** (not just recolours);
different SPECIES drop visibly different-sized hearts while two creatures of the same species
and element match exactly; nothing grows mid-life; offspring match the parent's element; the
variant layer is four element stations and spawns exactly what each advertises; shepherding
breeds rather than enlarges.
**FAIL:** a single element across a whole brood · a level/size field still on a config · two
same-species hearts of visibly different size · tadpole hearts dropping at roughly 0.6 / 1.5
rather than 1.6 / 2.1, i.e. a ~2.3× gap between the two tadpole elements (step 4) · anything
growing mid-life · a brood whose offspring change element · a matrix station spawning the
wrong variant · a shepherded lifeform getting bigger.

### QA-ECOLOGY-FAUNA-FEEDING ⬜ — intentional feeding + shark predation + jaw rig
**Source:** PR #614, shark-jaw `438070a2`, checklist entry in
`Docs/UNITY_VERIFICATION_CHECKLIST.md`. Design: `Docs/ECOSYSTEM.md` §7/§7.3.
1. Open `Assets/_Models/Fauna/MassSharkFauna.prefab`: confirm `SharkJawDriver` sits on
   `Shark_model` beside the `Animator` + `RigBuilder`, both mouth `MultiAimConstraint`s
   and the `MawTarget` are present and wired, and weight 0 = closed / 1 = aimed at
   `MawTarget`.
2. Confirm the tadpole's `FaunaConfigurationSO` / prefab Variant carries its intended
   elemental setup and points at the creature prefab's `Boid`.
3. Play `Menu_Main` (Blob cell) and watch herbivores approach mass.
4. Watch tadpole swarms around a concentration of mass.
5. Watch sharks: entry point, hemisphere, pursuit, and rhythm over ~60 s.
6. Watch a shark's mouth (and the danger prisms parented to the jaw bones) across a
   hunt cycle.

**PASS:** herbivores approach → brake → **turn to face** → suction, and park to graze a
buildup rather than drifting past; tadpoles settle instead of ping-ponging; sharks enter
from top/bottom ~1 per 30 s wave, stay in their hemisphere, visibly pursue, and show a
~10 s hunt / ~10 s rest rhythm; the mouth yawns open (≈0.6 s) entering a hunt and eases
shut (≈1.8 s) at rest with the teeth moving with it and no snap at spawn.
**FAIL:** herbivores vacuuming mass at range without facing it · swimming past food ·
oscillating tadpoles · sharks everywhere at once or never resting · a jaw that never
moves, snaps, or leaves its danger prisms behind.

### QA-ECOLOGY-HERBIVORE-RULES 🟡 — spawn rotation / shielded diet / steering, after the buff merge
**Source:** PR #631 (verified in-editor *before* the since-removed `DomainFaunaBuffSystem` landed — `Docs/ECOSYSTEM.md` §15; step 3's petal-bar watch no longer applies).
1. Lobby/Blob: watch several fauna waves — do groups rotate around the spawn ring, and
   does a full wave hatch?
2. Skim Race: watch brittlestars pick feed targets around the super-shielded track.
3. In freestyle, watch the **elemental petal bars** while a big live fauna population
   is up.

**PASS:** waves rotate around distinct ring points; brittlestars never target shielded
or super-shielded mass and never stall staring at it; the petal bars climb faster to 10
with transient spikes above it, and settle back to at most 10.
**FAIL:** every wave seeding at the same point · fauna steering onto shielded mass ·
a creature frozen mid-approach · any element **held** above level 10.

### QA-VESSEL-RHINO-SWORD ⬜ — sword point-velocity + the debris retune
**Source:** PR #639. Reference: `RHINO_SHIELD_SWIPE.md` § In-editor verification (5–11).
1. Fly straight with **no trigger**: hit a prism with the hull, then hit one with the
   parked sword at the same speed.
2. Mid-swipe: hit prisms with the **tip** and with the **hilt**. Select the
   ForceFieldSkimmer in play mode to see the per-point velocity gizmo rays.
3. Clip your own Rhino trail (small prisms, vol ≈ 0.75) and a fat environment prism at
   the same speed.
4. Fly a couple of other vessels and fire projectiles at prisms.
5. Play Astro League and trigger a field reset.

**PASS:** hull and parked-sword hits throw debris at the **same** speed; a tip strike
visibly beats a hilt strike and throws along the swing tangent; small and large prisms
at the same speed match; other vessels/projectiles throw debris at ~1/3 the old speed
with nothing else changed; Astro League's field-reset prisms animate out instead of
freezing.
**FAIL:** a parked sword adding speed · tip and hilt identical · debris speed varying
with prism size · debris pinned to one speed regardless of impact.
**Judgement call to report:** shatter is now ~3× slower on gentle grazes (violence
tracks force by design). Say whether the slow end reads as sluggish.

### QA-UI-ABILITY-ROW ⬜ — the four-icon ability row and its control hints
**Source:** PR #637. Note: hint placement failed in-editor three times on that branch
after passing the author's arithmetic — treat play-mode confirmation as required.
1. Let Unity reimport the six HUD prefabs; watch the Console for import errors.
2. Run **Audit Vessel Ability Rows** (see QA-AUDIT-TOOLS).
3. Play **Squirrel** in freestyle: four icons lower-right in **charge → mass → space →
   time** with even spacing. Confirm `(LT)` sits under **drift** (2nd) and `(RT)` under
   the **boost ring** (4th). Without a gamepad you should see the keyboard set
   (`LShift`/`RShift`).
4. Raise one element to level 5 (elemental crystals or the comeback buff).
5. Play **Sparrow**: same order, each glyph beside its own ability. *(The Sparrow's
   ability set changed in #675 — overheat is gone — so also confirm the row still holds
   four icons and no icon refers to a retired ability.)*
6. Play **Serpent**: labels at the right edge (**not** mid-screen), silhouette/trail at
   the left, boost button unmoved.

**PASS:** four icons in element order on Squirrel and Sparrow; hints sit on their own
ability; a level-5 element grows a white petal badge on that ability's icon and the icon
rests slightly larger; the Serpent HUD lands on-screen where described.
**FAIL:** icons out of order or missing · a glyph under the wrong ability or off-screen
· no upgrade signal at level 5 · Serpent labels mid-screen · a Sparrow icon still bound
to overheat.
**Known, do not fail on:** Sparrow renders Xbox **and** PlayStation glyph sets at once,
and its glyph art is wrong (`R1` where RT is meant) — both already logged.

### QA-VESSEL-HULL-MORPHS ⬜ — elemental hull morphs + the spliced Squirrel FBX
**Source:** PR #641. **Highest-risk item here is the Squirrel FBX** — a binary-spliced
hybrid that has never been through the importer.
1. Let Unity import. Watch specifically for the Squirrel FBX reimport and any error.
2. Run **Audit Vessel Elemental Morphs** — expect 7/11 vessels with all four elements
   (Squirrel included).
3. Fly the **Squirrel**: confirm input puppetry (pitch/yaw/roll/throttle take blending)
   behaves as before.
4. With the `ResourceSystem` elemental test-harness sliders in play mode, sweep each
   element 0→10 and watch the hull.
5. Repeat on Sparrow / Serpent / Manta, comparing hull against the HUD flowers.
6. Look at the **Dolphin** — its engine-case animation changed (engines no longer dragged
   toward identity).

**PASS:** clean import with no meta regeneration; audit reports 7/11; the Squirrel's
animation is unchanged from before the branch; hulls **glide** (never snap) between
extremes and agree with the HUD flowers; levels below 0 hold the level-0 silhouette and
above 10 hold the level-10 extreme; the Dolphin reads as fixed.
**FAIL:** Squirrel FBX import errors or lost animation takes · a vessel with shape keys
that never morphs · snapping instead of gliding · hull and flowers disagreeing · the
Dolphin reading as a regression.

### QA-SCURRY-SPAWN-RING ⬜ — half-nucleus cell, crystal volume, cell-relative spawn ring
**Source:** PR #659 (the ring's first version silently spawned players **inside** the
nucleus — that class of bug is what this item exists to catch). Dog Fight and Wildlife
Liberation now use the same formation code (sphere and equatorial ring respectively).
1. Run `CellSpawnFormationTests` (covered by QA-EDITMODE-TESTS) — note the result here too.
2. Run **Ecology ▸ Audit Cell-Owned Visuals**: expect *"SCENE-PLACED DUPLICATES: none"*
   and *"DEAD CELL OVERRIDES: none"*, with `SkyboxModel` entries under OK.
3. Open each of the 12 touched scenes so Unity reimports them — especially
   **Recording Studio** and **MattsRecording Studio** (their backdrop was left alone and
   must still render).
4. Play **Crystal Capture**. Read the console line `Spawn ring: N players at 236u
   (nucleus 196 + 40)`.
5. Play it at 4, 3 and 2 players.

**PASS:** audit clean; all 12 scenes reimport with no missing references and no console
errors; both Recording Studio backdrops still render; you spawn **outside** the core
facing it; 4/3/2 players give tetrahedron / triangle / opposite-poles; crystals fill the
nucleus rather than a wide ball.
**FAIL:** spawning inside the nucleus or at the old 70u radius · a formation that does
not match the player count · crystals scattered outside the nucleus · a black
Recording Studio.

### QA-ARCADE-SKIMRACE-INTENSITY3 ⬜ — new circuit + per-intensity laps
**Source:** PR #626 (scene YAML hand-authored; a silent fallback is the failure mode).
1. Open `MinigameSkimRace.unity`, select the crystal turn-monitor object, and confirm
   **Laps Per Intensity** shows `3, 3, 2, 2`.
2. Launch Skim Race at **intensity 3**.
3. Race the full track; watch the lane braid and the 120-unit lane separation at speed.
4. Note the crystal target the HUD shows at intensities 3 and 4.
5. Glance at frame time on the target device (intensity 3 goes ~304 → ~848 track prisms).

**PASS:** the laps list shows `3, 3, 2, 2` (empty means it silently fell back to
`optionalLaps` = 3 and the targets are wrong); you spawn behind the east circle's pole,
merge onto the track heading +Z with the first crystal ahead; targets read **56 / 54**
at intensities 3 / 4 (not 84 / 81); frame time is acceptable.
**FAIL:** an empty laps list · wrong crystal targets · spawning off-track or facing the
wrong way · a frame-time regression on device.
**Judgement call:** race length. Say whether intensity 3 runs long.

### QA-HAPTICS ⬜ — the two feels, and the silence around them

**Source:** PR #610. Reference: `Docs/HAPTICS.md`. Needs a **device or gamepad** —
haptics are a no-op on desktop without one.
1. Confirm `SquirrelImpactorDataContainer`, `SkimmerHapticsByPrismEffect`
   (`Min Strength = 0.35`) and `VesselHapticsByPrismEffect` import with no missing
   scripts.
2. Skim a run of prisms on the Squirrel.
3. Crash the vessel **body** into a prism.
4. Do both together — crash while skimming.
5. Tap UI buttons; boost; drift; joust; set off an explosion.
6. Toggle Haptics off in Settings, then move the level slider.
7. **On iOS specifically**, repeat steps 2–3.

**PASS:** a bright rapid pulse train while skimming that intensifies toward the skimmer
centre; one heavy low thud on a body crash that interrupts the train and never
machine-guns; **nothing** from UI/boost/drift/joust/explosions; the setting stops and
scales both feels; iOS plays both.
**FAIL:** silence on device during skims · a buzz on any of the silenced events ·
continuous rattling on crashes · the setting not taking effect · iOS failing to load
the skim clip.
**Report the feel:** the punish also fires when the Squirrel clips its own trail in a
tight drift — say whether that reads as fair.

### QA-PERF-DEATH-PATH 🟡 — re-profile the batched suction/explosion death path
**Source:** PR #658. Every frame-cost claim on that branch is structural, never measured.
Reference: `Docs/PRISM_EXPLOSION_BENCHMARK.md` § "Re-profiling the death path".
1. Run the 5-run `bench` with throttles lifted per the doc.
2. Record the five `Prism.Destroy.*` markers (total + self ms) and GC/frame; compare
   against a run at `f0ddfc21`.
3. Separately, **watch a cell with fauna feeding** — the grid rig produces zero
   implosions, so suction has to be observed in play.
4. Watch the convergence point of a suction as the creature moves.

**PASS:** benchmark numbers recorded (this item's deliverable is *data*, not a verdict);
suctions converge on the moving creature, animate for their full duration, and no prism
is left frozen mid-suction.
**FAIL:** a marker regressing sharply vs. the reference run · GC per frame appearing ·
suctions converging on a stale point or freezing.
*(The old ~0.43 ms self/death figure is stale — do not compare against it.)*

### QA-SPARROW-PROJECTILE-POOL ⬜ — async-refilled pooled projectiles are injected

**Source:** PR #606. The failure mode is *silent duds* seconds after spawn.
1. Spawn a Sparrow (any mode or freestyle) and fire full-auto for ~30 s.
2. Then dump several skyburst missiles.
3. Watch the Console throughout.
4. Optional: confirm `PoolRefill.Projectile*` markers still appear in the profiler.

**PASS:** every shot has launch SFX and live colliders for the whole 30 s, including
after the pools cycle through async-refilled instances; **no `NullReferenceException`
from `LaunchProjectile`**; the async refill markers still appear.
**FAIL:** any dud shot (no SFX / passes through prisms) or any NRE from `LaunchProjectile`.

### QA-RHINO-SKIMMER-SHAPE ⬜ — sword X/Z preserved, Space drives length, capsule follows the hull
**Source:** PRs #616 and #583.
1. Open `Rhino.prefab`: the ForceFieldSkimmer sits under `Rhino_Test (1)` (the fuselage),
   not `OrientationHandle`.
2. In play, pitch/yaw/roll the Rhino and watch the capsule and its collider gizmo.
3. Swap to the Rhino in freestyle and look at the blade at rest.
4. Skim prisms and watch the blade grow.
5. Collect **Space** crystals up to level 10, then take a Space debuff.
6. Watch the Rhino HUD's skimmer-scale fill.
7. Fly tight enough to clip your own just-laid trail.
8. Sanity-check a spherical-skimmer vessel (Squirrel).

**PASS:** the capsule sways with the hull instead of staying screen-fixed; the blade
keeps its thin profile at **all** times; growth is along the long axis only; resting
length grows toward 50 at Space 10 and shortens below 30 on a debuff; the HUD fill starts
at the true base and tracks growth; the Rhino cannot collide with its own just-laid trail;
the Squirrel's uniform scaling is unchanged.
**FAIL:** the blade inflating into a sphere/box · the capsule glued to the camera ·
self-collision with fresh trail · the HUD fill starting mid-bar · Squirrel scaling changed.

### QA-RHINO-RAMP-BOOST 🟡 — the ramp boost's final (inverted) direction
**Source:** PR #613 (engage/release verified mid-branch; the final inverted FOV/Panini
direction and the merged state were not). Reference: `RHINO_RAMP_BOOST.md`.
1. Hold full-speed-straight on the Rhino and watch speed climb.
2. Release and watch the return.
3. Wobble the stick mid-boost.
4. With a second client up, confirm the remote Rhino looks sane.

**PASS:** speed climbs **linearly** to ~6× over ~3.6 s; the view zooms *in* (narrower
FOV) as speed rises; release returns in ~0.5 s landing exactly on the pre-boost FOV and
Panini; no discrete "gear" steps.
**FAIL:** stepped speed · the view zooming out instead of in · FOV/Panini not returning
exactly to home · the remote client seeing something different.

### QA-TOYS-WANDERWAY-INVISIBLE ⬜ — the conveyor's transport is never watched
**Source:** PR #609.
1. Freestyle, fly the Wander toy and choose Without Ark, then fly straight for a while.
2. Hard-turn and reverse over ground you just covered.
3. Vary speed from cruise to boosted and watch the field ahead.

**PASS:** scenes only ever bloom in far ahead — never in your face; you never watch a
scene suction away in view (on a reverse the old ribbon **waits**, briefly idling, until
it has left your view); the field still holds ~7 scenes ahead at all speeds.
**FAIL:** a scene appearing close in front of you · watching a scene shrink away on
screen · the field starving (fewer scenes ahead) at high speed.

### QA-UI-TRAIL-DISPLAY-REMOVAL ⬜ — nothing broke when the vessel silhouette was deleted
**Source:** PR #634, then **#695** (the fleet-wide excision: **13 prefabs, 177 objects**
removed by YAML surgery, plus a stale-key purge across 13 more files — no Unity import
has ever run on it).
1. Open all six vessel prefabs plus the Sparrow / Rhino / Squirrel / Serpent / Manta HUD
   variants, `GameCanvas.prefab`, `GameCanvas-SkimRace.prefab` and `MiniGameHUD.prefab`.
   Look for missing-script warnings and confirm the hierarchy and HUD layout are intact.
2. Play a round on **Squirrel** and on **Sparrow** and watch the elemental petal bars.
3. Fly **Rhino / Serpent / Manta** briefly — nothing should have disappeared **except**
   the ship outline.

**PASS:** no *new* missing-script warnings anywhere; every HUD still lays out; petal bars
build, colour and animate on every vessel; the only visible loss is the silhouette.
**FAIL:** a new missing script · a HUD that lost more than the outline · petal bars
absent, mis-coloured or static.
**Known, do not fail on:** `SerpentHUDVariant.prefab` / `VesselHUDPrefab.prefab` carry a
pre-existing missing script; `Dolphin.prefab` authors no `elementBars` so its flowers are
built at runtime with a warning (fix: **Vessels ▸ Bake Elemental Petal Bars Into All
Vessel HUDs**); every vessel prefab carries harmless stale `ElementalBarsController`
keys from the `SilhouetteController` rename.

### QA-FTUE-QUEST-ROWS ⬜ — quest graphs lay out in venue rows
**Source:** PR #633 (six graph assets rewritten by script).
1. **FrogletTools ▸ Quest Graph Editor** → MainQuest → click through Phases 0–5.
2. On any phase: drag a node somewhere silly → **Layout Rows** → then `Ctrl+Z`.
3. Press **Save** once per phase.
4. Run **FrogletTools ▸ Quest Graph ▸ Layout All Phases (Rows)**.

**PASS:** every phase opens already in rows with edges intact and no node stacked at
the origin; Layout Rows re-snaps and undo restores the drag; Save produces a **no-op
diff**; the menu item reports 6 graphs / 17 rows with no further diff.
**FAIL:** nodes stacked at the origin · lost edges · a Save that rewrites the assets
substantially · an exception from either menu item.

### QA-SETTINGS-DISPLAY ⬜ — pixel-aware auto-detect + macOS fullscreen
**Source:** PR #651.
1. On **macOS**, run the player fullscreen.
2. On a high-DPI display, let auto-detect run and inspect the recommended render scale,
   AA and upscaling.
3. On a low-DPI display, repeat.

**PASS:** macOS fullscreen shows a correct borderless window (no black window, no offset
mouse, correct backbuffer); render scale is clamped 50–100 % and never supersamples;
MSAA steps down on high-DPI panels; a low-DPI panel is unaffected.
**FAIL:** a black or offset macOS fullscreen window · a render scale above 100 % ·
identical recommendations on wildly different displays.

### QA-UI-MODAL-STACK ⬜ — modals closed outside the API no longer corrupt the stack
**Source:** PR #649.
1. From the Arcade screen, open the configure modal and close it with the ✕, with the
   background tap, and with the Home nav button in turn.
2. After each close, navigate between screens and reopen a modal.

**PASS:** navigation stays responsive after every close path; reopening works; the Home
button is never left disabled.
**FAIL:** a screen that will not navigate, a dead Home button, or a modal that cannot be
reopened after one of the close paths.

### QA-STATE-RESET ⬜ — runtime game state resets to defaults between sessions
**Source:** PR #647. **Run this alongside QA-SCORING-CLIENT-MIRROR** — they are the two
halves of "nothing leaks between games", and B17 is the networked half.
1. Play a game to the end, return to the menu, and launch a **different** mode.
2. Repeat with the same mode twice (use Play Again where available).

**PASS:** the second launch starts with a clean score, intensity, player count and
domain assignment — no leakage from the previous round.
**FAIL:** any carried-over score, stale player count, or a domain that was not reassigned.

### QA-TOOLING-SHIP-PANEL ⬜ — the editor tool ship panel actually pushes
**Source:** PR #663 (buttons never pressed in a running editor). **Do this on a throwaway
branch, not on `bleeding-edge`.**
1. **FrogletTools ▸ Build ▸ Pending Tool Changes.** Confirm the branch pill shows your
   branch in green (or red + blocked on `bleeding-edge`).
2. Dirty a throwaway asset, hit **Refresh** — it should appear under *Other uncommitted
   project files*.
3. Tick it, **Push N selected**, and check the resulting commit.
4. Repeat with something else deliberately `git add`ed first.

**PASS:** the dialog lists exactly the selected path; the commit contains only that path;
a protected branch is refused; the pre-staged file **stays staged and out of the commit**.
**FAIL:** anything else riding along in the commit, a protected-branch push succeeding,
or the pre-staged file being swept in.

### QA-FLORA-LEAFSIZE ⬜ — garden flora still grow leaves at the authored size
**Source:** PR #656 (a duplicate declaration removed after a semantic merge conflict).
1. Confirm zero compile errors.
2. Freestyle → Cell Selector → **Hesperides**; look at leaf size on grown flora.

**PASS:** compiles clean and leaves grow at the authored size.
**FAIL:** compile error, or leaves that are obviously too big/small/absent.

### QA-NET-PRESENCE-PARTY 🟡 — party/presence regression pass
**Source:** PR #666 + `Docs/PartySystem/BUGS.md` (B2/B3/B5 open) +
`Docs/PresenceSystem/BUGS.md` (B4/B6 open), B12 graceful path never exercised.
Needs **MPPM with 3–4 virtual players**, and one **standalone build** for the graceful-quit case.
Procedures: `Docs/PartySystem/TESTS.md` (S-series) and `Docs/PresenceSystem/TESTS.md` (P-series).
1. Run the S-series and P-series test cases as written.
2. **B12 departure specifically**, distinguishing all three cases: graceful quit
   (in-game button / alt-F4) → expect **< 1 s**; hard kill / MPPM virtual-player
   deactivation → expect **~30–50 s** (UGS reap, correct); editor play-mode stop →
   < 1 s if the wire was reached, else reap.
3. Record the observed **fault rates** for presence reads and party-session reads over
   two independent runs (last measured: ~12 % and ~32 %).

**PASS:** B11/B13/B14 stay fixed (all instances reach `Present`; peers promote
CONNECTING… → ONLINE; no Relay 500 on boot); the graceful-quit path evicts in < 1 s;
fault rates are no worse than the last measurement.
**FAIL:** any of B11/B13/B14 recurring, a graceful quit taking the reap path, or fault
rates rising. Note B2/B3/B4/B5/B6 outcomes as data — they are known-open, so they do
not fail this item, but their current behaviour is what we need recorded.

---

## Priority 2 — lower risk, cosmetic, or data-gathering

### QA-P2-QUIT-BUTTON ⬜ — the drop-in quit button
**Source:** PR #701 (`QuitGameButton`, a self-wiring component for nested prefabs).
Place/locate one on a desktop build: it must wire itself to `Button.onClick`, quit
through `DesktopPlatformServices.Quit()` (normal shutdown — lifecycle events, state
machine, analytics flush), and be **hidden on mobile/console/WebGL** unless `desktopOnly`
is unticked. **PASS = it quits cleanly on desktop, is absent on a mobile build, and the
shutdown log shows the normal quit path.** **FAIL = a hard exit with no shutdown, a
visible button on mobile, or a listener left behind after destroy.**

### QA-P2-ANALYTICS-FLIGHT-CLOCK ⬜ — flight clock, cloud stats and vessel unlock
**Source:** the `claude/game-data-json-schema-u2mubn` merge (`FlightClock`,
`UGSDataService`, `UGSStatsManager`, `VesselUnlockSystem`, `SO_Vessel`,
`MenuCrystalClickHandler`). Fly freestyle and a game round, then check the analytics
dashboard / Cloud Save entry for the recorded flight time and stats, and confirm vessel
unlock state still resolves in the Hangar. **PASS = flight time accrues and lands in the
cloud payload, stats write, unlock state is unchanged.** **FAIL = a Cloud Save exception,
a clock that never accrues (or accrues while paused/in menu), or a vessel whose unlock
state flipped.** Reference: `Docs/Analytics/DATA_ARCHITECTURE.md`.

### QA-P2-SERPENT-SKIMMER ⬜ — Serpent's dead skimmer (known, unfixed)
Run **Audit Vessel Skimmers** and fly the Serpent through cell mass. Expected: it FAILS
the audit (inactive `VacuumSkimmer`, no impactor/container) and does not skim. **PASS =
the failure is exactly as described and nothing else regressed.** Report any *different*
symptom. Fix is tracked in `Docs/ElementalAbilitySystem/BACKLOG.md` §10–14.

### QA-P2-BENCH-LEGACY-AB ⬜ — record the legacy-CPU side of the prism A/B
Follow the cherry-pick recipe in `Docs/PRISM_EXPLOSION_BENCHMARK.md` on a `bench-legacy`
branch, then **Prism Grid Benchmark ▸ Generate Comparison Report**. **PASS = the report
exists and is attached to your results.** This item's deliverable is data.

### QA-P2-DEVICE-SOAK ⬜ — per-cell device soak
Soak each freestyle cell plus Scurry/Atlantis on target mobile hardware for ~10 minutes
each; record frame time, thermals and any hitching in
`Docs/PERFORMANCE_OPTIMIZATION.md`. **PASS = numbers recorded for every cell.** Add the
two new arenas to the sweep: **Dog Fight intensity 4** (34,654 prisms, the heaviest
party-game arena) and **Wildlife Liberation intensity 4**.

### QA-P2-CONIC-VFX-FLASH ⬜ — Dolphin cone spawn flash does not scale
Known cosmetic gap: the prefab's world-space ParticleSystem child ignores the container's
Z stretch, so the flash reads at the old length while mesh and damage reach 2400.
**PASS = confirmed still cosmetic only** (damage and mesh reach full length). Needs a VFX
tuning pass by someone at the editor.

### QA-P2-DANGLING-CELLDATA ⬜ — the project-wide dangling `cellData` GUID
`Clawfish`, `QuadFish`, `TermiteDrone`, the three `Worm*` prefabs, `oldWallFlora`, both
cytoplasm prefabs and three scenes (including `Menu_Main`) still point at a
`CellRuntimeDataSO` GUID that does not exist. Spawn each of those fauna and check for a
throw from `LifeForm.Start()` / `Flora.Plant()`. **PASS = enumerate which ones actually
throw** — that list scopes the fix branch.

### QA-P2-SPAWN-MATRIX-MOONS ⬜ — element-crystal "moons" swallowed by the toy body
Suspected pre-existing: the Spawn Matrix's four crystal moons sit ~2.2 world units out
while toys place at `toyBodyRadius = 22`. Look at the bench. **PASS = the four moons are
visible and distinct.** **FAIL = they are inside the sphere** (then the fix is a placement
value, not code).

### QA-P1-RHINO-RAMP-CEILING ⬜ — Rhino's ramp no longer gets faster with Time
The element-scaling unification removed a **fleet-wide** `Multiplier(Element.Time)` read from
`VesselTransformer.CurrentBoostAmount`, which had been multiplying the Rhino's ramp-boost
**ceiling** by up to ×2.5 on top of the wind-up rate it is supposed to scale. Nobody authored
that; the map asset, `CLAUDE.md` and `regatta_balance.py` all said Time does NOT touch the
ceiling. Now it doesn't.

Fly the Rhino in **Headlong** (its circuit race) with Time crystals collected to level 10, on a
long straight. **PASS = the ramp still SPOOLS UP visibly faster at high Time than at rest (that
half is intact), but the top speed it settles at is the same at Time 10 as at Time 0.**
**FAIL = high Time gives a higher top speed** (the read is back), or **the ramp no longer winds
up faster at all** (the wrong half was removed — `RampBoostActionSO.timeAccelerationMultiplier`).
Report how the corner/straight rhythm feels: the Rhino is ~2.5× slower flat-out at Time 10 than
it was, which is the correction, but it may want a ceiling endpoint of its own as a follow-up.

### QA-P1-SERPENT-BOOST-SPEED ⬜ — Serpent's boost is longer with Time, not faster
Same removal. The Serpent's Time was scaling boost **duration** (declared) **and** boost
**speed** (undeclared). Speed is now untouched by Time.

Fly the Serpent with Time at level 10 and again at rest, boosting on a straight.
**PASS = each boost charge lasts visibly longer at Time 10 (×1.6) while the speed it reaches is
the same as at rest.** **FAIL = the boost is also faster** (the read is back) or **the duration
no longer extends** (`ConsumeBoostActionSO.timeDurationMultiplier`).

### QA-P1-MANTA-SOAR-SPARROW-AFTERBURNER ⬜ — the two hulls that SHOULD still scale
These two legitimately used that fleet-wide read and their curves were re-authored onto their own
prefabs (`VesselTransformer.BoostSpeedMultiplier`: Manta ×1→×1.3 floored ×0.7, Sparrow
×1→×1.5 floored ×0.5). This is a regression check on the migration.

**PASS = Soar is still faster at high Time on the Manta (Redline or Regatta), and the Sparrow's
afterburner is still faster at high Time (Dog Fight or Breakwater).** **FAIL = either hull's boost
speed stopped responding to Time at all** — that means the prefab block did not deserialize, which
is the one part of this branch that had to be hand-authored into prefab YAML.

### QA-P2-ELEMENT-SCALING-REGRESSION ⬜ — the other eight migrated multipliers
All ten multipliers were proved bit-identical offline, but only in arithmetic. Spot-check the
cheapest four in play: **Squirrel** skim energy per hit rises with Charge; **Urchin** spike reach
rises with Charge; **Sparrow** turret prism z-stretch rises with Mass; **Scarab** forged ball size
rises with Space (×4 at Space 10). **PASS = each still responds to its element.**
**FAIL = any one stopped responding** — name which, because each has a different home.

---

## Not covered by this list

- **Automated CI checks** (`Tools/CI/validate_project.py`,
  `check_conditional_compilation.py`, the bleeding-edge landing guard and its follow-ups
  in PRs #683–#687, #692, #699) run in GitHub Actions and are verified there. QA does not
  need to re-run them; if a build branch is red, that is an engineering item. The one
  exception is the **player build itself** — see QA-BUILD-WINDOWS-PLAYER, which exists
  because that tier catches what neither CI statics nor the Editor can.
- **Docs-only branches** (#653, #623, #666's doc half, #697) — nothing to run.
- **Reverts** (#670, #682) — restore a previous tree; nothing new to exercise.
