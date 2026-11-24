using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Spearline's garden ruins in the world (SPEARLINE.md §2a): every peer lays <see cref="SpearlineRuins"/>' prisms
    /// from the broadcast rings, in the seals' prism and domain, once a course. They are scenery with the game's own
    /// prism rules: a hull or a bill breaks them, nothing grows back, and they are returned to the pool with the seals.
    /// The wheels turn: their prisms are POSED here every frame from the network clock, so every peer sees the same
    /// angle (a posed prism is the shape of its owner, not a moving prism: Docs/PRISM_MOTION.md §1).
    /// Nothing here reads or writes a vessel.
    /// </summary>
    public partial class SpearlineController
    {
        const string RuinOwnerId = "SpearlineRuins";

        readonly List<SpearlineRuinPart> _ruinParts = new();
        readonly List<SpearlineRuinWheel> _ruinWheels = new();
        readonly List<Prism> _ruinPrisms = new();
        Transform _ruinRoot;

        void LayRuins()
        {
            _ruinParts.Clear();
            _ruinWheels.Clear();
            SpearlineRuins.Lay(_course, SpearlineRuins.ForIntensity(Intensity), _ruinParts, _ruinWheels);
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
                if (_ruinPrisms[i] && _ruinPrisms[i].ownerID == RuinOwnerId) Release(_ruinPrisms[i]);
            _ruinPrisms.Clear();
            _ruinParts.Clear();
            _ruinWheels.Clear();
            if (_ruinRoot) Destroy(_ruinRoot.gameObject);
            _ruinRoot = null;
        }
    }
}
