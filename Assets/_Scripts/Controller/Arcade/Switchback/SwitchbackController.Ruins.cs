using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Switchback's garden ruins in the world (SWITCHBACK.md "The I4 ruins"): every peer lays
    /// <see cref="SwitchbackRuins"/>' prisms from the broadcast course and its Blast Run layout, in the Blast Run
    /// prism and in domain colours of their own, once a course. They are scenery with the game's own prism rules: a
    /// hull or a blast breaks them and nothing grows back. Nothing here reads or writes a vessel.
    /// </summary>
    public partial class SwitchbackController
    {
        const string RuinOwnerId = "SwitchbackRuins";

        [Header("Garden ruins (I4)")]
        [Tooltip("The domain colours the ruins are laid in, one ruin after another along the course, so they read apart " +
                 "from Blast Run's doors and block fields (Blue). Decoration (Yash, 2026-11-24). Empty = Blue, as Blast Run.")]
        [SerializeField] Domains[] ruinDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        readonly List<SpearlineRuinPart> _ruinParts = new();
        readonly List<Prism> _ruinPrisms = new();
        Transform _ruinRoot;

        /// <summary>Does this mode lay the ruins? They are fitted round the gauntlet, so a mode that flies this
        /// controller without it (Scarab Rally) lays none unless it says otherwise.</summary>
        protected virtual bool LaysRuins => BlastRunGauntlet;

        void LayRuins(SwitchbackFeatureLayout layout)
        {
            ClearRuins();
            if (!LaysRuins) return;
            SwitchbackRuins.Lay(_course, Intensity, layout, _ruinParts);
            if (_ruinParts.Count == 0) return;
            var prefab = featuresPrefab ? featuresPrefab.PrismPrefab : null;
            if (!prefab)
            {
                CSDebug.LogWarning("[Switchback] no Blast Run prism to lay the ruins from - the I4 ruins are not laid.");
                _ruinParts.Clear();
                return;
            }
            _ruinRoot = new GameObject("SwitchbackRuins").transform;
            _ruinRoot.SetParent(transform, false);
            var trail = new Trail();
            int ruin = -1, leg = int.MinValue;
            for (int i = 0; i < _ruinParts.Count; i++)
            {
                var part = _ruinParts[i];
                if (part.Leg != leg)
                {
                    leg = part.Leg;
                    ruin++;
                }
                var rotation = Quaternion.LookRotation(part.Forward, part.Up);
                _ruinPrisms.Add(PrismTrailBuilder.LayOne(prefab,
                    new PrismLay(new SpawnPoint(part.Position, rotation, part.Scale), RuinDomain(ruin), PrismKind.Plain),
                    _ruinRoot, trail, RuinOwnerId, admitAuthoredScale: true));
            }
        }

        Domains RuinDomain(int ruin) =>
            ruinDomains is { Length: > 0 } ? ruinDomains[ruin % ruinDomains.Length] : Domains.Blue;

        void ClearRuins()
        {
            for (int i = 0; i < _ruinPrisms.Count; i++)
            {
                var prism = _ruinPrisms[i];
                if (prism && prism.ownerID == RuinOwnerId && !EnvironmentPrismPool.TryRelease(prism)) Destroy(prism.gameObject);
            }
            _ruinPrisms.Clear();
            _ruinParts.Clear();
            if (_ruinRoot) Destroy(_ruinRoot.gameObject);
            _ruinRoot = null;
        }
    }
}
