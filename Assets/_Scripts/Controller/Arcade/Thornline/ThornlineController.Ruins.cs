using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Thornline's ruins in the world (THORNLINE.md §2a): every peer lays <see cref="ThornlineRuins"/>' prisms from the
    /// thorn chain, in the thorns' prism and in domain colours of their own, once a course. They are scenery with the
    /// game's own prism rules: a hull or a lance breaks them, a sling aimed at one docks on it, nothing grows back, and
    /// they go back to the pool with the thorns. The wheel turns: its prisms are POSED here every frame from the
    /// network clock, so every peer sees the same angle (a posed prism is the shape of its owner, not a moving prism:
    /// Docs/PRISM_MOTION.md §1). Nothing here reads or writes a vessel.
    /// The serialized field (<c>ruinDomains</c>) is declared in ThornlineController.cs with the others.
    /// </summary>
    public partial class ThornlineController
    {
        const string RuinOwnerId = "ThornlineRuins";

        readonly List<RaceGate> _ruinLanes = new();
        readonly List<int> _ruinLegs = new();
        readonly List<SpearlineRuinPart> _ruinParts = new();
        readonly List<SpearlineRuinWheel> _ruinWheels = new();
        readonly List<Prism> _ruinPrisms = new();
        Transform _ruinRoot;

        /// <summary>Lay the ruins round <paramref name="thorns"/> (world). Nothing below I4, or with no thorn prefab.</summary>
        void LayRuins(List<Vector3> thorns)
        {
            ThornlineRuins.Lay(thorns, Intensity, _ruinLanes, _ruinLegs, _ruinParts, _ruinWheels);
            if (_ruinParts.Count == 0 || !thornPrefab) return;
            _ruinRoot = new GameObject("ThornlineRuins").transform;
            var trail = new Trail();
            for (int i = 0; i < _ruinParts.Count; i++)
            {
                var part = _ruinParts[i];
                var rotation = Quaternion.LookRotation(part.Forward, part.Up);
                _ruinPrisms.Add(PrismTrailBuilder.LayOne(thornPrefab,
                    new PrismLay(new SpawnPoint(part.Position, rotation, part.Scale), RuinDomain(part.Leg), PrismKind.Plain),
                    _ruinRoot, trail, RuinOwnerId, admitAuthoredScale: true));
            }
        }

        Domains RuinDomain(int leg) =>
            ruinDomains is { Length: > 0 }
                ? ruinDomains[ThornlineRuins.Ordinal(_ruinLegs, leg) % ruinDomains.Length]
                : Domains.Blue;

        /// <summary>The clock the wheel turns on: the network's, so every peer poses it alike.</summary>
        float RuinClock => IsSpawned && NetworkManager != null ? (float)NetworkManager.ServerTime.Time : Time.time;

        /// <summary>Every frame: pose each wheel's standing prisms at the clock's angle.</summary>
        void TickRuins()
        {
            if (_ruinWheels.Count == 0 || _ruinPrisms.Count == 0) return;
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
            _ruinLanes.Clear();
            _ruinLegs.Clear();
            if (_ruinRoot) Destroy(_ruinRoot.gameObject);
            _ruinRoot = null;
        }
    }
}
