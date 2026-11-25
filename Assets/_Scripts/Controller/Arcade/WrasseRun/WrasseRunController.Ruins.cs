using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Wrasse Run's garden ruins in the world (WRASSERUN.md §3a): every peer lays <see cref="SpearlineRuins"/>' prisms
    /// round the broadcast lap, in the environment prism and in domain colours, once a course, at I4 only. The
    /// generator is Spearline's, called as it is: this file is only the hook. The ruins are scenery with the game's own
    /// prism rules: a hull breaks them, nothing grows back, and they are returned to the pool when the course goes. The
    /// wheel turns: its prisms are POSED here every frame from the network clock, so every peer sees the same angle (a
    /// posed prism is the shape of its owner, not a moving prism: Docs/PRISM_MOTION.md §1).
    /// Nothing here reads or writes a vessel, a kite or a ring.
    /// </summary>
    public partial class WrasseRunController
    {
        const string RuinOwnerId = "WrasseRunRuins";

        [Header("Garden ruins (I4)")]
        [Tooltip("The prism every ruin is laid from: the environment prism (SpawnablePrism). Authored by " +
                 "Tools/Build/author_wrasse_run_assets.py. Unset = no ruins, and a warning at I4.")]
        [SerializeField] Prism ruinPrefab;

        [Tooltip("The domain colours the ruins are laid in, one ruin after another round the lap, so they read apart " +
                 "from the lit ring (Lime). Decoration (Yash, 2026-11-24). Empty = Blue, the neutral colour.")]
        [SerializeField] Domains[] ruinDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        readonly List<SpearlineRuinPart> _ruinParts = new();
        readonly List<SpearlineRuinWheel> _ruinWheels = new();
        readonly List<Prism> _ruinPrisms = new();
        Transform _ruinRoot;

        /// <summary>The ruins, laid on every peer from the course every peer received.</summary>
        protected override void OnCourseRaised()
        {
            ClearRuins();
            LayRuins();
        }

        public override void OnNetworkDespawn()
        {
            ClearRuins();
            base.OnNetworkDespawn();
        }

        // A subclass must not declare Update (GateRaceController.Update runs the race); LateUpdate is free.
        // The wheel turns from the moment the course stands, the countdown included.
        void LateUpdate()
        {
            if (IsSpawned) TickRuins();
        }

        void LayRuins()
        {
            _ruinParts.Clear();
            _ruinWheels.Clear();
            SpearlineRuins.Lay(_course, SpearlineRuins.ForIntensity(Intensity), _ruinParts, _ruinWheels);
            if (_ruinParts.Count == 0) return;
            if (!ruinPrefab)
            {
                CSDebug.LogWarning("[Wrasse Run] no ruin prefab on the controller - the ruins are not laid.");
                _ruinParts.Clear();
                _ruinWheels.Clear();
                return;
            }
            _ruinRoot = new GameObject("WrasseRunRuins").transform;
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
            if (_ruinRoot) Destroy(_ruinRoot.gameObject);
            _ruinRoot = null;
        }
    }
}
