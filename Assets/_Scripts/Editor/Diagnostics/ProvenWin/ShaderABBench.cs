using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace CosmicShore.Editor.ProvenWin
{
    /// <summary>
    /// READER tool: a paired A/B bench for a shader change, run on the real GPU by the real Unity editor in
    /// batchmode. It never edits a project file: A and B are cloned side by side into a temporary
    /// <c>Assets/_ABBenchTemp</c> folder (shader renamed <c>ABBench/A</c> / <c>ABBench/B</c>, the swapped include
    /// repointed at the clone), and that folder is deleted before it exits. Output goes only to the spec's outDir.
    ///
    ///   1. CORRECTNESS: both materials are drawn at <c>timepoints</c> clock values x model rotations into an HDR
    ///      (ARGBHalf) target and read back; A and B are compared bit for bit, by largest absolute difference, and
    ///      by pixels that differ on an 8-bit display. PNGs of A, B and the diff mask are written.
    ///   2. COST: <c>samples</c> PAIRS. Each pair times A then B (order alternating per pair) as `chain` command
    ///      buffers of <c>drawsPerBatch</c> overdrawn draws submitted back to back with one GPU sync, after a
    ///      sustained warm-up so the GPU clock is up. Pairing makes the comparison immune to whatever else the
    ///      machine is doing: an interruption lands on both halves of a pair or on neither.
    ///      Reported: median of per-pair B/A ratios, share of pairs where B was faster, medians of each.
    ///   Guards: refuses Unity's magenta error shader (the URP pipeline is woken first: in batchmode nothing has
    ///   rendered, Shader.globalRenderPipeline is empty and every UniversalPipeline SubShader is rejected).
    ///
    /// Run: Unity.exe -batchmode -projectPath &lt;wt&gt; -executeMethod
    ///      CosmicShore.Editor.ProvenWin.ShaderABBench.Run -abSpec "&lt;a.json&gt;;&lt;b.json&gt;" -logFile &lt;log&gt;
    /// (never -nographics; add -force-vulkan / -force-d3d11 for other APIs). Playbook: Docs/ProvenWin/PLAYBOOK.md.
    /// </summary>
    public static class ShaderABBench
    {
        internal const string TempRoot = "Assets/_ABBenchTemp";

        [Serializable]
        public class Spec
        {
            public string material;          // asset path of the material under test
            public string mesh;              // asset path of the model holding the mesh
            public string meshName;          // optional: which mesh in the model
            public long meshFileId;          // optional: which mesh, by the local file id a prefab's m_Mesh names
            public string meshPrep;          // optional: "Namespace.Type.Method" taking (Mesh[, defaults]) -> Mesh
            public string swapFile;          // asset path of the file that differs (the .shader or an include it uses)
            public string variantA;          // absolute path of the A content
            public string variantB;          // absolute path of the B content
            public string outDir;            // absolute output folder
            public string clockProperty;     // optional float property driving animation (e.g. _ArcClock)
            public float clockSpan = 20f;    // clock values spread over [0, clockSpan)
            public int width = 2560;
            public int height = 1440;
            public float cameraDistance = 2.2f;
            public float fov = 60f;
            public int timepoints = 48;
            public int drawsPerBatch = 48;
            public int chain = 8;            // command buffers submitted back to back per timed half-pair
            public int samples = 200;        // timed pairs
            public float warmupSeconds = 3f; // sustained load before timing, so the GPU clock is up
            // The state the GAME draws the material in, when that differs from the .mat (a property block a script
            // writes, a colour-set tint). Measure what ships, not what the asset says.
            public List<FloatOverride> floats = new List<FloatOverride>();
            public List<ColorOverride> colors = new List<ColorOverride>();
            // Per-instance values the game stamps per object (a seed per shot): each moment draws a fresh value in [0,1),
            // the same for A and B.
            public List<string> randomPerMoment = new List<string>();
            // The shader animates on _Time rather than a clock property: the bench writes _Time itself each moment, so
            // A and B see the same instant (batchmode never advances it on its own).
            public bool driveGlobalTime;
            // Includes between swapFile and the shader, in order (swapFile is included by includeChain[0], and so on).
            public List<string> includeChain = new List<string>();
            // Static methods run once before drawing, "Namespace.Type.Method" (the game's own global publishers).
            public List<string> prepare = new List<string>();
            // Globals a running system would have set (a style flag, a band) that the publishers above do not.
            public List<VectorOverride> globalVectors = new List<VectorOverride>();
            public float modelScale = 1f;
            // A UI shader drawn as the canvas draws it: an orthographic view in pixels, no rotation, and a mesh named
            // "quad:WxH" whose object coordinates ARE canvas units (white vertex colour, UVs 0..1 across it).
            public bool orthographic;
            public List<string> logGlobals = new List<string>();   // globals to print after `prepare`, to check the state
        }

        [Serializable] public class VectorOverride { public string name; public Vector4 value; }

        [Serializable] public class FloatOverride { public string name; public float value; }
        [Serializable] public class ColorOverride { public string name; public Color value; }

        [Serializable]
        class Report
        {
            public string unityVersion;
            public string gpu;
            public string graphicsApi;
            public string spec;
            public int pixelsPerFrame;
            public int coveredPixelsMean;
            public int framesCompared;
            public long pixelsCompared;
            public long pixelsBitDifferent;
            public double maxAbsDiff;
            public long pixelsDifferent8bit;
            public int maxLevelDiff8bit;      // the most display levels (0-255) any channel of any pixel moved
            public long pixelsOverOneLevel;   // pixels where some channel moved by more than one display level
            public List<double> msA = new List<double>();
            public List<double> msB = new List<double>();
            public double medianA;
            public double medianB;
            public double medianRatioBA;     // median over pairs of B/A
            public double ratioP10;
            public double ratioP90;
            public double shareBFaster;      // fraction of pairs where B < A
            public double bPercentFaster;    // 100 * (1 - medianRatioBA)
            public string verdict;
        }

        /// <summary>Batchmode entry. -abSpec takes one spec or several separated by ';' (one editor start for all).</summary>
        public static void Run()
        {
            int exit = 0;
            string list = ArgAfter("-abSpec");
            if (string.IsNullOrEmpty(list)) { Debug.LogError("[ShaderABBench] -abSpec <spec.json>[;<spec.json>...] is required"); exit = 1; }
            else foreach (var specPath in list.Split(';').Where(s => s.Trim().Length > 0))
                if (!RunOne(specPath.Trim())) exit = 1;
            EditorApplication.Exit(exit);
        }

        /// <summary>Batchmode entry: reimports each shader named by -shaders "a;b;c" (asset paths), draws it once into a
        /// small target with every pass so its variants compile, and fails on any compiler error or the magenta fallback.
        /// The check a shader change needs on every graph it touches, not only the one the bench times.</summary>
        public static void CheckShaders()
        {
            int exit = 0;
            ShaderUtil.allowAsyncCompilation = false;
            var rt = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGBHalf) { name = "ABBench check" };
            rt.Create();
            WakePipeline(rt);
            foreach (var path in (ArgAfter("-shaders") ?? "").Split(';').Where(x => x.Trim().Length > 0).Select(x => x.Trim()))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var sh = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (sh == null) { Debug.LogError($"[ShaderABBench] check {path}: not a shader"); exit = 1; continue; }
                var m = new Material(sh);
                var mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
                var cmd = new CommandBuffer();
                cmd.SetRenderTarget(rt);
                cmd.ClearRenderTarget(true, true, Color.clear);
                for (int p = 0; p < m.passCount; p++) cmd.DrawMesh(mesh, Matrix4x4.TRS(new Vector3(0, 0, 3), Quaternion.identity, Vector3.one), m, 0, p);
                Graphics.ExecuteCommandBuffer(cmd);
                cmd.Release();
                Sync(rt);
                var errors = ShaderUtil.GetShaderMessages(sh).Where(x => x.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToList();
                bool fallback = sh.passCount <= 1 && sh.name.Contains("Error");
                foreach (var e in errors) Debug.LogError($"[ShaderABBench] check {path}: {e.message} ({e.file}:{e.line})");
                Debug.Log($"[ShaderABBench] check {path}: {(errors.Count == 0 && sh.isSupported ? "OK" : "FAIL")} passes={sh.passCount} errors={errors.Count} supported={sh.isSupported}");
                if (errors.Count > 0 || !sh.isSupported || fallback) exit = 1;
                UnityEngine.Object.DestroyImmediate(m);
            }
            rt.Release();
            EditorApplication.Exit(exit);
        }

        static bool RunOne(string specPath)
        {
            bool ok = true;
            RenderTexture rt = null;
            try
            {
                Debug.Log("[ShaderABBench] spec " + specPath);
                var spec = JsonUtility.FromJson<Spec>(File.ReadAllText(specPath));
                s_modelScale = spec.modelScale;
                Directory.CreateDirectory(spec.outDir);
                string a = File.ReadAllText(spec.variantA), b = File.ReadAllText(spec.variantB);
                if (a == b) throw new InvalidOperationException("variant A and B are identical");

                ShaderUtil.allowAsyncCompilation = false;
                var mat = AssetDatabase.LoadAssetAtPath<Material>(spec.material) ?? throw new FileNotFoundException(spec.material);
                var mesh = LoadMesh(spec);

                rt = new RenderTexture(spec.width, spec.height, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear) { name = "ABBench" };
                rt.Create();
                WakePipeline(rt);
                foreach (var call in spec.prepare) Invoke(call);
                foreach (var g in spec.globalVectors) Shader.SetGlobalVector(g.name, g.value);
                foreach (var name in spec.logGlobals) Debug.Log($"[ShaderABBench] global {name} = {Shader.GetGlobalVector(name)}");

                var matA = CloneVariant(mat, spec, "A", a);
                var matB = CloneVariant(mat, spec, "B", b);
                int passA = FirstForwardPass(matA), passB = FirstForwardPass(matB);

                var rep = new Report
                {
                    unityVersion = Application.unityVersion,
                    gpu = SystemInfo.graphicsDeviceName,
                    graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                    spec = File.ReadAllText(specPath),
                    pixelsPerFrame = spec.width * spec.height,
                };

                // â”€â”€ 1. Correctness â”€â”€
                var framesA = Capture(spec, "A", matA, mesh, passA, rt, out int coveredA);
                var framesB = Capture(spec, "B", matB, mesh, passB, rt, out _);
                rep.coveredPixelsMean = coveredA;
                Compare(spec, framesA, framesB, rep);

                // â”€â”€ 2. Cost, paired â”€â”€
                TimePaired(spec, matA, passA, matB, passB, mesh, rt, rep);

                rep.verdict =
                    (rep.pixelsDifferent8bit == 0
                        ? (rep.pixelsBitDifferent == 0 ? "BIT-IDENTICAL" : $"IDENTICAL at 8 bits ({rep.pixelsBitDifferent} px differ in HDR, max {rep.maxAbsDiff:G3})")
                        : $"DIFFERS: {rep.pixelsDifferent8bit} px at 8 bits, at most {rep.maxLevelDiff8bit} level(s), {rep.pixelsOverOneLevel} px by more than one (HDR max {rep.maxAbsDiff:G3})") +
                    $"; B/A {rep.medianRatioBA:F4} (p10 {rep.ratioP10:F4}, p90 {rep.ratioP90:F4}); B faster in {100 * rep.shareBFaster:F1}% of {rep.msA.Count} pairs" +
                    $"; medians A {rep.medianA:F3} ms, B {rep.medianB:F3} ms";

                File.WriteAllText(Path.Combine(spec.outDir, "report.json"), JsonUtility.ToJson(rep, true));
                Debug.Log("[ShaderABBench] " + rep.verdict);
            }
            catch (Exception e)
            {
                Debug.LogError("[ShaderABBench] " + e);
                ok = false;
            }
            finally
            {
                if (rt != null) rt.Release();
                if (AssetDatabase.IsValidFolder(TempRoot)) AssetDatabase.DeleteAsset(TempRoot);
            }
            return ok;
        }

        // Clone the material's shader (and the swapped include, when the swap is an include) under TempRoot/<label>,
        // renamed so A and B are two distinct shaders compiled side by side.
        internal static Material CloneVariant(Material mat, Spec spec, string label, string content)
        {
            string dir = $"{TempRoot}/{label}";
            Directory.CreateDirectory(dir);
            string shaderPath = AssetDatabase.GetAssetPath(mat.shader);
            string swap = spec.swapFile.Replace('\\', '/');
            string shaderText;
            if (string.Equals(swap, shaderPath, StringComparison.OrdinalIgnoreCase))
                shaderText = content;
            else
            {
                // The swapped file and every file in `includeChain` (the includes between it and the shader) are cloned into
                // one folder under their own names, so a relative #include in a clone finds the cloned file. The shader
                // is then repointed at the clones by asset PATH (an #include) and by GUID (a Shader Graph Custom
                // Function's m_FunctionSource).
                var files = new List<string> { swap };
                files.AddRange(spec.includeChain.Select(x => x.Replace('\\', '/')));
                for (int i = 0; i < files.Count; i++)
                    File.WriteAllText($"{dir}/{Path.GetFileName(files[i])}", i == 0 ? content : File.ReadAllText(files[i]));
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                shaderText = File.ReadAllText(shaderPath);
                bool referenced = false;
                foreach (var f in files)
                {
                    string clone = $"{dir}/{Path.GetFileName(f)}";
                    string guid = AssetDatabase.AssetPathToGUID(f), cloneGuid = AssetDatabase.AssetPathToGUID(clone);
                    if (shaderText.Contains(f)) { shaderText = shaderText.Replace(f, clone); referenced = true; }
                    if (!string.IsNullOrEmpty(guid) && shaderText.Contains(guid)) { shaderText = shaderText.Replace(guid, cloneGuid); referenced = true; }
                    // A relative #include "Name.cginc" resolves next to the clone, where the cloned file now sits.
                    if (shaderText.Contains("\"" + Path.GetFileName(f) + "\"")) referenced = true;
                }
                if (!referenced) throw new InvalidOperationException($"{shaderPath} references none of {string.Join(", ", files)} (add the includes between them to `includeChain`)");
            }
            // A .shader is renamed in its text; a .shadergraph takes its name from its file, so the clone's file name does it.
            string ext = Path.GetExtension(shaderPath);
            if (ext == ".shader")
                shaderText = new Regex("^\\s*Shader\\s+\"[^\"]*\"", RegexOptions.Multiline).Replace(shaderText, $"Shader \"ABBench/{label}\"", 1);
            string clonePath = $"{dir}/ABBench_{label}{ext}";
            File.WriteAllText(clonePath, shaderText);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var sh = AssetDatabase.LoadAssetAtPath<Shader>(clonePath) ?? throw new FileNotFoundException(clonePath);
            var m = new Material(mat) { name = $"{mat.name} ({label})", shader = sh };
            m.CopyPropertiesFromMaterial(mat);
            foreach (var f in spec.floats) m.SetFloat(f.name, f.value);
            foreach (var c in spec.colors) m.SetColor(c.name, c.value);
            Debug.Log($"[ShaderABBench] {label}: shader={sh.name} supported={sh.isSupported} hasError={ShaderUtil.ShaderHasError(sh)} " +
                      $"passCount={m.passCount} pipeline='{Shader.globalRenderPipeline}' api={SystemInfo.graphicsDeviceType}");
            foreach (var msg in ShaderUtil.GetShaderMessages(sh))
                if (msg.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                    throw new InvalidOperationException($"{label} shader error: {msg.message} ({msg.file}:{msg.line})");
            return m;
        }

        static void SetMoment(Spec spec, Material m, int i, float clock)
        {
            if (!string.IsNullOrEmpty(spec.clockProperty)) m.SetFloat(spec.clockProperty, clock);
            if (spec.driveGlobalTime) Shader.SetGlobalVector("_Time", new Vector4(clock / 20f, clock, clock * 2f, clock * 3f));
            var rng = new System.Random(5000 + i);
            foreach (var name in spec.randomPerMoment) m.SetFloat(name, (float)rng.NextDouble());
        }

        internal static void Invoke(string call)
        {
            int dot = call.LastIndexOf('.');
            string typeName = call.Substring(0, dot), method = call.Substring(dot + 1);
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(asm => asm.GetType(typeName)).FirstOrDefault(t => t != null)
                       ?? throw new TypeLoadException(typeName);
            var mi = type.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null)
                     ?? throw new MissingMethodException(call);
            mi.Invoke(null, null);
        }

        internal static void WakePipeline(RenderTexture rt)
        {
            var go = new GameObject("ABBench wake camera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            cam.targetTexture = rt;
            cam.enabled = false;
            cam.Render();
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(go);
        }

        static int FirstForwardPass(Material m)
        {
            for (int i = 0; i < m.passCount; i++)
                if (m.shader.FindPassTagValue(i, new ShaderTagId("LightMode")).name == "UniversalForward") return i;
            return 0;
        }

        internal static Mesh LoadMesh(Spec spec)
        {
            if (spec.mesh.StartsWith("quad:"))
            {
                var wh = spec.mesh.Substring(5).Split('x');
                float w = float.Parse(wh[0]), h = float.Parse(wh[1]);
                var q = new Mesh { name = spec.mesh };
                q.vertices = new[] { new Vector3(0, 0, 0), new Vector3(w, 0, 0), new Vector3(0, h, 0), new Vector3(w, h, 0) };
                q.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
                q.colors = new[] { Color.white, Color.white, Color.white, Color.white };
                q.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                q.RecalculateBounds();
                return q;
            }
            // "builtin:Sphere.fbx" names one of Unity's built-in meshes (a sky is drawn on a sphere round the eye).
            if (spec.mesh.StartsWith("builtin:")) return Resources.GetBuiltinResource<Mesh>(spec.mesh.Substring(8));
            var meshes = AssetDatabase.LoadAllAssetsAtPath(spec.mesh).OfType<Mesh>().ToList();
            var mesh = spec.meshFileId != 0
                ? meshes.FirstOrDefault(m => AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m, out string _, out long id) && id == spec.meshFileId)
                : string.IsNullOrEmpty(spec.meshName) ? meshes.FirstOrDefault() : meshes.FirstOrDefault(m => m.name == spec.meshName);
            if (mesh == null) throw new FileNotFoundException("no mesh in " + spec.mesh);
            if (string.IsNullOrEmpty(spec.meshPrep)) return mesh;

            int dot = spec.meshPrep.LastIndexOf('.');
            string typeName = spec.meshPrep.Substring(0, dot), method = spec.meshPrep.Substring(dot + 1);
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(asm => asm.GetType(typeName)).FirstOrDefault(t => t != null)
                       ?? throw new TypeLoadException(typeName);
            var mi = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                         .First(m => m.Name == method && m.GetParameters().FirstOrDefault()?.ParameterType == typeof(Mesh));
            var args = mi.GetParameters().Select((p, i) => i == 0 ? mesh : (p.HasDefaultValue ? p.DefaultValue : null)).ToArray();
            return (Mesh)mi.Invoke(null, args) ?? throw new InvalidOperationException(spec.meshPrep + " returned null");
        }

        static Matrix4x4 Model(int i)
        {
            var rng = new System.Random(1000 + i);
            var rot = Quaternion.Euler((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f);
            return Matrix4x4.TRS(Vector3.zero, rot, Vector3.one * s_modelScale);
        }

        static float s_modelScale = 1f;

        static void Draw(CommandBuffer cmd, Spec spec, Material m, Mesh mesh, int pass, Matrix4x4 model, int copies)
        {
            var eye = new Vector3(0, 0, -spec.cameraDistance);
            var view = Matrix4x4.TRS(eye, Quaternion.identity, Vector3.one).inverse;
            view.m20 = -view.m20; view.m21 = -view.m21; view.m22 = -view.m22; view.m23 = -view.m23; // camera looks down -Z
            var proj = Matrix4x4.Perspective(spec.fov, (float)spec.width / spec.height, 0.05f, 50f);
            if (spec.orthographic)
            {
                view = Matrix4x4.identity;
                proj = Matrix4x4.Ortho(0, spec.width, 0, spec.height, -100f, 100f);
                model = Matrix4x4.identity;
            }
            cmd.SetViewProjectionMatrices(view, proj);
            cmd.SetGlobalVector("_WorldSpaceCameraPos", eye);
            for (int c = 0; c < copies; c++)
                for (int s = 0; s < mesh.subMeshCount; s++)
                    cmd.DrawMesh(mesh, model, m, s, pass);
        }

        // GPU sync: a synchronous 1-pixel ReadPixels waits for everything queued before it. Not AsyncGPUReadback:
        // batchmode never ends a frame, so each request's staging buffer is never freed, and after a few thousand
        // the D3D12 device fails with E_OUTOFMEMORY (8007000e) and the editor crashes (measured 2026-11-12).
        static Texture2D s_syncTex;
        static void Sync(RenderTexture rt)
        {
            if (s_syncTex == null) s_syncTex = new Texture2D(1, 1, TextureFormat.RGBAHalf, false, true);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            s_syncTex.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false);
            RenderTexture.active = prev;
        }

        static List<ushort[]> Capture(Spec spec, string label, Material m, Mesh mesh, int pass, RenderTexture rt, out int coveredMean)
        {
            var frames = new List<ushort[]>();
            long covered = 0;
            var tex = new Texture2D(spec.width, spec.height, TextureFormat.RGBAHalf, false, true);
            for (int i = 0; i < spec.timepoints; i++)
            {
                SetMoment(spec, m, i, spec.clockSpan * i / spec.timepoints);
                var cmd = new CommandBuffer { name = "ABBench capture" };
                cmd.SetRenderTarget(rt);
                cmd.ClearRenderTarget(true, true, Color.clear);
                Draw(cmd, spec, m, mesh, pass, Model(i), 1);
                Graphics.ExecuteCommandBuffer(cmd);
                cmd.Release();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, spec.width, spec.height), 0, 0, false);
                RenderTexture.active = prev;
                var px = tex.GetPixelData<ushort>(0).ToArray();
                if (i == 0) GuardErrorShader(px, label);
                frames.Add(px);
                for (int p = 3; p < px.Length; p += 4) if (px[p] != 0) covered++;
                if (i < 6) WritePng(spec, $"{label}_t{i:D2}.png", px, spec.width, spec.height);
            }
            UnityEngine.Object.DestroyImmediate(tex);
            coveredMean = (int)(covered / spec.timepoints);
            return frames;
        }

        // Unity draws a shader it cannot use (no SubShader for the active pipeline, a compile error, a variant
        // missing for this API) in flat magenta. Timing or diffing that measures nothing, so refuse it.
        static void GuardErrorShader(ushort[] px, string label)
        {
            long covered = 0, magenta = 0;
            for (int p = 0; p < px.Length; p += 4)
            {
                if (px[p + 3] == 0) continue;
                covered++;
                if (Mathf.HalfToFloat(px[p]) > 0.99f && Mathf.HalfToFloat(px[p + 1]) < 0.01f && Mathf.HalfToFloat(px[p + 2]) > 0.99f) magenta++;
            }
            if (covered == 0) throw new InvalidOperationException($"variant {label}: nothing drawn");
            if (magenta * 2 > covered) throw new InvalidOperationException($"variant {label}: Unity's error shader (magenta) was drawn, not the material");
        }

        static void Compare(Spec spec, List<ushort[]> fa, List<ushort[]> fb, Report rep)
        {
            rep.framesCompared = fa.Count;
            for (int f = 0; f < fa.Count; f++)
            {
                var a = fa[f]; var b = fb[f];
                var diff = new ushort[a.Length];
                bool any = false;
                for (int p = 0; p < a.Length; p += 4)
                {
                    rep.pixelsCompared++;
                    bool bit = false, b8 = false, over1 = false;
                    for (int c = 0; c < 3; c++)
                    {
                        if (a[p + c] == b[p + c]) continue;
                        bit = true;
                        float va = Mathf.HalfToFloat(a[p + c]), vb = Mathf.HalfToFloat(b[p + c]);
                        double d = Math.Abs(va - vb);
                        if (d > rep.maxAbsDiff) rep.maxAbsDiff = d;
                        int levels = Math.Abs(To8(va) - To8(vb));
                        if (levels > 0) b8 = true;
                        if (levels > rep.maxLevelDiff8bit) rep.maxLevelDiff8bit = levels;
                        if (levels > 1) over1 = true;
                    }
                    ushort mark = Mathf.FloatToHalf(b8 ? 1f : bit ? 0.25f : 0f);
                    diff[p] = diff[p + 1] = diff[p + 2] = mark;
                    diff[p + 3] = Mathf.FloatToHalf(1f);
                    if (bit) { rep.pixelsBitDifferent++; any = true; }
                    if (b8) rep.pixelsDifferent8bit++;
                    if (over1) rep.pixelsOverOneLevel++;
                }
                if (f < 6 || (any && f < 24)) WritePng(spec, $"diff_t{f:D2}.png", diff, spec.width, spec.height);
            }
        }

        // What an 8-bit sRGB display would show for a linear value (clamped; the game tonemaps, which only compresses).
        static int To8(float linear) => Mathf.RoundToInt(Mathf.Clamp01(Mathf.LinearToGammaSpace(Mathf.Clamp01(linear))) * 255f);

        static void WritePng(Spec spec, string name, ushort[] px, int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var c = new Color32[w * h];
            for (int i = 0; i < c.Length; i++)
                c[i] = new Color32((byte)To8(Mathf.HalfToFloat(px[i * 4])), (byte)To8(Mathf.HalfToFloat(px[i * 4 + 1])),
                                   (byte)To8(Mathf.HalfToFloat(px[i * 4 + 2])), 255);
            t.SetPixels32(c);
            File.WriteAllBytes(Path.Combine(spec.outDir, name), t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
        }

        static List<CommandBuffer> Build(Spec spec, Material m, int pass, Mesh mesh, RenderTexture rt)
        {
            var cmds = new List<CommandBuffer>();
            for (int i = 0; i < spec.chain; i++)
            {
                var cmd = new CommandBuffer { name = "ABBench time" };
                cmd.SetRenderTarget(rt);
                cmd.ClearRenderTarget(true, true, Color.clear);
                Draw(cmd, spec, m, mesh, pass, Model(i % spec.timepoints), spec.drawsPerBatch);
                cmds.Add(cmd);
            }
            return cmds;
        }

        static double TimeChain(List<CommandBuffer> cmds, RenderTexture rt, Stopwatch sw)
        {
            sw.Restart();
            foreach (var c in cmds) Graphics.ExecuteCommandBuffer(c);
            Sync(rt);
            sw.Stop();
            return sw.Elapsed.TotalMilliseconds / cmds.Count;
        }

        // One command buffer per pose, built once. A half-pair submits `chain` of them back to back and syncs once,
        // so the GPU never idles (an idle GPU drops to its idle clock and the next batch times the ramp, not the
        // shader: measured 2026-11-11, p90 15x the median when each batch synced on its own).
        static void TimePaired(Spec spec, Material ma, int pa, Material mb, int pb, Mesh mesh, RenderTexture rt, Report rep)
        {
            var ca = Build(spec, ma, pa, mesh, rt);
            var cb = Build(spec, mb, pb, mesh, rt);
            var sw = new Stopwatch();
            var heat = Stopwatch.StartNew();
            while (heat.Elapsed.TotalSeconds < spec.warmupSeconds) { TimeChain(ca, rt, sw); TimeChain(cb, rt, sw); }

            var ratios = new List<double>();
            int bFaster = 0;
            for (int i = 0; i < spec.samples; i++)
            {
                float t = spec.clockSpan * (i % 97) / 97f;
                SetMoment(spec, ma, i % 97, t);
                SetMoment(spec, mb, i % 97, t);
                double ta, tb;
                if (i % 2 == 0) { ta = TimeChain(ca, rt, sw); tb = TimeChain(cb, rt, sw); }
                else { tb = TimeChain(cb, rt, sw); ta = TimeChain(ca, rt, sw); }
                rep.msA.Add(ta); rep.msB.Add(tb);
                ratios.Add(tb / ta);
                if (tb < ta) bFaster++;
            }
            foreach (var c in ca) c.Release();
            foreach (var c in cb) c.Release();

            var sr = ratios.OrderBy(x => x).ToList();
            rep.medianA = Median(rep.msA);
            rep.medianB = Median(rep.msB);
            rep.medianRatioBA = Median(sr);
            rep.ratioP10 = sr[(int)(sr.Count * 0.1)];
            rep.ratioP90 = sr[(int)(sr.Count * 0.9)];
            rep.shareBFaster = (double)bFaster / spec.samples;
            rep.bPercentFaster = 100.0 * (1.0 - rep.medianRatioBA);
        }

        static double Median(IEnumerable<double> xs)
        {
            var s = xs.OrderBy(x => x).ToList();
            return s.Count == 0 ? 0 : (s.Count % 2 == 1 ? s[s.Count / 2] : 0.5 * (s[s.Count / 2 - 1] + s[s.Count / 2]));
        }

        static string ArgAfter(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, flag);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
