using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The <b>escort sim</b>: plays Ark escorts with every seat flown by the AI, one after another, and writes each run's
    /// <see cref="ArkEscortReport"/> as a JSON line, so the difficulty tiers are tuned from what happened rather than
    /// guessed (Docs/Voyages/ARK_ESCORT.md, "Tuning"). A tool, never a player path: it does nothing unless the process was
    /// started with <c>-arkEscortSim</c>, which the editor launcher (<c>ArkEscortSimLauncher</c>) and the sim farm
    /// (<c>Tools/SimFarm/farm.py</c>, a Windows player built by <c>SimFarmBuild</c>) pass.
    ///
    /// <para>Arguments:
    /// <list type="bullet">
    /// <item><c>-arkEscortSim "5:Sparrow,Rhino,Manta;3:Squirrel,Dolphin,Serpent"</c> (difficulty:three vessels, runs
    /// separated by ';'), or <c>-arkEscortSim @path\job.json</c>: a farm job,
    /// <c>{"jobId":"g003-0012","seed":7101,"runs":[{"index":0,"difficulty":5,"squad":["Sparrow","Rhino","Manta"]}]}</c>
    /// (a <c>"plan"</c> string in the form above works in place of <c>runs</c>).</item>
    /// <item><c>-arkEscortOut path.jsonl</c>; <c>-arkEscortTimeScale 3</c>.</item>
    /// <item><c>-arkEscortSeed N</c>: run i sails with seed N + i (a job's <c>seed</c> does the same). Seeds the run's
    /// own draw (<see cref="ArkEscortRun.SeedOverride"/>) and <see cref="UnityEngine.Random"/>.</item>
    /// <item><c>-arkEscortFixedDt 0.0333</c>: every frame advances exactly that much game time
    /// (<see cref="Time.captureDeltaTime"/>), with no vsync and no frame cap, so a player runs the escort as fast as the CPU
    /// allows while each frame moves vessels as far as a 30 fps session does. Time scale stays 1 unless asked.
    /// EXPERIMENTAL: in the farm player (2026-10-31) the escort ran BELOW real time this way (x0.6, then single frames of
    /// 25-200 s); something still paces itself by real time and falls behind. The farm uses real time and a time scale.</item>
    /// <item><c>-arkEscortFps 60</c>: cap the frame rate (no vsync) in real-time mode, so a headless player does not
    /// spin; with a time scale, each frame then advances timeScale/fps game seconds.</item>
    /// <item><c>-arkEscortDrain 2</c>: real seconds between runs (8 by default, 2 with a fixed step).</item>
    /// <item><c>-arkEscortQuit</c>: when the plan is done, write <c>{"done":true,...}</c> as the last line and, in a
    /// player, quit (the editor launcher exits the editor on <see cref="AllDone"/>).</item>
    /// </list>
    /// <c>-arkEscortParams path.json</c>: a flat JSON object of named overrides (<c>{"ark.crawlSpeed": 18,
    /// "tier.4.population": 0.7}</c>) every run applies to its runtime copy of the escort (<see cref="ArkEscortParams"/>);
    /// each run's JSON line echoes them (<c>Params</c>) and lists the accepted key patterns (<c>ParamKeys</c>).
    /// Each line carries the job's <c>JobId</c>, <c>RunIndex</c>, <c>Seed</c>, <c>RealSeconds</c> and <c>FixedDt</c> ahead of
    /// the report's own fields. Parameters are not the sim's business: <c>-arkEscortParams</c> is read by the run.</para>
    /// </summary>
    public sealed class ArkEscortSim : MonoBehaviour
    {
        const string PlanArg = "-arkEscortSim";
        const string OutArg = "-arkEscortOut";
        const string ScaleArg = "-arkEscortTimeScale";
        const string SeedArg = "-arkEscortSeed";
        const string FixedDtArg = "-arkEscortFixedDt";
        const string DrainArg = "-arkEscortDrain";
        const string QuitArg = "-arkEscortQuit";
        const string FpsArg = "-arkEscortFps";
        const string ParamsArg = "-arkEscortParams";
        /// <summary><c>-arkEscortVerbose</c>: the ToyBox verbose channel into the editor log, so a run's END REASON
        /// ("[ArkEscort] Ended: ...", "[Arkway] Voyage ended: ...") is in the sim log instead of silent.</summary>
        const string VerboseArg = "-arkEscortVerbose";
        const float ReadyTimeoutSeconds = 600f;
        const float RunTimeoutSeconds = 1800f;
        const float HeartbeatSeconds = 30f;

        /// <summary>Raised once every planned run has finished (or the sim gave up): the editor launcher exits on it.</summary>
        public static event Action<bool> AllDone;

        /// <summary>True while the sim is driving this process.</summary>
        public static bool IsRunning { get; private set; }

        readonly List<(int index, int difficulty, SavedSquad squad, string name)> _plan = new();
        string _out;
        float _scale = 1f;
        float _fixedDt;
        float _drain = 8f;
        int _fps;
        bool _quit;
        string _jobId = "";
        int? _seed;
        ArkEscortReport _last;

        /// <summary>A farm job file (<c>-arkEscortSim @job.json</c>). Unknown fields (the farm's own) are ignored.</summary>
        [Serializable]
        sealed class Job
        {
            public string jobId;
            public int seed = int.MinValue;
            public string plan;
            public JobRun[] runs;
        }

        [Serializable]
        sealed class JobRun
        {
            public int index = -1;
            public int difficulty;
            public string[] squad;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            IsRunning = false;
            var args = Environment.GetCommandLineArgs();
            string plan = Arg(args, PlanArg);
            if (string.IsNullOrEmpty(plan)) return;
            // A sim is never listened to: silence it before anything plays (farm players run several at once).
            AudioListener.volume = 0f;
            AudioListener.pause = true;
            var go = new GameObject("ArkEscortSim");
            DontDestroyOnLoad(go);
            var sim = go.AddComponent<ArkEscortSim>();
            sim._out = Arg(args, OutArg) ?? Path.Combine(Application.persistentDataPath, "ark_escort_sim.jsonl");
            if (TryFloat(Arg(args, ScaleArg), out float s)) sim._scale = Mathf.Clamp(s, 0.25f, 8f);
            if (TryFloat(Arg(args, FixedDtArg), out float dt) && dt > 0f)
            {
                sim._fixedDt = Mathf.Clamp(dt, 1f / 240f, 0.1f);
                sim._drain = 2f;
            }
            if (TryFloat(Arg(args, DrainArg), out float drain)) sim._drain = Mathf.Clamp(drain, 0f, 60f);
            if (int.TryParse(Arg(args, FpsArg), NumberStyles.Integer, CultureInfo.InvariantCulture, out int fps))
                sim._fps = Mathf.Clamp(fps, 10, 240);
            if (int.TryParse(Arg(args, SeedArg), NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed))
                sim._seed = seed;
            sim._quit = Array.Exists(args, a => string.Equals(a, QuitArg, StringComparison.OrdinalIgnoreCase));

            if (sim._quit && !Application.isEditor)
            {
                // A farm player logs for a machine: the plain lines, not a stack per warning (30-70 MB a sweep otherwise).
                Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
                Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
            }

            if (plan.StartsWith("@")) sim.ParseJob(plan.Substring(1).Trim('"'));
            else sim.Parse(plan);
            sim.LoadParams(Arg(args, ParamsArg));
            if (Array.IndexOf(args, VerboseArg) >= 0) CSDebug.VerboseChannels |= CSLogChannel.ToyBox;
            IsRunning = true;
            sim.RunAsync(sim.GetCancellationTokenOnDestroy()).Forget();
        }

        static string Arg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        static bool TryFloat(string s, out float v) =>
            float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

        void LoadParams(string path)
        {
            ArkEscortParams.Pending = null;
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                var values = ArkEscortParams.ParseFlatJson(File.ReadAllText(path));
                ArkEscortParams.Pending = values;
                Log($"params: {values.Count} overrides from {path}");
            }
            catch (Exception e) { Log($"could not read params {path}: {e.Message}"); }
        }

        void ParseJob(string path)
        {
            Job job = null;
            try { job = JsonUtility.FromJson<Job>(File.ReadAllText(path)); }
            catch (Exception e) { Log($"could not read job {path}: {e.Message}"); }
            if (job == null) return;
            _jobId = job.jobId ?? Path.GetFileNameWithoutExtension(path);
            if (job.seed != int.MinValue) _seed = job.seed;
            if (job.runs != null && job.runs.Length > 0)
            {
                for (int i = 0; i < job.runs.Length; i++)
                {
                    var r = job.runs[i];
                    Add(r.index >= 0 ? r.index : i, r.difficulty, r.squad == null ? "" : string.Join(",", r.squad));
                }
            }
            else if (!string.IsNullOrEmpty(job.plan)) Parse(job.plan);
            else Log($"job {path} plans no runs");
        }

        void Parse(string plan)
        {
            foreach (var run in plan.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = run.Split(':');
                if (parts.Length != 2 || !int.TryParse(parts[0], out int d)) { Log($"bad run '{run}'"); continue; }
                Add(_plan.Count, d, parts[1]);
            }
        }

        void Add(int index, int difficulty, string squadText)
        {
            var vessels = squadText.Split(',');
            var slots = new SquadSlot[SavedSquad.SlotCount];
            bool ok = vessels.Length == SavedSquad.SlotCount;
            for (int i = 0; ok && i < slots.Length; i++)
            {
                ok = Enum.TryParse(vessels[i].Trim(), true, out VesselClassType v);
                // A real element, not None: the run seeds seats to level 5 in their slot's element, and None seeds
                // nothing - so the kits' level-5 safeties (Domain-Safe Skybursts, Clean Blast, Shield Steal) were
                // off and the squad chewed the Ark's own hull. Each hull takes the element its safety lives in.
                slots[i] = new SquadSlot(v, ArkEscortRoles.DefaultElement(v));
            }
            if (!ok) { Log($"bad squad '{squadText}'"); return; }
            _plan.Add((index, difficulty, new SavedSquad("escort sim", slots[0], slots[1], slots[2]), squadText));
        }

        static float Now => Time.realtimeSinceStartup;

        void ApplyClock()
        {
            if (_fixedDt > 0f)
            {
                Time.captureDeltaTime = _fixedDt;
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
            }
            else if (_fps > 0)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = _fps;
            }
            Time.timeScale = _scale;
        }

        async UniTaskVoid RunAsync(CancellationToken ct)
        {
            bool ok = false;
            int written = 0;
            try
            {
                Log($"plan: {_plan.Count} runs, time scale {_scale}, fixed dt {_fixedDt}, job '{_jobId}', " +
                    $"seed {(_seed.HasValue ? _seed.Value.ToString() : "clock")}, out {_out}");
                if (_plan.Count == 0) { Log("nothing to run"); return; }
                if (_fixedDt > 0f || _fps > 0) ApplyClock();
                var harbour = await ReadyAsync(ct);
                if (!harbour) { Log("never ready - giving up"); return; }

                ArkEscortRun.Finished += OnFinished;
                for (int i = 0; i < _plan.Count; i++)
                {
                    var (index, difficulty, squad, name) = _plan[i];
                    harbour = VoyageHarbour.Current;
                    if (!harbour) { Log("the harbour went away"); break; }
                    Log($"run {i + 1}/{_plan.Count} (#{index}): d{difficulty} {name}");
                    _last = null;
                    float started = Now;
                    var freestyle = FindAnyObjectByType<MenuCrystalClickHandler>(FindObjectsInactive.Include);
                    await EnterFreestyleAsync(freestyle, Now + 60f, ct);
                    harbour = VoyageHarbour.Current;
                    if (!harbour) { Log("the harbour went away entering freestyle"); break; }

                    int? seed = _seed.HasValue ? _seed.Value + index : (int?)null;
                    if (seed.HasValue)
                    {
                        ArkEscortRun.SeedOverride = seed.Value;
                        UnityEngine.Random.InitState(seed.Value);
                    }
                    harbour.SetSailEscort(difficulty, freestyle, squad, aiLocal: true);

                    float deadline = Now + RunTimeoutSeconds;
                    float sailed = Now;
                    float gameSailed = Time.time;
                    int framesSailed = Time.frameCount;
                    float beat = Now + HeartbeatSeconds;
                    while (_last == null && Now < deadline)
                    {
                        ApplyClock();
                        await UniTask.Delay(1000, DelayType.Realtime, cancellationToken: ct);
                        if (Now > beat)
                        {
                            // The farm's heartbeat (a quiet run logs nothing for minutes), and its speed: game s per real s.
                            beat = Now + HeartbeatSeconds;
                            float real = Mathf.Max(0.001f, Now - sailed);
                            Log($"beat: {real:0}s real, {Time.time - gameSailed:0}s game " +
                                $"(x{(Time.time - gameSailed) / real:0.00}), {(Time.frameCount - framesSailed) / real:0} fps, " +
                                $"escorting {(harbour && harbour.IsEscorting ? "yes" : "no")}");
                        }
                        harbour = VoyageHarbour.Current;
                        if (harbour && !harbour.IsEscorting && _last == null && Now > sailed + 60f)
                        {
                            Log("the escort is not out (did not sail, or ended unreported)");
                            break;
                        }
                    }
                    Time.timeScale = 1f;
                    ArkEscortRun.SeedOverride = null;
                    var report = _last ?? new ArkEscortReport
                        { Difficulty = difficulty, Squad = name, Result = ArkEscortReport.Outcome.None };
                    if (_last == null) Log("no report (timed out or never sailed)");
                    Write(report, index, seed, Now - started);
                    written++;

                    // Let the corridor drain before the next run stands its own.
                    if (i < _plan.Count - 1 || !_quit)
                        await UniTask.Delay(Mathf.RoundToInt(_drain * 1000f), DelayType.Realtime, cancellationToken: ct);
                }
                ok = true;
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Log($"threw: {e}"); }
            finally
            {
                ArkEscortRun.Finished -= OnFinished;
                ArkEscortParams.Pending = null;
                ArkEscortRun.SeedOverride = null;
                Time.timeScale = 1f;
                Time.captureDeltaTime = 0f;
                IsRunning = false;
                Log(ok ? "done" : "stopped");
                if (_quit)
                    Append($"{{\"done\":true,\"ok\":{(ok ? "true" : "false")},\"runs\":{written},\"planned\":{_plan.Count}," +
                           $"\"JobId\":\"{Escape(_jobId)}\",\"RealSeconds\":{Now.ToString("0.0", CultureInfo.InvariantCulture)}}}");
                AllDone?.Invoke(ok);
                if (_quit && !Application.isEditor) Application.Quit(ok ? 0 : 1);
            }
        }

        /// <summary>The game is in the menu with a vessel and a harbour; enters freestyle if the harbour waits for it.</summary>
        async UniTask<VoyageHarbour> ReadyAsync(CancellationToken ct)
        {
            float start = Now;
            float deadline = start + ReadyTimeoutSeconds;
            bool asked = false;
            while (Now < deadline)
            {
                var freestyle = FindAnyObjectByType<MenuCrystalClickHandler>(FindObjectsInactive.Include);
                var harbour = VoyageHarbour.Current;
                if (harbour && freestyle)
                {
                    // Into freestyle here, with all the time it needs (a first play in the editor imports as it goes, and
                    // the harbour's own hand-off waits only a few seconds); the escort then sails straight from it.
                    await UniTask.Delay(_fixedDt > 0f ? 2000 : 5000, DelayType.Realtime, cancellationToken: ct);
                    await EnterFreestyleAsync(freestyle, deadline, ct);
                    await UniTask.Delay(_fixedDt > 0f ? 1000 : 3000, DelayType.Realtime, cancellationToken: ct);
                    Log($"ready after {Now - start:0}s");
                    return VoyageHarbour.Current;
                }
                if (!harbour && freestyle && !asked && !freestyle.IsTransitioning && !freestyle.IsInFreestyle)
                {
                    // The harbour stands with the toybox; on a build where the toybox waits for freestyle, ask for it.
                    if (Now > start + 30f)
                    {
                        asked = true;
                        Log("no harbour after 30s in the menu - entering freestyle");
                        freestyle.ToggleTransition();
                    }
                }
                await UniTask.Delay(1000, DelayType.Realtime, cancellationToken: ct);
            }
            return null;
        }

        /// <summary>Into freestyle, asking again whenever the menu is idle outside it: a toggle asked while the menu's own
        /// intro transition runs is dropped, and the menu then settles outside freestyle.</summary>
        static async UniTask EnterFreestyleAsync(MenuCrystalClickHandler freestyle, float deadline, CancellationToken ct)
        {
            while (freestyle && (!freestyle.IsInFreestyle || freestyle.IsTransitioning) && Now < deadline)
            {
                if (!freestyle.IsInFreestyle && !freestyle.IsTransitioning) freestyle.ToggleTransition();
                await UniTask.Delay(500, DelayType.Realtime, cancellationToken: ct);
            }
        }

        void OnFinished(ArkEscortReport report) => _last = report;

        void Write(ArkEscortReport report, int index, int? seed, float realSeconds)
        {
            Log(report.Summary());
            // The farm's keys go ahead of the report's own, spliced into its JSON so the report class stays the run's.
            var head = new StringBuilder("{")
                .Append($"\"JobId\":\"{Escape(_jobId)}\",\"RunIndex\":{index},")
                .Append($"\"Seed\":{(seed.HasValue ? seed.Value.ToString(CultureInfo.InvariantCulture) : "null")},")
                .Append($"\"RealSeconds\":{realSeconds.ToString("0.0", CultureInfo.InvariantCulture)},")
                .Append($"\"FixedDt\":{_fixedDt.ToString("0.#####", CultureInfo.InvariantCulture)},");
            string json = JsonUtility.ToJson(report);
            Append(json.Length > 2 ? head + json.Substring(1) : head.Append("\"Empty\":true}").ToString());
        }

        void Append(string line)
        {
            try
            {
                var dir = Path.GetDirectoryName(_out);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(_out, line + "\n");
            }
            catch (Exception e) { CSDebug.LogWarning($"[ArkEscortSim] Could not write {_out}: {e.Message}"); }
        }

        static string Escape(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

        // The sim's output IS its deliverable: these lines are what a tuning session reads from the log.
        static void Log(string line) => CSDebug.LogWarning($"[ArkEscortSim] {line}");
    }
}
