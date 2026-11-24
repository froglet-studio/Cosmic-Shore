using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Waystation's garden ruins in the world (WAYSTATION.md, "Garden ruins"): every peer lays
    /// <see cref="WaystationRuins"/>' prisms from the broadcast course, in the walls' prism and in domain colours of
    /// their own, once a course, at I4 only. They are scenery with the game's own prism rules: a hull breaks them,
    /// nothing grows back, and they are returned to the pool with the walls. A wheel turns: its prisms are POSED here
    /// every frame from the network clock, so every peer sees the same angle (a posed prism is the shape of its owner,
    /// not a moving prism: Docs/PRISM_MOTION.md §1). Nothing here reads or writes a vessel.
    /// </summary>
    public partial class WaystationController
    {
        const string RuinOwnerId = "WaystationRuins";

        [Header("Garden ruins (I4)")]
        [Tooltip("The domain colours the ruins are laid in, one ruin after another along the course, so they read " +
                 "apart from the towers' walls and lanterns (Blue). Decoration (Yash, 2026-11-24). Empty = Blue, as " +
                 "the walls.")]
        [SerializeField] Domains[] ruinDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        readonly List<SpearlineRuinPart> _ruinParts = new();
        readonly List<SpearlineRuinWheel> _ruinWheels = new();
        readonly List<Prism> _ruinPrisms = new();
        Transform _ruinRoot;

        /// <summary>Lay the ruins of the course this peer was sent. Called once the walls' prism is known to be set.</summary>
        void LayRuins()
        {
            _ruinParts.Clear();
            _ruinWheels.Clear();
            WaystationRuins.Lay(_course, Intensity, _ruinParts, _ruinWheels);
            if (_ruinParts.Count == 0) return;
            _ruinRoot = new GameObject($"{ModeName}Ruins").transform;
            _ruinRoot.SetParent(transform, false);   // the rings' frame, as the walls
            var trail = new Trail();
            for (int i = 0; i < _ruinParts.Count; i++)
            {
                var part = _ruinParts[i];
                var rotation = Quaternion.LookRotation(part.Forward, part.Up);
                _ruinPrisms.Add(PrismTrailBuilder.LayOne(wallPrefab,
                    new PrismLay(new SpawnPoint(part.Position, rotation, part.Scale), RuinDomain(part.Leg), PrismKind.Plain),
                    _ruinRoot, trail, RuinOwnerId, admitAuthoredScale: true));
            }
        }

        Domains RuinDomain(int ruin) =>
            ruinDomains is { Length: > 0 } ? ruinDomains[ruin % ruinDomains.Length] : Domains.Blue;

        /// <summary>The clock the wheels turn on: the network's, so every peer poses them alike.</summary>
        float RuinClock => IsSpawned && NetworkManager != null ? (float)NetworkManager.ServerTime.Time : Time.time;

        /// <summary>Every frame: pose each wheel's standing prisms at the clock's angle, in the course's frame.</summary>
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
                    prism.transform.SetLocalPositionAndRotation(position, Quaternion.LookRotation(forward, up));
                }
            }
        }

        void ClearRuins()
        {
            for (int i = 0; i < _ruinPrisms.Count; i++)
            {
                var prism = _ruinPrisms[i];
                if (!prism || prism.destroyed || prism.ownerID != RuinOwnerId) continue;
                if (!EnvironmentPrismPool.TryRelease(prism)) Destroy(prism.gameObject);
            }
            _ruinPrisms.Clear();
            _ruinParts.Clear();
            _ruinWheels.Clear();
            if (_ruinRoot && _ruinRoot.childCount == 0) Destroy(_ruinRoot.gameObject);
            _ruinRoot = null;
        }
    }
}
