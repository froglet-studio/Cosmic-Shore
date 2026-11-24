using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Breakwater's barriers in the world (BREAKWATER.md "The barriers"): every peer lays
    /// <see cref="BreakwaterBarriers"/>' prisms from the broadcast course, in the arena's own prism and the arena's
    /// own colour (Blue, so every pilot's rounds open a curtain and are paid for it), once a course. A curtain's
    /// plates are plain prisms and a minefield's mines are danger prisms, both with the game's own rules: a hull or a
    /// round breaks them and nothing grows back. Nothing here moves, and nothing reads or writes a vessel or a station.
    /// </summary>
    public partial class BreakwaterController
    {
        const string BarrierOwnerId = "BreakwaterBarriers";

        [Header("Barriers (I4)")]
        [Tooltip("Lay the curtain walls, locks and minefields across the legs the ruins leave bare. Off = the legs " +
                 "between ruins stay open water.")]
        [SerializeField] bool layBarriers = true;

        readonly List<RaceGate> _barrierLap = new();
        readonly List<BreakwaterBarrierPart> _barrierParts = new();
        readonly List<Prism> _barrierPrisms = new();
        Transform _barrierRoot;

        void LayBarriers()
        {
            ClearBarriers();
            var prefab = arenaPrefab ? arenaPrefab.PrismPrefab : null;
            if (!layBarriers || !prefab) return;
            var pads = BreakwaterCourse.SpawnPadRing(ResolveCellCentre(), BreakwaterCourseSettings.DefaultSpawnRingRadius);
            BreakwaterBarriers.Lay(_course, LeadInGates, Intensity, pads, _barrierLap, _barrierParts);
            if (_barrierParts.Count == 0) return;
            _barrierRoot = new GameObject("BreakwaterBarriers").transform;
            var trail = new Trail();
            for (int i = 0; i < _barrierParts.Count; i++)
            {
                var part = _barrierParts[i];
                var rotation = Quaternion.LookRotation(part.Forward, part.Up);
                _barrierPrisms.Add(PrismTrailBuilder.LayOne(prefab,
                    new PrismLay(new SpawnPoint(part.Position, rotation, part.Scale), Domains.Blue,
                                 part.Danger ? PrismKind.Danger : PrismKind.Plain),
                    _barrierRoot, trail, BarrierOwnerId, admitAuthoredScale: true));
            }
        }

        void ClearBarriers()
        {
            for (int i = 0; i < _barrierPrisms.Count; i++)
            {
                var prism = _barrierPrisms[i];
                if (prism && prism.ownerID == BarrierOwnerId && !EnvironmentPrismPool.TryRelease(prism))
                    Destroy(prism.gameObject);
            }
            _barrierPrisms.Clear();
            _barrierParts.Clear();
            _barrierLap.Clear();
            if (_barrierRoot) Destroy(_barrierRoot.gameObject);
            _barrierRoot = null;
        }
    }
}
