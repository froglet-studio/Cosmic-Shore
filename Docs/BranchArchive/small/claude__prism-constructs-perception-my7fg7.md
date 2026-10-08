# Branch archive: `claude/prism-constructs-perception-my7fg7`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-07-09 by Claude
- **Unmerged commits:** 2
- **Forked from:** `f2b8f5aa2` (2026-07-08, Merge pull request #581 from froglet-studio/claude/fly-by-numbers-enhancement-)
- **Tip:** `3fbac7a3c`
- **Files touched (14):**
  - `Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset`
  - `Assets/_SO_Assets/Games/ArcadeGamePrismPerception.asset`
  - `Assets/_SO_Assets/Games/ArcadeGamePrismPerception.asset.meta`
  - `Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset`
  - `Assets/_Scenes/Singleplayer Scenes/MinigamePrismPerception.unity`
  - `Assets/_Scenes/Singleplayer Scenes/MinigamePrismPerception.unity.meta`
  - `Assets/_Scripts/Controller/Environment/Spawning/PrismPerceptionField.cs`
  - `Assets/_Scripts/Controller/Environment/Spawning/PrismPerceptionField.cs.meta`
  - `Assets/_Scripts/Data/Enums/GameModes.cs`
  - `CLAUDE.md`
  - `Docs/PrismPerception/PERCEPTION.md`
  - `Docs/PrismPerception/prism-perception-lab.html`
  - `Docs/SCENES.md`
  - `ProjectSettings/EditorBuildSettings.asset`

### `5589db019` — feat(perception): prism-domain perceptual-trick POCs (interactive bench + in-engine generator)

_Claude, 2026-07-09 19:03:52 +0000_

```text
Explore what the three domains can trick the eye into perceiving in 3D — the
partitive/halftone idea generalised to prism constructs. Key finding: a prism's
HDR _BrightColor emission is a near-CMY triad (Jade azure, Ruby violet, Gold
amber), NOT the team colours, so area-ratio dithering of the domains reaches a
wide interior of blues/purples/ambers no single prism owns.

- Docs/PrismPerception/prism-perception-lab.html — self-contained interactive
  optical bench (Canvas2D additive compositing = the real partitive physics, no
  external libraries): 8 orbitable stations — partitive gamut, value-by-density,
  splat surface, 3D error-diffusion image, view-dependent words, moiré depth,
  temporal PWM, and the anamorphic Sightline reveal.
- Docs/PrismPerception/PERCEPTION.md — theory, measured gamut (~19% of sRGB,
  cool-lavender neutral, saturated green/red unreachable), the 5 encoding axes,
  correctness gotchas (linear-light dither, cubic value ramp, isotropic 3D
  dither, distance-binding), and how it composes with the fundamentals.
- PrismPerceptionField.cs — real in-engine generator through the canonical
  PrismTrailBuilder path (PartitiveVolume + SplatSurface modes), linear-light
  barycentric solve with gamut clamp, LayBatched bloom-in — no new primitive,
  no decay, no timer.
```

```text
 .../_Scripts/Controller/Environment/Spawning/PrismPerceptionField.cs  |  212 +++++++
 Docs/PrismPerception/PERCEPTION.md                                    |  158 +++++
 Docs/PrismPerception/prism-perception-lab.html                        | 1052 +++++++++++++++++++++++++++++++
 3 files changed, 1422 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 382 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/Spawning/PrismPerceptionField.cs b/Assets/_Scripts/Controller/Environment/Spawning/PrismPerceptionField.cs
new file mode 100644
index 000000000..1facc34bd
--- /dev/null
+++ b/Assets/_Scripts/Controller/Environment/Spawning/PrismPerceptionField.cs
@@ -0,0 +1,212 @@
+using System.Collections.Generic;
+using System.Threading;
+using CosmicShore.Data;
+using Cysharp.Threading.Tasks;
+using UnityEngine;
+
+namespace CosmicShore.Gameplay
+{
+    /// <summary>
+    /// PROOF OF CONCEPT — "prism constructs, perception" (branch: prism-constructs-perception).
+    ///
+    /// Builds a static prism construct that tricks the eye into perceiving MORE than three domains,
+    /// the same way a print halftone fools it into a full gamut from three inks. It composes the
+    /// existing fundamentals only — it lays ordinary <see cref="Prism"/>s through the canonical
+    /// <see cref="PrismTrailBuilder"/> path, one <see cref="Domains"/> per prism, and lets the domain's
+    /// own HDR emission + URP Bloom do the additive (partitive) colour mixing. No new colour channel,
+    /// no decay, no timer: the field is a fixed stock of conserved mass that blooms in and then just
+    /// exists (continuity + mass-conservation laws).
+    ///
+    /// The trick that makes it work: a prism's <c>_BrightColor</c> emission is NOT its team colour —
+    /// Jade glows azure-cyan, Ruby violet, Gold amber (a near-CMY triad, values in
+    /// OriginalColorSetSO.InsideBlockColor). So area-ratio dithering of the three domains reaches a
+    /// wide interior of blues, teals, purples and ambers that no single prism owns.
+    ///
+    /// Two modes:
+    ///   • PartitiveVolume — fill a shape with a blue-noise dither of the three domains whose ratio
+    ///     hits a chosen target colour (barycentric weights, solved + gamut-clamped in LINEAR light).
+    ///   • SplatSurface    — a sparse trefoil-knot point cloud that reads as a continuous glowing
+    ///     surface (connect-the-dots → gaussian-splat), colour-swept along its length.
+    ///
+    /// COLLIDER BUDGET: every prism carries a trigger BoxCollider, so this is bounded by the same
+    /// per-cell collider budget as any trail. Keep <see cref="count"/> modest (≤ a few thousand),
+    /// prefer LayBatched (default) so the spawn never spikes a single frame, and treat large fields
+    /// as you would any dense prismscape. Requires a ThemeManager + a URP Bloom volume in the scene
+    /// (ChangeTeam indexes the theme material sets; the additive glow is what makes the mix read).
+    /// </summary>
+    public class PrismPerceptionField : MonoBehaviour
+    {
+        public enum FieldMode { PartitiveVolume, SplatSurface }
+        public enum FieldShape { Sphere, Box, Disc }
+
+        [Header("Prism")]
+        [Tooltip("Assign _Prefabs/Trails/SpawnablePrism.prefab (the environment prism).")]
+        [SerializeField] Prism prism;
+        [Tooltip("Uniform edge length of each prism. Smaller = finer halftone = fuses at closer range.")]
+        [SerializeField] float prismScale = 3f;
+
+        [Header("Construct")]
+        [SerializeField] FieldMode mode = FieldMode.PartitiveVolume;
+        [SerializeField] FieldShape shape = FieldShape.Sphere;
+        [Tooltip("How many prisms to lay. Watch the collider budget — this is a hard gate.")]
+        [SerializeField, Range(64, 4000)] int count = 1400;
+        [Tooltip("Radius / half-extent of the construct, in world units.")]
+        [SerializeField] float radius = 40f;
+        [Tooltip("Deterministic layout seed.")]
+        [SerializeField] int seed = 1;
+
+        [Header("Partitive target")]
+        [Tooltip("The colour the dithered field should FUSE to at distance. Only its chromaticity is " +
+                 "used; it is solved to domain area-ratios and gamut-clamped to the azure/violet/amber wedge.")]
+        [SerializeField] Color targetColor = new Color(0.33f, 0.40f, 0.96f, 1f); // periwinkle: none of the three
+
+        [Header("Build")]
+        [SerializeField] bool buildOnStart = true;
+        [Tooltip("Prisms laid per frame (batched) so the spawn never spikes a frame.")]
+        [SerializeField] int perFrame = 60;
+
+        // Real prism emissive primaries (OriginalColorSetSO.InsideBlockColor, linear HDR).
+        // These, not the team colours, are what the blocks GLOW — a near-CMY triad.
+        static readonly Vector3 P_JADE = new Vector3(0f, 0.588f, 1.135f); // azure-cyan  ≈ C
+        static readonly Vector3 P_RUBY = new Vector3(0.549f, 0f, 1.498f); // violet      ≈ M
+        static readonly Vector3 P_GOLD = new Vector3(1.498f, 0.668f, 0.089f); // amber    ≈ Y
+
+        CancellationTokenSource _cts;
+
+        void Start()
+        {
+            if (buildOnStart) Build();
+        }
+
+        void OnDestroy()
+        {
+            _cts?.Cancel();
+            _cts?.Dispose();
+        }
+
+        [ContextMenu("Build")]
+        public void Build()
+        {
+            if (prism == null)
+            {
+                Debug.LogError($"{nameof(PrismPerceptionField)}: assign the SpawnablePrism prefab.", this);
+                return;
+            }
+
+            _cts?.Cancel();
+            _cts = new CancellationTokenSource();
+
+            var elems = mode == FieldMode.SplatSurface
+                ? BuildSplatSurface()
+                : BuildPartitiveVolume();
+
+            // Lay through the canonical builder: Instantiate → ChangeTeam → pose → Initialize → bloom-in.
+            var trail = new Trail();
+            PrismTrailBuilder
+                .LayBatched(prism, elems, transform, trail, name, Mathf.Max(1, perFrame), _cts.Token)
+                .Forget();
+        }
+
+        // ── PartitiveVolume ────────────────────────────────────────────────────
+        // Fill a shape with domains dithered to the target colour's barycentric weights.
+        List<PrismLay> BuildPartitiveVolume()
+        {
+            var rng = new System.Random(seed);
+            Vector3 w = SolveDomainWeights(SrgbToLinear(targetColor)); // area fractions over {J,R,G}, gamut-clamped, sum=1
+            float wJ = w.x, wJR = w.x + w.y;
+
+            var quat = Quaternion.identity;
+            var s = Vector3.one * prismScale;
+            var list = new List<PrismLay>(count);
+            for (int i = 0; i < count; i++)
+            {
+                Vector3 pos = SamplePoint(shape, radius, rng);
+                // stochastic (blue-noise-ish) assignment to the solved ratio
+                double u = rng.NextDouble();
+                Domains dom = u < wJ ? Domains.Jade : u < wJR ? Domains.Ruby : Domains.Gold;
+                // face the prism outward-ish so its flat card catches the eye from many angles
+                Quaternion rot = pos.sqrMagnitude > 0.001f
+                    ? Quaternion.LookRotation(pos.normalized, Vector3.up) : quat;
+                list.Add(new PrismLay(new SpawnPoint(pos, rot, s), dom));
+            }
+            return list;
+        }
+
+        // ── SplatSurface ───────────────────────────────────────────────────────
+        // A trefoil knot as a sparse cloud that fuses into a continuous glowing surface,
+        // colour-swept Jade → Gold → Ruby along the parameter (connect-the-dots → splat).
+        List<PrismLay> BuildSplatSurface()
+        {
+            var rng = new System.Random(seed);
+            var s = Vector3.one * prismScale;
+            var list = new List<PrismLay>(count);
+            float tube = radius * 0.13f;
+            for (int i = 0; i < count; i++)
```

</details>

### `3fbac7a3c` — feat(perception): flyable MinigamePrismPerception arcade scene (Squirrel, mode 38)

_Claude, 2026-07-09 20:13:42 +0000_

```text
Test scene for the prism-perception constructs, launchable from the Arcade.

- MinigamePrismPerception.unity: clone of MinigameWildlifeBlitz (same donor the
  benchmark tool uses — preserves ContainerScope, Cell, GameCanvas, spawner,
  Bloom). Controller swapped to SandboxBenchmarkController (endless free
  flight, auto-start, HasEndGame=false); TurnMonitorController.monitors emptied
  so nothing ends the turn. Four PrismPerceptionField constructs placed around
  the spawn corridor: periwinkle gamut disc (the thesis swatch), cool-lavender
  "almost white" sphere, trefoil splat surface, plum box field.
- GameModes.PrismPerception = 38; ArcadeGamePrismPerception.asset (single
  player, Squirrel-only Vessels list, intensity 1-4); appended to
  OrganicRematchGames.asset (the live DI arcade list + GameCard lookup);
  added to ProgressionConfig alwaysUnlockedModes/fullIntensityModes so the
  card is unlocked; scene added to EditorBuildSettings.
- Collider budget: constructs total 3,100 trigger BoxColliders, laid batched
  60/frame via PrismTrailBuilder.LayBatched (bloom-in, no spawn spike),
  inspector-tunable per construct.
- Docs: SCENES.md + CLAUDE.md mode inventory; PERCEPTION.md gains launch +
  in-editor verification steps (enter via Bootstrap flow; direct scene play
  unsupported — ChangeTeam needs the persistent ThemeManager).
```

```text
 Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset               |    2 +
 Assets/_SO_Assets/Games/ArcadeGamePrismPerception.asset               |   35 +
 Assets/_SO_Assets/Games/ArcadeGamePrismPerception.asset.meta          |    8 +
 Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset           |    1 +
 Assets/_Scenes/Singleplayer Scenes/MinigamePrismPerception.unity      | 2305 +++++++++++++++++++++++++++++++
 Assets/_Scenes/Singleplayer Scenes/MinigamePrismPerception.unity.meta |    7 +
 .../Controller/Environment/Spawning/PrismPerceptionField.cs.meta      |   11 +
 Assets/_Scripts/Data/Enums/GameModes.cs                               |    4 +
 CLAUDE.md                                                             |    2 +-
 Docs/PrismPerception/PERCEPTION.md                                    |   44 +
 Docs/SCENES.md                                                        |    1 +
 ProjectSettings/EditorBuildSettings.asset                             |    3 +
 12 files changed, 2422 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Data/Enums/GameModes.cs b/Assets/_Scripts/Data/Enums/GameModes.cs
index 7721041e5..bfb95c04e 100644
--- a/Assets/_Scripts/Data/Enums/GameModes.cs
+++ b/Assets/_Scripts/Data/Enums/GameModes.cs
@@ -48,5 +48,9 @@ namespace CosmicShore.Data
         // AstroLeague (37): hypersea soccer domain minigame. See
         // _Scripts/Controller/Arcade/ASTROLEAGUE.md.
         AstroLeague = 37,
+        // PrismPerception (38): single-player free-flight gallery of prism
+        // perception constructs (domain-halftone colour fields, splat surfaces).
+        // See Docs/PrismPerception/PERCEPTION.md.
+        PrismPerception = 38,
     }
 }
\ No newline at end of file
diff --git a/CLAUDE.md b/CLAUDE.md
index c25b1578c..79a534fca 100644
--- a/CLAUDE.md
+++ b/CLAUDE.md
@@ -259,7 +259,7 @@ All in `Assets/_Scenes/Multiplayer Scenes/`.
 
 #### GameModes Enum (`Assets/_Scripts/Data/Enums/GameModes.cs`)
 
-37 game modes with explicit numeric IDs (highest is `AstroLeague(37)`; IDs 7 and 31 are skipped). Single-player: `Elimination(1)` through `ProtectMission(27)`. Multiplayer: `MultiplayerFreestyle(28)`, `MultiplayerCellularDuel(29)`, `Multiplayer2v2CoOpVsAI(30)`, `MultiplayerWildlifeBlitzGame(32)`, `HexRace(33)`, `MultiplayerJoust(34)`, `MultiplayerCrystalCapture(35)`, `AstroLeague(37)`. Meta-mode: `Tournament(36)` — the session-level meta that chains HexRace → Joust → Crystal Capture back-to-back via sequential `Single` loads (see `Docs/TournamentSystem/ARCHITECTURE.md`). `AstroLeague(37)` is hypersea soccer (a standalone domain minigame, see `_Scripts/Controller/Arcade/ASTROLEAGUE.md`). Meta sentinel: `Random(0)`. Note: IDs 7 and 31 are skipped — 7 was the retired standalone arcade Freestyle game (freestyle now lives in Menu_Main as the lava lamp; see "Lava-Lamp Mode"), 31 was never assigned. Do not reuse either ID.
+38 game modes with explicit numeric IDs (highest is `PrismPerception(38)`; IDs 7 and 31 are skipped). Single-player: `Elimination(1)` through `ProtectMission(27)`, plus `PrismPerception(38)` — endless free-flight gallery of prism perception constructs (see `Docs/PrismPerception/PERCEPTION.md`). Multiplayer: `MultiplayerFreestyle(28)`, `MultiplayerCellularDuel(29)`, `Multiplayer2v2CoOpVsAI(30)`, `MultiplayerWildlifeBlitzGame(32)`, `HexRace(33)`, `MultiplayerJoust(34)`, `MultiplayerCrystalCapture(35)`, `AstroLeague(37)`. Meta-mode: `Tournament(36)` — the session-level meta that chains HexRace → Joust → Crystal Capture back-to-back via sequential `Single` loads (see `Docs/TournamentSystem/ARCHITECTURE.md`). `AstroLeague(37)` is hypersea soccer (a standalone domain minigame, see `_Scripts/Controller/Arcade/ASTROLEAGUE.md`). Meta sentinel: `Random(0)`. Note: IDs 7 and 31 are skipped — 7 was the retired standalone arcade Freestyle game (freestyle now lives in Menu_Main as the lava lamp; see "Lava-Lamp Mode"), 31 was never assigned. Do not reuse either ID.
 
 Many single-player modes (1-6, 9-25, 27) reference scenes that no longer exist on disk — their `SO_ArcadeGame` assets still exist and appear in the Arcade UI, but launching them would fail.
 
diff --git a/Docs/PrismPerception/PERCEPTION.md b/Docs/PrismPerception/PERCEPTION.md
index 6e0732513..853de30f0 100644
--- a/Docs/PrismPerception/PERCEPTION.md
+++ b/Docs/PrismPerception/PERCEPTION.md
@@ -142,6 +142,50 @@ and treat a dense field as you would any dense prismscape.
 
 ---
 
+## In-game test scene: `MinigamePrismPerception` (arcade, Squirrel)
+
+**Launch:** Arcade → **Prism Perception** card → Squirrel is the only vessel → Start. The mode is
+`GameModes.PrismPerception (38)`, single-player, always unlocked (added to
+`ProgressionConfig.alwaysUnlockedModes` + `fullIntensityModes`).
+
+The scene is a clone of `MinigameWildlifeBlitz.unity` (the same donor the benchmark tool clones —
+it preserves the hard-to-replicate wiring: ContainerScope, Cell, GameCanvas, spawner, PostProcessing
+with Bloom), with these deltas:
+
+- The `Game` controller is swapped to **`SandboxBenchmarkController`** — endless free flight,
+  auto-start after ~1s, `HasEndGame=false`. The `TurnMonitorController.monitors` list is emptied
+  (and the dormant time monitor's duration raised), so nothing ever ends the turn. Exit via the
+  pause menu → main menu.
+- Four **`PrismPerceptionField`** constructs are placed around the spawn corridor (player spawns at
+  `(0,0,−240)` flying toward the cell at the origin):
+
+| Construct | Mode / shape | What it demonstrates | Where |
+|---|---|---|---|
+| `PrismConstruct_PeriwinkleGamutDisc` | PartitiveVolume / disc, 900 prisms | The thesis swatch: Jade azure + Ruby violet dither fusing to **periwinkle blue** — a hue no domain owns | left of the corridor, facing spawn |
+| `PrismConstruct_LavenderSphere` | PartitiveVolume / sphere, 800 | "Almost white": the equal-mix **cool lavender** (no true neutral exists) | right of the corridor |
+| `PrismConstruct_TrefoilSplat` | SplatSurface, 900 | Connect-the-dots → continuous glowing surface, colour-swept along the knot | above the corridor |
+| `PrismConstruct_PlumBox` | PartitiveVolume / box, 500 | Plum (violet+amber) volumetric halftone | behind/right of spawn — turn around |
+
+**Collider budget statement (hard gate):** the four constructs total **3,100 prisms** → 3,100 trigger
+`BoxCollider`s laid batched at 60/frame (~1s bloom-in wave), on top of the donor scene's normal cell
+ecology. That is within what the WildlifeBlitz scene already sustains at high intensity, and this is
+a *test* scene, not a shipping mode; counts are inspector-tunable per construct (`count`).
+
+**In-editor verification:**
+1. Open the project — Unity imports `PrismPerceptionField.cs` (+ the new scene/asset; no console errors).
+2. Play from **Bootstrap** → menu → Arcade → the **Prism Perception** card appears (unlocked) → Start.
+3. Expect: countdown auto-starts ≈1s after load; the Squirrel is flyable; the four constructs bloom
+   in over ~1s; the disc reads periwinkle from the corridor and decomposes into azure/violet dots up
+   close (fusion is distance-bound); the game never ends on its own.
+4. Known ecology interaction: the donor Cell's fauna treat construct prisms as ordinary mass —
+   opposing-domain fauna may graze constructs near the cell over long sessions. That is the HyperSea
+   being the HyperSea (universality — no carve-outs), and it's why the gallery sits away from the
+   cell centre. If it bothers testing, reduce the cell's spawn profile in the scene, or move the
+   constructs further out.
+5. Direct scene play (open `MinigamePrismPerception.unity` and press Play without Bootstrap) is NOT
+   supported — `Prism.ChangeTeam` needs the persistent `ThemeManager` from Bootstrap. Always enter
+   through the normal flow.
+
 ## How this composes with the fundamentals
 
 Per `CLAUDE.md` — *favour emergent systems, don't cheat emergence*:
diff --git a/Docs/SCENES.md b/Docs/SCENES.md
index 0cd6974d3..b4b043275 100644
--- a/Docs/SCENES.md
+++ b/Docs/SCENES.md
@@ -39,6 +39,7 @@ game scene and still exists.
 |---|---|---|---|
 | **MinigameCellularDuel** | `_Scenes/Singleplayer Scenes/` | `CellularDuel (8)` | `SinglePlayerCellularDuelController` |
 | **MinigameWildlifeBlitz** | `_Scenes/Singleplayer Scenes/` | `WildlifeBlitz (26)` | `SinglePlayerWildlifeBlitzController` |
+| **MinigamePrismPerception** | `_Scenes/Singleplayer Scenes/` | `PrismPerception (38)` | `SandboxBenchmarkController` (endless free flight; no turn monitor) |
 
 ### Multiplayer Game Scenes
 
```

</details>
