using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Skim Race's garden ruins in the world (SKIMRACE.md §5b): every peer lays <see cref="SkimRaceRuins"/>' prisms
    /// from the authored ribbon once the track is spawned, in a domain colour of their own. They are scenery with the
    /// game's own prism rules: a hull breaks them, nothing grows back, and they go back to the pool when the
    /// controller despawns. The wheels turn: their prisms are POSED here every frame from the network clock, so every
    /// peer sees the same angle (a posed prism is the shape of its owner, not a moving prism: Docs/PRISM_MOTION.md §1).
    /// Nothing here reads or writes a vessel, a crystal or the ribbon.
    /// </summary>
    public partial class SkimRaceController
    {
        const string RuinOwnerId = "SkimRaceRuins";

        [Header("Garden ruins (I4)")]
        [Tooltip("The scene's waypoint track: the ruins stand round its ribbon. Unset = no ruins.")]
        [SerializeField] SpawnableWaypointTrack ruinTrack;
        [Tooltip("The prism every ruin is built from. Unset = no ruins.")]
        [SerializeField] Prism ruinPrefab;
        [Tooltip("The domain colours the ruins are laid in, one ruin after another round the lap, so they read apart " +
                 "from the ribbon and its crystal markers (Blue). Decoration " +
                 "(Yash, 2026-11-24). Empty = Blue, as the ribbon.")]
        [SerializeField] Domains[] ruinDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        readonly List<Vector3> _ruinRibbon = new();
        readonly List<bool> _ruinMarker = new();
        readonly List<RaceGate> _ruinGates = new();
        readonly List<SpearlineRuinPart> _ruinParts = new();
        readonly List<SpearlineRuinWheel> _ruinWheels = new();
        readonly List<bool> _ruinLaid = new();
        // One entry for each part, null where the part was left out: a wheel names its parts by index.
        readonly List<Prism> _ruinPrisms = new();
        Transform _ruinRoot;

        /// <summary>Once, right after the track is spawned (its blocks say where the ribbon is in the world).</summary>
        void LayRuins()
        {
            if (SpearlineRuins.ForIntensity(Intensity).PerLap <= 0) return;
            if (!ruinTrack || !ruinPrefab)
            {
                CSDebug.LogWarning("[SkimRaceController] No garden ruins: ruinTrack or ruinPrefab is not assigned.");
                return;
            }
            var trails = ruinTrack.GetTrails();
            if (trails is not { Count: > 0 } || trails[0]?.TrailList is not { Count: > 0 } blocks || !blocks[0]) return;
            Transform laidTrack = blocks[0].transform.parent;
            if (!laidTrack) return;

            _ruinRibbon.Clear();
            _ruinMarker.Clear();
            foreach (var block in ruinTrack.GetPreviewBlocks(Intensity))
            {
                _ruinRibbon.Add(laidTrack.TransformPoint(block.Position));
                _ruinMarker.Add(block.IsMarker);
            }
            SkimRaceRuins.Lay(_ruinRibbon, _ruinMarker, laidTrack.position, Intensity,
                _ruinGates, _ruinParts, _ruinWheels, _ruinLaid);
            if (_ruinParts.Count == 0) return;

            _ruinRoot = new GameObject("SkimRaceRuins").transform;
            var trail = new Trail();
            int ruin = -1, ruinLeg = -1;   // the ruins take the colours in turn, in lap order
            for (int i = 0; i < _ruinParts.Count; i++)
            {
                if (!_ruinLaid[i])
                {
                    _ruinPrisms.Add(null);
                    continue;
                }
                var part = _ruinParts[i];
                if (part.Leg != ruinLeg)
                {
                    ruinLeg = part.Leg;
                    ruin++;
                }
                var rotation = Quaternion.LookRotation(part.Forward, part.Up);
                _ruinPrisms.Add(PrismTrailBuilder.LayOne(ruinPrefab,
                    new PrismLay(new SpawnPoint(part.Position, rotation, part.Scale), RuinDomain(ruin), PrismKind.Plain),
                    _ruinRoot, trail, RuinOwnerId, admitAuthoredScale: true));
            }
        }

        Domains RuinDomain(int ruin) =>
            ruinDomains is { Length: > 0 } ? ruinDomains[ruin % ruinDomains.Length] : Domains.Blue;

        /// <summary>The clock the wheels turn on: the network's, so every peer poses them alike.</summary>
        float RuinClock => IsSpawned && NetworkManager != null ? (float)NetworkManager.ServerTime.Time : Time.time;

        void Update() => TickRuins();

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
            _ruinLaid.Clear();
            if (_ruinRoot) Destroy(_ruinRoot.gameObject);
            _ruinRoot = null;
        }
    }
}
