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
    /// round breaks them. A broken one grows back once the field has passed its barrier (<see cref="TickBarriers"/>,
    /// the way Spearline's seals do), so the second lap meets walls too. A turnstile's wheels and a shutter's halves
    /// are POSED here every frame from the network clock, as the ruins' wheel is, so every peer sees the same
    /// moment (a posed prism is the shape of its owner, not a moving prism: Docs/PRISM_MOTION.md 1). Nothing reads
    /// or writes a vessel or a station.
    /// </summary>
    public partial class BreakwaterController
    {
        const string BarrierOwnerId = "BreakwaterBarriers";

        [Header("Barriers (I4)")]
        [Tooltip("Lay the curtain walls, locks and minefields across the legs the ruins leave bare. Off = the legs " +
                 "between ruins stay open water.")]
        [SerializeField] bool layBarriers = true;
        [Tooltip("How many broken barrier prisms may grow back in one frame.")]
        [SerializeField, Min(1)] int barrierRegrowPerFrame = 24;

        readonly List<RaceGate> _barrierLap = new();
        readonly List<BreakwaterBarrierPart> _barrierParts = new();
        readonly List<BreakwaterBarrierMover> _barrierMovers = new();
        readonly List<Prism> _barrierPrisms = new();
        // For each part: when it was first seen broken (-1 while it stands), and which barrier it belongs to.
        readonly List<float> _barrierGoneAt = new();
        readonly List<int> _barrierOf = new();
        readonly List<Vector3> _barrierMiddles = new();
        readonly List<bool> _barrierFieldAt = new();
        Transform _barrierRoot;
        int _barrierCursor;

        void LayBarriers()
        {
            ClearBarriers();
            var prefab = arenaPrefab ? arenaPrefab.PrismPrefab : null;
            if (!layBarriers || !prefab) return;
            var pads = BreakwaterCourse.SpawnPadRing(ResolveCellCentre(), BreakwaterCourseSettings.DefaultSpawnRingRadius);
            BreakwaterBarriers.Lay(_course, LeadInGates, Intensity, pads, _barrierLap, _barrierParts, _barrierMovers);
            if (_barrierParts.Count == 0) return;
            _barrierRoot = new GameObject("BreakwaterBarriers").transform;
            int leg = -1, first = 0;
            for (int i = 0; i <= _barrierParts.Count; i++)
            {
                if (i < _barrierParts.Count && _barrierParts[i].Leg == leg) continue;
                if (i > first)
                {
                    Vector3 sum = Vector3.zero;
                    for (int k = first; k < i; k++) sum += _barrierParts[k].Position;
                    _barrierMiddles.Add(sum / (i - first));
                    _barrierFieldAt.Add(false);
                }
                if (i == _barrierParts.Count) break;
                leg = _barrierParts[i].Leg;
                first = i;
            }
            int barrier = -1;
            leg = -1;
            for (int i = 0; i < _barrierParts.Count; i++)
            {
                if (_barrierParts[i].Leg != leg)
                {
                    leg = _barrierParts[i].Leg;
                    barrier++;
                }
                _barrierOf.Add(barrier);
                _barrierGoneAt.Add(-1f);
                _barrierPrisms.Add(LayBarrierPrism(prefab, i));
            }
        }

        Prism LayBarrierPrism(Prism prefab, int i)
        {
            var part = _barrierParts[i];
            var rotation = Quaternion.LookRotation(part.Forward, part.Up);
            return PrismTrailBuilder.LayOne(prefab,
                new PrismLay(new SpawnPoint(part.Position, rotation, part.Scale), Domains.Blue,
                             part.Danger ? PrismKind.Danger : PrismKind.Plain),
                _barrierRoot, new Trail(), BarrierOwnerId, admitAuthoredScale: true);
        }

        /// <summary>Every peer, every frame: a broken barrier prism grows back
        /// <see cref="BreakwaterBarriers.RegrowSeconds"/> after the last hull left its barrier
        /// (<see cref="BreakwaterBarriers.FieldAt"/>). The dead prism is released to its pool first.</summary>
        void TickBarriers()
        {
            if (_barrierPrisms.Count == 0 || gameData == null) return;
            PoseBarriers();
            var prefab = arenaPrefab ? arenaPrefab.PrismPrefab : null;
            if (!prefab) return;
            var players = gameData.Players;
            for (int b = 0; b < _barrierMiddles.Count; b++)
            {
                bool at = false;
                for (int p = 0; p < players.Count && !at; p++)
                {
                    var tf = players[p]?.Vessel?.Transform;
                    if (tf) at = BreakwaterBarriers.FieldAt((tf.position - _barrierMiddles[b]).magnitude);
                }
                _barrierFieldAt[b] = at;
            }

            float now = Time.time;
            int budget = barrierRegrowPerFrame;
            int count = _barrierPrisms.Count;
            int scan = Mathf.Min(count, 200);
            for (int n = 0; n < scan && budget > 0; n++)
            {
                int i = (_barrierCursor + n) % count;
                var prism = _barrierPrisms[i];
                if (prism && !prism.destroyed && prism.ownerID == BarrierOwnerId) { _barrierGoneAt[i] = -1f; continue; }
                if (_barrierGoneAt[i] < 0f || _barrierFieldAt[_barrierOf[i]]) { _barrierGoneAt[i] = now; continue; }
                if (!BreakwaterBarriers.Regrows(now - _barrierGoneAt[i])) continue;
                if (prism && prism.ownerID == BarrierOwnerId && !EnvironmentPrismPool.TryRelease(prism))
                    Destroy(prism.gameObject);
                _barrierPrisms[i] = LayBarrierPrism(prefab, i);
                _barrierGoneAt[i] = -1f;
                budget--;
            }
            _barrierCursor = (_barrierCursor + scan) % count;
        }

        // Each mover's standing prisms at the shared clock's moment. A prism that just grew back is posed here on
        // the next frame.
        void PoseBarriers()
        {
            if (_barrierMovers.Count == 0) return;
            float seconds = RuinClock;
            for (int m = 0; m < _barrierMovers.Count; m++)
            {
                var mover = _barrierMovers[m];
                for (int i = mover.First; i < mover.First + mover.Count && i < _barrierPrisms.Count; i++)
                {
                    var prism = _barrierPrisms[i];
                    if (!prism || prism.destroyed || prism.ownerID != BarrierOwnerId) continue;
                    BreakwaterBarriers.Pose(_barrierParts[i], mover, seconds, out var position, out var forward, out var up);
                    prism.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, up));
                }
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
            _barrierGoneAt.Clear();
            _barrierOf.Clear();
            _barrierMiddles.Clear();
            _barrierFieldAt.Clear();
            _barrierCursor = 0;
            _barrierParts.Clear();
            _barrierMovers.Clear();
            _barrierLap.Clear();
            if (_barrierRoot) Destroy(_barrierRoot.gameObject);
            _barrierRoot = null;
        }
    }
}
