#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CosmicShore.Data;
using CosmicShore.Utility;
using CosmicShore.Utility.PerformanceBenchmark;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Records a HUMAN's Regatta flight so the Squirrel AI can learn from it (Docs/AI_SYSTEM/AI_TRAINING_PLAN.md
    /// L3 step 1; Yash, 2026-10-15: "I play Regatta five times with the Squirrel and the AI reads that data").
    ///
    /// <para>While it is on (HUD console <c>flightrec on</c>, or the Third Eye's Record toggle) and the local
    /// player flies a race by hand, it samples ten times a second: position, heading, speed, boost, the four stick
    /// axes and the throttle (the same input an AI writes - the input-only contract makes the two comparable), rings
    /// threaded, and the flight's place against the pilot's OWN rail, measured with the Skim Race pilot's course
    /// model (<see cref="RegattaRingObjective"/>): progress along it, height above it, sideways offset, and the gap
    /// to its contact shell. Seconds the AI held the hull (the You / AI toggle) are not recorded.</para>
    ///
    /// <para>At the end of the race it writes one <c>flight_*.json</c> (schema <c>cosmic-shore.flight/1</c>) to
    /// <c>&lt;Diagnostics folder&gt;/Flights/</c>; Unity's after-play <b>Send to Claude</b> carries it to the
    /// person's <c>bugledger/&lt;user&gt;</c> branch beside the perf captures (<c>BugHuntReports</c>, kind
    /// <c>flights</c>). <c>Tools/Build/learn_from_flights.py</c> reads the files and turns them into the Regatta
    /// Squirrel policy (<c>SkimRaceAIConfig_Regatta</c>).</para>
    ///
    /// <para>Editor and development builds only, and off until switched on: it records nobody who did not ask.
    /// Nothing leaves the machine unless its owner presses Send. Players get their own consent question before
    /// any of this ships (AI_TRAINING_PLAN.md L3 step 2).</para>
    /// </summary>
    public sealed class HumanFlightRecorder : MonoBehaviour
    {
        public const string CommandName = "flightrec";
        public const string Schema = "cosmic-shore.flight/1";
        const string PrefKey = "CosmicShore.HumanFlightRecorder.On";
        const float SampleHz = 10f;
        const float FindPlayerEvery = 1f;

        /// <summary>Recording switched on (remembered on this machine).</summary>
        public static bool On
        {
            get => PlayerPrefs.GetInt(PrefKey, 0) == 1;
            set { PlayerPrefs.SetInt(PrefKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>The flights folder: beside the diagnostics captures, so Send to Claude finds them.</summary>
        public static string Folder => Path.Combine(DiagnosticsHUD.OutputDirectory, "Flights");

        /// <summary>True while a race is being recorded.</summary>
        public static bool Recording => s_instance && s_instance._course != null && s_instance._samples.Count > 0;

        /// <summary>Flights saved in <see cref="Folder"/> (counted at most every two seconds), for the Third Eye.</summary>
        public static int SavedCount
        {
            get
            {
                float now = Time.realtimeSinceStartup;
                if (now < s_countAt) return s_count;
                s_countAt = now + 2f;
                try { s_count = Directory.Exists(Folder) ? Directory.GetFiles(Folder, "flight_*.json").Length : 0; }
                catch (Exception) { s_count = 0; }
                return s_count;
            }
        }
        static int s_count;
        static float s_countAt = -1f;

        /// <summary>The last file written, for the console and the Third Eye.</summary>
        public static string LastFile { get; private set; }

        static HumanFlightRecorder s_instance;

        struct Sample
        {
            public float T, Speed, Boost, XSum, YSum, XDiff, YDiff, Throttle, S, Height, Lateral, Gap;
            public Vector3 Pos, Fwd;
            public int Rings;
        }

        readonly List<Sample> _samples = new(2048);
        GameDataSO _gameData;
        Player _player;
        RegattaRingObjective _objective;
        SkimRaceCourse _course;
        float _nextSample, _nextFind, _raceStart, _frameSum;
        int _frames, _hint = -1, _startRings;
        bool _turnWasRunning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            DiagnosticsHUD.RegisterCommand(CommandName, Handle);
            if (s_instance) return;
            var go = new GameObject("[Human Flight Recorder]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<HumanFlightRecorder>();
        }

        /// <summary>The console command: <c>flightrec</c> reports, <c>flightrec on | off</c> switches it.</summary>
        public static string Handle(string[] args)
        {
            string verb = args is { Length: > 0 } ? args[0].ToLowerInvariant() : "";
            if (verb == "on") On = true;
            else if (verb == "off") On = false;
            else if (verb != "") return $"usage: {CommandName} on | off";
            string state = On ? "ON: your Regatta races are recorded" : "off";
            string last = string.IsNullOrEmpty(LastFile) ? "" : $" | last: {LastFile}";
            return $"flight recorder {state} | {SavedCount} saved in {Folder}{last}";
        }

        void Update()
        {
            if (!On) { if (_course != null) Finish("recording switched off"); return; }
            float now = Time.time;
            if (!_gameData && now >= _nextFind) FindGameData(now);
            if (!_gameData) return;

            bool running = _gameData.IsTurnRunning && _gameData.GameMode == GameModes.Regatta;
            if (!running)
            {
                if (_turnWasRunning && _course != null) Finish("race over");
                _turnWasRunning = false;
                return;
            }
            if (!_turnWasRunning)
            {
                _turnWasRunning = true;
                Begin(now);
            }
            if (_course == null && !TryEnsureCourse(now)) return;

            _frames++;
            _frameSum += Time.unscaledDeltaTime;
            if (now < _nextSample) return;
            _nextSample = now + 1f / SampleHz;
            TakeSample(now);
        }

        void FindGameData(float now)
        {
            _nextFind = now + FindPlayerEvery;
            var all = Resources.FindObjectsOfTypeAll<GameDataSO>();
            for (int i = 0; i < all.Length; i++)
                if (all[i] && all[i].Players != null && all[i].Players.Count > 0) { _gameData = all[i]; return; }
        }

        void Begin(float now)
        {
            _samples.Clear();
            _course = null;
            _player = null;
            _hint = -1;
            _frames = 0;
            _frameSum = 0f;
            _raceStart = now;
            _nextSample = now;
            _objective = new RegattaRingObjective(_gameData);
        }

        bool TryEnsureCourse(float now)
        {
            if (now < _nextFind) return false;
            _nextFind = now + 0.5f;
            if (!_player) _player = LocalHuman();
            if (!_player || _player.Vessel?.VesselStatus == null) return false;
            if (!_objective.TryBuildCourse(_player.Vessel.VesselStatus, out var course)) return false;
            _course = course;
            _startRings = _player.RoundStats?.SwitchesThreaded ?? 0;
            return true;
        }

        Player LocalHuman()
        {
            var players = _gameData.Players;
            for (int i = 0; i < players.Count; i++)
                if (players[i] is Player p && p && p.IsLocalUser && !p.IsInitializedAsAI && p.Vessel != null)
                    return p;
            return null;
        }

        void TakeSample(float now)
        {
            if (!_player || _player.Vessel?.VesselStatus == null) return;
            // The AI holding this hull (the Third Eye's You / AI toggle) is not the human's flight.
            if (PilotControl.IsHeldByAI(_player)) return;
            var status = _player.Vessel.VesselStatus;
            var input = status.InputStatus;
            var tf = _player.Vessel.Transform;
            if (input == null || !tf) return;

            Vector3 pos = tf.position;
            float s = _course.Project(pos, ref _hint, out _, out _);
            Vector3 p = _course.Sample(s, out Vector3 tangent, out Vector3 normal);
            Vector3 lateralAxis = Vector3.Cross(normal, tangent);
            Vector3 rel = pos - p;
            float gap = _course.ShellClearance(pos, _hint, 6, out _);

            _samples.Add(new Sample
            {
                T = now - _raceStart,
                Pos = pos,
                Fwd = tf.forward,
                Speed = status.Speed,
                Boost = status.BoostMultiplier,
                XSum = input.XSum, YSum = input.YSum, XDiff = input.XDiff, YDiff = input.YDiff,
                Throttle = input.Throttle,
                Rings = (_player.RoundStats?.SwitchesThreaded ?? 0) - _startRings,
                S = s,
                Height = Vector3.Dot(rel, normal),
                Lateral = lateralAxis.sqrMagnitude > 1e-6f ? Vector3.Dot(rel, lateralAxis.normalized) : 0f,
                Gap = float.IsInfinity(gap) ? -1f : gap,
            });
        }

        void Finish(string why)
        {
            var course = _course;
            _course = null;
            if (course == null || _samples.Count < 2 * SampleHz) { _samples.Clear(); return; }
            try
            {
                Directory.CreateDirectory(Folder);
                string hull = _player && _player.Vessel?.VesselStatus != null ? _player.Vessel.VesselStatus.VesselType.ToString() : "Unknown";
                string file = Path.Combine(Folder, $"flight_Regatta_{hull}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                File.WriteAllText(file, Serialize(course, hull, why));
                LastFile = file;
                s_countAt = -1f;
                CSDebug.LogVerbose(CSLogChannel.AITraining, $"[HumanFlightRecorder] {_samples.Count} samples ({why}) -> {file}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HumanFlightRecorder] could not write the flight: {e.Message}");
            }
            _samples.Clear();
        }

        string Serialize(SkimRaceCourse course, string hull, string why)
        {
            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(64 * 1024 + _samples.Count * 140);
            int target = _gameData.SwitchTargetCount;
            int rings = _samples[_samples.Count - 1].Rings;
            int intensity = _gameData.SelectedIntensity != null ? _gameData.SelectedIntensity.Value : 0;
            sb.Append("{\n");
            sb.Append("\"schema\":\"").Append(Schema).Append("\",\n");
            sb.Append("\"mode\":\"Regatta\",\"hull\":\"").Append(hull).Append("\",");
            sb.Append("\"domain\":\"").Append(_player ? _player.Domain.ToString() : "").Append("\",");
            sb.Append("\"intensity\":").Append(intensity).Append(',');
            sb.Append("\"date\":\"").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", c)).Append("\",");
            sb.Append("\"ended\":\"").Append(why).Append("\",");
            sb.Append("\"raceTarget\":").Append(target).Append(",\"rings\":").Append(rings).Append(',');
            sb.Append("\"finished\":").Append(target > 0 && rings >= target ? "true" : "false").Append(',');
            sb.Append("\"raceSeconds\":").Append(_samples[_samples.Count - 1].T.ToString("0.00", c)).Append(',');
            sb.Append("\"meanFrameMs\":").Append((_frames > 0 ? 1000f * _frameSum / _frames : 0f).ToString("0.0", c)).Append(',');
            sb.Append("\"sampleHz\":").Append(SampleHz.ToString(c)).Append(',');
            sb.Append("\"skimLift\":").Append(course.SkimLift.ToString("0.###", c)).Append(',');
            sb.Append("\"referenceShellHeight\":").Append(SkimRaceCourse.ReferenceShellHeight.ToString("0.###", c)).Append(',');
            sb.Append("\"courseLength\":").Append(course.Length.ToString("0.0", c)).Append(",\n");

            // The pilot's own rail: the points and their up axes, so the reader can measure lines against it.
            sb.Append("\"lane\":[");
            for (int i = 0; i < course.Count; i++)
            {
                if (i > 0) sb.Append(',');
                Vector3 pt = course.PointAtIndex(i), up = course.PrismUp(i);
                sb.Append('[').Append(F(pt.x)).Append(',').Append(F(pt.y)).Append(',').Append(F(pt.z)).Append(',')
                  .Append(F(up.x, "0.###")).Append(',').Append(F(up.y, "0.###")).Append(',').Append(F(up.z, "0.###")).Append(']');
            }
            sb.Append("],\n");

            sb.Append("\"columns\":[\"t\",\"x\",\"y\",\"z\",\"fx\",\"fy\",\"fz\",\"speed\",\"boost\",\"xSum\",\"ySum\",\"xDiff\",\"yDiff\",\"throttle\",\"rings\",\"s\",\"height\",\"lateral\",\"gap\"],\n");
            sb.Append("\"samples\":[\n");
            for (int i = 0; i < _samples.Count; i++)
            {
                var m = _samples[i];
                if (i > 0) sb.Append(",\n");
                sb.Append('[').Append(F(m.T)).Append(',')
                  .Append(F(m.Pos.x)).Append(',').Append(F(m.Pos.y)).Append(',').Append(F(m.Pos.z)).Append(',')
                  .Append(F(m.Fwd.x, "0.###")).Append(',').Append(F(m.Fwd.y, "0.###")).Append(',').Append(F(m.Fwd.z, "0.###")).Append(',')
                  .Append(F(m.Speed)).Append(',').Append(F(m.Boost)).Append(',')
                  .Append(F(m.XSum, "0.###")).Append(',').Append(F(m.YSum, "0.###")).Append(',')
                  .Append(F(m.XDiff, "0.###")).Append(',').Append(F(m.YDiff, "0.###")).Append(',')
                  .Append(F(m.Throttle, "0.###")).Append(',').Append(m.Rings).Append(',')
                  .Append(F(m.S)).Append(',').Append(F(m.Height)).Append(',').Append(F(m.Lateral)).Append(',').Append(F(m.Gap)).Append(']');
            }
            sb.Append("\n]\n}\n");
            return sb.ToString();
        }

        static string F(float v, string format = "0.##") =>
            float.IsNaN(v) || float.IsInfinity(v) ? "0" : v.ToString(format, CultureInfo.InvariantCulture);

        // Stopping Play mid-race (or quitting) must not throw the race away: it is written as an unfinished
        // flight, which the learner ranks below finished ones. Before this, a race stopped short wrote nothing,
        // so there was no file for Send to Claude to carry and no reason for Unity to ask (Yash, 2026-10-15).
        void OnApplicationQuit()
        {
            if (_course != null) Finish("play stopped");
        }

        void OnDestroy()
        {
            if (_course != null) Finish("play stopped");
            if (s_instance == this) s_instance = null;
        }
    }
}
#endif
