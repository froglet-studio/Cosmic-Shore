using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Redline's garden ruins in the world (REDLINE.md section 4b): every peer lays <see cref="RedlineRuins"/>' prisms
    /// from the broadcast course, once a course, at I4 only. They are scenery with the game's own prism rules: a hull
    /// breaks them, nothing grows back, and they go back to the pool with the course. The wheel turns: its prisms are
    /// POSED here every frame from the network clock, so every peer sees the same angle (a posed prism is the shape of
    /// its owner, not a moving prism: Docs/PRISM_MOTION.md §1). Nothing here reads or writes a vessel.
    /// </summary>
    public partial class RedlineController
    {
        const string RuinOwnerId = "RedlineRuins";

        [Header("Garden ruins (I4)")]
        [Tooltip("The prism the ruins are built from (SpawnablePrism). Empty = no ruins are laid.")]
        [SerializeField] Prism ruinPrefab;

        [Tooltip("The domain colours the ruins are laid in, one ruin after another round the lap. Blue, Gold and Jade: " +
                 "Ruby is left to the mode's name and its heat. Decoration (Yash, 2026-11-24). Empty = Jade.")]
        [SerializeField] Domains[] ruinDomains = { Domains.Blue, Domains.Gold, Domains.Jade };

        readonly List<SpearlineRuinPart> _ruinParts = new();
        readonly List<SpearlineRuinWheel> _ruinWheels = new();
        readonly List<bool> _ruinHidden = new();
        readonly List<Prism> _ruinPrisms = new();   // one entry a part; null where the part is left out
        Transform _ruinRoot;

        /// <summary>Lay the lap's ruins round <paramref name="crystals"/> (this lap's Time crystal places).</summary>
        void LayRuins(IReadOnlyList<Vector3> crystals)
        {
            ClearRuins();
            if (!ruinPrefab) return;
            RedlineRuins.Lay(_course, Intensity, crystals, _ruinParts, _ruinWheels, _ruinHidden);
            if (_ruinParts.Count == 0) return;
            _ruinRoot = new GameObject("RedlineRuins").transform;
            var trail = new Trail();
            int laid = 0;
            for (int i = 0; i < _ruinParts.Count; i++)
            {
                if (_ruinHidden[i]) { _ruinPrisms.Add(null); continue; }
                var part = _ruinParts[i];
                var rotation = Quaternion.LookRotation(part.Forward, part.Up);
                _ruinPrisms.Add(PrismTrailBuilder.LayOne(ruinPrefab,
                    new PrismLay(new SpawnPoint(part.Position, rotation, part.Scale), RuinDomain(part.Leg), PrismKind.Plain),
                    _ruinRoot, trail, RuinOwnerId, admitAuthoredScale: true));
                laid++;
            }
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[Redline] intensity {Intensity}: {laid} ruin prisms of {_ruinParts.Count}, {_ruinWheels.Count} wheel(s).");
        }

        Domains RuinDomain(int leg) =>
            ruinDomains is { Length: > 0 } ? ruinDomains[leg % ruinDomains.Length] : Domains.Jade;

        /// <summary>The clock the wheel turns on: the network's, so every peer poses it alike.</summary>
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
            _ruinHidden.Clear();
            if (_ruinRoot) Destroy(_ruinRoot.gameObject);
            _ruinRoot = null;
        }
    }
}
