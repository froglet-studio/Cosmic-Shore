using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Breakwater's garden ruins in the world (BREAKWATER.md "The ruins"): every peer lays <see cref="BreakwaterRuins"/>'
    /// prisms from the broadcast course, in the arena's own prism but in the pilots' colours, once a course. They are
    /// scenery with the game's own prism rules: a hull or a round breaks them, nothing grows back, and they are
    /// go with the scene, as the arena does. A wheel, where one fits, is POSED here every frame from the network
    /// clock, so every peer sees the same angle (a posed prism is the shape of its owner, not a moving prism:
    /// Docs/PRISM_MOTION.md §1). Nothing here reads or writes a vessel or a station.
    /// </summary>
    public partial class BreakwaterController
    {
        const string RuinOwnerId = "BreakwaterRuins";

        [Header("Garden ruins (I4)")]
        [Tooltip("The domain colours the ruins are laid in, so they read apart from the stations (Blue). Each ruin, in " +
                 "course order, takes the colour holding the fewest ruin prisms so far, so every team's rounds find " +
                 "about the same hostile mass in them. Decoration (Yash, 2026-11-24). Empty = Blue, as the stations.")]
        [SerializeField] Domains[] ruinDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        readonly List<RaceGate> _ruinLap = new();
        readonly List<SpearlineRuinPart> _ruinParts = new();
        readonly List<SpearlineRuinWheel> _ruinWheels = new();
        readonly List<int> _ruinOf = new();
        readonly List<int> _ruinSlot = new();
        readonly List<int> _ruinSlotPrisms = new();
        // One entry for each part, null where the part was left out: a wheel names its parts by index.
        readonly List<Prism> _ruinPrisms = new();
        Transform _ruinRoot;

        void LayRuins()
        {
            ClearRuins();
            var prefab = arenaPrefab ? arenaPrefab.PrismPrefab : null;
            if (!prefab) return;
            var pads = BreakwaterCourse.SpawnPadRing(ResolveCellCentre(), BreakwaterCourseSettings.DefaultSpawnRingRadius);
            BreakwaterRuins.Lay(_course, LeadInGates, Intensity, pads, _ruinLap, _ruinParts, _ruinWheels, _ruinOf);
            if (_ruinParts.Count == 0) return;
            BreakwaterRuins.ShareOut(_ruinOf, ruinDomains?.Length ?? 0, _ruinSlot, _ruinSlotPrisms);
            _ruinRoot = new GameObject("BreakwaterRuins").transform;
            var trail = new Trail();
            for (int i = 0; i < _ruinParts.Count; i++)
            {
                if (_ruinOf[i] < 0)
                {
                    _ruinPrisms.Add(null);
                    continue;
                }
                var part = _ruinParts[i];
                var rotation = Quaternion.LookRotation(part.Forward, part.Up);
                _ruinPrisms.Add(PrismTrailBuilder.LayOne(prefab,
                    new PrismLay(new SpawnPoint(part.Position, rotation, part.Scale), RuinDomain(_ruinOf[i]), PrismKind.Plain),
                    _ruinRoot, trail, RuinOwnerId, admitAuthoredScale: true));
            }
        }

        Domains RuinDomain(int ruin) =>
            ruinDomains is { Length: > 0 } && ruin < _ruinSlot.Count ? ruinDomains[_ruinSlot[ruin]] : Domains.Blue;

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
            _ruinOf.Clear();
            _ruinLap.Clear();
            if (_ruinRoot) Destroy(_ruinRoot.gameObject);
            _ruinRoot = null;
        }
    }
}
