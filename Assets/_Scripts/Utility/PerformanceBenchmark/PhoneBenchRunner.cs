using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using CosmicShore.Core;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// A repeatable game benchmark for a phone (<c>Docs/perf/PHONE_BENCH.md</c>): plays a list of arcade games through the
    /// NORMAL flow with every seat flown by the AI, measures each one for a fixed window, and writes one JSON line per game
    /// to <c>persistentDataPath/PerfRuns/</c>. Nobody touches the phone, so two builds can be compared game for game.
    ///
    /// <para>Started only by a launch extra, never in a normal session, and only in development builds (it needs
    /// <see cref="PilotControl"/>):</para>
    /// <code>adb shell am start -S -n com.FrogletGames.CosmicShore/com.unity3d.player.UnityPlayerActivity \
    ///   --es csbench "Menu:0:40,Slingshot:1:40,SkimRace:1:40" --es csbenchtag new</code>
    /// <para>Each entry is <c>mode:intensity:seconds</c>; <c>mode</c> is a <c>GameModes</c> name, or <c>Menu</c> to measure the
    /// menu where it stands. In the editor the same list goes in <c>-csbench</c>. Per game: menu → configure from the arcade
    /// card (as the launch modal does) → launch → Ready → hand the host's hull to the AI → settle → measure (no Profiler)
    /// → one <c>capture</c> for the per-system breakdown (Profiler on, so after the measurement) → back to the menu.
    /// Shape borrowed from <see cref="SwitchbackSoakRunner"/>. It never writes a vessel, score, course or timer.</para>
    /// </summary>
    public class PhoneBenchRunner : MonoBehaviour
    {
        public const string ExtraName = "csbench";
        public const string TagExtraName = "csbenchtag";
        /// <summary>Optional extra Profiler markers for each game's capture, comma-separated (<c>--es csbenchm "SerpentTime.AI,AI.Pilot.Update"</c>).</summary>
        public const string MarkersExtraName = "csbenchm";
        /// <summary>Optional graphics override for the whole run, never saved: <c>--es csbenchgfx "MSAA2x:65"</c> (anti-aliasing name, render scale percent; either side may be empty).</summary>
        public const string GfxExtraName = "csbenchgfx";
        const string OutputFolder = "PerfRuns";
        const string OfflineKey = "CosmicShore.OfflinePreferred";
        const float SettleSeconds = 15f;
        const int CaptureSeconds = 15;
        const int MaxFrames = 120 * 600;   // two minutes at 600 fps (an uncapped PC), ten at 120 Hz
        /// <summary>When each game's Profiler capture runs: <c>all</c> (default), <c>slow</c> (only a game whose p95 is over 16.7 ms or that had a frame over 50 ms) or <c>none</c>.</summary>
        public const string CaptureExtraName = "csbenchcap";
        /// <summary><c>1</c> measures with VSync off and no frame cap, so a PC's numbers are costs and not the monitor's rate.</summary>
        public const string UncapExtraName = "csbenchuncap";
        const string AllModes = "All";
        const string MaelstromMode = "Maelstrom";
        const float MaelstromRoundCapSeconds = 150f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        struct Entry { public string Mode; public int Intensity; public float Seconds; }

        // The frame watch: every frame since the last ResetWatch, whatever the runner is waiting on (a load, a return,
        // a Maelstrom hub), so a freeze outside a measuring window is still counted.
        int _wFrames, _wOver100, _wOver500, _wOver1000;
        float _wMaxMs, _wStart;
        // The scene-switch watch: the longest frame around the frame the active scene changed in (the Single load's activation).
        string _scene = "";
        string _switchFrom;
        int _switchFramesLeft;
        float _switchMaxMs, _prev1Ms, _prev2Ms;
        string _captureWhen = "all";
        bool _uncap;

        readonly float[] _frames = new float[MaxFrames];
        static readonly FrameTiming[] s_timings = new FrameTiming[1];
        Entry[] _entries;
        string _tag;
        string _outPath;
        GameDataSO _gameData;
        bool _skippedName;
        string _markers = "";
        int _offlineWas;
        string _gfx;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void MaybeStart()
        {
            string spec = ReadExtra(ExtraName);
            if (string.IsNullOrWhiteSpace(spec)) return;
            var go = new GameObject("[PhoneBench]");
            DontDestroyOnLoad(go);
            var r = go.AddComponent<PhoneBenchRunner>();
            r._entries = Parse(spec);
            r._tag = DeviceLogFormat.SafeName(ReadExtra(TagExtraName), "bench");
            string markers = ReadExtra(MarkersExtraName);
            r._markers = string.IsNullOrWhiteSpace(markers) ? "" : " " + MarkerBudget.ExtraMarkersPrefix + markers.Replace(" ", "");
            r._gfx = ReadExtra(GfxExtraName);
            string cap = ReadExtra(CaptureExtraName);
            if (!string.IsNullOrWhiteSpace(cap)) r._captureWhen = cap.Trim().ToLowerInvariant();
            r._uncap = ReadExtra(UncapExtraName) == "1";
            // A PC's player build shares Documents with the editor, which sends that folder's captures by itself: keep ours apart.
            if (!Application.isMobilePlatform)
                DiagnosticsHUD.OutputDirectoryOverride = Path.Combine(Application.persistentDataPath, OutputFolder, "diag_" + r._tag);
            // Offline (a local host): a Relay hiccup mid-game bounces the host to the menu and voids the run. Put back at the end.
            r._offlineWas = PlayerPrefs.GetInt(OfflineKey, 0);
            PlayerPrefs.SetInt(OfflineKey, 1);
            PlayerPrefs.Save();
            CSDebug.Log($"[PhoneBench] {r._entries.Length} game(s), tag {r._tag}: {spec}");
        }

        static string ReadExtra(string name)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var intent = activity.Call<AndroidJavaObject>("getIntent");
                return intent.Call<string>("getStringExtra", name);
            }
            catch (Exception e) { CSDebug.LogWarning($"[PhoneBench] could not read the launch extra: {e.Message}"); return null; }
#else
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-" + name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
#endif
        }

        static Entry[] Parse(string spec)
        {
            var parts = spec.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var list = new Entry[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                var f = parts[i].Trim().Split(':');
                list[i] = new Entry
                {
                    Mode = f[0],
                    Intensity = f.Length > 1 && int.TryParse(f[1], out int n) ? n : 1,
                    Seconds = f.Length > 2 && float.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float s) ? s : 40f,
                };
            }
            return list;
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(Path.Combine(Application.persistentDataPath, OutputFolder));
            _outPath = Path.Combine(Application.persistentDataPath, OutputFolder,
                $"phonebench_{_tag}_{DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}.jsonl");

            var all = Resources.FindObjectsOfTypeAll<GameDataSO>();
            _gameData = all.Length > 0 ? all[0] : null;
            if (_gameData == null) { Finish("GameDataSO not found"); yield break; }
            yield return WaitForMenu(180f);
            if (!InMenuWithPilot()) { Finish("the menu never gave a local pilot"); yield break; }

            _entries = ExpandAll(_entries);
            CSDebug.Log($"[PhoneBench] {_entries.Length} entries after expanding '{AllModes}'");

            for (int i = 0; i < _entries.Length; i++)
            {
                var e = _entries[i];
                if (!InMenuWithPilot())
                {
                    yield return ReturnToMenu(i > 0 ? _entries[i - 1] : e);
                    if (!InMenuWithPilot()) { Finish("the menu never came back"); yield break; }
                }
                yield return new WaitForSecondsRealtime(2f);

                if (e.Mode.Equals("Menu", StringComparison.OrdinalIgnoreCase))
                {
                    yield return Measure(e, "Menu", true);
                    continue;
                }
                var card = FindCard(e.Mode);
                if (card == null) { WriteSkip(e, "no arcade card for this mode in this build"); continue; }
                Configure(card, e);
                ResetWatch();
                _gameData.InvokeGameLaunch();
                CSDebug.Log($"[PhoneBench] launching {e.Mode} I{e.Intensity}");
                if (e.Mode.Equals(MaelstromMode, StringComparison.OrdinalIgnoreCase))
                {
                    yield return MaelstromJourney(e);
                    continue;
                }

                MiniGameControllerBase controller = null;
                float deadline = Time.unscaledTime + 120f;
                while (Time.unscaledTime < deadline)
                {
                    if (SceneManager.GetActiveScene().name == _gameData.SceneName && _gameData.LocalPlayer?.Vessel != null)
                    {
                        controller = FindAnyObjectByType<MiniGameControllerBase>();
                        if (controller != null) break;
                    }
                    yield return new WaitForSecondsRealtime(0.25f);
                }
                if (controller == null) { WriteSkip(e, "the game scene never became ready"); continue; }
                yield return new WaitForSecondsRealtime(2f);

                deadline = Time.unscaledTime + 90f;
                while (!_gameData.IsTurnRunning && Time.unscaledTime < deadline)
                {
                    controller.OnReadyClicked();
                    float repress = Time.unscaledTime + 10f;
                    while (!_gameData.IsTurnRunning && Time.unscaledTime < repress) yield return new WaitForSecondsRealtime(0.25f);
                }
                if (!_gameData.IsTurnRunning) { WriteSkip(e, "the game never started"); continue; }
                WriteWatch(e, "load");   // launch to the turn running: the menu-to-game load and the Ready wait

                yield return null;
                string seat = "ai";
                if (_gameData.LocalPlayer is Player host && !PilotControl.TrySetAIFlies(host, true, _gameData, out var refusal))
                    seat = "idle: " + refusal;
                yield return Measure(e, seat, false);
            }
            if (!InMenuWithPilot() && _entries.Length > 0) yield return ReturnToMenu(_entries[_entries.Length - 1]);
            Finish("done");
        }

        void Update()
        {
            float ms = Time.unscaledDeltaTime * 1000f;
            _wFrames++;
            if (ms > _wMaxMs) _wMaxMs = ms;
            if (ms > 100f) _wOver100++;
            if (ms > 500f) _wOver500++;
            if (ms > 1000f) _wOver1000++;

            // A Single load's activation frame is reported one frame after the active scene changes, so take the
            // longest of the two frames before the change and the four after it.
            string scene = SceneManager.GetActiveScene().name;
            if (scene != _scene)
            {
                if (_switchFramesLeft > 0) WriteSwitch();
                _switchFrom = _scene;
                _scene = scene;
                _switchMaxMs = Mathf.Max(ms, Mathf.Max(_prev1Ms, _prev2Ms));
                _switchFramesLeft = 4;
            }
            else if (_switchFramesLeft > 0)
            {
                if (ms > _switchMaxMs) _switchMaxMs = ms;
                if (--_switchFramesLeft == 0) WriteSwitch();
            }
            _prev2Ms = _prev1Ms;
            _prev1Ms = ms;
        }

        void WriteSwitch()
        {
            _switchFramesLeft = 0;
            if (string.IsNullOrEmpty(_outPath) || string.IsNullOrEmpty(_switchFrom)) return;
            var sb = new StringBuilder(160).Append('{');
            Field(sb, "tag", _tag).Append(',');
            Field(sb, "phase", "switch").Append(',');
            Field(sb, "from", _switchFrom).Append(',');
            Field(sb, "to", _scene).Append(',');
            sb.Append("\"maxMs\":").Append(_switchMaxMs.ToString("F0", CultureInfo.InvariantCulture)).Append(',');
            sb.Append("\"allocMB\":").Append(UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024));
            Append(sb.Append('}').ToString());
        }

        void ResetWatch()
        {
            _wFrames = _wOver100 = _wOver500 = _wOver1000 = 0;
            _wMaxMs = 0f;
            _wStart = Time.realtimeSinceStartup;
        }

        void WriteWatch(Entry e, string phase, string scene = null)
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(256).Append('{');
            Field(sb, "tag", _tag).Append(',');
            Field(sb, "mode", e.Mode).Append(',');
            sb.Append("\"intensity\":").Append(e.Intensity).Append(',');
            Field(sb, "phase", phase).Append(',');
            Field(sb, "scene", scene ?? SceneManager.GetActiveScene().name).Append(',');
            float seconds = Time.realtimeSinceStartup - _wStart;
            sb.Append("\"seconds\":").Append(seconds.ToString("F1", inv)).Append(',');
            sb.Append("\"frames\":").Append(_wFrames).Append(',');
            sb.Append("\"fps\":").Append((_wFrames / Mathf.Max(0.001f, seconds)).ToString("F1", inv)).Append(',');
            sb.Append("\"maxMs\":").Append(_wMaxMs.ToString("F0", inv)).Append(',');
            sb.Append("\"over100\":").Append(_wOver100).Append(",\"over500\":").Append(_wOver500).Append(",\"over1000\":").Append(_wOver1000).Append(',');
            sb.Append("\"allocMB\":").Append(UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024));
            Append(sb.Append('}').ToString());
            CSDebug.Log($"[PhoneBench] {e.Mode} {phase}: {sb}");
        }

        /// <summary>Back to the menu from wherever the last entry left the game, with the return's frames watched.</summary>
        IEnumerator ReturnToMenu(Entry from)
        {
            ResetWatch();
            if (MaelstromController.Instance is { IsActive: true } maelstrom) maelstrom.EndMaelstrom();
            var loader = FindAnyObjectByType<SceneLoader>();
            if (loader) loader.ReturnToMainMenu();
            yield return WaitForMenu(120f);
            WriteWatch(from, "return");
        }

        void Uncap()
        {
            if (!_uncap) return;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
        }

        /// <summary>'All' becomes every arcade card this build has loaded (Maelstrom apart: it is not one game), by mode name.</summary>
        static Entry[] ExpandAll(Entry[] entries)
        {
            var list = new System.Collections.Generic.List<Entry>(entries.Length);
            foreach (var e in entries)
            {
                if (!e.Mode.Equals(AllModes, StringComparison.OrdinalIgnoreCase)) { list.Add(e); continue; }
                var names = new System.Collections.Generic.SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var g in Resources.FindObjectsOfTypeAll<SO_ArcadeGame>())
                    if (g != null) names.Add(g.Mode.ToString());
                names.Remove(MaelstromMode);
                foreach (var other in entries) names.Remove(other.Mode);
                foreach (string name in names) list.Add(new Entry { Mode = name, Intensity = e.Intensity, Seconds = e.Seconds });
            }
            return list.ToArray();
        }

        /// <summary>
        /// A Maelstrom as its host plays it, watched from the launch: the hub (its preview lay and countdown), each
        /// round's load, the round with the AI on the host's hull, the scoreboard, and the load back to the hub. One
        /// line per stretch (phase + scene) with its longest frame; the scene-switch lines hold each load's frame.
        /// Continue is pressed for the host 8 s after a round ends, or after <see cref="MaelstromRoundCapSeconds"/> of
        /// a round that has no clock.
        /// </summary>
        IEnumerator MaelstromJourney(Entry e)
        {
            float end = Time.realtimeSinceStartup + Mathf.Max(60f, e.Seconds);
            string stretch = null, stretchScene = null;
            bool ran = false, handed = false, advanced = false;
            float nextReady = 0f, advanceAt = -1f, roundStart = 0f;
            int rounds = 0;
            while (Time.realtimeSinceStartup < end)
            {
                Uncap();
                string scene = SceneManager.GetActiveScene().name;
                bool running = _gameData.IsTurnRunning;
                bool hub = scene.IndexOf(MaelstromMode, StringComparison.OrdinalIgnoreCase) >= 0;
                bool menu = !hub && scene.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0;
                if (scene != stretchScene) { ran = handed = advanced = false; nextReady = 0f; advanceAt = -1f; }
                string now = menu ? "menu" : hub ? "hub" : running ? "round" : ran ? "roundEnd" : "roundLoad";
                if (now != stretch || scene != stretchScene)
                {
                    if (stretch != null) WriteWatch(e, stretch, stretchScene);
                    ResetWatch();
                    stretch = now;
                    stretchScene = scene;
                }
                if (menu && Time.realtimeSinceStartup > end - Mathf.Max(60f, e.Seconds) + 45f) break;   // the Maelstrom ended by itself

                if (!hub && !menu)
                {
                    if (running && !ran) { ran = true; roundStart = Time.unscaledTime; rounds++; }
                    if (running && !handed)
                    {
                        handed = true;
                        if (_gameData.LocalPlayer is Player host) PilotControl.TrySetAIFlies(host, true, _gameData, out _);
                    }
                    if (!running && !ran && Time.unscaledTime >= nextReady && _gameData.LocalPlayer?.Vessel != null)
                    {
                        var controller = FindAnyObjectByType<MiniGameControllerBase>();
                        if (controller != null) { controller.OnReadyClicked(); nextReady = Time.unscaledTime + 10f; }
                    }
                    if (ran && !advanced && advanceAt < 0f && (!running || Time.unscaledTime - roundStart > MaelstromRoundCapSeconds))
                        advanceAt = Time.unscaledTime + (running ? 0f : 8f);
                    if (!advanced && advanceAt >= 0f && Time.unscaledTime >= advanceAt && MaelstromController.Instance != null)
                    {
                        advanced = true;
                        CSDebug.Log($"[PhoneBench] Maelstrom: Continue after round {rounds} ({scene})");
                        MaelstromController.Instance.AdvanceToNextGame();
                    }
                }
                yield return new WaitForSecondsRealtime(0.25f);
            }
            if (stretch != null) WriteWatch(e, stretch, stretchScene);
        }

        /// <summary>Applies the <c>csbenchgfx</c> override to a copy of the player's settings: nothing is saved, the next
        /// normal launch has the player's own settings. Re-applied per game because each scene has its own camera.</summary>
        void ApplyGfxOverride()
        {
            if (string.IsNullOrWhiteSpace(_gfx) || !DisplayGraphicsSettings.Instance) return;
            var data = DisplayGraphicsSettings.Instance.Current.Clone();
            var f = _gfx.Split(':');
            if (f[0].Trim().Length > 0 && Enum.TryParse(f[0].Trim(), true, out CosmicShore.Data.AntiAliasingSetting aa)) data.AntiAliasing = aa;
            if (f.Length > 1 && int.TryParse(f[1], out int pct)) data.RenderScalePercent = Mathf.Clamp(pct, 25, 200);
            GraphicsSettingsApplier.ApplyQuality(data);
        }

        IEnumerator Measure(Entry e, string seat, bool menu)
        {
            ApplyGfxOverride();
            Uncap();
            yield return new WaitForSecondsRealtime(SettleSeconds);
            Uncap();

            string scene = SceneManager.GetActiveScene().name;
            int n = 0, running = 0, timings = 0;
            double cpu = 0, cpuMain = 0, gpu = 0, gc = 0;
            long allocStart = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            using var gcRec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            float end = Time.realtimeSinceStartup + Mathf.Max(5f, e.Seconds);
            yield return null;
            while (Time.realtimeSinceStartup < end && n < _frames.Length)
            {
                _frames[n++] = Time.unscaledDeltaTime * 1000f;
                if (_gameData.IsTurnRunning) running++;
                if (gcRec.Valid) gc += gcRec.LastValue;
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, s_timings) == 1 && s_timings[0].cpuFrameTime > 0)
                {
                    cpu += s_timings[0].cpuFrameTime;
                    cpuMain += s_timings[0].cpuMainThreadFrameTime;
                    gpu += s_timings[0].gpuFrameTime;
                    timings++;
                }
                yield return null;
            }

            Array.Sort(_frames, 0, n);
            double sum = 0; int over20 = 0, over33 = 0, over50 = 0;
            for (int i = 0; i < n; i++)
            {
                float ms = _frames[i];
                sum += ms;
                if (ms > 20f) over20++;
                if (ms > 33.4f) over33++;
                if (ms > 50f) over50++;
            }
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(512).Append('{');
            Field(sb, "tag", _tag).Append(',');
            Field(sb, "mode", e.Mode).Append(',');
            sb.Append("\"intensity\":").Append(e.Intensity).Append(',');
            Field(sb, "scene", scene).Append(',');
            Field(sb, "seat", seat).Append(',');
            sb.Append("\"seconds\":").Append((sum / 1000.0).ToString("F1", inv)).Append(',');
            sb.Append("\"frames\":").Append(n).Append(',');
            sb.Append("\"fps\":").Append((n / Math.Max(1e-3, sum / 1000.0)).ToString("F1", inv)).Append(',');
            sb.Append("\"avgMs\":").Append((sum / Math.Max(1, n)).ToString("F2", inv)).Append(',');
            sb.Append("\"p50Ms\":").Append(DeviceLogFormat.Percentile(_frames, n, .5f).ToString("F2", inv)).Append(',');
            sb.Append("\"p95Ms\":").Append(DeviceLogFormat.Percentile(_frames, n, .95f).ToString("F2", inv)).Append(',');
            sb.Append("\"p99Ms\":").Append(DeviceLogFormat.Percentile(_frames, n, .99f).ToString("F2", inv)).Append(',');
            sb.Append("\"maxMs\":").Append((n > 0 ? _frames[n - 1] : 0f).ToString("F1", inv)).Append(',');
            sb.Append("\"over20Pct\":").Append((100.0 * over20 / Math.Max(1, n)).ToString("F1", inv)).Append(',');
            sb.Append("\"over33Pct\":").Append((100.0 * over33 / Math.Max(1, n)).ToString("F1", inv)).Append(',');
            sb.Append("\"over50\":").Append(over50).Append(',');
            sb.Append("\"cpuMs\":").Append((timings > 0 ? cpu / timings : -1).ToString("F2", inv)).Append(',');
            sb.Append("\"cpuMainMs\":").Append((timings > 0 ? cpuMain / timings : -1).ToString("F2", inv)).Append(',');
            sb.Append("\"vsync\":").Append(QualitySettings.vSyncCount).Append(',');
            sb.Append("\"gpuMs\":").Append((timings > 0 ? gpu / timings : -1).ToString("F2", inv)).Append(',');
            sb.Append("\"gcKBPerFrame\":").Append((gc / 1024.0 / Math.Max(1, n)).ToString("F1", inv)).Append(',');
            sb.Append("\"allocMBStart\":").Append(allocStart / (1024 * 1024)).Append(',');
            sb.Append("\"allocMBEnd\":").Append(UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024)).Append(',');
            sb.Append("\"turnRunningPct\":").Append(menu ? -1 : (int)(100.0 * running / Math.Max(1, n))).Append(',');
            sb.Append("\"targetFps\":").Append(Application.targetFrameRate).Append(',');
            sb.Append("\"refreshHz\":").Append(Screen.currentResolution.refreshRateRatio.value.ToString("F0", inv)).Append(',');
            Field(sb, "quality", QualitySettings.names[QualitySettings.GetQualityLevel()]).Append(',');
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp)
                sb.Append("\"renderScale\":").Append(urp.renderScale.ToString("F2", inv)).Append(",\"msaa\":").Append(urp.msaaSampleCount).Append(',');
            Field(sb, "gfx", _gfx ?? "").Append(',');
            Field(sb, "build", Application.buildGUID).Append(',');
            Field(sb, "version", Application.version);
            sb.Append('}');
            Append(sb.ToString());
            CSDebug.Log($"[PhoneBench] {e.Mode} I{e.Intensity}: {sb}");

            // The per-system breakdown, AFTER the clean window: a capture turns the Profiler on.
            bool slow = DeviceLogFormat.Percentile(_frames, n, .95f) > 16.7f || over50 > 0;
            if (_captureWhen == "none" || (_captureWhen == "slow" && !slow)) yield break;
            string label = DeviceLogFormat.SafeName($"bench_{_tag}_{e.Mode}", "bench");
            CSDebug.Log("[PhoneBench] " + DiagnosticsHUD.RunConsole($"{DiagnosticsHUD.CaptureCommand} {label} {CaptureSeconds}{_markers}"));
            yield return new WaitForSecondsRealtime(CaptureSeconds + 3f);
        }

        void Configure(SO_ArcadeGame card, Entry e)
        {
            _gameData.SyncFromArcadeGame(card);
            _gameData.IsTraining = false;
            if (_gameData.selectedVesselClass != null && card.Vessels is { Count: > 0 } && card.Vessels[0] != null)
                _gameData.selectedVesselClass.Value = card.Vessels[0].Class;
            if (_gameData.SelectedIntensity != null)
                _gameData.SelectedIntensity.Value = Mathf.Clamp(e.Intensity, card.MinIntensity, card.MaxIntensity);
            int players = Mathf.Clamp(3, card.MinPlayersAllowed, Mathf.Max(card.MinPlayersAllowed, card.MaxPlayersAllowed));
            _gameData.ConfigurePlayerCounts(players, 1);
            _gameData.RequestedDomainCount = Mathf.Clamp(players, card.MinDomainsAllowed, card.MaxDomainsAllowed);
        }

        static SO_ArcadeGame FindCard(string mode)
        {
            foreach (var g in Resources.FindObjectsOfTypeAll<SO_ArcadeGame>())
                if (g != null && g.Mode.ToString().Equals(mode, StringComparison.OrdinalIgnoreCase)) return g;
            return null;
        }

        IEnumerator WaitForMenu(float seconds)
        {
            float deadline = Time.unscaledTime + seconds;
            while (Time.unscaledTime < deadline && !InMenuWithPilot())
            {
                // Nobody is holding the phone to answer the first-run age gate or the username step: the soaks' way past them.
                var privacy = CosmicShore.UI.PrivacyConsentOverlay.Current;
                if (privacy) Destroy(privacy.gameObject);
                if (!_skippedName)
                {
                    var auth = FindAnyObjectByType<AuthenticationSceneController>();
                    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    if (auth && auth.GetType().GetField("_awaitingPlayerInput", flags)?.GetValue(auth) is true)
                    {
                        _skippedName = true;
                        auth.GetType().GetMethod("NavigateToMainMenu", flags)?.Invoke(auth, null);
                    }
                }
                yield return new WaitForSecondsRealtime(0.25f);
            }
            yield return new WaitForSecondsRealtime(1.5f);
        }

        bool InMenuWithPilot() =>
            SceneManager.GetActiveScene().name.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0 && _gameData.LocalPlayer?.Vessel != null;

        void WriteSkip(Entry e, string why)
        {
            var sb = new StringBuilder("{");
            Field(sb, "tag", _tag).Append(',');
            Field(sb, "mode", e.Mode).Append(',');
            sb.Append("\"intensity\":").Append(e.Intensity).Append(',');
            Field(sb, "skipped", why);
            Append(sb.Append('}').ToString());
            CSDebug.LogWarning($"[PhoneBench] skipped {e.Mode}: {why}");
        }

        void Finish(string why)
        {
            PlayerPrefs.SetInt(OfflineKey, _offlineWas);
            PlayerPrefs.Save();
            Append($"{{\"tag\":\"{_tag}\",\"done\":\"{why}\"}}");
            CSDebug.Log($"[PhoneBench] finished ({why}) -> {_outPath}");
        }

        void Append(string line)
        {
            try { File.AppendAllText(_outPath, line + "\n"); }
            catch (Exception ex) { CSDebug.LogError("[PhoneBench] write failed: " + ex.Message); }
        }

        static StringBuilder Field(StringBuilder sb, string name, string value) =>
            sb.Append('"').Append(name).Append("\":\"").Append((value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
#endif
    }
}
