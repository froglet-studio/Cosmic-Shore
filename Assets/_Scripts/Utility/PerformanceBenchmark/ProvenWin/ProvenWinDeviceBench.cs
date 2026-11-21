using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// The device half of the Proven Win bench (Docs/ProvenWin/PLAYBOOK.md): the same paired A/B that
    /// <c>ShaderABBench</c> runs in the editor, run on a phone's own GPU. Lives only in the throwaway bench scene that
    /// <c>ProvenWinDeviceBenchBuilder</c> builds into a development APK; nothing in the game references it.
    ///
    /// Each entry holds a material pair (A = shipped, B = the change, both compiled into the build), a mesh and the
    /// draw setup. Per entry it captures <see cref="Entry.timepoints"/> moments of both into an HDR target and compares
    /// them (bits, 8-bit display levels), then times <see cref="Entry.samples"/> interleaved pairs, each half a chain of
    /// command buffers ended by a one-pixel ReadPixels (a GPU sync that allocates nothing per call). The report goes to
    /// Application.persistentDataPath/provenwin_report.json and one log line per entry (tag ProvenWin), then it quits.
    /// Its console output IS its deliverable (check_console_logging allow-lists this folder).
    /// </summary>
    public class ProvenWinDeviceBench : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public string name;
            public Material a;
            public Material b;
            public Mesh mesh;
            public int pass;
            public float cameraDistance = 2.2f;
            public float modelScale = 1f;
            public float fov = 60f;
            public int width = 1920;
            public int height = 1080;
            public int timepoints = 16;
            public int drawsPerBatch = 16;
            public int chain = 8;
            public int samples = 100;
            public float warmupSeconds = 3f;
            public string clockProperty;
            public float clockSpan = 20f;
            public bool driveGlobalTime;
            public List<string> randomPerMoment = new List<string>();
            public List<string> globalVectorNames = new List<string>();
            public List<Vector4> globalVectorValues = new List<Vector4>();
        }

        [Serializable]
        class Result
        {
            public string name, device, gpu, api;
            public int coveredPixelsMean;
            public long pixelsCompared, pixelsBitDifferent, pixelsDifferent8bit, pixelsOverOneLevel;
            public int maxLevelDiff8bit;
            public double maxAbsDiff, medianA, medianB, medianRatioBA, ratioP10, ratioP90, shareBFaster;
            public string verdict;
        }

        [Serializable] class Report { public List<Result> results = new List<Result>(); }

        [SerializeField] List<Entry> entries = new List<Entry>();

        /// <summary>The A/B pairs to run; filled by ProvenWinDeviceBenchBuilder.</summary>
        public List<Entry> Entries => entries;

        Texture2D _syncTex;

        IEnumerator Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            for (int i = 0; i < 10; i++) yield return null;   // let the pipeline and the first frames settle
            var report = new Report();
            string path = Path.Combine(Application.persistentDataPath, "provenwin_report.json");
            foreach (var e in entries)
            {
                Result r;
                try { r = Run(e); }
                catch (Exception ex) { r = new Result { name = e.name, verdict = "ERROR " + ex.Message }; }
                report.results.Add(r);
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
                Debug.Log($"ProvenWin {r.name}: {r.verdict}");
                yield return null;
            }
            Debug.Log("ProvenWin DONE " + path);
            Application.Quit();
        }

        Result Run(Entry e)
        {
            foreach (var (n, v) in e.globalVectorNames.Zip(e.globalVectorValues, (n, v) => (n, v))) Shader.SetGlobalVector(n, v);
            var rt = new RenderTexture(e.width, e.height, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            rt.Create();
            var r = new Result
            {
                name = e.name, device = SystemInfo.deviceModel, gpu = SystemInfo.graphicsDeviceName,
                api = SystemInfo.graphicsDeviceType.ToString(),
            };
            try
            {
                Compare(e, rt, r);
                Time(e, rt, r);
                r.verdict =
                    (r.pixelsBitDifferent == 0 ? "BIT-IDENTICAL" :
                     r.pixelsDifferent8bit == 0 ? $"IDENTICAL at 8 bits ({r.pixelsBitDifferent} px differ in HDR)" :
                     $"DIFFERS: {r.pixelsDifferent8bit} px at 8 bits, at most {r.maxLevelDiff8bit} level(s), {r.pixelsOverOneLevel} px by more than one") +
                    $"; B/A {r.medianRatioBA:F4} (p10 {r.ratioP10:F4}, p90 {r.ratioP90:F4}); B faster in {100 * r.shareBFaster:F1}%" +
                    $"; medians A {r.medianA:F3} ms, B {r.medianB:F3} ms; {r.gpu} {r.api}";
            }
            finally { rt.Release(); }
            return r;
        }

        void SetMoment(Entry e, Material m, int i, float clock)
        {
            if (!string.IsNullOrEmpty(e.clockProperty)) m.SetFloat(e.clockProperty, clock);
            if (e.driveGlobalTime) Shader.SetGlobalVector("_Time", new Vector4(clock / 20f, clock, clock * 2f, clock * 3f));
            var rng = new System.Random(5000 + i);
            foreach (var p in e.randomPerMoment) m.SetFloat(p, (float)rng.NextDouble());
        }

        static Matrix4x4 Model(int i, float scale)
        {
            var rng = new System.Random(1000 + i);
            var rot = Quaternion.Euler((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f);
            return Matrix4x4.TRS(Vector3.zero, rot, Vector3.one * scale);
        }

        static void Draw(CommandBuffer cmd, Entry e, Material m, int i, int copies)
        {
            var eye = new Vector3(0, 0, -e.cameraDistance);
            var view = Matrix4x4.TRS(eye, Quaternion.identity, Vector3.one).inverse;
            view.m20 = -view.m20; view.m21 = -view.m21; view.m22 = -view.m22; view.m23 = -view.m23;
            var proj = Matrix4x4.Perspective(e.fov, (float)e.width / e.height, Mathf.Max(0.05f, e.cameraDistance * 0.01f),
                                             Mathf.Max(50f, e.cameraDistance * 4f + e.modelScale * 2f));
            cmd.SetViewProjectionMatrices(view, proj);
            cmd.SetGlobalVector("_WorldSpaceCameraPos", eye);
            var model = Model(i, e.modelScale);
            for (int c = 0; c < copies; c++)
                for (int s = 0; s < e.mesh.subMeshCount; s++)
                    cmd.DrawMesh(e.mesh, model, m, s, e.pass);
        }

        void Sync(RenderTexture rt)
        {
            if (_syncTex == null) _syncTex = new Texture2D(1, 1, TextureFormat.RGBAHalf, false, true);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            _syncTex.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false);
            RenderTexture.active = prev;
        }

        static int To8(float linear) => Mathf.RoundToInt(Mathf.LinearToGammaSpace(Mathf.Clamp01(linear)) * 255f);

        ushort[] Capture(Entry e, Material m, int i, RenderTexture rt, Texture2D tex)
        {
            SetMoment(e, m, i, e.clockSpan * i / Mathf.Max(1, e.timepoints));
            var cmd = new CommandBuffer();
            cmd.SetRenderTarget(rt);
            cmd.ClearRenderTarget(true, true, Color.clear);
            Draw(cmd, e, m, i, 1);
            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Release();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, e.width, e.height), 0, 0, false);
            RenderTexture.active = prev;
            return tex.GetPixelData<ushort>(0).ToArray();
        }

        void Compare(Entry e, RenderTexture rt, Result r)
        {
            var tex = new Texture2D(e.width, e.height, TextureFormat.RGBAHalf, false, true);
            long covered = 0;
            for (int i = 0; i < e.timepoints; i++)
            {
                var pa = Capture(e, e.a, i, rt, tex);
                var pb = Capture(e, e.b, i, rt, tex);
                for (int p = 0; p < pa.Length; p += 4)
                {
                    if (pa[p + 3] != 0) covered++;
                    r.pixelsCompared++;
                    bool bit = false, b8 = false, over1 = false;
                    for (int c = 0; c < 3; c++)
                    {
                        if (pa[p + c] == pb[p + c]) continue;
                        bit = true;
                        float va = Mathf.HalfToFloat(pa[p + c]), vb = Mathf.HalfToFloat(pb[p + c]);
                        r.maxAbsDiff = Math.Max(r.maxAbsDiff, Math.Abs(va - vb));
                        int levels = Math.Abs(To8(va) - To8(vb));
                        if (levels > 0) b8 = true;
                        if (levels > 1) over1 = true;
                        r.maxLevelDiff8bit = Math.Max(r.maxLevelDiff8bit, levels);
                    }
                    if (bit) r.pixelsBitDifferent++;
                    if (b8) r.pixelsDifferent8bit++;
                    if (over1) r.pixelsOverOneLevel++;
                }
            }
            r.coveredPixelsMean = (int)(covered / Math.Max(1, e.timepoints));
            Destroy(tex);
        }

        List<CommandBuffer> Build(Entry e, Material m, RenderTexture rt)
        {
            var list = new List<CommandBuffer>();
            for (int i = 0; i < e.chain; i++)
            {
                var cmd = new CommandBuffer();
                cmd.SetRenderTarget(rt);
                cmd.ClearRenderTarget(true, true, Color.clear);
                Draw(cmd, e, m, i % Math.Max(1, e.timepoints), e.drawsPerBatch);
                list.Add(cmd);
            }
            return list;
        }

        double TimeChain(List<CommandBuffer> cmds, RenderTexture rt, Stopwatch sw)
        {
            var prev = RenderTexture.active;
            sw.Restart();
            foreach (var c in cmds) Graphics.ExecuteCommandBuffer(c);
            Sync(rt);
            sw.Stop();
            RenderTexture.active = prev;
            return sw.Elapsed.TotalMilliseconds / cmds.Count;
        }

        void Time(Entry e, RenderTexture rt, Result r)
        {
            var ca = Build(e, e.a, rt);
            var cb = Build(e, e.b, rt);
            var sw = new Stopwatch();
            var heat = Stopwatch.StartNew();
            while (heat.Elapsed.TotalSeconds < e.warmupSeconds) { TimeChain(ca, rt, sw); TimeChain(cb, rt, sw); }
            var msA = new List<double>();
            var msB = new List<double>();
            var ratios = new List<double>();
            int bFaster = 0;
            for (int i = 0; i < e.samples; i++)
            {
                float t = e.clockSpan * (i % 97) / 97f;
                SetMoment(e, e.a, i % 97, t);
                SetMoment(e, e.b, i % 97, t);
                double ta, tb;
                if (i % 2 == 0) { ta = TimeChain(ca, rt, sw); tb = TimeChain(cb, rt, sw); }
                else { tb = TimeChain(cb, rt, sw); ta = TimeChain(ca, rt, sw); }
                msA.Add(ta); msB.Add(tb); ratios.Add(tb / ta);
                if (tb < ta) bFaster++;
            }
            foreach (var c in ca) c.Release();
            foreach (var c in cb) c.Release();
            ratios.Sort();
            r.medianA = Median(msA);
            r.medianB = Median(msB);
            r.medianRatioBA = Median(ratios);
            r.ratioP10 = ratios[(int)(ratios.Count * 0.1)];
            r.ratioP90 = ratios[(int)(ratios.Count * 0.9)];
            r.shareBFaster = (double)bFaster / e.samples;
        }

        static double Median(List<double> xs)
        {
            var s = xs.OrderBy(x => x).ToList();
            return s.Count == 0 ? 0 : (s.Count % 2 == 1 ? s[s.Count / 2] : 0.5 * (s[s.Count / 2 - 1] + s[s.Count / 2]));
        }
    }
}
