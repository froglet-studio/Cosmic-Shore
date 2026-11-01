using System;
using System.Collections.Generic;
using System.IO;
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
    /// started with <c>-arkEscortSim</c>, which the editor launcher (<c>ArkEscortSimLauncher</c>) passes.
    ///
    /// <para>Arguments: <c>-arkEscortSim "5:Sparrow,Rhino,Manta;3:Squirrel,Dolphin,Serpent"</c> (difficulty:three vessels,
    /// runs separated by ';'), <c>-arkEscortOut path.jsonl</c>, <c>-arkEscortTimeScale 3</c>, and
    /// <c>-arkEscortParams path.json</c>: a flat JSON object of named overrides (<c>{"ark.crawlSpeed": 18,
    /// "tier.4.population": 0.7}</c>) every run applies to its runtime copy of the escort (<see cref="ArkEscortParams"/>);
    /// each run's JSON line echoes them (<c>Params</c>) and lists the accepted key patterns (<c>ParamKeys</c>).</para>
    /// </summary>
    public sealed class ArkEscortSim : MonoBehaviour
    {
        const string PlanArg = "-arkEscortSim";
        const string OutArg = "-arkEscortOut";
        const string ScaleArg = "-arkEscortTimeScale";
        const string ParamsArg = "-arkEscortParams";
        /// <summary><c>-arkEscortVerbose</c>: the ToyBox verbose channel into the editor log, so a run's END REASON
        /// ("[ArkEscort] Ended: ...", "[Arkway] Voyage ended: ...") is in the sim log instead of silent.</summary>
        const string VerboseArg = "-arkEscortVerbose";
        const float ReadyTimeoutSeconds = 600f;
        const float RunTimeoutSeconds = 1800f;

        /// <summary>Raised once every planned run has finished (or the sim gave up): the editor launcher exits on it.</summary>
        public static event Action<bool> AllDone;

        /// <summary>True while the sim is driving this process.</summary>
        public static bool IsRunning { get; private set; }

        readonly List<(int difficulty, SavedSquad squad, string name)> _plan = new();
        string _out;
        float _scale = 1f;
        ArkEscortReport _last;

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
            if (float.TryParse(Arg(args, ScaleArg), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float s)) sim._scale = Mathf.Clamp(s, 0.25f, 8f);
            sim.Parse(plan);
            sim.LoadParams(Arg(args, ParamsArg));
            if (Array.IndexOf(args, VerboseArg) >= 0) CSDebug.VerboseChannels |= CSLogChannel.ToyBox;
            IsRunning = true;
            string shots = Arg(args, ArkEscortShots.ShotsArg);
            if (!string.IsNullOrEmpty(shots))
                ArkEscortShots.Install(shots, Array.IndexOf(args, ArkEscortShots.ShotsOnlyArg) >= 0);
            sim.RunAsync(sim.GetCancellationTokenOnDestroy()).Forget();
        }

        static string Arg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

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

        void Parse(string plan)
        {
            foreach (var run in plan.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = run.Split(':');
                if (parts.Length != 2 || !int.TryParse(parts[0], out int d)) { Log($"bad run '{run}'"); continue; }
                var vessels = parts[1].Split(',');
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
                if (!ok) { Log($"bad squad '{parts[1]}'"); continue; }
                _plan.Add((d, new SavedSquad("escort sim", slots[0], slots[1], slots[2]), parts[1]));
            }
        }

        async UniTaskVoid RunAsync(CancellationToken ct)
        {
            bool ok = false;
            try
            {
                Log($"plan: {_plan.Count} runs, time scale {_scale}, out {_out}");
                var harbour = await ReadyAsync(ct);
                if (!harbour) { Log("never ready - giving up"); return; }

                ArkEscortRun.Finished += OnFinished;
                for (int i = 0; i < _plan.Count; i++)
                {
                    var (difficulty, squad, name) = _plan[i];
                    harbour = VoyageHarbour.Current;
                    if (!harbour) { Log("the harbour went away"); break; }
                    Log($"run {i + 1}/{_plan.Count}: d{difficulty} {name}");
                    _last = null;
                    var freestyle = FindAnyObjectByType<MenuCrystalClickHandler>(FindObjectsInactive.Include);
                    if (freestyle && !freestyle.IsInFreestyle && !freestyle.IsTransitioning) freestyle.ToggleTransition();
                    float settle = Time.unscaledTime + 60f;
                    while (freestyle && (!freestyle.IsInFreestyle || freestyle.IsTransitioning) && Time.unscaledTime < settle)
                        await UniTask.Delay(500, DelayType.Realtime, cancellationToken: ct);
                    harbour = VoyageHarbour.Current;
                    if (!harbour) { Log("the harbour went away entering freestyle"); break; }
                    harbour.SetSailEscort(difficulty, freestyle, squad, aiLocal: true);

                    float deadline = Time.unscaledTime + RunTimeoutSeconds;
                    while (_last == null && Time.unscaledTime < deadline)
                    {
                        Time.timeScale = _scale;
                        await UniTask.Delay(1000, DelayType.Realtime, cancellationToken: ct);
                        harbour = VoyageHarbour.Current;
                        if (harbour && !harbour.IsEscorting && _last == null && Time.unscaledTime > deadline - RunTimeoutSeconds + 60f)
                        {
                            Log("the escort is not out (did not sail, or ended unreported)");
                            break;
                        }
                    }
                    Time.timeScale = 1f;
                    if (_last == null)
                    {
                        Log("no report (timed out or never sailed)");
                        Write(new ArkEscortReport { Difficulty = difficulty, Squad = name, Result = ArkEscortReport.Outcome.None });
                    }
                    else Write(_last);

                    // Let the corridor drain before the next run stands its own.
                    await UniTask.Delay(8000, DelayType.Realtime, cancellationToken: ct);
                }
                ok = true;
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Log($"threw: {e}"); }
            finally
            {
                ArkEscortRun.Finished -= OnFinished;
                ArkEscortParams.Pending = null;
                Time.timeScale = 1f;
                IsRunning = false;
                Log(ok ? "done" : "stopped");
                AllDone?.Invoke(ok);
            }
        }

        /// <summary>The game is in the menu with a vessel and a harbour; enters freestyle if the harbour waits for it.</summary>
        async UniTask<VoyageHarbour> ReadyAsync(CancellationToken ct)
        {
            float deadline = Time.unscaledTime + ReadyTimeoutSeconds;
            bool asked = false;
            while (Time.unscaledTime < deadline)
            {
                var freestyle = FindAnyObjectByType<MenuCrystalClickHandler>(FindObjectsInactive.Include);
                var harbour = VoyageHarbour.Current;
                if (harbour && freestyle)
                {
                    // Into freestyle here, with all the time it needs (a first play in the editor imports as it goes, and
                    // the harbour's own hand-off waits only a few seconds); the escort then sails straight from it.
                    await UniTask.Delay(5000, DelayType.Realtime, cancellationToken: ct);
                    if (!freestyle.IsInFreestyle && !freestyle.IsTransitioning) freestyle.ToggleTransition();
                    while ((!freestyle.IsInFreestyle || freestyle.IsTransitioning) && Time.unscaledTime < deadline)
                        await UniTask.Delay(500, DelayType.Realtime, cancellationToken: ct);
                    await UniTask.Delay(3000, DelayType.Realtime, cancellationToken: ct);
                    return VoyageHarbour.Current;
                }
                if (!harbour && freestyle && !asked && !freestyle.IsTransitioning && !freestyle.IsInFreestyle)
                {
                    // The harbour stands with the toybox; on a build where the toybox waits for freestyle, ask for it.
                    if (Time.unscaledTime > deadline - ReadyTimeoutSeconds + 30f)
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

        void OnFinished(ArkEscortReport report) => _last = report;

        void Write(ArkEscortReport report)
        {
            Log(report.Summary());
            try
            {
                var dir = Path.GetDirectoryName(_out);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(_out, JsonUtility.ToJson(report) + "\n");
            }
            catch (Exception e) { CSDebug.LogWarning($"[ArkEscortSim] Could not write {_out}: {e.Message}"); }
        }

        // The sim's output IS its deliverable: these lines are what a tuning session reads from the log.
        static void Log(string line) => CSDebug.LogWarning($"[ArkEscortSim] {line}");
    }
}
