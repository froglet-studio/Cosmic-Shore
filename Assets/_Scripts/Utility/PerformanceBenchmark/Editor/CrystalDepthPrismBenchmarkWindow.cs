#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using System.Text;
using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Utility.PerformanceBenchmark.Editor
{
    /// <summary>
    /// The frame cost of the depth-prism crystals (Omni Shepard Lab rounds 8-10): N omni crystals
    /// (<c>Crystal.prefab</c>) and N Mass crystals (<c>CrystalMass.prefab</c>) in a ring, the camera orbiting
    /// outside it and looking in, so most crystals are drawn and every crystal's ratchet sees its depths change.
    /// Each copy is stripped to its visual before it wakes (the crystal's gameplay behaviours, colliders and
    /// network object go; <see cref="CrystalDepthPrisms"/>, <see cref="CrystalSpaceSquares"/>,
    /// <see cref="CrystalFlipWave"/>, <see cref="CrystalEdgeArcs"/> and <see cref="CrystalAccentTint"/> stay).
    ///
    /// It reads per-frame samples from ProfilerRecorders and reports the median and 95th percentile of each:
    /// main thread, the crystals' own markers (summed over every crystal in a frame), skinned-mesh update, and
    /// the render counters (draw calls, SetPass calls, batches). It also lists every batch / SRP counter this Unity
    /// exposes, so a counter it does not know by name still shows up. A stat this Unity does not expose reads
    /// "not found"; one that exists but never fired reads "no samples".
    ///
    /// The ratchet reads <c>CameraManager</c>'s active controller camera, falling back to <c>Camera.main</c>.
    /// This tool moves <c>Camera.main</c>, so run it in a quiet scene with no active camera controller (the window
    /// warns otherwise), where the two are the same camera.
    ///
    /// READER tool: it spawns into the open play-mode scene and destroys what it spawned, moves the camera and
    /// puts it back, and writes no asset, so it carries no ledger or ship panel.
    /// Instructions: Docs/Studios/OMNI_SHEPARD_LAB.md, "Performance".
    /// </summary>
    public class CrystalDepthPrismBenchmarkWindow : EditorWindow
    {
        const string OmniPrefabPath = "Assets/_Prefabs/Environment/Crystal.prefab";
        const string MassPrefabPath = "Assets/_Prefabs/Environment/CrystalMass.prefab";
        const float Spacing = 25f;               // centre to centre; a crystal is 15 u across
        const float OrbitDegreesPerSecond = 20f;

        enum Kinds { Both = 0, OmniOnly = 1, MassOnly = 2 }

        static readonly (string label, string stat)[] Stats =
        {
            ("Main thread", "Main Thread"),
            ("Depth prisms", "CrystalDepthPrisms.LateUpdate"),
            ("Space squares", "CrystalSpaceSquares.LateUpdate"),
            ("Flip wave", "CrystalFlipWave.LateUpdate"),
            ("Skinned meshes", "PostLateUpdate.UpdateAllSkinnedMeshes"),
            ("Draw calls", "Draw Calls Count"),
            ("SetPass calls", "SetPass Calls Count"),
            ("Batches", "Batches Count"),
            ("Shadow casters", "Shadow Casters Count"),
            ("Triangles", "Triangles Count"),
        };

        static readonly System.Type[] VisualBehaviours =
        {
            typeof(CrystalDepthPrisms), typeof(CrystalSpaceSquares), typeof(CrystalFlipWave), typeof(CrystalEdgeArcs), typeof(CrystalAccentTint),
        };

        sealed class Result
        {
            public int Count;
            public Kinds Kinds;
            public int Frames;
            public readonly Dictionary<string, string> Stats = new();
        }

        [SerializeField] Kinds kinds = Kinds.Both;
        [SerializeField] int warmupFrames = 120;
        [SerializeField] int sampleFrames = 600;

        readonly List<Result> results = new();
        readonly Queue<int> pending = new();
        GameObject root;
        int running;
        int phaseEndsAt;
        bool sampling;
        ProfilerRecorder[] recorders;
        Camera camera;
        Pose cameraHome;
        Vector3 ringCentre;
        float ringRadius;
        float orbitStart;
        Vector2 scroll;
        string batchCounters;

        [MenuItem("FrogletTools/Performance/Benchmarks/Crystal Depth Prism", false, 210)]
        [FrogletTool(FrogletToolCategory.Performance, Importance = 3,
            Description = "Frame cost of N omni + N Mass depth-prism crystals (N = 10/50/200): crystal markers, draw calls, SetPass calls, batches.")]
        public static void Open()
        {
            var window = GetWindow<CrystalDepthPrismBenchmarkWindow>("Depth Prism Benchmark");
            window.minSize = new Vector2(600, 420);
            window.Show();
        }

        void OnDisable()
        {
            Teardown();
            EditorApplication.update -= Tick;
        }

        void OnGUI()
        {
            FrogletEditorPalette.Banner("Crystal Depth Prism Benchmark",
                "N omni + N Mass crystals in a ring, the camera orbiting. Enter Play Mode in a quiet scene first.",
                FrogletEditorPalette.ColorFor(FrogletToolCategory.Performance));
            GUILayout.Space(6);

            bool busy = root != null || pending.Count > 0;
            using (new EditorGUI.DisabledScope(busy))
            {
                kinds = (Kinds)EditorGUILayout.EnumPopup(new GUIContent("Crystals", "Both: N omni AND N Mass. Or one kind alone, to attribute a cost."), kinds);
                warmupFrames = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("Warm-up frames", "Frames to let spawning (and the first-frame mesh bakes) settle before sampling."), warmupFrames), 10, 2000);
                sampleFrames = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("Sample frames", "Frames sampled per run."), sampleFrames), 30, 5000);
            }

            GUILayout.Space(6);
            bool playing = EditorApplication.isPlaying;
            using (new EditorGUILayout.HorizontalScope())
            {
                var accent = FrogletEditorPalette.ColorFor(FrogletToolCategory.Performance);
                if (FrogletEditorPalette.ColorButton("Run N = 10 / 50 / 200", accent, 170f, 26f, "Three runs, same settings.", playing && !busy))
                    Queue(10, 50, 200);
                if (FrogletEditorPalette.ColorButton("N = 50 only", accent, 100f, 26f, null, playing && !busy, true))
                    Queue(50);
                if (FrogletEditorPalette.ColorButton("Copy report", FrogletEditorPalette.Slate, 110f, 26f, null, results.Count > 0, true))
                    EditorGUIUtility.systemCopyBuffer = Report();
                if (FrogletEditorPalette.ColorButton("Clear", FrogletEditorPalette.Slate, 70f, 26f, null, results.Count > 0 && !busy, true))
                    results.Clear();
            }
            if (!playing) EditorGUILayout.HelpBox("Enter Play Mode in a quiet scene (a Main Camera, nothing else spawning, no CameraManager controller) to run.", MessageType.Info);
            else if (ControllerCameraActive()) EditorGUILayout.HelpBox("A CameraManager controller is active: the crystals read ITS camera, not the Camera.main this tool moves, so their ratchets will not see the orbit. Use a quiet scene.", MessageType.Warning);
            if (busy) EditorGUILayout.HelpBox($"Running N = {running} ({(sampling ? "sampling" : "warming up")})...", MessageType.None);

            FrogletEditorPalette.HorizontalRule();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField(Report(), FrogletEditorPalette.CardBodyWrapped);
            EditorGUILayout.EndScrollView();
        }

        static bool ControllerCameraActive()
        {
            var manager = CameraManager.Instance;
            var controller = manager ? manager.GetActiveController() as CustomCameraController : null;
            return controller && controller.Camera && controller.Camera.isActiveAndEnabled && controller.Camera != Camera.main;
        }

        void Queue(params int[] counts)
        {
            foreach (int n in counts) pending.Enqueue(n);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                Teardown();
                pending.Clear();
                EditorApplication.update -= Tick;
                Repaint();
                return;
            }

            if (root == null)
            {
                if (pending.Count == 0)
                {
                    EditorApplication.update -= Tick;
                    return;
                }
                running = pending.Dequeue();
                if (!Spawn(running, out var problem))
                {
                    EditorUtility.DisplayDialog("Crystal Depth Prism Benchmark", problem, "OK");
                    Teardown();
                    pending.Clear();
                    return;
                }
                sampling = false;
                phaseEndsAt = Time.frameCount + warmupFrames;
                Repaint();
                return;
            }

            Orbit();
            if (Time.frameCount < phaseEndsAt) return;

            if (!sampling)
            {
                recorders = Stats.Select(s => Record(s.stat)).ToArray();
                sampling = true;
                phaseEndsAt = Time.frameCount + sampleFrames;
                Repaint();
                return;
            }

            var result = new Result { Count = running, Kinds = kinds, Frames = sampleFrames };
            for (int i = 0; i < Stats.Length; i++) result.Stats[Stats[i].label] = Summarise(recorders[i]);
            results.Add(result);
            Teardown();
            Repaint();
        }

        bool Spawn(int n, out string problem)
        {
            var omni = AssetDatabase.LoadAssetAtPath<GameObject>(OmniPrefabPath);
            var mass = AssetDatabase.LoadAssetAtPath<GameObject>(MassPrefabPath);
            camera = Camera.main;
            if (!omni || !mass || !camera)
            {
                problem = !omni ? $"{OmniPrefabPath} not found" : !mass ? $"{MassPrefabPath} not found" : "the scene has no Camera.main to orbit";
                return false;
            }
            cameraHome = new Pose(camera.transform.position, camera.transform.rotation);

            var prefabs = new List<GameObject>();
            for (int i = 0; i < n; i++)
            {
                if (kinds != Kinds.MassOnly) prefabs.Add(omni);
                if (kinds != Kinds.OmniOnly) prefabs.Add(mass);
            }

            // Spawned under an INACTIVE root so nothing wakes before it is stripped to its visual.
            root = new GameObject($"DepthPrismBenchmark (N = {n}, {kinds})");
            root.SetActive(false);
            ringRadius = Mathf.Max(60f, prefabs.Count * Spacing / (2f * Mathf.PI));
            ringCentre = cameraHome.position + cameraHome.rotation * Vector3.forward * (2f * ringRadius);
            for (int i = 0; i < prefabs.Count; i++)
            {
                float a = 2f * Mathf.PI * i / prefabs.Count;
                var at = ringCentre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * ringRadius;
                var crystal = Instantiate(prefabs[i], at, Quaternion.identity, root.transform);
                StripToVisual(crystal);
            }
            root.SetActive(true);
            orbitStart = Time.time;
            Orbit();
            problem = null;
            return true;
        }

        /// <summary>Behaviours first (a component another requires blocks its own removal - a NetworkObject while its
        /// NetworkBehaviours remain - so a few passes), then the rest.</summary>
        static void StripToVisual(GameObject crystal)
        {
            for (int pass = 0; pass < 4; pass++)
                foreach (var c in crystal.GetComponentsInChildren<MonoBehaviour>(true))
                    if (c && !VisualBehaviours.Contains(c.GetType()) && CanRemove(c)) DestroyImmediate(c);
            foreach (var c in crystal.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            foreach (var c in crystal.GetComponentsInChildren<Component>(true))
                if (c && c is not Transform && c is not Renderer && c is not MeshFilter && c is not MonoBehaviour) DestroyImmediate(c);
        }

        /// <summary>Nothing else on its GameObject [RequireComponent]s this component's type.</summary>
        static bool CanRemove(Component c)
        {
            foreach (var other in c.GetComponents<Component>())
            {
                if (!other || other == c) continue;
                foreach (RequireComponent req in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                    if (Requires(req.m_Type0, c) || Requires(req.m_Type1, c) || Requires(req.m_Type2, c)) return false;
            }
            return true;
        }

        static bool Requires(System.Type type, Component c) => type != null && type.IsInstanceOfType(c);

        /// <summary>The camera circles outside the ring at 1.4 x its radius, a little above it, looking at its centre.</summary>
        void Orbit()
        {
            if (!camera) return;
            float a = (Time.time - orbitStart) * OrbitDegreesPerSecond * Mathf.Deg2Rad;
            var at = ringCentre + new Vector3(Mathf.Cos(a) * 1.4f * ringRadius, 0.3f * ringRadius, Mathf.Sin(a) * 1.4f * ringRadius);
            camera.transform.SetPositionAndRotation(at, Quaternion.LookRotation(ringCentre - at, Vector3.up));
        }

        ProfilerRecorder Record(string stat)
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            if (batchCounters == null)
                batchCounters = string.Join(", ", handles.Select(h => ProfilerRecorderHandle.GetDescription(h).Name)
                    .Where(name => name.Contains("Batch") || name.Contains("SRP")).Distinct().OrderBy(x => x));
            foreach (var handle in handles)
            {
                if (ProfilerRecorderHandle.GetDescription(handle).Name != stat) continue;
                // Default (SumAllSamplesInFrame | WrapAroundWhenCapacityReached) does NOT include
                // StartImmediately, and the constructor - unlike ProfilerRecorder.StartNew - does not start
                // the recorder: without the flag it records nothing and every stat reads empty.
                return new ProfilerRecorder(handle, 5000,
                    ProfilerRecorderOptions.Default | ProfilerRecorderOptions.StartImmediately);
            }
            return default;
        }

        /// <summary>
        /// The stat's line value: milliseconds for a timing, a plain number for a counter. "not found" and
        /// "no samples" are reported apart, so a recorder that never started cannot pass for a missing stat.
        /// </summary>
        static string Summarise(ProfilerRecorder recorder)
        {
            if (!recorder.Valid) return "not found";
            if (recorder.Count == 0)
            {
                recorder.Dispose();
                return "no samples";
            }
            bool time = recorder.UnitType == ProfilerMarkerDataUnit.TimeNanoseconds;
            var values = new List<double>(recorder.Count);
            for (int i = 0; i < recorder.Count; i++) values.Add(recorder.GetSample(i).Value * (time ? 1e-6 : 1.0));
            recorder.Dispose();
            values.Sort();
            double median = values[values.Count / 2], p95 = values[Mathf.Min(values.Count - 1, (int)(0.95 * values.Count))];
            return time ? $"{median,9:F3} / {p95,9:F3} ms" : $"{median,9:F0} / {p95,9:F0}";
        }

        void Teardown()
        {
            if (recorders != null)
                foreach (var r in recorders)
                    if (r.Valid) r.Dispose();
            recorders = null;
            if (root) Destroy(root);
            root = null;
            if (camera) camera.transform.SetPositionAndRotation(cameraHome.position, cameraHome.rotation);
            camera = null;
            sampling = false;
        }

        string Report()
        {
            if (results.Count == 0) return "No runs yet.";
            var sb = new StringBuilder();
            sb.AppendLine($"Crystal Depth Prism Benchmark - Unity {Application.unityVersion}, {SystemInfo.processorType}, {SystemInfo.graphicsDeviceName}");
            sb.AppendLine("Median / 95th percentile per frame. Crystal markers are summed over every crystal in a frame.");
            foreach (var r in results)
            {
                sb.AppendLine();
                sb.AppendLine($"N = {r.Count} ({r.Kinds}), {r.Frames} frames");
                foreach (var (label, _) in Stats)
                    sb.AppendLine($"  {label,-16} {(r.Stats.TryGetValue(label, out var v) ? v : "not run")}");
            }
            sb.AppendLine();
            sb.AppendLine($"Batch / SRP counters this Unity exposes: {(string.IsNullOrEmpty(batchCounters) ? "(none)" : batchCounters)}");
            return sb.ToString();
        }
    }
}
#endif
