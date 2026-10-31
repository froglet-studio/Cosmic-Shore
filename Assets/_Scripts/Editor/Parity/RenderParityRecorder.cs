using System;
using System.IO;
using System.Text;
using CosmicShore.Editor.Froglet;
using CosmicShore.Utility;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.Editor.Parity
{
    /// <summary>
    /// FrogletTools > Amoebius > Render Parity: Record Control Take. The Unity half of the
    /// render-parity harness (<c>Port/parity/README.md</c> § Render parity): opens the control scene
    /// (<c>Assets/_Scenes/Game_TestDesign/RenderParity.unity</c>, written by
    /// <c>Tools/Build/author_render_parity_scene.py</c>), enters Play mode, and records every frame
    /// of the <see cref="RenderParityTake"/> with the Unity Recorder - a PNG sequence from the take's
    /// camera at the take's resolution, and an MP4 of the same frames - into
    /// <c>Port/parity/goldens/render/&lt;take&gt;/</c>, with <c>take.json</c> (fps, warm-up, frames,
    /// size, MSAA, keyframes, Unity version, git head) next to them. Amoebius renders the same scene
    /// through <c>Port/tools/render_parity.py</c>, which then scores the pixel discrepancy against
    /// these goldens: the loss the engine's rendering is iterated on.
    ///
    /// <para>The Recorder runs at the take's fps with the frame rate capped (the same fixed step the
    /// take sets with <c>Time.captureFramerate</c>), from the first Play frame to the end of the take,
    /// so the warm-up frames are in the sequence too: the comparison aligns the two sequences on the
    /// camera's jump at take frame 0, never on frame numbers.</para>
    ///
    /// <para>READER-style tool: its output lands OUTSIDE <c>Assets/</c> (under <c>Port/parity/</c>),
    /// it touches no scene, prefab or SO, so it records nothing in <c>FrogletToolChangeLedger</c> and
    /// draws no ship panel. The run survives the domain reload Play mode causes: the request lives in
    /// <see cref="SessionState"/> and <see cref="Hook"/> re-arms after the reload.</para>
    /// </summary>
    public static class RenderParityRecorder
    {
        public const string ScenePath = "Assets/_Scenes/Game_TestDesign/RenderParity.unity";
        public const string GoldensRoot = "Port/parity/goldens/render";
        const string KeyActive = "CosmicShore.RenderParity.Active";
        const string KeyTake = "CosmicShore.RenderParity.Take";
        const string KeyBatch = "CosmicShore.RenderParity.Batch";
        const string KeyRestoreBootstrap = "CosmicShore.RenderParity.RestoreBootstrap";
        const string KeyPrevLoadBootstrap = "CosmicShore.RenderParity.PrevLoadBootstrap";

        [MenuItem("FrogletTools/Amoebius/Render Parity: Record Control Take", false, 320)]
        [FrogletTool(FrogletToolCategory.Build, Importance = 4,
            Description = "Record the render-parity control take with the Unity Recorder (the golden Amoebius is scored against).")]
        public static void RecordControlTake() => Record("control");

        [MenuItem("FrogletTools/Amoebius/Render Parity: Cancel Recording", false, 321)]
        public static void Cancel()
        {
            SessionState.EraseBool(KeyActive);
            SessionState.EraseString(KeyTake);
            RestoreBootstrap();
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            Debug.Log("[RenderParity] cancelled");
        }

        /// <summary>
        /// Puts back the SceneBootstrapper "Load Main_Menu Scene" pref that <see cref="Record"/> cleared
        /// for the take (an EditorPref, so it outlives this editor: every exit path restores it).
        /// </summary>
        static void RestoreBootstrap()
        {
            if (!SessionState.GetBool(KeyRestoreBootstrap, false)) return;
            SceneBootstrapper.LoadBootstrapSceneOnPlay = SessionState.GetBool(KeyPrevLoadBootstrap, true);
            SessionState.EraseBool(KeyRestoreBootstrap);
            SessionState.EraseBool(KeyPrevLoadBootstrap);
        }

        /// <summary>
        /// The dispatch entry point: <c>Unity -batchmode -projectPath . -executeMethod
        /// CosmicShore.Editor.Parity.RenderParityRecorder.RecordBatch -parityTake control</c>
        /// (no <c>-nographics</c>: the Recorder needs the GPU; no <c>-quit</c>: the editor exits itself
        /// when the take is written, 0 on success, 1 on failure). <c>Tools/Build/render_parity_golden.py</c>
        /// finds the editor from ProjectSettings/ProjectVersion.txt and runs this.
        /// </summary>
        public static void RecordBatch()
        {
            string take = "control";
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-parityTake") take = args[i + 1];
            SessionState.SetBool(KeyBatch, true);
            Record(take);
            if (!SessionState.GetBool(KeyActive, false))
            {
                Debug.LogError("[RenderParity] batch record did not start (see the errors above)");
                RestoreBootstrap();
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Record the control scene's take under GoldensRoot/TAKE. Public for a CLI wrapper.</summary>
        public static void Record(string take)
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[RenderParity] exit Play mode first"); return; }
            if (!File.Exists(ScenePath))
            {
                Debug.LogError($"[RenderParity] no control scene at {ScenePath}: run `python3 Tools/Build/author_render_parity_scene.py`");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(KeyActive, true);
            SessionState.SetString(KeyTake, take);
            // Play the control scene itself - suppress SceneBootstrapper's Bootstrap redirect for the
            // take (as BenchmarkAutoStart does) and restore it when the take ends.
            if (!SessionState.GetBool(KeyRestoreBootstrap, false))
            {
                SessionState.SetBool(KeyRestoreBootstrap, true);
                SessionState.SetBool(KeyPrevLoadBootstrap, SceneBootstrapper.LoadBootstrapSceneOnPlay);
            }
            SceneBootstrapper.LoadBootstrapSceneOnPlay = false;
            Debug.Log($"[RenderParity] recording take '{take}' -> {Path.Combine(GoldensRoot, take)}");
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoad]
        static class Hook
        {
            static RecorderController s_controller;
            static RenderParityTake s_take;
            static string s_dir;
            static DateTime s_started;

            static Hook()
            {
                EditorApplication.playModeStateChanged += OnPlayMode;
            }

            static void OnPlayMode(PlayModeStateChange change)
            {
                if (!SessionState.GetBool(KeyActive, false)) return;
                if (change == PlayModeStateChange.EnteredPlayMode)
                {
                    // A throw here would leave Play mode running with nothing to end it: a batch run
                    // would sit until the wrapper's timeout kills it, skipping every restore.
                    try { Begin(); }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        Debug.LogError("[RenderParity] the Recorder did not start; cancelling");
                        bool batch = SessionState.GetBool(KeyBatch, false);
                        Cancel();
                        if (batch) EditorApplication.Exit(1);
                    }
                }
                else if (change == PlayModeStateChange.ExitingPlayMode) End();
            }

            static void Begin()
            {
                s_take = UnityEngine.Object.FindFirstObjectByType<RenderParityTake>();
                if (s_take == null)
                {
                    Debug.LogError("[RenderParity] the scene has no RenderParityTake; cancelling");
                    Cancel();
                    if (SessionState.GetBool(KeyBatch, false)) EditorApplication.Exit(1);
                    return;
                }
                string take = SessionState.GetString(KeyTake, "control");
                s_dir = Path.GetFullPath(Path.Combine(GoldensRoot, take));
                Directory.CreateDirectory(Path.Combine(s_dir, "frames"));
                foreach (var old in Directory.GetFiles(Path.Combine(s_dir, "frames"), "*.png")) File.Delete(old);

                var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
                int total = s_take.WarmupFrames + s_take.TakeFrames;
                settings.SetRecordModeToFrameInterval(0, total - 1);
                settings.FrameRate = s_take.Fps;
                settings.CapFrameRate = true;
                settings.FrameRatePlayback = FrameRatePlayback.Constant;

                var png = ScriptableObject.CreateInstance<ImageRecorderSettings>();
                png.name = "render-parity-frames";
                png.Enabled = true;
                png.OutputFormat = ImageRecorderSettings.ImageRecorderOutputFormat.PNG;
                png.CaptureAlpha = false;
                png.imageInputSettings = CameraInput(s_take);
                // Concatenated, not Path.Combine: Mono's Path.Combine rejects the Recorder's <Frame> wildcard.
                png.OutputFile = Path.Combine(s_dir, "frames") + "/f<Frame>";
                settings.AddRecorderSettings(png);

                var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
                movie.name = "render-parity-movie";
                movie.Enabled = true;
                movie.EncoderSettings = new CoreEncoderSettings { Codec = CoreEncoderSettings.OutputCodec.MP4, EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High };
                movie.CaptureAlpha = false;
                movie.ImageInputSettings = CameraInput(s_take);
                movie.OutputFile = Path.Combine(s_dir, "take");
                settings.AddRecorderSettings(movie);

                s_controller = new RecorderController(settings);
                s_controller.PrepareRecording();
                s_controller.StartRecording();
                s_started = DateTime.UtcNow;
                EditorApplication.update += Poll;
                Debug.Log($"[RenderParity] Recorder started: {total} frames ({s_take.WarmupFrames} warm-up + {s_take.TakeFrames}) at {s_take.Fps} fps, {s_take.Width}x{s_take.Height} -> {s_dir}");
            }

            static CameraInputSettings CameraInput(RenderParityTake take) => new()
            {
                Source = ImageSource.MainCamera,
                OutputWidth = take.Width,
                OutputHeight = take.Height,
                CaptureUI = false,
                FlipFinalOutput = false,
            };

            static void Poll()
            {
                if (s_controller == null) { EditorApplication.update -= Poll; return; }
                if (s_controller.IsRecording()) return;
                EditorApplication.update -= Poll;
                WriteTakeJson();
                Debug.Log($"[RenderParity] recorded in {(DateTime.UtcNow - s_started).TotalSeconds:F0} s -> {s_dir}");
                SessionState.EraseBool(KeyActive);
                SessionState.EraseString(KeyTake);
                RestoreBootstrap();
                bool batch = SessionState.GetBool(KeyBatch, false);
                SessionState.EraseBool(KeyBatch);
                if (batch)
                {
                    bool ok = File.Exists(Path.Combine(s_dir, "take.mp4")) && File.Exists(Path.Combine(s_dir, "take.json"));
                    if (!ok) Debug.LogError($"[RenderParity] take.mp4 or take.json missing in {s_dir}");
                    EditorApplication.Exit(ok ? 0 : 1);
                    return;
                }
                EditorApplication.ExitPlaymode();
            }

            static void End()
            {
                EditorApplication.update -= Poll;
                if (s_controller != null && s_controller.IsRecording())
                {
                    s_controller.StopRecording();
                    Debug.LogWarning("[RenderParity] Play mode left before the take finished: the frames are partial");
                }
                s_controller = null;
                s_take = null;
                SessionState.EraseBool(KeyActive);
                SessionState.EraseString(KeyTake);
                RestoreBootstrap();
            }

            static void WriteTakeJson()
            {
                int msaa = 1;
                if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp) msaa = urp.msaaSampleCount;
                var sb = new StringBuilder();
                sb.Append("{\n");
                sb.Append($"  \"scene\": \"{ScenePath}\",\n");
                sb.Append($"  \"fps\": {s_take.Fps},\n  \"warmupFrames\": {s_take.WarmupFrames},\n  \"takeFrames\": {s_take.TakeFrames},\n");
                sb.Append($"  \"width\": {s_take.Width},\n  \"height\": {s_take.Height},\n  \"msaa\": {msaa},\n");
                sb.Append($"  \"frameFile\": \"frames/f<Frame>.png (the Recorder's <Frame>: 4 digits, the first Play frame is 0000)\",\n");
                sb.Append($"  \"movie\": \"take.mp4\",\n");
                sb.Append($"  \"unity\": \"{Application.unityVersion}\",\n");
                sb.Append($"  \"git\": \"{GitHead()}\",\n");
                sb.Append($"  \"recorded\": \"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}\",\n");
                sb.Append("  \"keyframes\": [");
                bool first = true;
                foreach (var k in s_take.Keyframes)
                {
                    sb.Append(first ? "\n" : ",\n"); first = false;
                    sb.Append($"    {{\"frame\": {k.frame}, \"position\": [{F(k.position.x)}, {F(k.position.y)}, {F(k.position.z)}], \"lookAt\": [{F(k.lookAt.x)}, {F(k.lookAt.y)}, {F(k.lookAt.z)}]}}");
                }
                sb.Append("\n  ]\n}\n");
                File.WriteAllText(Path.Combine(s_dir, "take.json"), sb.ToString());
            }

            static string F(float v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

            static string GitHead()
            {
                try
                {
                    string head = File.ReadAllText(Path.GetFullPath(".git/HEAD")).Trim();
                    if (head.StartsWith("ref: ")) head = File.ReadAllText(Path.GetFullPath(".git/" + head.Substring(5))).Trim();
                    return head;
                }
                catch { return ""; }
            }
        }
    }
}
