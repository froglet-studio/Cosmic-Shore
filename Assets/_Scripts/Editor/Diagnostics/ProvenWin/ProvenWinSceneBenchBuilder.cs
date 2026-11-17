using System;
using System.IO;
using System.Linq;
using CosmicShore.Utility.PerformanceBenchmark;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CosmicShore.Editor.ProvenWin
{
    /// <summary>
    /// Builds the Proven Win IN-SCENE bench: a Windows development player whose only scene instantiates one real prefab
    /// and runs <see cref="ProvenWinSceneBench"/> (A = a component enabled as shipped, B = disabled as a change leaves
    /// it, paired blocks of frames, the engine's own profiler counters, a screenshot of each state). The scene is written
    /// to a temp folder and deleted after the build; the scripting backend is set to Mono for the build and restored.
    ///
    /// Run: Unity.exe -batchmode -projectPath &lt;wt&gt; -executeMethod
    ///      CosmicShore.Editor.ProvenWin.ProvenWinSceneBenchBuilder.Build -prefab &lt;asset path&gt; -exe &lt;out.exe&gt;
    ///      [-ownerType T -setupMethod M -toggledType C]
    /// Then run the exe windowed: &lt;out.exe&gt; -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -provenwinOut &lt;json&gt;
    /// </summary>
    public static class ProvenWinSceneBenchBuilder
    {
        const string Temp = "Assets/_ABSceneBenchTemp";

        public static void Build()
        {
            int exit = 0;
            var group = NamedBuildTarget.Standalone;
            var backendBefore = PlayerSettings.GetScriptingBackend(group);
            try
            {
                string prefabPath = Arg("-prefab") ?? throw new ArgumentException("-prefab is required");
                string exe = Arg("-exe") ?? throw new ArgumentException("-exe is required");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) ?? throw new FileNotFoundException(prefabPath);

                Directory.CreateDirectory(Temp);
                AssetDatabase.Refresh();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var cam = new GameObject("Camera").AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                var bench = new GameObject("ProvenWinSceneBench").AddComponent<ProvenWinSceneBench>();
                var so = new SerializedObject(bench);
                so.FindProperty("prefab").objectReferenceValue = prefab;
                if (Arg("-ownerType") is { } owner) so.FindProperty("ownerType").stringValue = owner;
                if (Arg("-setupMethod") is { } setup) so.FindProperty("setupMethod").stringValue = setup;
                if (Arg("-toggledType") is { } toggled) so.FindProperty("toggledType").stringValue = toggled;
                so.ApplyModifiedPropertiesWithoutUndo();
                string scenePath = $"{Temp}/ProvenWinSceneBench.unity";
                EditorSceneManager.SaveScene(scene, scenePath);

                PlayerSettings.SetScriptingBackend(group, ScriptingImplementation.Mono2x);
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(exe)));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { scenePath },
                    locationPathName = exe,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development,
                });
                Debug.Log($"[SceneBench] build {report.summary.result}: {exe} ({report.summary.totalTime.TotalMinutes:F1} min, {report.summary.totalErrors} errors)");
                if (report.summary.result != BuildResult.Succeeded) exit = 1;
            }
            catch (Exception e)
            {
                Debug.LogError("[SceneBench] " + e);
                exit = 1;
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(group, backendBefore);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (AssetDatabase.IsValidFolder(Temp)) AssetDatabase.DeleteAsset(Temp);
            }
            EditorApplication.Exit(exit);
        }

        /// <summary>
        /// The same bench in the editor's own Play mode, for a project whose Library is not built for Windows (a
        /// platform switch would reimport everything). Run WITHOUT -batchmode so the Game view renders:
        /// Unity.exe -projectPath &lt;wt&gt; -buildTarget Android -executeMethod
        /// CosmicShore.Editor.ProvenWin.ProvenWinSceneBenchBuilder.RunInEditor -prefab &lt;path&gt; -provenwinOut &lt;json&gt;
        /// The editor quits by itself when the json is written (or after 15 minutes). Editor numbers are inflated
        /// against a player, but A and B are measured alike, so their ratio is still the evidence.
        /// </summary>
        public static void RunInEditor()
        {
            try
            {
                // -prefab: a fresh instance in an empty scene. -scene: the real scene, opened as it is and NOT saved
                // (the bench object is added in memory; Play keeps it because this project skips the scene reload).
                string prefabPath = Arg("-prefab"), scenePathIn = Arg("-scene");
                string outPath = Arg("-provenwinOut") ?? throw new ArgumentException("-provenwinOut is required");
                if (File.Exists(outPath)) File.Delete(outPath);
                GameObject prefab = null;
                if (scenePathIn != null) EditorSceneManager.OpenScene(scenePathIn, OpenSceneMode.Single);
                else
                {
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath ?? throw new ArgumentException("-prefab or -scene is required"))
                             ?? throw new FileNotFoundException(prefabPath);
                    Directory.CreateDirectory(Temp);
                    AssetDatabase.Refresh();
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    var cam = new GameObject("Camera").AddComponent<Camera>();
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = Color.black;
                }
                var bench = new GameObject("ProvenWinSceneBench").AddComponent<ProvenWinSceneBench>();
                var so = new SerializedObject(bench);
                so.FindProperty("prefab").objectReferenceValue = prefab;
                if (Arg("-ownerType") is { } owner) so.FindProperty("ownerType").stringValue = owner;
                if (Arg("-setupMethod") is { } setup) so.FindProperty("setupMethod").stringValue = setup;
                if (Arg("-toggledType") is { } toggled) so.FindProperty("toggledType").stringValue = toggled;
                if (Arg("-offscreenAOn") is { } aon) so.FindProperty("offscreenAOn").boolValue = aon == "true" || aon == "1";
                if (Arg("-spawnInView") is { } siv) so.FindProperty("spawnInView").intValue = int.Parse(siv);
                if (Arg("-spawnBehind") is { } sb) so.FindProperty("spawnBehind").intValue = int.Parse(sb);
                if (Arg("-offscreenBoundsSize") is { } ob) so.FindProperty("offscreenBoundsSize").floatValue = float.Parse(ob, System.Globalization.CultureInfo.InvariantCulture);
                if (Arg("-nestUnder") is { } nu) so.FindProperty("nestUnder").stringValue = nu;
                if (Arg("-nestKeep") is { } nk) so.FindProperty("nestKeep").stringValue = nk;
                if (Arg("-toggleTypes") is { } tt) so.FindProperty("toggleTypes").stringValue = tt;
                if (Arg("-toggleUnder") is { } tu) so.FindProperty("toggleUnder").stringValue = tu;
                if (Arg("-keepTypes") is { } kt) so.FindProperty("keepTypes").stringValue = kt;
                if (Arg("-ownerPathContains") is { } ownerPath) so.FindProperty("ownerPathContains").stringValue = ownerPath;
                if (Arg("-setupMethod") == "none") so.FindProperty("setupMethod").stringValue = "";
                if (Arg("-diagnoseFrames") is { } diag) so.FindProperty("diagnoseFrames").intValue = int.Parse(diag);
                so.ApplyModifiedPropertiesWithoutUndo();
                if (prefab) EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), $"{Temp}/ProvenWinSceneBench.unity");
                // The project's SceneBootstrapper turns every Play into a Bootstrap boot. Its switch is an EditorPrefs
                // key shared with every editor on this machine, so leave it alone and use its in-process
                // "restarting to switch scene" guard, which makes it stand aside for this one Play.
                var bootstrapper = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CosmicShore.Editor.SceneBootstrapper")).FirstOrDefault(t => t != null)
                                   ?? AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } }).FirstOrDefault(t => t.Name == "SceneBootstrapper");
                bootstrapper?.GetField("s_restartingToSwitchScene", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.SetValue(null, true);
                SessionState.SetString(WatchKey, outPath);
                SessionState.SetFloat(WatchStartKey, (float)EditorApplication.timeSinceStartup);
                Rearm();   // this project enters Play without a domain reload, so arm the watcher now as well
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e)
            {
                Debug.LogError("[SceneBench] " + e);
                EditorApplication.Exit(1);
            }
        }

        const string WatchKey = "ProvenWinSceneBench.out", WatchStartKey = "ProvenWinSceneBench.start";

        // Survives the domain reload of entering Play mode: re-arms the watcher from SessionState.
        [InitializeOnLoadMethod]
        static void Rearm()
        {
            if (string.IsNullOrEmpty(SessionState.GetString(WatchKey, ""))) return;
            EditorApplication.update -= Watch;
            EditorApplication.update += Watch;
        }

        static void Watch()
        {
            string outPath = SessionState.GetString(WatchKey, "");
            bool done = File.Exists(outPath);
            bool timedOut = EditorApplication.timeSinceStartup - SessionState.GetFloat(WatchStartKey, 0f) > 900;
            if (!done && !timedOut) return;
            EditorApplication.update -= Watch;
            SessionState.EraseString(WatchKey);
            EditorApplication.isPlaying = false;
            if (AssetDatabase.IsValidFolder(Temp)) AssetDatabase.DeleteAsset(Temp);
            EditorApplication.Exit(done ? 0 : 1);
        }

        static string Arg(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, flag);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
