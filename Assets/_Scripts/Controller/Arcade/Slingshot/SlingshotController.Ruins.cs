using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Slingshot's garden ruins in the world (SLINGSHOT.md, "The ruins"): every peer lays <see cref="SlingshotRuins"/>'
    /// prisms from the broadcast rings, in domain colours of their own, once a course. They are scenery with the game's
    /// own prism rules: a hull breaks them, nothing grows back, and they are returned to the pool with the course. The
    /// wheel turns: its prisms are POSED here every frame from the network clock, so every peer sees the same angle (a
    /// posed prism is the shape of its owner, not a moving prism: Docs/PRISM_MOTION.md §1).
    /// Nothing here reads or writes a vessel.
    /// </summary>
    public partial class SlingshotController
    {
        const string RuinOwnerId = "SlingshotRuins";

        [Header("Garden ruins (I4)")]
        [Tooltip("The prism every ruin is laid in (SpawnablePrism). Empty: no ruins are laid.")]
        [SerializeField] Prism ruinPrefab;

        [Tooltip("The domain colours the ruins are laid in, one ruin after another round the lap, so they read apart " +
                 "from the Orrery's moons, belts and tails (Blue). Decoration (Yash, 2026-11-24). Empty = Blue, as the Orrery.")]
        [SerializeField] Domains[] ruinDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        readonly List<SpearlineRuinPart> _ruinParts = new();
        readonly List<SpearlineRuinWheel> _ruinWheels = new();
        readonly List<Prism> _ruinPrisms = new();
        Transform _ruinRoot;

        void LayRuins()
        {
            SlingshotRuins.Lay(_course, Intensity, _ruinParts, _ruinWheels);
            if (_ruinParts.Count == 0) return;
            if (!ruinPrefab)
            {
                CSDebug.LogWarning("[Slingshot] no ruin prefab on the controller - the I4 ruins are not laid.");
                _ruinParts.Clear();
                _ruinWheels.Clear();
                return;
            }
            _ruinRoot = new GameObject("SlingshotRuins").transform;
            var trail = new Trail();
            // The colour turns with each RUIN, not each leg: legs too short for a ruin would skip a colour
            // (the first I4 race in the editor laid Jade and Gold and no Ruby).
            int ruin = -1, lastLeg = -1;
            for (int i = 0; i < _ruinParts.Count; i++)
            {
                var part = _ruinParts[i];
                if (part.Leg != lastLeg)
                {
                    ruin++;
                    lastLeg = part.Leg;
                }
                var rotation = Quaternion.LookRotation(part.Forward, part.Up);
                _ruinPrisms.Add(PrismTrailBuilder.LayOne(ruinPrefab,
                    new PrismLay(new SpawnPoint(part.Position, rotation, part.Scale), RuinDomain(ruin), PrismKind.Plain),
                    _ruinRoot, trail, RuinOwnerId, admitAuthoredScale: true));
            }
        }

        Domains RuinDomain(int ruin) =>
            ruinDomains is { Length: > 0 } ? ruinDomains[ruin % ruinDomains.Length] : Domains.Blue;

        /// <summary>The clock the wheel turns on: the network's, so every peer poses it alike.</summary>
        float RuinClock => IsSpawned && NetworkManager != null ? (float)NetworkManager.ServerTime.Time : Time.time;

        /// <summary>Every frame: pose each wheel's standing prisms at the clock's angle.</summary>
        void LateUpdate()
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
                if (prism && prism.ownerID == RuinOwnerId && !EnvironmentPrismPool.TryRelease(prism)) Destroy(prism.gameObject);
            }
            _ruinPrisms.Clear();
            _ruinParts.Clear();
            _ruinWheels.Clear();
            if (_ruinRoot) Destroy(_ruinRoot.gameObject);
            _ruinRoot = null;
        }
    }
}
