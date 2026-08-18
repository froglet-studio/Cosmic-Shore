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
