using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Headlong's garden ruins in the world (HEADLONG.md §0a): every peer lays <see cref="HeadlongRuins"/>' prisms from
    /// the broadcast course, in the hedges' prism and in domain colours of their own, once a course. They are scenery
    /// with the game's own prism rules: a hull ploughs them, nothing stands them back up (the hedges do stand up; the
    /// ruins stay broken), and they are returned to the pool with the stampede. The wheel turns: its prisms are POSED
    /// here every frame from the network clock, so every peer sees the same angle (a posed prism is the shape of its
    /// owner, not a moving prism: Docs/PRISM_MOTION.md §1). Nothing here reads or writes a vessel.
    /// </summary>
    public partial class HeadlongController
    {
        const string RuinOwnerId = "HeadlongRuins";

        [Header("Garden ruins (I4)")]
        [Tooltip("The domain colours the ruins are laid in, one ruin after another round the lap, so they read apart " +
                 "from the hedges (Blue). Decoration (Yash, 2026-11-24). Empty = Blue, as the hedges.")]
        [SerializeField] Domains[] ruinDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        readonly List<SpearlineRuinPart> _ruinParts = new();
        readonly List<SpearlineRuinWheel> _ruinWheels = new();
        readonly List<bool> _ruinLaid = new();
        // One entry for each part, null where the part was left out: a wheel names its parts by index.
        readonly List<Prism> _ruinPrisms = new();
        Transform _ruinRoot;

        /// <summary>Called once the hedges and the crystals of the course are placed (the ruins keep off both).</summary>
        void LayRuins(IReadOnlyList<HeadlongHedges.Hedge> hedges, IReadOnlyList<Vector3> crystals)
        {
            HeadlongRuins.Lay(_course, Intensity, hedges, crystals, _ruinParts, _ruinWheels, _ruinLaid);
            if (_ruinParts.Count == 0 || !hedgePrefab) return;
            _ruinRoot = new GameObject("HeadlongRuins").transform;
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
                _ruinPrisms.Add(PrismTrailBuilder.LayOne(hedgePrefab,
                    new PrismLay(new SpawnPoint(part.Position, rotation, part.Scale), RuinDomain(part.Leg), PrismKind.Plain),
                    _ruinRoot, trail, RuinOwnerId, admitAuthoredScale: true));
            }
        }

        Domains RuinDomain(int leg) =>
            ruinDomains is { Length: > 0 } ? ruinDomains[leg % ruinDomains.Length] : Domains.Blue;

        /// <summary>The clock the wheel turns on: the network's, so every peer poses it alike.</summary>
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
            if (_ruinRoot && _ruinRoot.childCount == 0) Destroy(_ruinRoot.gameObject);
            _ruinRoot = null;
        }
    }
}
