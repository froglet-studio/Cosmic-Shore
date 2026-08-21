# QA Backlog — untested development on `bleeding-edge`

Generated: 2026-08-21 · Scan covers: up to `ce6a9c78` (PRs #583–#766) · **Owner of this file: the `/qa-backlog` skill — do not hand-edit.**

> Note (2026-08-11): `bleeding-edge` was briefly force-pushed back to `0e855b24` (dropping PRs #674–#679) and then restored — the current tip `b0cf4f0f` re-includes all of that work plus PRs #680/#681/#695/#696. No items were pruned. The `windows-build-failures` build-fix branch is validated by QA-BUILD-COMPILE on Windows and has no separate item.

Every item below landed on a shared branch without ever being opened in Unity by its author (or was play-tested only in part). Work top-down: P0 first.

**How to report:** copy `RESULTS/TEMPLATE.md` → `RESULTS/<date>-<tester>.md`, fill the table, commit. Full workflow: `Docs/QA/README.md`.

**Status key:** ⬜ never run · 🟡 partially confirmed · 🔴 failed, awaiting fix · ⛔ blocked. Items that PASS leave this file (→ `ARCHIVE.md`).

**Standing preconditions for every item** (do these once per session):

* Pull the branch under test, let Unity fully reimport (a stale `Library/` masks asset changes and is the single most common false failure here).
* Keep the Console open with Error Pause off and Clear on Play off.
* Unless an item says otherwise, "freestyle" means: launch to `Menu_Main`, tap the centre crystal to take control of the vessel.

## ⚡ Quick wins — start here if you have a few minutes

Hand-curated by `/qa-backlog` each run: open items that are the fastest / lowest-effort to get a
clean verdict on (asset-only, one-glance, or a single short check). Not a priority ranking —
the P0 gates below still matter more; this is just "what can I knock out quickly." Refreshed
every run, so it can lag reality by one submission.

1. **QA-DOLPHIN-SPEED-TUNE** — Dolphin freestyle, check three numbers: cruise ≈78, boost fill ≈3.6 s, peak ≈357.
2. **QA-DOLPHIN-CAPSULE-BLAST** — first an editor check: `_Prefabs/Projectile/AOEConicExplosion.prefab` has a **Capsule Collider** (not Sphere/missing); then a charged Dolphin crystal blast should fan wide-in-jaw-plane, growing in length.
3. **QA-DOLPHIN-SKIM-ENERGY-CTA** — editor: the six HUD-variant prefabs have no missing scripts; then Dolphin skim → energy fills much slower, and the jaw gauge arms **lime** at full.
4. **QA-VESSEL-SELF-TRAIL** — fly a tight loop so you cross your own just-laid trail: no skim/ram/slow off the fresh trail.
5. **QA-UI-MODAL-STACK** — open the Arcade configure modal, close it three ways (✕, background tap, Home), confirm nav still works.
6. **QA-PALETTE-DANGER-GOLD** — get shielded prisms of all three domains on screen (a populated cell / HexRace) and check gold reads in the pastel family.
7. **QA-P2-DANGLING-CELLDATA** — a populated cell in freestyle: watch the Console for `LifeForm.Start()` / `Flora.Plant()` throws (PR #731 may have fixed this — verify which, if any, still throw).

Editor-only, no play mode: **QA-CRASH-DETECTOR-TOOL** (just open FrogletTools ▸ Misc ▸ Crash Detector and confirm it doesn't throw) and **QA-PRISM-SHIELD-GPU-VISUALS** (run the jiggle test + glance for magenta on Skim Race track prisms) are the cheapest if you're already in the editor.

Tip: #1–#4, #6, #7 are all "one Dolphin freestyle session in a populated cell" — load a lifeform-rich cell (Cell Selector → Yggdra/Hesperides) on the Dolphin and knock out several in a row. #2/#3 start with a quick editor prefab glance.

## Priority 0 — gates. Nothing below matters if these fail.

### QA-BUILD-COMPILE ⬜ — the project compiles, imports and boots
Source: every headless branch since #583. Why P0: ~15 branches of hand-authored C#, prefab, scene and ScriptableObject YAML have never been through Unity's importer or compiler.

1. Open the project on the branch under test. Wait for import + compile to settle.
2. Read the whole Console. Record every compile error, every `Missing (Mono Script)`, every "broken/dangling reference", and every meta-file regeneration warning.
3. Launch to `Menu_Main`. Enter freestyle. Return to the menu. Launch one arcade game (any) and return home.

PASS: zero compile errors; no `Missing (Mono Script)` on any object you touched; the app reaches `Menu_Main`, freestyle and one game round without an exception. FAIL: any compile error, any missing script, or an exception that stops the boot or the round. Record the full first error verbatim — everything else on this list is blocked behind it.

### QA-PRISM-OCCLUSION ⬜ — camera↔vessel prism corridor (shader, magenta risk)
Source: PR #661. Platform law; `PrismOcclusionCorridor.hlsl` gained a Worley loop, `UNITY_MATRIX_V` and a `#define` kernel selector with no compiler. Reference: `Docs/PRISM_CLOCK_WIRING_CHECKLIST.md` Phase 8.

1. Load any scene with prisms. Look at the prisms first — an HLSL compile failure turns every prism magenta on load. If magenta: stop, FAIL, attach the shader error.
2. Freestyle: lay a wall of trail, then fly so the wall sits between the camera and your ship.
3. Fly away from the wall so nothing occludes you.
4. Hold still with prisms in the corridor for ~10 s and watch the stipple.
5. Fly to a boundary of the cleared cone and study the edge.
6. Swap to a much larger/smaller vessel (Vessel Changer toy) and repeat step 2.
7. Check the Console for `[PrismOcclusion]` messages.

PASS: prisms between camera and ship dissolve so the ship stays visible; they snap back opaque as you leave (the band is short by design); the boundary has no hard seam or banding; the stipple flows/evolves rather than strobing; the cleared cone rescales with vessel size; zero `[PrismOcclusion]` console errors. FAIL: magenta prisms · ship hidden behind prisms · visible flicker/strobe · hard-edged rectangle or ring at the boundary · corridor obviously the wrong size on one vessel · any `[PrismOcclusion]` error.

### QA-SPEED-TUNNEL ⬜ — the speed tunnel as a fleet-wide law
Source: PR #668 (deleted the per-vessel `SpeedTunnelEffectController`; a single static driver now covers all 11 vessels). Reference: `Docs/SPEED_TUNNEL.md` §5.

1. Fly Rhino, Manta, Dolphin, Squirrel, Sparrow, Serpent in turn (Vessel Changer toy in freestyle). For each: accelerate to top speed, then drop to cruise.
2. Boost a Dolphin and a Serpent (both top out ≈210) and compare the effect.
3. Swap vessels while the tunnel is engaged (boost → open Vessel Changer → swap).
4. Play Astro League and score a goal — watch the replay camera.
5. Change the FOV setting in Settings mid-session, then boost again.

PASS: every vessel narrows FOV + relaxes Panini purely as a function of its own speed and returns exactly to its pre-boost framing; the same speed on two different vessels looks the same (step 2); a mid-effect vessel swap leaves the new vessel with a correct, non-stuck view; the goal replay camera is not tunnelled; after a FOV setting change the effect anchors to the new home value. FAIL: any vessel with no effect · FOV stuck narrow after release or after a swap · the replay shot visibly zooming · a snap to a foreign FOV when the setting changes.

### QA-PRISM-CLOCK-ENV-SNAP 🟡 — environment-lay prisms snap (known defect, confirm scope)
Source: PR #642 item C13. `SegmentSpawner`-instantiated prisms get no companion entity, log `[PrismClock] STRICT MODE` and pop into existence instead of blooming. Strict mode is working as designed; QA's job is to bound the blast radius.

1. Launch Skim Race / HexRace (any intensity) and watch the track build.
2. Read the Console for `[PrismClock] STRICT MODE` errors; note the count and whether it is bounded (one burst at build) or continuous.
3. Fly the Wanderway conveyor toy in freestyle and watch scenes arrive.
4. Note every other place prisms appear to snap rather than bloom (cell environments, trails, flora, fauna, cage bars).

PASS (for this pass): the snap and the STRICT MODE errors occur only on `SegmentSpawner` tracks and Wanderway scenes, are bounded to build time, and nothing else in the game snaps. FAIL: snapping/errors anywhere else (especially vessel trails, cell environments or lifeforms), errors continuing every frame, or the errors accompanied by prisms that never appear at all.

### QA-DOLPHIN-SKIM ⬜ — nobody has ever seen a Dolphin skim work
Source: PR #660 + `Docs/UNITY_VERIFICATION_CHECKLIST.md`. The Dolphin's `VesselStatus` pointed at a disabled legacy skimmer, so every contact was dropped silently. The fix is unconfirmed. Reference: `DOLPHIN_ENERGY_ECONOMY.md` §6.

1. Run FrogletTools ▸ Vessels ▸ Audit Vessel Skimmers. This is the gate.
2. Freestyle as the Dolphin: fly through cell mass so prisms pass through the skimmer.
3. Hold drift until the ring steps up, then release. Then fly straight for 10 s. Then drift → release → drift again.
4. Hit a crystal.
5. Raise Charge to level 5 (elemental crystals) and plant two team crystals back to back.
6. With a second client (MPPM), confirm both peers agree on the level-5 upgrades.

PASS: audit reports `Dolphin NearFieldSkimmer: 'EnergySkimmer' OK`; crackle arcs sweep the skimmer sphere per prism and the HUD jaw icon punches per skim; the gape widens as energy fills; drift fills the ring and flying straight does not; speed returns to normal after an interrupted discharge; the crystal fires the cone, empties energy and flashes the Space icon; two crystals plantable at Charge L5, preview tinted your domain and blooming (not popping); both peers agree. FAIL: audit reports anything else for Dolphin · no visible/audible skim feedback · ring fills while flying straight · speed stuck high after drift→release→drift · peers disagree on upgrade state. (Serpent is expected to FAIL the same audit — that is a known, separate item.)

### QA-RIBCAGE-MODE 🟡 — "Peel the Cage" has never been opened
> **Last result:** 🟡 PARTIAL — Most of the steps seem fine, I'm not sure if the danger blocks work at all, but it not working isn't labeled as a failure  _(build claude/untested-backlog-qa-workflow-7a0nb9 @ 68d2dab · Unity 6000.4.11f1.x · Windows, Unity Editor, 2026-08-07, andrew)_

Source: PR #662 + later tuning. Whole new game mode (`GameModes.Ribcage = 39`), authored headless. Reference: `_Scripts/Controller/Arcade/RIBCAGE.md` § In-editor verification.

1. Open `MinigameRibcage.unity`. Confirm no `Missing (Mono Script)`, the controller shows `rule = RibcageScoringRule` with milestone fractions 0.25 / 0.5, and the Cell lists four configs with Cell Type Choice = Intensity Wise.
2. Launch at intensity 1 → count the shells. Relaunch at intensity 4 → count again.
3. Inspect the weave: are the openings triangles (each cell crossed by a diagonal, lean alternating)?
4. Compare outer vs innermost rind spacing.
5. Orbit the whole cage: do the inner rinds' dense polar caps point different ways?
6. Line up on the centre from outside and fly straight in.
7. Run FrogletTools ▸ Ecology ▸ Measure Cell Environment Baselines; expect 10,620 / 14,731 / 17,992 / 20,153 prisms for intensities 1–4.
8. Ram a plain rib. Then find a danger bar (distinct material) and ram it.
9. Play a full round to the target and watch the scoreboard.

PASS: 2 shells at intensity 1 and 5 at intensity 4 (nested at 360/295/230/165/100); triangular openings; the innermost rind is visibly the tightest; the cage visibly twists as you orbit; no free corridor to the centre; baselines within a few hundred of the expected counts; a plain bar shatters on one hit with no shield to shed; the danger bar also one-hits but full-stops you, debuffs all four elements ~4 s and resets boost; no fauna hatch at any point; the round ends on prisms destroyed and the scoreboard counts prisms. FAIL: every intensity looking the same (Cell not on `IntensityWise`, or configs out of order) · bubble-shaped openings · uniform spacing core-to-surface · rinds all aligned at the poles · a clean corridor to the centre · two-hit/shielded bars · any fauna · baselines off by thousands.

### QA-SHELL-COLLISION ⬜ — shape-precise shielded-prism collision (Burst shell tier)
Source: PR #627. Reference: `Docs/SPATIAL_INDEX.md` § "Shell view — in-editor verification". Touches every skim and every shield pop in the game.

1. Squirrel on Skim Race: skim the super-shielded track lining along its length, including grazing passes at the spike tips and passes aimed at the gaps between spikes.
2. Rhino: swipe a shielded prism and note at what distance the shield pops.
3. Fly a dense trail while crystals auto-shield prisms around you.
4. Profile a HexRace round: watch `ShellContact.Build` / `ShellContact.Query` and `Physics.SendEvents`.
5. Toggle the runtime A/B switch off and back on.

PASS: skims register at the stella surface (≈3× the box), spike-tip grazes hit, aimed-at-the-gap passes do not; boost is granted per shell touch; the Rhino pops at octahedron reach rather than point-blank; no prism becomes untouchable and no double-fire (pop and destroy in one contact); the two markers stay sub-ms and `Physics.SendEvents` is flat vs. the previous build; the A/B toggle reverts cleanly. FAIL: skims only at the box · gap false-positives · pop-then-destroy · any prism that cannot be hit at all · marker spikes or a rising `Physics.SendEvents` train.

### QA-EDITMODE-TESTS 🔴 — run the test suites that were written but never executed
> **Last result:** 🔴 FAIL — None of the required tests were even there for me to test.  _(build claude/untested-backlog-qa-workflow-7a0nb9 @ 68d2dab · Unity 6000.4.11f1.x · Windows, Unity Editor, 2026-08-07, andrew)_

Source: PRs #659, #639, #627, #641, #668, #651. These are NUnit suites authored headless — they have literally never been run.

1. Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All.
2. Record every failing test by name, plus the total pass/fail count.
3. Specifically confirm these suites are present and green: `CellSpawnFormationTests`, `SkimmerSwingKinematicsTests`, `ShieldShellMathTests`, `VesselElementalMorphTests`, `VesselRigPartResolutionTests`, `SpeedTunnelLawTests`, `SettingsAutoDetectorTests`, `GeometryUtilsTests`, `PrismOcclusionCoverageTests`, `ShipModifierTests` (PR #679), `DisplayNameValidatorTests` (PR #674/display-name-validation), `PrismDeathVisualTierTests` (PR #715), the super-shield jiggle suite (PR #730).

PASS: all EditMode tests green and all nine suites present. FAIL: any red test (record the name + assertion message) or a suite that does not appear at all (means it did not compile into the test assembly).

### QA-AUDIT-TOOLS 🔴 — run every FrogletTools auditor and record its verdict
> **Last result:** 🔴 FAIL — Three problems beyond the known exceptions; everything else (skimmers, ability rows, hull morphs, speed-tunnel law, occlusion corridor, baselines) ran fine. (1) **Audit Cell-Owned Visuals** logged errors: "'CosmicShore.Core.NetworkMonitor' is missing the class attribute 'ExtensionOfNativeClass'!" (x2) and warning "GameObject (named 'NetworkMonitor') references runtime script in scene file. Fixing!", then "[CellOwnedVisualAudit] 26 scenes scanned." (2) **Validate Lifeform Crystals** — the menu item does not exist on this build (could not run it). (3) **Game Mode Prefab Kit ▸ Validate** — 1 error + ~40 warnings; logged "[PrefabKit] Created kit config at Assets/Resources/GameModePrefabKit.asset with 9 seeded entries." For reference the baseline line read: "SpawnableAtlantis 67,722 prisms / 950,437 volume", and the occlusion-corridor check reported the hlsl GUID pinned (OK).  _(build bleeding-edge @ b0cf4f0f · Unity 6000.4.11f1.x · Windows, Unity Editor, 2026-08-11, andrew)_

Source: PRs #637, #641, #653, #659, #661, #668, #646, #650. Each auditor is a cheap, asset-only check that encodes a contract; several have never been run. Run each and paste its report into your results file:

1. Vessels ▸ Audit Vessel Skimmers
2. Vessels ▸ Audit Vessel Ability Rows
3. Vessels ▸ Audit Vessel Elemental Morphs
4. Vessels ▸ Validate Speed Tunnel Law
5. Ecology ▸ Audit Cell-Owned Visuals
6. Ecology ▸ Validate Lifeform Crystals
7. Ecology ▸ Prism Animation (validator) ▸ Validate Occlusion Corridor
8. Ecology ▸ Measure Cell Environment Baselines
9. Game Modes ▸ Game Mode Prefab Kit ▸ Validate
10. Build ▸ Pending Tool Changes (should list nothing unexpected)

PASS: every tool runs without throwing, and each reports either clean or only the known exceptions: Serpent fails the skimmer audit; Manta/Rhino/Serpent are listed as design-blocked in the ability-row audit; Dolphin/Urchin/Rhino/Grizzly lack elemental morphs; `SkyboxModel` entries listed under OK in the cell-visual audit. FAIL: any tool that throws, or any new failure beyond the known exceptions above — especially "SCENE-PLACED DUPLICATES" or "DEAD CELL OVERRIDES" being non-empty.

### QA-PRISM-OCCLUSION-SHATTER ⬜ — the corridor dither's new hard-edged SHATTER shape + Dither Lab tool
Source: PR #677 (`prisms-occlusion-shapes`). 467 lines of new `PrismOcclusionCorridor.hlsl` (a new SHATTER kernel, triangular flecks, live scale dials) plus a brand-new editor tool `PrismOcclusionDitherLab.cs` (844 lines) — none of it compiled or run. This is the same platform-law surface as QA-PRISM-OCCLUSION; do that item too and treat this as the shape-specific delta.

1. Load any scene with prisms. If any prism is magenta on load, the HLSL failed to compile — stop, FAIL, attach the shader error.
2. Freestyle: put a wall of trail between the camera and your ship. The cleared region's stipple should now read as a **cracked lattice of hard-edged polygons** (SHATTER), not round or triangular flecks, and should not strobe.
3. Hold still ~10 s: the pattern should slowly evolve/orbit, not freeze or twinkle.
4. Open FrogletTools ▸ Ecology ▸ Prism Animation ▸ Occlusion Dither Lab. Confirm it opens without throwing, drives the kernel/scale live in play mode, and its coverage readout is sane. Change the kernel and scale and confirm the corridor updates in play.
5. Console: zero `[PrismOcclusion]` errors.

PASS: no magenta; the ship stays visible through corridor prisms; the SHATTER lattice reads as hard-edged cracked polygons and evolves smoothly; the Dither Lab opens, drives the effect live and reports coverage without throwing; no console errors. FAIL: magenta prisms · any HLSL/`[PrismOcclusion]` error · the Lab throwing or not affecting the corridor · a strobing/twinkling dither · the ship occluded.

### QA-MENU-CRASH-PAUSE-PANEL ⬜ — Menu_Main no longer crashes the Windows player (type-punned pause panel)
Source: direct commit `b08a35d7` (`fix(ui): the Menu_Main crash is a type-punned pause-panel reference`). `PauseMenu.pauseMenuPanel` was a `GameObject` field still holding a `CanvasGroup`-typed pointer in `Pause_Menu_Panel.prefab`; the Editor coerced the mismatch to null (silent) but the **IL2CPP player** handed the punned pointer to native `GameObject` calls — an access violation (not a catchable exception) that took the Windows build down on **every** entry to Menu_Main. Fix repoints the prefab at the panel root and routes the reference through a validated `Panel` property. Also touches `SquadMemberCard.cs`. This is a **player-build** crash, so the Editor alone can't fully clear it.

1. Open `_Prefabs/UI Elements/Panels/Pause_Menu_Panel.prefab`: no `Missing (Mono Script)`; `PauseMenu.pauseMenuPanel` points at the panel **GameObject** (its own root), not a CanvasGroup.
2. Editor: launch to Menu_Main, open the pause panel (first tap warms it), close it — no null-ref, panel appears.
3. **The real gate — Windows player build.** Make a Windows (IL2CPP) build, launch it, and enter Menu_Main. Do it several times (and back-and-forth from a game) — it must not crash.
4. Sanity-check anything using `SquadMemberCard` still displays.

PASS: prefab repointed with no missing script; the pause panel warms and opens in the Editor; the **Windows player build reaches Menu_Main repeatedly with no crash**; squad cards still render. FAIL: a missing script or a CanvasGroup-typed reference remaining · the pause panel not opening/warming · **any crash entering Menu_Main in the Windows player** · broken squad cards.

### QA-DOGFIGHT-MODE ⬜ — "Dog Fight": the Sparrow-only gun duel in the Boneyard
Source: `dog-fight-game-mode` (feat `3324b951`). A whole new game mode — 96 files, 15,626 insertions, authored headless — with new asset-writing tools (`Tools/Build/author_dogfight_assets.py`, `boneyard_budget.py`), a new scene (EditorBuildSettings changed), a `ScriptableEventCombatHitStats` SOAP type, and `GameDataSO` additions. Reference: `_Scripts/Controller/Arcade/DOGFIGHT.md`.

1. Open the Dog Fight scene: no `Missing (Mono Script)`; the controller and its scoring rule are wired; the arena ("Boneyard") builds.
2. Launch the mode (any player count — AI backfill for solo). It reaches gameplay without an exception.
3. Confirm it is Sparrow-only and gun-combat focused (the Boneyard as the arena, the enemy marker, crystal drops).
4. Play a full round to the win condition and watch the scoreboard resolve (combat-hit / kill scoring).
5. Return to menu and relaunch once — no leaked state, no crash.

PASS: scene opens clean; the mode launches, plays a full round to a resolved scoreboard, and returns/relaunches without error; combat scoring behaves; the Boneyard arena builds as intended. FAIL: missing scripts · a scene/controller that throws on load or launch · the round never resolving · a scoreboard that doesn't tally combat hits/kills · a crash on return/relaunch.

### QA-BENDS-MODE ⬜ — "The Bends": the Dolphin-only debuff duel (GameModes.Bends = 42)
Source: PR #752 (`dolphin-dogfighting-game`). A new mode — `GameModes.Bends = 42`, Dolphin-only debuff duel — 42 files, 12,899 insertions, authored headless.

1. Open the Bends scene / launch the mode: no missing scripts; controller + scoring rule wired; the arena builds.
2. Launch (AI backfill for solo): reaches gameplay without an exception; confirm it is Dolphin-only.
3. Play the debuff-duel loop — the win/scoring condition (debuffs applied / duel outcome) behaves as designed.
4. Play a full round to resolution; scoreboard resolves; return to menu and relaunch once — clean.

PASS: scene clean; the mode launches Dolphin-only, plays its debuff duel to a resolved scoreboard, and returns/relaunches without error. FAIL: missing scripts · a controller that throws on load/launch · the duel/scoring not resolving · a crash on return/relaunch.

## Priority 1 — merged features that have never been played

### QA-ECOLOGY-WORM-KAIJU 🟡 — the worm colony boss
> **Last result:** 🟡 PARTIAL — Almost every PASS condition is true; the outstanding one is "devours creatures at the jaws and pursues pilots" — not observed yet.  _(build bleeding-edge @ 9e8cf3f · Unity 6000.4.11f1.x · Windows, Unity Editor, 2026-08-10, andrew)_

Source: PR #667. Reference: `Docs/ECOSYSTEM.md` §23.6 (spawn steps + dials).

1. Freestyle → Lifeform Matrix toy → "Worm Colony" → any element station.
2. Watch it move, feed (prism mass, other creatures, and you), and grow.
3. Kill a mid-body segment and watch what happens to the colony.
4. Kill the head; kill the tail. Watch each death sequence to completion.

PASS: the colony slithers follow-the-leader; it grazes mass, devours creatures at the jaws and pursues pilots; growth is funded by feeding; a mid-body kill splits the colony into two viable colonies; head/tail (capital) deaths each drop exactly one elemental crystal and body segments drop none; every death withers (extremities first) rather than vanishing. FAIL: any segment popping out of existence · a split that strands a headless or tailless remnant · a body segment dropping a crystal, or a capital dropping none · the colony feeding on nothing / never growing · exceptions in the Console.

### QA-ECOLOGY-HESPERIDES ⬜ — the garden cell
Source: PR #646 (9 hand-authored prefabs + 56 SO assets, first import). Reference: `Docs/ECOSYSTEM.md` §21.7.

1. Freestyle → Cell Selector toy → Hesperides. Watch it build.
2. Run Measure Cell Environment Baselines on `SpawnableHesperides` — expect ≈ 12,060 prisms / ≈ 507k volume.
3. Run Validate Lifeform Crystals — the eight new flora prefabs must pass.
4. Stay in the cell and let flora grow through several waves. Watch the eight forms (Arbor/Rosette/Frond/Coral/Spire/Tendril/Reed/Lantern) plant on their site kinds (beds, climbs, baskets, water, ledges).
5. Check the phase readout over time (it must not boot straight to Frenzy).
6. Plant-test the three repaired flora (Pine, Nerve, Wall) — they had a dangling `cellData` GUID and have not been planted since the repair.

PASS: imports clean; baseline within a few hundred prisms / few thousand volume; crystal validator green; flora actually grow on the authored sites and the garden thickens toward the mature planting; phase ladder behaves; the three repaired species plant without throwing. FAIL: import errors or `None` references (a null `prism` builds a silent, empty cell) · baseline off by more than a few hundred (PhaseThresholds must be re-authored) · flora planting inside each other / floating / not planting at all · an exception from `Flora.Plant()`.

### QA-ECOLOGY-CALDERA-OUROBOR ⬜ — the two nucleus-aware cells
Source: PR #645. Reference: `Docs/ECOSYSTEM.md` §18.3.

1. Cell Selector → Caldera. Then → Ourobor. Confirm each imports and builds.
2. Run Measure Cell Environment Baselines: expect Caldera 41,353 / 1,210,753 and Ourobor 37,889 / 751,449.
3. Caldera: confirm four inward-aimed massifs in tetrahedral symmetry, no ground plane, and nothing laid inside the nucleus radius.
4. Ourobor: fly a full lap of a band — confirm countryside + cityscape on both faces and that no global "up" survives the lap.
5. In both: sanity-check danger-prism density (does it play hot or cold?).

PASS: both import with no missing scripts and no `None` refs; baselines within a few hundred of the expected counts; Caldera's nucleus interior is empty; Ourobor's bands read as continuous two-sided worlds. FAIL: a cell that builds zero prisms · baselines off by >few hundred · any prism inside Caldera's nucleus · a band that reads as flat/one-sided or disorienting to the point of unplayable (note it as PARTIAL + a note rather than FAIL if it is a taste call).

### QA-ECOLOGY-FREESTYLE-SIX ⬜ — the prepopulated cells + the deferred menu build
Source: PR #636 (gated minigame loads already field-verified; the rest is not).

1. Launch to `Menu_Main` repeatedly until a non-Blob cell rolls, if the boot still rolls worlds; otherwise pick each of the six via the Cell Selector.
2. Watch when the veil appears relative to the menu settling, and listen to audio during the build.
3. Watch the prism counter run to completion; then confirm the veil fades into a fully-grown world.
4. Fly through each cell: phase ladder behaviour, clearance pads keeping spawns and crystals clear, shielded/danger accents reading correctly.
5. Run Measure Cell Environment Baselines and compare with: Yggdra 34,340 · Daedala 33,858 · Orrery 34,573 · Zephyr 36,069 · Caldera 31,194 · Geode 34,365 · Atlantis 69,078.

PASS: the veil appears after the menu settles, audio stays clean, the counter completes and the world is fully grown when the veil lifts; no cell sits in Frenzy at rest; baselines match. FAIL: a build that wedges (look for the `CloneBatchAsync` watchdog warning in the log) · audio underruns/stutter during the build · the veil lifting on a half-built world · a cell permanently in Frenzy.

### QA-TOYS-CELL-SELECTOR ⬜ — opt-in worlds, and the freestyle reset
Source: PR #638. Reference: `Docs/ToySystem/BACKLOG.md`.

1. The headline: enter `Menu_Main` cold — no veil, no "GROWING…" hold. Launch an arcade game and return — same. Console should log the Cell assigning Blob.
2. Fly the Cell Selector (≈300° around the membrane ring), pick e.g. Yggdra: old world suctions away, veil raises with the prism/percent readout, Yggdra grows in. Check the cell then reads Calm, not Frenzy.
3. The riskiest path — the reset. With a world loaded and a long trail laid, fly the toy and pick the same cell. Do this on the Squirrel specifically.
4. Repeat the reset several times, then lay fresh trail.
5. Run the Wanderway conveyor, then reset the cell.

PASS: cold boot and game-return are veil-free; a pick suctions the old world and blooms the new one behind one veil; picking the current cell resets freestyle cleanly; no `Trail`/`TrailFollower` NullReferenceExceptions on the Squirrel; after several resets pooled trail prisms still spawn at full size; the Wanderway belt survives a reset untouched. FAIL: a veil on cold boot or on return from a game · any NRE during a reset · shrunken/zero-scale trail prisms after a reset (suction scale baked into the pool) · the conveyor's scenes vanishing or duplicating.

### QA-TOYS-EMBLEMS ⬜ — every toy is an icon of what it selects
Source: PR #655 (~2,400 lines, nothing compiled or run).

1. Enter freestyle — watch for any emblem visibly assembling during the bloom. Check this on a return from an arcade game, not just cold boot.
2. Compare Load Time Insights before/after: no new Environment-category span.
3. Fly the whole membrane ring. For each toy, ask: identifiable without reading the label? Record each failure.
4. Fly Wanderway → orbit spins up over ~0.8 s; leave freestyle → drops to a dormant crawl; fly again → stops. While doing this, watch other toys' colours.
5. Fly the domain changer → the vessel-changer emblem hulls re-tint within 0.5 s. Swap ship → the emblem core becomes the new hull and keeps spinning.
6. Cell Selector emblem: at boot it is a small bare core; pick a world → after the veil it blooms as that world; pick the environment-free cell again → the placeholder returns (not an invisible station).
7. At the Lifeform bench, look at the seven flora icons.

PASS: no emblem assembles in view; no new load span; the Wanderway spin states behave and no other toy changes colour; the vessel/domain emblems re-tint and re-shape; the Cell Selector emblem tracks the loaded world; flora icons read as branch/lattice/surface forms, not spheres. FAIL: an emblem building in view · another toy changing colour when Wanderway spins (shared-material bug) · an invisible station · spheres where flora forms should be. (Ring-distance legibility is a judgement call — report failures as notes, not FAIL, unless a toy is genuinely unidentifiable at ring distance.)

### QA-TOYS-WANDERWAY-RUN ⬜ — grand scale, the tether, and the way home
Source: PR #654. Reference: `Docs/ToySystem/BACKLOG.md` ▸ "Wanderway — the run".

1. Fly the toy → the cell suctions away and returns as bare Blob behind one veil.
2. Fly outward and watch your trail length settle.
3. Turn around → the return station should be riding the tail of your tether. Fly it.
4. Wander again → the belt resumes with no second veiled build (watch the prism count).
5. Exit a run via the overview button and via gamepad Start.
6. Repeat step 2 on the Squirrel, riding your own tether.

PASS: one veil, one build (30k prisms) ever; the trail stabilises at ~100 prisms and stays there; the station sits one tether-length behind and glides (not snaps); flying it returns you home with speed intact; both alternate exits end the run; the Squirrel rider stays put as the tail recycles. FAIL: trail length climbing without bound (a ribbon is not rolling) · a doubled prism count on the second wander · the station snapping or unreachable · a Squirrel thrown off its own tether · scenes visibly popping in or out of existence.

### QA-ECOLOGY-ELEMENT-LEVEL-MATRIX ⬜ — full element × level spawn spread
Source: PR #635.

1. Open a spread-enabled spawn config in the inspector: confirm `Spread Elements`, a 4-entry `Element Palette`, and a populated `Levels` block. (If a field reads default, re-save the asset from the inspector.)
2. `Menu_Main` / Blob cell, freestyle, watch a few fauna waves.
3. Kill a level-5 creature and look at the crystal it drops.
4. Let a brood reproduce and compare the offspring's element with the parent's.
5. Lifeform Matrix toy: spawn each station's advertised variant.
6. Play Skim Race and Nucleus Rush briefly and judge whether cadence still feels right.

PASS: one species' brood shows all four crystal models (not just recolours) and visibly mixed body sizes with occasional giants; a level-5 death drops a visibly larger crystal; offspring match the parent's element; the matrix spawns exactly what each station advertises. FAIL: a single element across a whole brood · uniform body sizes · a brood whose offspring change element · a matrix station spawning the wrong variant.

### QA-ECOLOGY-FAUNA-FEEDING ⬜ — intentional feeding + shark predation + jaw rig
Source: PR #614, shark-jaw `438070a2`, checklist entry in `Docs/UNITY_VERIFICATION_CHECKLIST.md`. Design: `Docs/ECOSYSTEM.md` §7/§7.3.

1. Open `Assets/_Models/Fauna/MassSharkFauna.prefab`: confirm `SharkJawDriver` sits on `Shark_model` beside the `Animator` + `RigBuilder`, both mouth `MultiAimConstraint`s and the `MawTarget` are present and wired, and weight 0 = closed / 1 = aimed at `MawTarget`.
2. Confirm the tadpole's `FaunaConfigurationSO` / prefab Variant carries its intended elemental setup and points at the creature prefab's `Boid`.
3. Play `Menu_Main` (Blob cell) and watch herbivores approach mass.
4. Watch tadpole swarms around a concentration of mass.
5. Watch sharks: entry point, hemisphere, pursuit, and rhythm over ~60 s.
6. Watch a shark's mouth (and the danger prisms parented to the jaw bones) across a hunt cycle.

PASS: herbivores approach → brake → turn to face → suction, and park to graze a buildup rather than drifting past; tadpoles settle instead of ping-ponging; sharks enter from top/bottom ~1 per 30 s wave, stay in their hemisphere, visibly pursue, and show a ~10 s hunt / ~10 s rest rhythm; the mouth yawns open (≈0.6 s) entering a hunt and eases shut (≈1.8 s) at rest with the teeth moving with it and no snap at spawn. FAIL: herbivores vacuuming mass at range without facing it · swimming past food · oscillating tadpoles · sharks everywhere at once or never resting · a jaw that never moves, snaps, or leaves its danger prisms behind.

### QA-ECOLOGY-HERBIVORE-RULES 🟡 — spawn rotation / shielded diet / steering, after the buff merge
Source: PR #631 (verified in-editor before `DomainFaunaBuffSystem` landed).

1. Lobby/Blob: watch several fauna waves — do groups rotate around the spawn ring, and does a full wave hatch?
2. Skim Race: watch brittlestars pick feed targets around the super-shielded track.
3. In freestyle, watch the elemental petal bars while a big live fauna population is up.

PASS: waves rotate around distinct ring points; brittlestars never target shielded or super-shielded mass and never stall staring at it; the petal bars climb faster to 10 with transient spikes above it, and settle back to at most 10. FAIL: every wave seeding at the same point · fauna steering onto shielded mass · a creature frozen mid-approach · any element held above level 10.

### QA-VESSEL-RHINO-SWORD ⬜ — sword point-velocity + the debris retune
Source: PR #639. Reference: `RHINO_SHIELD_SWIPE.md` § In-editor verification (5–11).

1. Fly straight with no trigger: hit a prism with the hull, then hit one with the parked sword at the same speed.
2. Mid-swipe: hit prisms with the tip and with the hilt. Select the ForceFieldSkimmer in play mode to see the per-point velocity gizmo rays.
3. Clip your own Rhino trail (small prisms, vol ≈ 0.75) and a fat environment prism at the same speed.
4. Fly a couple of other vessels and fire projectiles at prisms.
5. Play Astro League and trigger a field reset.

PASS: hull and parked-sword hits throw debris at the same speed; a tip strike visibly beats a hilt strike and throws along the swing tangent; small and large prisms at the same speed match; other vessels/projectiles throw debris at ~1/3 the old speed with nothing else changed; Astro League's field-reset prisms animate out instead of freezing. FAIL: a parked sword adding speed · tip and hilt identical · debris speed varying with prism size · debris pinned to one speed regardless of impact. Judgement call to report: shatter is now ~3× slower on gentle grazes (violence tracks force by design). Say whether the slow end reads as sluggish.

### QA-VESSEL-AOE-IMPULSE ⬜ — explosion inertia, the Dolphin cone, and debris spin
Source: PRs #652, #632, #643.

1. Select `_Prefabs/Projectile/AOEConicExplosion.prefab`: confirm Inertia 1.8 / Proportional Debris ✓ / Debris Restitution 0.333 in the inspector.
2. After a HexRace/Skim track spawn (so pools have cycled super-shielded prisms), lay a Squirrel overheat danger trail, then detonate a Dolphin crystal blast into it.
3. Dolphin + crystal in open space: watch the cone's reach and where destruction ends.
4. Watch the direction struck prisms fly.
5. Fire one spherical AOE (e.g. Rhino) as a regression check.
6. Blow up prisms at a range of impact speeds and watch debris tumble.

PASS: the blast expands through danger and regular-shielded prisms (shields pop, danger takes damage) and stops only on stellated super-shielded prisms; the cone mesh and its destruction both reach ≈2400 units with a travelling wavefront; struck prisms fly radially from the apex, not from the wavefront; the spherical AOE is unchanged; debris tumbles noticeably more than before at the same flight speed and shatter timing. FAIL: the blast stopping on a danger prism · destruction falling short of the cone mesh · debris flying from the moving wavefront · flight speed or shatter pace changing with the spin tune (that would mean something else moved). Known cosmetic gap (report, do not fail on): the conic VFX spawn flash does not scale with the tripled height.

### QA-VESSEL-SPARROW-ROLL 🔴 — Sparrow rolls on prism hit
> **Last result:** 🔴 FAIL — Hitting a prism as the Sparrow shifts my movement (course is redirected) rather than rolling the vessel in place — matches the item's "still being deflected off-course" FAIL criterion.  _(build bleeding-edge @ eb85e1e · Unity 6000.4.11f1.x · Windows, Unity Editor, 2026-08-17, andrew)_

Source: PR #669 (two hand-authored assets, never imported; 60° is a guess).

1. Inspect the Sparrow's prism-effect container: `VesselRollByPrismEffect` in slot 0, inspecting cleanly (no `Missing (Mono Script)`).
2. Fly the Sparrow into prisms at a few angles and speeds.

PASS: the asset inspects cleanly and every prism hit rolls the vessel rather than redirecting its course; control is recoverable. FAIL: missing script · no roll · the vessel still being deflected off-course · a roll so violent it reads as a loss of control. Report the feel: `rollDegrees` (60) is a single inspector value — say whether it wants to be larger or smaller.

### QA-UI-ABILITY-ROW ⬜ — the four-icon ability row and its control hints
Source: PR #637. Note: hint placement failed in-editor three times on that branch after passing the author's arithmetic — treat play-mode confirmation as required.

1. Let Unity reimport the six HUD prefabs; watch the Console for import errors.
2. Run Audit Vessel Ability Rows (see QA-AUDIT-TOOLS).
3. Play Squirrel in freestyle: four icons lower-right in charge → mass → space → time with even spacing. Confirm `(LT)` sits under drift (2nd) and `(RT)` under the boost ring (4th). Without a gamepad you should see the keyboard set (`LShift`/`RShift`).
4. Raise one element to level 5 (elemental crystals or the comeback buff).
5. Play Sparrow: same order, each glyph beside its own ability.
6. Play Serpent: labels at the right edge (not mid-screen), silhouette/trail at the left, boost button unmoved.

PASS: four icons in element order on Squirrel and Sparrow; hints sit on their own ability; a level-5 element grows a white petal badge on that ability's icon and the icon rests slightly larger; the Serpent HUD lands on-screen where described. FAIL: icons out of order or missing · a glyph under the wrong ability or off-screen · no upgrade signal at level 5 · Serpent labels mid-screen. Known, do not fail on: Sparrow renders Xbox and PlayStation glyph sets at once, and its glyph art is wrong (`R1` where RT is meant) — both already logged.

### QA-VESSEL-HULL-MORPHS ⬜ — elemental hull morphs + the spliced Squirrel FBX
Source: PR #641. Highest-risk item here is the Squirrel FBX — a binary-spliced hybrid that has never been through the importer.

1. Let Unity import. Watch specifically for the Squirrel FBX reimport and any error.
2. Run Audit Vessel Elemental Morphs — expect 7/11 vessels with all four elements (Squirrel included).
3. Fly the Squirrel: confirm input puppetry (pitch/yaw/roll/throttle take blending) behaves as before.
4. With the `ResourceSystem` elemental test-harness sliders in play mode, sweep each element 0→10 and watch the hull.
5. Repeat on Sparrow / Serpent / Manta, comparing hull against the HUD flowers.
6. Look at the Dolphin — its engine-case animation changed (engines no longer dragged toward identity).

PASS: clean import with no meta regeneration; audit reports 7/11; the Squirrel's animation is unchanged from before the branch; hulls glide (never snap) between extremes and agree with the HUD flowers; levels below 0 hold the level-0 silhouette and above 10 hold the level-10 extreme; the Dolphin reads as fixed. FAIL: Squirrel FBX import errors or lost animation takes · a vessel with shape keys that never morphs · snapping instead of gliding · hull and flowers disagreeing · the Dolphin reading as a regression.

### QA-SCURRY-SPAWN-RING ⬜ — half-nucleus cell, crystal volume, cell-relative spawn ring
Source: PR #659 (the ring's first version silently spawned players inside the nucleus — that class of bug is what this item exists to catch).

1. Run `CellSpawnFormationTests` (covered by QA-EDITMODE-TESTS) — note the result here too.
2. Run Ecology ▸ Audit Cell-Owned Visuals: expect "SCENE-PLACED DUPLICATES: none" and "DEAD CELL OVERRIDES: none", with `SkyboxModel` entries under OK.
3. Open each of the 12 touched scenes so Unity reimports them — especially Recording Studio and MattsRecording Studio (their backdrop was left alone and must still render).
4. Play Crystal Capture. Read the console line `Spawn ring: N players at 236u (nucleus 196 + 40)`.
5. Play it at 4, 3 and 2 players.

PASS: audit clean; all 12 scenes reimport with no missing references and no console errors; both Recording Studio backdrops still render; you spawn outside the core facing it; 4/3/2 players give tetrahedron / triangle / opposite-poles; crystals fill the nucleus rather than a wide ball. FAIL: spawning inside the nucleus or at the old 70u radius · a formation that does not match the player count · crystals scattered outside the nucleus · a black Recording Studio.

### QA-ARCADE-SKIMRACE-INTENSITY3 ⬜ — new circuit + per-intensity laps
Source: PR #626 (scene YAML hand-authored; a silent fallback is the failure mode).

1. Open `MinigameHexRace.unity`, select the crystal turn-monitor object, and confirm Laps Per Intensity shows `3, 3, 2, 2`.
2. Launch Skim Race at intensity 3.
3. Race the full track; watch the lane braid and the 120-unit lane separation at speed.
4. Note the crystal target the HUD shows at intensities 3 and 4.
5. Glance at frame time on the target device (intensity 3 goes ~304 → ~848 track prisms).

PASS: the laps list shows `3, 3, 2, 2` (empty means it silently fell back to `optionalLaps` = 3 and the targets are wrong); you spawn behind the east circle's pole, merge onto the track heading +Z with the first crystal ahead; targets read 56 / 54 at intensities 3 / 4 (not 84 / 81); frame time is acceptable. FAIL: an empty laps list · wrong crystal targets · spawning off-track or facing the wrong way · a frame-time regression on device. Judgement call: race length. Say whether intensity 3 runs long.

### QA-HAPTICS ⬜ — the two feels, and the silence around them
Source: PR #610. Reference: `Docs/HAPTICS.md`. Needs a device or gamepad — haptics are a no-op on desktop without one.

1. Confirm `SquirrelImpactorDataContainer`, `SkimmerHapticsByPrismEffect` (`Min Strength = 0.35`) and `VesselHapticsByPrismEffect` import with no missing scripts.
2. Skim a run of prisms on the Squirrel.
3. Crash the vessel body into a prism.
4. Do both together — crash while skimming.
5. Tap UI buttons; boost; drift; joust; set off an explosion.
6. Toggle Haptics off in Settings, then move the level slider.
7. On iOS specifically, repeat steps 2–3.

PASS: a bright rapid pulse train while skimming that intensifies toward the skimmer centre; one heavy low thud on a body crash that interrupts the train and never machine-guns; nothing from UI/boost/drift/joust/explosions; the setting stops and scales both feels; iOS plays both. FAIL: silence on device during skims · a buzz on any of the silenced events · continuous rattling on crashes · the setting not taking effect · iOS failing to load the skim clip. Report the feel: the punish also fires when the Squirrel clips its own trail in a tight drift — say whether that reads as fair.

### QA-PALETTE-SHIELDED ⬜ — Ruby + Gold shielded prisms
Source: PR #644 (colorimetry verified by simulation, never by the engine). Reference: `Docs/PALETTE.md` §6.

1. Pull and let the colour-set asset reimport (a stale Library masks this entirely).
2. Get shielded prisms of all three domains on screen — easiest via a cell with lifeforms in freestyle, a `SegmentSpawner` track (HexRace / Skim Race), or Astro League.
3. Compare each domain's shielded vs unshielded prisms side by side.

PASS: Ruby and Gold facets read clearly; shielded is obviously distinct from unshielded within each domain; no domain blooms hotter or reads flatter than the others (Ruby's rim peaks at 1.19 and Gold's at 1.05, both below Jade's 1.22 — if Jade does not blow out, neither should these). FAIL: a domain blowing out under bloom · shielded reading dimmer than unshielded in the same domain · a domain that reads flat/muddy next to the other two.

### QA-PERF-DEATH-PATH 🟡 — re-profile the batched suction/explosion death path
Source: PR #658. Every frame-cost claim on that branch is structural, never measured. Reference: `Docs/PRISM_EXPLOSION_BENCHMARK.md` § "Re-profiling the death path".

1. Run the 5-run `bench` with throttles lifted per the doc.
2. Record the five `Prism.Destroy.*` markers (total + self ms) and GC/frame; compare against a run at `f0ddfc21`.
3. Separately, watch a cell with fauna feeding — the grid rig produces zero implosions, so suction has to be observed in play.
4. Watch the convergence point of a suction as the creature moves.

PASS: benchmark numbers recorded (this item's deliverable is data, not a verdict); suctions converge on the moving creature, animate for their full duration, and no prism is left frozen mid-suction. FAIL: a marker regressing sharply vs. the reference run · GC per frame appearing · suctions converging on a stale point or freezing. (The old ~0.43 ms self/death figure is stale — do not compare against it.)

### QA-SPARROW-PROJECTILE-POOL 🔴 — async-refilled pooled projectiles are injected
> **Last result:** 🔴 FAIL — Launch SFX play and there are no NREs, but normal shots sometimes pass straight through prisms, and the Console throws "Projectile already released! Should not call twice!".  _(build bleeding-edge @ 9e8cf3f · Unity 6000.4.11f1.x · Windows, Unity Editor, 2026-08-10, andrew)_

Source: PR #606. The failure mode is silent duds seconds after spawn.

1. Spawn a Sparrow (any mode or freestyle) and fire full-auto for ~30 s.
2. Then dump several skyburst missiles.
3. Watch the Console throughout.
4. Optional: confirm `PoolRefill.Projectile*` markers still appear in the profiler.

PASS: every shot has launch SFX and live colliders for the whole 30 s, including after the pools cycle through async-refilled instances; no `NullReferenceException` from `LaunchProjectile`; the async refill markers still appear. FAIL: any dud shot (no SFX / passes through prisms) or any NRE from `LaunchProjectile`.

### QA-RHINO-SKIMMER-SHAPE ⬜ — sword X/Z preserved, Space drives length, capsule follows the hull
Source: PRs #616 and #583.

1. Open `Rhino.prefab`: the ForceFieldSkimmer sits under `Rhino_Test (1)` (the fuselage), not `OrientationHandle`.
2. In play, pitch/yaw/roll the Rhino and watch the capsule and its collider gizmo.
3. Swap to the Rhino in freestyle and look at the blade at rest.
4. Skim prisms and watch the blade grow.
5. Collect Space crystals up to level 10, then take a Space debuff.
6. Watch the Rhino HUD's skimmer-scale fill.
7. Fly tight enough to clip your own just-laid trail.
8. Sanity-check a spherical-skimmer vessel (Squirrel).

PASS: the capsule sways with the hull instead of staying screen-fixed; the blade keeps its thin profile at all times; growth is along the long axis only; resting length grows toward 50 at Space 10 and shortens below 30 on a debuff; the HUD fill starts at the true base and tracks growth; the Rhino cannot collide with its own just-laid trail; the Squirrel's uniform scaling is unchanged. FAIL: the blade inflating into a sphere/box · the capsule glued to the camera · self-collision with fresh trail · the HUD fill starting mid-bar · Squirrel scaling changed.

### QA-RHINO-RAMP-BOOST 🟡 — the ramp boost's final (inverted) direction
Source: PR #613 (engage/release verified mid-branch; the final inverted FOV/Panini direction and the merged state were not). Reference: `RHINO_RAMP_BOOST.md`.

1. Hold full-speed-straight on the Rhino and watch speed climb.
2. Release and watch the return.
3. Wobble the stick mid-boost.
4. With a second client up, confirm the remote Rhino looks sane.

PASS: speed climbs linearly to ~6× over ~3.6 s; the view zooms in (narrower FOV) as speed rises; release returns in ~0.5 s landing exactly on the pre-boost FOV and Panini; no discrete "gear" steps. FAIL: stepped speed · the view zooming out instead of in · FOV/Panini not returning exactly to home · the remote client seeing something different.

### QA-TOYS-WANDERWAY-INVISIBLE ⬜ — the conveyor's transport is never watched
Source: PR #609.

1. Freestyle, fly the Wanderway toy, then fly straight for a while.
2. Hard-turn and reverse over ground you just covered.
3. Vary speed from cruise to boosted and watch the field ahead.

PASS: scenes only ever bloom in far ahead — never in your face; you never watch a scene suction away in view (on a reverse the old ribbon waits, briefly idling, until it has left your view); the field still holds ~7 scenes ahead at all speeds. FAIL: a scene appearing close in front of you · watching a scene shrink away on screen · the field starving (fewer scenes ahead) at high speed.

### QA-UI-TRAIL-DISPLAY-REMOVAL ⬜ — nothing broke when the silhouette HUD was deleted
Source: PR #634 (prefab YAML surgery across 6 vessel prefabs + 3 HUD prefabs).

1. Open each vessel prefab and `GameCanvas.prefab`, `GameCanvas-HexRace.prefab`, `MiniGameHUD.prefab`. Look for missing-script warnings.
2. Play a round on Squirrel and on Sparrow and watch the elemental petal bars.

PASS: no missing-script warnings anywhere; petal bars build, colour and animate correctly on both vessels. FAIL: any missing script · petal bars absent, mis-coloured or static.

### QA-FTUE-QUEST-ROWS ⛔ — quest graphs lay out in venue rows
> **Last result:** ⛔ BLOCKED — Could not run — the Quest Graph Editor does not exist on this build. There is no `FrogletTools ▸ Quest Graph Editor` menu, and no `Quest Graph ▸ Layout All Phases (Rows)` menu item; the only FrogletTools graph/layout entry is the unrelated Prism "Auto-Wire Clock Properties". The absence itself is the finding — PR #633's editor tooling appears not to be present on bleeding-edge (same class of gap as the missing "Validate Lifeform Crystals" tool). Nothing about node layout could be judged.  _(build bleeding-edge @ b0cf4f0f · Unity 6000.4.11f1.x · Windows, Unity Editor, 2026-08-11, andrew)_

Source: PR #633 (six graph assets rewritten by script).

1. FrogletTools ▸ Quest Graph Editor → MainQuest → click through Phases 0–5.
2. On any phase: drag a node somewhere silly → Layout Rows → then `Ctrl+Z`.
3. Press Save once per phase.
4. Run FrogletTools ▸ Quest Graph ▸ Layout All Phases (Rows).

PASS: every phase opens already in rows with edges intact and no node stacked at the origin; Layout Rows re-snaps and undo restores the drag; Save produces a no-op diff; the menu item reports 6 graphs / 17 rows with no further diff. FAIL: nodes stacked at the origin · lost edges · a Save that rewrites the assets substantially · an exception from either menu item.

### QA-SETTINGS-DISPLAY ⬜ — pixel-aware auto-detect + macOS fullscreen
Source: PR #651.

1. On macOS, run the player fullscreen.
2. On a high-DPI display, let auto-detect run and inspect the recommended render scale, AA and upscaling.
3. On a low-DPI display, repeat.

PASS: macOS fullscreen shows a correct borderless window (no black window, no offset mouse, correct backbuffer); render scale is clamped 50–100 % and never supersamples; MSAA steps down on high-DPI panels; a low-DPI panel is unaffected. FAIL: a black or offset macOS fullscreen window · a render scale above 100 % · identical recommendations on wildly different displays.

### QA-UI-MODAL-STACK ⬜ — modals closed outside the API no longer corrupt the stack
Source: PR #649.

1. From the Arcade screen, open the configure modal and close it with the ✕, with the background tap, and with the Home nav button in turn.
2. After each close, navigate between screens and reopen a modal.

PASS: navigation stays responsive after every close path; reopening works; the Home button is never left disabled. FAIL: a screen that will not navigate, a dead Home button, or a modal that cannot be reopened after one of the close paths.

### QA-STATE-RESET ⬜ — runtime game state resets to defaults between sessions
Source: PR #647.

1. Play a game to the end, return to the menu, and launch a different mode.
2. Repeat with the same mode twice (use Play Again where available).

PASS: the second launch starts with a clean score, intensity, player count and domain assignment — no leakage from the previous round. FAIL: any carried-over score, stale player count, or a domain that was not reassigned.

### QA-TOOLING-SHIP-PANEL ⬜ — the editor tool ship panel actually pushes
Source: PR #663 (buttons never pressed in a running editor). Do this on a throwaway branch, not on `bleeding-edge`.

1. FrogletTools ▸ Build ▸ Pending Tool Changes. Confirm the branch pill shows your branch in green (or red + blocked on `bleeding-edge`).
2. Dirty a throwaway asset, hit Refresh — it should appear under Other uncommitted project files.
3. Tick it, Push N selected, and check the resulting commit.
4. Repeat with something else deliberately `git add`ed first.

PASS: the dialog lists exactly the selected path; the commit contains only that path; a protected branch is refused; the pre-staged file stays staged and out of the commit. FAIL: anything else riding along in the commit, a protected-branch push succeeding, or the pre-staged file being swept in.

### QA-FLORA-LEAFSIZE 🔴 — garden flora still grow leaves at the authored size
> **Last result:** 🔴 FAIL — No reference for what the leaf size should be, and nothing that looked like a leaf to analyze in the first place.  _(build bleeding-edge @ 9e8cf3f · Unity 6000.4.11f1.x · Windows, Unity Editor, 2026-08-10, andrew)_

Source: PR #656 (a duplicate declaration removed after a semantic merge conflict).

1. Confirm zero compile errors.
2. Freestyle → Cell Selector → Hesperides; look at leaf size on grown flora.

PASS: compiles clean and leaves grow at the authored size. FAIL: compile error, or leaves that are obviously too big/small/absent.

### QA-NET-PRESENCE-PARTY 🟡 — party/presence regression pass
Source: PR #666 + `Docs/PartySystem/BUGS.md` (B2/B3/B5 open) + `Docs/PresenceSystem/BUGS.md` (B4/B6 open), B12 graceful path never exercised. Needs MPPM with 3–4 virtual players, and one standalone build for the graceful-quit case. Procedures: `Docs/PartySystem/TESTS.md` (S-series) and `Docs/PresenceSystem/TESTS.md` (P-series).

1. Run the S-series and P-series test cases as written.
2. B12 departure specifically, distinguishing all three cases: graceful quit (in-game button / alt-F4) → expect < 1 s; hard kill / MPPM virtual-player deactivation → expect ~30–50 s (UGS reap, correct); editor play-mode stop → < 1 s if the wire was reached, else reap.
3. Record the observed fault rates for presence reads and party-session reads over two independent runs (last measured: ~12 % and ~32 %).

PASS: B11/B13/B14 stay fixed (all instances reach `Present`; peers promote CONNECTING… → ONLINE; no Relay 500 on boot); the graceful-quit path evicts in < 1 s; fault rates are no worse than the last measurement. FAIL: any of B11/B13/B14 recurring, a graceful quit taking the reap path, or fault rates rising. Note B2/B3/B4/B5/B6 outcomes as data — they are known-open, so they do not fail this item, but their current behaviour is what we need recorded.

### QA-SPARROW-BOOST-REDESIGN ⬜ — overheat removed, base strafing roll, Elemental Ward (Time-5)
Source: PR #675 (`sparrow-ability-redesign`). Touches a **platform** surface (`ResourceSystem.ApplyElementalEffect`) plus two vessel prefabs edited as hand-written YAML — a removed GameObject, a removed resource slot, renamed serialized fields. Prefab integrity is the real risk. Reference: `SPARROW_AFTERBURNER.md` § In-editor verification.

1. Project compiles with zero errors; no new console warnings on Sparrow or Serpent spawn. (Known pre-existing, not this branch: the Sparrow's `ElementalBarsController.view` reference is already dangling on `bleeding-edge`.)
2. **Prefab integrity (top risk).** Open `Sparrow.prefab`: no missing-script slots; the `OverheatingBoostActionExecutor` child is gone; the `ResourceSystem` list reads Missiles / FullAuto / ExhaustBarrage (3 entries, no Heat); `SparrowHUDController.barrelRollController` points at the root's `BarrelRollController`; `VesselElementalImmunity` on the root reads `WhileBoosting` + `Time`. Then `Serpent.prefab`: `VesselElementalImmunity` on the root reads `WhileTranslationRestricted` + `None`.
3. Hold boost 60 s — no force-release, no danger trail, no self-slam (overheat is gone).
4. Time at 0: boost + full stick deflection rolls **once** per press (roll is now base kit, no Time gate).
5. The boost (rightmost) ability icon's ring: full on press, wipes empty with a punch on roll, empty until the next press — never a partial fill.
6. Time ≥ 5 (`ResourceSystem.TimeTestHarness = 0.5`): a danger prism **while boosting** → element flowers do not dip; **not boosting** → they dip. Slow and input-mute still land either way (by design).
7. Serpent stopped + danger prism → no flower dip at any Time level.
8. **MPPM two clients**, both Sparrows, one at Time 5: both machines agree on who resists the drain (replicated `NetElementUnlocks` path — a local read would pass step 6 and fail here).
9. `FrogletTools ▸ Vessels ▸ Audit Vessel Ability Rows` — Sparrow still 4/4 in charge → mass → space → time.

PASS: compiles clean; both prefabs intact per step 2; unlimited boost with no overheat side-effects; base-kit roll once per press; binary roll pip; Ward blocks only the elemental drain while boosting (Sparrow) / while stopped (Serpent), never the slow or mute; both peers agree; ability-row audit still 4/4. FAIL: compile error · any missing script or leftover Heat/overheat slot · boost force-releasing or laying a danger trail · roll not firing at Time 0 · Ward blocking the slow/mute, or blocking while not boosting · peers disagreeing · audit not 4/4.

### QA-SPARROW-STOPPED-ROLL ⬜ — strafing roll works stopped, pitch/yaw 3× in the stationary stance
Source: PR #679 (`sparrow-strafing-roll-stopped`). Code only — no prefab/scene/SO touched — so the risk is a compile check plus feel. Also raises the stopped turn rate on the **shared** `VesselTransformer`, so the **Serpent inherits it**. Adds `ShipModifierTests`. Reference: `SPARROW_AFTERBURNER.md`.

1. Project compiles; run `CosmicShore.Tests.EditMode` — `ShipModifierTests` gained two cases pinning the new `ignoresTranslationRestriction` flag.
2. Sparrow, **flying** roll first (regression risk): boost + full left stick → rolls and strafes once per press. Must be **unchanged**.
3. Toggle the stationary/turret stance. Boost + full left stick → **rolls and strafes**; speed does not change; still once per press; charge ring arms and wipes as when flying.
4. After the stopped roll: still stopped, still in fire mode, no trail/bridging prisms laid.
5. **Stale-course check:** stopped, rotate to aim well away from your stop heading, then dodge — the strafe must go where the stick points relative to your **current** facing (a skew toward the old heading = wrong projection plane).
6. **No banked lurch:** stopped, take a knockback (Rhino ram / danger prism) — you must not move; release the stance — you must not lurch.
7. **Stopped turn rate:** flying, time a full 180° yaw; toggle the stance and repeat — roughly a third the time; pitch likewise; release and the rate drops straight back.
7b. **Serpent:** stop into its weave stance, take a knockback, release — no movement stopped, no lurch after; note its pitch/yaw are **also 3×** stopped (same default). Any vessel: flying turn rates unchanged everywhere.
8. **MPPM two clients:** roll while stopped on A; B sees the same displacement.

PASS: compiles + `ShipModifierTests` green; flying roll unchanged; stopped roll strafes once per press with no speed/stance change and no prisms laid; strafe follows current facing; no movement/lurch from a stopped knockback; stopped pitch/yaw ~3× and snaps back on release; Serpent behaves the same; both peers agree. FAIL: compile/test failure · flying roll changed · stopped roll not firing, changing speed, or laying trail · strafe skewing to the old heading · a knockback moving or a release lurching a stopped vessel · stopped turn rate not ~3× or staying fast after release · peers disagreeing.

### QA-DISPLAYNAME-VALIDATION ⬜ — one validated path for display names (filter/format/no-duplicates)
Source: `display-name-validation` + PR #674 (`errors`) + the Cloud Save namespace fix in PR #677. A 494-line `DisplayNameValidator`, a `DisplayNameRegistry` over Cloud Save, and a 243-line `DisplayNameValidatorTests` suite — first import, and the registry needed two separate Cloud Save 3.4 API-namespace fixes (real compile risk).

1. Project compiles with zero errors — specifically no unresolved Cloud Save / `Unity.Services.CloudSave.Models` namespace errors in `DisplayNameRegistry.cs`.
2. Run `DisplayNameValidatorTests` in EditMode (also covered by QA-EDITMODE-TESTS) — all green.
3. In the profile/username setup UI: try an empty name, an over-long name, disallowed characters, and profanity — each is rejected or auto-formatted per the rules, with clear feedback.
4. Set a valid name; confirm it persists (Cloud Save) and shows in the profile widget and arcade profile.
5. Attempt a name that collides with an existing one — the no-duplicates path must reject it.

PASS: compiles clean; `DisplayNameValidatorTests` green; invalid names are filtered/formatted, valid ones persist and display, duplicates are rejected; no Cloud Save exceptions in the Console. FAIL: any compile/namespace error · a red validator test · an invalid name getting through or a valid one rejected · a duplicate accepted · a Cloud Save throw on save/load.

### QA-MENU-CAMERA-RIG ⬜ — Cinemachine menu camera replaced by a vessel-framing config rig
Source: `menu-camera-transitions`. `MainMenuCameraController` rewritten (~1,368 lines), new `MenuCameraConfigSO`, `Menu_Main.unity` edited, the old `CinemachineMatchTargetOrientation` deleted. Scene YAML + a large controller rewrite, never play-tested. Reference: CLAUDE.md § "Camera" / `Docs/CameraMigrationReview.md`.

1. Open `Menu_Main.unity`: no `Missing (Mono Script)`, no dangling Cinemachine references, the menu camera object drives `MainMenuCameraController` with `MenuCameraConfigSO` assets assigned.
2. Launch to `Menu_Main`: the autopilot vessel is framed cleanly (orbit / trail / chase / top-down configs each frame the local vessel), no jitter or wrong target.
3. Tap the centre crystal → the camera blends onto the gameplay pose and hands to the player-cam; centre-tap back → it eases back to the menu framing. Both transitions are smooth, no snap.
4. Navigate menu screens while the vessel drifts behind — camera stays stable.
5. (If available) join a second client via party invite — each client's menu camera follows its own vessel.

PASS: scene opens clean; every menu config frames the local vessel with no jitter/wrong target; the crystal in/out transitions are smooth with no snap; multi-client cameras stay independent. FAIL: missing scripts or dangling Cinemachine refs · a camera pointed at nothing / the wrong vessel · a hard snap or jitter on the freestyle transition · a camera that follows the wrong client's vessel.

### QA-PAUSE-MENU-RETURN ⬜ — smoother game→menu return + pause-panel prewarm
Source: `game-pause-menu-perf`. Changes the game→menu return path (`SceneLoader` +88 lines, `MultiplayerMiniGameControllerBase`) to unpause/veil/settle behind cover, and prewarms the pause panel so the first pause tap doesn't hitch. Return-flow changes can wedge, so this is play-verified, not read.

1. Play any arcade game, then return to the menu (Home/return button). The return is smooth — no frozen frame, no visible half-loaded menu, no lingering pause state.
2. In-game, open the pause menu on the **first** tap — it should appear without a hitch (prewarmed).
3. Do a full round → return → launch another mode, twice, watching for a wedged return or a stuck veil.
4. MPPM: a client returning to the menu is veiled and lands cleanly (no client racing the host's scene load).

PASS: every game→menu return is smooth and lands on a clean menu; the first pause tap does not hitch; no wedged returns or stuck veils across repeated launches; clients return cleanly. FAIL: a frozen/half-loaded return · a first-pause hitch remaining · a return that wedges or leaves a veil up · a client desyncing on return.

### QA-WILDLIFE-LIBERATION ⬜ — the Sparrow-only three-cage hunt (first free-for-all race)
Source: `wildlifeliberation-game-mode` (merged and re-tuned through `9b9d9b60`, 2026-08-07; not previously on the QA list). `GameModes.WildlifeLiberation = 40` — three concentric cages at 1050/600/200 pen three tiers of wildlife; first PLAYER to 500 kills wins. Made every creature shootable and generalized the fauna pen into a per-species band. Reference: `_Scripts/Controller/Arcade/WILDLIFE_LIBERATION.md`.

1. Launch `MinigameWildlifeLiberation` (any player count): no `Missing (Mono Script)`; the controller shows `WildlifeLiberationScoringRuleSO`; the Cell shows the three-cage containment.
2. Confirm three nested cages at 1050 / 600 / 200 with three tiers of creatures — a heavy swarm of small ones outside, bigger in the middle, biggest/toughest in the core.
3. As the Sparrow, shoot creatures' body prisms — every creature (not just the worm colony) dies to losing its body, withers (extremities first) and drops one elemental crystal.
4. Confirm creatures stay penned to their band — outside their annulus nothing is prey and goals clamp back in; no creature is led to mass it cannot reach/eat.
5. Play toward the target: the winner is resolved as a **PLAYER** (not a domain); domain sums stay as a secondary HUD readout. Reach 500 kills and confirm the round ends on that player.
6. Watch the collider budget / frame time — this mode is deliberately very heavy.

PASS: launches clean; three cages at the stated radii with the three tiers; every creature killable by body-prism destruction, withering to one crystal; creatures stay in their bands; the winner is a player at 500 kills with domain sums as secondary; frame time acceptable on target hardware. FAIL: missing scripts · a creature that can't be killed or that vanishes instead of withering · a body segment dropping a crystal or a capital dropping none · creatures escaping their band or being led to inedible mass · the mode resolving a winning domain instead of a player · a hard frame-time cliff.

### QA-SPARROW-PRISM-ATTACK ⬜ — Turret Stance fires real prisms (two flight visualizations)
Source: PR #696 (`sparrow-prism-attack`). Large (4,405 insertions): the Sparrow's turret stance now fires real prisms "on the bullets' terms", with a live-switchable A/B flight visual, a new prism-flight clock wired into both prism graphs, a new asset-writing editor tool (`Tools/Shaders/wire_prism_flight_clock.py`, 664 lines), and `PrismImplosion` changes. Space-5 now gates a shield. Reference: `_Scripts/Controller/Vessel/R_VesselActions/SPARROW_TURRET_STANCE.md`.

1. **Asset gates first:** run `python3 Tools/Shaders/wire_prism_flight_clock.py --check`, then FrogletTools ▸ Ecology ▸ Prism Animation ▸ Validate Clock Wiring (now requires the three `_Flight*` properties + `PrismFlightClock` on BlockGraph and ExplodingBlockGraph). Open both graphs — no import errors, `FlightStartTime/Duration/Velocity` on the Blackboard. If any prism is magenta on load, the graph failed to compile — FAIL.
2. Sparrow, stopped, hold fire in **TranslateAndGrow** (`FullAutoBlockShootAction.asset` ▸ Flight Visualization): prisms visibly leave the muzzles, scale up in flight, and anchor at ~286 u. Prisms teleporting to range with no flight, or `[PrismClock]` console errors, = wiring failure.
3. Flip to **ReverseSuction** live (next volley switches): faces stream from the moving shot point into the anchored shape; the real prism appears as the stream completes.
4. Confirm fired prisms behave as bullets: friendly-fire on, a pilot's own shots never destroy their own fired prisms, the 0.2 s placement-immunity window holds, and the hit sphere is sized to the projectile (not its z-stretch).
5. Raise Space to 5 → the shield gate engages; raise Charge to 5 → friendly fire still on, only the skyburst spared.

PASS: both asset gates pass and both graphs import clean (no magenta, no `[PrismClock]` errors); both flight visualizations render as described; fired prisms hit like bullets with the immunity window and correct hit size; Space-5 shield and Charge-5 sparing behave. FAIL: a failed wire/validate gate · magenta prisms · prisms teleporting to range · a visualization that shows nothing · a pilot destroying their own fired prisms · wrong hit size · the Space-5/Charge-5 gates not behaving.

### QA-DOLPHIN-CAPSULE-BLAST ⬜ — crystal blast sweeps a capsule aligned with the jaw gape
Source: PR #680 (`dolphin-echobliteration-capsule`). The Dolphin crystal blast's cross-section becomes a **capsule** (fixed width across the beam; skim energy buys length along the jaw-open axis). **Hand-authored asset YAML** — a `SphereCollider` rewritten into a `CapsuleCollider` in place (class id 135→136) — so step 1 is a genuine import check. Touches `PrismSpatialIndex`, `AOEConicExplosion`, `AOEExplosion`. Related: QA-VESSEL-AOE-IMPULSE.

1. **The hand-authored collider imported.** Open `_Prefabs/Projectile/AOEConicExplosion.prefab`: the root shows a **Capsule Collider** (Is Trigger ✓, Radius 0.0667, Height 1, Direction Z-Axis, Center 0/-0.5/0) — not a missing component, not a Sphere Collider, not a second collider alongside.
2. Project compiles (nothing here is `#if`-guarded, but no compiler ran author-side).
3. Empty-energy blast: fly to a crystal with no banked energy — the blast looks and destroys about as before, slightly lozenge-shaped, not a sphere.
4. Charged blast: bank skim energy, then detonate — the blast is a fan, **wide in the jaw plane, narrow across it**, and grows in **length** (not radius) with energy. The vessel-impact volume and the destruction volume are the same shape.
5. Regression: fire a spherical AOE (e.g. Rhino) — unchanged (CoreScale 0 collapses to the plain circular cone).

PASS: the capsule collider imported exactly as specified; compiles clean; empty blast reads unchanged; the charged blast is a length-growing jaw-plane fan with matching impact/destruction shapes; other vessels' blasts unchanged. FAIL: a missing/Sphere/duplicate collider on the prefab · compile error · the blast still a growing circular cone · impact and destruction shapes disagreeing · a spherical AOE that changed.

### QA-DOLPHIN-SKIM-ENERGY-CTA ⬜ — 15× skim-energy nerf, lime jaw CTA at full, dead silhouette removed
Source: PR #695 (`dolphin-prism-energy`). Skim banks 15× less energy per prism; the HUD jaw gauge arms **lime** at full energy; and the dead vessel-silhouette HUD element is deleted **fleet-wide** (YAML surgery removing content from 6 HUD prefabs — missing-script risk). Related: QA-DOLPHIN-SKIM, QA-UI-TRAIL-DISPLAY-REMOVAL.

1. Open the six touched HUD prefabs (`MantaHUDVariant`, `RhinoHUDVariant`, `SerpentHUDVariant`, `SparrowHUDVariant`, `SquirrelHUDVariant`, `VesselHUDPrefab`): no `Missing (Mono Script)` where the silhouette element was removed.
2. Dolphin freestyle: skim a run of prisms — energy now fills **much** more slowly (~15× more prisms to fill than before).
3. Fill the jaw energy gauge to full — it arms **lime** as a call-to-action; below full it does not.
4. Play a round on two other vessels and confirm their HUDs still build and animate (petal bars etc.) with the silhouette gone.

PASS: no missing scripts on the six prefabs; skim energy fills ~15× slower; the jaw gauge arms lime only at full; other vessels' HUDs intact. FAIL: any missing script · energy filling at the old rate · the lime CTA never arming or arming below full · a broken HUD on any vessel after the silhouette removal.

### QA-PROFILE-ADS-REMOVAL ⬜ — Unity Ads removed entirely + profile double-submit closed
Source: `profile-save-and-ads-removal`. Removes Unity Ads (package manifest + `RewardedAdsButton` deleted, `UnityConnectSettings`/`ProjectSettings` changed) and closes the display-name double-submit window. Package/manifest change = compile/build + package-resolution risk.

1. Project compiles and the package manifest resolves with no Unity Ads / Advertisement package errors in the Console.
2. Nothing in the menu references a missing `RewardedAdsButton` (no missing-script slots on the daily-reward card or anywhere ads UI lived).
3. The daily-reward flow works without the rewarded-ad path.
4. In the profile/username UI, submit a display name and rapidly tap submit again — the double-submit window is closed (no duplicate request / no error).

PASS: compiles and resolves packages cleanly; no missing ads-UI scripts; daily reward works ad-free; the name double-submit is prevented. FAIL: a package-resolution/compile error · a missing `RewardedAdsButton` reference · a broken daily-reward flow · a display name that still double-submits.

### QA-ASTROLEAGUE-REWORK ⬜ — Astro League as Rhino-only sword soccer (bigger court, strike feedback, food web)
Source: `astro-league-improvements` (feat `769eeb61`, + `17e9116f` court-shrink follow-up). Significant rework of the existing Astro League: bigger court then a 40% shrink, ball settling, strike feedback, smarter AI, and a "working food web" (touches `Cell.cs`, `CellLifeSpawnerBase`, `Fauna`, `ECOSYSTEM.md`). Reference: `_Scripts/Controller/Arcade/ASTROLEAGUE.md`, `Docs/ECOSYSTEM.md`.

1. Launch Astro League: no missing scripts; the court builds with its cage cover.
2. Play — the ball settles rather than drifting forever; striking it gives clear feedback; goals score and golden-goal resolves.
3. Confirm it plays as Rhino-only sword soccer and the AI is a credible opponent (not passive/stuck).
4. Watch the cell's food web over a couple of minutes — fauna spawn, feed and behave (no frozen creatures, no runaway population).
5. Score to the win condition and confirm the scoreboard resolves; return to menu cleanly.

PASS: court + cage build clean; ball settles, strikes give feedback, goals/golden-goal resolve; AI competes; the food web runs without frozen/runaway fauna; the round resolves and returns cleanly. FAIL: missing scripts · a ball that never settles or a strike with no feedback · passive/stuck AI · frozen or exploding fauna · a round that won't resolve.

### QA-SPARROW-MISSILE-BAY ⬜ — bay-animated skyburst launch with the real missile model
Source: PR #708 (`sparrow-missile-bay`). The Sparrow's skyburst now launches from a bay animation using the real missile model (`Sparrow.prefab`, `SparrowAnimationController`, `FireGunActionExecutor`, `SkyBurstGunAction.asset`). Reference: `_Scripts/Controller/Vessel/R_VesselActions/SPARROW_SKYBURST_BAY.md` + `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

1. Project compiles; open `Sparrow.prefab` — no missing scripts; the bay/missile wiring resolves.
2. Sparrow in freestyle: fire a skyburst missile — a bay animation plays and the **real missile model** launches (not a placeholder/instant spawn).
3. Fire several in succession — the bay animates each time and never jams/desyncs; the missile still flies and detonates as before.
4. Confirm normal full-auto fire is unaffected.

PASS: compiles, prefab intact; the skyburst launches from an animated bay with the real model; repeated fire animates cleanly; missiles fly/detonate normally; full-auto unaffected. FAIL: missing scripts · no bay animation or a placeholder model · a jam/desync on repeated fire · a missile that no longer flies/detonates · full-auto broken.

### QA-ECOLOGY-JOUST-WITHER ⬜ — joust takes the heart, starvation exposes it; both leave a skeleton
Source: PR #709 (`squirrel-joust-starvation-wither`). New `LifeformDeathStyle` enum + `HealthPrism`, `ElementalCrystalImpactor`, `VesselWitherLifeformByCrystalEffectSO`, `PrismSpatialIndex`, `FloraConfigurationSO` changes; wither cadence moved onto the variant config. A **LOCKED ecology** surface (continuity of existence / wither-to-crystal / mass conservation) — verify against those invariants. Reference: `Docs/ECOSYSTEM.md`.

1. Project compiles; a cell with lifeforms builds with no missing scripts.
2. **Starvation:** let a creature starve — it withers from its extremities inward, **exposes/leaves a skeleton**, and drops its elemental crystal. It does not vanish.
3. **Joust kill:** joust a creature to death (Squirrel) — the joust **takes the heart** (crystal), and the death still leaves a skeleton and withers rather than popping out of existence.
4. Confirm mass behaves: no creature pops in/out, and an interrupted wither still resolves (the deferred heart survives per `f12f9822`).
5. Watch a few waves — the wither cadence reads right (not instant, not stuck).

PASS: compiles; starvation withers-to-crystal and leaves a skeleton; a joust kill takes the heart and still withers/skeletons; nothing pops in or out; interrupted withers still finish. FAIL: a creature vanishing instead of withering · no skeleton left · a joust kill dropping no heart or a starved creature dropping none · an interrupted wither leaving a stuck/immortal husk · any missing script.

### QA-RAMPAGE-REBUILD ⬜ — Rampage rebuilt as the Dolphin's demolition race (four intensities)
Source: PR #717 (`dolphin-rampage-minigame`). A rebuild of the Rampage mode as the Dolphin's demolition race — 64 files, four intensities via a new `Tools/Build/rampage_intensity.py`, `SpawnProfileSO`/`GameDataSO` additions, crystals coupled to the nucleus, banded flora, AI-drift fix, and a fixed sticky cell-config race. Reference: `_Scripts/Controller/Arcade/RAMPAGE.md`, `Docs/ECOSYSTEM.md`.

1. Open the Rampage scene / launch the mode: no `Missing (Mono Script)`; the controller + scoring rule wired; the cell builds.
2. Launch at intensity 1, then at intensity 4 — the intensity ladder visibly differs (arena/population scale), not identical.
3. Confirm it plays as a Dolphin demolition race: the objective (omni) crystal is coupled to the nucleus and the objective arrow tracks only that managed crystal; flora is banded; AI drifts/plays sensibly.
4. Play a full round to the destruction target and watch the scoreboard resolve (environment-mass kills credited per-simulator and by domain).
5. Return to menu and relaunch once — clean, no leaked score/state.

PASS: scene clean; the four intensities differ; the demolition race plays with the nucleus-coupled objective crystal and correct arrow tracking; the round resolves on the destruction target with a sane scoreboard; return/relaunch clean. FAIL: missing scripts · identical intensities (config race not fixed) · the objective arrow tracking the wrong crystal · AI stuck/not drifting · a round that won't resolve · leaked score on relaunch.

### QA-PRISM-DEATH-TIER ⬜ — death visuals wear the dying prism's tier + danger-prism detonations
Source: PR #715 (`danger-prisms-explosions`). Prism **death visuals now wear the dying prism's TIER** (plain/danger/shielded/super-shielded), not just its domain; danger prisms carry a detonation gain with extended reach. `PrismDebris` + `PrismExplosion` changes, new EditMode suite `PrismDeathVisualTierTests`. Related: QA-VESSEL-AOE-IMPULSE, QA-PALETTE-*.

1. Project compiles; run `PrismDeathVisualTierTests` in EditMode (also under QA-EDITMODE-TESTS) — green.
2. Destroy prisms of different tiers (plain, danger, shielded, super-shielded) and watch the death debris/explosion — each reads as its **tier**, not a generic domain-coloured burst.
3. Detonate near danger prisms — the danger detonation gain visibly reaches farther than a plain destruction.
4. Regression: a normal (plain) prism death still looks/behaves as before; no magenta or missing VFX.

PASS: compiles + `PrismDeathVisualTierTests` green; death visuals differ by tier; danger detonations reach farther; plain deaths unchanged. FAIL: compile/test failure · all deaths looking identical regardless of tier · danger detonation with no extra reach · missing/broken death VFX on any tier.

### QA-RHINO-SWORD-V3 ⬜ — the energy-sword v3 rework
Source: PR #726 (`energy-sword-v3-rework`, after `energy-sword-rework-retry`). A v3 rework of the Rhino's energy sword. Builds on the earlier sword work (QA-VESSEL-RHINO-SWORD, PR #639) — verify the new behaviour end-to-end. Reference: `_Scripts/Controller/Vessel/R_VesselActions/RHINO_SHIELD_SWIPE.md`.

1. Project compiles; open `Rhino.prefab` — no missing scripts on the sword/skimmer.
2. Rhino in freestyle: swing the sword and hit prisms — the swing/point-velocity, growth, and shield interaction behave per the v3 design (no dead sword, no stuck scale).
3. Skim shielded/super-shielded track lining and confirm shell contacts still register (cross-check QA-SHELL-COLLISION).
4. Clip your own just-laid trail — no self-collision (cross-check QA-VESSEL-SELF-TRAIL).
5. Compare against the prior sword feel — the rework should read as intended, not a regression.

PASS: compiles, prefab intact; the sword swings, grows and damages as designed in v3; shell contacts register; no self-trail collision; no dead/stuck sword. FAIL: missing scripts · a sword that doesn't swing/grow/damage · broken shell contacts · self-trail collision · an obvious regression from the prior sword.

### QA-DOLPHIN-CRYSTAL-ENERGY-CLUSTER ⬜ — crystal-spawn rework, prism-collision energy, new skim effect
Source: PRs #720 (`dolphin-crystal-spawn-rework`), #721 (`dolphin-prism-collision-energy`), #723 (`dolphin-skim-effect`). A themed cluster of Dolphin energy/crystal changes — do them in one Dolphin session. Related: QA-DOLPHIN-SKIM, QA-DOLPHIN-SKIM-ENERGY-CTA.

1. Project compiles; Dolphin freestyle, no missing scripts.
2. **Crystal spawn (#720):** trigger the Dolphin crystal — it spawns/blooms per the rework (not popping), fires its effect, and behaves as intended.
3. **Prism-collision energy (#721):** ram/skim prism mass — the Dolphin banks energy from prism collision as designed; the HUD energy gauge moves accordingly.
4. **Skim effect (#723):** skim prisms and confirm the new skim VFX renders (no magenta, no missing effect).
5. Regression: normal flight/boost unaffected.

PASS: compiles; crystal spawns/blooms and fires; prism-collision energy banks and the gauge tracks it; the new skim effect renders cleanly; no regressions. FAIL: missing scripts · crystal popping/not firing · no energy from prism collision or a stuck gauge · magenta/missing skim VFX · flight/boost regressed.

### QA-PRISM-SHIELD-GPU-VISUALS ⬜ — octahedron-shield GPU morph + prism jiggle shader
Source: PRs #729 (`octahedron-shield-gpu-morph`), #727 (`prism-jiggle-shader-effect`), #730 (`prism-super-shield-jiggle-tests`). GPU-driven shield morph + a prism "jiggle" shader effect, with a new EditMode jiggle test suite. Shader work → magenta risk on shielded prisms.

1. Run the super-shield jiggle EditMode suite (PR #730; also under QA-EDITMODE-TESTS) — green.
2. Get shielded + super-shielded prisms on screen (a cell with lifeforms, a HexRace/Skim track, or Astro League). If any prism is **magenta**, a shader failed — stop, FAIL.
3. Watch a prism engage/disengage its shield — the octahedron shield morph now runs on the GPU; it should bloom/shatter smoothly, not snap or render the plain box (cross-check the exotic-visual handoff — a bare mesh swap renders nothing).
4. Watch the jiggle effect on shielded prisms — it reads as a subtle animated jiggle, not a static or broken look.
5. Confirm shielded collision still works (cross-check QA-SHELL-COLLISION).

PASS: jiggle suite green; no magenta; shield morph runs on GPU and blooms/shatters smoothly; jiggle animates; shell collision intact. FAIL: a red test · magenta prisms · a shield that snaps, renders as the plain box, or renders nothing · a static/broken jiggle · broken shielded collision.

### QA-ECOLOGY-GYROID-COLONY ⬜ — the gyroid octagon flora colony + Gyroid Lab
Source: PR #734 (`flora-populations-gyroid`). A gyroid octagon flora colony ("a crystal in every window") with population-event reproduction (one birth per fauna-wave cycle from the colony frontier), a maturity gate (reproduce only once fully grown incl. bloom), colony diagnostics, a lattice-defect auditor, and a 5× ceiling raise. LOCKED ecology surface — verify against continuity/mass-conservation invariants. Reference: `Docs/ECOSYSTEM.md`.

1. Project compiles; freestyle → Cell Selector → the gyroid colony cell (or Lifeform Matrix). It builds with no missing scripts.
2. Watch the colony grow: plants mature, and **reproduction happens once per fauna-wave cycle** (a population event, not per-plant timers), popped from the frontier.
3. Confirm a plant reproduces **only once fully grown** (maturity gate incl. the bloom) — no daughters spawning from immature plants.
4. Watch for lattice defects — the octagon lattice should be clean (no permanent holes at plant boundaries, no chirality/z-mirror twins); the defect auditor should not scream.
5. Continuity/mass: nothing pops in or out; growth withers/blooms; the colony ceiling behaves (grows toward the raised ceiling, doesn't freeze or explode).

PASS: builds clean; population-event reproduction from mature plants; a clean lattice with no boundary holes or mirror twins; continuity of existence and mass conservation hold; the colony grows toward its ceiling. FAIL: missing scripts · per-plant timer reproduction or immature daughters · lattice holes / chirality twins / auditor errors · anything popping in/out · a frozen or runaway colony.

### QA-ECOLOGY-CRYSTAL-LEVELING ⬜ — level is earned, heart size per level, crystal colours
Source: PRs #737 (`crystal-sizing-lifeform-leveling`), #728 (`lifeform-crystal-colors`). Lifeform level is now EARNED (not always 1), a dropped heart is one size per level, Mass/Charge crystals got a model-size correction (and the Charge bump was reverted — measure not infer), and lifeform crystals carry per-element colours. LOCKED ecology surface. Reference: `Docs/ECOSYSTEM.md`.

1. Project compiles; a cell with lifeforms builds clean.
2. Kill creatures at different levels — the dropped heart's **size scales with the level** (a level-5 kill drops a visibly bigger crystal than a level-1).
3. Confirm each element's crystal reads in its correct colour (Charge/Mass/Space/Time distinct), and Mass/Charge crystals are correctly sized (no oversized/undersized model).
4. Confirm level is actually earned over time (a fresh creature isn't stuck at 1 forever, and isn't rolled/handed a level it didn't earn).

PASS: compiles; heart size tracks level; per-element crystal colours correct; Mass/Charge model sizes corrected; level is earned. FAIL: uniform heart size regardless of level · wrong/duplicate crystal colours · a mis-sized Mass/Charge crystal · level stuck at 1 or handed unearned.

### QA-VESSEL-SELF-TRAIL ⬜ — don't skim or ram your own trail while laying it
Source: PR #736 (`vessel-self-trail-collision`). A vessel no longer skims or rams the trail it is actively laying (a self-collision grace while laying), scoped so the Rhino's signed-off self-farm still works. Impact-effects surface.

1. Fly a tight loop/curve so you cross your own **just-laid** trail — no skim trigger, no ram/slow, no self-damage from the fresh trail.
2. Confirm you CAN still interact with **older** trail (the grace is only for the trail being laid) once it's no longer fresh.
3. Rhino specifically: its signed-off self-farm behaviour still works (it can still consume/interact with its own trail where intended).
4. Other vessels: normal trail/prism interactions with non-self mass unchanged.

PASS: no skim/ram/damage from the trail you're actively laying; older trail still interacts; the Rhino self-farm still works; interactions with other mass unchanged. FAIL: still colliding with/slowing on fresh self-trail · unable to interact with older trail · the Rhino self-farm broken · other-mass interactions changed.

### QA-KEYBOARD-CONTROLS ⬜ — keyboard control scheme
Source: PR #722 (`keyboard-controls`). A keyboard control scheme (input strategy). Verify on desktop with keyboard.

1. Launch and take control of a vessel with the keyboard only (no gamepad).
2. Confirm pitch/yaw/roll/throttle and the ability inputs (boost, drift, fire, etc.) all map to sensible keys and respond.
3. Confirm menu/HUD navigation isn't double-driven or broken by the keyboard mapping.
4. Plug in a gamepad mid-session (if available) — the input strategy switches cleanly.

PASS: full vessel control from the keyboard with sensible mappings; abilities respond; no double-driven UI; gamepad hand-off works. FAIL: unmapped/broken controls · an ability with no key · keyboard driving the UI and the vessel at once · a broken device switch.

### QA-URCHIN-VESSEL ⬜ — the Urchin vessel revived (chain-reaction spikes + prismscape rider)
Source: PR #746 (`restore-urchin-vessel`). A whole vessel brought back — 100 files, 8,464 insertions, authored headless, with a new asset-writing tool (`Tools/Build/author_urchin_assets.py`) and an 854-line verification checklist. Reference: `Docs/UNITY_VERIFICATION_CHECKLIST.md`, `Docs/ElementalAbilitySystem/FLEET_MAPS.md`.

1. Project compiles; open `Urchin.prefab` — no `Missing (Mono Script)`; camera/telemetry/HUD/skimmer wiring resolves.
2. Fly the Urchin in freestyle: it spawns, is controllable, and its HUD renders (ability row, petal bars).
3. Exercise its abilities — the chain-reaction spikes and the "prismscape rider" behave as designed (spikes chain; the rider interacts with prism mass).
4. Confirm it swaps in cleanly via the Vessel Changer toy and inherits pose/speed.
5. Run FrogletTools ▸ Vessels ▸ Audit Vessel Skimmers / Ability Rows against the Urchin — record its verdict (may be design-blocked; note what the audits say).

PASS: compiles, prefab intact; the Urchin spawns, flies, and renders its HUD; chain-spikes and prismscape rider work; a Vessel Changer swap is clean; audits report clean or a known/annotated state. FAIL: missing scripts · a vessel that won't spawn/fly · abilities that don't fire · a broken HUD · a swap that throws · an unexpected audit failure.

### QA-DOLPHIN-ELEMENTAL-REWORK ⬜ — elemental map re-cut around one weapon + Time-5 Drift Ward
Source: PRs #740 (`dolphin-elemental-upgrades`, re-cut the elemental map around one weapon), #749 (`dolphin-time5-debuff-immunity`, Time 5 re-scoped to **Drift Ward** — elemental-debuff immunity **while drifting**). Vessel elemental-ability surface. Reference: `Docs/ElementalAbilitySystem/FLEET_MAPS.md`, `DOLPHIN_ENERGY_ECONOMY.md`.

1. Project compiles; Dolphin HUD shows four ability icons in charge → mass → space → time order (run Audit Vessel Ability Rows).
2. Raise each element to its unlock level (5) via crystals / test harness — the re-cut map's upgrades apply to the intended weapon/abilities, and the icon upgrade signal (badge/tint/scale) fires.
3. **Time 5 Drift Ward:** at Time ≥ 5, take a danger-prism/elemental debuff **while drifting** → the element flowers do not dip (immunity); **not drifting** → they dip. The slow/mute still land either way (by design).
4. MPPM two clients, one at Time 5: both agree on who resists the drain (replicated unlock state).

PASS: compiles; ability row 4/4 in order; the re-cut upgrades apply as intended; Drift Ward blocks the elemental drain only while drifting; peers agree. FAIL: compile error · wrong/missing ability order or upgrade signal · Drift Ward blocking when not drifting, or not blocking while drifting, or blocking the slow/mute · peers disagreeing.

### QA-TOYS-SWITCH-RING ⬜ — every freestyle toy inside a switch ring
Source: PR #750 (`freestyle-toys-switch-fundamental`). All freestyle toys are now placed inside a "switch ring" (a new asset-writing tool `Tools/Build/toy_switch_ring_geometry.py`). Reference: `Docs/ToySystem/ARCHITECTURE.md`, `BACKLOG.md`.

1. Enter freestyle (Menu_Main → take control): the toys sit in a switch ring around the membrane; no toy is missing or mis-placed, nothing assembles in view.
2. Fly each toy in the ring and confirm it still triggers its function (cell selector, vessel changer, domain changer, painting, Wanderway).
3. Confirm the ring re-arms correctly after use (a used toy doesn't switch you back before you fly clear).
4. Return from an arcade game and re-enter freestyle — the ring is intact.

PASS: all toys present in the switch ring and each still triggers its function; re-arm behaves; the ring survives a game round-trip; nothing assembles in view. FAIL: a missing/mis-placed toy · a toy that no longer triggers · broken re-arm · an emblem building in view · the ring absent after a game return.

### QA-ECOLOGY-LATTICE-FLORA ⬜ — gyroid branch-pair + Schwarz-P non-Euclidean tile + charge flora shields
Source: PRs #747 (`branch-spindle-gyroid-redesign` — gyroid branch is a mirrored half-branch pair), #744 (`schwarz-p-noneuclidean-tile` — Schwarz P grows on its own non-Euclidean tile, per-element lattice scale, a silent prism-size clamp), #748 (`charge-flora-prism-shield` — Charge armours its mass; both lattice species fitted for the shield). Extends QA-ECOLOGY-GYROID-COLONY; LOCKED ecology surface. Reference: `Docs/ECOSYSTEM.md`.

1. Project compiles; load the gyroid / Schwarz-P lattice flora cell (Cell Selector or Lifeform Matrix) — no missing scripts, no `None` refs.
2. **Gyroid branch:** grown gyroid flora show the mirrored half-branch pair geometry (not a single branch punched through the prism); the branch-pair verifier's intent holds visually.
3. **Schwarz P:** the Schwarz-P species grows on its non-Euclidean tile, sized per-element; no prisms are silently clamped to a wrong size (leaves/tiles look correctly scaled).
4. **Charge shields:** Charge-domain lattice flora carry the prism shield (their mass is armoured) and the shield fits the leaf clearance — no shield clipping through or dwarfing the leaf.
5. Continuity/mass: growth blooms/withers, nothing pops; population behaves per QA-ECOLOGY-GYROID-COLONY.

PASS: compiles; gyroid branches are mirrored half-pairs; Schwarz-P grows correctly-scaled on its tile with no bad clamps; charge flora are shielded with a well-fitted shield; continuity/mass hold. FAIL: missing scripts/None refs · a single-branch gyroid · mis-scaled/clamped Schwarz-P · charge flora with no shield or a clipping/oversized one · anything popping in/out.

### QA-ECOLOGY-QUASICRYSTAL ⬜ — the Lattice cell grows to twelve quasicrystal colonies
Source: PRs #753 (`gyroid-schwarz-flora-cell` — seed the Lattice cell with EIGHT founders, not 240), #754 (`exotic-quasicrystal-flora` — grow the Lattice cell to twelve colonies with the quasicrystal). Extends QA-ECOLOGY-LATTICE-FLORA / QA-ECOLOGY-GYROID-COLONY; a new quasicrystal flora form + the Lattice cell tuning. LOCKED ecology surface. Reference: `Docs/ECOSYSTEM.md`.

1. Project compiles; freestyle → Cell Selector → the Lattice cell — it builds with no missing scripts / `None` refs.
2. Confirm it seeds with a small number of founders (~8, not 240 — a wall of founders on boot means the seed fix didn't take) and grows out to ~twelve colonies over time.
3. Look at the **quasicrystal** flora form — it grows as a coherent quasicrystal lattice (not a broken/degenerate mesh), alongside the gyroid/Schwarz-P species.
4. Continuity/mass: growth blooms/withers, nothing pops; population behaves (no frozen/runaway colony) per QA-ECOLOGY-GYROID-COLONY.

PASS: compiles; the Lattice cell seeds with ~8 founders and grows to ~twelve colonies; the quasicrystal form renders and grows coherently; continuity/mass hold. FAIL: missing scripts/None refs · a 240-founder boot wall · a broken/degenerate quasicrystal · anything popping in/out · a frozen or runaway colony.

### QA-MAELSTROM-POOL ⬜ — the four new modes join the Maelstrom (Tournament) pool
Source: PR #766 (`1f0b235a` feat(tournament)). Maelstrom/Tournament now draws from a pool that includes **Rampage, Peel the Cage (Ribcage), Scarab Scramble, and The Bends** (plus a corrected pool-math fix and a scene-wiring check). `TournamentDataSO` + `TournamentData.asset`. **Depends on** the individual modes working (QA-RAMPAGE-REBUILD, QA-RIBCAGE-MODE, QA-SCARAB-MODE ✓, QA-BENDS-MODE). Reference: `Docs/TournamentSystem/ARCHITECTURE.md`.

1. Launch **Maelstrom** (Tournament). Confirm the mode chains multiple minigames back-to-back and that the pool now includes the four new modes (over a few runs you should see them appear, not only the legacy HexRace/Joust/Crystal Capture).
2. Play a chain through at least one of the new modes (e.g. it rolls Scarab Scramble or The Bends) and confirm the transition in/out of it works — scores fold into the standings, the next mode loads.
3. Confirm the race-to-N standings / summary resolve correctly with the larger pool (the "stale 3-mode pool math" fix from this PR).
4. No missing scripts / scene-wiring errors on any pool member as it loads.

PASS: Maelstrom chains modes including the four new ones; transitions in/out of a new mode work; standings/summary resolve with the corrected pool math; no load errors. FAIL: a pool member that won't load or throws · standings math wrong (a mode not counted, or a wrong race-to-N) · a chain that wedges between modes · the new modes never appearing in the pool.

### QA-ENTER-PLAYMODE-OPTIONS ⬜ — no stale static state with fast play-mode entry
Source: `managed-callbacks-performance` (`b3af31e1` "enable Enter Play Mode Options behind a full static-state audit", `4d97ba09` cut domain-reload cost). The editor now uses **Enter Play Mode Options** (domain/scene reload disabled for faster iteration) — which surfaces any static field that isn't reset between play sessions as a "works first time, breaks second time" bug. The audit is unverified in practice. **Editor-only concern.**

1. Enter Play Mode on Menu_Main, exit, and **re-enter several times in a row** — the menu, autopilot vessel, and freestyle behave identically on the 2nd/3rd entry as the 1st (no doubled objects, no stale singletons, no missing managers).
2. Repeat with a gameplay scene: play a round, exit play mode, re-enter, play again — score/state start clean each time (cross-check QA-STATE-RESET), no leftover objects or events firing twice.
3. Watch the Console across repeated entries for `static`-state-related nulls or "already registered/subscribed" style warnings.

PASS: repeated play-mode entries behave identically to a cold entry; no doubled/stale objects, no double-fired events, clean console. FAIL: any behaviour that only breaks on the 2nd+ entry · doubled objects / stale singletons · events firing multiple times · nulls from un-reset statics.

## Priority 2 — lower risk, cosmetic, or data-gathering

### QA-P2-SERPENT-SKIMMER ⬜ — Serpent's dead skimmer (known, unfixed)
Run Audit Vessel Skimmers and fly the Serpent through cell mass. Expected: it FAILS the audit (inactive `VacuumSkimmer`, no impactor/container) and does not skim. PASS = the failure is exactly as described and nothing else regressed. Report any different symptom. Fix is tracked in `Docs/ElementalAbilitySystem/BACKLOG.md` §10–14.

### QA-P2-BENCH-LEGACY-AB ⬜ — record the legacy-CPU side of the prism A/B
Follow the cherry-pick recipe in `Docs/PRISM_EXPLOSION_BENCHMARK.md` on a `bench-legacy` branch, then Prism Grid Benchmark ▸ Generate Comparison Report. PASS = the report exists and is attached to your results. This item's deliverable is data.

### QA-P2-DEVICE-SOAK ⬜ — per-cell device soak
Soak each freestyle cell plus Scurry/Atlantis on target mobile hardware for ~10 minutes each; record frame time, thermals and any hitching in `Docs/PERFORMANCE_OPTIMIZATION.md`. PASS = numbers recorded for every cell.

### QA-P2-CONIC-VFX-FLASH ⬜ — Dolphin cone spawn flash does not scale
Known cosmetic gap: the prefab's world-space ParticleSystem child ignores the container's Z stretch, so the flash reads at the old length while mesh and damage reach 2400. PASS = confirmed still cosmetic only (damage and mesh reach full length). Needs a VFX tuning pass by someone at the editor.

### QA-P2-DANGLING-CELLDATA ⬜ — the project-wide dangling `cellData` GUID
`Clawfish`, `QuadFish`, `TermiteDrone`, the three `Worm*` prefabs, `oldWallFlora`, both cytoplasm prefabs and three scenes (including `Menu_Main`) still point at a `CellRuntimeDataSO` GUID that does not exist. Spawn each of those fauna and check for a throw from `LifeForm.Start()` / `Flora.Plant()`. PASS = enumerate which ones actually throw — that list scopes the fix branch.

### QA-ANALYTICS-FLIGHT-TIME ⬜ — menu freestyle counts as flight time; starter/selected vessel to HANGAR_DATA
Source: `game-data-json-schema`. `FlightClock`, `VesselUnlockSystem`, `UGSDataService` (Cloud Save +69) and `PlayerDataService` changed so menu-freestyle flight accrues flight time and the starter + `SelectedVessel` land in `HANGAR_DATA`. Data-path change with a Cloud Save save/load surface — mostly data-gathering, low gameplay risk.

1. Menu_Main freestyle: take control and fly for a bit, return to menu.
2. Confirm accrued flight time is recorded (analytics/HANGAR_DATA) rather than only in-game flight counting.
3. Confirm the starter vessel and current `SelectedVessel` are written to `HANGAR_DATA` and survive a Cloud Save round-trip (relaunch).
4. Watch the Console for any Cloud Save serialization error.

PASS: menu-freestyle flight time is recorded; starter + selected vessel persist in `HANGAR_DATA` across a relaunch; no Cloud Save exceptions. FAIL: freestyle flight not counted · vessel fields missing or not persisting · a Cloud Save serialization throw. (Deliverable here is partly data — note the observed values.)

### QA-DOLPHIN-SPEED-TUNE ⬜ — cruise +30%, charged boost +70%, faster fill / slower drain
Source: PR #681 (`dolphin-speed-boost-tuning`). Two authored numbers changed in existing serialized assets (`Dolphin.prefab` `DefaultThrottleScaler 50→68`; `ChargeBoostAction.asset` `maxBoostMultiplier 2→2.259`, `chargeTimeToFull 4→3.636`, `dischargeTimeToEmpty 2→2.5`). Data-only, low import risk, but never flown. Reference: `DOLPHIN_ENERGY_ECONOMY.md` §2.

1. Menu_Main freestyle, Dolphin. Full throttle, no boost → `VesselStatus.Speed` settles at **78** (was 60).
2. Hold drift from an empty meter → the boost ring fills in **~3.6 s** (was 4).
3. Full charged-boost discharge → peak speed reaches **~357** (was 210), draining over ~2.5 s.
4. Sanity: no other vessel's speed/boost changed (the asset is Dolphin-only).

PASS: cruise ~78, fill ~3.6 s, charged peak ~357, drain ~2.5 s; no other vessel affected; the Dolphin's **minimum speed is now 0** (updated per PR #760 — the vessel can come to a full stop). FAIL: values materially off from those targets · another vessel's boost/speed changed · a non-zero minimum speed (the old floor of 10 was removed by #760). (Feel is a judgement call — note whether the new boost peak plays too strong, and whether a full stop feels right.)

### QA-PALETTE-DANGER-GOLD ⬜ — danger tier un-inverted + gold shielded brought into the pastel family
Source: PRs #705 (`danger-prisms-shielded-color`, `ThemeManager`) + #707 (`gold-shielded-prism-contrast`, `OriginalColorSetSO.asset`). Palette-only fixes: the danger tier was un-inverted, gold's unshielded rim corrected, and gold's shielded prism brought into the pastel family. Colour verification. Related: QA-PALETTE-SHIELDED. Reference: `Docs/PALETTE.md`.

1. Pull and let the colour-set asset reimport (a stale Library masks palette changes).
2. Get shielded + unshielded prisms of all three domains on screen (a cell with lifeforms in freestyle, a HexRace/Skim track, or Astro League).
3. Get danger prisms on screen (lay a danger trail) and compare against the shielded/unshielded tiers.
4. Focus on **Gold**: its shielded prism should read in the same pastel family as Ruby/Jade shielded, and its unshielded rim should look right (not inverted/over-bright).

PASS: the danger tier reads correctly (not inverted); gold shielded sits in the pastel family alongside the other domains; gold unshielded rim looks right; no domain blows out under bloom. FAIL: a danger tier that still reads inverted · gold shielded reading flat/muddy or out of family · an over-bright/blown-out gold rim.

### QA-CRYSTAL-EFFECTS 🔴 — elemental crystal capture effect + omni-crystal bloom
> **Last result:** 🔴 FAIL — In HexRace: the crystal model and its breaking/collection effect look normal, but when a new crystal spawns in it POPS into existence instead of blooming in — the omni/crystal bloom-in (PR #724) is not playing on spawn.  _(build bleeding-edge @ 55b310a · Unity 6000.4.11f1.x · Windows, Unity Editor, 2026-08-18, andrew)_

Source: PRs #725 (`elemental-crystal-capture-effect`), #724 (`omni-crystal-bloom`). Two crystal VFX: a capture effect when an elemental crystal is collected, and a bloom on the omni crystal. Cosmetic/visual.

1. Collect an elemental crystal — a capture effect plays (no magenta, no missing VFX).
2. Watch an omni crystal spawn/appear — it blooms in (continuity of existence), not popping.
3. Confirm neither effect throws or leaves artifacts.

PASS: the capture effect plays on collection and the omni crystal blooms in, both clean. FAIL: missing/magenta VFX · an omni crystal that pops · console errors from either effect.

### QA-SPARROW-SPREAD-HAPTICS ⬜ — Sparrow spread + haptics
Source: PR #719 (`sparrow-spread-haptics`). Sparrow shot spread plus haptic feedback. Haptics need a device/gamepad. Related: QA-HAPTICS.

1. Sparrow freestyle: fire and observe the shot spread behaves as designed.
2. On a device/gamepad, confirm the associated haptic fires with the spread and stays within the two-feel haptics policy (no buzz on silenced events).
3. Regression: the two standard feels (skim pulse, prism thud) still behave (cross-check QA-HAPTICS).

PASS: the spread reads as intended; the haptic fires appropriately on a device and respects the haptics policy; the standard feels are intact. FAIL: broken/absent spread · a haptic that fires on silenced events or not at all · a regression to the two standard feels.

### QA-CRASH-DETECTOR-TOOL ⬜ — the editor Crash Detector + Diagnostics lane / Bug Ledger
Source: `tools-docs-crash-detector` (`419590fb` add editor crash detector, `0448192e` Diagnostics lane + shared Bug Ledger, `5b8cddae` ledger archive / findings / severity / doc links). A new editor tool at **FrogletTools ▸ Misc ▸ Crash Detector** with a Diagnostics lane and a shared Bug Ledger. Reader/diagnostics tool.

1. Open **FrogletTools ▸ Misc ▸ Crash Detector** — it opens without throwing.
2. Exercise the Diagnostics lane / Bug Ledger UI (view findings, severity, doc links) — nothing throws; links resolve.
3. If it can surface recent editor crashes/errors, confirm it lists something sensible (or an empty state) rather than erroring.

PASS: the tool opens and its Diagnostics/Ledger UI works without throwing; findings/links render. FAIL: the menu item missing or throwing on open · a Diagnostics/Ledger panel that errors · broken doc links / severity display.

## Not covered by this list

* Automated CI checks (`Tools/CI/validate_project.py`, `check_conditional_compilation.py`, the Thursday build promotion in PR #664) run in GitHub Actions and are verified there. QA does not need to re-run them; if a build branch is red, that is an engineering item.
* Docs-only branches (#653, #623, #666's doc half) — nothing to run.
