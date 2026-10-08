# Branch archive: `claude/explore-cellular-automata-9oWWg`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-06-12 by Claude
- **Unmerged commits:** 1
- **Forked from:** `8a3d6d0d3` (2026-04-16, Merge pull request #483 from froglet-studio/claude/update-skimmer-prism-effect)
- **Tip:** `a1a6c85eb`
- **Files touched (15):**
  - `Assets/_SO_Assets/CellularAutomata/test_boid_weights.bytes`
  - `Assets/_SO_Assets/CellularAutomata/test_flow_weights.bytes`
  - `Assets/_SO_Assets/CellularAutomata/test_nca_weights.bytes`
  - `Assets/_Scripts/Game/CellularAutomata/NCAConfigSO.cs`
  - `Assets/_Scripts/Game/CellularAutomata/NCASimulator.cs`
  - `Assets/_Scripts/Game/CellularAutomata/NCAStep.compute`
  - `Assets/_Scripts/Game/CellularAutomata/NCAWeightAsset.cs`
  - `Assets/_Scripts/Game/CellularAutomata/NeuralBoidConfigSO.cs`
  - `Assets/_Scripts/Game/CellularAutomata/NeuralBoidSimulator.cs`
  - `Assets/_Scripts/Game/CellularAutomata/NeuralBoidStep.compute`
  - `Assets/_Scripts/Game/CellularAutomata/NeuralBoidWeightAsset.cs`
  - `Assets/_Scripts/Game/Environment/FlowField/NeuralFlowFieldSO.cs`
  - `Tools/NCA_Training/export_weights.py`
  - `Tools/NCA_Training/train_nca.py`
  - `Tools/NCA_Training/train_neural_boids.py`

### `a1a6c85eb` — Add learned cellular automata and neural boid systems

_Claude, 2026-06-12 01:38:55 +0000_

```text
Implements the core infrastructure for the Continuous Automata Exploration
initiative across both the "learned cellular" and "learned continuous" quadrants:

Learned Cellular (Growing NCA):
- NCAStep.compute: GPU compute shader with Perceive/Update/Apply kernels
- NCASimulator.cs: MonoBehaviour driving double-buffered NCA simulation
- NCAConfigSO.cs: ScriptableObject for grid size, fire rate, step count
- NCAWeightAsset.cs: Loads trained neural net weights from .bytes files

Learned Continuous (Neural Boids):
- NeuralBoidStep.compute: Replaces hardcoded separation/cohesion/alignment
  with a learned NN forward pass (perception -> dense(64, ReLU) -> dense(3))
- NeuralBoidSimulator.cs: GPU-accelerated neural boid simulation
- NeuralBoidConfigSO.cs / NeuralBoidWeightAsset.cs: Config and weight loading

Learned Flow Fields:
- NeuralFlowFieldSO.cs: FlowFieldSO subclass that evaluates a neural net
  instead of hand-designed Gaussian/Elliptical/Polar equations

Unity <> ML Pipeline:
- Tools/NCA_Training/export_weights.py: Export TF/PyTorch weights to .bytes
- Tools/NCA_Training/train_nca.py: Full NCA training loop (supports animated targets)
- Tools/NCA_Training/train_neural_boids.py: Train boid rules from trajectory data
  (includes synthetic murmuration generator for testing)
- Test weight .bytes files for pipeline validation
```

```text
 Assets/_SO_Assets/CellularAutomata/test_boid_weights.bytes      | Bin 0 -> 4364 bytes
 Assets/_SO_Assets/CellularAutomata/test_flow_weights.bytes      | Bin 0 -> 908 bytes
 Assets/_SO_Assets/CellularAutomata/test_nca_weights.bytes       | Bin 0 -> 33344 bytes
 Assets/_Scripts/Game/CellularAutomata/NCAConfigSO.cs            |  45 +++++
 Assets/_Scripts/Game/CellularAutomata/NCASimulator.cs           | 349 ++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Game/CellularAutomata/NCAStep.compute           | 328 +++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Game/CellularAutomata/NCAWeightAsset.cs         |  78 +++++++++
 Assets/_Scripts/Game/CellularAutomata/NeuralBoidConfigSO.cs     |  48 ++++++
 Assets/_Scripts/Game/CellularAutomata/NeuralBoidSimulator.cs    | 205 ++++++++++++++++++++++
 Assets/_Scripts/Game/CellularAutomata/NeuralBoidStep.compute    | 152 +++++++++++++++++
 Assets/_Scripts/Game/CellularAutomata/NeuralBoidWeightAsset.cs  |  73 ++++++++
 Assets/_Scripts/Game/Environment/FlowField/NeuralFlowFieldSO.cs |  94 ++++++++++
 Tools/NCA_Training/export_weights.py                            | 163 ++++++++++++++++++
 Tools/NCA_Training/train_nca.py                                 | 302 +++++++++++++++++++++++++++++++++
 Tools/NCA_Training/train_neural_boids.py                        | 286 +++++++++++++++++++++++++++++++
 15 files changed, 2123 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 2195 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/CellularAutomata/NCAConfigSO.cs b/Assets/_Scripts/Game/CellularAutomata/NCAConfigSO.cs
new file mode 100644
index 000000000..83f423a9e
--- /dev/null
+++ b/Assets/_Scripts/Game/CellularAutomata/NCAConfigSO.cs
@@ -0,0 +1,45 @@
+using UnityEngine;
+
+namespace CosmicShore.Game.CellularAutomata
+{
+    [CreateAssetMenu(
+        fileName = "NCAConfig",
+        menuName = "ScriptableObjects/CellularAutomata/NCA Config")]
+    public class NCAConfigSO : ScriptableObject
+    {
+        [Header("Grid")]
+        [SerializeField] int gridWidth = 72;
+        [SerializeField] int gridHeight = 72;
+
+        [Header("Simulation")]
+        [Tooltip("Fraction of cells that update each step. 0.5 matches the paper default.")]
+        [SerializeField, Range(0.01f, 1f)] float fireRate = 0.5f;
+
+        [Tooltip("Number of NCA steps to run per Unity frame.")]
+        [SerializeField, Range(1, 16)] int stepsPerFrame = 1;
+
+        [Tooltip("Multiplier on the state delta each step.")]
+        [SerializeField] float stepSize = 1f;
+
+        [Tooltip("Rotation angle (degrees) applied to Sobel perception kernels for angle invariance.")]
+        [SerializeField] float perceptionAngleDegrees = 0f;
+
+        [Header("Assets")]
+        [SerializeField] NCAWeightAsset weights;
+        [SerializeField] ComputeShader computeShader;
+
+        [Header("Animated Targets (optional)")]
+        [Tooltip("For time-varying NCA: sequence of target frames the simulation can track.")]
+        [SerializeField] Texture2D[] targetFrames;
+
+        public int GridWidth => gridWidth;
+        public int GridHeight => gridHeight;
+        public float FireRate => fireRate;
+        public int StepsPerFrame => stepsPerFrame;
+        public float StepSize => stepSize;
+        public float PerceptionAngleRadians => perceptionAngleDegrees * Mathf.Deg2Rad;
+        public NCAWeightAsset Weights => weights;
+        public ComputeShader ComputeShader => computeShader;
+        public Texture2D[] TargetFrames => targetFrames;
+    }
+}
diff --git a/Assets/_Scripts/Game/CellularAutomata/NCASimulator.cs b/Assets/_Scripts/Game/CellularAutomata/NCASimulator.cs
new file mode 100644
index 000000000..c187a1172
--- /dev/null
+++ b/Assets/_Scripts/Game/CellularAutomata/NCASimulator.cs
@@ -0,0 +1,349 @@
+using UnityEngine;
+using Unity.Profiling;
+
+namespace CosmicShore.Game.CellularAutomata
+{
+    public class NCASimulator : MonoBehaviour
+    {
+        [Header("Configuration")]
+        [SerializeField] NCAConfigSO config;
+
+        [Header("Debug Visualization")]
+        [Tooltip("Assign a renderer to display the NCA RGBA output in the scene.")]
+        [SerializeField] Renderer debugRenderer;
+
+        static readonly ProfilerMarker s_StepMarker = new("NCASimulator.Step");
+        static readonly ProfilerMarker s_PerceiveMarker = new("NCASimulator.Perceive");
+        static readonly ProfilerMarker s_UpdateMarker = new("NCASimulator.Update");
+        static readonly ProfilerMarker s_ApplyMarker = new("NCASimulator.Apply");
+
+        // Double-buffered state: 4 RenderTextures × 2 sets (read/write)
+        RenderTexture[] _stateA = new RenderTexture[4];
+        RenderTexture[] _stateB = new RenderTexture[4];
+        bool _readFromA = true;
+
+        // Perception (48 channels = 12 RGBA textures)
+        RenderTexture[] _perception = new RenderTexture[12];
+
+        // Delta output (16 channels = 4 RGBA textures)
+        RenderTexture[] _delta = new RenderTexture[4];
+
+        // Weight buffers
+        ComputeBuffer _weights1Buffer;
+        ComputeBuffer _biases1Buffer;
+        ComputeBuffer _weights2Buffer;
+        ComputeBuffer _biases2Buffer;
+
+        // Random values buffer for stochastic masking
+        ComputeBuffer _randomBuffer;
+        float[] _randomValues;
+
+        // Kernel IDs
+        int _perceiveKernel;
+        int _updateKernel;
+        int _applyKernel;
+
+        // Output texture for external sampling
+        public RenderTexture OutputRGBA => ReadState[0];
+
+        RenderTexture[] ReadState => _readFromA ? _stateA : _stateB;
+        RenderTexture[] WriteState => _readFromA ? _stateB : _stateA;
+
+        bool _initialized;
+
+        void OnEnable()
+        {
+            if (config == null)
+            {
+                Debug.LogError($"[{nameof(NCASimulator)}] No config assigned on {name}.");
+                return;
+            }
+            Initialize();
+        }
+
+        void OnDisable()
+        {
+            Cleanup();
+        }
+
+        void Update()
+        {
+            if (!_initialized) return;
+
+            for (int i = 0; i < config.StepsPerFrame; i++)
+            {
+                Step();
+            }
+
+            if (debugRenderer != null)
+            {
+                var mpb = new MaterialPropertyBlock();
+                debugRenderer.GetPropertyBlock(mpb);
+                mpb.SetTexture("_MainTex", OutputRGBA);
+                debugRenderer.SetPropertyBlock(mpb);
+            }
+        }
+
+        void Initialize()
+        {
+            if (config.ComputeShader == null || config.Weights == null)
+            {
+                Debug.LogError($"[{nameof(NCASimulator)}] Config missing compute shader or weights on {name}.");
+                return;
+            }
```

</details>
