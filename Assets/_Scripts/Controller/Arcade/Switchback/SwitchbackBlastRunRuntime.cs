using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using Unity.Netcode;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The per-peer, per-match half of Blast Run (SWITCHBACK.md "Blast Run"):
    /// <list type="bullet">
    /// <item>The block fields' movers (sliders and rotors, <see cref="BlastRunMover"/>) are posed every frame off the
    /// shared server clock - a closed-form function of time, so nothing about them is replicated.</item>
    /// <item>Server: which laid prisms still stand (<see cref="PrismStanding"/>), for the AI's blast planner
    /// (<see cref="SwitchbackBlastAI"/>) - every layout prism is matched to its spawned prism by laid position.</item>
    /// <item>One <c>[Switchback] RUN</c> log line at the finish (intensity, seed, time, gates, prisms destroyed,
    /// stations, keys, movers), so a test session can be read back from the log.</item>
    /// </list>
    /// The finish watches the LOCAL pilot only; the scoring is still the gate race's own
    /// (<see cref="GateRaceController"/>).
    /// </summary>
    public sealed class SwitchbackBlastRunRuntime : MonoBehaviour, ISwitchbackAIWorld
    {
        /// <summary>A prism laid at most this far from where the layout put it is that prism.</summary>
        const float PrismMatchDistance = 0.75f;
        /// <summary>How long to keep looking for moving prisms that the budgeted lay has not produced yet.</summary>
        const float PrismScanTimeout = 30f;
        const float PrismScanInterval = 0.5f;

        static readonly ProfilerMarker s_tickMarker = new("Switchback.BlastRun.TickMovers");
        static readonly ProfilerMarker s_scanMarker = new("Switchback.BlastRun.ScanPrisms");
        /// <summary>The scan's reused buffer: one lay container of a few thousand prisms, every half second for 30 s.</summary>
        static readonly List<Prism> s_found = new(4096);

        GameDataSO _gameData;
        int _intensity;
        int _seed;
        int _courseCount;
        System.Func<int> _raceLength;

        SwitchbackFeatureLayout _layout;
        Transform _prismContainer;
        Prism[] _moving;            // per layout prism index; null until matched (and for non-moving ones)
        bool[] _lost;               // per layout prism index: matched once, since destroyed, pooled or re-parented
        int _movingExpected, _movingMatched;
        readonly List<(int first, int count)> _tracked = new();   // the movers' prism ranges
        float _nextScan, _scanDeadline;

        // Server, for the AI: EVERY layout prism matched to its spawned prism (the movers through _moving), found through
        // a hash of laid positions so a scan is one lookup per spawned prism.
        bool _trackAll;
        Prism[] _static;            // per layout prism index; null until matched (and for moving ones)
        int _allExpected, _allMatched;
        Dictionary<Vector3Int, int> _laidCell;   // cell of a laid position -> first layout index in it
        int[] _laidNext;                         // chain within a cell (-1 = end)
        // The movers' prisms, hashed the same way (every peer): a scan is one lookup per spawned prism instead of
        // movers x spawned distance tests.
        Dictionary<Vector3Int, int> _moveCell;
        int[] _moveNext;

        bool _finished;
        int _blocksAtStart = -1;

        /// <summary>Hand over the match: the Blast Run layout and the container its prisms were laid into.</summary>
        public void Init(GameDataSO gameData, int intensity, int seed, int courseCount, System.Func<int> raceLength,
                         SwitchbackFeatureLayout layout, Transform prismContainer, bool trackAllPrisms = false)
        {
            _gameData = gameData;
            _intensity = intensity;
            _seed = seed;
            _courseCount = courseCount;
            _raceLength = raceLength;
            _layout = layout;
            _prismContainer = prismContainer;

            if (_layout != null)
            {
                _moving = new Prism[_layout.Prisms.Count];
                _lost = new bool[_layout.Prisms.Count];
                foreach (var m in _layout.Movers) _tracked.Add((m.FirstPrism, m.Count));
                foreach (var r in _tracked) _movingExpected += r.count;
                _scanDeadline = Time.time + PrismScanTimeout;
                BuildMovingIndex();
                if (trackAllPrisms) BuildLaidIndex();
            }
        }

        /// <summary>The tracked prism at <paramref name="k"/> is still the one laid there: not destroyed (gone, or flagged
        /// <see cref="Prism.destroyed"/>) and not handed to another parent (a pool re-using it). Anything else is dropped for good - never moved again.</summary>
        bool Live(int k)
        {
            var prism = _moving[k];
            if (prism == null) return false;
            if (!prism || prism.destroyed || !_prismContainer || !prism.transform.IsChildOf(_prismContainer))
            {
                _moving[k] = null;
                _lost[k] = true;
                return false;
            }
            return true;
        }

        bool IsMoving(int k)
        {
            foreach (var r in _tracked) if (k >= r.first && k < r.first + r.count) return true;
            return false;
        }

        static Vector3Int CellOf(Vector3 p) => new(Mathf.FloorToInt(p.x / PrismMatchDistance),
                                                   Mathf.FloorToInt(p.y / PrismMatchDistance),
                                                   Mathf.FloorToInt(p.z / PrismMatchDistance));

        void BuildLaidIndex()
        {
            int n = _layout.Prisms.Count;
            _trackAll = true;
            _static = new Prism[n];
            _laidNext = new int[n];
            _laidCell = new Dictionary<Vector3Int, int>(n);
            for (int k = n - 1; k >= 0; k--)
            {
                _laidNext[k] = -1;
                if (IsMoving(k)) continue;
                _allExpected++;
                var c = CellOf(_layout.Prisms[k].Position);
                if (_laidCell.TryGetValue(c, out int head)) _laidNext[k] = head;
                _laidCell[c] = k;
            }
        }

        void BuildMovingIndex()
        {
            _moveNext = new int[_layout.Prisms.Count];
            _moveCell = new Dictionary<Vector3Int, int>(_movingExpected);
            // Ranges walked backwards so a cell's chain runs in layout order - the old linear match's order.
            for (int r = _tracked.Count - 1; r >= 0; r--)
                for (int k = _tracked[r].first + _tracked[r].count - 1; k >= _tracked[r].first; k--)
                {
                    if (k < 0 || k >= _moveNext.Length) continue;
                    var c = CellOf(_layout.Prisms[k].Position);
                    _moveNext[k] = _moveCell.TryGetValue(c, out int head) ? head : -1;
                    _moveCell[c] = k;
                }
        }

        /// <summary>The movers' prisms the scan has not matched yet: each spawned prism looked up by its position.</summary>
        void MatchMoving(List<Prism> found)
        {
            float sq = PrismMatchDistance * PrismMatchDistance;
            for (int f = 0; f < found.Count && _movingMatched < _movingExpected; f++)
            {
                var prism = found[f];
                if (!prism) continue;
                Vector3 at = prism.transform.position;
                var c = CellOf(at);
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (!_moveCell.TryGetValue(new Vector3Int(c.x + dx, c.y + dy, c.z + dz), out int k)) continue;
                    for (; k >= 0; k = _moveNext[k])
                    {
                        if (_moving[k] || _lost[k] || (_layout.Prisms[k].Position - at).sqrMagnitude > sq) continue;
                        _moving[k] = prism;
                        _movingMatched++;
                        goto next;
                    }
                }
                next: ;
            }
        }

        /// <summary>The still-static prisms the scan has not matched yet: look each spawned prism up by its position.</summary>
        void MatchStatic(List<Prism> found)
        {
            float sq = PrismMatchDistance * PrismMatchDistance;
            for (int f = 0; f < found.Count; f++)
            {
                var prism = found[f];
                if (!prism) continue;
                Vector3 at = prism.transform.position;
                var c = CellOf(at);
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (!_laidCell.TryGetValue(new Vector3Int(c.x + dx, c.y + dy, c.z + dz), out int k)) continue;
                    for (; k >= 0; k = _laidNext[k])
                    {
                        if (_static[k] || _lost[k] || (_layout.Prisms[k].Position - at).sqrMagnitude > sq) continue;
                        _static[k] = prism;
                        _allMatched++;
                        goto next;
                    }
                }
                next: ;
            }
        }

        /// <summary>
        /// Server, for the AI planner: does layout prism <paramref name="k"/> still stand? Not yet matched (still being
        /// laid, or the scan timed out) reads as standing - the planner then over-counts an obstacle, never under-counts it.
        /// Destroyed, pooled or re-parented reads as gone for good.
        /// </summary>
        public bool PrismStanding(int k)
        {
            if (_layout == null || k < 0 || k >= _lost.Length) return true;
            if (_lost[k]) return false;
            if (_moving[k] != null) return Live(k);
            if (!_trackAll) return true;
            var prism = _static[k];
            if (prism == null) return true;
            if (!prism || prism.destroyed || !_prismContainer || !prism.transform.IsChildOf(_prismContainer))
            {
                _static[k] = null;
                _lost[k] = true;
                return false;
            }
            return true;
        }

        /// <summary>The runtime does not watch the key crystals: <see cref="SwitchbackBlastAI"/> does.</summary>
        public bool KeyAvailable(int station) => true;

        void Update()
        {
            if (_gameData == null || _courseCount == 0) return;
            TickMoving();

            var stats = _gameData.LocalPlayer?.RoundStats;
            if (stats == null) return;
            if (_blocksAtStart < 0 && _gameData.TurnStartTime > 0f) _blocksAtStart = stats.BlocksDestroyed;
            TickFinish(stats, stats.SwitchesThreaded);
        }

        /// <summary>The movers, off the server clock so every peer's prisms stand in the same place.</summary>
        void TickMoving()
        {
            if (_layout == null || (_tracked.Count == 0 && !_trackAll)) return;
            if ((_movingMatched < _movingExpected || (_trackAll && _allMatched < _allExpected))
                && Time.time >= _nextScan && Time.time < _scanDeadline) ScanForMovingPrisms();

            var nm = NetworkManager.Singleton;
            float t = nm != null && nm.IsListening ? nm.ServerTime.TimeAsFloat : Time.time;
            TickMovers(t);
        }

        /// <summary>The prisms are laid ASYNC and in budgeted batches: match what exists now to the layout by laid
        /// position, and come back until every moving prism is found (or the scan times out - those stay put).</summary>
        void ScanForMovingPrisms()
        {
            _nextScan = Time.time + PrismScanInterval;
            if (!_prismContainer) return;
            using var _ = s_scanMarker.Auto();
            s_found.Clear();
            _prismContainer.GetComponentsInChildren(true, s_found);
            if (_trackAll && _allMatched < _allExpected) MatchStatic(s_found);
            if (_movingMatched < _movingExpected) MatchMoving(s_found);
            s_found.Clear();
            if ((_movingMatched >= _movingExpected && (!_trackAll || _allMatched >= _allExpected))
                || Time.time + PrismScanInterval >= _scanDeadline)
                CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                    $"[Switchback] Blast Run movers: {_movingMatched}/{_movingExpected} prisms found" +
                    (_trackAll ? $"; AI liveness: {_allMatched}/{_allExpected} static prisms." : "."));
        }

        // ── Blast Run movers ──────────────────────────────────────────

        /// <summary>Every peer: pose each Blast Run mover at shared time <paramref name="t"/> - the pose is computed once
        /// per group, then written to each of its standing prisms (no allocation). A blasted prism is simply skipped.
        /// The prisms stay children of the lay container (a per-group parent transform would save only the per-prism
        /// pose maths: <see cref="Prism.NotifyPositionChanged"/> must still run per prism for the spatial index, the
        /// shield shell and the render matrix).</summary>
        void TickMovers(float t)
        {
            using var _ = s_tickMarker.Auto();
            var movers = _layout.Movers;
            for (int i = 0; i < movers.Count; i++)
            {
                var mover = movers[i];
                mover.PoseAt(t, out Quaternion spin, out Vector3 offset);
                for (int j = 0; j < mover.Count; j++)
                {
                    int k = mover.FirstPrism + j;
                    if (!Live(k)) continue;
                    var laid = _layout.Prisms[k];
                    mover.Apply(spin, offset, laid.Position, laid.Rotation, out Vector3 position, out Quaternion rotation);
                    var prism = _moving[k];
                    if (mover.Motion == BlastRunMotion.Spin) prism.transform.SetPositionAndRotation(position, rotation);
                    else prism.transform.position = position;
                    prism.NotifyPositionChanged();
                }
            }
        }

        void TickFinish(IRoundStats stats, int threaded)
        {
            int length = _raceLength != null ? _raceLength() : _courseCount;
            if (_finished || length <= 0 || threaded < length || _gameData.TurnStartTime <= 0f) return;
            _finished = true;

            float time = Time.time - _gameData.TurnStartTime;
            int hits = _blocksAtStart >= 0 ? stats.BlocksDestroyed - _blocksAtStart : -1;
            CSDebug.Log($"[Switchback] RUN blastRun intensity={_intensity} seed={_seed} time={time:F2}s " +
                        $"gates={threaded}/{length} prismsDestroyed={hits}" +
                        (_layout != null
                            ? $" stations={_layout.Stations.Count} keys={BlastRunKeyCrystals.Count} movers={_layout.Movers.Count}"
                            : ""));
        }
    }
}
