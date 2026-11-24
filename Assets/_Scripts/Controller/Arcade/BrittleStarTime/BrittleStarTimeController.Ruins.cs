using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Brittle Star Time's garden ruins in the world (BRITTLESTARTIME.md section 0a; every peer): at I4,
    /// <see cref="SpearlineRuins"/>' prisms laid round the flown line (<see cref="BrittleStarTimeRuins"/>) from the
    /// broadcast course, in the let-go posts' prism and in domain colours of their own. Scenery with the game's own prism
    /// rules: a hull breaks them, nothing grows back, and they go back to the pool with the course. A wheel's prisms are
    /// POSED every frame from the network clock, so every peer sees the same angle (a posed prism is the shape of its
    /// owner, not a moving prism: Docs/PRISM_MOTION.md section 1). Nothing here reads or writes a vessel.
    /// </summary>
    public partial class BrittleStarTimeController
    {
        const string RuinOwnerId = "BrittleStarTimeRuins";

        [Header("Garden ruins (I4)")]
        [Tooltip("The domain colours the ruins are laid in, one ruin after another round the lap, so they read apart " +
                 "from the let-go posts (Blue). Decoration (Yash, 2026-11-24). Empty = Blue, as the posts.")]
        [SerializeField] Domains[] ruinDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        readonly List<SpearlineRuinPart> _ruinParts = new();
        readonly List<SpearlineRuinWheel> _ruinWheels = new();
        readonly List<Prism> _ruinPrisms = new();
        Transform _ruinRoot;

        void LayRuins()
        {
            ClearRuins();
            if (!postPrefab || _course.Count == 0) return;
            BrittleStarTimeRuins.Lay(_course, Intensity, BrittleStarTimeCourse.CornerRadiusAt(TopSpeed),
                                     BrittleStarTimeLine.Marks(_course, TopSpeed), _ruinParts, _ruinWheels);
            if (_ruinParts.Count == 0) return;
            _ruinRoot = new GameObject($"{ModeName}Ruins").transform;
            var trail = new Trail();
            for (int i = 0; i < _ruinParts.Count; i++)
            {
                var part = _ruinParts[i];
                var rotation = Quaternion.LookRotation(part.Forward, part.Up);
                _ruinPrisms.Add(PrismTrailBuilder.LayOne(postPrefab,
                    new PrismLay(new SpawnPoint(part.Position, rotation, part.Scale), RuinDomain(part.Leg), PrismKind.Plain),
                    _ruinRoot, trail, RuinOwnerId, admitAuthoredScale: true));
            }
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[{ModeName}] intensity {Intensity}: {_ruinPrisms.Count} ruin prisms.");
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
