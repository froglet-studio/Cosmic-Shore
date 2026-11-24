using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Grizzly Time's I4 ruins in the world (GRIZZLYTIME.md §0.2): every peer lays <see cref="GrizzlyTimeRuins"/>'
    /// prisms from the broadcast rings, in the kegs' prism and in domain colours of their own, once a course. They are
    /// scenery with the game's own prism rules: a hull, a bomb or a Rush breaks them, nothing grows back, and they go
    /// back to the pool with the course. The wheels turn: their prisms are POSED every frame from the network clock,
    /// so every peer sees the same angle (a posed prism is the shape of its owner, not a moving prism:
    /// Docs/PRISM_MOTION.md §1). Nothing here reads or writes a vessel.
    /// </summary>
    public partial class GrizzlyTimeController
    {
        const string RuinOwnerId = "GrizzlyTimeRuins";

        [Header("Ruins (I4)")]
        [Tooltip("The domain colours the ruins are laid in, one ruin after another round the lap, so they read apart " +
                 "from the kegs and their chalk (Blue). Decoration (Yash, 2026-11-24). Empty = Blue, as the kegs.")]
        [SerializeField] Domains[] ruinDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        readonly List<SpearlineRuinPart> _ruinParts = new();
        readonly List<SpearlineRuinWheel> _ruinWheels = new();
        readonly List<Prism> _ruinPrisms = new();
        Transform _ruinRoot;

        void LayRuins()
        {
            _ruinParts.Clear();
            _ruinWheels.Clear();
            if (!kegPrefab) return;
            GrizzlyTimeRuins.Lay(_course, Intensity, _ruinParts, _ruinWheels);
            if (_ruinParts.Count == 0) return;
            _ruinRoot = new GameObject("GrizzlyTimeRuins").transform;
            var trail = new Trail();
            for (int i = 0; i < _ruinParts.Count; i++)
            {
                var part = _ruinParts[i];
                var rotation = Quaternion.LookRotation(part.Forward, part.Up);
                _ruinPrisms.Add(PrismTrailBuilder.LayOne(kegPrefab,
                    new PrismLay(new SpawnPoint(part.Position, rotation, part.Scale), RuinDomain(part.Leg), PrismKind.Plain),
                    _ruinRoot, trail, RuinOwnerId, admitAuthoredScale: true));
            }
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[GrizzlyTime] {_ruinPrisms.Count} ruin prisms, {_ruinWheels.Count} turning wheel(s).");
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
