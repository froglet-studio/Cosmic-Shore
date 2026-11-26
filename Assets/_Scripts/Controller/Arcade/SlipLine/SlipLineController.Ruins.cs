using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Slip Line's ruins in the world (SLIP_LINE.md §3a): every peer lays <see cref="SlipLineRuins"/>' prisms from the
    /// broadcast rings and its own rails, in the rails' prism, once a course, at I4 only. They are scenery with the
    /// game's own prism rules: a hull breaks them, nothing grows back, and they go back to the pool with the rails.
    /// A wheel, where one stands, is POSED every frame from the network clock, so every peer sees the same angle (a
    /// posed prism is the shape of its owner, not a moving prism: Docs/PRISM_MOTION.md §1).
    /// Nothing here reads or writes a vessel.
    /// </summary>
    public partial class SlipLineController
    {
        const string RuinOwnerId = "SlipLineRuins";

        [Header("Ruins (I4)")]
        [Tooltip("The domain colours the ruins are laid in, one ruin after another down the course. Blue is the one " +
                 "colour no rail wears (the rails are Ruby, Jade and Gold), so a ruin never reads as a rail. " +
                 "Decoration (Yash, 2026-11-24). Empty = Blue.")]
        [SerializeField] Domains[] ruinDomains = { Domains.Blue };

        readonly List<RaceGate> _ruinLap = new();
        readonly List<SpearlineRuinPart> _ruinParts = new();
        readonly List<SpearlineRuinWheel> _ruinWheels = new();
        readonly List<Prism> _ruinPrisms = new();
        Transform _ruinRoot;

        void LayRuins()
        {
            SlipLineRuins.Lay(_course, _rails, Intensity, _ruinLap, _ruinParts, _ruinWheels);
            if (_ruinParts.Count == 0 || !railPrefab) return;
            _ruinRoot = new GameObject("SlipLineRuins").transform;
            int ruin = -1, leg = -1;
            for (int i = 0; i < _ruinParts.Count; i++)
            {
                var part = _ruinParts[i];
                if (part.Leg != leg) { leg = part.Leg; ruin++; }
                var rotation = Quaternion.LookRotation(part.Forward, part.Up);
                // a trail of its own for every prism: one prism is no trail to slip down (minSlipTrailLength), so a
                // ruin is never a ride, whatever colour it is given
                _ruinPrisms.Add(PrismTrailBuilder.LayOne(railPrefab,
                    new PrismLay(new SpawnPoint(part.Position, rotation, part.Scale), RuinDomain(ruin), PrismKind.Plain),
                    _ruinRoot, new Trail(), RuinOwnerId, admitAuthoredScale: true));
            }
        }

        Domains RuinDomain(int ruin) =>
            ruinDomains is { Length: > 0 } ? ruinDomains[ruin % ruinDomains.Length] : Domains.Blue;

        /// <summary>The clock the wheels turn on: the network's, so every peer poses them alike.</summary>
        float RuinClock => IsSpawned && NetworkManager != null ? (float)NetworkManager.ServerTime.Time : Time.time;

        /// <summary>Every frame, every peer: pose each wheel's standing prisms at the clock's angle.</summary>
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
                if (prism && !prism.destroyed && prism.ownerID == RuinOwnerId && !EnvironmentPrismPool.TryRelease(prism))
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
