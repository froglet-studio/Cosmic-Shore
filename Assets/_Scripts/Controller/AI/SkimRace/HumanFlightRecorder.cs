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
    /// The AI training recorder: records how a HUMAN flies, in every game mode and every vessel, so the AIs can learn
    /// from it (Docs/AI_SYSTEM/AI_TRAINING_PLAN.md L3; Docs/SKIM_RACE_AI.md §16). AUTOMATIC (Yash, 2026-10-15/16): no
    /// button. Recording starts when a match's turn starts (Go) and stops when it ends (the Play Again menu) or when Play
    /// stops. Single player for now; recording every online player's inputs is later.
    ///
    /// <para>Ten times a second, for the local player's own vessel while they fly it by hand: position, heading and
    /// up, speed, boost, the four stick axes and the throttle (the same input an AI writes), and rings threaded. Seconds
    /// the AI holds the hull (the You / AI toggle) are not recorded. Where the mode lays a rail (Regatta) the pilot's
    /// own rail is copied from the arena at the end (<see cref="RegattaCourseSource.TryResolveLiveRails"/>), so
    /// <c>Tools/Build/learn_from_flights.py</c> can measure the flight against it.</para>
    ///
    /// <para>One <c>flight_&lt;Mode&gt;_&lt;Vessel&gt;_&lt;time&gt;.json</c> per match (schema <c>cosmic-shore.flight/2</c>,
    /// with the mode, the vessel and the training type) in <c>&lt;Diagnostics folder&gt;/Flights/</c>. After Play, Unity
    /// asks "Store AI training record?" naming the vessel, the mode and the training type, and stores it on the
    /// person's <c>bugledger/&lt;user&gt;</c> branch (<c>BugHuntAutoSend</c>, <c>BugHuntReports</c> kind <c>flights</c>). A
    /// match that could not be stored says why in one Console warning. Editor and development builds only; nothing
    /// leaves the machine unless its owner presses Store. Players get their own consent question before any of this
    /// ships.</para>
    /// </summary>
    public sealed class HumanFlightRecorder : MonoBehaviour
    {
        public const string CommandName = "flightrec";
        public const string Schema = "cosmic-shore.flight/2";

        /// <summary>The kind of AI training a record serves, as Unity's "Store AI training record?" names it.</summary>
        public const string TrainingType = "imitation: learning from your flying";
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
        string _waiting = "waiting for a match";

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
            return $"AI training recorder (automatic, every mode): {Status} | {SavedCount} stored in {Folder}{last}";
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

            // Every game mode: an AI training record of whichever vessel the local player flies by hand.
            bool running = _gameData.IsTurnRunning;
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
            _waiting = !_gameData ? "waiting for a match" : $"waiting for the {_gameData.GameMode} race to start";
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
                Debug.LogWarning($"[HumanFlightRecorder] AI training record not stored ({why}): only {_samples.Count} hand-flown samples.");
                _samples.Clear();
                return;
            }
            try
            {
                Directory.CreateDirectory(Folder);
                string hull = _player && _player.Vessel?.VesselStatus != null ? _player.Vessel.VesselStatus.VesselType.ToString() : "Unknown";
                string mode = _gameData ? _gameData.GameMode.ToString() : "Unknown";
                string file = Path.Combine(Folder, $"flight_{mode}_{hull}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                File.WriteAllText(file, Serialize(hull, why));
                LastFile = file;
                s_countAt = -1f;
                CSDebug.LogVerbose(CSLogChannel.AITraining, $"[HumanFlightRecorder] {_samples.Count} samples ({why}) -> {file}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HumanFlightRecorder] AI training record not stored ({why}): {e.GetType().Name}: {e.Message}");
            }
            _samples.Clear();
        }

        /// <summary>The pilot's own rail, straight from the arena: the lane in the player's domain colour (else the
        /// first), each prism's position and up axis, and the contact shell's half-size (authored scale x 1.5).</summary>
        bool TryReadLane(List<Vector3> points, List<Vector3> ups, out Vector3 shellHalf, out string why)
        {
            shellHalf = Vector3.zero;
            if (!RegattaCourseSource.TryResolveLiveRails(out var rails, out why)) return false;
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
            // A rail to measure the flight against exists only where the mode lays one (Regatta, for now).
            bool regatta = _gameData && _gameData.GameMode == GameModes.Regatta;
            Vector3 shellHalf = Vector3.zero;
            string laneWhy = "";
            bool haveLane = regatta && TryReadLane(lane, ups, out shellHalf, out laneWhy);
            var sb = new StringBuilder(64 * 1024 + _samples.Count * 150);
            int target = _gameData ? _gameData.SwitchTargetCount : 0;
            int rings = _samples[_samples.Count - 1].Rings;
            int intensity = _gameData && _gameData.SelectedIntensity != null ? _gameData.SelectedIntensity.Value : 0;
            sb.Append("{\n");
            sb.Append("\"schema\":\"").Append(Schema).Append("\",\n");
            sb.Append("\"mode\":\"").Append(_gameData ? _gameData.GameMode.ToString() : "Unknown").Append("\",\"hull\":\"").Append(hull).Append("\",");
            sb.Append("\"training\":\"").Append(TrainingType).Append("\",");
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
            if (regatta && !haveLane)
                Debug.LogWarning($"[HumanFlightRecorder] Regatta record stored WITHOUT its rail ({laneWhy}); the learner cannot measure it against the rail.");
            return sb.ToString();
        }

        static string F(float v, string format = "0.##") =>
            float.IsNaN(v) || float.IsInfinity(v) ? "0" : v.ToString(format, CultureInfo.InvariantCulture);
    }
}
#endif
