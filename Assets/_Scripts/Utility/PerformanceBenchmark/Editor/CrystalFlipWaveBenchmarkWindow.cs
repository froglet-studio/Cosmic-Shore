#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using System.Text;
using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CosmicShore.Utility.PerformanceBenchmark.Editor
{
    /// <summary>
    /// A/B cost of the Time crystal's flip wave: N copies of <c>CrystalTime.prefab</c> in front of (or behind)
    /// the camera, playing either the PROCEDURAL wave (<see cref="CrystalFlipWave"/>, what ships) or the LEGACY
    /// setup it replaced, rebuilt on the same prefab: an Animator in <c>AlwaysAnimate</c> playing the FBX's take
    /// (the old prefab's culling mode; the clip is played through a PlayableGraph because its controller asset
    /// was deleted, and the old vertex hop's once-per-loop snap is left out - one transform write every 2 s).
    /// Each copy is stripped of its gameplay components (Crystal, colliders, sound) before it wakes, so only
    /// the visual is measured.
    ///
    /// It reads per-frame samples from ProfilerRecorders - main thread, <c>CrystalFlipWave.LateUpdate</c>
    /// (summed over every crystal in a frame), the Animator's PlayerLoop update and skinned-mesh update - and
    /// reports the median and 95th percentile of each. A stat this Unity version does not expose reads "not
    /// found"; one that exists but never fired (the flip wave in a legacy run) reads "no samples".
    ///
    /// READER tool: it spawns into the open play-mode scene and destroys what it spawned, and writes no asset,
    /// so it carries no ledger or ship panel. Instructions: Docs/TIME_CRYSTAL.md §7.
    /// </summary>
    public class CrystalFlipWaveBenchmarkWindow : EditorWindow
    {
        const string PrefabPath = "Assets/_Prefabs/Environment/CrystalTime.prefab";
        const string FbxPath = "Assets/_Models/TimeCrystalExport.fbx";
        const string TakeName = "TimeCrystalArmature|TimeSequenceAnimFinal.001";
        const float Spacing = 4f;

        enum Mode { Procedural = 0, LegacyAnimator = 1 }
        enum Placement { InView = 0, BehindCamera = 1 }

        static readonly (string label, string stat)[] Stats =
        {
            ("Main thread", "Main Thread"),
            ("Flip wave", "CrystalFlipWave.LateUpdate"),
            ("Animators", "PreLateUpdate.DirectorUpdateAnimationBegin"),
            ("Animators (end)", "PreLateUpdate.DirectorUpdateAnimationEnd"),
            ("Skinned meshes", "PostLateUpdate.UpdateAllSkinnedMeshes"),
        };

        sealed class Result
        {
            public Mode Mode;
            public Placement Placement;
            public int Count;
            public int Frames;
            /// <summary>Per stat: the line's value text - a median / p95 pair, "not found" or "no samples".</summary>
            public readonly Dictionary<string, string> Stats = new();
        }

        [SerializeField] int count = 100;
        [SerializeField] int warmupFrames = 120;
        [SerializeField] int sampleFrames = 600;
        [SerializeField] Placement placement = Placement.InView;

        readonly List<Result> results = new();
        readonly Queue<Mode> pending = new();
        readonly List<PlayableGraph> graphs = new();
        GameObject root;
        Mode running;
        int phaseEndsAt;
        bool sampling;
        ProfilerRecorder[] recorders;
        Vector2 scroll;

        [MenuItem("FrogletTools/Performance/Crystal Flip Wave Benchmark", false, 40)]
        [FrogletTool(FrogletToolCategory.Performance, Importance = 2,
            Description = "A/B frame cost of N Time crystals: the procedural flip wave vs the legacy Animator + take it replaced.")]
        public static void Open()
        {
            var window = GetWindow<CrystalFlipWaveBenchmarkWindow>("Flip Wave Benchmark");
            window.minSize = new Vector2(560, 360);
            window.Show();
        }

        void OnDisable() => Teardown();

        void OnGUI()
        {
            FrogletEditorPalette.Banner("Crystal Flip Wave Benchmark",
                "N Time crystals: procedural wave vs the legacy Animator. Enter Play Mode first.",
                FrogletEditorPalette.ColorFor(FrogletToolCategory.Performance));
            GUILayout.Space(6);

            bool busy = root != null || pending.Count > 0;
            using (new EditorGUI.DisabledScope(busy))
            {
                count = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("Crystals", "How many Time crystals each run spawns."), count), 1, 2000);
                warmupFrames = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("Warm-up frames", "Frames to let spawning settle before sampling."), warmupFrames), 10, 2000);
                sampleFrames = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("Sample frames", "Frames sampled per run."), sampleFrames), 30, 5000);
                placement = (Placement)EditorGUILayout.EnumPopup(new GUIContent("Placement", "In view: every crystal is drawn. Behind camera: none is (the procedural wave then does no work; the legacy Animator still animates)."), placement);
            }

            GUILayout.Space(6);
            bool playing = EditorApplication.isPlaying;
            using (new EditorGUILayout.HorizontalScope())
            {
                var accent = FrogletEditorPalette.ColorFor(FrogletToolCategory.Performance);
                if (FrogletEditorPalette.ColorButton("Run A/B", accent, 120f, 26f, "Procedural, then legacy, same settings.", playing && !busy))
                    Queue(Mode.Procedural, Mode.LegacyAnimator);
                if (FrogletEditorPalette.ColorButton("Procedural only", accent, 130f, 26f, null, playing && !busy, true))
                    Queue(Mode.Procedural);
                if (FrogletEditorPalette.ColorButton("Legacy only", accent, 110f, 26f, null, playing && !busy, true))
                    Queue(Mode.LegacyAnimator);
                if (FrogletEditorPalette.ColorButton("Copy report", FrogletEditorPalette.Slate, 110f, 26f, null, results.Count > 0, true))
                    EditorGUIUtility.systemCopyBuffer = Report();
            }
            if (!playing) EditorGUILayout.HelpBox("Enter Play Mode in a quiet scene (nothing else spawning) to run.", MessageType.Info);
            if (busy) EditorGUILayout.HelpBox($"Running {running} ({(sampling ? "sampling" : "warming up")})...", MessageType.None);

            FrogletEditorPalette.HorizontalRule();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField(Report(), FrogletEditorPalette.CardBodyWrapped);
            EditorGUILayout.EndScrollView();
        }

        void Queue(params Mode[] modes)
        {
            foreach (var m in modes) pending.Enqueue(m);
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
                    results.Add(new Result { Mode = running, Placement = placement, Count = 0, Frames = 0 });
                    EditorUtility.DisplayDialog("Crystal Flip Wave Benchmark", problem, "OK");
                    Teardown();
                    pending.Clear();
                    return;
                }
                sampling = false;
                phaseEndsAt = Time.frameCount + warmupFrames;
                Repaint();
                return;
            }

            if (Time.frameCount < phaseEndsAt) return;

            if (!sampling)
            {
                recorders = Stats.Select(s => Record(s.stat)).ToArray();
                sampling = true;
                phaseEndsAt = Time.frameCount + sampleFrames;
                Repaint();
                return;
            }

            var result = new Result { Mode = running, Placement = placement, Count = count, Frames = sampleFrames };
            for (int i = 0; i < Stats.Length; i++) result.Stats[Stats[i].label] = Summarise(recorders[i]);
            results.Add(result);
            Teardown();
            Repaint();
        }

        bool Spawn(Mode mode, out string problem)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var take = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<AnimationClip>().FirstOrDefault(c => c.name == TakeName);
            var camera = Camera.main;
            if (!prefab || !take || !camera)
            {
                problem = !prefab ? $"{PrefabPath} not found" : !take ? $"take '{TakeName}' not found in {FbxPath}" : "the scene has no Camera.main to place the crystals in front of";
                return false;
            }

            // Spawned under an INACTIVE root so nothing wakes before it is stripped to its visual.
            root = new GameObject($"FlipWaveBenchmark ({mode})");
            root.SetActive(false);

            int side = Mathf.CeilToInt(Mathf.Sqrt(count));
            float extent = side * Spacing;
            float distance = 0.6f * extent / Mathf.Tan(0.5f * camera.fieldOfView * Mathf.Deg2Rad) + Spacing;
            var forward = placement == Placement.InView ? camera.transform.forward : -camera.transform.forward;
            var centre = camera.transform.position + forward * distance;
            var right = camera.transform.right;
            var up = camera.transform.up;

            var animators = new List<Animator>();
            for (int i = 0; i < count; i++)
            {
                var offset = ((i % side) - 0.5f * (side - 1)) * Spacing * right + ((i / side) - 0.5f * (side - 1)) * Spacing * up;
                var crystal = Instantiate(prefab, centre + offset, Quaternion.identity, root.transform);
                var wave = crystal.GetComponent<CrystalFlipWave>();
                var model = wave ? new SerializedObject(wave).FindProperty("model").objectReferenceValue as Transform : null;
                if (!model)
                {
                    problem = "CrystalTime has no CrystalFlipWave with a model - is this branch's prefab in place?";
                    return false;
                }

                // Behaviours first: a sound emitter requires its AudioSource and would block that removal.
                foreach (var c in crystal.GetComponents<MonoBehaviour>())
                    if (c != wave) DestroyImmediate(c);
                foreach (var c in crystal.GetComponents<Component>())
                    if (c is not Transform && c is not MeshFilter && c != wave) DestroyImmediate(c);
                foreach (var c in crystal.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);

                if (mode == Mode.LegacyAnimator)
                {
                    DestroyImmediate(wave);
                    if (!model.TryGetComponent(out Animator animator)) animator = model.gameObject.AddComponent<Animator>();
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // the old prefab's m_CullingMode: 0
                    animator.applyRootMotion = false;
                    animators.Add(animator);
                }
            }

            root.SetActive(true);
            foreach (var animator in animators)
            {
                AnimationPlayableUtilities.PlayClip(animator, take, out var graph);
                graphs.Add(graph);
            }
            problem = null;
            return true;
        }

        static ProfilerRecorder Record(string stat)
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
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
        /// The stat's line value. "not found" (this Unity exposes no stat by that name) and "no samples" (found,
        /// but it never fired - the flip wave in a legacy run) are reported apart, so a recorder that never
        /// started cannot pass for a stat that does not exist.
        /// </summary>
        static string Summarise(ProfilerRecorder recorder)
        {
            if (!recorder.Valid) return "not found";
            if (recorder.Count == 0)
            {
                recorder.Dispose();
                return "no samples";
            }
            var ms = new List<double>(recorder.Count);
            for (int i = 0; i < recorder.Count; i++) ms.Add(recorder.GetSample(i).Value * 1e-6);   // ns -> ms
            recorder.Dispose();
            ms.Sort();
            return $"{ms[ms.Count / 2],8:F3} / {ms[Mathf.Min(ms.Count - 1, (int)(0.95 * ms.Count))],8:F3}";
        }

        void Teardown()
        {
            if (recorders != null)
                foreach (var r in recorders)
                    if (r.Valid) r.Dispose();
            recorders = null;
            foreach (var g in graphs)
                if (g.IsValid()) g.Destroy();
            graphs.Clear();
            if (root) Destroy(root);
            root = null;
            sampling = false;
        }

        string Report()
        {
            if (results.Count == 0) return "No runs yet.";
            var sb = new StringBuilder();
            sb.AppendLine($"Crystal Flip Wave Benchmark - Unity {Application.unityVersion}, {SystemInfo.processorType}");
            sb.AppendLine("ms per frame, median / 95th percentile. Flip wave is summed over every crystal in a frame.");
            foreach (var r in results)
            {
                sb.AppendLine();
                sb.AppendLine($"{r.Mode}, {r.Count} crystals, {r.Placement}, {r.Frames} frames");
                foreach (var (label, _) in Stats)
                    sb.AppendLine($"  {label,-16} {(r.Stats.TryGetValue(label, out var v) ? v : "not run")}");
            }
            return sb.ToString();
        }
    }
}
#endif
