using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// SpeedPainter - the Angler-only circuit race, and the Angler's TIME card. A closed loop of rings is flown in LAPS; the
    /// first domain whose lead runner threads the last gate of the last lap wins (golf: finish time).
    ///
    /// <para><b>Paint your path, zoom along it.</b> The Angler cruises at 40 u/s; it RACES with its two groups
    /// (ANGLER.md §1m): every leg carries two prism MASS clusters (<see cref="SpeedPainterCourse.Clusters"/>) - one on the
    /// leg, one past the next ring. Paint the near one and let go (one group is thrown at it), paint the far one and let
    /// go (the other group), then dash to each group in turn: the second dash runs through the ring. The creatures eat
    /// what they reach, so each cluster grows back a prism at a time.</para>
    ///
    /// <para><b>What the mode adds:</b> the clusters, laid on every peer from the broadcast course in
    /// <see cref="OnCourseRaised"/> and regrown in <see cref="LateUpdate"/>, and the start hint. The vessel is the shipped
    /// Angler (its AI, <see cref="AnglerLeapfrogAIPolicySO"/>, is the hull's). Everything else is the gate-race platform.
    /// The shape is Garrett's Lantern Run (one lantern a ring, cece/confident-goodall-6cpxan), with two clusters a leg
    /// for the two groups. See SPEEDPAINTER.md.</para>
    /// </summary>
    public class SpeedPainterController : GateRaceController
    {
        [Header("SpeedPainter circuit")]
        [Tooltip("Laps of the ring set that make one race. The authored gate target is the RACE length, so the circuit " +
                 "is laid with target/laps rings - one number authored once, in the end-condition overrides.")]
        [SerializeField, Min(1)] int laps = SpeedPainterCourseSource.DefaultLaps;

        [Header("Mass clusters")]
        [Tooltip("The prism every cluster is laid from: the environment prism (SpawnablePrism). Authored by " +
                 "Tools/Build/author_speed_painter_assets.py. Unset = no clusters (nothing to paint), and an error.")]
        [SerializeField] Prism clusterPrefab;

        [Tooltip("u a cluster keeps outside the nucleus.")]
        [SerializeField, Min(0f)] float nucleusMargin = 30f;

        protected override string ModeName => "SpeedPainter";

        /// <summary>Regatta's gate steering: commit from the approach point, aim through the ring's centre.</summary>
        protected override bool AICommitFromApproachPoint => true;

        protected override RaceCourseSource CreateCourseSource() => new SpeedPainterCourseSource { Laps = laps };

        /// <summary>Owner id no pilot carries: the clusters are nobody's trail, and only prisms still carrying it count.</summary>
        const string ClusterOwnerId = "SpeedPainterCluster";

        static readonly ProfilerMarker s_tickMarker = new("SpeedPainter.Tick");

        /// <summary>One laid cluster: its slots, the prism in each (null = eaten, waiting to grow back), its regrow clock.</summary>
        sealed class LaidCluster
        {
            public Vector3 Centre;
            public Vector3[] Slots;
            public Prism[] Prisms;
            public Trail Trail;
            public float NextRegrowAt;
        }

        readonly List<LaidCluster> _clusters = new();
        Transform _clusterRoot;
        float _regrowSeconds = 1f;
        bool _hintPosted;

        // ── Lifecycle ─────────────────────────────────────────────────────

        public override void OnNetworkDespawn()
        {
            ClearClusters();
            _hintPosted = false;
            base.OnNetworkDespawn();
        }

        /// <summary>The clusters, laid on every peer from the course every peer received.</summary>
        protected override void OnCourseRaised()
        {
            ClearClusters();
            if (!clusterPrefab)
            {
                CSDebug.LogError("[SpeedPainter] No cluster prefab on the controller - the race has nothing to paint. " +
                                 "Re-run Tools/Build/author_speed_painter_assets.py.");
                return;
            }

            ResolveShell(out float inner, out _);
            var clusters = SpeedPainterCourse.Clusters(_course, Intensity, ResolveCellCentre(),
                                                       inner / innerRadiusNucleusFactor + nucleusMargin);
            _regrowSeconds = SpeedPainterCourse.RegrowSeconds(Intensity);

            // At the scene root, at world identity: a laid prism's position is local to its parent, and the course is
            // already in world coordinates.
            _clusterRoot = new GameObject("SpeedPainterClusters").transform;
            for (int c = 0; c < clusters.Count; c++)
            {
                var laid = new LaidCluster
                {
                    Centre = clusters[c].Centre,
                    Slots = clusters[c].Slots,
                    Prisms = new Prism[clusters[c].Slots.Length],
                    Trail = new Trail(),
                    NextRegrowAt = Time.time + _regrowSeconds,
                };
                _clusters.Add(laid);
                // The first cohort is laid behind the connecting panel, so the arena-ready gate waits for it.
                for (int k = 0; k < laid.Slots.Length; k++) LaySlot(laid, k, forReveal: true);
            }
        }

        void LaySlot(LaidCluster cluster, int slot, bool forReveal)
        {
            Vector3 at = cluster.Slots[slot];
            Vector3 outward = at - cluster.Centre;
            var rotation = outward.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(outward) : Quaternion.identity;
            // A regrown prism comes mid-race, so it must not hold the arena-ready gate (a continuous source can never
            // all-clear it).
            var prism = PrismTrailBuilder.LayOne(clusterPrefab,
                new PrismLay(new SpawnPoint(at, rotation, SpeedPainterCourse.ClusterPrismScale), Domains.Blue),
                _clusterRoot, cluster.Trail, ClusterOwnerId, watchForReveal: forReveal);
            if (!prism) return;
            if (!forReveal) prism.CompletesAsLiveRibbon = true;
            cluster.Prisms[slot] = prism;
        }

        /// <summary>Still the cluster's: alive, still carrying the cluster's owner id and the neutral domain (the pool
        /// re-issues a destroyed prism to someone else, and a steal re-colours one).</summary>
        static bool IsClusterPrism(Prism prism) =>
            prism && !prism.destroyed && prism.ownerID == ClusterOwnerId && prism.Domain == Domains.Blue;

        // A subclass must not declare Update (GateRaceController.Update runs the race); LateUpdate is free.
        void LateUpdate()
        {
            if (!IsSpawned || gameData == null || !gameData.IsTurnRunning) return;
            using var _ = s_tickMarker.Auto();

            if (!_hintPosted)
            {
                _hintPosted = true;
                var local = gameData.LocalPlayer;
                if (local != null) GameToastAPI.Post(GameToastSituation.SpeedPainterPaintHint, local.Domain);
            }

            TickRegrow();
        }

        /// <summary>
        /// The clusters grow back: every <see cref="SpeedPainterCourse.RegrowSeconds"/>, each cluster with an empty slot
        /// grows one prism into its first empty slot. Every peer regrows from what it saw eaten, as Serpentine's debris
        /// does; the fauna eat on the host, so a client may draw a prism the host has already lost (SPEEDPAINTER.md).
        /// </summary>
        void TickRegrow()
        {
            if (_clusters.Count == 0 || !clusterPrefab) return;
            float now = Time.time;
            for (int c = 0; c < _clusters.Count; c++)
            {
                var cluster = _clusters[c];
                if (now < cluster.NextRegrowAt) continue;
                cluster.NextRegrowAt = now + _regrowSeconds;
                for (int k = 0; k < cluster.Prisms.Length; k++)
                {
                    if (IsClusterPrism(cluster.Prisms[k])) continue;
                    cluster.Prisms[k] = null;
                    LaySlot(cluster, k, forReveal: false);
                    break;
                }
            }
        }

        void ClearClusters()
        {
            for (int c = 0; c < _clusters.Count; c++)
            {
                var prisms = _clusters[c].Prisms;
                for (int k = 0; k < prisms.Length; k++)
                    if (IsClusterPrism(prisms[k]) && !EnvironmentPrismPool.TryRelease(prisms[k]))
                        Destroy(prisms[k].gameObject);
            }
            _clusters.Clear();
            if (_clusterRoot && _clusterRoot.childCount == 0) Destroy(_clusterRoot.gameObject);
            _clusterRoot = null;
        }
    }
}
