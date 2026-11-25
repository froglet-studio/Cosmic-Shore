using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Perch Run's garden ruins in the world (PERCH_RUN.md §3a): every peer lays <see cref="PerchRunRuins"/>' prisms
    /// from the broadcast rings, in the coral's prism and in domain colours of their own, once a course. They are
    /// scenery with the game's own prism rules: plain prisms, nothing grows back, and they are returned to the pool
    /// with the coral. Nothing turns: Spearline's wheel needs a longer leg than this course has
    /// (PerchRunRuinsTests.No_wheel_fits_a_perch_run_leg), so nothing is posed per frame. Nothing here reads or writes
    /// a vessel.
    /// </summary>
    public partial class PerchRunController
    {
        const string RuinOwnerId = "PerchRunRuins";

        [Header("Garden ruins (I4)")]
        [Tooltip("The domain colours the ruins are laid in, one ruin after another along the course, so they read apart " +
                 "from the rings and the coral (Blue). Decoration (Yash, 2026-11-24). Empty = Blue, as the coral.")]
        [SerializeField] Domains[] ruinDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        readonly List<RaceGate> _ruinLap = new();
        readonly List<SpearlineRuinPart> _ruinParts = new();
        readonly List<Prism> _ruinPrisms = new();
        Transform _ruinRoot;

        void LayRuins()
        {
            PerchRunRuins.Lay(_course, Intensity, _ruinLap, _ruinParts, null);
            if (_ruinParts.Count == 0 || !coralPrefab) return;
            _ruinRoot = new GameObject("PerchRunRuins").transform;
            var trail = new Trail();
            int ruin = -1, lastLeg = -1;
            for (int i = 0; i < _ruinParts.Count; i++)
            {
                var part = _ruinParts[i];
                if (part.Leg != lastLeg)
                {
                    lastLeg = part.Leg;
                    ruin++;
                }
                var rotation = Quaternion.LookRotation(part.Forward, part.Up);
                var prism = PrismTrailBuilder.LayOne(coralPrefab,
                    new PrismLay(new SpawnPoint(part.Position, rotation, part.Scale), RuinDomain(ruin), PrismKind.Plain),
                    _ruinRoot, trail, RuinOwnerId, admitAuthoredScale: true);
                if (prism) _ruinPrisms.Add(prism);
            }
        }

        /// <summary>The colour of the <paramref name="ruin"/>-th ruin along the course.</summary>
        Domains RuinDomain(int ruin) =>
            ruinDomains is { Length: > 0 } ? ruinDomains[ruin % ruinDomains.Length] : Domains.Blue;

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
            _ruinLap.Clear();
            if (_ruinRoot) Destroy(_ruinRoot.gameObject);
            _ruinRoot = null;
        }
    }
}
