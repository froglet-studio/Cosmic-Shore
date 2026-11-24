using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Decoy Run's garden ruins in the world (DECOY_RUN.md §2a): every peer lays <see cref="DecoyRunRuins"/>' prisms
    /// from the broadcast rings, once a course, in the chalk's prism and in domain colours of their own. They are
    /// scenery with the game's own prism rules (plain mass: a hull breaks it, the net drags it, nothing grows back).
    /// Nothing here reads or writes a vessel.
    /// </summary>
    public partial class DecoyRunController
    {
        const string RuinOwnerId = "DecoyRunRuins";

        [Header("Garden ruins (I4)")]
        [Tooltip("The domain colours the ruins are laid in, one ruin after another along the course, so they read " +
                 "apart from the chalk (Blue). Decoration (Yash, 2026-11-24). Empty = Blue, as the chalk.")]
        [SerializeField] Domains[] ruinDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        readonly List<RaceGate> _ruinLine = new();
        readonly List<SpearlineRuinPart> _ruinParts = new();
        readonly List<Prism> _ruinPrisms = new();
        Transform _ruinRoot;

        void LayRuins()
        {
            _ruinParts.Clear();
            DecoyRunRuins.Lay(_course, _settings, Intensity, _ruinLine, _ruinParts);
            if (_ruinParts.Count == 0) return;
            _ruinRoot = new GameObject("DecoyRunRuins").transform;
            var trail = new Trail();
            for (int i = 0; i < _ruinParts.Count; i++)
            {
                var part = _ruinParts[i];
                var rotation = Quaternion.LookRotation(part.Forward, part.Up);
                var prism = PrismTrailBuilder.LayOne(chalkPrefab,
                    new PrismLay(new SpawnPoint(part.Position, rotation, part.Scale),
                                 RuinDomain(DecoyRunRuins.CourseLeg(part)), PrismKind.Plain),
                    _ruinRoot, trail, RuinOwnerId, admitAuthoredScale: true);
                if (prism) _ruinPrisms.Add(prism);
            }
        }

        Domains RuinDomain(int leg) =>
            ruinDomains is { Length: > 0 } ? ruinDomains[leg % ruinDomains.Length] : Domains.Blue;

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
            if (_ruinRoot) Destroy(_ruinRoot.gameObject);
            _ruinRoot = null;
        }
    }
}
