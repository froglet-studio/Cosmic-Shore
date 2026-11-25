using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Skein's garden ruins in the world (SKEIN.md section 20): every peer lays <see cref="SkeinRuins"/>' prisms from
    /// the broadcast rings and the arena's own cable, in the rail's prism, once a course. They are scenery with the
    /// game's own prism rules: a hull breaks them, nothing grows back, and they are returned to the pool with the
    /// course.
    ///
    /// <para><b>A ruin is never a rail.</b> Each ruin prism is laid into a trail of its own, so the game reads it as a
    /// lone prism (<c>PrismscapeTopology.DimensionOf</c>: a trail of one is a Singleton), which no hull rides: an
    /// Urchin that touches one flies on. Laid into one shared trail they would be a rail that jumps about the ruin,
    /// and declared a Volume they would be the pond SKEIN.md section 9 keeps out of this race.</para>
    ///
    /// <para>A wheel's prisms are POSED here every frame from the network clock, so every peer sees the same angle (a
    /// posed prism is the shape of its owner, not a moving prism: Docs/PRISM_MOTION.md §1). Nothing here reads or
    /// writes a vessel.</para>
    /// </summary>
    public partial class SkeinController
    {
        const string RuinOwnerId = "SkeinRuins";

        [Header("Garden ruins (I4)")]
        [Tooltip("The domain colours the ruins are laid in, one ruin after another along the race. Blue: the rails are " +
                 "Jade, Ruby and Gold and a pilot's own colour is the fast one, so a ruin in one of those would read as " +
                 "a rail and would cost one team less to break than another. Decoration (Yash, 2026-11-24). " +
                 "Empty = Blue.")]
        [SerializeField] Domains[] ruinDomains = { Domains.Blue };

        readonly List<SpearlineRuinPart> _ruinParts = new();
        readonly List<SpearlineRuinWheel> _ruinWheels = new();
        readonly List<bool> _ruinLaid = new();
        readonly List<Vector3> _ruinKeepOff = new();
        // One entry for each part, null where the part was left out: a wheel names its parts by index.
        readonly List<Prism> _ruinPrisms = new();
        Transform _ruinRoot;

        /// <summary>Every peer, once the rings stand: the ruins of this course.</summary>
        protected override void OnCourseRaised()
        {
            ClearRuins();
            LayRuins();
        }

        void LayRuins()
        {
            if (SpearlineRuins.ForIntensity(Intensity).PerLap <= 0) return;
            if (!TryResolveArena(out var arena, out _) || !arena.RailPrism) return;
            if (!SkeinRuins.TryKeepOff(arena.CableSeed, arena.CourseSettings, ResolveCellCentre(), _ruinKeepOff)) return;

            SkeinRuins.Lay(_course, Intensity, _ruinKeepOff, _ruinParts, _ruinWheels, _ruinLaid);
            _ruinKeepOff.Clear();
            if (_ruinParts.Count == 0) return;

            _ruinRoot = new GameObject("SkeinRuins").transform;
            int laid = 0;
            for (int i = 0; i < _ruinParts.Count; i++)
            {
                if (!_ruinLaid[i])
                {
                    _ruinPrisms.Add(null);
                    continue;
                }
                var part = _ruinParts[i];
                var rotation = Quaternion.LookRotation(part.Forward, part.Up);
                _ruinPrisms.Add(PrismTrailBuilder.LayOne(arena.RailPrism,
                    new PrismLay(new SpawnPoint(part.Position, rotation, part.Scale), RuinDomain(part.Leg), PrismKind.Plain),
                    _ruinRoot, new Trail(), RuinOwnerId, admitAuthoredScale: true));
                laid++;
            }
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[Skein] intensity {Intensity}: {laid} ruin prisms.");
        }

        Domains RuinDomain(int leg) =>
            ruinDomains is { Length: > 0 } ? ruinDomains[SkeinRuins.RuinOf(leg) % ruinDomains.Length] : Domains.Blue;

        /// <summary>The clock the wheels turn on: the network's, so every peer poses them alike.</summary>
        float RuinClock => IsSpawned && NetworkManager != null ? (float)NetworkManager.ServerTime.Time : Time.time;

        /// <summary>Every frame: pose each wheel's standing prisms at the clock's angle.</summary>
        void LateUpdate()
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
                if (prism && prism.ownerID == RuinOwnerId && !EnvironmentPrismPool.TryRelease(prism))
                    Destroy(prism.gameObject);
            }
            _ruinPrisms.Clear();
            _ruinParts.Clear();
            _ruinWheels.Clear();
            _ruinLaid.Clear();
            _ruinKeepOff.Clear();
            if (_ruinRoot) Destroy(_ruinRoot.gameObject);
            _ruinRoot = null;
        }
    }
}
