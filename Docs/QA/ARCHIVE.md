# QA Archive — items that PASSED

Kept so a re-scan never resurrects a passed item. **Owner: the `/qa-backlog` skill — do not hand-edit.**

Each entry below left the backlog because a submitted RESULTS file marked it PASS. The
`<!-- archived:QA-... -->` markers let the apply engine avoid re-archiving on re-runs.

<!-- archived:QA-CHARGE-CRYSTAL-SHADER -->
_Passed on build bleeding-edge @ eb85e1e · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-17, andrew)._

### QA-CHARGE-CRYSTAL-SHADER ⬜ — dedicated charge-crystal shader (edge-only plasma, blooms in)
Source: PR #710 (`charge-crystal-shader`). New `ChargeCrystal.shader` + `CrystalEdgeArcs` + `CrystalEdgeArcMeshBaker` + a re-imported crystal FBX; a follow-up (`5b5ca689`) makes it honour `_opacity` so the crystal still **blooms in** rather than popping. Shader work → magenta risk on charge crystals.

1. Load a scene with charge crystals (freestyle in a cell with lifeforms, or any mode that drops elemental crystals). If a charge crystal renders **magenta**, the shader failed to compile — stop, FAIL, attach the error.
2. Watch a charge crystal spawn — it should **bloom in** (continuity of existence), not pop.
3. Look at the effect: edge-only plasma discharge / arcs along the crystal edges, reading as a charge crystal (distinct from the other three elements).
4. Skim/collect one — it behaves as a normal charge crystal (energy/level applied), and withers/leaves on death per the usual rules.

PASS: no magenta; charge crystals bloom in; the edge-arc plasma renders and reads as "charge"; collection and death behave normally. FAIL: a magenta/failed shader · a crystal that pops instead of blooming · no edge-arc effect or a broken look · collection/death misbehaving.
<!-- /archived:QA-CHARGE-CRYSTAL-SHADER -->

<!-- archived:QA-UI-QUIT-BUTTON -->
_Passed on build bleeding-edge @ eb85e1e · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-17, andrew)._

### QA-UI-QUIT-BUTTON ⬜ — quit-game control moved into the settings panel
Source: `quit-game-button` (`fabe7074`). The standalone `QuitGameButton.cs` was removed and its behaviour folded into `GameSettingsPanelController`.

1. Open the in-game settings panel: a Quit control is present with no missing-script slot where the old button was.
2. Trigger quit from the settings panel — it does what it should (returns to menu / quits per design) with no exception.
3. Confirm nothing else in the settings panel regressed.

PASS: the quit control is present in the settings panel and works with no missing scripts or exceptions; the rest of the panel is intact. FAIL: a missing button/script · a quit control that throws or does nothing · another settings control broken by the move.
<!-- /archived:QA-UI-QUIT-BUTTON -->

<!-- archived:QA-DOLPHIN-DRIFT-VELOCITY -->
_Passed on build bleeding-edge @ 55b310a · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-18, andrew)._

### QA-DOLPHIN-DRIFT-VELOCITY ⬜ — drift holds velocity magnitude for its whole duration
Source: PR #716 (`dolphin-drift-velocity`). The Dolphin now **holds its velocity magnitude for the duration of a drift** (`VesselTransformer` + `SingleStickVesselTransformer` + `Dolphin.prefab`). Touches the **shared** transformer, so re-check other single-stick vessels don't regress. Reference: `DOLPHIN_ENERGY_ECONOMY.md`, `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

1. Project compiles. Dolphin in freestyle: enter a drift — speed **holds** at its magnitude through the whole drift rather than bleeding off.
2. Release the drift, then re-drift — the speed hold re-arms cleanly each time (watch the re-drift verification row in the checklist).
3. Confirm it doesn't leak into non-drift flight (normal accel/decel unchanged when not drifting).
4. Sanity-check another single-stick vessel (e.g. Serpent/Sparrow that share `SingleStickVesselTransformer`) — its drift/turn behaviour is unchanged.

PASS: compiles; drift holds velocity for its full duration and re-arms on re-drift; non-drift flight unchanged; other single-stick vessels unaffected. FAIL: compile error · speed bleeding off during a drift · the hold not re-arming on re-drift · non-drift flight altered · another single-stick vessel regressing.
<!-- /archived:QA-DOLPHIN-DRIFT-VELOCITY -->

<!-- archived:QA-P2-LIFEFORM-MATRIX-MOONS -->
_Passed on build bleeding-edge @ 55b310a · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-18, andrew)._

### QA-P2-LIFEFORM-MATRIX-MOONS ⬜ — element-crystal "moons" swallowed by the toy body
Suspected pre-existing: the Lifeform Matrix's four crystal moons sit ~2.2 world units out while toys place at `toyBodyRadius = 22`. Look at the bench. PASS = the four moons are visible and distinct. FAIL = they are inside the sphere (then the fix is a placement value, not code).
<!-- /archived:QA-P2-LIFEFORM-MATRIX-MOONS -->

<!-- archived:QA-SCARAB-MODE -->
_Passed on build bleeding-edge @ 0475661 · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-20, andrew)._

### QA-SCARAB-MODE ⬜ — the Scarab vessel + party game
Source: PRs #755 (`scarab-party-game` — smooth nucleus release, bigger skimmer, cap overload, domain blast), #758 (`scarab-wing-prism-dais` — membrane blow-out identity test), #761 (`scarab-squirrel-colliders` — omni-only ball forge, no hull omni effects, per-cell ball overload). A large new vessel/party-game cluster (79 + 17 + 31 files), authored headless.

1. Project compiles; open the Scarab vessel prefab and its mode scene — no `Missing (Mono Script)`; controller + scoring rule wired.
2. Launch the Scarab mode (any player count): it reaches gameplay without an exception; the arena/cell builds.
3. Fly the Scarab: the skimmer, nucleus release, cap/overload, and domain blast behave as designed; the omni-only ball forge works and hull omni effects are absent (per #761).
4. Play a full round to the win condition; scoreboard resolves; return/relaunch clean.
5. Confirm the Squirrel colliders touched in #761 didn't regress the Squirrel (fly it briefly).

PASS: compiles, prefabs/scene intact; the Scarab mode launches, plays to a resolved scoreboard, and returns cleanly; Scarab abilities and the ball forge behave; Squirrel unaffected. FAIL: missing scripts · a scene/vessel that throws on load or launch · abilities/ball forge not working · a round that won't resolve · a Squirrel regression.
<!-- /archived:QA-SCARAB-MODE -->

<!-- archived:QA-DOGFIGHT-MODE -->
_Passed on build bleeding-edge @ ce6a9c7 · Unity 6000.4.11f1.x · Windows, Unity Editor (2026-08-21, andrew)._

### QA-DOGFIGHT-MODE ⬜ — "Dog Fight": the Sparrow-only gun duel in the Boneyard
Source: `dog-fight-game-mode` (feat `3324b951`). A whole new game mode — 96 files, 15,626 insertions, authored headless — with new asset-writing tools (`Tools/Build/author_dogfight_assets.py`, `boneyard_budget.py`), a new scene (EditorBuildSettings changed), a `ScriptableEventCombatHitStats` SOAP type, and `GameDataSO` additions. Reference: `_Scripts/Controller/Arcade/DOGFIGHT.md`.

1. Open the Dog Fight scene: no `Missing (Mono Script)`; the controller and its scoring rule are wired; the arena ("Boneyard") builds.
2. Launch the mode (any player count — AI backfill for solo). It reaches gameplay without an exception.
3. Confirm it is Sparrow-only and gun-combat focused (the Boneyard as the arena, the enemy marker, crystal drops).
4. Play a full round to the win condition and watch the scoreboard resolve (combat-hit / kill scoring).
5. Return to menu and relaunch once — no leaked state, no crash.

PASS: scene opens clean; the mode launches, plays a full round to a resolved scoreboard, and returns/relaunches without error; combat scoring behaves; the Boneyard arena builds as intended. FAIL: missing scripts · a scene/controller that throws on load or launch · the round never resolving · a scoreboard that doesn't tally combat hits/kills · a crash on return/relaunch.
<!-- /archived:QA-DOGFIGHT-MODE -->
