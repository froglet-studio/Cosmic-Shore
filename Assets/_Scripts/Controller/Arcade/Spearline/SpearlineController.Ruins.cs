using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Spearline's garden ruins in the world (SPEARLINE.md §2a): every peer lays <see cref="SpearlineRuins"/>' prisms
    /// from the broadcast rings, in the seals' prism and domain, once a course. They are scenery with the game's own
    /// prism rules: a hull or a bill breaks them, nothing grows back, and they are returned to the pool with the seals.
    /// Nothing here reads or writes a vessel.
    /// </summary>
    public partial class SpearlineController
    {
        const string RuinOwnerId = "SpearlineRuins";

        readonly List<SpearlineRuinPart> _ruinParts = new();
        readonly List<Prism> _ruinPrisms = new();
        Transform _ruinRoot;

        void LayRuins()
        {
            _ruinParts.Clear();
            SpearlineRuins.Lay(_course, SpearlineRuins.ForIntensity(Intensity), _ruinParts);
            if (_ruinParts.Count == 0) return;
            _ruinRoot = new GameObject("SpearlineRuins").transform;
            var trail = new Trail();
            for (int i = 0; i < _ruinParts.Count; i++)
            {
                var part = _ruinParts[i];
                var rotation = Quaternion.LookRotation(part.Forward, part.Up);
                _ruinPrisms.Add(PrismTrailBuilder.LayOne(sealPrefab,
                    new PrismLay(new SpawnPoint(part.Position, rotation, part.Scale), Domains.Blue, PrismKind.Plain),
                    _ruinRoot, trail, RuinOwnerId, admitAuthoredScale: true));
            }
        }

        void ClearRuins()
        {
            for (int i = 0; i < _ruinPrisms.Count; i++)
                if (_ruinPrisms[i] && _ruinPrisms[i].ownerID == RuinOwnerId) Release(_ruinPrisms[i]);
            _ruinPrisms.Clear();
            _ruinParts.Clear();
            if (_ruinRoot) Destroy(_ruinRoot.gameObject);
            _ruinRoot = null;
        }
    }
}
