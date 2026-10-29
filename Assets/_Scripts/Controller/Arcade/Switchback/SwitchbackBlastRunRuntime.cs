using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The per-peer, per-match half of Blast Run (SWITCHBACK.md "Blast Run"):
    /// <list type="bullet">
    /// <item>The block fields' movers (sliders and rotors, <see cref="BlastRunMover"/>) are posed every frame off the
    /// shared server clock - a closed-form function of time, so nothing about them is replicated.</item>
    /// <item>One <c>[Switchback] RUN</c> log line at the finish (intensity, seed, time, gates, prisms destroyed,
    /// stations, keys, movers), so a test session can be read back from the log.</item>
    /// </list>
    /// The finish watches the LOCAL pilot only; the scoring is still the gate race's own
    /// (<see cref="GateRaceController"/>).
    /// </summary>
    public sealed class SwitchbackBlastRunRuntime : MonoBehaviour
    {
        /// <summary>A prism laid at most this far from where the layout put it is that prism.</summary>
        const float PrismMatchDistance = 0.75f;
        /// <summary>How long to keep looking for moving prisms that the budgeted lay has not produced yet.</summary>
        const float PrismScanTimeout = 30f;
        const float PrismScanInterval = 0.5f;

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

        bool _finished;
        int _blocksAtStart = -1;

        /// <summary>Hand over the match: the Blast Run layout and the container its prisms were laid into.</summary>
        public void Init(GameDataSO gameData, int intensity, int seed, int courseCount, System.Func<int> raceLength,
                         SwitchbackFeatureLayout layout, Transform prismContainer)
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
            if (_layout == null || _tracked.Count == 0) return;
            if (_movingMatched < _movingExpected && Time.time >= _nextScan && Time.time < _scanDeadline) ScanForMovingPrisms();

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
            var found = _prismContainer.GetComponentsInChildren<Prism>(true);
            float sq = PrismMatchDistance * PrismMatchDistance;
            foreach (var range in _tracked)
                for (int j = 0; j < range.count; j++)
                {
                    int k = range.first + j;
                    if (_moving[k] || _lost[k]) continue;
                    Vector3 laid = _layout.Prisms[k].Position;
                    for (int f = 0; f < found.Length; f++)
                    {
                        if (!found[f] || (found[f].transform.position - laid).sqrMagnitude > sq) continue;
                        _moving[k] = found[f];
                        _movingMatched++;
                        break;
                    }
                }
            if (_movingMatched >= _movingExpected || Time.time + PrismScanInterval >= _scanDeadline)
                CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                    $"[Switchback] Blast Run movers: {_movingMatched}/{_movingExpected} prisms found.");
        }

        // ── Blast Run movers ──────────────────────────────────────────

        /// <summary>Every peer: pose each Blast Run mover at shared time <paramref name="t"/> - the pose is computed once
        /// per group, then written to each of its standing prisms (no allocation). A blasted prism is simply skipped.
        /// The prisms stay children of the lay container (a per-group parent transform would save only the per-prism
        /// pose maths: <see cref="Prism.NotifyPositionChanged"/> must still run per prism for the spatial index, the
        /// shield shell and the render matrix).</summary>
        void TickMovers(float t)
        {
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
