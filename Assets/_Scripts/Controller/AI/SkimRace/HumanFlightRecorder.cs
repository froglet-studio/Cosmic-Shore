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
    /// L3 step 1; Docs/SKIM_RACE_AI.md §16). AUTOMATIC (Yash, 2026-10-15: "when I press Go the recording starts; when
    /// the Play Again menu comes or I stop the editor it stops; then I send it to Claude"): no button, no switch.
    ///
    /// <para>Every Regatta race the local player flies by hand is recorded from the moment the race starts until it
    /// ends (the Play Again menu), or until Play stops. Ten times a second: position, heading and up, speed, boost,
    /// the four stick axes and the throttle (the same input an AI writes), and rings threaded. Seconds the AI holds
    /// the hull (the You / AI toggle) are not recorded. At the end the pilot's own rail is copied from the arena
    /// (its prisms, their up axes, its contact-shell size), so <c>Tools/Build/learn_from_flights.py</c> measures the
    /// flight against the rail afterwards. Nothing during the race depends on the rail, so a race is never lost to it.</para>
    ///
    /// <para>One <c>flight_*.json</c> per race (schema <c>cosmic-shore.flight/2</c>) in
    /// <c>&lt;Diagnostics folder&gt;/Flights/</c>; Unity's after-play <b>Send to Claude</b> asks about it and carries it
    /// to the person's <c>bugledger/&lt;user&gt;</c> branch (<c>BugHuntReports</c> kind <c>flights</c>). A Regatta race
    /// that could not be saved says why in one Console warning. Editor and development builds only; nothing leaves the
    /// machine unless its owner presses Send. Players get their own consent question before any of this ships.</para>
    /// </summary>
    public sealed class HumanFlightRecorder : MonoBehaviour
    {
        public const string CommandName = "flightrec";
        public const string Schema = "cosmic-shore.flight/2";
        const float SampleHz = 10f;
        const float LookEvery = 0.5f;

        /// <summary>The flights folder: beside the diagnostics captures, so Send to Claude finds them.</summary>
        public static string Folder => Path.Combine(DiagnosticsHUD.OutputDirectory, "Flights");

        /// <summary>True while a race is being recorded.</summary>
        public static bool Recording => s_instance && s_instance._recording;

        /// <summary>What the recorder is doing, in words (HUD <c>flightrec</c>).</summary>
        public static string Status =>
            !Application.isPlaying ? "waiting for Play"
            : !s_instance ? "not started (no recorder object)"
            : s_instance._recording ? $"RECORDING {Time.time - s_instance._raceStart:0}s ({s_instance._samples.Count} samples)"
            : s_instance._waiting;

        /// <summary>The last file written.</summary>
        public static string LastFile { get; private set; }

        /// <summary>Flights saved in <see cref="Folder"/> (counted at most every two seconds).</summary>
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
        static HumanFlightRecorder s_instance;

        struct Sample
        {
            public float T, Speed, Boost, XSum, YSum, XDiff, YDiff, Throttle;
            public Vector3 Pos, Fwd, Up;
            public int Rings;
        }

        readonly List<Sample> _samples = new(2048);
        GameDataSO _gameData;
        Player _player;
        float _nextSample, _nextLook, _raceStart, _frameSum;
        int _frames, _startRings;
        bool _recording, _turnWasRunning;
        string _waiting = "waiting for a Regatta race";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            DiagnosticsHUD.RegisterCommand(CommandName, Handle);
            if (s_instance) return;
            // A plain (hidden-in-hierarchy) object: a DontSave one is not guaranteed its Update in every editor setup.
            var go = new GameObject("[Human Flight Recorder]") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<HumanFlightRecorder>();
        }

        /// <summary>The console command: what the recorder is doing, how many flights are saved, and where.</summary>
        public static string Handle(string[] args)
        {
            string last = string.IsNullOrEmpty(LastFile) ? "" : $" | last: {Path.GetFileName(LastFile)}";
            return $"flight recorder (automatic in Regatta): {Status} | {SavedCount} saved in {Folder}{last}";
        }

        void Update()
        {
            float now = Time.time;
            if (now >= _nextLook && !_recording)
            {
                _nextLook = now + LookEvery;
                Look();
            }
            if (!_gameData) return;

            bool running = _gameData.IsTurnRunning && _gameData.GameMode == GameModes.Regatta;
            if (!running)
            {
                if (_recording) Finish("race over");
                _turnWasRunning = false;
                return;
            }

            if (!_turnWasRunning)
            {
                _turnWasRunning = true;
                Begin(now);
            }
            if (!_recording)
            {
                if (now < _nextLook) return;
                _nextLook = now + LookEvery;
                if (!TryFindLocalHuman()) { _waiting = "waiting for your hull (AI-only races are not recorded)"; return; }
                _recording = true;
                _startRings = _player.RoundStats?.SwitchesThreaded ?? 0;
            }

            _frames++;
            _frameSum += Time.unscaledDeltaTime;
            if (now < _nextSample) return;
            _nextSample = now + 1f / SampleHz;
            TakeSample(now);
        }

        /// <summary>Find the match's game data: the asset whose local player is set (else one with players).</summary>
        void Look()
        {
            if (_gameData && _gameData.Players != null && _gameData.Players.Count > 0) return;
            _gameData = null;
            var all = Resources.FindObjectsOfTypeAll<GameDataSO>();
            for (int i = 0; i < all.Length && !_gameData; i++)
                if (all[i] && all[i].LocalPlayer != null) _gameData = all[i];
            for (int i = 0; i < all.Length && !_gameData; i++)
                if (all[i] && all[i].Players != null && all[i].Players.Count > 0) _gameData = all[i];
            _waiting = !_gameData ? "waiting for a match"
                : _gameData.GameMode != GameModes.Regatta ? $"waiting for Regatta (this is {_gameData.GameMode})"
                : "waiting for the race to start";
        }

        void Begin(float now)
        {
            _samples.Clear();
            _player = null;
            _recording = false;
            _frames = 0;
            _frameSum = 0f;
            _raceStart = now;
            _nextSample = now;
            _nextLook = now;
        }

        bool TryFindLocalHuman()
        {
            if (_gameData.LocalPlayer is Player local && local && !local.IsInitializedAsAI && local.Vessel != null)
            {
                _player = local;
                return true;
            }
            var players = _gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] is Player p && p && p.IsLocalUser && !p.IsInitializedAsAI && p.Vessel != null)
                {
                    _player = p;
                    return true;
                }
            }
            return false;
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
            _samples.Add(new Sample
            {
                T = now - _raceStart,
                Pos = tf.position, Fwd = tf.forward, Up = tf.up,
                Speed = status.Speed, Boost = status.BoostMultiplier,
                XSum = input.XSum, YSum = input.YSum, XDiff = input.XDiff, YDiff = input.YDiff,
                Throttle = input.Throttle,
                Rings = (_player.RoundStats?.SwitchesThreaded ?? 0) - _startRings,
            });
        }

        // Stopping Play mid-race (or quitting) writes the race so far as an unfinished flight.
        void OnApplicationQuit()
        {
            if (_recording) Finish("play stopped");
        }

        void OnDestroy()
        {
            if (_recording) Finish("play stopped");
            if (s_instance == this) s_instance = null;
        }

        void Finish(string why)
        {
            _recording = false;
            if (_samples.Count < 2 * SampleHz)
            {
                Debug.LogWarning($"[HumanFlightRecorder] Regatta race not saved ({why}): only {_samples.Count} hand-flown samples.");
                _samples.Clear();
                return;
            }
            try
            {
                Directory.CreateDirectory(Folder);
                string hull = _player && _player.Vessel?.VesselStatus != null ? _player.Vessel.VesselStatus.VesselType.ToString() : "Unknown";
                string file = Path.Combine(Folder, $"flight_Regatta_{hull}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                File.WriteAllText(file, Serialize(hull, why));
                LastFile = file;
                s_countAt = -1f;
                CSDebug.LogVerbose(CSLogChannel.AITraining, $"[HumanFlightRecorder] {_samples.Count} samples ({why}) -> {file}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HumanFlightRecorder] Regatta race not saved ({why}): {e.GetType().Name}: {e.Message}");
            }
            _samples.Clear();
        }

        /// <summary>The pilot's own rail, straight from the arena: the lane in the player's domain colour (else the
        /// first), each prism's position and up axis, and the contact shell's half-size (authored scale x 1.5).</summary>
        bool TryReadLane(List<Vector3> points, List<Vector3> ups, out Vector3 shellHalf, out string why)
        {
            shellHalf = Vector3.zero;
            var rails = FindAnyObjectByType<SpawnableRegattaRails>();
            if (!rails) { why = "no Regatta rails in the scene"; return false; }
            var trails = rails.GetTrails();
            if (trails == null || trails.Count == 0) { why = "the rails have no lanes"; return false; }
            Domains domain = _player ? _player.Domain : Domains.Jade;
            List<Prism> lane = null;
            for (int t = 0; t < trails.Count && lane == null; t++)
            {
                var list = trails[t]?.TrailList;
                if (list == null) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    if (!list[i]) continue;
                    if (list[i].Domain == domain) lane = list;
                    break;
                }
            }
            lane ??= trails[0]?.TrailList;
            if (lane == null || lane.Count < 3) { why = "your lane has no prisms"; return false; }
            for (int i = 0; i < lane.Count; i++)
            {
                var prism = lane[i];
                if (!prism) continue;
                var tr = prism.transform;
                points.Add(tr.position);
                ups.Add(tr.up);
                Vector3 scale = prism.TargetScale.sqrMagnitude > 1e-8f
                    ? (tr.parent ? Vector3.Scale(prism.TargetScale, tr.parent.lossyScale) : prism.TargetScale)
                    : tr.lossyScale;
                if (points.Count == 1) shellHalf = scale * SkimRaceShell.SuperShieldHalfScale;
            }
            why = null;
            return points.Count >= 3;
        }

        string Serialize(string hull, string why)
        {
            var c = CultureInfo.InvariantCulture;
            var lane = new List<Vector3>(2048);
            var ups = new List<Vector3>(2048);
            bool haveLane = TryReadLane(lane, ups, out Vector3 shellHalf, out string laneWhy);
            var sb = new StringBuilder(64 * 1024 + _samples.Count * 150);
            int target = _gameData ? _gameData.SwitchTargetCount : 0;
            int rings = _samples[_samples.Count - 1].Rings;
            int intensity = _gameData && _gameData.SelectedIntensity != null ? _gameData.SelectedIntensity.Value : 0;
            sb.Append("{\n");
            sb.Append("\"schema\":\"").Append(Schema).Append("\",\n");
            sb.Append("\"mode\":\"Regatta\",\"hull\":\"").Append(hull).Append("\",");
            sb.Append("\"domain\":\"").Append(_player ? _player.Domain.ToString() : "").Append("\",");
            sb.Append("\"intensity\":").Append(intensity).Append(',');
            sb.Append("\"date\":\"").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", c)).Append("\",");
            sb.Append("\"ended\":\"").Append(why).Append("\",");
            sb.Append("\"raceTarget\":").Append(target).Append(",\"rings\":").Append(rings).Append(',');
            sb.Append("\"finished\":").Append(target > 0 && rings >= target ? "true" : "false").Append(',');
            sb.Append("\"raceSeconds\":").Append(F(_samples[_samples.Count - 1].T)).Append(',');
            sb.Append("\"meanFrameMs\":").Append(F(_frames > 0 ? 1000f * _frameSum / _frames : 0f, "0.0")).Append(',');
            sb.Append("\"sampleHz\":").Append(F(SampleHz)).Append(',');
            sb.Append("\"referenceShellHeight\":").Append(F(SkimRaceCourse.ReferenceShellHeight, "0.###")).Append(',');
            sb.Append("\"shellHalf\":[").Append(F(shellHalf.x)).Append(',').Append(F(shellHalf.y)).Append(',').Append(F(shellHalf.z)).Append("],");
            sb.Append("\"laneError\":\"").Append(haveLane ? "" : laneWhy).Append("\",\n");

            sb.Append("\"lane\":[");
            for (int i = 0; i < lane.Count; i++)
            {
                if (i > 0) sb.Append(',');
                Vector3 p = lane[i], u = ups[i];
                sb.Append('[').Append(F(p.x)).Append(',').Append(F(p.y)).Append(',').Append(F(p.z)).Append(',')
                  .Append(F(u.x, "0.###")).Append(',').Append(F(u.y, "0.###")).Append(',').Append(F(u.z, "0.###")).Append(']');
            }
            sb.Append("],\n");

            sb.Append("\"columns\":[\"t\",\"x\",\"y\",\"z\",\"fx\",\"fy\",\"fz\",\"ux\",\"uy\",\"uz\",\"speed\",\"boost\",\"xSum\",\"ySum\",\"xDiff\",\"yDiff\",\"throttle\",\"rings\"],\n");
            sb.Append("\"samples\":[\n");
            for (int i = 0; i < _samples.Count; i++)
            {
                var m = _samples[i];
                if (i > 0) sb.Append(",\n");
                sb.Append('[').Append(F(m.T)).Append(',')
                  .Append(F(m.Pos.x)).Append(',').Append(F(m.Pos.y)).Append(',').Append(F(m.Pos.z)).Append(',')
                  .Append(F(m.Fwd.x, "0.###")).Append(',').Append(F(m.Fwd.y, "0.###")).Append(',').Append(F(m.Fwd.z, "0.###")).Append(',')
                  .Append(F(m.Up.x, "0.###")).Append(',').Append(F(m.Up.y, "0.###")).Append(',').Append(F(m.Up.z, "0.###")).Append(',')
                  .Append(F(m.Speed)).Append(',').Append(F(m.Boost)).Append(',')
                  .Append(F(m.XSum, "0.###")).Append(',').Append(F(m.YSum, "0.###")).Append(',')
                  .Append(F(m.XDiff, "0.###")).Append(',').Append(F(m.YDiff, "0.###")).Append(',')
                  .Append(F(m.Throttle, "0.###")).Append(',').Append(m.Rings).Append(']');
            }
            sb.Append("\n]\n}\n");
            if (!haveLane)
                Debug.LogWarning($"[HumanFlightRecorder] Regatta flight saved WITHOUT its rail ({laneWhy}); the learner cannot measure it against the rail.");
            return sb.ToString();
        }

        static string F(float v, string format = "0.##") =>
            float.IsNaN(v) || float.IsInfinity(v) ? "0" : v.ToString(format, CultureInfo.InvariantCulture);
    }
}
#endif
