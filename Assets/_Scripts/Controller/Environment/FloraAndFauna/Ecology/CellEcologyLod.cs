// Round 11f (Docs/ECOLOGY_LOD.md §4): the cell's ecology LOD host - thin Unity glue around EcologyLodDirector.
// One per cell, made on the first population's registration (the SubstrateCellHost pattern). Its members call
// Advance() from their Update; the first call in a frame does the work:
//   - every frame: senses the cell's vessels (10 Hz, extrapolated by their velocity in between) plus the main camera,
//     hands them to the director as pilots, and runs Guard() - expand anything collapsed that a pilot wants or sees;
//   - once per second: the director's macro tick (collapse what is far and unseen, tick what is collapsed).
// Typechecked by Tools/Build/swarm_glue_typecheck; the director and the rules it applies are RUN by
// Tools/Build/ecology_lod_harness and Tools/Build/swarm_core_harness (mode lod).
using System.Collections.Generic;
using CosmicShore.Utility;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public sealed class CellEcologyLod
    {
        static readonly Dictionary<Cell, CellEcologyLod> s_hosts = new();
        static readonly Collider[] s_overlap = new Collider[1024];
        static readonly List<IVesselStatus> s_seen = new(16);
        static readonly ProfilerMarker s_mGuard = new("CellEcologyLod.Guard");
        static readonly ProfilerMarker s_mTick = new("CellEcologyLod.MacroTick");
        static readonly ProfilerMarker s_mSense = new("CellEcologyLod.Sense");

        /// <summary>Seconds between macro ticks (research dt_macro).</summary>
        public const float MacroDt = 1f;
        /// <summary>Seconds between vessel senses; positions are extrapolated by velocity in between.</summary>
        const float SenseDt = 0.1f;
        /// <summary>At most this many pilots (vessels + the camera) are tested per population.</summary>
        const int MaxPilots = 16;

        public readonly Cell Cell;
        public readonly EcologyLodDirector Director = new();
        readonly Vector3 _centre;
        readonly float _radius;
        readonly EcologyPilot[] _sensed = new EcologyPilot[MaxPilots];
        readonly EcologyPilot[] _now = new EcologyPilot[MaxPilots];
        int _nSensed;
        float _sensedAt = -1f, _macroAcc;
        int _frame = -1;
        bool _warnedSense;

        CellEcologyLod(Cell cell)
        {
            Cell = cell;
            _centre = cell.transform.position;
            _radius = cell.MembraneRadius > 1f ? cell.MembraneRadius : 1200f;
            _macroAcc = Random.value * MacroDt;   // stagger cells' macro ticks across frames
        }

        // Enter Play Mode without a domain reload keeps statics: start every session clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_hosts.Clear();

        /// <summary>The cell's LOD host, made on first use.</summary>
        public static CellEcologyLod For(Cell cell)
        {
            if (!cell) return null;
            if (s_hosts.TryGetValue(cell, out var h)) return h;
            h = new CellEcologyLod(cell);
            s_hosts[cell] = h;
            return h;
        }

        public void Register(IMacroPopulation population) => Director.Register(population);

        /// <summary>A population leaves (it is being destroyed). The last one out retires the host.</summary>
        public void Unregister(IMacroPopulation population)
        {
            Director.Unregister(population);
            if (Director.Populations.Count == 0) s_hosts.Remove(Cell);
        }

        /// <summary>Once per frame, by any member (the first call in a frame does the work).</summary>
        public void Advance()
        {
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;
            float now = Time.time;
            if (_sensedAt < 0f || now - _sensedAt >= SenseDt)
                using (s_mSense.Auto()) Sense(now);
            Extrapolate(now);
            using (s_mGuard.Auto()) Director.Guard();
            _macroAcc += Time.deltaTime;
            if (_macroAcc < MacroDt) return;
            _macroAcc -= MacroDt;
            if (_macroAcc > MacroDt) _macroAcc = 0f;   // a hitch never banks macro ticks
            using (s_mTick.Auto()) Director.Tick(MacroDt);
        }

        /// <summary>Every vessel in the cell (position + velocity), and the main camera as a pilot looking down its
        /// forward axis - the research's visibility test uses the pilot's heading as its view, which a free-look camera
        /// does not share.</summary>
        void Sense(float now)
        {
            _sensedAt = now;
            _nSensed = 0;
            s_seen.Clear();
            var cam = Camera.main;
            if (cam)
            {
                Vector3 p = cam.transform.position, f = cam.transform.forward;
                _sensed[_nSensed++] = new EcologyPilot { X = p.x, Y = p.y, Z = p.z, Vx = f.x, Vy = f.y, Vz = f.z };
            }
            int hits = Physics.OverlapSphereNonAlloc(_centre, _radius * 1.25f, s_overlap, Fauna.VesselSenseMask);
            if (hits >= s_overlap.Length && !_warnedSense)
            {
                _warnedSense = true;
                CSDebug.LogWarning($"[EcologyLod] the cell-wide vessel sense filled its {s_overlap.Length}-collider buffer; " +
                                   "a vessel may be missed (a missed pilot can watch a collapsed population).");
            }
            for (int h = 0; h < hits && _nSensed < MaxPilots; h++)
            {
                var col = s_overlap[h];
                if (!col) continue;
                if (!col.TryGetComponent(out IVesselStatus status))
                    status = col.GetComponentInParent<IVesselStatus>();
                if (status == null || s_seen.Contains(status) || status is not Component c || !c) continue;
                s_seen.Add(status);
                Vector3 p = c.transform.position, v = status.Course * status.Speed;
                _sensed[_nSensed++] = new EcologyPilot { X = p.x, Y = p.y, Z = p.z, Vx = v.x, Vy = v.y, Vz = v.z, Speed = status.Speed };
            }
        }

        /// <summary>Moves each sensed vessel along its velocity to now (the camera stays where it was sensed).</summary>
        void Extrapolate(float now)
        {
            float dt = Mathf.Max(0f, now - _sensedAt);
            for (int i = 0; i < _nSensed; i++)
            {
                var p = _sensed[i];
                if (p.Speed > 0)
                {
                    p.X += p.Vx * dt; p.Y += p.Vy * dt; p.Z += p.Vz * dt;
                }
                _now[i] = p;
            }
            Director.SetPilots(new System.ReadOnlySpan<EcologyPilot>(_now, 0, _nSensed));
        }
    }
}
