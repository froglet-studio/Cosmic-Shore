using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Serpentine's garden ruins (SERPENTTIME.md, "Garden ruins at I4"): Spearline's ruins (<see cref="SpearlineRuins"/>,
    /// SPEARLINE.md §2a) laid round this mode's own lap, on the hardest rung only. Every peer lays them from the course
    /// it was sent, in the environment prism the debris and the shapes are laid from, each ruin in one domain colour.
    /// They are scenery with the game's own prism rules: a hull, a round or a wall breaks or takes them, nothing grows
    /// back, and they go back to the pool with the course. The wheel turns: its prisms are POSED every frame from the
    /// network clock, so every peer sees the same angle (a posed prism is the shape of its owner, not a moving prism:
    /// Docs/PRISM_MOTION.md §1). Nothing here reads or writes a vessel, and the generator is Spearline's, unchanged.
    /// </summary>
    public partial class SerpentTimeController
    {
        /// <summary>Owner id no pilot carries: only prisms still carrying it are the ruins'.</summary>
        const string RuinOwnerId = "SerpentineRuins";

        [Header("Garden ruins (I4)")]
        [Tooltip("The domain colours the ruins are laid in, one ruin after another round the lap, so they read apart " +
                 "from the shapes at the rings and the gate debris (both Blue). Decoration (Yash, 2026-11-24). " +
                 "Empty = Blue, as the shapes.")]
        [SerializeField] Domains[] ruinDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        readonly List<SpearlineRuinPart> _ruinParts = new();
        readonly List<SpearlineRuinWheel> _ruinWheels = new();
        readonly List<Prism> _ruinPrisms = new();
        Transform _ruinRoot;

        /// <summary>Lay the lap's ruins (none below I4, none without the environment prism). Behind the connecting
        /// panel, as the shapes and the debris are.</summary>
        void LayRuins()
        {
            ClearRuins();
            if (!debrisPrefab) return;
            SpearlineRuins.Lay(_course, SpearlineRuins.ForIntensity(Intensity), _ruinParts, _ruinWheels);
            if (_ruinParts.Count == 0) return;
            _ruinRoot = new GameObject("SerpentineRuins").transform;
            var trail = new Trail();
            for (int i = 0; i < _ruinParts.Count; i++)
            {
                var part = _ruinParts[i];
                var rotation = Quaternion.LookRotation(part.Forward, part.Up);
                _ruinPrisms.Add(PrismTrailBuilder.LayOne(debrisPrefab,
                    new PrismLay(new SpawnPoint(part.Position, rotation, part.Scale), RuinDomain(part.Leg), PrismKind.Plain),
                    _ruinRoot, trail, RuinOwnerId, admitAuthoredScale: true));
            }
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[Serpentine] intensity {Intensity}: {_ruinPrisms.Count} ruin prisms, {_ruinWheels.Count} turning.");
        }

        Domains RuinDomain(int leg) =>
            ruinDomains is { Length: > 0 } ? ruinDomains[leg % ruinDomains.Length] : Domains.Blue;

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
                // A prism a wall took or the pool re-issued is no longer the ruins' to return.
                if (prism && !prism.destroyed && prism.ownerID == RuinOwnerId && !EnvironmentPrismPool.TryRelease(prism))
                    Destroy(prism.gameObject);
            }
            _ruinPrisms.Clear();
            _ruinParts.Clear();
            _ruinWheels.Clear();
            // A prism a wall still holds stays parented here; the scene's teardown takes it.
            if (_ruinRoot && _ruinRoot.childCount == 0) Destroy(_ruinRoot.gameObject);
            _ruinRoot = null;
        }
    }
}
