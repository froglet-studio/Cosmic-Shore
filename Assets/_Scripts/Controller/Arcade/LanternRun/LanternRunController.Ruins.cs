using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Lantern Run's garden ruins in the world (LANTERNRUN.md, "The ruins"): every peer lays
    /// <see cref="LanternRunRuins"/>' prisms from the broadcast rings, in the clusters' prism and in domain colours of
    /// their own, once a course. They are scenery with the game's own prism rules: a hull breaks them, a creature eats
    /// them, nothing grows back, and they are returned to the pool with the clusters. The wheels turn: their prisms are
    /// POSED here every frame from the network clock, so every peer sees the same angle (a posed prism is the shape of
    /// its owner, not a moving prism: Docs/PRISM_MOTION.md §1). Nothing here reads or writes a vessel.
    /// </summary>
    public partial class LanternRunController
    {
        const string RuinOwnerId = "LanternRunRuins";

        [Header("Garden ruins (I4)")]
        [Tooltip("The domain colours the ruins are laid in, one ruin after another round the lap, so they read apart " +
                 "from the clusters (Blue). Decoration (Yash, 2026-11-24). Empty = Blue, as the clusters.")]
        [SerializeField] Domains[] ruinDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        readonly List<SpearlineRuinPart> _ruinParts = new();
        readonly List<SpearlineRuinWheel> _ruinWheels = new();
        readonly List<bool> _ruinLaid = new();
        // One entry for each part, null where the part was left out: a wheel names its parts by index.
        readonly List<Prism> _ruinPrisms = new();
        Transform _ruinRoot;

        void LayRuins(IReadOnlyList<LanternRunCourse.Cluster> clusters)
        {
            LanternRunRuins.Lay(_course, Intensity, clusters, _ruinParts, _ruinWheels, _ruinLaid);
            if (_ruinParts.Count == 0) return;
            _ruinRoot = new GameObject("LanternRunRuins").transform;
            var trail = new Trail();
            for (int i = 0; i < _ruinParts.Count; i++)
            {
                if (!_ruinLaid[i])
                {
                    _ruinPrisms.Add(null);
                    continue;
                }
                var part = _ruinParts[i];
                var rotation = Quaternion.LookRotation(part.Forward, part.Up);
                _ruinPrisms.Add(PrismTrailBuilder.LayOne(clusterPrefab,
                    new PrismLay(new SpawnPoint(part.Position, rotation, part.Scale), RuinDomain(part.Leg), PrismKind.Plain),
                    _ruinRoot, trail, RuinOwnerId, admitAuthoredScale: true));
            }
        }

        Domains RuinDomain(int leg) =>
            ruinDomains is { Length: > 0 } ? ruinDomains[leg % ruinDomains.Length] : Domains.Blue;

        /// <summary>The clock the wheels turn on: the network's, so every peer poses them alike.</summary>
        float RuinClock => IsSpawned && NetworkManager != null ? (float)NetworkManager.ServerTime.Time : Time.time;

        /// <summary>Every frame: pose each wheel's standing prisms at the clock's angle.</summary>
        void TickRuins()
        {
            if (_ruinWheels.Count == 0) return;
            float seconds = RuinClock;
            for (int w = 0; w < _ruinWheels.Count; w++)
            {
                var wheel = _ruinWheels[w];
                for (int i = wheel.First; i < wheel.First + wheel.Count && i < _ruinPrisms.Count; i++)
                {
                    var prism = _ruinPrisms[i];
                    if (!prism || prism.destroyed || prism.ownerID != RuinOwnerId) continue;
                    SpearlineRuins.Pose(_ruinParts[i], wheel, seconds, out var position, out var forward, out var up);
                    prism.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, up));
                }
            }
        }

        void ClearRuins()
        {
            for (int i = 0; i < _ruinPrisms.Count; i++)
            {
                var prism = _ruinPrisms[i];
                if (prism && prism.ownerID == RuinOwnerId && !EnvironmentPrismPool.TryRelease(prism))
                    Destroy(prism.gameObject);
            }
            _ruinPrisms.Clear();
            _ruinParts.Clear();
            _ruinWheels.Clear();
            _ruinLaid.Clear();
            if (_ruinRoot) Destroy(_ruinRoot.gameObject);
            _ruinRoot = null;
        }
    }
}
