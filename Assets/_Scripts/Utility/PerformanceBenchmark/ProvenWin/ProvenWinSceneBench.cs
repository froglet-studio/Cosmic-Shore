using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// The in-scene half of the Proven Win bench (Docs/ProvenWin/PLAYBOOK.md), for a change whose effect is "this
    /// component stops running" (an Animator put to sleep, a camera parked): it instantiates a real prefab in a real
    /// player, waits for it to settle, then alternates A (the component enabled, as shipped) and B (disabled, as the
    /// change leaves it) in paired blocks of frames, reading the engine's own profiler counters each frame, and compares
    /// a screenshot of each state. Lives only in the throwaway scene ProvenWinSceneBenchBuilder builds into a
    /// development player; nothing in the game references it. Its output IS its deliverable.
    /// </summary>
    public class ProvenWinSceneBench : MonoBehaviour
    {
        [SerializeField] GameObject prefab;
        [Tooltip("Assembly-qualified or full type name of the MonoBehaviour that owns the component to toggle.")]
        [SerializeField] string ownerType = "CosmicShore.UI.SceneTransitionModal";
        [Tooltip("Optional: only an owner whose hierarchy path contains this text (several instances of one type).")]
        [SerializeField] string ownerPathContains;
        [Tooltip("Optional public method on the owner, called with `setupArgument` once before settling (e.g. TransitionDoor(true) opens the doors as SceneLoader does).")]
        [SerializeField] string setupMethod = "TransitionDoor";
        [SerializeField] bool setupArgument = true;
        [Tooltip("Full type name of the component on the owner's GameObject that B disables.")]
        [SerializeField] string toggledType = "UnityEngine.Animator";
        [SerializeField] int settleFrames = 240;
        [SerializeField] int pairs = 40;
        [SerializeField] int framesPerHalf = 30;
        [SerializeField] int discardFramesPerHalf = 3;
        [Tooltip("Optional: B disables EVERY enabled component of these types (full names, ';'-separated) under `toggleUnder`, all together. One writer switched off alone cannot show a change while others keep dirtying the same canvas; freeze them together first, then leave one in at a time.")]
        [SerializeField] string toggleTypes;
        [Tooltip("Name of the root object `toggleTypes` searches under (e.g. UI_Refactored); empty = the whole scene.")]
        [SerializeField] string toggleUnder;
        [Tooltip("With toggleTypes: these types (full names, ';'-separated) stay ON in B (leave-one-in).")]
        [SerializeField] string keepTypes;
        [Tooltip("Optional (probe P3): give every child of these objects (names, ';'-separated) that is hidden (a CanvasGroup at alpha 0, or any child not named in `nestKeep`) its own nested Canvas at setup; then B DISABLES those canvases. A is the nested-but-enabled layout, so compare A with a run without this option too.")]
        [SerializeField] string nestUnder;
        [SerializeField] string nestKeep;
        [Tooltip("Optional stress: instantiate the prefab this many times BEHIND the camera (off screen), in a ring.")]
        [SerializeField] int spawnBehind;
        [Tooltip("Optional: also place this many copies on a grid that spans past the edges of the view (some fully in, some straddling the frustum), so the screenshots test the on-screen and edge cases.")]
        [SerializeField] int spawnInView;
        [Tooltip("Optional (with spawnBehind): B sets updateWhenOffscreen = false on every SkinnedMeshRenderer of the spawned copies, with a local bounds cube of this full size (0 = keep the authored bounds).")]
        [SerializeField] float offscreenBoundsSize = -1f;
        [Tooltip("With spawnBehind: A forces updateWhenOffscreen ON (the state before a change that turns it off has shipped), instead of whatever the spawned copies have after their Awake.")]
        [SerializeField] bool offscreenAOn;
        [Tooltip("When > 0, skip the A/B and instead record, for this many frames, which UI objects re-dirty the canvases: RectTransforms whose transform changed, elements in uGUI's layout and graphic rebuild queues, and enabled Animators under a canvas. The candidate list for the next A/B.")]
        [SerializeField] int diagnoseFrames;
        [SerializeField] string[] counters = { "PostLateUpdate.PlayerUpdateCanvases", "Canvas.BuildBatch", "UGUI.Rendering.UpdateBatches", "Main Thread", "PreLateUpdate.UpdateAllSkinnedMeshes", "MeshSkinning.Update", "Mesh.CreateMesh", "SkinnedMeshRenderer.UpdateBounds" };

        [Serializable] class Series { public string counter; public List<double> a = new List<double>(); public List<double> b = new List<double>(); public double medianA, medianB, ratioBA, shareBLower; }
        [Serializable] class Report { public string prefab, toggled, device, gpu; public int pairs, framesPerHalf; public long screenshotPixels, screenshotPixelsDifferent; public List<Series> series = new List<Series>(); public string verdict; }

        bool _sceneLeft;
        string _leftFor;

        // A scene that navigates on its own (Authentication goes on to Menu_Main) would destroy the bench mid-run and
        // leave no report, so the editor waited out its 15-minute timeout. Survive the load and say what happened.
        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += (from, to) => { _sceneLeft = true; _leftFor = to.name; };
        }

        IEnumerator Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            string outPath = Arg("-provenwinOut") ?? Path.Combine(Application.persistentDataPath, "provenwin_scene.json");
            var report = new Report { prefab = prefab ? prefab.name : UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, toggled = toggledType, device = SystemInfo.deviceModel, gpu = SystemInfo.graphicsDeviceName, pairs = pairs, framesPerHalf = framesPerHalf };
            Behaviour toggled = null;
            var group = new List<Behaviour>();
            try
            {
                // With a prefab, measure a fresh instance; without one, measure the owner already in the open scene.
                var candidates = prefab
                    ? Instantiate(prefab).GetComponentsInChildren<MonoBehaviour>(true)
                    : FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                var owner = candidates.First(m => m && m.GetType().FullName == ownerType &&
                                                  (string.IsNullOrEmpty(ownerPathContains) || PathOf(m.transform).Contains(ownerPathContains)));
                if (!string.IsNullOrEmpty(setupMethod)) owner.GetType().GetMethod(setupMethod)?.Invoke(owner, new object[] { setupArgument });
                toggled = (Behaviour)owner.GetComponents<Component>().First(c => c && c.GetType().FullName == toggledType);
            }
            catch (Exception e) { report.verdict = "ERROR setup: " + e.Message; }
            var skinned = new List<SkinnedMeshRenderer>();
            if (spawnBehind > 0 && prefab)
            {
                var cam = Camera.main ? Camera.main : FindFirstObjectByType<Camera>();
                var origin = cam ? cam.transform.position : Vector3.zero;
                var back = cam ? -cam.transform.forward : Vector3.back;
                for (int k = 0; k < spawnBehind; k++)
                {
                    float a = k * 2.399963f;   // golden angle: a disc of copies behind the eye
                    var offset = back * (20f + (k % 7)) + Quaternion.LookRotation(back) * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (2f + 0.2f * k);
                    var copy = Instantiate(prefab, origin + offset, Quaternion.identity);
                    skinned.AddRange(copy.GetComponentsInChildren<SkinnedMeshRenderer>(true));
                }
                if (spawnInView > 0 && cam)
                {
                    int side = Mathf.CeilToInt(Mathf.Sqrt(spawnInView));
                    float dist = 0.6f;
                    float halfH = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * dist, halfW = halfH * cam.aspect;
                    for (int k = 0; k < spawnInView; k++)
                    {
                        float u = (k % side) / (float)Mathf.Max(1, side - 1) * 2.2f - 1.1f;   // -1.1..1.1: past both edges
                        float v = (k / side) / (float)Mathf.Max(1, side - 1) * 2.2f - 1.1f;
                        var pos = origin + cam.transform.forward * dist + cam.transform.right * (u * halfW) + cam.transform.up * (v * halfH);
                        skinned.AddRange(Instantiate(prefab, pos, Quaternion.identity).GetComponentsInChildren<SkinnedMeshRenderer>(true));
                    }
                }
                report.toggled = $"{spawnBehind} copies behind the camera, {spawnInView} across the view, {skinned.Count} skinned renderers; B: updateWhenOffscreen off";
                report.verdict = null;
            }
            if (!string.IsNullOrEmpty(nestUnder))
            {
                var parents = nestUnder.Split(';').Select(x => x.Trim()).Where(x => x.Length > 0).ToHashSet();
                var keepNames = (nestKeep ?? "").Split(';').Select(x => x.Trim()).Where(x => x.Length > 0).ToHashSet();
                foreach (var parent in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(t => parents.Contains(t.name)).ToList())
                    for (int c = 0; c < parent.childCount; c++)
                    {
                        var child = parent.GetChild(c);
                        if (!child.gameObject.activeInHierarchy || keepNames.Contains(child.name) || child.GetComponent<Canvas>()) continue;
                        var cg = child.GetComponent<CanvasGroup>();
                        bool hidden = parent.name == "Screens" || (cg ? cg.alpha <= 0f : true);   // screens off the strip are hidden whatever their alpha
                        if (!hidden) continue;
                        var canvas = child.gameObject.AddComponent<Canvas>();
                        child.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                        group.Add(canvas);
                    }
                report.toggled = $"{group.Count} hidden subtrees on their own canvases: " + string.Join(", ", group.Select(g => g.name));
                Debug.Log("ProvenWin scene: " + report.toggled);
                report.verdict = null;
            }
            if (!string.IsNullOrEmpty(toggleTypes))
            {
                var types = toggleTypes.Split(';').Select(x => x.Trim()).Where(x => x.Length > 0).ToHashSet();
                var keep = (keepTypes ?? "").Split(';').Select(x => x.Trim()).Where(x => x.Length > 0).ToHashSet();
                var roots = string.IsNullOrEmpty(toggleUnder)
                    ? UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()
                    : FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name == toggleUnder).Select(t => t.gameObject).ToArray();
                foreach (var root in roots)
                    foreach (var b in root.GetComponentsInChildren<Behaviour>(false))
                        if (b && b.enabled && types.Contains(b.GetType().FullName) && !keep.Contains(b.GetType().FullName)) group.Add(b);
                foreach (var b in group) if (b is Animator an) an.keepAnimatorStateOnDisable = true;
                report.toggled = $"{group.Count} components: " + string.Join(", ", group.GroupBy(b => b.GetType().Name).Select(g => $"{g.Key} x{g.Count()}"));
                Debug.Log($"ProvenWin scene: PlayerPrefs menu.hint.fly.closed = {PlayerPrefs.GetInt("menu.hint.fly.closed", -1)}");
                Debug.Log("ProvenWin scene: freezing " + report.toggled + "; " + string.Join(" | ", group.Select(b => PathOf(b.transform))));
                report.verdict = null;
            }

            if (diagnoseFrames > 0)
            {
                for (int i = 0; i < settleFrames; i++) yield return null;
                yield return Diagnose(outPath);
                Application.Quit();
                yield break;
            }

            if (toggled != null || group.Count > 0 || skinned.Count > 0)
            {
                var authored = skinned.Select(r => (offscreenAOn || r.updateWhenOffscreen, r.localBounds)).ToList();
                s_skinned = skinned; s_authored = authored; s_bounds = offscreenBoundsSize;
                for (int i = 0; i < settleFrames; i++) yield return null;
                // Find each counter by name among everything the profiler can record (markers keep their own category).
                var handles = new List<ProfilerRecorderHandle>();
                ProfilerRecorderHandle.GetAvailable(handles);
                var byName = new Dictionary<string, ProfilerRecorderHandle>();
                foreach (var h in handles)
                {
                    var d = ProfilerRecorderHandle.GetDescription(h);
                    if (!byName.ContainsKey(d.Name)) byName[d.Name] = h;
                }
                var recorders = counters.Select(n => byName.TryGetValue(n, out var h)
                    ? new ProfilerRecorder(h, 1, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.StartImmediately)
                    : ProfilerRecorder.StartNew(ProfilerCategory.Internal, n, 1)).ToArray();
                for (int c = 0; c < recorders.Length; c++)
                    if (!recorders[c].Valid) Debug.LogWarning($"ProvenWin scene: counter '{counters[c]}' is not available in this player");
                var all = counters.Select(n => new Series { counter = n }).ToArray();
                int bbIndex = Math.Max(0, Array.IndexOf(counters, "Canvas.BuildBatch"));
                var countA = new List<double>(); var countB = new List<double>();
                for (int p = 0; p < pairs && !_sceneLeft; p++)
                {
                    for (int half = 0; half < 2; half++)
                    {
                        bool b = (p % 2 == 0) == (half == 1);   // ABBA order
                        SetB(toggled, group, b);
                        for (int f = 0; f < framesPerHalf; f++)
                        {
                            yield return null;
                            if (f < discardFramesPerHalf) continue;
                            for (int c = 0; c < recorders.Length; c++)
                                (b ? all[c].b : all[c].a).Add(recorders[c].LastValue / 1e6);
                            int n = recorders[bbIndex].Valid && recorders[bbIndex].Count > 0 ? (int)recorders[bbIndex].GetSample(0).Count : 0;
                            (b ? countB : countA).Add(n);
                        }
                    }
                }
                foreach (var r in recorders) r.Dispose();
                report.series.Add(new Series { counter = "Canvas.BuildBatch calls per frame", a = countA, b = countB, medianA = countA.DefaultIfEmpty(0).Average(), medianB = countB.DefaultIfEmpty(0).Average() });
                foreach (var s in all)
                {
                    s.medianA = Median(s.a); s.medianB = Median(s.b);
                    s.ratioBA = s.medianA > 0 ? s.medianB / s.medianA : 0;
                    s.shareBLower = s.b.Zip(s.a, (x, y) => x < y ? 1.0 : 0.0).DefaultIfEmpty(0).Average();
                    report.series.Add(s);
                }

                // Screenshots of both states, after the same settle, with scaled time stopped so anything animated on
                // Time.deltaTime holds still between the two (a moving picture would differ whatever B does).
                float timeScaleBefore = Time.timeScale;
                Time.timeScale = 0f;
                SetB(toggled, group, false);
                for (int i = 0; i < 10; i++) yield return null;
                yield return new WaitForEndOfFrame();
                var shotA = ScreenCapture.CaptureScreenshotAsTexture();
                SetB(toggled, group, true);
                for (int i = 0; i < 10; i++) yield return null;
                yield return new WaitForEndOfFrame();
                var shotB = ScreenCapture.CaptureScreenshotAsTexture();
                Time.timeScale = timeScaleBefore;
                var pa = shotA.GetPixels32(); var pb = shotB.GetPixels32();
                report.screenshotPixels = pa.Length;
                for (int i = 0; i < pa.Length && i < pb.Length; i++)
                    if (pa[i].r != pb[i].r || pa[i].g != pb[i].g || pa[i].b != pb[i].b) report.screenshotPixelsDifferent++;
                File.WriteAllBytes(Path.ChangeExtension(outPath, ".A.png"), shotA.EncodeToPNG());
                File.WriteAllBytes(Path.ChangeExtension(outPath, ".B.png"), shotB.EncodeToPNG());
                report.verdict = (report.screenshotPixelsDifferent == 0 ? "SCREEN IDENTICAL" : $"SCREEN DIFFERS: {report.screenshotPixelsDifferent} px") + "; " +
                                 string.Join("; ", report.series.Select(s => $"{s.counter} A {s.medianA:F4} ms B {s.medianB:F4} ms (B lower in {100 * s.shareBLower:F0}% of frames)"));
            }
            if (_sceneLeft)
                report.verdict = $"ERROR: the scene loaded '{_leftFor}' by itself during the bench, so nothing measured here " +
                                 "describes it. Bench it where it stays (its real boot path, or a prefab in an empty scene).";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
            File.WriteAllText(outPath, JsonUtility.ToJson(report, true));
            Debug.Log("ProvenWin scene: " + report.verdict);
            Application.Quit();
        }

        static List<SkinnedMeshRenderer> s_skinned = new List<SkinnedMeshRenderer>();
        static List<(bool, Bounds)> s_authored = new List<(bool, Bounds)>();
        static float s_bounds = -1f;

        static void SetB(Behaviour one, List<Behaviour> group, bool b)
        {
            for (int k = 0; k < s_skinned.Count; k++)
            {
                var r = s_skinned[k];
                if (!r) continue;
                r.updateWhenOffscreen = b ? false : s_authored[k].Item1;
                r.localBounds = b && s_bounds > 0f ? new Bounds(Vector3.zero, Vector3.one * s_bounds) : s_authored[k].Item2;
            }
            if (s_skinned.Count > 0 && group.Count == 0 && !one) return;
            if (group.Count > 0) { foreach (var x in group) if (x) x.enabled = !b; }
            else if (one) one.enabled = !b;
        }

        [Serializable] class Dirty { public string path; public int frames; public string kind; public string components; }
        [Serializable] class Diagnosis { public string scene; public int frames; public List<Dirty> dirty = new List<Dirty>(); }

        IEnumerator Diagnose(string outPath)
        {
            var counts = new Dictionary<string, Dirty>();
            void Count(string kind, Component c)
            {
                if (!c) return;
                string key = kind + ":" + PathOf(c.transform) + (kind == "animator" ? "" : "");
                if (!counts.TryGetValue(key, out var d))
                    counts[key] = d = new Dirty
                    {
                        path = PathOf(c.transform), kind = kind,
                        // What could be driving it: the components on it and on its parent.
                        components = string.Join(", ", c.GetComponents<Component>().Concat(c.transform.parent ? c.transform.parent.GetComponents<Component>() : Array.Empty<Component>())
                                                         .Where(x => x).Select(x => x.GetType().Name).Distinct()),
                    };
                d.frames++;
            }
            var registryType = typeof(UnityEngine.UI.CanvasUpdateRegistry);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var layoutField = registryType.GetField("m_LayoutRebuildQueue", flags);
            var graphicField = registryType.GetField("m_GraphicRebuildQueue", flags);
            var rects = new List<RectTransform>();
            void Sample()
            {
                var registry = UnityEngine.UI.CanvasUpdateRegistry.instance;
                foreach (var f in new[] { layoutField, graphicField })
                {
                    // uGUI's IndexedSet does not implement enumeration; read it through Count and the indexer.
                    var queue = f?.GetValue(registry);
                    if (queue == null) continue;
                    var type = queue.GetType();
                    int n = (int)type.GetProperty("Count").GetValue(queue);
                    var item = type.GetProperty("Item");
                    for (int k = 0; k < n; k++)
                        Count(f == layoutField ? "layout" : "graphic", (item.GetValue(queue, new object[] { k }) as UnityEngine.UI.ICanvasElement)?.transform);
                }
                foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (!canvas.isRootCanvas) continue;
                    canvas.GetComponentsInChildren(false, rects);
                    foreach (var rt in rects) { if (rt.hasChanged) { Count("moved", rt); rt.hasChanged = false; } }
                    foreach (var an in canvas.GetComponentsInChildren<Animator>(false)) if (an.enabled) Count("animator", an);
                }
            }
            // Clear the flags once, then sample just before the canvases are rendered each frame.
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                foreach (var rt in canvas.GetComponentsInChildren<RectTransform>(true)) rt.hasChanged = false;
            // Sampled from the coroutine each frame (after Update): transform flags persist until read, so writes
            // from Animators and LateUpdate are seen the next frame; the rebuild queues show what Update dirtied.
            int sampled = 0, canvases = 0;
            for (int i = 0; i < diagnoseFrames; i++)
            {
                yield return null;
                try { Sample(); sampled++; }
                catch (Exception e) { Debug.LogWarning("ProvenWin scene: sample failed: " + e.Message); }
            }
            canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None).Count(c => c.isRootCanvas);
            Debug.Log($"ProvenWin scene: sampled {sampled} frames over {canvases} root canvases");
            var report = new Diagnosis { scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + $" ({canvases} root canvases)", frames = sampled };
            report.dirty = counts.Values.OrderByDescending(d => d.frames).ThenBy(d => d.path).Take(200).ToList();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
            File.WriteAllText(outPath, JsonUtility.ToJson(report, true));
            Debug.Log($"ProvenWin scene: diagnosed {counts.Count} dirtying objects over {diagnoseFrames} frames");
        }

        static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (; t; t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        static double Median(List<double> xs)
        {
            if (xs.Count == 0) return 0;
            var s = xs.OrderBy(x => x).ToList();
            return s.Count % 2 == 1 ? s[s.Count / 2] : 0.5 * (s[s.Count / 2 - 1] + s[s.Count / 2]);
        }

        static string Arg(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, flag);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
