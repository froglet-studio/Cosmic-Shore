using System;
using System.Collections.Generic;
using System.IO;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Measures one Skim Race from the GAME'S OWN state and nothing else — the benchmark's
    /// referee. It never touches the race; it only reads:
    /// <list type="bullet">
    /// <item><b>Start</b>: <c>GameDataSO.IsTurnRunning</c> rising (the countdown ending, the same
    /// instant <c>SkimRaceScoreTracker</c> starts the race clock).</item>
    /// <item><b>Collections</b>: each seat's authoritative <c>RoundStats.CrystalsCollected</c>,
    /// timestamped the frame it changes.</item>
    /// <item><b>Finish</b>: the server's own result — <c>GameDataSO.WinnerDomain</c> and the winning
    /// seats' <c>Score</c>, which <c>SkimRaceController</c> sets to the race time when a domain's
    /// summed crystals reach <c>CrystalTargetCount</c>. A race is a SUCCESS only if that happened
    /// for the AI's domain, the domain's collected count reached the target, and the finish
    /// time is at or under the benchmark limit.</item>
    /// </list>
    /// One JSON object per race is appended to the session file.
    /// </summary>
    public class SkimRaceRaceRecorder : MonoBehaviour
    {
        [Serializable]
        public class SeatRecord
        {
            public string name;
            public bool isAI;
            public string domain;
            public int crystals;
            public float score;
            public List<float> collectionTimes = new();
            public string policy = "";
            public string difficulty = "";   // the lobby AI difficulty this AI seat flew
            public int mistakes;             // crystals it misjudged on purpose (Easy / Medium)
            public int recoveries;
            public int speedLossEvents;
            public int stallEvents;
            public float maxSpeed;
            public float meanSpeed;
        }

        [Serializable]
        public class RaceRecord
        {
            public string session;
            public int raceIndex;
            public string commit;
            public string build;
            public string scene;
            public int intensity;
            public int trackSeed;
            public int requiredCrystals;
            public float benchmarkLimitSeconds;
            public string startedUtc;
            public bool finished;
            public string winnerDomain;
            public float authoritativeFinishTime;
            public float measuredRaceTime;
            public float meanFrameMs;
            public float maxFrameMs;
            public string quality;      // graphics quality level the race rendered at (frame rate affects every pilot)
            public float timeScaleMin = 1f, timeScaleMax = 1f;  // anything but 1 invalidates the race
            public string aiDomain;
            public int aiDomainCrystals;
            public bool success;
            public string failureReason = "";
            public List<SeatRecord> seats = new();
        }

        GameDataSO _gameData;
        string _filePath;
        string _session;
        string _commit;
        int _raceIndex;
        float _limit;
        float _timeoutSeconds;

        bool _running;
        bool _written;
        float _start;
        RaceRecord _race;
        readonly Dictionary<string, int> _lastCount = new();
        readonly Dictionary<string, SkimRacePilot> _pilots = new();
        readonly Dictionary<string, float> _speedSum = new();
        readonly Dictionary<string, int> _speedSamples = new();
        readonly Dictionary<string, float> _speedWindow = new();
        readonly Dictionary<string, float> _lastBoost = new();
        readonly System.Text.StringBuilder _hits = new();
        readonly Collider[] _hitScratch = new Collider[32];

        /// <summary>Also write a 10 Hz per-AI-seat trace (CSV) beside the results file.</summary>
        public bool TraceFrames;
        // Each seat's record by player name: SampleSeats runs every frame for every player, and a
        // List.Find with a lambda there allocated a closure per player per frame.
        readonly Dictionary<string, SeatRecord> _seatByName = new();
        float _nextTrace;
        readonly System.Text.StringBuilder _trace = new();

        public bool Finished => _written;
        float _frameSum, _frameMax;
        int _frames;
        public RaceRecord Record => _race;

        public event Action<RaceRecord> RaceRecorded;

        public void Configure(GameDataSO gameData, string filePath, string session, string commit,
            int raceIndex, float limitSeconds, float timeoutSeconds)
        {
            _gameData = gameData;
            _filePath = filePath;
            _session = session;
            _commit = commit;
            _raceIndex = raceIndex;
            _limit = limitSeconds;
            _timeoutSeconds = timeoutSeconds;
        }

        void Update()
        {
            if (_gameData == null || _written) return;

            if (!_running)
            {
                if (_gameData.IsTurnRunning) BeginRace();
                return;
            }

            float now = Time.time - _start;
            _frameSum += Time.unscaledDeltaTime;
            _frames++;
            _frameMax = Mathf.Max(_frameMax, Time.unscaledDeltaTime);
            _race.timeScaleMin = Mathf.Min(_race.timeScaleMin, Time.timeScale);
            _race.timeScaleMax = Mathf.Max(_race.timeScaleMax, Time.timeScale);
            SampleSeats(now);

            bool ended = !string.IsNullOrEmpty(_gameData.WinnerName) || (_gameData.WinnerDomain != Domains.Blue && !_gameData.IsTurnRunning);
            if (ended) { FinishRace(now, null); return; }
            if (now > _timeoutSeconds) FinishRace(now, $"timeout: no domain reached the target within {_timeoutSeconds:F0}s");
        }

        void BeginRace()
        {
            _running = true;
            _start = Time.time;
            _race = new RaceRecord
            {
                session = _session,
                raceIndex = _raceIndex,
                commit = _commit,
                build = Application.isEditor ? $"editor {Application.unityVersion}" : $"player {Application.version}",
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                intensity = _gameData.SelectedIntensity != null ? _gameData.SelectedIntensity.Value : 0,
                trackSeed = FindAnyObjectByType<SkimRaceController>() is { } ctl ? ctl.TrackSeed : 0,
                benchmarkLimitSeconds = _limit,
                startedUtc = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
            };

            _seatByName.Clear();
            foreach (var p in _gameData.Players)
            {
                if (p == null) continue;
                var seat = new SeatRecord { name = p.Name, isAI = p.IsInitializedAsAI, domain = p.Domain.ToString() };
                _race.seats.Add(seat);
                _seatByName[p.Name] = seat;
                _lastCount[p.Name] = p.RoundStats != null ? p.RoundStats.CrystalsCollected : 0;
                var go = p.Vessel?.Transform != null ? p.Vessel.Transform.gameObject : null;
                var pilot = go != null ? go.GetComponent<SkimRacePilot>() : null;
                if (pilot != null)
                {
                    if (TraceFrames && _pilots.Count == 0) WriteProbe(p, pilot);
                    _pilots[p.Name] = pilot;
                    seat.policy = pilot.Config != null ? pilot.Config.PolicyVersion : "";
                    seat.difficulty = AIDifficultyRules.Resolve(_gameData.RequestedAIDifficulty).ToString();
                    if (string.IsNullOrEmpty(_race.aiDomain)) _race.aiDomain = p.Domain.ToString();
                }
            }
        }

        void SampleSeats(float now)
        {
            if (_race.requiredCrystals == 0) _race.requiredCrystals = _gameData.CrystalTargetCount;

            foreach (var p in _gameData.Players)
            {
                if (p == null) continue;
                if (!_seatByName.TryGetValue(p.Name, out var seat)) continue;
                int c = p.RoundStats != null ? p.RoundStats.CrystalsCollected : 0;
                if (_lastCount.TryGetValue(p.Name, out int last) && c > last)
                    for (int k = last; k < c; k++) seat.collectionTimes.Add(now);
                _lastCount[p.Name] = c;
                seat.crystals = c;

                var st = p.Vessel?.VesselStatus;
                if (st == null) continue;
                float spd = st.Speed;
                seat.maxSpeed = Mathf.Max(seat.maxSpeed, spd);
                _speedSum[p.Name] = (_speedSum.TryGetValue(p.Name, out var ss) ? ss : 0f) + spd;
                _speedSamples[p.Name] = (_speedSamples.TryGetValue(p.Name, out var sn) ? sn : 0) + 1;

                // Speed-loss events: a sudden >25% drop while the stick asks for full throttle is a
                // collision with track mass (the hull's prism-impact slow), not a deliberate brake.
                float prev = _speedWindow.TryGetValue(p.Name, out var pw) ? pw : spd;
                if (spd < prev * 0.75f && prev > 80f && st.InputStatus != null && st.InputStatus.XDiff > 0.9f)
                    seat.speedLossEvents++;
                _speedWindow[p.Name] = Mathf.Lerp(prev, spd, 0.25f);

                if (_pilots.TryGetValue(p.Name, out var pilot) && pilot != null && pilot.Driver != null)
                {
                    seat.recoveries = pilot.Driver.Recoveries;
                    seat.mistakes = pilot.Driver.Handicap != null ? pilot.Driver.Handicap.Mistakes : 0;
                    if (TraceFrames) NoteBoostReset(now, p, st, c, pilot);
                    if (TraceFrames && now >= _nextTrace) AppendTrace(now, p.Name, pilot);
                }
            }
        }

        // Diagnostic: the frame the AI's skim boost collapses to 1x without a pickup is a hull
        // contact (VesselResetBoostPrismEffect). Name what was in reach so the simulator's contact
        // model can be checked against the game's.
        void NoteBoostReset(float now, IPlayer p, IVesselStatus st, int crystals, SkimRacePilot pilot)
        {
            float boost = st.IsBoosting ? st.BoostMultiplier : 1f;
            float prev = _lastBoost.TryGetValue(p.Name, out var pb) ? pb : boost;
            _lastBoost[p.Name] = boost;
            if (!(prev - boost > 1f && boost < 1.5f)) return;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            Vector3 pos = p.Vessel.Transform.position;
            float clear = -1f;
            if (pilot.Course != null && pilot.Course.HasShells)
            {
                int h = -1;
                pilot.Course.Project(pos, ref h, out _, out _);
                clear = pilot.Course.ShellClearance(pos, h, 8, out _);
            }
            _hits.Append(string.Format(inv, "{0:F2},{1:F1},{2:F1},{3:F1},{4:F2},{5},{6:F2}", now, pos.x, pos.y, pos.z, prev, crystals, clear));
            int n = Physics.OverlapSphereNonAlloc(pos, 12f, _hitScratch, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var prism = _hitScratch[i] != null ? _hitScratch[i].GetComponentInParent<Prism>() : null;
                if (prism == null) continue;
                _hits.Append(string.Format(inv, ",[{0} kind={1} d={2:F1} sz={3}]", prism.ownerID,
                    PrismKinds.Of(prism), Vector3.Distance(pos, prism.transform.position), prism.transform.lossyScale.ToString("F1")));
            }
            _hits.Append('\n');
        }

        /// <summary>One-off physical measurements for the offline simulator's calibration.</summary>
        void WriteProbe(IPlayer p, SkimRacePilot pilot)
        {
            try
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                var sb = new System.Text.StringBuilder();
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath));
                var vt = p.Vessel.Transform;
                if (pilot != null && pilot.Config != null)
                    sb.AppendLine("policy " + pilot.Config.name + " " + JsonUtility.ToJson(pilot.Config));
                sb.AppendLine($"vesselPos {vt.position.ToString("F2")} fwd {vt.forward.ToString("F3")} up {vt.up.ToString("F3")}");
                foreach (var col in vt.GetComponentsInChildren<Collider>(true))
                    sb.AppendLine($"collider {col.GetType().Name} '{col.name}' trigger={col.isTrigger} enabled={col.enabled} " +
                                  $"layer={LayerMask.LayerToName(col.gameObject.layer)} boundsSize={col.bounds.size.ToString("F2")} " +
                                  $"center={(col.bounds.center - vt.position).ToString("F2")}");
                var sk = p.Vessel.VesselStatus.NearFieldSkimmer;
                if (sk != null) sb.AppendLine($"skimmer '{sk.name}' lossyScale={sk.transform.lossyScale.ToString("F2")}");
                foreach (var c in Crystal.Active)
                {
                    if (c == null) continue;
                    foreach (var col in c.GetComponentsInChildren<Collider>(true))
                        sb.AppendLine($"crystal {c.ownDomain} pos={c.transform.position.ToString("F1")} {col.GetType().Name} " +
                                      $"trigger={col.isTrigger} boundsSize={col.bounds.size.ToString("F2")}");
                }
                var track = FindAnyObjectByType<SpawnableWaypointTrack>();
                var trails = track != null ? track.GetTrails() : null;
                if (trails != null && trails.Count > 0 && trails[0].TrailList.Count > 1)
                {
                    for (int k = 0; k < 2; k++)
                    {
                        var pr = trails[0].TrailList[k];
                        sb.AppendLine($"trackPrism[{k}] pos={pr.transform.position.ToString("F2")} lossy={pr.transform.lossyScale.ToString("F2")} " +
                                      $"super={pr.prismProperties.IsSuperShielded} shield={pr.prismProperties.IsShielded} domain={pr.Domain}");
                        foreach (var col in pr.GetComponentsInChildren<Collider>(true))
                            sb.AppendLine($"   {col.GetType().Name} trigger={col.isTrigger} enabled={col.enabled} boundsSize={col.bounds.size.ToString("F2")}");
                    }
                }
                if (sk != null)
                    foreach (var col in sk.GetComponentsInChildren<Collider>(true))
                        sb.AppendLine($"skimmerCollider {col.GetType().Name} enabled={col.enabled} boundsSize={col.bounds.size.ToString("F2")} " +
                                      (col is SphereCollider sc ? $"radius={sc.radius * sc.transform.lossyScale.x:F2}" : ""));
                if (SkimRaceCourseSource.TryBuildFromScene(out var course))
                {
                    sb.AppendLine(string.Format(inv, "course count={0} length={1:F1}", course.Count, course.Length));
                    for (int i = 0; i < Mathf.Min(6, course.Count); i++)
                        sb.AppendLine("  pt " + course.PointAtIndex(i).ToString("F1"));
                }
                foreach (var q in _gameData.Players)
                    if (q?.Vessel != null)
                        sb.AppendLine($"seat {q.Name} ai={q.IsInitializedAsAI} dom={q.Domain} pos={q.Vessel.Transform.position.ToString("F1")} fwd={q.Vessel.Transform.forward.ToString("F2")}");
                File.WriteAllText(Path.ChangeExtension(_filePath, null) + $"_race{_raceIndex}_probe.txt", sb.ToString());
            }
            catch (Exception e) { CSDebug.LogWarning($"[SkimRaceBenchmark] probe failed: {e.Message}"); }
        }

        void AppendTrace(float now, string seatName, SkimRacePilot pilot)
        {
            _nextTrace = now + 0.1f;
            var o = pilot.LastObservation;
            var a = pilot.LastAction;
            var d = pilot.Driver.LastDiagnostics;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (_trace.Length == 0)
                _trace.Append("t,seat,x,y,z,speed,boost,hasTarget,tx,ty,tz,dist,ahead,courseDist,collected,mode,headErr,cmdErr,pull,unreach,yaw,pitch,thr\n");
            _trace.Append(string.Format(inv,
                "{0:F2},{1},{2:F1},{3:F1},{4:F1},{5:F1},{6:F2},{7},{8:F1},{9:F1},{10:F1},{11:F1},{12:F1},{13:F1},{14},{15},{16:F1},{17:F1},{18},{19},{20:F2},{21:F2},{22:F2}\n",
                now, seatName, o.Position.x, o.Position.y, o.Position.z, o.Speed, o.BoostMultiplier,
                o.HasTarget ? 1 : 0, o.TargetPosition.x, o.TargetPosition.y, o.TargetPosition.z, o.TargetDistance,
                o.TargetAheadOnCourse, o.CourseDistance, o.Collected, (int)d.Mode, d.HeadingErrorDegrees,
                d.CommandErrorDegrees, d.CrystalPull ? 1 : 0, d.Unreachable ? 1 : 0, a.Yaw, a.Pitch, a.Throttle));
        }

        /// <summary>Writes a failure record for a race the normal flow never started.</summary>
        public void RecordNotStarted(string reason)
        {
            if (_written) return;
            if (!_running) BeginRace();
            FinishRace(0f, reason);
        }

        void FinishRace(float now, string timeoutReason)
        {
            SampleSeats(now);
            _written = true;
            _race.measuredRaceTime = now;
            _race.meanFrameMs = _frames > 0 ? 1000f * _frameSum / _frames : 0f;
            _race.maxFrameMs = 1000f * _frameMax;
            _race.quality = QualitySettings.names[QualitySettings.GetQualityLevel()];
            _race.winnerDomain = _gameData.WinnerDomain.ToString();
            _race.finished = timeoutReason == null && _gameData.WinnerDomain != Domains.Blue;

            float finish = 0f;
            foreach (var s in _race.seats)
            {
                var stats = _gameData.RoundStatsList.Find(r => r.Name == s.name);
                if (stats != null) s.score = stats.Score;
                if (_speedSamples.TryGetValue(s.name, out int n) && n > 0) s.meanSpeed = _speedSum[s.name] / n;
                if (s.domain == _race.winnerDomain) finish = Mathf.Max(finish, s.score);
            }
            _race.authoritativeFinishTime = _race.finished ? finish : 0f;

            // With several AI seats, judge the one whose domain WON (the race ends at the first
            // finisher, so that is the only AI whose own finish time exists).
            foreach (var s in _race.seats)
                if (!string.IsNullOrEmpty(s.policy) && s.domain == _race.winnerDomain) { _race.aiDomain = s.domain; break; }
            int domainCrystals = 0;
            foreach (var s in _race.seats)
                if (s.domain == _race.aiDomain) domainCrystals += s.crystals;
            _race.aiDomainCrystals = domainCrystals;

            _race.success = Evaluate(_race.finished, _race.winnerDomain, _race.aiDomain, domainCrystals,
                _race.requiredCrystals, _race.authoritativeFinishTime, _limit, timeoutReason, out var reason);
            if (_race.timeScaleMin < 0.999f || _race.timeScaleMax > 1.001f)
            {
                _race.success = false;
                reason = $"invalid: Time.timeScale left 1 during the race ({_race.timeScaleMin:F2}..{_race.timeScaleMax:F2})";
            }
            _race.failureReason = reason;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath));
                File.AppendAllText(_filePath, JsonUtility.ToJson(_race) + "\n");
                if (TraceFrames && _trace.Length > 0)
                    File.WriteAllText(Path.ChangeExtension(_filePath, null) + $"_race{_raceIndex}_trace.csv", _trace.ToString());
                if (TraceFrames && _hits.Length > 0)
                    File.WriteAllText(Path.ChangeExtension(_filePath, null) + $"_race{_raceIndex}_hits.csv",
                        "t,x,y,z,boostBefore,crystals,trackShellClearance,prismsWithin12...\n" + _hits);
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[SkimRaceBenchmark] Could not write {_filePath}: {e.Message}");
            }

            CSDebug.Log($"[SkimRaceBenchmark] race {_raceIndex} I{_race.intensity} seed {_race.trackSeed}: " +
                      $"{(_race.success ? "SUCCESS" : "FAIL")} finish={_race.authoritativeFinishTime:F2}s " +
                      $"ai={_race.aiDomain} {domainCrystals}/{_race.requiredCrystals} {_race.failureReason}");
            RaceRecorded?.Invoke(_race);
        }
    
        /// <summary>
        /// The benchmark's time limit for an intensity - the ONE source of truth the runner, the
        /// benchmark window and the remote command default to (an explicit limit still overrides).
        /// I2 was re-baselined from 70 s to 80 s by product decision (Docs/SKIM_RACE_AI.md §6.11): 70 s
        /// is not reachable there without changing the game for every pilot. The other intensities
        /// stay at 70 s and are written out so it is plain that only I2 moved.
        /// </summary>
        public static float DefaultLimitSeconds(int intensity)
        {
            switch (intensity)
            {
                case 1: return 70f;
                case 2: return 80f;
                case 3: return 70f;
                case 4: return 70f;
                default: return 70f;
            }
        }

        /// <summary>
        /// The benchmark verdict, as a pure function so it can be tested: SUCCESS only when a
        /// domain finished, that domain is the AI's, it collected at least the required count,
        /// and the authoritative finish time is positive and at or under the limit.
        /// </summary>
        public static bool Evaluate(bool finished, string winnerDomain, string aiDomain, int collected,
            int required, float finishTime, float limit, string timeoutReason, out string failureReason)
        {
            bool aiWon = finished && !string.IsNullOrEmpty(aiDomain) && winnerDomain == aiDomain;
            bool allCollected = required > 0 && collected >= required;
            bool inTime = finishTime > 0f && finishTime <= limit;
            failureReason = "";
            if (timeoutReason != null) failureReason = timeoutReason;
            else if (!aiWon) failureReason = $"domain {winnerDomain} finished first";
            else if (!allCollected) failureReason = $"collected {collected}/{required}";
            else if (!inTime) failureReason = $"finish {finishTime:F2}s > {limit:F1}s";
            return timeoutReason == null && aiWon && allCollected && inTime;
        }
    }
}
