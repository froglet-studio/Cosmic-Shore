using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Warpline's garden ruins in the world (WARPLINE.md §2a): every peer lays <see cref="SpearlineRuins"/>' prisms from
    /// the broadcast rings, once a course, on the hardest rung only. The generator is Spearline's, called with
    /// Warpline's own lap; nothing of it is copied here. The ruins are scenery with the game's own prism rules: a hull
    /// breaks them, a tunnel does to them what it does to any prism, nothing grows back, and they go back to the pool
    /// when the course does. The wheels turn: their prisms are POSED here every frame from the network clock, so every
    /// peer sees the same angle (a posed prism is the shape of its owner, not a moving prism: Docs/PRISM_MOTION.md §1).
    /// Nothing here reads or writes a vessel.
    /// </summary>
    public partial class WarplineController
    {
        const string RuinOwnerId = "WarplineRuins";

        [Header("Garden ruins (I4)")]
        [Tooltip("The prism every ruin is laid from: the environment prism (SpawnablePrism). Authored by " +
                 "Tools/Build/author_warpline_assets.py. Unset = no ruins, and a warning.")]
        [SerializeField] Prism ruinPrefab;

        [Tooltip("The domain colours the ruins are laid in, one ruin after another round the lap, so they read apart " +
                 "from the rings (Blue). Decoration (Yash, 2026-11-24). Empty = Blue, as the rings.")]
        [SerializeField] Domains[] ruinDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        readonly List<SpearlineRuinPart> _ruinParts = new();
        readonly List<SpearlineRuinWheel> _ruinWheels = new();
        readonly List<Prism> _ruinPrisms = new();
        Transform _ruinRoot;

        public override void OnNetworkDespawn()
        {
            ClearRuins();
            base.OnNetworkDespawn();
        }

        /// <summary>The ruins, laid on every peer from the course every peer received.</summary>
        protected override void OnCourseRaised()
        {
            ClearRuins();
            LayRuins();
        }

        // A subclass must not declare Update (GateRaceController.Update runs the race); LateUpdate is free.
        // The wheels turn from the moment the course stands, the countdown included.
        void LateUpdate() => TickRuins();

        void LayRuins()
        {
            SpearlineRuins.Lay(_course, SpearlineRuins.ForIntensity(Intensity), _ruinParts, _ruinWheels);
            if (_ruinParts.Count == 0) return;
            if (!ruinPrefab)
            {
                CSDebug.LogWarning("[Warpline] no ruin prefab on the controller - the ruins are not laid. " +
                                   "Re-run Tools/Build/author_warpline_assets.py.");
                _ruinParts.Clear();
                _ruinWheels.Clear();
                return;
            }
            _ruinRoot = new GameObject("WarplineRuins").transform;
            var trail = new Trail();
            for (int i = 0; i < _ruinParts.Count; i++)
            {
                var part = _ruinParts[i];
                var rotation = Quaternion.LookRotation(part.Forward, part.Up);
                _ruinPrisms.Add(PrismTrailBuilder.LayOne(ruinPrefab,
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
            if (_ruinRoot) Destroy(_ruinRoot.gameObject);
            _ruinRoot = null;
        }
    }
}
