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
        [Tooltip("Optional public method on the owner, called with `setupArgument` once before settling (e.g. TransitionDoor(true) opens the doors as SceneLoader does).")]
        [SerializeField] string setupMethod = "TransitionDoor";
        [SerializeField] bool setupArgument = true;
        [Tooltip("Full type name of the component on the owner's GameObject that B disables.")]
        [SerializeField] string toggledType = "UnityEngine.Animator";
        [SerializeField] int settleFrames = 240;
        [SerializeField] int pairs = 40;
        [SerializeField] int framesPerHalf = 30;
        [SerializeField] int discardFramesPerHalf = 3;
        [SerializeField] string[] counters = { "PostLateUpdate.PlayerUpdateCanvases", "Canvas.BuildBatch", "UGUI.Rendering.UpdateBatches", "Main Thread" };

        [Serializable] class Series { public string counter; public List<double> a = new List<double>(); public List<double> b = new List<double>(); public double medianA, medianB, ratioBA, shareBLower; }
        [Serializable] class Report { public string prefab, toggled, device, gpu; public int pairs, framesPerHalf; public long screenshotPixels, screenshotPixelsDifferent; public List<Series> series = new List<Series>(); public string verdict; }

        IEnumerator Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            string outPath = Arg("-provenwinOut") ?? Path.Combine(Application.persistentDataPath, "provenwin_scene.json");
            var report = new Report { prefab = prefab ? prefab.name : "none", toggled = toggledType, device = SystemInfo.deviceModel, gpu = SystemInfo.graphicsDeviceName, pairs = pairs, framesPerHalf = framesPerHalf };
            Behaviour toggled = null;
            try
            {
                var go = Instantiate(prefab);
                var owner = go.GetComponentsInChildren<MonoBehaviour>(true).First(m => m && m.GetType().FullName == ownerType);
                if (!string.IsNullOrEmpty(setupMethod)) owner.GetType().GetMethod(setupMethod)?.Invoke(owner, new object[] { setupArgument });
                toggled = (Behaviour)owner.GetComponents<Component>().First(c => c && c.GetType().FullName == toggledType);
            }
            catch (Exception e) { report.verdict = "ERROR setup: " + e.Message; }

            if (toggled != null)
            {
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
                for (int p = 0; p < pairs; p++)
                {
                    for (int half = 0; half < 2; half++)
                    {
                        bool b = (p % 2 == 0) == (half == 1);   // ABBA order
                        toggled.enabled = !b;
                        for (int f = 0; f < framesPerHalf; f++)
                        {
                            yield return null;
                            if (f < discardFramesPerHalf) continue;
                            for (int c = 0; c < recorders.Length; c++)
                                (b ? all[c].b : all[c].a).Add(recorders[c].LastValue / 1e6);
                        }
                    }
                }
                foreach (var r in recorders) r.Dispose();
                foreach (var s in all)
                {
                    s.medianA = Median(s.a); s.medianB = Median(s.b);
                    s.ratioBA = s.medianA > 0 ? s.medianB / s.medianA : 0;
                    s.shareBLower = s.b.Zip(s.a, (x, y) => x < y ? 1.0 : 0.0).DefaultIfEmpty(0).Average();
                    report.series.Add(s);
                }

                // Screenshots of both states, after the same settle.
                toggled.enabled = true;
                for (int i = 0; i < 10; i++) yield return null;
                yield return new WaitForEndOfFrame();
                var shotA = ScreenCapture.CaptureScreenshotAsTexture();
                toggled.enabled = false;
                for (int i = 0; i < 10; i++) yield return null;
                yield return new WaitForEndOfFrame();
                var shotB = ScreenCapture.CaptureScreenshotAsTexture();
                var pa = shotA.GetPixels32(); var pb = shotB.GetPixels32();
                report.screenshotPixels = pa.Length;
                for (int i = 0; i < pa.Length && i < pb.Length; i++)
                    if (pa[i].r != pb[i].r || pa[i].g != pb[i].g || pa[i].b != pb[i].b) report.screenshotPixelsDifferent++;
                File.WriteAllBytes(Path.ChangeExtension(outPath, ".A.png"), shotA.EncodeToPNG());
                File.WriteAllBytes(Path.ChangeExtension(outPath, ".B.png"), shotB.EncodeToPNG());
                report.verdict = (report.screenshotPixelsDifferent == 0 ? "SCREEN IDENTICAL" : $"SCREEN DIFFERS: {report.screenshotPixelsDifferent} px") + "; " +
                                 string.Join("; ", report.series.Select(s => $"{s.counter} A {s.medianA:F4} ms B {s.medianB:F4} ms (B lower in {100 * s.shareBLower:F0}% of frames)"));
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
            File.WriteAllText(outPath, JsonUtility.ToJson(report, true));
            Debug.Log("ProvenWin scene: " + report.verdict);
            Application.Quit();
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
