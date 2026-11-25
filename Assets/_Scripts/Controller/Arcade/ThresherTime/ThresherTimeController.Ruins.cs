using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Thresher Time's garden ruins in the world (THRESHERTIME.md §0b): every peer lays <see cref="ThresherTimeRuins"/>'
    /// prisms from the broadcast rings, in the Belfry's prism and in domain colours of their own, once a course. They are
    /// scenery with the game's own prism rules: a hull, a ball or a blast breaks them and nothing grows back. The wheel
    /// turns: its prisms are POSED here every frame off the Belfry's shared clock, so every peer sees the same angle (a
    /// posed prism is the shape of its owner, not a moving prism: Docs/PRISM_MOTION.md §1).
    /// Nothing here reads or writes a vessel.
    /// </summary>
    public partial class ThresherTimeController
    {
        const string RuinOwnerId = "ThresherTimeRuins";

        [Header("Garden ruins (I4)")]
        [Tooltip("The domain colours the ruins are laid in, one ruin after another round the lap, so they read apart " +
                 "from the Belfry's rings, chains and maces (Blue). Decoration (Yash, 2026-11-24). Empty = Blue, as the Belfry.")]
        [SerializeField] Domains[] ruinDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        readonly List<SpearlineRuinPart> _ruinParts = new();
        readonly List<SpearlineRuinWheel> _ruinWheels = new();
        readonly List<bool> _ruinStands = new();
        readonly List<Prism> _ruinPrisms = new();
        readonly Trail _ruinTrail = new();
        Transform _ruinRoot;

        /// <summary>After the Belfry is raised: the ruins keep out of its maces' sweep.</summary>
        void LayRuins()
        {
            var prefab = belfryPrismPrefab ? belfryPrismPrefab : nestPrefab;
            if (!prefab) return;
            ThresherTimeRuins.Lay(_course, Intensity, _belfry, _ruinParts, _ruinWheels, _ruinStands);
            if (_ruinParts.Count == 0) return;
            _ruinRoot = new GameObject("ThresherTimeRuins").transform;
            for (int i = 0; i < _ruinParts.Count; i++)
            {
                if (!_ruinStands[i]) { _ruinPrisms.Add(null); continue; }
                var part = _ruinParts[i];
                var rotation = Quaternion.LookRotation(part.Forward, part.Up);
                _ruinPrisms.Add(PrismTrailBuilder.LayOne(prefab,
                    new PrismLay(new SpawnPoint(part.Position, rotation, part.Scale), RuinDomain(part.Leg), PrismKind.Plain),
                    _ruinRoot, _ruinTrail, RuinOwnerId, admitAuthoredScale: true));
            }
        }

        Domains RuinDomain(int leg) =>
            ruinDomains is { Length: > 0 } ? ruinDomains[leg % ruinDomains.Length] : Domains.Blue;

        /// <summary>Every frame: pose each wheel's standing prisms at the shared clock's angle.</summary>
        void TickRuins()
        {
            if (_ruinWheels.Count == 0) return;
            float seconds = SharedTime;
            for (int w = 0; w < _ruinWheels.Count; w++)
            {
                var wheel = _ruinWheels[w];
                for (int i = wheel.First; i < wheel.First + wheel.Count && i < _ruinPrisms.Count; i++)
                {
                    var prism = _ruinPrisms[i];
                    if (!Holds(prism, RuinOwnerId)) continue;
                    SpearlineRuins.Pose(_ruinParts[i], wheel, seconds, out var position, out var forward, out var up);
                    prism.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, up));
                    prism.NotifyPositionChanged();
                }
            }
        }

        void ClearRuins()
        {
            for (int i = 0; i < _ruinPrisms.Count; i++)
                if (Holds(_ruinPrisms[i], RuinOwnerId))
                    _ruinPrisms[i].Damage(Vector3.zero, Domains.Blue, string.Empty, devastate: true);
            _ruinPrisms.Clear();
            _ruinParts.Clear();
            _ruinWheels.Clear();
            _ruinStands.Clear();
            if (_ruinRoot) Destroy(_ruinRoot.gameObject);
            _ruinRoot = null;
        }
    }
}
