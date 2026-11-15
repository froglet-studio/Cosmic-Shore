using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Spearline - the Swordfish-only lap race, and the Swordfish's TIME card (SPEARLINE.md). The course is Headlong's
    /// circuit (this airframe's ramp and turn), and the rings carry SEALS: panes of prisms in the mouth
    /// (<see cref="SpearlineSeals"/>). A sealed ring counts only when the pilot crosses it through a hole, and the hull
    /// alone rams a lane narrower than the hole. So every seal asks for a swing of the bill on the way through, and
    /// Quickdraw (Time) is the sword's rate: the faster it swings, the less speed a seal costs.
    ///
    /// <para><b>What the mode adds:</b> the rings, the seals (every peer laying the same prisms from the broadcast
    /// rings) and their regrowth. A cut seal prism grows back <see cref="SpearlineSealSettings.RegrowSeconds"/> after
    /// the field has passed it (no pilot is flying for that ring and no hull is at it), so the leader cuts and the pack
    /// that follows close behind flies through the leader's holes. <b>What it does not:</b> nothing about the vessel.
    /// The AI flies the gate race's own steering and its shipped sword hand (<c>SwordfishSwordAutopilot</c>), which
    /// swings at hostile prisms in reach; a crossing that did not open the seal is a missed mouth, and the gate race's
    /// re-attack brings it round again.</para>
    /// </summary>
    public class SpearlineController : GateRaceController
    {
        [Header("AI nose lead")]
        [Tooltip("Each AI's nose lead by hull (AIPilot.NoseLeadGain), set as it is armed. The Rhino's 2.5 on Headlong's " +
                 "circuit: the same airframe.")]
        [SerializeField] AINoseLeadRow[] aiNoseLead = { new AINoseLeadRow(VesselClassType.Swordfish, 2.5f) };

        protected override AINoseLeadRow[] AINoseLeadTable => aiNoseLead;

        [Header("Spearline")]
        [Tooltip("Laps of the ring set that make one race. The authored gate target is the RACE length, so the circuit " +
                 "is laid with target/laps rings.")]
        [SerializeField, Min(1)] int laps = SpearlineCourseSource.DefaultLaps;

        [Tooltip("The prism every seal is laid from: the environment prism (SpawnablePrism). Authored by " +
                 "Tools/Build/author_spearline_assets.py. Unset = no seals, and a warning.")]
        [SerializeField] Prism sealPrefab;

        [Tooltip("Seal prisms grown back a frame, at most.")]
        [SerializeField, Min(1)] int sealRegrowPerFrame = 40;

        [Tooltip("A seal grows back only while no hull is within this distance (u) of its ring's centre, past the mouth.")]
        [SerializeField, Min(0f)] float sealClearance = 60f;

        const string SealOwnerId = "Spearline";

        protected override string ModeName => "Spearline";

        protected override RaceCourseSource CreateCourseSource() => new SpearlineCourseSource { Laps = laps };

        protected override bool AICommitFromApproachPoint => true;

        readonly List<Vector3> _sealPositions = new();
        readonly List<int> _sealRing = new();
        readonly List<Prism> _sealPrisms = new();
        readonly List<float> _goneAt = new();
        readonly Dictionary<int, (int start, int count)> _sealOf = new();
        readonly List<Vector3> _scratch = new();
        Transform _sealRoot;
        int _regrowCursor;
        bool _hintPosted;

        SpearlineSealSettings Settings => SpearlineSeals.ForIntensity(Intensity);

        public override void OnNetworkDespawn()
        {
            ClearSeals();
            base.OnNetworkDespawn();
        }

        // ── The seals (every peer, from the broadcast rings) ─────────────

        protected override void OnCourseRaised()
        {
            ClearSeals();
            _hintPosted = false;
            if (!sealPrefab)
            {
                CSDebug.LogWarning("[Spearline] no seal prefab on the controller - the seals are not laid.");
                return;
            }
            var s = Settings;
            _sealRoot = new GameObject("SpearlineSeals").transform;
            for (int ring = 0; ring < _course.Count; ring++)
            {
                if (!SpearlineSeals.IsSealed(ring, s)) continue;
                _scratch.Clear();
                SpearlineSeals.Lay(_course[ring], _scratch);
                _sealOf[ring] = (_sealPositions.Count, _scratch.Count);
                for (int k = 0; k < _scratch.Count; k++)
                {
                    _sealPositions.Add(_scratch[k]);
                    _sealRing.Add(ring);
                    _sealPrisms.Add(null);
                    _goneAt.Add(-1f);
                    _sealPrisms[_sealPrisms.Count - 1] = LaySealPrism(_sealPrisms.Count - 1, forReveal: true);
                }
            }
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[Spearline] intensity {Intensity}: {_course.Count} rings, {_sealOf.Count} sealed, {_sealPrisms.Count} seal prisms.");
        }

        Prism LaySealPrism(int i, bool forReveal)
        {
            int ring = _sealRing[i];
            var rotation = Quaternion.LookRotation(_course[ring].Axis);
            var scale = new Vector3(SpearlineSeals.PrismSize, SpearlineSeals.PrismSize, SpearlineSeals.PrismDepth);
            return PrismTrailBuilder.LayOne(sealPrefab,
                new PrismLay(new SpawnPoint(_sealPositions[i], rotation, scale), Domains.Blue,
                             SpearlineSeals.KindFor(ring, Settings)),
                _sealRoot, new Trail(), SealOwnerId, admitAuthoredScale: true, watchForReveal: forReveal);
        }

        static bool Holds(Prism prism) => prism && !prism.destroyed && prism.ownerID == SealOwnerId;

        /// <summary>A sealed ring threads only through a hole: the ring's own mouth, AND enough of the seal round the
        /// crossing already gone (<see cref="SpearlineSeals.OpenShare"/>).</summary>
        protected override bool ThreadsGate(int ringIndex, Vector3 prev, Vector3 cur)
        {
            if (!base.ThreadsGate(ringIndex, prev, cur)) return false;
            if (!_sealOf.TryGetValue(ringIndex, out var seal) || seal.count == 0) return true;
            var gate = _course[ringIndex];
            if (!SpearlineSeals.PlaneCrossing(gate, prev, cur, out var crossing)) return true;
            float share = SpearlineSeals.OpenShare(gate, crossing, new SealView(_sealPositions, seal.start, seal.count),
                                                   k => Holds(_sealPrisms[seal.start + k]));
            return share >= Settings.OpenFraction;
        }

        /// <summary>One ring's slice of the seal positions, without a copy.</summary>
        readonly struct SealView : IReadOnlyList<Vector3>
        {
            readonly List<Vector3> _all;
            readonly int _start;
            public SealView(List<Vector3> all, int start, int count) { _all = all; _start = start; Count = count; }
            public int Count { get; }
            public Vector3 this[int index] => _all[_start + index];
            public IEnumerator<Vector3> GetEnumerator() { for (int i = 0; i < Count; i++) yield return this[i]; }
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }

        /// <summary>Every peer, every frame: the hint once the turn runs, and a cut seal prism grows back
        /// <see cref="SpearlineSealSettings.RegrowSeconds"/> after the field has passed its ring.</summary>
        void LateUpdate()
        {
            if (!_hintPosted && gameData != null && gameData.IsTurnRunning)
            {
                _hintPosted = true;
                var local = gameData.LocalPlayer;
                if (local != null) GameToastAPI.Post(GameToastSituation.SpearlineHint, local.Domain);
            }

            if (_sealPrisms.Count == 0 || !sealPrefab) return;
            float now = Time.time;
            float regrow = Settings.RegrowSeconds;
            int budget = sealRegrowPerFrame;
            for (int n = 0; n < _sealPrisms.Count && budget > 0; n++)
            {
                int i = (_regrowCursor + n) % _sealPrisms.Count;
                if (Holds(_sealPrisms[i])) { _goneAt[i] = -1f; continue; }
                if (_goneAt[i] < 0f) { _goneAt[i] = now; continue; }
                int ring = _sealRing[i];
                if (now - _goneAt[i] < regrow || FieldAtRing(ring)) continue;
                _sealPrisms[i] = LaySealPrism(i, forReveal: false);
                _goneAt[i] = -1f;
                budget--;
            }
            _regrowCursor = (_regrowCursor + 1) % _sealPrisms.Count;
        }

        /// <summary>Is any pilot flying for this ring now, or any hull at it? Then its seal does not grow back yet
        /// (growing it on a hull, or in front of a pilot mid-approach, would close a hole they were promised).</summary>
        bool FieldAtRing(int ring)
        {
            var gate = _course[ring];
            float reach = gate.Radius + sealClearance;
            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                var tf = p?.Vessel?.Transform;
                if (!tf) continue;
                if ((tf.position - gate.Position).sqrMagnitude < reach * reach) return true;
                int threaded = p.RoundStats?.SwitchesThreaded ?? 0;
                if (threaded < RaceLength && RingIndexFor(threaded) == ring) return true;
            }
            return false;
        }

        void ClearSeals()
        {
            for (int i = 0; i < _sealPrisms.Count; i++)
            {
                var prism = _sealPrisms[i];
                if (Holds(prism) && !EnvironmentPrismPool.TryRelease(prism)) Destroy(prism.gameObject);
            }
            _sealPrisms.Clear();
            _sealPositions.Clear();
            _sealRing.Clear();
            _goneAt.Clear();
            _sealOf.Clear();
            _regrowCursor = 0;
            if (_sealRoot) Destroy(_sealRoot.gameObject);
            _sealRoot = null;
        }
    }
}
