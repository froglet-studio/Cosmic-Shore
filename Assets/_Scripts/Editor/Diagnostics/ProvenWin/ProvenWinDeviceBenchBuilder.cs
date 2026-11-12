using System;
using System.IO;
using System.Linq;
using CosmicShore.Utility;
using CosmicShore.Utility.PerformanceBenchmark;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Editor.ProvenWin
{
    /// <summary>
    /// Builds the Proven Win DEVICE bench: one development APK whose only scene runs every given A/B spec (the same
    /// spec files <see cref="ShaderABBench"/> reads) on the phone's GPU and writes provenwin_report.json
    /// (<see cref="ProvenWinDeviceBench"/>). A and B shaders, their materials (with the spec's game-state overrides) and
    /// any prepared mesh are written under <see cref="ShaderABBench.TempRoot"/> for the build and deleted after it, so
    /// the project is left as it was. A WRITER only into that temp folder and the output path; nothing to land.
    ///
    /// Run: Unity.exe -batchmode -projectPath &lt;wt&gt; -buildTarget Android -executeMethod
    ///      CosmicShore.Editor.ProvenWin.ProvenWinDeviceBenchBuilder.Build -abSpec "a.json;b.json" -apk &lt;out.apk&gt;
    ///      [-deviceSamples 100] [-deviceTimepoints 8]
    /// Then: adb install -r out.apk; adb shell am start -n &lt;package&gt;/com.unity3d.player.UnityPlayerActivity;
    ///       adb logcat -s Unity | grep ProvenWin; adb pull /sdcard/Android/data/&lt;package&gt;/files/provenwin_report.json
    /// </summary>
    public static class ProvenWinDeviceBenchBuilder
    {
        public static void Build()
        {
            int exit = 0;
            try
            {
                string apk = Arg("-apk") ?? throw new ArgumentException("-apk <out.apk> is required");
                int samples = int.Parse(Arg("-deviceSamples") ?? "100");
                int timepoints = int.Parse(Arg("-deviceTimepoints") ?? "8");
                ShaderUtil.allowAsyncCompilation = false;
                var rt = new RenderTexture(64, 64, 24) { name = "device bench wake" };
                rt.Create();
                ShaderABBench.WakePipeline(rt);
                rt.Release();

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camGo = new GameObject("Camera");
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                var bench = new GameObject("ProvenWinDeviceBench").AddComponent<ProvenWinDeviceBench>();

                foreach (var specPath in (Arg("-abSpec") ?? "").Split(';').Select(x => x.Trim()).Where(x => x.Length > 0))
                {
                    var spec = JsonUtility.FromJson<ShaderABBench.Spec>(File.ReadAllText(specPath));
                    string name = Path.GetFileNameWithoutExtension(specPath).Replace("spec_", "");
                    foreach (var call in spec.prepare) ShaderABBench.Invoke(call);
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(spec.material) ?? throw new FileNotFoundException(spec.material);
                    var a = Persist(ShaderABBench.CloneVariant(mat, spec, name + "_A", File.ReadAllText(spec.variantA)), $"{name}_A.mat");
                    var b = Persist(ShaderABBench.CloneVariant(mat, spec, name + "_B", File.ReadAllText(spec.variantB)), $"{name}_B.mat");
                    var entry = new ProvenWinDeviceBench.Entry
                    {
                        name = name, a = a, b = b, mesh = PersistMesh(ShaderABBench.LoadMesh(spec), name),
                        pass = FirstForwardPass(a), cameraDistance = spec.cameraDistance, modelScale = spec.modelScale,
                        fov = spec.fov, timepoints = timepoints, samples = samples, drawsPerBatch = spec.drawsPerBatch,
                        chain = spec.chain, warmupSeconds = spec.warmupSeconds, clockProperty = spec.clockProperty,
                        clockSpan = spec.clockSpan, driveGlobalTime = spec.driveGlobalTime,
                        randomPerMoment = spec.randomPerMoment.ToList(),
                        globalVectorNames = spec.globalVectors.Select(g => g.name).ToList(),
                        globalVectorValues = spec.globalVectors.Select(g => g.value).ToList(),
                    };
                    bench.Entries.Add(entry);
                    Debug.Log($"[DeviceBench] entry {name}: {a.shader.name} vs {b.shader.name}, mesh {entry.mesh.name}");
                }

                string scenePath = $"{ShaderABBench.TempRoot}/ProvenWinDeviceBench.unity";
                EditorUtility.SetDirty(bench);
                EditorSceneManager.SaveScene(scene, scenePath);
                AssetDatabase.SaveAssets();

                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(apk)));
                // ARM64 only: every phone worth measuring runs it, and it halves the IL2CPP native compile.
                var archBefore = PlayerSettings.Android.targetArchitectures;
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                BuildReport report;
                try
                {
                    report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { scenePath },
                    locationPathName = apk,
                    target = BuildTarget.Android,
                    options = BuildOptions.Development,
                });
                }
                finally { PlayerSettings.Android.targetArchitectures = archBefore; }
                Debug.Log($"[DeviceBench] build {report.summary.result}: {apk} ({report.summary.totalSize / 1048576} MB, " +
                          $"{report.summary.totalTime.TotalMinutes:F1} min, {report.summary.totalErrors} errors); package {PlayerSettings.applicationIdentifier}");
                if (report.summary.result != BuildResult.Succeeded) exit = 1;
            }
            catch (Exception e)
            {
                Debug.LogError("[DeviceBench] " + e);
                exit = 1;
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (AssetDatabase.IsValidFolder(ShaderABBench.TempRoot)) AssetDatabase.DeleteAsset(ShaderABBench.TempRoot);
            }
            EditorApplication.Exit(exit);
        }

        static Material Persist(Material m, string file)
        {
            string path = $"{ShaderABBench.TempRoot}/{file}";
            AssetDatabase.CreateAsset(m, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        // A built-in or asset mesh is referenced as it is; a mesh made at run time (a baked crystal twin) is saved.
        static Mesh PersistMesh(Mesh mesh, string name)
        {
            if (AssetDatabase.Contains(mesh) || AssetDatabase.GetAssetPath(mesh).StartsWith("Library/")) return mesh;
            if (!mesh.isReadable && !CrystalEdgeArcMeshBaker.TryGetReadable(mesh, out mesh))
                throw new InvalidOperationException($"{name}: the prepared mesh is not readable and cannot be saved for the build");
            var copy = UnityEngine.Object.Instantiate(mesh);
            string path = $"{ShaderABBench.TempRoot}/{name}_mesh.asset";
            AssetDatabase.CreateAsset(copy, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        static int FirstForwardPass(Material m)
        {
            for (int i = 0; i < m.passCount; i++)
                if (m.shader.FindPassTagValue(i, new UnityEngine.Rendering.ShaderTagId("LightMode")).name == "UniversalForward") return i;
            return 0;
        }

        static string Arg(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, flag);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
