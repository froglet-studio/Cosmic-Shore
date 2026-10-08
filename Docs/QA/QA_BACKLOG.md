# QA Backlog — untested development on `bleeding-edge`

**Generated:** 2026-10-05 (arcade/arena matrix pass), **refreshed 2026-10-06** for PRs #971–#976,
**refreshed 2026-10-08** for the party cards and the round-3 PRs
· **Scan covers:** PR bodies for merges up to `3ba8ea1d2` (PRs #583–#956, the 2026-10-05 refresh
in PR #961), plus a full sweep of every launchable Arcade and Arena card, its mode doc's
verification section, and the per-vessel entries in `Docs/UNITY_VERIFICATION_CHECKLIST.md`,
against `bleeding-edge` at `bf0015838`; **plus** PRs #971–#976 at `71d67ba9b` (six new Block P
items); **plus**, at `1a437696a`, Block J (the two party cards, never tracked before) and
Block Q (seven items from merged PRs #998, #1001, #1002, #1004, #1005 and #1007, read from each PR's
"Needs Editor verification" section). PRs #964–#970, #980, #983, #985–#996, #999, #1006, #1009 and
#1010 are merged too but are **not** itemised yet: the "Known, do not fail on" lines that round 3
made untrue are corrected below, and their own checks wait for the next full `/qa-backlog` scan.
· **Owner of this file:** the `/qa-backlog` skill — do not hand-edit.

> **The 11 parallel PRs from the same 2026-10-05 audit have all merged (#964–#976, 2026-10-06).**
> Six of them now have their own items in **Block P** at the end of Priority 0: the Urchin HUD,
> hull ability rows, Urchin AI, the fleet AI boost, Hijack replication and toasts. The mode items
> above Block P no longer excuse those gaps: a missing Urchin ability row, a missing pop-up
> message where a mode now has one, or a missing objective arrow is now a real failure.

**Why this list is laid out in blocks.** The audit found that of 31 launchable arcade/arena
cards, **16 had never been opened in Unity** and **11 more had only a partial pass** (one rung,
one playtest, or a UI-only pass); only Skim Race, Joust, Scurry and Astro League count as
played. The previous list covered six of those modes. Every never-run or partly-run mode now
has its own item, and each hull's open vessel checks sit next to the modes that hull flies, so
**one Editor session clears one block**. Blocks are ordered by how many players reach them:

| Block | What | Why here | Items |
|---|---|---|---|
| 0 | Gates + the whole-roster smoke test | If these fail, nothing below can be judged | 11 |
| A | Scarab hull + Scarab Scramble, Tollway, Wrecking Ball, Undertow | Unlocked for every player (`isLocked: 0`) | 5 |
| B | Butterfly hull (fold feel) + Waystation, Dustup, Tapestry, Sirocco | Unlocked for every player; never opened at all | 5 |
| C | Arena: Regatta, Broadside, Brood Rush, Astro League rework, pilot swap | Every card seats the starter Squirrel | 5 |
| D | Maelstrom tournament | Starter hull; strings blocks A–C together | 1 |
| E | Sparrow hull (13 red entries) + Dog Fight, Salvo, Breakwater, Wildlife Liberation | On 11 cards, the most in the fleet | 14 |
| F | Dolphin hull (10 red entries) + Rampage rungs 1–3, The Bends, Switchback | 7 cards; its forest is shared by four modes | 9 |
| G | Rhino: Cleave, Headlong | 2 hull-locked cards | 2 |
| H | Manta: Redline, Bloomrush | 2 hull-locked cards | 2 |
| I | Urchin hull + Skein, Hijack | Least finished; its HUD, AI and Hijack sync merged 2026-10-06 (Block P) | 3 |
| J | Party cards: Multiplayer Freestyle, Online Duel for the Cell | Never opened or tracked; on no live roster; two players needed | 2 |
| P | The parallel PRs' own Editor checks (#971–#976) | Merged 2026-10-06, never opened in Unity | 6 |
| Q | The round-3 PRs' own Editor checks (#998, #1001, #1002, #1004, #1005, #1007) | Merged 2026-10-08, never opened in Unity; three need two players | 7 |

Priority 1 and 2 (platform, ecology, toys, UI, the other vessels) follow unchanged.

Every item below landed on a shared branch **without ever being opened in Unity**
by its author (or was play-tested only in part). Work top-down: Block 0 first, then the blocks
in order.

**How to report:** use **FrogletTools ▸ QA ▸ QA Session** in Unity (or copy
`RESULTS/TEMPLATE.md` → `RESULTS/<date>-<tester>.md`), fill the table, submit. Full workflow:
`Docs/QA/README.md`.

**Status key:** ⬜ never run · 🟡 partially confirmed · 🔴 failed, awaiting fix
· ⛔ blocked. Items that PASS leave this file (→ `ARCHIVE.md`).

**Words used in many items:** the *Arcade screen* and *Arena screen* are the two game-picker
screens off the main menu; *intensity* is the 1–4 row on a card's launch panel; *Multiplayer
Play Mode* is **Window ▸ Multiplayer ▸ Multiplayer Play Mode**, which opens a second player
window; *toasts* are the short pop-up messages a mode shows during play; the *objective arrow*
is the on-screen arrow pointing at what to do next.

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

## Priority 0 — Block 0: gates. Run these first; nothing below matters if they fail.

One Editor session for everything except the two **player builds** (Windows, iOS), which need a
build machine and can run in parallel. QA-ARCADE-ROSTER-SMOKE is the cheapest check on the whole
list — run it first in every session that will touch a mode block, on the build you are testing.

### QA-ARCADE-ROSTER-SMOKE ⬜ — every Arcade and Arena card opens, launches and forces the right ship
**Source:** the arcade/arena audit of 2026-10-05 (29 cards on the two live rosters; 16 say
in their own docs they were never opened in Unity, 11 more were played only in part). This
is the cheapest possible check across the whole roster and it runs before every mode block
below: a card that fails here makes its block unrunnable, so report it and skip that block.
Rosters: `_SO_Assets/Games/GameLists/ArcadeGames.asset` (25) and `ArenaGames.asset` (4).

1. Open `Menu_Main` and press Play. Wait for the main menu.
2. Open the **Arcade** screen. Count the cards, scrolling to the very bottom. There must be
   **24 cards in the grid** plus the wide **Maelstrom banner** (Maelstrom is not a grid card). Scroll to the
   last card and click it.
3. For each Arcade card, open it, set **intensity 1**, launch with the default lobby (you +
   AI), and wait until the countdown ends and you can fly. Look at your own ship, then
   press **Esc** and return to the menu. Write down any card that does not reach "fly".
   The ship must be: **Squirrel** for Skim Race and Joust; the ship you picked (Sparrow, Manta
   or Squirrel) for Scurry; **Dolphin** for Rampage, The Bends and Switchback; **Rhino** for
   Cleave and Headlong; **Sparrow** for Wildlife Liberation, Dog Fight, Salvo and Breakwater;
   **Scarab** for Scarab Scramble, Tollway, Wrecking Ball and Undertow; **Urchin** for Hijack
   and Skein; **Manta** for Bloomrush and Redline; **Butterfly** for Waystation, Dustup,
   Tapestry and Sirocco.
4. Open the **Arena** screen. There must be **4 cards**: Astro League, Brood Rush, Regatta,
   Broadside. For each: open it, step through the ship picker, pick one ship, press the
   select button under the ship picture, launch at intensity 1, fly for a moment, return.
5. Back on the Arcade screen, click the **Maelstrom banner** and confirm its lobby scene loads (the full run is
   QA-MAELSTROM-RUN).
6. Keep the Console open throughout and copy any red error that names a card or a scene.

**PASS:** 24 Arcade cards are drawn and the last one opens; every card reaches "fly" with
the ship listed for it in step 3; the Arena screen shows 4 cards and each launches with the
ship you picked; the Maelstrom lobby loads; no red Console error names a mode, controller
or scene.
**FAIL:** a card missing from either grid · a card that does nothing when clicked · a
launch that hangs on the connecting panel or the countdown · the wrong ship spawning · a
red error naming a mode, controller, scene or `Missing (Mono Script)`.
**Known, do not fail on:** the Butterfly, Manta, Urchin, Scarab and Serpent are silent or nearly
silent. (The Urchin now HAS ability icons, and the modes now have their pop-up messages and
arrows - those are checked in Block P and the mode items, not here.)

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

### QA-IOS-BUILD ⬜ — an iOS build compiles and reaches the main menu
**Source:** PR #946 (Cosmic Shore bundle ids, a free unsigned-ipa build, the first iOS
compile fix). **Why a gate, and why separate from the Windows build:** iOS is a fresh
IL2CPP target that has never produced a running build; a platform-only compile error is
invisible in the Editor and on the Windows player.
1. Produce an **iOS build** (or take the unsigned ipa this branch's CI produced). Record
   whether the build itself completes.
2. Install and launch on an iOS device. Sign in and reach `Menu_Main`.
3. Read the device log afterwards.

**PASS:** the build completes; the app launches, signs in and reaches `Menu_Main`; no
managed exception in the device log that does not also appear in the Editor.
**FAIL:** a build that fails to compile or link for iOS · an app that closes before the
menu · an iOS-only managed exception. Attach the build log / device log for any failure.
**BLOCKED is fine** if no iOS device or signing path is available — say so.

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

## Priority 0 — Block A: the Scarab (unlocked for every player) — the hull and its four modes

The Scarab is unlocked for every player (`isLocked: 0`) and carries four Arcade cards. One
session: the vessel item first (if the Scarab will not spawn, the four modes cannot run).

### QA-SCARAB-VESSEL ⬜ — the Scarab hull, its moves, and its ring, from the ground up
**Source:** `Docs/UNITY_VERIFICATION_CHECKLIST.md` 🔴 "Scarab vessel foundation" (new
`VesselClassType 12`, prefab cloned outside the editor), 🔴 "Scarab hull + puppetry +
elemental morphs", 🔴 "Scarab analog juke + mirrored cavitation plate" (three mechanics
retired: grapple, reverse modifier, phase grab), 🔴 "Scarab switch — interior fill removed".
The Scarab is unlocked for every new player and carries four modes, so this is the
highest-impact vessel item. Reference: `_Scripts/Controller/Vessel/R_VesselActions/SCARAB.md`.

1. Start `Menu_Main`. Use the **Vessel Changer** toy and pick the Scarab. If it shows as a
   plain sphere and nothing spawns, stop: open `_SO_Assets/Vessel Prefab Container.asset`,
   look at slot 7, and report whether it reads `None (Transform)`.
2. Enter freestyle (click the centre of the screen, or press **Y** on a gamepad). Look at the
   ship from the side while it sits still.
3. Fly hard turns left and right, speed up, slow down.
4. Push the **right stick** sharply to one side (the juke dash). Then fly a hard turn
   immediately after it.
5. Hold the **left trigger** fully down (on keyboard: the drift key) and fly normally for
   10 s. Nudge the right stick partway while holding it.
6. Hold **B** on a gamepad (or **R** on keyboard) and bump a ball in Scarab Scramble or
   freestyle if one is near.
7. Press the Mass ability (place a ring). Look through the ring's middle.
8. Use the Element Charger toy (or crystals) to raise one element from 0 to 10, then
   another. Watch the hull.
9. Open `Scarab.prefab` in the Project window and read the Inspector top to bottom.
10. If you can run two players (Multiplayer Play Mode, 2 players), fly a Scarab on each and
    juke near each other.

**PASS:** the Scarab spawns and reads as a beetle — domed shell with a seam, a forward horn,
six legs — with the team colour on the shell and horn, not the belly; side-on at rest the
belly closes with the shell (no gap of daylight, no parts poking through); turning opens the
wing cases (wider on the outside of the turn), legs tuck at speed and splay when slow, the
horn swings against the nose, antennae lag and settle; the juke spins the whole visible ship
360° and the bank into turns comes back straight after; holding the left trigger changes
nothing (no twitch, no flipped plate) and a partial right-stick nudge works as normal;
holding B/R does nothing special to a ball (an ordinary bounce); the ring blooms with an
empty middle and reads larger than before; each element glides the hull into a new shape
over about a second (never a snap) while the puppetry keeps moving; the prefab shows no
`Missing (Mono Script)`; a remote Scarab's drift pose and juke look the same on both
machines.
**FAIL:** a sphere or nothing in the Vessel Changer · the Sparrow's model visible under the
beetle · team colour on the underside · a visible gap between belly and shell · a rigid
hull that never animates · a juke that does not spin, or a bank that never comes back ·
any visible effect from holding the left trigger or B/R · a disc of prisms filling the ring
· a morph that snaps · a missing-script row.
**Known, do not fail on:** the silhouette is a placeholder ("a low-poly scarab, not a
floating-parts spaceship") — do not file the look itself. The Scarab's four ability icons are
white **placeholder** outlines (a blast, a ring, a ball, a dial) waiting for final art; they
must NOT be the Sparrow's missiles/bullet/boost pictures any more (QA-HULL-ABILITY-ROWS). The juke has no sound (`jukeWhooshEvent` is
empty on purpose). Raising Mass to 5 does nothing visible to the ring ("Armored Switch" has
no new home yet).
**Report a number:** select the Scarab's hull object (`SparrowModel1`) and write down its
**world scale** from the Inspector — the blast plate is sized from it.

### QA-SCARAB-SCRAMBLE-MODE ⬜ — "Scarab Scramble" has never been opened
**Source:** `Docs/UNITY_VERIFICATION_CHECKLIST.md` 🔴 "Scarab Scramble — the Scarab-only
hoop-court party mode" (`claude/scarab-party-game-pxe569`). `GameModes.ScarabScramble = 43`.
The forge gate is now installed by nobody (unverified). AI Scarabs juke since PR #1002 (checked in
QA-SCARAB-SCRAMBLE-AI-JUKES). Reference: `_Scripts/Controller/Arcade/SCARABSCRAMBLE.md`
§ In-editor verification.

1. Open `MinigameScarabScramble.unity`. Select the `Game` object and confirm it carries
   `ScarabScrambleController` and `ScarabScrambleGoalTurnMonitor` with no
   `Missing (Mono Script)`.
2. Launch from the Arcade card at intensity 1, you plus AI. Watch the start: a court sphere
   about 480 across blooms in the middle, **four rings** bloom in facing the centre,
   crystals appear inside the court.
3. Fly your ship's front sphere (the skimmer) through a **bright white** crystal. It must
   turn into a ball of your colour, where it was, at rest. Then fly through a **coloured**
   (elemental) crystal: it must be collected normally, not turned into a ball.
4. Push your ball through any ring, from either side.
5. Push an **enemy's** ball through a ring. Then touch it yourself and push it through again.
6. Juke (right stick, sharply) into an enemy ball. Then bump one without juking.
7. Bank a ball off two or more walls into a ring.
8. Get four or more balls loose in the court at once.
9. Play to the end (first team to **10**). Press Play Again.
10. With two players (Multiplayer Play Mode), repeat steps 3, 4 and 6 from the second
    player's window.
11. Stay in a match long enough for trail to pile up in the court and watch the court edge.

**PASS:** the scene opens clean; the court, four rings and crystals appear; a white crystal
becomes your ball and a coloured one does not; the objective arrow points at your ball; a
ball through a ring flares the ring, detonates the ball, shows `{name} rings one home -
n/10`, and moves your team's score; an enemy ball scores nothing until its owner touches it
again; a juke converts an enemy ball to your colour and a plain bump never does; a bank shot
shows `BANK x{n}`; a fourth loose ball detonates **all** balls at once with a court-wide
message on every machine; the match ends at 10 with a winner banner and Play Again reloads;
the second player sees balls at the right size and colour, their goals count on both
machines, and their juke steals; creatures pour over the court wall and graze once enough
trail piles up.
**FAIL:** missing scripts · no rings or no crystals · an elemental crystal turning into a
ball · an enemy ball scoring for you · a plain bump stealing · balls at the wrong size on the
second machine · a match that never ends or Play Again that does nothing.
**Known, do not fail on:** intensity changes nothing in this arena (single cell by design).
**Report:** whether AI Scarabs juke-steal balls in a real match (PR #1002 added it; the ratio of
steals to plate knocks is the number QA-SCARAB-SCRAMBLE-AI-JUKES asks for).

### QA-TOLLWAY-MODE ⬜ — "Tollway" has never been played end to end (GameModes 48)
**Source:** PR #848 + the replicated-press and plant-anchoring passes. Scarab-only toll race:
plant your ring on a plant, then drive a ball through it. **The first playtest found the
human could not plant a single ring** (the AI could) — the plant-snap rework that followed
is unplayed. First shipped user of `FloraNetworkSync`. Reference:
`_Scripts/Controller/Arcade/TOLLWAY.md` § In-editor verification (18 steps; this is the
short form).

1. Open `MinigameTollway.unity`. The `Game` object carries `TollwayController` and
   `TollwayTollTurnMonitor`, and the controller's `settings`, `rule`, `arenaCell` and
   `cellData` fields are all filled.
2. Launch at intensity 1 with AI. Find the plants inside the court: there should be **14**,
   each with a visible coloured crystal, inside the court wall and clear of the middle.
3. Relaunch at intensities 2, 3 and 4 and look at the plants each time.
4. Back at intensity 1: press the place-ring button (**A** on a gamepad) in open space, away
   from any plant.
5. Fly at a plant and press it — once from close, once from about 150 units, once from about
   200 units. Approach the same plant from two directions.
6. Press again at the same plant.
7. Watch the Mass ability icon after a successful press for one minute. Press during that
   minute.
8. When it is ready, plant a second ring on a different plant. Watch the first ring.
9. Fly through a bright white crystal to make a ball, then push the ball through **your**
   ring. Then knock a ball through an **enemy** ring.
10. Watch an AI team for two minutes.
11. Play to the end (first team to **4** tolls).
12. With two players (Multiplayer Play Mode): compare where the 14 plants are on both
    windows, plant a ring from the second player, and score a toll from each.
13. Regression: in freestyle and in Scarab Scramble, place a ring in open space.

**PASS:** 14 plants inside the court at every intensity, and the species visibly changes
between intensities (a pillar, a plate colony, a squat cactus, a needle cage); pressing in
open space does nothing, spends nothing, and shows "No plant on this line — fly at one and
plant your ring"; a press at a plant from all three distances plants a ring centred on the
plant's crystal and facing the way **you** flew; a second press on the same plant is
refused; a radial veil covers the Mass icon and empties over 60 s, and presses during it do
nothing; planting a second ring shrinks the first away; a ball through your ring makes the
ring vanish, a scarab-wing platform rise, shows `{name} collects a toll - n/4`, moves your
score and refills your meter instantly; a ball through an enemy ring scores for **them**;
the ball flies on after a toll; the AI plants and escorts balls and can reach 4 on its own;
the plants sit in the same places on both machines; freestyle and Scarab Scramble still
place a ring in open space.
**FAIL:** fewer than 14 plants, plants outside the court wall, or the same species at every
intensity (a grown-nothing plant means a broken flora reference) · being unable to plant
from any distance · a ring planted off the plant · two of your rings standing at once · a
toll scoring for the wrong team · plants in different places on the two machines (if this
fails, the rest of the multiplayer steps are meaningless — stop) · freestyle ring placement
broken.
**Known, do not fail on:** the cell's flora families cover only 3 of 5 growth families (a
generator check that fails offline; not visible in play).

### QA-WRECKING-BALL-MODE ⬜ — "Wrecking Ball" has been played once but never measured
**Source:** PR #879. Scarab bowling through a forest; compiled and played once by its
author ("an excellent start"). The volume ladder and target (**1500** prisms) are estimates.
Reference: `_Scripts/Controller/Arcade/WRECKING_BALL.md` § Verification.

1. Launch Wrecking Ball at intensity 1 with one AI. Look around before moving.
2. Fly through a bright white crystal to make a ball, then bowl it into a stand of trees.
   Watch your score row.
3. Hit a ball an **AI** made into the trees.
4. Flick the right stick beside the forest (the cavitation plate).
5. Watch the AI for a minute.
6. Run **FrogletTools ▸ Ecology ▸ Measure Cell Environment Baselines** and write down the
   intensity-1 volume.
7. Play a full round at intensity 1 and time it. Then launch intensities 2, 3 and 4 briefly.
8. Regression: in Astro League and Scarab Scramble, a ball still bounces, scores and can be
   stolen.

**PASS:** a court about 720 across with the forest standing **inside** it and crystals inside
the court, pilots starting on a ring just outside; your ball's kills raise your score, and
kills by a ball an AI made credit **you** when you hit it; the plate raises the score; the
AI fetches crystals, bowls and dashes near the forest; the forest's peak creature frenzy does
not start immediately on boot; the round ends at 1500; the three regression modes behave
as before.
**FAIL:** a forest outside the court wall · ball kills scoring for nobody · an AI-made ball
crediting the AI after you hit it · frenzy on the first frame · a round that cannot reach
1500.
**Report numbers:** the intensity-1 baseline volume (the model expects about **396,000**; far
off means the ladder needs re-deriving) and the round time at intensity 1. Say whether the
forest reads too sparse or too dense.

### QA-UNDERTOW-MODE ⬜ — "Undertow" has been played once but never measured
**Source:** PR #879. Scarab dash duel in Wildlife Liberation's cages; compiled and played once
by its author. The AI's dash ranges come from geometry and were never measured; the
per-player score card shows bends only. Reference: `_Scripts/Controller/Arcade/UNDERTOW.md`
§ Verification.

1. Launch Undertow, two seats, one AI. Look around before moving.
2. Dash (right stick, sharply) beside the AI so the blast catches it. Watch its element
   flowers and your team's score row.
3. Dash through a swarm of creatures.
4. Watch the AI for a minute.
5. Play to the end (**12** points).
6. Regression: in Scarab Scramble, dash through the cleanup creatures.

**PASS:** Wildlife Liberation's cages and swarm around you, pilots on a ring about 1150 out,
crystals inside about 480; a dash that catches the AI drops its petals **and** adds **3**
to your team; each creature killed by a dash adds **1** (its heart is freed and the body
unravels); the AI chases you and dashes when close; the round ends at 12 with a winner; in
Scarab Scramble a dash now kills creatures (scoring nothing there).
**FAIL:** a dash that debuffs but does not score (or the reverse) · creatures scoring per
prism instead of per creature · an AI that never dashes · a round that never ends.
**Known, do not fail on:** your own score card counts bends only, not creature kills (the
team total is correct).

## Priority 0 — Block B: the Butterfly (unlocked for every player) — the hull and its four modes

Also unlocked for every player, and the newest hull: nothing in this block has ever been opened.
One session: the vessel item first.

### QA-BUTTERFLY-VESSEL ⬜ — the Butterfly, a whole new vessel, has never been flown
**Source:** PR #916 (+ #925 fold gates, the 2026-09-25 "one reach" fold and the
2026-09-28 seamless-transit pass). A brand-new hull, unlocked for every player, with a two-mode right
trigger, a fold ability, and four games. Authored headless — **never compiled or flown**; the
fold's look and feel is explicitly "NOT verified". References:
`_Scripts/Controller/Vessel/R_VesselActions/BUTTERFLY.md` § 8 and `BUTTERFLY_FOLD.md`.

1. Let the project compile; watch the Console on first load of `Menu_Main`.
2. Freestyle as the **Butterfly**. Pull the right trigger once: exactly **one** mode flip.
   Mass mode lays a **wide wake**; Dust mode shows **falling motes in your domain's
   shielded colour** under the hull.
3. Change your domain with the Domain Changer toy — the motes must recolour.
4. In Dust mode, pass over **your own** mass vs **opposing** mass.
5. **Hold the left trigger** (the fold): the ship stops, the wings close and a ghost of the
   ship appears ahead along your heading. Hold for 5 s. Release.
6. Press the fold again immediately, and watch the Time ability icon.
7. Look at the two rings the fold leaves behind. Look **through** one before flying it.
   Fly through it.
8. Fly outside the cell's outer wall and fold.
9. With a second player (Multiplayer Play Mode, 2 players), fold on one machine and watch
   it on the other; then have the second player fly through your gate.

**PASS:** compiles; one trigger pull = one mode flip; Mass lays a wide wake and Dust shows
motes in your team's shielded colour that recolour on a domain change; Dust grows/arms/
shields your own mass and destroys/shrinks/steals opposing mass; holding the fold stops the
ship and shows a ghost ahead, nothing piles up where you stopped, and release moves you to
the ghost at your previous speed; a second press during the veil does nothing; the fold
leaves two linked rings, each ring shows the world on the **other** side, and flying through
carries you out of the other with no visible jump or camera cut; outside the wall the ghost
reaches further the longer you hold; the other machine sees the ship stop with its wings
shut and arrive at the same place, and the guest's transit through a gate works.
**FAIL:** a compile error · more than one flip per pull · motes in the wrong colour or not
recolouring · Dust treating own and opposing mass the same · no ghost, or prisms piling up
during a hold · a fold that teleports you straight back · a ring that shows the world behind
it, or a one-frame jump on transit · a guest not teleporting.
**Known, do not fail on:** pressing **A** on the gamepad in menu freestyle makes the camera
stop following the vessel (logged, unresolved — a separate bug). The Butterfly makes **no
sounds** (its sound slots exist since PRs #966 and #1001 but are all empty). Its ship icons are placeholders and its class icon
is empty. The ghost is an opaque copy of the hull (a translucent one is intended later).
**Report the feel — this is the point of the item:** say whether the fold's stop, the ghost's
bloom, the wither/re-appear and the gate transit read well or feel slow, and whether
teammates being carried off by a gate they flew into by accident is a problem.

### QA-WAYSTATION-MODE ⬜ — "Waystation" (the Butterfly's race) has never been opened
**Source:** PR #916. `GameModes.Waystation = 58`, ring clusters laid a fold apart. The course
C# was compiled and run offline and agrees with the model gate for gate; the controller, the
AI fold drive and the scene have had **no type check**. Reference:
`_Scripts/Controller/Arcade/WAYSTATION.md`.

1. Launch Waystation from the Arcade card at intensity 1, you plus AI.
2. Fly the first cluster of rings (a coil), in order. Watch the score row and the lime
   next-ring highlight.
3. Line up on the cluster's last ring (the exit gate, which faces the next cluster), thread
   it, then fold (hold and release the left trigger) toward the next cluster.
4. Fold **across** a ring you have not threaded.
5. Watch an AI Butterfly through two clusters.
6. Play to the end (**24** rings). Then launch intensity 4 and compare.

**PASS:** rings come in coils with a separate exit gate per cluster; threading in order
counts and moves the lime highlight; threading the exit gate well lines you up to fold onto
the next cluster; a fold that passes through a ring does **not** count it; AI Butterflies
progress through clusters on their own; the race ends at 24 with a winner; intensity 4's
course is visibly harder.
**FAIL:** a course that does not build · a ring counted by a fold passing through it · AI
that never leaves the first cluster · a race that never ends.
**Known, do not fail on:** silence (the Butterfly has no sounds); the preview window on the
card shows an empty shell; there are only two pop-up message types (no milestones).
**Report:** whether AI Butterflies fold at sensible moments (the fold drive is untuned).

### QA-DUSTUP-MODE ⬜ — "Dustup" (the Butterfly's dust duel) has never been opened
**Source:** PR #927. `GameModes.Dustup = 59`, in Dog Fight's Boneyard. Authored headless — **no
type check** on the controller, rule or the shared `ButterflyAutopilotModeDriver`. Target
(**10** dustings) is unmeasured. Reference: `_Scripts/Controller/Arcade/DUSTUP.md`.

1. Launch Dustup at intensity 2 with at least one AI rival.
2. Watch the AI Butterflies for 20 s after the countdown.
3. Flip to Dust mode (right trigger), get **above** a rival and fly through them with the
   motes under your hull. Then hover over them without breaking off.
4. Fly **at** a rival nose-first in Dust mode, so your body (not the motes) meets them.
5. Dust a **teammate** (2v2).
6. Play to the end and time the match.

**PASS:** AI Butterflies switch into Dust mode on their own and hunt from above; one pass
through a rival scores **1** and drains their element flowers for a few seconds; hovering
scores only the pass that caught them; a nose-first hit scores nothing; a teammate is never
dusted or scored; the match ends at 10 with a winner.
**FAIL:** AI that never enter Dust mode · a pass that does not score · hovering scoring
repeatedly · a teammate scored · a match that never ends.
**Known, do not fail on:** an AI that spawns stopped does not switch modes until it moves;
silence; the card's preview window has no rivals to score on.
**Report:** match length, and whether the AI's height above its target looks right.

### QA-TAPESTRY-MODE ⬜ — "Tapestry" (the Butterfly's painting war) has never been opened
**Source:** PR #927. `GameModes.Tapestry = 60`, a **timed** (150 s) mode scored on standing
prism volume (the new `VolumeRemaining` metric). Barren cell, no creatures. Authored
headless, no type check. Reference: `_Scripts/Controller/Arcade/TAPESTRY.md`.

1. Launch Tapestry at intensity 1 with AI. Count the crystals at the start roughly.
2. Fly broad arcs in Mass mode for 20 s. Watch your score.
3. Collect a Mass crystal and fly again — compare the wake's width.
4. Flip to Dust mode and fly over a rival's painting. Watch both scores.
5. Fly Dust mode over **your own** painting.
6. Let the clock run out. Read the final scores.
7. Launch intensity 4 and count crystals again.

**PASS:** your score rises while you paint and the wake widens after a Mass crystal; raiding
a rival lowers their score and (on a steal) raises yours; dust on your own paint grows or
shields it rather than destroying it; the round ends at 150 s and the highest standing
volume wins; intensity 1 has about 24 crystals and intensity 4 about 6; no creatures appear.
**FAIL:** a score that never moves · a raid that does not lower the rival · own paint
destroyed by your own dust · a round that does not end on the clock · creatures in the cell.
**Known, do not fail on:** ties resolve in team order; silence.
**Report:** whether six-figure scores fit in the score columns or get cut off.

### QA-SIROCCO-MODE ⬜ — "Sirocco" (the Butterfly's erosion race) has never been opened
**Source:** PR #927. `GameModes.Sirocco = 61`, in Rampage's cactus forest; first team to
destroy **600** hostile prisms. Authored headless, no type check; 600 is a guess. Reference:
`_Scripts/Controller/Arcade/SIROCCO.md`.

1. Launch Sirocco at intensity 1 with AI.
2. Flip to Dust mode and skim low over a stand of a **rival** colour. Watch your score.
3. Pass back over the same stand twice more.
4. Skim over a stand of **your own** colour.
5. Reach a rival plant's heart with the dust. Watch your element flowers.
6. Watch the AI for a minute.
7. Play to the end and time the match. Note how much forest is left.

**PASS:** passing over rival mass destroys some prisms (score rises), shrinks or steals others;
repeat passes erode a stand further; your own colour grows, arms or shields and never scores;
reaching a heart withers the plant and collects the heart for you; AI fly erosion runs over
dense rival mass; the match ends at 600 with forest still standing.
**FAIL:** dust that never scores · own-colour mass scoring or being destroyed · a heart that
is not collected · AI that never dust · a match that strips the forest bare before ending.
**Known, do not fail on:** silence. (Sirocco now HAS an objective arrow, added 2026-10-06: it
points at the densest standing forest of another team's colour, never at the crystal. A missing
arrow, or one pointing at the crystal, is a failure.)
**Report:** match length at intensity 1 (600 is unmeasured).

## Priority 0 — Block C: the Arena screen — four multi-hull cards and the pilot swap

Every Arena card seats the Squirrel (unlocked from the start), so these are reachable by every
player. One session; Multiplayer Play Mode with 2 players helps for the replication checks.

### QA-REGATTA-ARENA ⬜ — Regatta, the first ARENA mode, has never been run as a race
**Source:** PR #880 (+ the `/arenagame` surface). An **arena** race any hull can enter — a new
launch screen and a new roster. Never run in the Editor apart from two **UI-only** playtests
on 2026-09-15 (which fixed the carousel offering two hulls, the Scarab wearing the Sparrow's
icon, the select button sitting on Play, and the Urchin drawing as a white square). The pure
course code was compiled and run for real; the controller and the starting-element plumbing
were only inspected. Reference: `_Scripts/Controller/Arcade/REGATTA.md` § 8.

1. Compile; run the edit-mode tests (`RegattaCourseTests`, `VesselStartingElementsTests`,
   `ArenaRosterTests`) from **Window ▸ General ▸ Test Runner**.
2. Open the **Arena** screen (not the Arcade screen) and pick **Regatta**. Step through the
   ship picker: **all eight hulls** with correct pictures (check the Scarab and the Urchin
   in particular). The select button sits under the ship picture, not over Play; Start
   stays disabled until it is pressed.
3. Launch at intensity 1: **three team-coloured rails braided through eight rings**.
4. Fly an **Urchin**: grind your own colour's rail (it should latch and run at about 300).
   Then fly a **Squirrel** along a rail and watch its boost gauge fill.
5. Look at the element flowers on screen: a Manta (handicapped) shows **fire** petals, a
   Sparrow (helped) shows **white** petals.
6. With a party guest (Multiplayer Play Mode), compare one hull's element levels on both
   machines.
7. Race to the end (**24** rings). Return to `Menu_Main` and watch the menu ship.
8. Relaunch at intensities 2, 3 and 4 and look at the course.

**PASS:** the tests pass; all eight hulls in the picker with correct pictures; the select
button is under the ship and Start waits for it; the braided course builds at every
intensity; the Urchin grinds its own rail and the Squirrel's gauge fills on one; flowers show
the seeded handicap/help; host and guest agree on levels; the race ends with a winner and the
menu ship starts at rest.
**FAIL:** a failing test · a hull missing or drawn as a white square · the select button
over Play · a course that does not build · flowers that ignore the seeded levels · host and
guest disagreeing.
**Known, do not fail on:** hull speeds still spread about 5–6× even with the seeded elements.
(AI Sparrows, Serpents, Dolphins and Scarabs now boost on the straights - QA-AI-BOOST-POLICIES
checks that and records lap times; the Urchin now shows its ability icons.)

### QA-BROADSIDE-ARENA ⬜ — "Broadside", the seven-hull brawl, has never been opened
**Source:** `GameModes.Broadside = 57`, in Dog Fight's Boneyard. Authored headless with **one
playtest fed back**; every hit is priced by its verb (Round 1, Strike 8, Debuff 12, Rocket
10/20/30). Three hull weapons were newly wired to report hits (Rhino sword, Squirrel joust,
Urchin spikes). First to **100 points per pilot** on the team wins. Reference:
`_Scripts/Controller/Arcade/BROADSIDE.md`.

1. Open the **Arena** screen, pick **Broadside**. Confirm the picker offers **seven** hulls
   (Sparrow, Urchin, Rhino, Squirrel, Dolphin, Scarab, Manta) and **not** the Serpent.
2. Launch at intensity 1 against AI. For each hull you can, land its weapon on a rival and
   watch your team's score:
   - **Sparrow:** guns (+1 per hit), a rocket (+10, +20 or +30).
   - **Urchin:** tap the spike trigger at a rival (+1, and the rival spins).
   - **Rhino:** energise the sword and fly the blade through a rival (+8).
   - **Squirrel:** joust a rival by overtaking them (+8).
   - **Dolphin:** collect a crystal and catch a rival in the cone (+12).
   - **Scarab:** dash beside a rival (+12).
   - **Manta:** catch a rival in a bloom (+12).
3. Hold the Rhino's blade on one rival for a whole pass.
4. Watch the AI for two minutes: who lands hits, and who never does.
5. Play to the end.

**PASS:** seven hulls, no Serpent; every hull's weapon scores its verb's price exactly once
per landed hit; a blade held through a rival scores once per about 1.4 s, not every frame;
the AI of every hull lands at least some hits; the round ends on the team target with a
winner.
**FAIL:** a hull whose weapon passes through a rival and scores nothing · any hit scoring a
different price than listed · a Rhino blade scoring every frame · the Serpent offered.
**Known, do not fail on:** the Dolphin is the weakest seat (its cone needs a crystal); the
Rhino's sword drains no elements.
**Report:** which hulls feel strongest and weakest, and whether each AI's weapon reads as a
real attack (the Urchin's spike tap in particular).

### QA-BROODRUSH-ARENA ⬜ — "Brood Rush" has no recorded test pass
**Source:** `GameModes.BroodRush = 38`, the nucleus-control arena mode where creature waves are
the score clock. Its doc gives **no status line**, and it moved to six arena seats (one pilot
per hull) on the cell-relative spawn ring. Creature waves are local to each machine. Reference:
`_Scripts/Controller/Arcade/BROODRUSH.md`.

1. Open the **Arena** screen, pick **Brood Rush**. Confirm the picker offers six hulls
   (Squirrel, Manta, Dolphin, Rhino, Serpent, Sparrow).
2. Launch 1v1 (you + AI) and watch the spawn: everyone on a ring around the cell, nobody
   overlapping.
3. Fly through the cell's core (the nucleus) and lay trail inside it. Leave the AI alone.
4. Wait for the first wave (about **30 s** after the countdown). Watch the score and the
   pop-up message.
5. Let the AI out-lay you inside the core, and wait for the next wave.
6. Leave the core empty of trail for one wave.
7. Play to the end (first team to **3** broods). Then try a 3v3.
8. With two players (Multiplayer Play Mode), compare the score at each wave on both windows.

**PASS:** six seats spawn on the ring without overlap; a wave hatches about every 30 s in the
colour of whoever holds the core and scores **1** brood for them with a "brood hatched — n/3"
message; out-laying flips the core; an empty core scores nobody; the match ends at 3 (roughly
1.5–2.5 minutes); the score agrees on both machines.
**FAIL:** a wave that never scores · a wave scoring for a team that holds nothing · the
first wave arriving before the countdown ends · a 3v3 that spawns pilots inside each other ·
scores that disagree between machines.
**Known, do not fail on:** the creatures themselves may differ slightly between machines (they are local) — only the score must agree;
the card's preview window may show a stale layout.

### QA-ASTROLEAGUE-REWORK ⬜ — blade strikes, the fauna pen, the smaller court, the settling ball
**Source:** PRs #704 and #706 (#706 is a playtest follow-up to #704, itself unflown). The mode
is now an **arena** card (Rhino, Scarab, Squirrel), and its doc still says "Rhino-only" (stale).
Reference: `_Scripts/Controller/Arcade/ASTROLEAGUE.md`, `Docs/ECOSYSTEM.md`.
1. Play **Astro League** from the Arena screen as the Rhino. Judge the court size first —
   #706 cut it **40 %** linearly.
2. **Strike the ball with a swung sword tip**, then with a **parked** sword at the same
   closing speed.
3. Strike the ball hard and let it run. Time roughly how long until it comes to rest.
4. Bounce the ball off a **wall** repeatedly and watch the carom lose energy.
5. Fly the pitch and watch the **fauna**: the nucleus is play geometry here, not a
   control zone, so creatures must be edible inside it.
6. Check the **sphere cage cover** renders on every intensity.
7. Score a goal and watch the replay (also covered by QA-SPEED-TUNNEL step 4).
8. Watch/listen for the new strike feedback: pop, shake, burst.
9. Play one match each as the **Scarab** and the **Squirrel**.

**PASS:** a swung tip sends the ball dramatically harder than a parked sword's deflect;
a struck ball loses roughly 65 % of its speed in ~3 s and genuinely reaches **rest**
rather than creeping; wall bounces cost energy while a vessel strike stays fully
elastic; fauna are eaten inside the court (no arena-wide sanctuary); the cage renders at
every intensity; strike feedback fires on contact; the Scarab and Squirrel can both move
the ball and score.
**FAIL:** a parked sword firing the ball · a ball that never settles or that stops dead
· fauna untouchable anywhere on the pitch · a missing cage at some intensity · no strike
feedback · a hull that cannot move the ball.
**Known, do not fail on:** the card
preview's objective text reads "Crush Ballz , bruh." (copy drift, logged); the match has no
authored goal target row (it ends on its own rule).
**Judgement calls to report (each a one-field edit on `AstroLeagueSettings.asset`):**
`ballDrag = 0.35` (sluggish → drop toward 0.2; won't die → push toward 0.5),
`wallRestitution = 0.72` (a billiards-ish guess), and the 40 % court cut (if it now
reads too cramped, say so).

### QA-ARENA-PILOT-SWAP ⬜ — six arena seats and the D-pad swap into an AI teammate's hull
**Source:** PR #915. Arena seats up to six (2v2v2), one pilot per hull, with a live D-pad
swap into an AI teammate's hull. Playtested by the author but reported "has issues" with
no specifics — treat as playtest-pending.
1. Launch **Regatta 2v2v2** as the **Urchin** with a **Serpent** AI ally.
2. Press **D-pad right**: you should now fly the **Serpent** — camera on it, your input
   live — while the AI takes over the Urchin. Press again to swap back.
3. Watch the Console for `[PilotSwap]` warnings; a failed takeover names what did not land.
4. Repeat the swap in **Broadside** and **Astro League**, and with a remote guest doing
   the swapping.
5. Build a 3v3 by hand in the lobby: no AI should change team on its own.
6. Two guests press SELECT VESSEL on the **same** hull: exactly one gets it.

**PASS:** D-pad swaps you into the ally hull with camera + input following, and back; no
`[PilotSwap]` warnings; the swap works in every listed mode and for a guest; no AI
self-reassigns team; a contested hull pick resolves to exactly one player.
**FAIL:** a swap that moves the camera but not input (or vice versa) · a `[PilotSwap]`
warning · an AI changing team on its own · both guests getting the same hull · a swap
that throws.
**Known, do not fail on:** a swap gives no toast — the camera move is the only feedback.
(Swapping into the Urchin now shows its four-icon ability row; a missing row is a failure.)

## Priority 0 — Block D: Maelstrom — the tournament across the modes above

Reachable with the Squirrel from the Arcade banner. Run it after blocks A–C so a failure inside
one pooled mode is already known and is not mistaken for a tournament bug.

### QA-MAELSTROM-RUN ⬜ — a full Maelstrom tournament has no QA entry
**Source:** `Docs/MaelstromSystem/ARCHITECTURE.md` (status: "implemented end-to-end"; the full
QA matrix is listed as **deferred**) and `MAELSTROM_UX_HANDOFF.md` § 7. The meta-mode strings
the domain games into one tournament, race to **6** placement points. The audit measured the
same hull repeating in back-to-back rounds **26.7 %** of the time at intensity 1. Many of the
pooled modes are themselves untested (blocks above), so run this **after** blocks A–C.

1. On the Arcade screen click the **Maelstrom banner**. Start solo (AI fill the other seats).
2. After the first game, press **Continue** and read the between-games screen.
3. Keep going until a team reaches 6. Write down each game played and its ship.
4. On the final summary, press **Play Again** twice quickly. Then, in a second run, press
   **Main Menu** on the summary.
5. With 2–4 players (Multiplayer Play Mode), run a short tournament. On a client, look for
   buttons on the per-game scoreboard and the final summary; press Main Menu on a client.
6. After a tournament, launch an ordinary Arcade game.
7. Run the edit-mode tests `MaelstromStandingsFormatterTests` and `MaelstromDataSOTests`.

**PASS:** each Continue shows a readable (~2 s) screen with the header, "Up next: <mode> ·
Intensity N", per-team rows and "(You)" on your team; the final summary shows "MAELSTROM
RESULTS", final standings with "(You)", and per-game blocks; Play Again / Main Menu are
host-only and fire once even when pressed twice; Main Menu covers the screen immediately;
on clients "(You)" sits on their own team, there are no per-game buttons and no Play Again,
Main Menu on a client leaves only that client; standings match on every machine; a normal
game afterwards shows the normal buttons; both tests pass.
**FAIL:** a game that never loads the next · a blank between-games screen · a double
Play Again · standings that differ between machines · tournament buttons leaking into a
normal game.
**Known, do not fail on:** no rewards, share screen or host migration (deferred); the card
has no preview window or Test Flight.
**Report:** how often the same ship came up twice in a row.

## Priority 0 — Block E: the Sparrow — the most-carded hull (11 cards) and its four modes

The Sparrow vessel has the most open checks in the fleet (13 red checklist entries, folded here
into nine items). Two sessions if needed: vessel items, then modes. Dog Fight is the best
scene for the vessel items (it has rivals to shoot).

### QA-SPARROW-HEAT-GUNS ⬜ — the Sparrow's guns: heat, the six-stage cone, fixed range
**Source:** PR #924 (heat model, "Round 8") on top of the never-verified spread passes:
`Docs/UNITY_VERIFICATION_CHECKLIST.md` 🔴 "Sparrow — the spread cone becomes a four-stage
curve", 🔴 "Sparrow spray accuracy — fire rate, decaying-accuracy cone, escalating haptic".
The current numbers live in `FullAutoAction.asset` (onset 5 s, plateaus 5 s, collapse to
~37.5°, cooling ×5, range to 25 % at full spread) and supersede the older checklist's
numbers. The Sparrow sits on the most cards in the fleet (11). Reference:
`R_VesselActions/SPARROW_SPRAY_ACCURACY.md` § In-editor verification.

1. Select `_SO_Assets/VesselActions/Sparrow/FullAutoAction.asset`. The **Accuracy** foldout
   shows Firing Rate **90**, onset **5**, plateau **5**, collapse plateau **5**, cooling
   **5**, range at full spread **0.25**. Open `Sparrow.prefab`: a **GunSprayAccuracy** child
   under `VesselActions`, script resolved.
2. Launch **Dog Fight** as the Sparrow. From a cold gun, fire short taps at open space and
   note where tracers die (about 258). Collect Space crystals and repeat.
3. Hold fire on a distant wall for 30 s and watch the circle of impacts and the heat gauge on
   the guns ability icon.
4. Hold for about 10 s, release for 1 s, fire again. Then release for 2 s instead.
5. With the cone open, enter Turret Stance (input 6) **without releasing the trigger**.
6. Stopped in Turret Stance, hold fire and look at the prisms.
7. Set the Game view to 30 fps (or `Application.targetFrameRate = 30`) and fire again.
8. With a gamepad: hold fire and feel the buzz; ram a prism mid-spray.
9. Hold 10+ s in both fire modes with the Profiler open. Keep the Console visible.

**PASS:** the asset and prefab show the listed values with no missing scripts; tracer reach
is the same with and without Space; a pull of up to 5 s from cold is a tight line; the cone
runs point (to 5 s) → growing (to 10 s) → static (to 15 s) → growing faster (to 20 s) →
static (to 25 s) → fast collapse to a huge spray by 30 s; reach barely moves until the
collapse and then pulls in to about a quarter; the gauge crosses five evenly spaced marks
in step with the stages; releasing **cools** rather than resets (1 s release → near the edge
of accuracy, 2 s → dead-on); a stance flip keeps the open cone and the heat; turret prisms
scatter in a volume, each pointing along its own flight; the stream looks equally dense at
30 fps; the buzz climbs then goes flat, and a ram's thud cuts through it; no hitch, no gun
errors in the Console.
**FAIL:** a field showing a different value (the key did not import) · reach changing with
Space · accuracy reset by a release or a stance flip · a stream that halves at 30 fps · a
missing script · `[FullAutoActionExecutor]` / `[FullAutoBlockShoot]` errors.
**Report the feel:** whether the heat phases make the weapon fun or frustrating, and whether
Dog Fight's 90-point and Salvo's 700-prism targets still feel right with these guns.

### QA-SPARROW-ROUNDS ⬜ — rounds grow in flight, the see-through charge shell, Mass-5 shields
**Source:** `Docs/UNITY_VERIFICATION_CHECKLIST.md` 🔴 "Sparrow rounds grow as they fly + MASS-5
shield restore", 🔴 "Sparrow round growth — model fixed + charge shell", 🔴 "Sparrow charge
shell round 5 — one stroke per round". **Highest risk: a hand-written instanced shader
(`ProjectileChargeField.shader`) never imported by Unity** — magenta means it failed.

1. Open `SparrowProjectile.prefab`. Both the dart and its `ChargeField` child must render
   (not magenta). Select `ProjectileChargeFieldMaterial`: **Enable GPU Instancing** is ticked.
2. Freestyle or Dog Fight as the Sparrow. Fire at empty space and follow one round from the
   muzzle to the end of its life.
3. Fire at a wall of prisms. Compare the hole a round makes with the shell you can see.
4. Collect Mass crystals up to 10 and fire again. Then drain Mass and fire again.
5. Hold full-auto and watch the two streams side by side.
6. Below Mass 5, enter Turret Stance and fire prisms. Repeat at Mass 5 or higher. Then
   raise **Space** to 5 with Mass below 5 and fire.
7. Fire a Manta round and a Sparrow rocket for comparison.

**PASS:** no magenta, instancing ticked; the dart is a thin pale-blue needle that stays the
same size the whole flight at every Mass level; around it a see-through shell starts close
and ends roughly 6× wider at Mass 10, and you can see the arena through it; the hole matches
the shell's size at that moment; each round shows **one** short bolt at its own angle, the
two muzzles never mirror each other, and a sustained burst reads as occasional sparks, not a
continuous lightning rope; arcs read blue with red cores; turret prisms arrive shielded only
at Mass 5+; Space 5 makes shots pierce but does not shield; other projectiles unchanged.
**FAIL:** a magenta shell (do **not** patch it live — report and attach the Console) · the
dart swelling into a lozenge · a shell you cannot see through · identical strokes on every
round · a bright lightning rope · Space 5 shielding prisms.
**Report:** if the shell reads too bright or too solid, say so — the knobs are documented
(`_ArcIntensity`, `_FresnelRimIntensity`).

### QA-SPARROW-ROCKETS ⬜ — two rockets out of one bay, one pull = one rocket, gunfire refills
**Source:** `Docs/UNITY_VERIFICATION_CHECKLIST.md` 🔴 "Sparrow — TWO rockets out of one bay",
🔴 "Sparrow follow-up + the fleet's drain rule" (both `cece/sweet-noether-trw75r`,
2026-09-22), 🔴 "Skyburst — the missile grows 20× in the first fifth of its flight". The
"one pull fires one rocket" fix is **fleet-wide** (a doubled input subscription). Nothing has
run in the Editor; `SparrowMissileVariantTests`, `SparrowMissileFuzeTests`,
`SparrowRoundGrowthTests`, `RoundGrowthRampTests` and `CombatHitDrainTests` were not run.

1. Run the five test classes above from **Window ▸ General ▸ Test Runner**.
2. Open `SkyBurstProjectile.prefab` ▸ `Projectile`: Flight Growth Target = `MissileVisual`,
   Flight Growth Uniform ticked, Flight Growth Complete At 01 = **0.2**.
3. In Dog Fight or Salvo as the Sparrow, tap the left trigger **once**. Count rockets and
   watch the ammo gauge.
4. Watch a base rocket fly past a pilot without touching it, and hit something directly.
5. Enter Turret Stance (A / Space) and fire. Watch the rocket and what it leaves behind.
   Fire a second heavy rocket straight down the first one's line.
6. Shoot one of the small prisms a heavy rocket laid.
7. With guns, destroy hostile prisms and watch the Charge ability gauge. Then shoot your
   **own** trail. Then destroy 30+ prisms with a rocket blast only.
8. Tap any ability once on two other ships (one tap = one action). Repeat after a round
   boundary in Cellular Duel and after the countdown pause.
9. With two players (Multiplayer Play Mode), fire a heavy rocket and watch it on the other
   window.

**PASS:** the tests pass; the three prefab fields hold their values; one tap = one rocket and
one ammo step, on every ship and after both re-subscribe paths; a base rocket is slower and
does nothing until it touches something; a heavy rocket is faster, lays a line of small
prisms about every 30 units, detonates early near a pilot or creature, and leaves a cairn of
prisms; a rocket never detonates on its own ribbon, and the second heavy flies past the
first's ribbon; laid prisms die to gunfire; every rocket leaves the bay small, swells over
about 0.6 s, then holds its size; the gauge resets every **25** gun kills (own-domain kills
count too) and does **not** move for blast kills; the other window sees the heavy rocket's
ribbon and early detonation.
**FAIL:** two rockets per tap, or any ability acting twice on one tap · a base rocket
detonating near a pilot · a heavy rocket blowing up on its own first prism · blast kills
refilling the gauge · a rocket still growing at impact, or launches growing larger as the
pool is reused · the remote window showing a base rocket for a heavy one · `SafeLookRotation`
spam.
**Known, do not fail on:** the bay's icon steps 0 → 1 → 2 and then stays at 2 (three-sprite
art gap).
**Report:** missiles now hit with a smaller sphere (about 3.8 units at rest, was 8.5) — say
whether landing one in Dog Fight feels fair.

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

### QA-SPARROW-ROLL-ARM-WINDOW ⬜ — the strafing roll arms for 0.3 s, and the horizon tilts the right way
**Source:** `Docs/UNITY_VERIFICATION_CHECKLIST.md` 🔴 "Sparrow — the strafing roll's arm is a
0.3 s WINDOW, not the whole boost hold" (`claude/sparrow-spin-cooldown-p8agtv`). The roll used
to fire during any long boosted turn.

1. Open `Sparrow.prefab` ▸ `BarrelRollController`: **Roll Arm Window Seconds = 0.3** (not 0).
2. Fly the Sparrow, hold boost through a long hard turn with the stick pinned to the edge.
3. Tap boost and slam the stick to full deflection in the same beat.
4. Hold the stick at full deflection, then press boost.
5. Stop (Turret Stance), then press boost and slam the stick.
6. Watch the boost icon's ring on every boost press, with and without a roll.
7. Watch the horizon during a roll at cruise and while boosting.

**PASS:** the field reads 0.3; a long boosted turn never rolls; tap-and-slam rolls exactly as
before; boost with the stick already pinned rolls immediately; the stopped dodge still
strafes; the ring fills and wipes ~0.3 s later with no punch, and with a punch on a press that
rolls; the horizon tilts the same way as the roll (no bank fighting it).
**FAIL:** the field at 0 · a roll during a held boost turn · a deliberate tap-and-slam that
misses · the horizon tilting against the roll.
**Report:** whether 0.3 s is too tight on a gamepad (0.4–0.5 is the alternative).

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
11. Play a full round to the point target (**90**) and watch the scoreboard.

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
**Known, do not fail on:** a hit flash is not yet shown to the **victim** (hit feedback is
not replicated; the victim's petal loss IS, since PR #1007 - QA-COMBAT-PETAL-DRAIN-NET); the card preview's objective text is the placeholder "Classic
Dogfight". The 90-point target is unmeasured — report the match length.

### QA-SALVO-MODE ⬜ — "Salvo" has never been opened
**Source:** `GameModes.Salvo = 44`, Sparrow destruction race in the Boneyard (shared with Dog
Fight, Broadside and Dustup). Authored headless, **never run**. The "wingman reload" (a
teammate's crystal refills your rockets) has no feedback beyond the gauge. Target **700**
prisms, unmeasured. Reference: `_Scripts/Controller/Arcade/SALVO.md` § In-editor verification.

1. Open `MinigameSalvo.unity`. The controller shows `rule = SalvoScoringRule`,
   `missileResourceIndex = 0`, elemental scatter **14 / 400 / 42**; the Cell shows four
   Boneyard configs on **Intensity Wise**.
2. Launch a 2v2 (you + AI teammate) at intensity 1. Count the white crystals at the start.
3. Shoot wreckage with the guns and watch your team score. Land a rocket on a hulk. Shoot a
   **teammate's** trail.
4. Empty your rocket tank (two rockets). Touch nothing; let your **teammate** collect a white
   crystal. Then let an **opponent** collect one.
5. Skim a coloured (elemental) crystal with an empty tank.
6. Collect a white crystal and watch where the next one appears.
7. Watch the objective arrow and the AI for two minutes.
8. With two players (Multiplayer Play Mode): the client empties its tank and the host
   collects; then the reverse.
9. Play to the end and time it. Press Replay.
10. Regression: launch Dog Fight and count its crystals.

**PASS:** the scene opens clean; white crystals = players + 5 (9 in a 4-player lobby), spread
inside about 420 and not stacked in the middle, and one respawns elsewhere after collection;
gun kills tick the score per prism, a rocket adds its whole harvest, a teammate's trail scores
nothing; a teammate's white crystal refills **your** tank on your machine and an opponent's
does not; an elemental crystal raises its element and refills nothing; refills work both ways
between host and client; the arrow points at the nearest white crystal; AI Sparrows fly at
crystals and shoot wreckage rather than circling pilots; the match ends at 700 with "SALVO
TIME" for the winners and "N Prisms Left" for the losers; Replay resets to 0; Dog Fight still
has 4 fixed white crystals.
**FAIL:** destruction not scoring · a refill from an opponent's crystal, or no refill from a
teammate's · refills failing in one direction only between machines · AI orbiting enemy
pilots · Dog Fight's crystal count changed.
**Report:** match length at intensity 1, and roughly how much destruction came from rockets
versus guns.

### QA-BREAKWATER-MODE ⬜ — "Breakwater" has never been played
**Source:** `GameModes.Breakwater = 50`, Sparrow station race: fifteen dishes with danger
plugs you open with rockets, guns, or by threading the eye. Every file was compiled offline and
`BreakwaterCourseTests` ran (49/49), but **"nothing here has been run in the Unity editor"**. The
scene needs one open-and-save in the Editor to re-mint its network object ids — commit that
diff. Reference: `_Scripts/Controller/Arcade/BREAKWATER.md` § In-editor verification.

1. Open `MinigameBreakwater.unity`, then save it (**File ▸ Save**). Note whether git shows a
   change to the scene (that change is expected; report it so it can be committed). The
   controller shows `rule = BreakwaterScoringRule` and `arenaPrefab = SpawnableBreakwater`;
   the spawner shows `spawnFormation = EquatorialRing`.
2. Launch at intensity 1. Before touching the stick, look around: fifteen dishes facing
   different ways, each with a blue ring at its mouth and a woven danger plug in its throat;
   nothing near the start positions. The Console shows `[Breakwater] Course seed …: 15 stations`.
3. Fire a rocket at a plug. Then grind a plug open in Turret Stance. Then fly through a
   plug's eye without touching it.
4. Destroy about 50 hostile prisms and watch the Charge ability gauge.
5. Thread station 1. Then fly through station 3 before station 2. Then 2, then 3.
6. Thread a station from its far side. Fly just past a ring's rim.
7. Clip a plug's bars. Then clip the collar around a ring.
8. Two pilots on one team: one threads 5, the other 0, then 3. Watch the team box.
9. Watch an AI Sparrow for two stations.
10. Thread the last station and look at the objective arrow. Finish the race. Press Replay.
11. Compare intensity 1 and 4: port sizes, how twisty the course is, and whether one rocket
    clears a whole plug.
12. Run **FrogletTools ▸ Ecology ▸ Measure Cell Environment Baselines**.
13. Regression: launch Switchback (twenty rings, lime next ring, AI flying the course).

**PASS:** the arena builds with fifteen stations and clear start positions; all three ways
through a plug cross the ring and score the same **1**; about 50 gun kills refill one rocket;
stations count only in order (3 before 2 does nothing), from either side, and never for a
near miss; plug bars full-stop and debuff you while the collar is an ordinary hit; the team
box shows the **best** single pilot's count (5, not 8); AI thread stations in order and fire
down a station's axis on the way in; after the last station the arrow points to the next
circuit station and the start gate is never offered again; the race ends with "VICTORY" and
Replay rolls a new course; intensity 4 has tighter ports, a twistier course and one-rocket
plugs; baselines match the doc's table; Switchback unchanged.
**FAIL:** no arena, or stations near the spawns · a method that does not score · stations
counting out of order · a near miss counting · the team box summing pilots · an AI stuck at a
plug · Replay rebuilding the same course · Switchback changed.
**Report:** the station count shown in the score row and the finish time at intensity 1.

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
13. Play a round toward the kill target (**30** — the shipped value; the mode doc argues
    for 500).

**PASS:** three well-separated cages whose shape changes with intensity; baselines
within a few hundred; equatorial outside spawn; a 1-prism creature dies to one shot and
drops a crystal; kills counted per creature and only when you caused them; tiers stay
spread in their own rooms; the population visibly grows; both players get a Sparrow
regardless of their pick.
**FAIL:** one cage / adjacent layers · a creature that survives losing all its prisms ·
a counter ticking per prism · score moving for a starvation or a shark kill · creatures
clumped at the arena centre or wandering between rooms · a flat population three minutes
in · a non-Sparrow vessel spawning on either machine.
**Known, do not fail on:** nothing from the 2026-10-05 list any more: the Clawfish is now
hunted (shoot a tail rib to kill it), the objective arrow points at the nearest creature, and
the pop-up messages are authored. Their absence is now a failure.
**Report:** how long 30 kills takes, so the target can be set from a real number.

## Priority 0 — Block F: the Dolphin — 10 red vessel entries and its three modes

The Dolphin's ten open checklist entries are folded into six items. Fly QA-DOLPHIN-RIG-SWAP
early in the session: it gates a merge. Rampage's forest is shared by The Bends, Bloomrush and
Sirocco, so its ladder numbers are worth taking even if time runs short.

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

### QA-DOLPHIN-SPEED-TUNE ⬜ — cruise 68, boost peak about 347, and a real full stop
**Source:** PR #681 (cruise/boost retune) and `Docs/UNITY_VERIFICATION_CHECKLIST.md` 🔴
"Dolphin minimum speed → 0" (`claude/dolphin-minimum-speed-59q8ay`), which **supersedes the
earlier 78 / 357 numbers**: the 10-unit speed floor was removed, so cruise is now 68 and the
peak about 347, and releasing the throttle stops the ship. In-place scalar edits; the
arithmetic is machine-checked, the feel is unflown.
1. Menu_Main → freestyle → **Dolphin**. Full throttle, no boost — read the speed on the
   DiagnosticsHUD.
2. Release the throttle completely and watch the ship. Then throttle back up.
3. Hold drift from an empty meter and time the boost ring filling.
4. Release a full meter and read the peak speed, then time the fall back to cruise.
5. Drift → release → drift again.
6. Stop completely, then start a drift from rest.
7. Fly any other vessel as a regression check.

**PASS:** cruise settles at **≈68**; with the throttle released the Dolphin comes to a smooth
**full stop** (not a crawl) and recovers responsively; the ring fills in **≈3.6 s**; the peak
reads **≈347** and takes ~2.5 s to fall back; speed returns to normal with no stuck
multiplier; a drift from rest reads as "parked", not stuck; no other vessel changed.
**FAIL:** numbers materially off the targets above · a crawl instead of a stop · a stop you
cannot recover from · a stuck multiplier after drift→release→drift · another vessel's speed
changing.
**Judgement call to report — this is the point of the item:** whether ~347 is too much (the
speed tunnel amplifies it), and whether a dead stop feels right.

### QA-DOLPHIN-DRIFT-HOLD ⬜ — the drift freezes speed and direction; the throttle is inert while drifting
**Source:** `Docs/UNITY_VERIFICATION_CHECKLIST.md` 🔴 "Dolphin drift holds its velocity —
throttle disabled for the drift" and 🔴 "Vector flight model — Squirrel drift fix, Dolphin
migration, Scarab refactor" (the Dolphin's drift is now **Locked**: entering a drift no longer
costs speed). Reference: `R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md` §2.

1. Open the Dolphin prefab ▸ `VesselTransformer`: **Hold Speed While Drifting** is ticked.
   Open Squirrel, Rhino and Manta: it is **unticked**.
2. Freestyle on the Dolphin at part throttle. Start a drift, then sweep the throttle from end
   to end. Watch the speed and steer with the stick.
3. Repeat from a crawl and from full throttle.
4. Release the drift. Then boost off a full meter.
5. Drift into a danger prism.
6. Drift, then end the turn / swap ships mid-drift, then fly again.
7. Enter a drift straight out of a full boost.
8. Swap to the Squirrel and drift with the throttle moving.
9. With two players (Multiplayer Play Mode), both on Dolphins, drift on one and watch it on
   the other.

**PASS:** the field is ticked only on the Dolphin; during a drift the speed does not move
with the throttle but the heading still swings; the held speed is whatever you had when the
drift began; on release the throttle works immediately and boost accelerates as before; a
danger prism still slams you to the danger slow mid-drift; nothing stays locked after a
swap or turn end; entering a drift from a boost keeps the boosted speed and lets it decay
naturally (no slam to cruise); the Squirrel's drift still follows the throttle; the remote
Dolphin's drift speed matches on both machines.
**FAIL:** speed changing with throttle during a Dolphin drift · a held speed that is always
the same number · a danger prism ignored mid-drift · a throttle that stays dead after a swap
· a drift entry that slams speed down · the Squirrel's drift changed.
**Report:** whether re-drifting at the peak of a boost lets you ratchet speed upward.

### QA-DOLPHIN-ECHO-SIGHT ⬜ — the re-cut element map, the Echo Sight halo, and the ability cards
**Source:** `Docs/UNITY_VERIFICATION_CHECKLIST.md` 🔴 "Dolphin elemental map re-cut around one
weapon" (`claude/dolphin-elemental-upgrades-umokil`, five passes) and 🔴 "Ability Lockup
(TOTEM) — Dolphin". **Highest risk: a hand-written halo shader (`EchoSightHalo.shader`) and a
fragment-stage change to the prism sight shader, neither seen by Unity.** Reference:
`R_VesselActions/DOLPHIN_CRYSTAL_SEEDING.md` §8–12, `Docs/ABILITY_LOCKUP.md`.

1. Open `DolphinHUDVariant.prefab`: four slots left to right — ProfileButton, CrystalButton,
   JawButton, DriftButton; `CrystalPip0/1` are **gone**; the `Profile` object has its script.
   Run **FrogletTools ▸ Vessels ▸ Audit Vessel Ability Rows** (Dolphin 4/4, order ✅).
2. Select `Resources/EchoSightHalo.mat`: it shows the `EchoSightHalo` shader, not magenta.
3. Freestyle on the Dolphin. Watch the Charge icon as energy banks; hold and release the
   right trigger.
4. Hold the right trigger near a wall of prisms. Look at a shielded prism inside the cone.
5. At Charge 5, put another ship inside the cone; release; swap ship while holding.
6. In **Rampage** at Charge 5, hold the trigger with a rival in the cone — in open space,
   surrounded by lit prisms, and fully behind prisms. Then mark a rival 1000+ units away.
   Put two rivals of different teams in one cone.
7. Below Mass 5, wait for a seeded crystal; then raise Mass to 5; then change team with the
   Domain Changer toy.
8. Lay a drift trail. Fire a blast at a dense wall; then one that catches a rival and some
   creatures; then one that catches neither.
9. Look at the four ability cards (flat plates with the element flower docked above each
   icon).

**PASS:** the HUD slots and audit are correct; the halo material compiles; the Charge icon
draws a solid capsule (not a bowtie) that grows with energy, grey at rest and white while
held; prisms in the cone light **whole** (jagged prism-by-prism edge, never a smooth cut
across a face) and read as lit, not washed white, with the shielded tier still telling apart;
at Charge 5 a rival in the cone glows in its own team colour with a ring-shaped halo on its
outline that reads in all three Rampage cases, stays roughly the same size beyond ~750 units,
stays circular, and fades out cleanly with no lingering glow (also after a ship swap); two
rivals show their own colours; below Mass 5 seeds are lime with a lime Mass icon and a rival
can collect them, at Mass 5 they and the icon take your team colour, follow a team change,
and a rival cannot collect them; drift trail prisms are not shielded and do not grow; the tally
under the jaws shows a 4–5 digit number at full size, and after a blast a white pilot count and
a blue creature count that fade after ~2.5 s, blank (no "0") when nothing was caught; each
ability card sits behind its icon with no second row of flowers anywhere.
**FAIL:** a magenta or invisible halo · the whole field of prisms lighting at once regardless
of where the cone points (report it — the fallback switch is `PRISM_SIGHT_WHOLE_PRISM 0`) ·
a bowtie Charge icon · a black Mass icon · a glow that does not fade · a remote Dolphin's
sight brightening things on your machine · a card covering its icon · two rows of flowers.

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

### QA-DOLPHIN-RIG-SWAP ⬜ — Dolphin rig swap: flight 17, the re-flight that gates the merge
**Source:** `Docs/UNITY_VERIFICATION_CHECKLIST.md` 🔴 "Dolphin rig swap — FLOWN, BROKE, FIXED;
needs a re-flight" (2026-08-26; **flight 17 pending since 2026-09-15**). Sixteen flights so far.
The legacy hull was replaced by the `dolphin_shapekey` rig; every structural check passed while
the ship was still broken in flight, so only a flight settles it. Reference:
`Docs/VESSEL_CONSTRUCTION.md` §4.6.5.

1. Run **FrogletTools ▸ Vessels ▸ Audit Vessel Tails and Jets**: six jets, on the nozzle bones.
2. Run **Audit Corridor Vessel Radii**: the Dolphin's number has not moved.
3. Run **Audit Vessel Skimmers**: the Dolphin passes exactly as before.
4. Fly the Dolphin in freestyle **side by side** (or alternately) with a Dolphin on an older
   build if you have one. Turn hard in both directions.
5. Raise each element from 0 to 10 and watch the hull change shape.
6. Watch the jaws, wings and six thrusters while flying, boosting and drifting. Skim, lay
   trail and fire a crystal blast.

**PASS:** the three audits pass with the stated results; in ordinary flight the wings and all
six boosters swing **with** the body's turn (they orbit it rather than spinning in place
while the tail sweeps away), and the boosters swing about a common seat behind the body with
the old, exaggerated amplitude; each element visibly morphs the hull; jaws, wings and
thrusters animate; trail, skim and blast are unchanged; the team colour is on the accent, not
the body, and no leftover yellow from Blender shows.
**FAIL:** puppetry that tears apart · boosters spinning in place · a hull that does not morph ·
team colour on the body or a yellow accent · any audit regressing. If an audit is not green,
**do not hand-fix the prefab** — report what it printed.
**Report the feel:** two inspector choices on `Dolphin.prefab` ▸ `RiptideAnimation` are open —
`thrusterAnimationScaler 75` (the old amplitude) and `mirrorAppendageRoll` off. Say whether the
roll on the parts still reads backwards.

### QA-RAMPAGE-LADDER ⬜ — Rampage intensities 1–3 have never been played
**Source:** `_Scripts/Controller/Arcade/RAMPAGE.md` § In-editor verification. Rampage
(`GameModes.Rampage = 2`, Dolphin, target **2000** prisms) was play-tested at **intensity 4
only**; rungs 1–3 were "authored headless". Both measured baselines **predate the sixth flora
species (2026-09-20)** and were never re-measured. The forest is also the arena for The Bends,
Bloomrush and Sirocco, so its numbers matter four times.

1. Open `MinigameRampage.unity`: the Cell shows **4** configs and **Intensity Wise**.
2. Launch solo with AI backfill (1 human + 3 AI) at intensity **1**. Confirm every ship,
   including the AI, is a Dolphin, spread on a large sphere facing the cell.
3. Wait about 60 s and look at the forest: plants fill the cell from just outside the core out
   to the outer wall, in mixed colours and elements, and different **species** are visibly
   different sizes.
4. Read the cell's live volume and prism count on the DiagnosticsHUD once the forest stops
   growing. Write both numbers down.
5. Skim the forest, collect a crystal, fire the cone. Watch the score, the objective arrow and
   where the crystal reappears.
6. Watch one AI for a minute.
7. Play to the target and time the match. Press Replay.
8. Repeat steps 2–4 and 7 at intensities **2** and **3**. Then launch intensity 4 once for
   comparison.
9. With two players (Multiplayer Play Mode) at intensity 4, compare the prism count on both
   windows.

**PASS:** every ship is a Dolphin; at each rung the forest fills the space between core and wall
and stops growing at the frenzy threshold rather than early; nothing planted inside the core or
outside the wall; blast kills score for your team and your own trail scores nothing; the arrow
points at the crystal and re-acquires; the crystal reappears inside the core; the AI flies at
the crystal and swings its nose onto a stand of trees; the match ends at 2000 and Replay
reloads cleanly; the four rungs look and profile obviously different; both machines show the
same prism count (a client log line `IntensityWise config choice DEFERRED` once is fine).
**FAIL:** a non-Dolphin ship · a forest that stops far short or overruns · rungs that look the
same · a client showing a fraction of the host's prisms (a known race that must not regress) ·
anonymous (unscored) blast kills.
**Report numbers — the point of the item:** volume and prism count per rung, and match time per
rung. The old model said about **569k / 3,500** at intensity 1 and **1.62M / 9,830** at 4.

### QA-BENDS-MODE ⬜ — "The Bends" has never been played to a result
**Source:** `GameModes.Bends = 42`, Dolphin duel: a "bend" is catching a rival in your crystal
cone, which drains their elements; first team to **3** bends wins. Authored headless; a playtest
found **the AI never landed a hit**, fixes followed (`Docs/UNITY_VERIFICATION_CHECKLIST.md` 🔴
"Bends AI aim: wavefront intercept lead + human focus") and none has been verified. Reference:
`_Scripts/Controller/Arcade/BENDS.md` § Verification.

1. Open `MinigameBends.unity`: no missing scripts; `BendsController` and
   `BendsPointTurnMonitor` resolve on the Game object.
2. In **any** mode as the Dolphin, catch an AI in your crystal cone. Watch its element flowers.
3. Start a 2-player, 2-team Bends match. Bend the opponent once. Watch the score and the target
   readout.
4. With two players (Multiplayer Play Mode), have the **client's** Dolphin land a bend.
5. Catch two opponents in one blast. Hold one opponent inside the cone for a second or more.
6. Bend a pilot who is elementally immune (if one is available).
7. Watch an AI with you within about 2400 units: does its nose lead you as you cross, and does
   its blast land? Then fly beyond 2400 units. Then put a human and an AI rival both in range.
8. Play to 3. Read the scoreboard. Press Replay.

**PASS:** a caught AI's flowers drop and recover over about 4 s; one bend adds **+1** and the
deficit drops from 3 to 2; a client's bend adds exactly **+1** on both machines (not 2, not
0); a double catch adds 2; a long hold adds 1 once; an immune pilot scores nothing; the AI
leads a crossing target further at long range, lands blasts on a straight-flying rival,
prefers the human, and grazes the forest when nobody is in range; the scoreboard's second line
reads "N bends" correctly on every machine and Replay resets to 0.
**FAIL:** flowers that never drop (the wiring is broken — stop, nothing else matters) · a client
bend scoring 2 or 0 · an AI that never lands a hit in two minutes · a match that ends in two
hits.
**Known, do not fail on:** the card preview says "Shoot your Echo" (the mode has no guns; copy
drift, logged). (The objective arrow is now wired here: it points at the nearest rival pilot,
never a teammate.)
**Report:** whether a 3-bend match feels too abrupt.

### QA-SWITCHBACK-MODE ⬜ — "Switchback" has never been opened
**Source:** `GameModes.Switchback = 45`, Dolphin gate race (twenty rings, **20** to win). C#
compiled headless and the course generator ran over 400 seeds, but **nothing has been run in
the Editor**. Its ring and grid-growing code is shared with Breakwater, Headlong, Redline,
Regatta and Waystation. Reference: `_Scripts/Controller/Arcade/SWITCHBACK.md` § In-editor
verification.

1. Open `MinigameSwitchback.unity`: `rule = SwitchbackScoringRule`; the Cell shows **one**
   config (Skim Race) on Random; the spawner shows `EquatorialRing`, 150 outside the core.
2. Launch. Twenty blue rings bloom in, scattered and facing different ways. Gate 1 sits above
   the cell centre, the same distance from every pilot.
3. Thread gate 1. Then gate 3 before gate 2. Then 2, then 3.
4. Thread a gate from its far side. Fly just past a ring's rim.
5. Look at the ring colours: your next ring must be **lime**, all others blue.
6. Two pilots on one team: one threads 5, the other 0, then 3. Watch the team box.
7. With two players (Multiplayer Play Mode): the client threads gates; then the host. Compare
   which ring is lime on each window when their counts differ.
8. Collect a white crystal and catch a rival in the cone.
9. Watch an AI Dolphin through three gates. Finish the race. Press Replay.
10. Compare intensity 1 and 4.
11. Open the Arcade screen with no favourites set and scroll to the bottom; open the last
    card. Then favourite a mode and repeat.

**PASS:** twenty rings at every intensity; gates count only in order, from either side, never
for a near miss; your next ring is lime and the lime moves on; the team box shows the **best**
pilot's count (5, not 8); a client's gates count on both machines and each window shows its own
lime ring; a cone hit debuffs the rival for about 4 s without changing either gate count; the
AI lines up on each gate's axis and threads in order; the race ends with "VICTORY" + time, "N
Gates Left" for losers, and Replay resets to 0/20; rings are smaller and the course twistier at
4; every Arcade card is drawn and the last one opens, with no `ArcadeExploreView` error.
**FAIL:** no rings · out-of-order or near-miss gates counting · the team box summing pilots ·
a shared lime ring across machines · an AI circling a ring · a missing or unopenable last card.

## Priority 0 — Block G: the Rhino — Cleave and Headlong

If the session has time, the Rhino vessel items in Priority 1 fit here too
(QA-VESSEL-RHINO-SWORD, QA-RHINO-SWORD-COMBOS, QA-RHINO-SKIMMER-SHAPE, QA-RHINO-RAMP-BOOST,
QA-P1-RHINO-RAMP-CEILING); Headlong exercises the ramp boost directly.

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
**Known, do not fail on:** the Rhino's ability row shows two LOCKED cards (the 1st and 3rd)
- those are open design slots with no ability yet; the other two icons are white placeholders.
(The objective arrow and the pop-up messages now exist; the arrow points at a dense part of the
arena and moves at most every 1.5 s.)

### QA-HEADLONG-MODE ⬜ — "Headlong" (the Rhino's circuit) has never been flown
**Source:** PR #852. `GameModes.Headlong = 49`, Rhino-only closed circuit (8 rings a lap, **24**
to win) built around the graded ramp boost. "Not editor-verified … nobody has flown it." The AI
was never tuned for Rhino speed. Its doc points to a checklist entry that does not exist.
Reference: `_Scripts/Controller/Arcade/HEADLONG.md` § 8.

1. On a fresh account, open the Arcade screen: the **Headlong** card is clickable and its
   launch panel pins the ship to the **Rhino**.
2. Launch at intensity 1 with 2 players. The connecting panel holds until the circuit arrives,
   then eight rings bloom in a closed loop; your next ring is lime and the arrow points at it.
3. Fly a lap at full throttle, watching the speed readout.
4. Thread the eighth ring and look at the score row and the lime ring.
5. Launch intensity 4 and fly several laps, once feeding in more stick than each corner needs
   and once with the smallest stick you can.
6. Add an AI and watch it for two laps.
7. With two players (Multiplayer Play Mode), compare the circuit and the gate counts.
8. Regression: launch Switchback.

**PASS:** the card pins the Rhino; speed climbs steadily toward about 1200 over ~5 s; at
intensities 1–2 exactly **one** corner costs speed; after ring 8 the lime returns to ring 1 and
the row reads **8/24**, not "finished"; intensity 4 has three speed-costing corners including a
hairpin to about a third of top speed, and extra stick visibly costs exit speed; the AI completes
more than one lap; both machines see the same circuit and no crossing counts twice; Switchback
unchanged.
**FAIL:** a non-Rhino ship · a circuit that never arrives · "finished" after one lap · an AI stuck
after lap 1 · a crossing credited twice.
**Known, do not fail on:** the Rhino's ability row shows two LOCKED cards (the 1st and 3rd)
- open design slots; the other two icons are white placeholders (QA-HULL-ABILITY-ROWS).
**Report:** whether the graded ramp feels too forgiving or too punishing (dial:
`straightnessGraceBand`).

## Priority 0 — Block H: the Manta — Redline and Bloomrush

Short block. QA-P1-MANTA-SOAR-SPARROW-AFTERBURNER (Priority 1) fits in the same session.

### QA-REDLINE-MODE ⬜ — "Redline" (the Manta's circuit) has never been flown
**Source:** `GameModes.Redline = 53`, Manta-only closed circuit (8 rings a lap, **24** to win),
flown on Soar (both triggers). "Not editor-verified." The AI never uses the Manta's turn ability,
so it overshoots intensity-4 hairpins; its new Soar drive is a **fleet-wide** Manta AI change.
Reference: `_Scripts/Controller/Arcade/REDLINE.md` § 8.

1. On a fresh account, the **Redline** card is clickable and pins the ship to the **Manta**.
2. Launch at intensity 1, 2 players: eight rings in a closed loop, lime next ring, arrow on it.
3. Fly a lap with both triggers held down. Watch the speed readout.
4. Thread the eighth ring and look at the score row.
5. Launch intensity 4 and fly several laps.
6. Add an AI Manta and watch it for two laps.
7. With two players (Multiplayer Play Mode), compare gate counts; fly fast through gates on the
   client.
8. Regression: launch Headlong; and fly an AI Manta in another mode (Scurry or Brood Rush).

**PASS:** the card pins the Manta; speed settles at about **720** within ~2 s; at intensity 1
nothing forces you to lift a trigger; after ring 8 the row reads **8/24**; at intensity 4 two
corners force a trigger off and a third can just be held flat on a clean line; the AI soars on
straights, lifts in corners and completes more than one lap; a fast client's crossings are never
dropped; Headlong unchanged; AI Mantas elsewhere still fly normally.
**FAIL:** a non-Manta ship · "finished" after one lap · a fast crossing dropped as implausible ·
an AI that never lifts or never finishes a lap · AI Mantas in other modes behaving oddly.
**Known, do not fail on:** the Manta is
silent (its six sting sound slots are empty); the card preview is an empty shell.

### QA-BLOOMRUSH-MODE ⬜ — "Bloomrush" has never been compiled or played in Unity
**Source:** `GameModes.Bloomrush = 52`, Manta party game: tag everything you fly past, then reach
a crystal before the fuses burn down and set it all off. A **120 s timed** round scored on hostile
prism **volume** (`VolumeDestroyed`). Doc: "Not yet compiled or played in Unity." In Rampage's
forest. Reference: `_Scripts/Controller/Arcade/BLOOMRUSH.md`.

1. Launch Bloomrush at intensity 1 with AI. Wait for the countdown to end.
2. Do nothing for 25 s and watch for a hint message.
3. Fly close past plants of a rival colour. Look for halos on what you tagged.
4. Fly into a white crystal. Watch the detonations and your score.
5. Tag more, then let the fuses run out without reaching a crystal. Compare the blasts.
6. Watch the AI for a minute.
7. Let the clock run out. Read the scoreboard. Then launch intensity 4 and watch how fast the
   halos burn down.
8. Go back to freestyle as the Manta and tag something; time its fuse.

**PASS:** an idle hint appears after about 25 s; each tagged target shows a halo (visible to you
only) that quickens and goes hot as its fuse burns; a crystal detonates your tags nearest-first
as a rolling chain, shows a cash-out message, and raises your score by volume; fuses that run
out make smaller blasts; the AI grazes and collects crystals; the round ends at 120 s and the
highest volume wins (fuses beaten breaks a tie); fuses are visibly shorter at intensity 4;
freestyle fuses are back to 25 s.
**FAIL:** a compile error · tags with no halo · a crystal that detonates nothing · a score that
never moves · a round that does not end at 120 s · freestyle fuses stuck at a match's value.
**Known, do not fail on:** **silence** — six sound slots on `MantaStingConfig.asset` are empty
by design until audio lands.

## Priority 0 — Block I: the Urchin — the hull, Skein and Hijack

Last because the Urchin was the least finished hull. Its HUD, its AI and Hijack's replicated
arena all merged 2026-10-06 and have their own items in Block P (QA-URCHIN-HUD, QA-URCHIN-AI,
QA-HIJACK-OWNERSHIP-SYNC); run those first, then this block. The vessel item first. Its
2026-10-08 fixes (Urchin backlog U1–U16 and Hijack shield sync) are in Block Q
(QA-URCHIN-TWO-PEER, QA-URCHIN-BACKLOG-SOLO, QA-HIJACK-SHIELD-SYNC).

### QA-URCHIN-VESSEL ⬜ — the Urchin's spikes, trail ride, launch and Track Projector
**Source:** `Docs/UNITY_VERIFICATION_CHECKLIST.md` 🔴 "Urchin revival — chain-reaction spikes +
trail rider" and 🔴 "Urchin — end-of-ribbon launch, merged spike trigger, Track Projector"
(prefab edited as YAML, import never checked). `URCHIN_BACKLOG.md` U1/U2 (which blocked a
multiplayer ship) and U4-U16 were fixed by PR #1005 - Block Q items QA-URCHIN-TWO-PEER and
QA-URCHIN-BACKLOG-SOLO. References: `URCHIN_TRAIL_RIDER.md`, `URCHIN_CHAIN_SPIKES.md`,
`URCHIN_TRACK_PROJECTOR.md`.

1. Open `Urchin.prefab`: `ActionExecutorRegistry` lists **three** executors and the input
   bindings read 1 → `UrchinSpikeAction`, 2 → `UrchinTrackAction`, 7 → `UrchinSlipAction`, with
   no missing-script or unassigned warnings. Open `Resources/ElementalAbilityMaps/Urchin.asset`:
   four entries (Chain Spikes, Trail Rider, Track Projector, Slip).
2. Freestyle as the Urchin. Fly into a trail: you latch on. Push and pull the stick to slide
   both ways along it.
3. Ride an open (non-loop) trail to its end without steering. Then ride a loop.
4. After a launch, watch your speed bleed off. Respawn and fly again.
5. **Tap** the right trigger. Then **hold** it for half a second and for 2.5 s, and release.
   Swap ship mid-hold.
6. Press the left trigger (Track Projector) at cruise and at grind speed. Press it again
   immediately.
7. Ride across the surface of a plate-shaped (gyroid) flora if one is near.

**PASS:** the prefab and map are as listed; the Urchin latches onto a trail and slides with the
stick both ways; at an open trail's end it **launches** off rather than parking, and does not
immediately re-latch to the same ribbon; a loop is ridden forever; excess ride speed bleeds off
over several seconds and does not survive a respawn; a tap fires one aimed ring blast; a hold
fires a burst (more spikes the longer you held, 6 up to 36) on release; a swap mid-hold fires
nothing; the track lays a straight 13-prism stretch ahead of your nose and is on a 20 s
cooldown; the Urchin rolls across a surface flora.
**FAIL:** a missing executor or binding · no latch, or a frozen ride · parking at a ribbon's end ·
re-latching to the same ribbon at once · a tap that bursts, or a hold that fires nothing · a burst
after a mid-hold swap · a track that lands behind you or never cools down.
**Known, do not fail on:** the four ability icons are white placeholders (art pending); three
sound slots are empty. (Chain depth and reach must now agree between machines, and a remote steal
must debit the victim - both checked in QA-URCHIN-TWO-PEER.)

### QA-SKEIN-MODE ⬜ — "Skein": the intensity ladder and the AI Urchin have never been verified
**Source:** PR #855. `GameModes.Skein = 51`, Urchin-only cable race. **Two playtest passes
happened** (`SKEIN.md` §13–14: rings, the arrow, a faster Urchin, a start collar and start line);
the §15 intensity-ladder pass ("no hunting for the next ring") is unverified, and the AI Urchin
has never been seen racing end to end — without it there is no solo play. The ring target is
missing from `EndConditionOverrides.asset`, so the code default (**24**) applies. Reference:
`_Scripts/Controller/Arcade/SKEIN.md` §10, §15.

1. Open `MinigameSkein.unity` and save it; report whether git shows a scene change (expected
   once — network object ids are re-minted on first save).
2. Launch at intensity **1**. Fly the course. From each ring, before turning, check whether the
   next ring is already on screen.
3. Launch at intensity **4** and compare.
4. At intensity 1, ride the cable through a wide ring that surrounds the whole cable (a
   "collar") on different strands.
5. Add AI Urchins and watch one for two minutes. Then play solo with only AI opponents.
6. Play to the end (24 rings).
7. With two players (Multiplayer Play Mode), compare ring counts.

**PASS:** at intensity 1 the next ring is visible from every ring as you pass through (the
objective arrow is effectively never needed), and collars can be threaded from any strand;
intensity 4 is visibly harder (more hunting, single-strand rings); AI Urchins grind the cable at
full speed, thread rings in order and finish; a solo match against AI ends with a winner; both
machines agree on counts.
**FAIL:** rings at intensity 1 that are behind you when you pass the previous one · a collar
that misses a strand · AI that orbit, stall on the cable, or never finish (no solo play) · a
match that never ends.
**Known, do not fail on:** the Urchin's ability icons are white placeholders. (Skein now has
pop-up messages - halfway, lead change, home stretch - and AI Urchins that Slip off wrong strands
and spike rival ones; see QA-URCHIN-AI.)
**Report:** match length, and whether 24 rings is the right course length.

### QA-HIJACK-MODE ⬜ — "Hijack" has never been opened (score agrees; the arena will not)
**Source:** `GameModes.Hijack = 46`, Urchin steal race on the Switchyard (rail thirds and
spiked "burrs"); first team to **750** stolen wins. "Nothing … has been run in the Unity editor."
Prism ownership in the Switchyard **is replicated since PR #972** (QA-HIJACK-OWNERSHIP-SYNC
covers the two-machine checks); before it, ride speed, the objective arrow and AI rail choice
differed on each machine. Reference:
`_Scripts/Controller/Arcade/HIJACK.md` § 9.

1. Open `MinigameHijack.unity`: four Switchyard configs on **Intensity Wise**; `rule =
   HijackScoringRule`. Save it and report whether git shows a scene change.
2. Launch at intensity 1, **host only** first. Grind onto a rail section of a **rival** colour;
   watch your score crawl up and your speed.
3. Tap the spike trigger into a rival burr. Then ride a rail of **your own** colour.
4. Grind a rail to its end **without steering**.
5. Attach to a burr and roll across its spines.
6. Watch AI Urchins for two minutes.
7. Fall about 375 behind and watch your element flowers.
8. Play to 750.
9. Now with a client (Multiplayer Play Mode): play a minute, then compare the **scores** on both
   windows, and note which rail sections look fast or slow on each.
10. Regression: fly the Urchin in freestyle.

**PASS:** riding a rival section scores steadily at a visible crawl (about 20) and your own
colour rides fast (about 300) and scores nothing; a spike tap into a rival burr jumps the score
by 100+ and snaps your speed back; reaching a rail's end launches you into the burr without
steering; rolling a burr grows your spines and flips rival ones one per hop; AI grind at full
speed, launch off ends, and their team's score climbs without circling pilots or the core
crystal; a trailing team's flowers fill about 3 levels; the match ends at 750 with "HEIST TIME";
the **scores agree** on both machines; freestyle Urchin unchanged.
**FAIL:** stealing that does not score · own-colour riding that scores · a launch that needs
steering to hit the burr · AI that orbit or crawl · scores that differ between machines.
**Known, do not fail on:** the Urchin's ability icons are white placeholders. Rail colours, ride
speeds, the arrow and (since PR #1004, QA-HIJACK-SHIELD-SYNC) rail prism SHIELDS must now AGREE
between machines - a shield on one machine and not the other is a failure.

## Priority 0 — Block J: the two party cards — Multiplayer Freestyle and Online Duel for the Cell

Neither card has ever been opened in Unity, and until this refresh neither was tracked here or in
`Docs/UNITY_VERIFICATION_CHECKLIST.md` (re-audit G37). Both are on `AllGames.asset` and
`LaunchPartyAllGames.asset` but on **neither** live roster (`ArcadeGames.asset`,
`ArenaGames.asset`), so the first thing each item checks is whether a player can reach the card at
all. Both need **two players**, so run them in one Multiplayer Play Mode session.

**Two-player setup used by every two-player item in Blocks J and Q:**
1. Open **Window ▸ Multiplayer ▸ Multiplayer Play Mode**. Tick **Player 2** and type a tag for it
   (for example `P2`) **before** you press Play. Without a tag the second window signs in as the
   same account as other untagged windows and the invite in step 3 never arrives.
2. Open `Menu_Main` and press Play. Wait until both windows show the main menu.
3. In the main window, click the **+** slot on the party panel and pick the other player. In the
   Player 2 window, press **Accept** on the invite. Wait (up to 15 seconds) until both windows show
   two people in the party. The main window is the **host**; the Player 2 window is the **client**.

### QA-PARTY-FREESTYLE-MODE ⬜ — "Multiplayer Freestyle" has never been opened
**Source:** `GameModes.MultiplayerFreestyle = 28`; card `ArcadeGameMultiplayerFreestyle.asset`;
scene `MinigameFreestyleMultiplayer_Gameplay.unity`; `MultiplayerFreestyleController`. Status read
off the card and scene in `Docs/SCENES.md` § Multiplayer Freestyle (PR #1000, whose step 4 is
folded in here): no scoring, no natural end (the scene's turn-monitor list is empty), each player
starts on their own countdown, 2–3 players, no AI fill, six hulls on the card. Re-audit G37.

1. In the Project window, select `Assets/_SO_Assets/Games/ArcadeGameMultiplayerFreestyle.asset`.
   Read **Vessels**, **Min Players Allowed**, **Max Players Allowed** and **Scene Name**.
2. Open `MinigameFreestyleMultiplayer_Gameplay.unity` (type its name in the Project window's search
   box). Select the object that has `TurnMonitorController` and look at its **Monitors** list. Look
   at the Console for red errors and at the Hierarchy for any `Missing (Mono Script)`.
3. Do the two-player setup at the top of Block J.
4. In the host window, open the **Arcade** screen and then the **Arena** screen and look for a card
   named **Multiplayer Freestyle**. If neither screen has it, stop: mark this item ⛔ BLOCKED and
   write "no Multiplayer Freestyle card on the Arcade or Arena screen" in the notes.
5. Open the card, pick a ship, and launch. Watch both windows through the countdown.
6. In each window, fly for a minute: lay trail, fly through the other player's trail, and find the
   other player's ship.
7. Keep flying for five minutes and watch for any end-of-round or winner screen.
8. In the client window, leave the match with the in-game leave button. Watch both windows.

**PASS:** the card lists six ships (Dolphin, Manta, Rhino, Sparrow, Serpent, Squirrel), 2 and 3
players, and that scene name; the scene opens with no red errors or missing scripts and its
Monitors list is empty; both windows get through the countdown and each player can fly; each
window shows the other player's ship and trail moving smoothly in the same place; the match keeps
running with no end screen; the leaving player returns to the menu and the host keeps flying with
no errors.
**FAIL:** a red error or `Missing (Mono Script)` · a window stuck on the connecting panel or the
countdown · a player who never gets control · the other player's ship invisible, frozen or in the
wrong place · an end screen or a winner banner · the leaving player stranded on a black screen, or
an error in either window when they leave.
**Known, do not fail on:** there is no score, no timer and no end by design (the card says "No
rules, time, or score"); intensity changes nothing (the scene has one cell config); no AI fills an
empty seat; the card has no background render of its own yet.
**Report:** whether the card was reachable from the menu (step 4), and if not, how (if at all) you
launched the scene.

### QA-PARTY-DUEL-MODE ⬜ — "Online Duel for the Cell" has never been opened
**Source:** `GameModes.OnlineDuelForTheCell = 29`; card `ArcadeGameOnlineDuelForTheCell.asset`;
scene `MinigameDuelForCellMultiplayer_Gameplay.unity`; `OnlineDuelForTheCellController`. Status in
`Docs/SCENES.md` § Multiplayer Cellular Duel (PR #1000, whose step 3 is folded in here): exactly two
players; two rounds of one 120 s turn; at round 2 the controller swaps the two ships
(`NetworkObject.ChangeOwnership` + `gameData.SwapVessels`) and swaps back on replay; most mass wins
(`VolumeCreated` + `HostileVolumeDestroyed` + `FriendlyVolumeDestroyed`, ×1 each, no scoring-rule
asset); no AI fill. The card's hull list is still serialized under the retired `Captains` key, so
**Vessels** is expected to read empty and each pilot flies the ship they brought. The swap reads
`Players[0]` and `Players[1]` only. The Urchin's swap throw was fixed by PR #973. Re-audit G37/G38.

1. Select `Assets/_SO_Assets/Games/ArcadeGameOnlineDuelForTheCell.asset`. Look at **Vessels** and
   write down exactly what it shows: an empty list, six ships, or "Type mismatch" / None entries.
   Read **Min/Max Players Allowed** and **Min/Max Intensity**.
2. Open `MinigameDuelForCellMultiplayer_Gameplay.unity`. Look at the Console for red errors and the
   Hierarchy for `Missing (Mono Script)`. Select the object that has `NetworkTimeBasedTurnMonitor`
   and read its duration.
3. Do the two-player setup at the top of Block J. In each window, take control of the ship (click
   the centre of the screen, or press **Y** on a gamepad), open the Vessel Changer and pick a
   **different** ship in each window (for example Sparrow in the host, Dolphin in the client).
4. In the host window, look for an **Online Duel for the Cell** card on the Arcade and Arena
   screens. If neither has it, stop: mark this item ⛔ BLOCKED and write "no Duel card on the Arcade
   or Arena screen".
5. Launch it at intensity 1. Write down which ship each window flies.
6. Round 1: in both windows, lay trail and fly through the other player's trail for the whole two
   minutes. Watch both players' scores in both windows.
7. When round 2 starts, look at each window: which ship are you flying now? Steer it and check the
   camera follows it.
8. Play round 2 to the end. Read the end screen in both windows.
9. Press **Play Again** (or Ready) in both windows. Check which ship each window flies.
10. Go back to the menu, pick the **Urchin** in one window, and repeat steps 5–7.
11. Leave the party so you are alone, and try to launch the card.

**PASS:** the card reads 2 / 2 players and intensity 1–2; the scene opens clean and the turn lasts
120 seconds; both players start round 1 in the ships they picked; scores rise when you lay trail and
when you destroy the other player's trail, and match in both windows; at round 2 each player is
flying the ship the **other** player flew in round 1, can steer it, and the camera follows it; the
match ends after round 2 and both windows name the same winner, the one with the higher score; after
Play Again each player is back in their original ship; the Urchin swaps with no error; a
one-player launch is refused (or not offered) rather than starting.
**FAIL:** a red error or missing script · a window stuck on the connecting panel · after the swap, a
player steering the wrong ship, a camera left on the old ship, or dead controls · scores that differ
between windows · the lower score named winner, or the two windows naming different winners ·
"No network object found in vessel", `NullReferenceException` or `ArgumentOutOfRangeException` in
the Console · a one-player match that starts.
**Known, do not fail on:** intensity 2 looks the same as 1 (one cell config); no AI fills the second
seat; the card has no background render of its own yet.
**Report:** what **Vessels** shows (step 1) — that decides whether the card's ship list must be
re-authored — and whether the card was reachable from the menu (step 4).

---

## Priority 0 — Block P: the parallel PRs' own checks (merged 2026-10-06)

All eleven PRs from the 2026-10-05 audit have merged. The six below (#971–#976) have their
items in this block; their "Known, do not fail on" lines have been removed from the items above.
The other five (#964–#970) have no items of their own yet - the lines they made untrue are
corrected above, and their checks wait for the next `/qa-backlog` run.

| Parallel PR (subject) | What it changes | Items whose "Known, do not fail on" lines it retires |
|---|---|---|
| Mode generator gates | Generators stop reverting rendered card art and stop emitting the retired call-to-action key | none in play (offline gates); spot-check card art in QA-ARCADE-ROSTER-SMOKE |
| Wildlife Liberation / Tollway | Kill target vs doc, Clawfish heart, Tollway flora-family check | QA-WILDLIFE-LIBERATION, QA-TOLLWAY-MODE |
| Objective arrows | Arrows for Bends, Cleave, Sirocco, Wildlife Liberation, Brood Rush, Scurry (+ Maelstrom lobby) | QA-BENDS-MODE, QA-CLEAVE-MODE, QA-SIROCCO-MODE, QA-WILDLIFE-LIBERATION, QA-BROODRUSH-ARENA |
| Toasts — **merged #976** → QA-MODE-TOASTS | Pop-up message configs for Rampage, Cleave, Salvo, Switchback, Headlong, Redline, Breakwater, Skein, Hijack, Wildlife Liberation, Astro League | every mode item that listed "no pop-up messages" (retired) |
| Registration drift | Scarab class list, Butterfly class icons, orphan scenes/cards, retired keys, stale preview copy | QA-SCARAB-VESSEL, QA-BUTTERFLY-VESSEL, QA-ASTROLEAGUE-REWORK, QA-BENDS-MODE |
| Urchin HUD — **merged #973** → QA-URCHIN-HUD | A four-icon ability row for the Urchin | QA-URCHIN-VESSEL, QA-SKEIN-MODE, QA-HIJACK-MODE, QA-ARENA-PILOT-SWAP, QA-REGATTA-ARENA (retired) |
| Hull ability rows — **merged #971** → QA-HULL-ABILITY-ROWS | Missing/borrowed icons on the Rhino, Serpent, Scarab rows | QA-SCARAB-VESSEL, QA-CLEAVE-MODE, QA-HEADLONG-MODE (retired) |
| Urchin AI — **merged #975** → QA-URCHIN-AI | AI Urchins that spike, track and ride (Skein/Hijack backfill and solo play) | QA-URCHIN-VESSEL, QA-SKEIN-MODE, QA-HIJACK-MODE (retired) |
| Fleet AI boost — **merged #974** → QA-AI-BOOST-POLICIES | AI boost for the hulls that could not use it (Regatta: Sparrow, Serpent, Dolphin, Scarab; plus the Squirrel outside Skim Race) | QA-REGATTA-ARENA (retired) |
| Hijack replication — **merged #972** → QA-HIJACK-OWNERSHIP-SYNC | Prism ownership replicated, so ride speed, arrow and AI rail choice agree across machines | QA-HIJACK-MODE (retired) |
| Audio slots | Empty sound slots on Manta, Urchin, Serpent, Scarab, Butterfly | QA-BLOOMRUSH-MODE, QA-REDLINE-MODE, QA-SCARAB-VESSEL, QA-BUTTERFLY-VESSEL, QA-URCHIN-VESSEL |

<!-- qa-parallel-pr-checks: new items from the PRs above go between these markers -->

### QA-URCHIN-HUD ⬜ — the Urchin finally has its four ability icons and gauges
**Source:** PR #973 (`Tools/Build/author_urchin_hud.py`; `UrchinHUDVariant.prefab`,
`Urchin.prefab`, `UrchinVesselHUDController`). Prefabs written as text and never imported; the
Urchin flew with no HUD at all before this. Reference: `Docs/UNITY_VERIFICATION_CHECKLIST.md`
"Urchin HUD variant and four-icon row".

1. In the Project window, open `Assets/_Prefabs/UI Elements/VesselHUD/UrchinHUDVariant.prefab`.
   Check the Inspector header says it is a variant of `VesselHUDPrefab`. Select the object that
   has `UrchinVesselHUDView` and count the entries under **Ability Icons**.
2. Open `Assets/_Prefabs/Spacevessels/Urchin.prefab`. On its `VesselStatus` component, read the
   **Vessel HUD Controller** field. Look for any field that says **Missing**.
3. Run **FrogletTools ▸ Vessels ▸ Audit Vessel Ability Rows**, then **FrogletTools ▸ Vessels ▸
   Audit Ability Lockups**. Find the Urchin lines in each report.
4. Start freestyle (click the centre of the main menu screen, or press **Y** on a gamepad), open
   the Vessel Changer and pick the **Urchin**. Look at the bottom-right of the screen.
5. Press the **right trigger** to fire spikes until the first card's bar drops; wait and watch it.
6. Fly into a trail so the Urchin latches on; then press **B** to slip off.
7. Press the **left trigger** to project a track; press it again straight away.
8. Raise any element to level 5 (fly through elemental crystals) and look at that element's card.
9. With **Window ▸ Multiplayer ▸ Multiplayer Play Mode** open a second player window. Fly an
   Urchin in one window and look at the other window's screen. Then swap away from the Urchin and
   back in the first window and fire spikes once.

**PASS:** the variant shows 4 ability icons (Charge, Mass, Space, Time) with the ammo and riding
bars filled in; `Urchin.prefab`'s Vessel HUD Controller is set and nothing says Missing; both
auditors pass the Urchin (4/4, in order); in play, four cards sit bottom-right with element
flowers above them and a locked crystal card to their left; control hints read RT on the 1st
card, LT on the 3rd and B on the 4th, none on the 2nd; the 1st card's bar drops when you fire and
refills over time; the 2nd card fills within a blink when you latch and empties when you slip
(never stuck half-full); the 3rd card shows a clockwise recharge sweep for about 20 seconds and
a second press does nothing; a level-5 element shows a badge on its card; the other window shows
no Urchin HUD for someone else's ship; after swapping back, one spike shot drops the bar once.
**FAIL:** a missing icon, Missing reference or "VesselHUDController is null on Urchin" warning in
the Console · an error (red text) when the Urchin spawns · cards in the wrong order or a hint under
the wrong card · a bar that never moves · a riding bar parked half-full · a track press that works
during the recharge · another player's HUD drawn on your screen · the bar dropping twice per shot
after a swap.
**Known, do not fail on:** the four icons are plain white placeholder outlines (final art
pending); holding the spike trigger shows no charge-up bar (not built yet).

### QA-HULL-ABILITY-ROWS ⬜ — Rhino, Serpent and Scarab show their own ability icons
**Source:** PR #971 (`Tools/Build/author_hull_ability_rows.py`, `author_hull_icon_placeholders.py`).
Before it the Rhino bound 0 of 4 icons, the Serpent 1 of 4, and the Scarab wore the Sparrow's
pictures. Prefabs written as text, never imported; no code changed.

1. Open `RhinoHUDVariant`, `SerpentHUDVariant`, `ScarabHUDVariant` (in `Assets/_Prefabs/UI
   Elements/VesselHUD/`) and `Assets/_Prefabs/Spacevessels/Serpent.prefab`, one at a time.
   Watch the Console for red errors. In the Project window, check the new `*-PLACEHOLDER.png`
   files under `Assets/_Graphics/Icons/AbilityIcons/Rhino`, `/Serpent` and `/Scarab` show as
   Sprite images.
2. Run **FrogletTools ▸ Vessels ▸ Audit Vessel Ability Rows** and read the Rhino, Serpent and
   Scarab lines. Run **FrogletTools ▸ Vessels ▸ Audit Ability Lockups**.
3. Start freestyle and pick the **Rhino** in the Vessel Changer. Read the four cards bottom-right,
   left to right.
4. Pick the **Serpent**. Read the four cards and their control hints. Fire the rifle (right
   trigger) and watch the first card.
5. Pick the **Scarab**. Read the four cards. Use the blast, the switch and the ball and watch the
   card colours.

**PASS:** no import errors and the placeholders are Sprites; the row auditor reports Rhino Mass and
Time bound and Serpent Charge, Space and Time bound, and the only complaints left are "open design
slot / no icon" lines for Rhino Charge, Rhino Space and Serpent Mass; the lockup auditor reports no
icon that does not fit; the Rhino reads LOCKED / Trail Slabs / LOCKED / Ramp Spool with no old
Rhino chrome in the bottom-right; the Serpent reads Sniper Shot (RT) / LOCKED / Scope (LT) /
Pellets (A) and firing the rifle sweeps a cooldown shadow over the first card; the Scarab shows a
blast, a ring, a ball and a dial (not missiles or bullets) and its cards still change colour as
before.
**FAIL:** a red error on import · a placeholder that imports as a texture, not a Sprite · a hull
with a missing card or an icon on the wrong card · Sparrow pictures still on the Scarab · old
Rhino chrome back on screen · the Serpent's first card not sweeping after a shot.
**Known, do not fail on:** all eight icons are plain white placeholders; the Serpent may show a
"pitch not uniform" line in the row auditor (it measures spacing only between bound icons and
skips the locked slot - not a real layout fault); the LOCKED cards are open design slots with no
ability behind them.

### QA-AI-BOOST-POLICIES ⬜ — AI ships now boost on the straights
**Source:** PR #974 (`AIPilot.boostPolicy`; five assets in `Assets/_SO_Assets/AI Boost Policies/`;
design `Assets/_Scripts/Controller/AI/AI_BOOST.md`). Before it only the Manta and Rhino AI could
boost, so AI Sparrows, Serpents, Dolphins and Scarabs finished Regatta but never won. Code
type-checked offline only; never compiled in Unity.

1. Wait for Unity to finish compiling. Open `Sparrow`, `Serpent`, `Dolphin`, `Squirrel` and
   `Scarab.prefab` (in `Assets/_Prefabs/Spacevessels/`) and find **Boost Policy** on each one's
   `AIPilot` component.
2. From the **Arena** screen launch **Regatta** with AI in Sparrow, Serpent, Dolphin and Scarab
   seats (tap an ally's chip on the launch panel to change its ship). Watch each AI on a long
   straight and as it approaches a ring. Note each finishing time.
3. Launch **Scurry** or **Brood Rush** with an AI **Squirrel** and follow it on a long straight.
4. Go back to the main menu and wait a minute in freestyle with the menu ship flying itself.
5. Launch any race with an AI **Scarab** whose Time element is 5 or more (in Regatta, check the
   card's starting elements) and watch it on straights.
6. Regression: play **The Bends** with an AI Dolphin, **Dog Fight** with an AI Sparrow, **Skim
   Race** and **Astro League** with AI.
7. In an Arena match, use **D-pad right** to swap into an AI teammate's ship while it is
   boosting.

**PASS:** each prefab's Boost Policy shows its matching asset; in Regatta the Sparrow boosts
between rings and lets go before each ring, the Serpent spends its pellets on long legs and not
right before a ring, and the Dolphin's burst fires mid-leg rather than after the ring; those three
finish closer to the Manta and Rhino than before; the AI Squirrel lays one boost ring ahead on a
long straight, flies through its middle without stopping, and speeds up; the menu ship never lays
rings; the Scarab at Time 5+ surges forward on straights no more than about every 2 seconds, and
not at all below Time 5; Bends, Dog Fight, Skim Race and Astro League AI behave as before; after a
mid-boost swap the ship stops boosting when you let go.
**FAIL:** a Boost Policy field empty or Missing · an AI that boosts straight into rings or turns ·
a Squirrel that crashes into its own ring and stops dead, or lays rings in the menu · a Scarab
surge below Time 5 · a changed AI in the regression modes · a ship stuck boosting after a swap.
**Known, do not fail on:** the Squirrel's ring is only visible on the host's machine (AI actions
run on the host); in Regatta and Skim Race the AI Squirrel flies the Skim Race racing pilot
instead and does not lay rings. **Report:** each hull's Regatta finishing time.

### QA-URCHIN-AI ⬜ — AI Urchins race Skein and raid Hijack using their whole kit
**Source:** PR #975 (`UrchinAutopilotDriver`, `Assets/Resources/UrchinAutopilotConfig.asset`,
tests `UrchinRailAssessmentTests`). Before it the AI Urchin only steered, so Skein above
intensity 1 had no working solo play. Offline compile and simulated run only.

1. Wait for Unity to finish compiling. Select `Assets/Resources/UrchinAutopilotConfig.asset` and
   check the Inspector shows numbers, not "missing script".
2. Open **Window ▸ General ▸ Test Runner**, choose **EditMode**, and run
   `UrchinRailAssessmentTests`.
3. From the **Arcade** screen launch **Skein** at intensity **3** or **4** with 2–4 players so AI
   fill the seats. Follow one AI Urchin for three minutes (watch the scoreboard's ring count).
4. Launch **Skein** at intensity **1** with AI and let it run to the end.
5. Launch **Hijack** solo with AI and watch an AI raider for two minutes.
6. Regression: launch **Regatta** and **Broadside** with AI Urchins.

**PASS:** the config shows values; all 13 tests pass; at intensity 3–4 the AI's ring count rises
steadily and it finishes, and you see it do at least two of these: slip off a cable that does not
lead to its next ring and fly straight at the ring; tap spikes on a cable of a rival's colour so
the cable turns its colour (instead of crawling along it slowly); turn round to ride the other way
when its ring is behind it; lay a track on a long straight and launch off it toward the ring; at
intensity 1 the AI rides through the collars and finishes; Hijack raiders grind, spike rival rail
sections and burrs, slip out when parked, and their team's steal count climbs (they never lay a
track in Hijack); Regatta and Broadside Urchins behave as before.
**FAIL:** "missing script" on the config · a failing test · an AI that stops scoring rings, sits
crawling on a rival cable, or never finishes (no solo play) · Hijack raiders that orbit or stall ·
a changed Regatta/Broadside Urchin.
**Known, do not fail on:** a track laid only occasionally (it fires only on long lined-up legs).
**Report:** how long a solo Skein match takes at intensity 4.

### QA-HIJACK-OWNERSHIP-SYNC ⬜ — Hijack's rail and burr colours agree on every machine
**Source:** PR #972 (`HijackOwnershipLedger`, four RPCs on `HijackController`; `HIJACK.md` §11).
Hijack is scored on who owns each prism, but ownership used to be local to each machine.
Compiled and simulated offline (two peers, 20/20) only.

1. Open `Assets/_Scenes/Multiplayer Scenes/MinigameHijack.unity`. Select the Hijack controller
   object and find the **Ownership Sync** header. Watch the Console for red errors.
2. Open **Window ▸ Multiplayer ▸ Multiplayer Play Mode** with one extra player. Launch Hijack with
   the two players on **different** teams.
3. In the second window, grind a rail section of the other team's colour and spike a burr. Watch
   the first window.
4. In the first window, steal a burr. Watch the second window's burr and its objective arrow.
5. In one window, watch the other player grinding a rival rail for ten seconds.
6. In the second window, empty a burr completely. Watch where the AI raiders go.
7. Stop. Start again with only the first window, steal one or two burrs, **then** join the second
   player mid-match. Look at those burrs in the second window.
8. Compare both windows' scores. Then play one full match solo with AI.

**PASS:** the header shows flush 0.1 and grace 0.5 and there are no errors; the second player's
stolen prisms turn their colour in the first window within a blink, and the first player rides
fast over them; a burr stolen in the first window flips in the second, and its arrow moves on to a
burr that still has loot; prisms the other player steals flip and STAY flipped; AI raiders stop
choosing the rail to an emptied burr; a player joining late sees the already-stolen burrs in the
thief's colour; scores agree; the solo match plays as before with no warnings.
**FAIL:** a red error or missing-script line · colours that differ between the two windows ·
stolen prisms that flip and then snap back after about half a second · an arrow pointing at an
empty burr · AI raiders still heading for an emptied burr · a late joiner seeing the original
colours · scores that differ.
(Shields are synced too since PR #1004; their two-machine checks are QA-HIJACK-SHIELD-SYNC.)

### QA-MODE-TOASTS ⬜ — pop-up messages in eleven modes that had none
**Source:** PR #976 (eleven `GameToastConfig_*` assets, `DomainRaceToasts`, new publishers in the
Wildlife Liberation, Astro League and Salvo controllers, a stolen-prisms counter for Hijack;
`Assets/_Scripts/UI/GameToastSystem/GAME_TOASTS.md`). The PR's own description is a copy of
another PR's, so these steps were written from the change itself. Never run in Unity.

1. Wait for Unity to finish compiling. Select `Assets/_SO_Assets/Game Toasts/GameToastLibrary.asset`
   and check it lists configs for Rampage, Cleave, Salvo, Switchback, Headlong, Redline,
   Breakwater, Skein, Hijack, Wildlife Liberation and Astro League, none shown as Missing.
2. Play **Rampage** with AI to the end. Watch the pop-up messages (top of the screen).
3. Play **Salvo** 2v2 (you plus an AI teammate). Let your teammate pick up a white crystal. Then
   play a Salvo match alone on your team.
4. Play **Hijack** with AI to at least 200 stolen.
5. Play one gate race: **Switchback** or **Skein**, then one lapped race: **Headlong**, **Redline**
   or **Breakwater**.
6. Play **Cleave** and **Wildlife Liberation**. In Wildlife Liberation, fly into the core cage.
7. Play **Astro League** until a goal, a match point, and if possible a draw at full time.
8. Repeat one of the races with **Window ▸ Multiplayer ▸ Multiplayer Play Mode** (two windows),
   and once leave and rejoin (or press Ready to replay) mid-match.

**PASS:** every listed config is present; in Rampage, Salvo and Hijack a message appears when the
leading team reaches a quarter and half of the target, and when the lead changes after that (no
more than one lead message per 8 seconds); in Salvo your teammate's crystal pickup shows "<name>
reloaded the wing" and refills your missiles, and nothing appears when you are alone on your
team; Hijack shows "<name> has stolen N prisms" every 100; the gate races show halfway, lead
change and home stretch messages, and the lapped races also show one "final lap" message;
Cleave and Wildlife Liberation show their quarter / half / lead messages with the screen shake;
the first pilot into Wildlife's core cage triggers "<name> broke into the core!" once per match;
Astro League shows a goal message with the scorer, "MATCH POINT" one goal from the limit, and
"Golden goal" on a draw; with two windows each message appears once in each window; a rejoin or
replay does not dump a burst of old messages.
**FAIL:** a config missing from the library · a mode in the list with no messages at all · a
message with a blank or `{0}` in it · the wing-reload message for a solo team · the same message
twice in one window · a burst of stale messages after a rejoin or replay.
**Judgement call:** whether the message wording and frequency feel right; report any that are
noisy.

<!-- /qa-parallel-pr-checks -->

---

## Priority 0 — Block Q: the round-3 PRs' own checks (merged 2026-10-08)

PRs #998–#1007 from the 2026-10-08 re-audit merged without anyone opening them in Unity. The items
below cover #1007, #998, #1005, #1004, #1002 and #1001, ordered from the widest reach to the
narrowest. Three of them need **two players** (the two-player setup at the top of Block J) —
QA-COMBAT-PETAL-DRAIN-NET, QA-URCHIN-TWO-PEER and QA-HIJACK-SHIELD-SYNC — so run those three in
one session. #1000 changed docs only; its two Inspector checks are folded into Block J. #999,
#1006 and the other PRs merged the same week carry no Editor checklist of their own and are not
itemised yet.

<!-- qa-round3-pr-checks: new items from the round-3 PRs go between these markers -->

### QA-COMBAT-PETAL-DRAIN-NET ⬜ — a shot pilot loses petals once, on every machine, and a client's hit scores once
**Source:** PR #1007 (`ElementalTransfer.ApplyAllAuthoritative` / `RouteFor`, the relay RPCs on
`NetworkVesselImpactor`, `CombatHitDrain.Apply`; `Docs/ELEMENTAL_ECONOMY.md` §7–§8,
`_Scripts/Controller/Arcade/DOGFIGHT.md`). A combat hit's petal drain is now settled once, on the
victim's own machine. Before it, an AI that shot a client never drained the client, and in human
vs human the victim lost petals only if its own copy of the shot connected. Reaches every armed
mode (Dog Fight, Salvo, Broadside, The Bends, Undertow). Compiled offline only; Netcode's RPC code
generation has never run on it. **Also checks a question the PR left open:**
`StatsManager.CombatHitLanded` credits every hit the host sees, and the host also replays a client's
shots, so a client's hit may be scored twice — once from the host's replay and once from the
client's own `Player.ReportCombatHit_ServerRpc` (step 9).

1. Open the project and wait for Unity to finish compiling. Look at the Console for red errors,
   especially any naming `NetworkVesselImpactor` or an RPC.
2. Open **Window ▸ General ▸ Test Runner**, choose **EditMode**, and run
   `ElementalTransferRouteTests` and `CombatHitDrainTests`.
3. Play **Dog Fight** alone against AI. Let an AI hit you, and watch your four element flowers
   (above your ability cards) and the crystals that fly off your ship.
4. Do the two-player setup at the top of Block J, then launch **Dog Fight** with AI filling the
   other seats. In the client window, fly through elemental crystals until the client's flowers
   are coloured, not grey.
5. **AI shoots the client.** Fly the client in front of an AI ship and take hits. Watch the client's
   flowers in the client window, and count the crystals that leave the client's ship in **both**
   windows.
6. **Host shoots the client.** Have the host hit the client a few times. Watch the client's flowers
   and the crystals in both windows.
7. **Client shoots the host.** Swap roles and repeat step 6.
8. **Client shoots an AI.** Watch that AI's flowers (or its ship's shape changing) and its crystals
   in both windows.
9. **Is a client's hit scored twice?** Note the host's score. Have the host hit the client with
   exactly **one** missile, dead-on, and write down how much the host's score rises. Then have the
   client hit the host with exactly one missile and write down how much the client's score rises,
   in both windows. Then have the client land a short burst of bullets on the host while you count
   the hits, and compare with how much the client's score rose.
10. **One shot, four petals.** Hit a pilot who has petals in all four elements with one missile
    blast. Count the crystals that fly off in each window.
11. **Ward.** Hit a pilot while it is immune to debuffs (just after it respawns or uses a ward).
12. Optional: in **Hijack** with two players, spike the other pilot just as a ship swap happens.

**PASS:** no red errors; both test classes pass; solo play drains and ejects as before; when an AI
hits the client, the client's flowers step down in the client window and the same number of
crystals leave its ship in both windows; host→client and client→host hits step the victim's flowers
down in both windows a moment after the hit, with one crystal per petal in both windows, and only on
hits the shooter's score counted; client→AI hits drain the AI in both windows; **a client's missile
raises the client's score by the same amount as the host's identical missile (50 in Dog Fight), and
each client bullet hit adds 1, in both windows**; one blast on a four-element pilot ejects exactly 4
crystals in each window and each flower steps down once; a warded pilot loses nothing; nothing
throws in Hijack.
**FAIL:** a red error or RPC error · a failing test · the client's flowers not moving when an AI
shoots it (the bug this PR fixes) · flowers stepping down in one window only · different crystal
counts in the two windows · 8 crystals from one four-element blast · **a client's hit scoring twice
(a missile adding 100, or bullets adding 2 each)** · a warded pilot losing petals · solo play
changed.
**Known, do not fail on:** a pilot whose flowers are all grey has nothing to lose, so hitting it
ejects nothing; the victim's own screen still shows no hit flash (hit feedback is not replicated);
the Serpent's sniper round, explosion debuffs and skimmer steals still settle on each machine
separately (named in the PR as not covered yet), so do not judge them here.
**Report:** the two score jumps from step 9. If the client's is double, say so in the notes — it
points at `StatsManager.CombatHitLanded`.

### QA-CLEAVE-PREVIEW-RING ⬜ — the Cleave card preview stands you on the real spawn ring at every intensity
**Source:** PR #998 (`Tools/Build/author_preview_spawns.py`,
`ModePreviewDefinitionSO.ResolveSpawnRingRadiusFloor`, `ModePreviewArena`, `ModePreviewSession`;
tests `ModePreviewSpawnFloorTests`; `_Scripts/Controller/Arcade/CLEAVE.md`). The preview used to
stand the pilot 3150 out on all four rungs, about three times too far on the small rungs 3 and 4.
The change runs through every card's preview, so step 6 checks two others.

1. Wait for Unity to finish compiling. In **Window ▸ General ▸ Test Runner ▸ EditMode**, run
   `ModePreviewSpawnFloorTests`.
2. Select `Assets/_SO_Assets/Mode Previews/ModePreview_Cleave.asset` and read **Spawn Ring Radius
   Floor By Intensity**. Then select `ModePreview_Rampage.asset` in the same folder and read the
   same field.
3. Open **FrogletTools ▸ Toolbox ▸ Logging** and turn on the **ArcadeLaunch** channel.
4. Open `Menu_Main`, press Play, open the **Arcade** screen, open the **Cleave** card and start
   **Test Flight** at intensity 1. Find the `[ModePreview] Spawn for Cleave` line in the Console.
5. Step the intensity row to **3**, then **4**, then **2**. Each time, watch where the ship arrives
   and read the new log line.
6. Open the **Rampage** and **Wrecking Ball** cards' previews.

**PASS:** all 9 tests pass; Cleave's list reads 3150, 3150, 1050, 1050 and Rampage's list is empty;
at intensities 1 and 2 the log line says `floor=3150` and the ship arrives about 3150 from the
centre; at 3 and 4 the arena rebuilds (the Cage), the line says `floor=1050`, and the ship arrives
about 1050 from the centre, outside the cage; the Rampage and Wrecking Ball previews put the ship
where they did before.
**FAIL:** a failing test · different numbers in either list · at intensity 3 or 4 the ship still far
out (about 3150) or inside the cage · the arena not rebuilding when the intensity changes · another
card's preview ship arriving somewhere new.

### QA-URCHIN-TWO-PEER ⬜ — two machines agree on an Urchin's spike chains, its steals, and a quick double swap
**Source:** PR #1005, `URCHIN_BACKLOG.md` U1, U2 and U13 (in
`_Scripts/Controller/Vessel/R_VesselActions/`). U1 and U2 blocked shipping the Urchin in
multiplayer: each machine ran the spike chain off its own copy of the pilot's Charge level, and a
client's steal credited the thief but never took the prisms off the victim. Now the chain reads the
replicated level (`R_VesselElementalAbilityHandler.ReplicatedLevel`), and
`ReportPrismStolen_ServerRpc` names the victim so the host debits it (`StatsManager.DebitPrismSteal`;
the trade is `Docs/ScoringSystem/BUGS.md` B19). U13: a client's second quick ship swap used to
start while its player still pointed at the despawned ship. Compiled offline only.

1. Do the two-player setup at the top of Block J. Launch **Hijack** with the two players on
   **different** teams.
2. **U1, Charge 8+.** In the client window, fly through Charge crystals until the client's Charge
   flower reads 8 or more. Tap the right trigger into a stretch of rail of the host's colour, then
   hold it for 2.5 seconds and release. In **both** windows, count how many waves the chain spreads
   in and how far it reaches.
3. **U1, Charge 0.** Start a fresh match and repeat step 2 before collecting any Charge.
4. **U2, client steals.** Before stealing, in the host window, select the host's player object in the
   Hierarchy and find its **Round Stats** component; write down **Prisms Remaining** and **Volume
   Remaining**. Do the same for the client's player object. Then, with the client, steal about 20
   of the host's prisms and read the four numbers again in the host window.
5. **U2, host steals.** With the host, steal about 20 of the client's prisms and read the numbers
   again, in both windows.
6. **U13.** Go back to `Menu_Main` with both players still in the party. In the client window, take
   control of the ship and use the Vessel Changer twice in quick succession. Then open any Arcade
   card's preview in the client window and swap ship there.

**PASS:** at Charge 8+ both windows show the same number of chain waves and the same reach, and at
Charge 0 both show the same short resting chain; after the client's steal the host's Prisms Remaining
and Volume Remaining fall by about the same amount the client's rose; after the host's steal the
client's numbers move once (not twice); the second quick swap waits until the first new ship is in
place, with no `MissingReferenceException`, and the preview swap still lands.
**FAIL:** a longer or shorter chain in one window than the other · the victim's remaining numbers
not moving after a client steal (the U2 bug) · a tally moving twice for one steal · a
`MissingReferenceException` or a stuck swap after the double swap.
**Known, do not fail on:** a pilot whose Charge is below zero (a Charge debuff) now gets the short
resting chain on every machine — that is the trade the fix chose.

### QA-URCHIN-BACKLOG-SOLO ⬜ — five Urchin fixes you can check alone: stuck spikes, cell rides, guns, shared materials, double Slip
**Source:** PR #1005, `URCHIN_BACKLOG.md` U4, U5, U6, U14, U16. U4: a spike that stuck kept
converting that frame's hits. U5: every authored cell environment declared its world a 1D trail, so
the Urchin tried to rail-grind it (now `PrismscapeDimension.Volume`). U6: a dead `GunsActive` flag
removed. U14: `ApplyShipMaterialToSlots` cloned every material (now `sharedMaterials`). U16: a
cancelled Slip ghost could make the hull solid again mid-way through the next ghost. Compiled
offline only.

1. Start freestyle (click the centre of the main menu screen, or press **Y** on a gamepad) and pick
   the **Urchin** in the Vessel Changer.
2. **U4.** Fire Chain Spikes (right trigger) into a dense mass of prisms. Watch a spike that sticks.
3. **U5.** Still in freestyle, fly the **Cell Selector** toy (about 300° around the membrane ring)
   and pick a world with a built structure (for example **Atlantis**, **Geode** or the
   **Boneyard** if offered, otherwise **Yggdra**). When it has grown in, fly the Urchin into the
   structure.
4. **U5 regression.** Launch **Skein**, **Hijack**, **Regatta** (with an Urchin seat) and
   **Breakwater** and latch onto their rails.
5. **U6.** Latch onto a trail and fire spikes while riding.
6. **U14.** Open **Window ▸ Analysis ▸ Frame Debugger** (or the Memory Profiler), then change the
   Urchin's team colour mid-session (with the domain-changer toy) and check the ship's materials.
7. **U16.** Press Slip (**B**) twice inside one ghost window, then watch the second ghost.

**PASS:** a stuck spike converts only the prism it stuck in and stays where it struck; in an authored
cell structure the Urchin rides the surface instead of snapping onto a rail that runs through the
world; the Skein, Hijack, Regatta and Breakwater rails still rail-grind; spikes fire while riding; the
ship repaints in the new colour and no new `(Instance)` materials appear for its Body or Window slots;
the second ghost lasts its full time and the ship stays intangible throughout.
**FAIL:** a stuck spike that keeps converting or jumps forward a segment · the Urchin snapping onto a
line through an authored cell · a mode rail that no longer grinds · no spikes while riding · new
`(Instance)` materials after a colour change · the ship turning solid partway through the second
ghost.

### QA-HIJACK-SHIELD-SYNC ⬜ — a rail prism's shield is the same on every machine in Hijack
**Source:** PR #1004 (`HijackOwnershipLedger` packs each yard prism's shield tier into the same
ownership word #972 replicates; new `shieldSweepBudget` field; `HijackYard` loot skips
super-shielded prisms; `_Scripts/Controller/Arcade/HIJACK.md` §9 steps 10g–10h, §10–§11). Before it
shields were local, so a Mass-5 prism shielded on one machine and not on the other counted as a steal
on one and a shield break on the other. Compiled and simulated offline (46/46 two-peer assertions)
only.

1. Open `Assets/_Scenes/Multiplayer Scenes/MinigameHijack.unity` and let it compile. Select the
   Hijack controller object and find **Shield Sweep Budget** under **Ownership Sync**. Look at the
   Console for red errors, especially RPC errors.
2. Do the two-player setup at the top of Block J, then launch **Hijack** with the two players on
   **different** teams.
3. **Client shields, host steals.** In the client window, raise the client's **Mass** to level 5
   (fly through Mass crystals) and ride one of the client's own rail thirds. Watch those prisms in
   both windows.
4. In the host window, ride the host's ship (or watch an AI raider) over that third, twice. Watch
   both windows after each pass.
5. **Host shields, client steals.** Swap roles and repeat steps 3–4.
6. **No flicker.** While the other player lays Mass-5 armour, watch the shields on your screen for
   ten seconds.
7. **Late join.** With some Mass-5 armour laid, add a third Multiplayer Play Mode player (tag `P3`)
   and join it to the match. Once its yard is laid, look at the same prisms.
8. **Regression.** Run QA-HIJACK-OWNERSHIP-SYNC steps 3–8.
9. **Profiler.** In the host window, open **Window ▸ Analysis ▸ Profiler** at intensity 4 and find
   `HijackOwnershipLedger.SweepShields`.
10. Play one full match alone with AI.

**PASS:** the field reads 4096 and there are no errors; the shielded prisms come up shielded in both
windows within a blink; the **first** pass only breaks the shields (the prisms keep their owner's
colour and the shield-break debris plays in both windows) and the **second** pass flips them, in both
windows; the same holds with roles swapped and both windows agree after each pass; shields stay up
with no drop-and-return after about half a second; the late joiner sees the same prisms shielded;
the QA-HIJACK-OWNERSHIP-SYNC steps still pass; `SweepShields` costs a few tens of microseconds a
frame; the solo match plays as before with no new warnings.
**FAIL:** an RPC error · a prism shielded in one window and bare in the other · a first pass that
flips a shielded prism in either window · the two windows disagreeing after a pass · shields that
blink off and back · a late joiner seeing bare prisms · `SweepShields` costing much more than about
0.1 ms a frame · a changed solo match.

### QA-SCARAB-SCRAMBLE-AI-JUKES ⬜ — AI Scarabs dash to steal rival balls and to escort their own
**Source:** PR #1002 (`ScarabScrambleController.TryAIJuke`, the `ScarabScrambleJukePlanner` geometry,
6 new **AI Jukes** fields; tests `ScarabScrambleJukePlannerTests`;
`_Scripts/Controller/Arcade/SCARABSCRAMBLE.md` § AI). Before it the AI never juked, so it never
stole a ball or fired the cavitation plate. The steal-versus-plate-knock ratio is modelled, not
measured. Compiled offline; the 16 tests ran headless outside Unity.

1. Open `MinigameScarabScramble.unity` and let it compile. Select the `Game` object and find the
   **AI Jukes** header on `ScarabScrambleController`.
2. In **Window ▸ General ▸ Test Runner ▸ EditMode**, run `ScarabScrambleJukePlannerTests`.
3. Open **FrogletTools ▸ Toolbox ▸ Logging** and turn on the **ScarabDash** channel. Launch Scarab
   Scramble at intensity 1, you plus two AI teams.
4. **Steal.** Forge a ball (fly your front sphere through a white crystal) and leave it loose near an
   AI that has no ball. Watch the AI.
5. **Escort.** Follow an AI escorting its own ball down a long straight.
6. Play to the final whistle and keep watching the AI for ten seconds after it.
7. Optional: with two players (setup at the top of Block J), watch an AI steal from the client
   window.

**PASS:** six fields read 260, 0.1, 4.5, 150, 30, 70; all 16 tests pass; the Console shows
`[ScarabJuke] Fired … committed` lines from AI ships with the 360° spin and the plate; the AI flies
at the loose ball and dashes as it arrives, and the ball either turns the AI's colour or is thrown
off its line; an escorting AI makes about one sideways dash per straight, never with its own ball
close beside it, and never more than one dash per half second; no AI dashes after the final whistle;
in the client window an AI steal shows the right colour change.
**FAIL:** missing fields or a failing test · no `[ScarabJuke]` lines from AI ships · an AI that never
goes for a loose rival ball · dashes faster than one per half second, or with its own ball beside it
· an AI dashing after full time · a colour change missing on the client.
**Report:** roughly how often an AI dash steals the ball versus knocks it away with the plate, and
whether the escort dash helps or only looks busy.

### QA-BUTTERFLY-SOUND-SLOTS ⬜ — the Butterfly's wingbeat and heart sounds have (empty) slots
**Source:** PR #1001 (`ButterflyAnimation.wingbeatEvent` + `wingbeatMinAmplitude`,
`ButterflyDustField.heartWitherEvent` / `heartNourishEvent`; `BUTTERFLY.md`). Three sounds that had
no slot at all now have empty ones, per the locked "every sound is an exposed, empty field" rule. No
prefab was edited, so the fields appear on the next save. Compiled offline only.

1. Open `Assets/_Prefabs/Spacevessels/Butterfly.prefab`, select its `ButterflyAnimation` component
   and look under **Audio**.
2. Open `Assets/_Prefabs/Spacevessels/Components/ButterflyDustSkimmer.prefab`, select
   `ButterflyDustField` and look under **Audio**.
3. Start freestyle and fly the Butterfly for a minute with every slot empty. Watch the Console.
4. Optional: temporarily assign any sound to the three slots (**do not save**), then fly: flap, hold
   the Fold (left trigger), pass over a rival creature's heart in Dust mode, and pass over your own
   team's lifeform. Undo the assignments afterwards.

**PASS:** `ButterflyAnimation` shows an empty **Wingbeat Event** and **Wingbeat Min Amplitude** = 4;
`ButterflyDustField` shows empty **Heart Wither Event** and **Heart Nourish Event**; no new warnings
or errors in the Console; with test sounds assigned, the wingbeat plays once per flap and goes quiet
while the Fold is held, the wither plays once as a rival creature dies, and the nourish plays at most
once per lifeform every 5 seconds.
**FAIL:** a slot missing from either component · a Console error or warning from these components ·
(with test sounds) a wingbeat that keeps playing during the Fold, or a wither or nourish that plays
when nothing died or was fed.
**Known, do not fail on:** the Butterfly is silent with the slots empty — the audio owner wires them.

<!-- /qa-round3-pr-checks -->


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

### QA-SIEGE-1 ⬜ — the siege: a cloud that surrounds you, leaves a gap, and dives if you stay

**Source:** branch `claude/siege-port`, ported from the flight lab's SIEGE (`Tools/Ecology/flight/src/70_siege.js` on
`cece/gifted-curie-x2cpd0`). Only the headless harnesses have run (`Tools/Build/substrate_harness/run.sh siege` replays
five lab encounters event for event; `run.sh all`; `showcase_cell_harness/run.sh quick`; `author_substrate_fauna.py
--check`); `/verify-unity` was not available and it has never been opened in the editor. Reference:
`Docs/SUBSTRATE_FAUNA.md` §10. Tunables: the `Siege` block of `Substrate Siege Species.asset`.

**Why it matters:** this is the lab encounter Garrett flew and called scary. It only works if the gap is readable, the
danger is telegraphed, and staying put is what gets you hurt.

1. **Find it.** Open the Swarm cell (Unity 2021.3.9f1). A cloud of ~150 small Time-coloured bodies hatches as one
   ball 690-1080 u from the cell centre. With nobody near it drifts slowly as a loose cloud and stays in that band.
2. **Stalked, then gathered round.** Fly within ~640 u. The cloud slides ahead of your line at ~430 u, then spreads
   into a shell round you (~300 u radius) with an open cap on one side: the iris. No glow yet; touching a member now
   must NOT burn petals.
3. **The glow is the warning.** As the shell closes (to ~130 u) members start to glow and the danger tier rises on
   exactly the glowing ones. A member must glow for at least a quarter second before it can bite.
4. **The wall bites.** Brush the shell wall while it is closed: the members you touch bite (a burn), and the wall
   breaches round that point.
5. **Stay and it dives.** Hover inside a closed shell. After about a second of HOLD every member dives at once from all
   sides. Expect one or two burns (the vessel's 1 s danger cooldown), then the cloud scatters and cools off ~10 s.
6. **The gap is a way out.** Repeat, but fly out through the open cap while it is still closing. You should escape
   with no burn, and the cloud cools off ~6 s before stalking you again. Then try ramming through members mid-dive:
   note whether a dive feels like a crystal fountain (rammed members are not shielded; say if they should be).
7. **The leash.** Get stalked, then fly far out of its band (toward the rim or the core). Once you are ~300 u past
   the band the cloud gives up and drifts home.
8. **Budgets.** Physics debugger during a dive: at most 4 siege proxies, locust proxies at most 8, colliders under
   1,200 in total.
9. **It lives.** Leave it alone for 10+ minutes, then come back: still roughly 100-160 members, none starving, with
   new members appearing as it eats the band's flora. Ram 20 or so members and check it recovers over time rather
   than dwindling (if it dwindles, raise `Metabolism` or lower `BirthStock`).

**PASS:** a readable gather, close, hold and dive with a visible gap; only glowing members burn; flying out the gap
avoids the bite; hovering gets dived on; the cloud stays in its band when idle and gives up past the leash; budgets
hold; it feeds and breeds. **FAIL:** a burn from a member that was not glowing · a dive with no hold first · no
visible gap · the cloud follows you across the cell · proxies or colliders over budget · the cloud dwindles or starves

### QA-SWARM-ROUND11-14 ⬜ — the balance pass: sated swarms live, swarms keep to their bands, packs survive a ram

**Source:** branch `overnight/balance`. Only the headless showcase harness (`Tools/Build/showcase_cell_harness/run.sh all`,
with a clock-only negative-control mutant that fails the new starvation check), the substrate / swarm / builders /
threat-flora harnesses, the authoring `--check`s and the player reference compile have run; it has never been opened in
the editor. Reference: `Docs/SWARM_FAUNA.md` §26.6.

**Why it matters:** in a 30-minute session the inner (Mass) swarm grew to full size, stopped feeding because it was
full, and then starved to nothing while still full; the wearers and the pack died out too. Swarms also drifted half
out of their shells while roaming.

1. **A full swarm lives.** Open the Swarm cell and leave the inner (whale, Mass) swarm alone for 15 minutes, watching its
   member count (SwarmFauna inspector). It should reach full size
   and stay there. It must NOT shed a member a second after ~10 minutes while its stomach reads full.
2. **A hungry swarm still starves.** Remove the inner shell's plants (or fly through and eat them) and wait. Once its
   stomach falls below half and 90 s pass without a bite, members wither one at a time, each leaving a skeleton and a
   crystal.
3. **Swarms keep to their shells.** Watch each swarm for a few minutes from outside its band (inner 470-620, middle
   690-840, outer 910-1080 u from the cell centre). The whole body should stay in its shell while it roams, not ride
   half outside along the edge.
4. **The pack survives a ram.** Find the Time pack hunters (690-1080 u) and ram through them repeatedly until only one
   or two are left. Survivors must not breed young that wither within seconds; the pack recovers by breeding only once
   fed.
5. **Leeches give a proxy to each of a puddle.** Fly straight through a puddle of 4 leeches at full speed: each one you
   pass through is rammable (a body to hit), not a ghost.
6. **Thieves and wearers roost.** Fly to the far side of the cell from the thief nest (rim, 1,085-1,140 u) and stay
   away from the wearers (400-465 u) for a minute. In the Profiler the thief and wearer colony ticks should stop
   (roosting); flying back within ~400 u of them wakes them.
7. **Colliders.** With all three pilots' worth of activity (or a busy solo run), the physics debugger's collider count
   stays under 1,200.

**PASS:** no full swarm starves; swarms stay inside their shells; the pack and wearers are still present after 30
minutes; leech rams always register; thieves and wearers roost when nobody is near. **FAIL:** a full-stomach swarm
loses members steadily · a swarm rides the edge of its shell · a pack dies out after a ram · a leech pass hits nothing
· thieves or wearers never stop ticking with nobody within 400 u.

### QA-SWARM-ROUND11-13 ⬜ — the demo cell starts hostile, and claiming the nucleus takes it

**Source:** branch `overnight/hostile`. Only the headless resolver harness (`Tools/Build/cell_control_harness/run.sh`, with three
negative-control mutants that must fail), the player + editor reference compiles and the authoring gates have run; it has never been opened in the editor. Reference:
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
   - **Outside the Swarm cell the rule is the SHIPPED one** (the volume-paid feed is opt-in,
     `CellConfigDataSO.ConservedFaunaStomach`, Swarm cell only - ECOLOGY_LOD.md §2.0). In Skim Race or Scarab
     Scramble, a tadpole grazing a thin trail must survive as long as it did before this branch. FAIL if grazers on a
     trail diet thin out noticeably faster there than on bleeding-edge.
   - **Inside the Swarm cell** (the flag is on) a grazer living only on thin trail prisms starves sooner than one
     on full-size leaves. That is intended.
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


### QA-FAIR-BURNS-1 ⬜ — spawn grace and the pack wind-up

**Source:** PR "Fair burns: pack wind-up and spawn grace". Proven headless (substrate harness P/H, swarm core
harness "pack hunter wind-up", player and editor refcompile). Not run in the editor. Reference:
`Docs/ELEMENTAL_ECONOMY.md` §4.1 "Fair burns".

1. **Inspect.** `VesselElementalDebuffByDangerPrismEffect.asset` shows Spawn Grace ▸ Spawn Grace Seconds **1**.
   `Substrate Pack Hunter Species.asset` has `StrikeWindupS` **0.4**. A swarm config shows Hunt Windup Seconds and
   Puff Windup Seconds **0.4**. Any Cell Config shows Stakes ▸ Petal Burn Rule **Tuned**.
2. **Spawn grace.** Start freestyle in the Swarm cell as the non-controlling domain and fly straight into a striking
   creature within the first second of the go. No petal should burn. Touch again after 1 s: it burns as usual.
3. **Wind-up, substrate pack.** Let the Time pack ring you. After the ring closes, the hunters should visibly turn
   on you (swell, sprint) for a beat before the first bite can land. Flying out through the gap in that beat
   should escape without a burn.
4. **Wind-up, swarm pack hunter and pufferfish.** Startle a Time swarm, then a Charge swarm. Their plates should
   light about 0.4 s after they turn on you, not on the same frame.
5. **Tuned everywhere.** In a cell other than Swarm, touch a hostile danger prism: each flower loses one petal.
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

### QA-MENU-VEIL-PAUSE 🔴 — the menu-return veil hold and the prewarmed pause menu
**Last QA:** FAIL on `5663cc4b3` (2026-10-08, akouroshm) — The menu-return/loading veil does not hide the build: prisms are visibly **popping in during loading**, and on entering a game the player sees a **"metal seal" opening up** — an animation that appears to be a remnant of an old loading-screen concept, not the current veil. The teardown/build is not being covered. (Observed on 71d67ba9b.)

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

### QA-TOY-ELEMENT-CHARGER ⬜ — the Element Charger toy grants elements to your vessel
**Source:** PR #913. New freestyle toy, authored headless and never compiled. Reference:
`Docs/ToySystem/` Element Charger entry.
1. Compile, then run `ElementChargerToyTests`.
2. In `Menu_Main` freestyle, fly the **Element Charger** (about 150° around the cell ring).
   The choice row should bloom with **Charge** on your left.
3. Fly each crystal in turn. Its elemental flower should step up by **5**, a level-5
   ability upgrade should light on that element's ability icon, and the **hull should
   morph**.
4. Keep charging one element past 10: the overcharge must **drain back to 10** at about
   one level every five seconds.
5. Swap vessels (Vessel Changer toy): the new hull starts at **its own** levels, not the
   charged ones.

**PASS:** the row blooms with the four elements; each crystal raises its flower by 5,
lights the L5 upgrade and morphs the hull; overcharge drains back to 10; a vessel swap
resets to the new hull's own levels.
**FAIL:** a crystal that does nothing · no upgrade light at level 5 · a hull that never
morphs · overcharge that stays above 10 · charged levels carrying across a vessel swap.

### QA-TOY-WANDER-MERGE ⬜ — Wanderway and Arkway merged into one Wander toy
**Source:** PR #930 (merge) + #929 (the Arkway voyage). The two wander toys are now **one
Wander toy with two choices**; authored headless, never compiled. Reference:
`Docs/ToySystem/ARCHITECTURE.md`.
1. Compile and watch the Console for errors.
2. `Menu_Main` freestyle → fly the **Wander** toy. Two stations should bloom: a **mini
   Ark** (With Ark) and an **archway microscene** (Without Ark).
3. Thread **Without Ark**: the microscene belt primes behind the load veil the first time,
   and resumes with no second veiled build after that.
4. Thread **With Ark**: the Ark voyage starts behind the veil — real cells stream past and
   the Ark itself is present.
5. With a run live, fly the Wander toy again: it should bring you **home**.
6. From the Toy Box menu, open Wander: with nothing running both cards **Start**; with one
   running, that card reads **"Come home"** and the other is not committable.
7. Change your domain: the emblem's mini Ark and the With Ark station recolour.

**PASS:** compiles; both stations bloom; each choice starts its run behind one veil; a
live run's toy brings you home; the Toy Box cards read Start / Come home correctly;
the Ark elements recolour on a domain change.
**FAIL:** a compile error · only one station · a second veiled rebuild on resume · a run
that cannot be ended from the toy · both Toy Box cards committable while a run is live ·
the emblem not recolouring.
**Known, do not fail on:** the Wander codex portrait still shows the old Wanderway image
(re-bake pending).

### QA-ARKWAY-VOYAGE ⬜ — the Arkway is a real playable voyage now
**Source:** PR #929. The Arkway streams **real cells** past a **fed-upon Ark** and its
corridor prisms now actually render. Authored headless. Reference:
`Docs/ECOSYSTEM.md §41` and `Docs/ToySystem/ARCHITECTURE.md` § Arkway.
1. Start an Arkway voyage (via the Wander toy, With Ark).
2. Watch the cells stream past — confirm they are real built cells, not empty space.
3. Watch the **Ark** hull: fauna should be able to feed on it (its mass is ordinary
   conserved prism mass in its domain).
4. Confirm the **corridor prisms** along the route are visible (the fix this PR made).
5. End the voyage and confirm you return home cleanly.

**PASS:** real cells stream past; the Ark is present and its mass is grazeable; corridor
prisms render; the voyage ends and returns you home.
**FAIL:** empty space instead of cells · an Ark nothing can touch · invisible corridor
prisms · a voyage that cannot be ended.

### QA-ECOLOGY-BORROMEAN-FLORA ⬜ — the Borromean-rings flora and its four cells
**Source:** PR #894. A new flora grown on the Borromean rings' minimal surface, plus four
cells that grow it (the Garland cell family). Authored headless, **never compiled** (CI's
Unity job is skipped). Reference: `Docs/ECOSYSTEM.md`.
1. Compile; watch the Console for errors on first load.
2. Freestyle → Cell Selector toy → each of the **four** Borromean/Garland cells. Confirm
   each imports and builds with no `None` references.
3. Run **FrogletTools ▸ Ecology ▸ Validate Lifeform Crystals** — the new flora must pass.
4. Stay in a cell and let the flora grow several waves; confirm it grows on the surface
   (connected, not disconnected lumps) and the phase readout does not boot to Frenzy.

**PASS:** compiles; all four cells build cleanly; the crystal validator is green; the
flora grows coherently on its surface; no cell sits in Frenzy at rest.
**FAIL:** a compile error · a cell that builds zero prisms or has `None` refs · a crystal
validator failure · flora growing in disconnected lumps or not at all · a cell stuck in
Frenzy.

### QA-ECOLOGY-MANDELBULB-FLORA ⬜ — the Mandelbulb flora family and the Arboretum
**Source:** PR #896. A new flora family plus the **elemental form law** (an element
changes a plant's FORM, not just colour) and the **Arboretum** cell. Authored headless,
never compiled. Reference: `Docs/ECOSYSTEM.md`.
1. Compile; watch the Console.
2. Freestyle → Cell Selector → **Arboretum**. Confirm it builds.
3. Let the Mandelbulb flora grow; spawn or find all four elements of it and confirm each
   element reads as a **different form**, not the same shape recoloured.
4. Run **Validate Lifeform Crystals** — the family must pass.

**PASS:** compiles; the Arboretum builds; the four elements of the flora are visibly
different forms; the crystal validator is green.
**FAIL:** a compile error · an Arboretum that does not build · four elements that are the
same shape in different colours · a crystal validator failure.
**Known, do not fail on:** the Cell Selector shows a grown world as a bare station with
no scale model (a separate, logged gap).

### QA-RHINO-SWORD-COMBOS ⬜ — trigger-tap sword combos, prism slicing, and the supershield bind
**Source:** PRs #921 (trigger-tap combos + energized set), #914 (the sword SLICES prisms
it destroys), #904 (the sword binds in super-shielded mass with no recoil, + a speed/turn
retune). Reference: `_Scripts/Controller/Vessel/R_VesselActions/RHINO_SHIELD_SWIPE.md`.
1. Freestyle as the **Rhino**. Tap the sword trigger in sequence and confirm a **combo**
   plays through (not just a single repeated swipe), and that the energized set reads.
2. Swipe through ordinary prisms and watch them **slice** as they are destroyed.
3. Swipe into a **super-shielded** prism (e.g. the Skim Race track lining): the sword
   should **bind** in it with **no recoil** kicking you off.
4. Fly the Rhino normally and judge the retuned speed/turn — it should feel controllable.

**PASS:** trigger taps chain into combos with the energized set; destroyed prisms slice;
the sword binds in super-shielded mass without recoil; the speed/turn retune is flyable.
**FAIL:** combos that never chain · no slice on destruction · a sword that bounces off
super-shielded mass instead of binding · a retune that makes the Rhino uncontrollable.
**Report the feel:** say whether the combo timing and the new speed/turn read well.

### QA-SERPENT-RETICLE-PELLETS ⬜ — a visible reticle, a piercing sniper round, and Solid Fuel Pellets
**Source:** PRs #900 (a reticle the pilot can see + a sniper round that pierces) and #903
(Solid Fuel Pellets restored as the Serpent's Time ability, with its icon).
1. Freestyle as the **Serpent**. Confirm a **reticle** is drawn where the pilot is aiming.
2. Fire the sniper round at a line of prisms: it should **pierce** through more than one
   rather than stopping on the first.
3. Raise **Time** to level 5 and confirm the **Solid Fuel Pellets** ability is available
   and its **icon** appears in the ability row.

**PASS:** a visible aiming reticle; the sniper round pierces multiple prisms; Solid Fuel
Pellets is present at Time 5 with its icon on the row.
**FAIL:** no reticle · a round that stops on the first prism · a missing or icon-less
Time ability.

### QA-LIT-FUNDAMENTAL ⬜ — LIT promoted to a fundamental, explosion temp shield retired
**Source:** PR #891. "LIT" became a platform fundamental and the old explosion
temporary-shield hack was retired. Touches how own-domain mass reacts to a blast, so a
defect is broad.
1. Fire an own-domain explosion (e.g. a Dolphin crystal blast) into **your own** mass and
   confirm it reads as **accepted** (lights/shields) rather than clipping or being
   destroyed.
2. Fire the same blast into **opposing** mass and confirm it is destroyed as before.
3. Watch the Console for any LIT-related exception during a blast.

**PASS:** own-domain mass reacts to a blast as LIT (no destruction, no clipping);
opposing mass is destroyed; no exceptions.
**FAIL:** own mass destroyed by your own blast · a visible clip/pop where a shield used to
read · any LIT exception.

### QA-ARCADE-CARD-BACKGROUNDS ⬜ — genre petals and rendered card backgrounds on every card
**Source:** PR #911. Every arcade/arena card gained a **genre petal** marker and a
**rendered intensity-2 background** image of its own arena.
1. Open the **Arcade** screen and scroll every card. Each should show a background image
   that looks like **that mode's own arena**, not a placeholder or a shared image.
2. Open the **Arena** screen and do the same.
3. Confirm each card shows its **genre petals** and that no card is blank or broken.

**PASS:** every arcade and arena card shows a distinct, mode-appropriate background and
its genre petals; none is blank, stretched or sharing another card's image.
**FAIL:** a blank/placeholder background · a card wearing the wrong mode's image · missing
genre petals · a stretched or broken image.

### QA-TOYBOX-ACTIVITY ⬜ — daily activity, shuffle, pole switches, and text-free toys
**Source:** PR #910. The toybox gained a **daily activity** button and a **shuffle**
button, **pole switches**, and the toys lost their text labels.
1. `Menu_Main` freestyle: open the Toy Box and confirm a **daily activity** and a
   **shuffle** control are present and do something when used.
2. Fly the toy ring and confirm the toys read **without text labels** — each still
   identifiable by its shape/switch ring.
3. Use a **pole switch** and confirm it activates as a switch should.

**PASS:** daily activity and shuffle both work; toys are identifiable with no text; pole
switches activate.
**FAIL:** a dead daily-activity or shuffle control · a toy that is unidentifiable without
its old label · a pole switch that does nothing.
*(Toy legibility without labels is partly a taste call — report a genuinely
unidentifiable toy as a note unless it is completely unreadable.)*

### QA-MP-SESSION-LIFECYCLE ⬜ — a client can always leave, and a departed pilot's ship keeps flying
**Source:** PR #865. Multiplayer session lifecycle rework: a client can always leave and
is never stranded, and a pilot who leaves mid-game leaves a ship that keeps flying. Needs
**MPPM with at least 2 virtual players**.
1. Host + one client. Start a multiplayer game.
2. Have the client **leave mid-game** (in-game leave button). Confirm the client returns
   cleanly to the menu — not stranded on a black screen or a dead scene.
3. On the host, confirm the departed pilot's **ship keeps flying** (as AI or drift) rather
   than freezing or vanishing abruptly.
4. Repeat with the client leaving from the scoreboard/end screen.

**PASS:** the leaving client always reaches the menu cleanly; the host never sees the
departed ship freeze or pop out; no exceptions on either machine.
**FAIL:** a client stranded after leaving · a departed ship that freezes or vanishes
instantly (breaking continuity) · an exception on either machine on a leave.

### QA-OFFLINE-FALLBACK ⬜ — single-player offline fallback and the online/offline toggle
**Source:** PR #812. When UGS/Relay is unreachable the game falls back to a local host,
caches player data, and offers a player-facing online/offline toggle. Reference:
`Docs/OFFLINE_MODE.md`.
1. Launch with **networking cut** (no internet, or block UGS) and confirm the game still
   reaches `Menu_Main` instead of hanging at boot.
2. Confirm your **name, vessel unlocks and progression** still show (served from the disk
   cache), and that matchmaking / party creation are stood down.
3. Launch and play a single-player arcade game offline — it should run on the local host.
4. Restore networking and use the **reconnect / online toggle**; confirm it comes back
   online without an app restart.

**PASS:** boot reaches the menu offline; cached name/unlocks/progression show; online-only
UI (invites, leaderboards, purchases) is gated; a single-player game runs offline; the
reconnect toggle restores online play in place.
**FAIL:** a boot that hangs with no network · lost name/unlocks offline · an offline game
that will not start · a reconnect that needs an app restart or throws.

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
