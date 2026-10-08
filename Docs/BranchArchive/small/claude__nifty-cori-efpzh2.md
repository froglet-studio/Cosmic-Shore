# Branch archive: `claude/nifty-cori-efpzh2`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-06-12 by Claude
- **Unmerged commits:** 3
- **Forked from:** `5de898fca` (2026-06-12, Merge branch 'claude/cool-bell-m15cu3' into bleeding-edge)
- **Tip:** `1948a1d3f`
- **Files touched (70):**
  - `.gitignore`
  - `Assets/_Prefabs/Environment/Automata.meta`
  - `Assets/_Prefabs/Environment/Automata/ParticleAutomataDemo.prefab`
  - `Assets/_Prefabs/Environment/Automata/ParticleAutomataDemo.prefab.meta`
  - `Assets/_SO_Assets/Automata.meta`
  - `Assets/_SO_Assets/Automata/Mat_ParticleAutomata.mat`
  - `Assets/_SO_Assets/Automata/Mat_ParticleAutomata.mat.meta`
  - `Assets/_SO_Assets/Automata/ParticleAutomataConfig_Jelly.asset`
  - `Assets/_SO_Assets/Automata/ParticleAutomataConfig_Jelly.asset.meta`
  - `Assets/_SO_Assets/Automata/ParticleAutomataConfig_SphereTorus.asset`
  - `Assets/_SO_Assets/Automata/ParticleAutomataConfig_SphereTorus.asset.meta`
  - `Assets/_SO_Assets/Automata/Weights_Jelly.asset`
  - `Assets/_SO_Assets/Automata/Weights_Jelly.asset.meta`
  - `Assets/_SO_Assets/Automata/Weights_PulseSphere.asset`
  - `Assets/_SO_Assets/Automata/Weights_PulseSphere.asset.meta`
  - `Assets/_SO_Assets/Automata/Weights_SphereTorus.asset`
  - `Assets/_SO_Assets/Automata/Weights_SphereTorus.asset.meta`
  - `Assets/_SO_Assets/Automata/jelly.bytes`
  - `Assets/_SO_Assets/Automata/jelly.bytes.meta`
  - `Assets/_SO_Assets/Automata/pulse_sphere.bytes`
  - `Assets/_SO_Assets/Automata/pulse_sphere.bytes.meta`
  - `Assets/_SO_Assets/Automata/sphere_torus.bytes`
  - `Assets/_SO_Assets/Automata/sphere_torus.bytes.meta`
  - `Assets/_SO_Assets/Automata/test_boid_weights.bytes`
  - `Assets/_SO_Assets/Automata/test_flow_weights.bytes`
  - `Assets/_SO_Assets/Automata/test_nca_weights.bytes`
  - `Assets/_Scripts/Controller/Automata.meta`
  - `Assets/_Scripts/Controller/Automata/CellularNCA.meta`
  - `Assets/_Scripts/Controller/Automata/CellularNCA/NCAConfigSO.cs`
  - `Assets/_Scripts/Controller/Automata/CellularNCA/NCAConfigSO.cs.meta`
  - `Assets/_Scripts/Controller/Automata/CellularNCA/NCASimulator.cs`
  - `Assets/_Scripts/Controller/Automata/CellularNCA/NCASimulator.cs.meta`
  - `Assets/_Scripts/Controller/Automata/CellularNCA/NCAStep.compute`
  - `Assets/_Scripts/Controller/Automata/CellularNCA/NCAStep.compute.meta`
  - `Assets/_Scripts/Controller/Automata/CellularNCA/NCAWeightAsset.cs`
  - `Assets/_Scripts/Controller/Automata/CellularNCA/NCAWeightAsset.cs.meta`
  - `Assets/_Scripts/Controller/Automata/NeuralBoids.meta`
  - `Assets/_Scripts/Controller/Automata/NeuralBoids/NeuralBoidConfigSO.cs`
  - `Assets/_Scripts/Controller/Automata/NeuralBoids/NeuralBoidConfigSO.cs.meta`
  - `Assets/_Scripts/Controller/Automata/NeuralBoids/NeuralBoidSimulator.cs`
  - … and 30 more

### `b11b9bf6e` — feat(automata): particle NCA — learned continuous automata converging to animated 3D shapes

_Claude, 2026-06-12 04:16:44 +0000_

```text
The continuous-space generalization of Growing Neural Cellular Automata
(distill.pub/2020/growing-ca): a particle population whose shared, local,
LEARNED rule is trained end-to-end so the population grows from a seed
into a specified animated 3D shape, loops the animation via phase
conditioning, and self-heals after damage — the 3D continuous analog of
the self-healing emoji result.

Particle NCA (centerpiece):
- Tools/NCA_Training/train_particle_nca.py: PyTorch trainer with the
  Distill sample-pool curriculum generalized for animated targets
  (rank-based seed reinjection + damage-the-best), Chamfer loss through
  BPTT rollouts, procedural animated targets (pulse_sphere, sphere_torus
  morph, jelly swimmer, helix) plus .npz frame-bank loading, and a
  self-describing PNCA .bytes export carrying network dims + trained
  simulation constants.
- Tools/NCA_Training/verify_export_parity.py: NumPy transliteration of
  the compute shader algorithm diffed against the PyTorch model —
  verified parity at ~1e-10.
- Assets/_Scripts/Controller/Automata/ParticleNCA/: GPU runtime —
  StepParticles/ScatterDamage/ResetToSeed compute kernels (brute-force
  kNN, fixed-function perception, MLP forward, Wang-hash stochastic fire
  mask), simulator with DamageSphere/ResetToSeed gameplay API, zero-
  readback DrawProcedural billboard renderer with URP shader.

Ported forward from the exploration branch (relocated to current
conventions, namespace CosmicShore.Gameplay):
- CellularNCA/: 2D Growing NCA GPU port (Perceive/Update/Apply kernels)
- NeuralBoids/: learned boid steering (visuals now plain Transforms)
- NeuralFlowFieldSO: neural FlowFieldSO variant
- train_nca.py / train_neural_boids.py / export_weights.py

Docs: Docs/ContinuousAutomata/ARCHITECTURE.md (quadrant map, model spec,
training recipe, runtime integration, fundamentals alignment, roadmap),
Tools/NCA_Training/README.md, CLAUDE.md documentation index row.
```

```text
 .../_Scripts/Controller/Automata/NeuralBoids/NeuralBoidStep.compute   | 152 ++++++++
 .../Controller/Automata/NeuralBoids/NeuralBoidStep.compute.meta       |   8 +
 .../_Scripts/Controller/Automata/NeuralBoids/NeuralBoidWeightAsset.cs |  73 ++++
 .../Controller/Automata/NeuralBoids/NeuralBoidWeightAsset.cs.meta     |  11 +
 Assets/_Scripts/Controller/Automata/ParticleNCA.meta                  |   8 +
 .../Controller/Automata/ParticleNCA/ParticleAutomataConfigSO.cs       |  51 +++
 .../Controller/Automata/ParticleNCA/ParticleAutomataConfigSO.cs.meta  |  11 +
 .../Controller/Automata/ParticleNCA/ParticleAutomataRender.shader     | 102 +++++
 .../Automata/ParticleNCA/ParticleAutomataRender.shader.meta           |   9 +
 .../Controller/Automata/ParticleNCA/ParticleAutomataRenderer.cs       |  73 ++++
 .../Controller/Automata/ParticleNCA/ParticleAutomataRenderer.cs.meta  |  11 +
 .../Controller/Automata/ParticleNCA/ParticleAutomataSimulator.cs      | 228 ++++++++++++
 .../Controller/Automata/ParticleNCA/ParticleAutomataSimulator.cs.meta |  11 +
 .../Controller/Automata/ParticleNCA/ParticleAutomataStep.compute      | 268 +++++++++++++
 .../Controller/Automata/ParticleNCA/ParticleAutomataStep.compute.meta |   8 +
 .../Controller/Automata/ParticleNCA/ParticleAutomataWeightAsset.cs    | 108 ++++++
 .../Automata/ParticleNCA/ParticleAutomataWeightAsset.cs.meta          |  11 +
 Assets/_Scripts/Controller/Environment/FlowField/NeuralFlowFieldSO.cs | 101 +++++
 .../Controller/Environment/FlowField/NeuralFlowFieldSO.cs.meta        |  11 +
 Assets/_Scripts/Tests/EditMode/ParticleAutomataWeightAssetTests.cs    | 109 ++++++
 .../_Scripts/Tests/EditMode/ParticleAutomataWeightAssetTests.cs.meta  |  11 +
 CLAUDE.md                                                             |   1 +
 Docs/ContinuousAutomata/ARCHITECTURE.md                               | 206 ++++++++++
 Tools/NCA_Training/README.md                                          |  62 +++
 Tools/NCA_Training/export_weights.py                                  | 163 ++++++++
 Tools/NCA_Training/train_nca.py                                       | 302 +++++++++++++++
 Tools/NCA_Training/train_neural_boids.py                              | 286 ++++++++++++++
 Tools/NCA_Training/train_particle_nca.py                              | 642 ++++++++++++++++++++++++++++++++
 Tools/NCA_Training/verify_export_parity.py                            | 182 +++++++++
 62 files changed, 4585 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 4318 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Automata/CellularNCA/NCAConfigSO.cs b/Assets/_Scripts/Controller/Automata/CellularNCA/NCAConfigSO.cs
new file mode 100644
index 000000000..f70d963b1
--- /dev/null
+++ b/Assets/_Scripts/Controller/Automata/CellularNCA/NCAConfigSO.cs
@@ -0,0 +1,45 @@
+using UnityEngine;
+
+namespace CosmicShore.Gameplay
+{
+    [CreateAssetMenu(
+        fileName = "NCAConfig",
+        menuName = "ScriptableObjects/Automata/NCA Config")]
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
diff --git a/Assets/_Scripts/Controller/Automata/CellularNCA/NCASimulator.cs b/Assets/_Scripts/Controller/Automata/CellularNCA/NCASimulator.cs
new file mode 100644
index 000000000..438fd9356
--- /dev/null
+++ b/Assets/_Scripts/Controller/Automata/CellularNCA/NCASimulator.cs
@@ -0,0 +1,349 @@
+using UnityEngine;
+using Unity.Profiling;
+
+namespace CosmicShore.Gameplay
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

### `441b89c7b` — feat(automata): trained particle NCA weights + demo assets (jelly, sphere_torus, pulse_sphere)

_Claude, 2026-06-12 07:15:16 +0000_

```text
Three trained models from the new pipeline, all parity-verified against
the compute shader algorithm (~1e-10):
- jelly.bytes — best convergence (from-seed chamfer 0.068): grows a
  recognizable pulsing jellyfish from a seed ball and re-forms after
  scatter damage. Wired into ParticleAutomataConfig_Jelly + demo prefab.
- sphere_torus.bytes / pulse_sphere.bytes — proof-of-pipeline; track
  their targets but still exhibit long-horizon dispersion (need a GPU
  training budget for crisp persistence, see ARCHITECTURE.md roadmap).

Trainer stabilization from the convergence sessions: two-stage lr decay
(0.3x at 50%, 0.1x at 80%) and a pool blow-up guard that reseeds
catastrophically diverged entries instead of letting them dominate
gradients.
```

```text
 Assets/_Prefabs/Environment/Automata/ParticleAutomataDemo.prefab   |   2 +-
 Assets/_SO_Assets/Automata/ParticleAutomataConfig_Jelly.asset      |  22 ++++++++++++++++++++++
 Assets/_SO_Assets/Automata/ParticleAutomataConfig_Jelly.asset.meta |   8 ++++++++
 Assets/_SO_Assets/Automata/Weights_PulseSphere.asset               |  15 +++++++++++++++
 Assets/_SO_Assets/Automata/Weights_PulseSphere.asset.meta          |   8 ++++++++
 Assets/_SO_Assets/Automata/jelly.bytes                             | Bin 0 -> 22364 bytes
 Assets/_SO_Assets/Automata/pulse_sphere.bytes                      | Bin 0 -> 22364 bytes
 Assets/_SO_Assets/Automata/pulse_sphere.bytes.meta                 |   7 +++++++
 Assets/_SO_Assets/Automata/sphere_torus.bytes                      | Bin 0 -> 22364 bytes
 Tools/NCA_Training/train_particle_nca.py                           |  15 +++++++++++++--
 10 files changed, 74 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Tools/NCA_Training/train_particle_nca.py b/Tools/NCA_Training/train_particle_nca.py
index 3c61f4304..24fa2693c 100644
--- a/Tools/NCA_Training/train_particle_nca.py
+++ b/Tools/NCA_Training/train_particle_nca.py
@@ -443,7 +443,7 @@ def render_gif(model, target, args, gif_path):
             ax.set_xlim(-lim, lim); ax.set_ylim(-lim, lim); ax.set_zlim(-lim, lim)
             ax.set_axis_off()
             ax.set_title(title, color='#d0d0e8', fontsize=10)
-            ax.view_init(elev=18, azim=35 + i * 0.6)
+            ax.view_init(elev=26, azim=35 + i * 0.6)
         fig.tight_layout(pad=0.2)
         fig.canvas.draw()
         buf = np.asarray(fig.canvas.buffer_rgba())
@@ -486,9 +486,12 @@ def train(args):
     model.train()
 
     for it in range(args.steps):
-        if it == int(args.steps * 0.6):
+        if it == int(args.steps * 0.5):
             for pg in optimizer.param_groups:
                 pg['lr'] = args.lr * 0.3
+        if it == int(args.steps * 0.8):
+            for pg in optimizer.param_groups:
+                pg['lr'] = args.lr * 0.1
 
         idx = np.random.choice(args.pool, args.batch, replace=False)
         p = pool_p[idx].clone()
@@ -502,6 +505,14 @@ def train(args):
         # state would compound and poison the pool).
         with torch.no_grad():
             pre_loss = chamfer(p, target.at_phase(phase))
+            # Safety valve: a pool entry that has catastrophically diverged
+            # (rule hit an explosive regime) would dominate every gradient it
+            # appears in — reset it to a seed instead of trying to repair it.
+            for b in (pre_loss > 5.0).nonzero().flatten().tolist():
+                bp, bh = make_seed(1, args.particles, args.channels, device)
+                p[b], h[b] = bp[0], bh[0]
+                phase[b] = float(np.random.rand())
+                pre_loss[b] = chamfer(p[b:b + 1], target.at_phase(phase[b:b + 1]))[0]
             order = pre_loss.argsort(descending=True)
         worst = order[0].item()
         sp, sh = make_seed(1, args.particles, args.channels, device)
```

</details>

### `1948a1d3f` — feat(automata): export best-eval checkpoint instead of final training state

_Claude, 2026-06-12 07:42:33 +0000_

```text
CPU-scale particle NCA runs oscillate between basins — the 5500-iter
stabilized jelly run ended at from-seed chamfer 1.17 while holding a
0.12 model at iteration 2000 that the old last-state export threw away.
Track the from-seed eval, snapshot to <output>.best.pt on improvement,
and export whichever of best/final evaluates lower.
```

```text
 Tools/NCA_Training/train_particle_nca.py | 38 ++++++++++++++++++++++++++++++++------
 1 file changed, 32 insertions(+), 6 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Tools/NCA_Training/train_particle_nca.py b/Tools/NCA_Training/train_particle_nca.py
index 24fa2693c..462f5e4e2 100644
--- a/Tools/NCA_Training/train_particle_nca.py
+++ b/Tools/NCA_Training/train_particle_nca.py
@@ -346,6 +346,17 @@ def scatter_damage(p, h, center, radius, scatter_radius=0.6):
     return p, h
 
 
+def evaluate_from_seed(model, target, args, device):
+    """Grow from a fresh seed for 1.5 loops (no grad) and return the
+    Chamfer against the target — the ground-truth convergence metric."""
+    with torch.no_grad():
+        ep, eh = make_seed(1, args.particles, args.channels, device)
+        ephase = torch.zeros(1, device=device)
+        ep, eh, ephase = model.rollout(ep, eh, ephase,
+                                       int(args.steps_per_loop * 1.5))
+        return chamfer(ep, target.at_phase(ephase)).item()
+
+
 # ── Export ──────────────────────────────────────────────────────────────
 
 def export_weights(model, output_path):
@@ -482,6 +493,8 @@ def train(args):
 
     os.makedirs(os.path.dirname(args.output) or '.', exist_ok=True)
     ckpt_path = args.output + '.ckpt.pt'
+    best_path = args.output + '.best.pt'
+    best_eval = float('inf')
     t0 = time.time()
     model.train()
 
@@ -571,19 +584,32 @@ def train(args):
         if it % 500 == 0 and it > 0:
             # Ground-truth convergence signal: grow from a fresh seed for
             # 1.5 loops with no grad and measure chamfer against the target.
-            with torch.no_grad():
-                ep, eh = make_seed(1, args.particles, args.channels, device)
-                ephase = torch.zeros(1, device=device)
-                ep, eh, ephase = model.rollout(ep, eh, ephase,
-                                               int(args.steps_per_loop * 1.5))
-                eval_chamfer = chamfer(ep, target.at_phase(ephase)).item()
+            eval_chamfer = evaluate_from_seed(model, target, args, device)
             print(f"  [eval] from-seed chamfer after 1.5 loops: {eval_chamfer:.5f}",
                   flush=True)
+            if eval_chamfer < best_eval:
+                best_eval = eval_chamfer
+                torch.save({'model': model.state_dict(), 'iter': it,
+                            'args': vars(args), 'eval': eval_chamfer}, best_path)
+                print(f"  [eval] new best — checkpoint saved", flush=True)
 
         if it % 250 == 0 or it == args.steps - 1:
             torch.save({'model': model.state_dict(), 'iter': it,
                         'args': vars(args)}, ckpt_path)
 
+    # Export the model with the best from-seed eval, not necessarily the
+    # final state — CPU-scale runs oscillate between basins, and the last
+    # iteration is regularly worse than the best one seen (observed: a run
+    # ending at 1.17 that held a 0.12 model mid-training).
+    final_eval = evaluate_from_seed(model, target, args, device)
+    print(f"final from-seed eval: {final_eval:.5f}  (best seen: {best_eval:.5f})")
+    if best_eval < final_eval and os.path.exists(best_path):
+        best = torch.load(best_path, map_location=device, weights_only=False)
+        model.load_state_dict(best['model'])
+        print(f"Exporting BEST checkpoint (iter {best['iter']}, eval {best['eval']:.5f})")
+    else:
+        print("Exporting final state")
+
     export_weights(model, args.output)
     torch.save({'model': model.state_dict(), 'iter': args.steps,
                 'args': vars(args)}, ckpt_path)
```

</details>
