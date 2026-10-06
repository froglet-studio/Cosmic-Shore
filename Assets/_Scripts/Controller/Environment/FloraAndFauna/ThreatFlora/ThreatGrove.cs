// THE THREAT-FLORA GROVE, per cell (round 11c, Docs/THREAT_FLORA.md §4). The thin Unity glue around the two pure
// cores: SnapTrapCore (every snap trap in the cell, one colony) and PhysarumCore (the cell's one slime-mould
// network). It is created on demand by the first plant that needs it, lives on a child of the cell, and does
// three things a frame at most:
//   1. step the cores on their own clocks (snap traps at SnapTrapHz, the network at PhysarumHz inside Advance);
//   2. turn the cores' EVENTS into prism work - lay, re-pose (one transform write + one GPU flight stamp per
//      keyframe, never per frame), glow (one colour stamp), danger on/off, eat;
//   3. refresh what the cores sense: vessels near the grove each snap tick, edible food every FoodRefreshSeconds.
// Mass is booked in the cores (their Audit() closes); this file only moves prisms in and out of the world to match.
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ECS;
using CosmicShore.Utility;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
// Unity.Mathematics also declares Random; the UnityEngine one is meant here (CS0104 otherwise).
using Random = UnityEngine.Random;
using NVec = System.Numerics.Vector3;

namespace CosmicShore.Gameplay
{
    public sealed class ThreatGrove : MonoBehaviour
    {
        static readonly Dictionary<int, ThreatGrove> s_byCell = new();
        static readonly HashSet<Transform> s_tubeRoots = new();
        static readonly ProfilerMarker s_snapMarker = new("ThreatGrove.SnapTraps");
        static readonly ProfilerMarker s_physMarker = new("ThreatGrove.Physarum");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_byCell.Clear(); s_tubeRoots.Clear(); }

        /// <summary>
        /// A physarum tube: living tissue of a grove although its HealthPrism has no LifeForm (the network is one body
        /// with many hearts, so no single sclerotium owns a tube). Other species' "never living tissue" rules ask
        /// this - round 11e's BuilderPrismWorld does - so a tube is never loot, carried off or built into a wall.
        /// </summary>
        public static bool IsGroveTissue(Prism prism) =>
            prism && prism.transform.parent && s_tubeRoots.Contains(prism.transform.parent);

        /// <summary>
        /// p moved just clear of every grove (grown by clearance), keeping its distance from the cell centre. For a
        /// creature's home that must not sit on a grove (round 11e's thief nest when it finds no plant to perch on).
        /// </summary>
        public static Vector3 OutsideGroves(Vector3 p, float clearance)
        {
            foreach (var grove in s_byCell.Values)
                if (grove) p = ToU(grove._shape.PushOutside(ToN(p), clearance));
            return p;
        }

        /// <summary>The cell's grove, created on first use. Null when there is no cell or config to build it from.</summary>
        public static ThreatGrove For(Cell cell, ThreatGroveConfigSO config)
        {
            if (!cell || !config) return null;
            int key = cell.GetInstanceID();
            if (s_byCell.TryGetValue(key, out var grove) && grove) return grove;
            var go = new GameObject("ThreatGrove");
            go.transform.SetParent(cell.transform, false);
            grove = go.AddComponent<ThreatGrove>();
            grove.Bind(cell, config);
            s_byCell[key] = grove;
            return grove;
        }

        Cell _cell;
        ThreatGroveConfigSO _cfg;
        ThreatGroveShape _shape;
        Vector3 _centre;
        float _reach;
        int _vesselMask;
        uint _seed;

        public ThreatGroveShape Shape => _shape;
        public ThreatGroveConfigSO Config => _cfg;
        public Cell HostCell => _cell;

        void Bind(Cell cell, ThreatGroveConfigSO config)
        {
            _cell = cell;
            _cfg = config;
            _shape = config.BuildShape(cell.transform.position);
            _shape.Bounds(out NVec lo, out NVec hi);
            _centre = ToU((lo + hi) * 0.5f);
            _reach = (hi - lo).Length() * 0.5f;
            _vesselMask = ~LayerMask.GetMask("TrailBlocks", "Mound");
            _seed = (uint)(cell.ID * 7919 + 17);
            _tubeRoot = new GameObject("PhysarumTubes").transform;
            _tubeRoot.SetParent(transform, false);
            s_tubeRoots.Add(_tubeRoot);
        }

        void OnDestroy()
        {
            if (_cell) s_byCell.Remove(_cell.GetInstanceID());
            if (_tubeRoot) s_tubeRoots.Remove(_tubeRoot);
        }

        // ═════════════════════════════════════════════════════════════════════ shared helpers

        static NVec ToN(Vector3 v) => new NVec(v.x, v.y, v.z);
        static Vector3 ToU(NVec v) => new Vector3(v.X, v.Y, v.Z);

        static Quaternion Look(NVec forward, NVec up)
        {
            Vector3 f = ToU(forward), u = ToU(up);
            if (f.sqrMagnitude < 1e-8f) f = Vector3.forward;
            if (u.sqrMagnitude < 1e-8f || Mathf.Abs(Vector3.Dot(f.normalized, u.normalized)) > 0.999f)
                u = Mathf.Abs(f.normalized.y) < 0.9f ? Vector3.up : Vector3.right;
            return Quaternion.LookRotation(f, u);
        }

        /// <summary>
        /// Move a live prism to its keyframe ONCE and let the GPU fly it there (Docs/PRISM_ANIMATION.md §5 C5): the
        /// transform, collider and spatial-index entry are final at once, and the visual walks in from where it was
        /// over the stamp. StampFlight's full vector is v·2·d/π, so v = (end - start)·π / (2·d). Attitude settles at
        /// the stamp (the flight clock carries translation only).
        /// </summary>
        internal static void PoseAndFly(Prism prism, Vector3 end, Quaternion rot, float duration)
        {
            if (!prism || prism.destroyed) return;
            Vector3 start = prism.transform.position;
            prism.transform.SetPositionAndRotation(end, rot);
            prism.NotifyPositionChanged();
            Vector3 delta = end - start;
            if (duration <= 1e-3f || delta.sqrMagnitude < 1e-4f) return;
            Vector3 v = delta * (Mathf.PI / (2f * duration));
            PrismRenderService.StampFlight(in prism.RenderHandle, PrismClock.Now, duration, new float3(v.x, v.y, v.z));
        }

        /// <summary>Glow a prism into (or out of) its domain's DANGER palette over duration, without making it
        /// dangerous: the telegraph is a colour, the sting is the tier.</summary>
        internal void StampGlow(Prism prism, bool on, float duration)
        {
            if (!prism || prism.destroyed || !_cfg.Theme || _cfg.Theme.ColorSet == null) return;
            var set = _cfg.Theme.ColorSet;
            if (!set.TryGetPrismKindColors(prism.Domain, PrismKind.Plain, out Color pb, out Color pd)) return;
            if (!set.TryGetPrismKindColors(prism.Domain, PrismKind.Danger, out Color db, out Color dd)) return;
            float3 spread = SpreadOf(prism);
            Color fromB = on ? pb : db, fromD = on ? pd : dd, toB = on ? db : pb, toD = on ? dd : pd;
            PrismRenderService.SetColors(in prism.RenderHandle, PrismRenderService.ToFloat4(toB), PrismRenderService.ToFloat4(toD), spread);
            PrismRenderService.StampColorTransition(in prism.RenderHandle, PrismClock.Now, Mathf.Max(0.05f, duration),
                PrismRenderService.ToFloat4(fromB), PrismRenderService.ToFloat4(fromD), spread);
        }

        static readonly int SpreadId = Shader.PropertyToID("_Spread");

        static float3 SpreadOf(Prism prism)
        {
            if (prism.TryGetComponent(out MeshRenderer mr) && mr.sharedMaterial && mr.sharedMaterial.HasProperty(SpreadId))
            {
                Vector4 s = mr.sharedMaterial.GetVector(SpreadId);
                return new float3(s.x, s.y, s.z);
            }
            return new float3(1f, 1f, 1f);
        }

        /// <summary>The danger tier on or off (the SwarmTadpoleFauna.SetTier pair: there is no "make safe" API).</summary>
        internal static void SetDanger(Prism prism, bool danger)
        {
            // no IsCreationComplete gate: PrismStateManager.MakeDangerous handles a birth transition itself, and a
            // wave pulse is half a second - a tube still blooming must not sit out the pulse running through it
            if (!prism || prism.destroyed || prism.prismProperties == null) return;
            bool now = prism.prismProperties.IsDangerous;
            if (danger == now) return;
            if (danger) { prism.MakeDangerous(); return; }
            prism.prismProperties.IsDangerous = false;
            prism.DeactivateShields();
        }

        /// <summary>
        /// Food for a threat plant: ordinary cell mass. Never shielded (Fauna.IsShieldedMass - the one canonical
        /// rule), never a LIVING body (a lifeform's or a creature's prism), never any grove's tubes, never a builder's
        /// structure or a prism a builder is carrying (BuilderRegistry: a fortress wall or a thief's hoard is another
        /// creature's held mass, as a shell is), and always what the cell lets a herbivore of
        /// <paramref name="eater"/> eat there (Cell.IsPreyForHerbivore).
        /// </summary>
        bool IsEdible(Prism prism, Domains eater)
        {
            if (!prism || prism.destroyed) return false;
            if (Fauna.IsShieldedMass(prism)) return false;
            if (prism is HealthPrism hp && (hp.LifeForm || hp.ResolveOwnerFauna() != null)) return false;
            if (IsGroveTissue(prism)) return false;
            if (BuilderRegistry.IsBuilt(prism) || BuilderRegistry.IsCarried(prism)) return false;
            return _cell && _cell.IsPreyForHerbivore(prism.transform.position, eater, prism.Domain);
        }

        readonly List<Prism> _query = new(256);

        /// <summary>Eat up to <paramref name="bites"/> edible prisms within radius of at, nearest first. Returns the volume.</summary>
        float Eat(Vector3 at, float radius, int bites, Domains eater, Transform mouth, string eaterName)
        {
            var index = PrismSpatialIndex.Instance;
            if (index == null || bites <= 0) return 0f;
            index.QuerySphere(at, radius, _query);
            float eaten = 0f;
            for (int n = 0; n < bites; n++)
            {
                int best = -1;
                float bd = float.MaxValue;
                for (int k = 0; k < _query.Count; k++)
                {
                    var p = _query[k];
                    if (!IsEdible(p, eater)) continue;
                    float d = (p.transform.position - at).sqrMagnitude;
                    if (d < bd) { bd = d; best = k; }
                }
                if (best < 0) break;
                var food = _query[best];
                _query[best] = null;
                float v = food.Volume;
                food.Consume(mouth, eater, eaterName, false, true);
                eaten += v;
            }
            return eaten;
        }

        // ═════════════════════════════════════════════════════════════════════ vessels

        static readonly Collider[] s_overlap = new Collider[64];
        readonly List<IVesselStatus> _seen = new(8);
        readonly Dictionary<IVesselStatus, Vector3> _lastPos = new();
        readonly Dictionary<IVesselStatus, Vector3> _nextPos = new();
        ThreatVessel[] _vessels = new ThreatVessel[8];
        int _vesselCount;
        const float VesselRadius = 6f;

        void SenseVessels(float margin)
        {
            _seen.Clear();
            _nextPos.Clear();
            _vesselCount = 0;
            int hits = Physics.OverlapSphereNonAlloc(_centre, _reach + margin, s_overlap, _vesselMask);
            for (int h = 0; h < hits; h++)
            {
                var col = s_overlap[h];
                if (!col) continue;
                if (!col.TryGetComponent(out IVesselStatus status))
                    status = col.GetComponentInParent<IVesselStatus>();
                if (status == null || _seen.Contains(status) || status is not Component c || !c) continue;
                _seen.Add(status);
                Vector3 pos = c.transform.position;
                Vector3 prev = _lastPos.TryGetValue(status, out var lp) ? lp : pos;
                _nextPos[status] = pos;
                if (_vesselCount == _vessels.Length) System.Array.Resize(ref _vessels, _vessels.Length * 2);
                _vessels[_vesselCount++] = new ThreatVessel { Position = ToN(pos), Previous = ToN(prev), Radius = VesselRadius };
            }
            _lastPos.Clear();
            foreach (var kv in _nextPos) _lastPos[kv.Key] = kv.Value;
        }

        // ═════════════════════════════════════════════════════════════════════ snap traps

        SnapTrapCore _snap;
        float _snapAcc, _budRetryAt;
        readonly List<SnapTrapFlora> _trapOwner = new();
        HealthPrism[] _slotPrism = new HealthPrism[SnapTrapCore.SlotCount * 16];
        readonly Dictionary<HealthPrism, int> _slotOf = new();
        int[] _clumpCount;

        public SnapTrapCore SnapTraps => _snap;

        NVec ClumpCentre(int c)
        {
            int n = Mathf.Max(1, _cfg.SnapTrapClumps);
            NVec axis = _shape.Axis;
            ThreatFloraMath.Frame(axis, out NVec e1, out NVec e2);
            float ang = n == 1 ? 0f : ThreatFloraMath.Deg2Rad(_cfg.ClumpSpreadDegrees);
            float phi = 2f * Mathf.PI * c / n;
            NVec dir = NVec.Normalize(axis + (e1 * Mathf.Cos(phi) + e2 * Mathf.Sin(phi)) * Mathf.Tan(ang));
            float r = 0.5f * (_shape.InnerRadius + _shape.OuterRadius);
            return _shape.CellCentre + dir * r;
        }

        /// <summary>Where a SEEDED trap goes: the clump with the fewest traps, anywhere within its radius, rooted at
        /// the rim; it faces the cell centre (the open water the traffic crosses) until heliotropism turns it.</summary>
        public Vector3 PickTrapSite(out Vector3 axis)
        {
            int n = Mathf.Max(1, _cfg.SnapTrapClumps);
            if (_clumpCount == null || _clumpCount.Length != n) _clumpCount = new int[n];
            int c = 0;
            for (int k = 1; k < n; k++) if (_clumpCount[k] < _clumpCount[c]) c = k;
            _clumpCount[c]++;
            NVec centre = ClumpCentre(c);
            NVec p = ToN(RootAtRim(ToU(centre + ToN(Random.insideUnitSphere) * _cfg.ClumpRadius)));
            NVec inward = NVec.Normalize(_shape.CellCentre - p);
            axis = ToU(inward);   // the planted heading the heliotropism cone is about (RootAtRim)
            return ToU(p);
        }

        /// <summary>
        /// A trap ROOTS at the rim (TrapRootDepthMin..Max inside the grove's outer radius) and is planted facing
        /// the cell centre; heliotropism turns it within HelioConeDegrees of that. Its whole body then stays between
        /// the outer swarm band and the membrane at any heading it can reach (harness S9).
        /// </summary>
        public Vector3 RootAtRim(Vector3 at)
        {
            NVec p = _shape.Clamp(ToN(at), 20f);
            NVec off = p - _shape.CellCentre;
            float d = off.Length();
            if (d < 1e-3f) return ToU(p);
            float r = _shape.OuterRadius - Random.Range(ThreatGroveDefaults.TrapRootDepthMin, ThreatGroveDefaults.TrapRootDepthMax);
            return ToU(_shape.CellCentre + off / d * r);
        }

        /// <summary>A trap joins the colony. Seeded traps bring their own body into the rhizome; a budded daughter
        /// brings nothing (the rhizome raised the BudRequest because it already held her body).</summary>
        public int RegisterTrap(SnapTrapFlora flora, Vector3 heart, Vector3 axis, bool budded)
        {
            if (_snap == null)
                _snap = new SnapTrapCore(_cfg.BuildSnapTrap(flora.Element), _seed);
            int i = _snap.AddTrap(ToN(heart), ToN(axis), budded);
            while (_trapOwner.Count <= i) _trapOwner.Add(null);
            _trapOwner[i] = flora;
            EnsureSlots(i);
            flora.PlaceHeart(ToU(_snap.HeartCrystalPosed(i)));
            ProcessSnapEvents();
            return i;
        }

        void EnsureSlots(int trap)
        {
            int need = (trap + 1) * SnapTrapCore.SlotCount;
            if (_slotPrism.Length < need) System.Array.Resize(ref _slotPrism, Mathf.Max(need, _slotPrism.Length * 2));
        }

        /// <summary>A trap slot's prism left by an active force (eaten, rammed, shot): it leaves the colony's ledger.</summary>
        public void TrapSlotRemoved(int trap, HealthPrism prism)
        {
            if (_snap == null || !prism || !_slotOf.TryGetValue(prism, out int o)) return;
            _slotOf.Remove(prism);
            if (_slotPrism[o] == prism) _slotPrism[o] = null;
            _snap.SlotLost(o / SnapTrapCore.SlotCount, o % SnapTrapCore.SlotCount);
        }

        /// <summary>The trap died (heart jousted, or nothing left of it). Its standing body leaves the colony as cell
        /// mass (skeleton or debris - whatever the death style makes of it); the rhizome lives on in the others.</summary>
        public void TrapDied(int trap, SnapTrapFlora who)
        {
            if (!Owns(trap, who)) return;
            _snap.Kill(trap);
            for (int s = 0; s < SnapTrapCore.SlotCount; s++)
            {
                int o = trap * SnapTrapCore.SlotCount + s;
                var hp = _slotPrism[o];
                if (hp) _slotOf.Remove(hp);
                _slotPrism[o] = null;
            }
        }

        public void TrapGone(int trap, SnapTrapFlora who)
        {
            if (!Owns(trap, who)) return;
            TrapDied(trap, who);
            _snap.Release(trap);
            if (trap < _trapOwner.Count) _trapOwner[trap] = null;
        }

        // a core row is reused only after TrapGone released it; the owner check makes a late call from a husk inert
        bool Owns(int trap, SnapTrapFlora who) =>
            _snap != null && trap >= 0 && trap < _snap.Count && trap < _trapOwner.Count && _trapOwner[trap] == who;

        void StepSnapTraps(float dt)
        {
            if (_snap == null || _snap.LiveCount == 0) return;
            float tick = 1f / Mathf.Max(1f, _cfg.SnapTrapHz);
            _snapAcc += dt;
            if (_snapAcc < tick) return;
            using (s_snapMarker.Auto())
            {
                SenseVessels(_snap.P.Sense + 60f);   // the trigger reads paths a little past Sense
                int guard = 0;
                while (_snapAcc >= tick && guard++ < 4)
                {
                    _snapAcc -= tick;
                    _snap.Step(tick, _vessels, _vesselCount);
                    ProcessSnapEvents();
                }
                if (_snapAcc > tick) _snapAcc = 0f;
            }
        }

        void ProcessSnapEvents()
        {
            var ev = _snap.Events;
            for (int e = 0; e < ev.Count; e++)   // by index: Deposit can append Lay events this tick
            {
                var x = ev[e];
                SnapTrapFlora owner = x.Trap >= 0 && x.Trap < _trapOwner.Count ? _trapOwner[x.Trap] : null;
                switch (x.Kind)
                {
                    case SnapTrapEventKind.Lay:
                        LaySlot(owner, x.Trap, x.Slot);
                        break;
                    case SnapTrapEventKind.Pose:
                        PoseTrap(owner, x.Trap, x.PoseMask, x.Duration);
                        break;
                    case SnapTrapEventKind.Glow:
                        GlowTrap(x.Trap, x.Glow != 0, x.Duration);
                        break;
                    case SnapTrapEventKind.Fired:
                        LobesDanger(x.Trap, true);
                        break;
                    case SnapTrapEventKind.Snap:
                        if (CSDebug.IsVerbose(CSLogChannel.Ecology))
                            CSDebug.LogVerbose(CSLogChannel.Ecology, $"[ThreatGrove] snap trap {x.Trap} snapped a vessel at {ToU(x.Position)}");
                        break;
                    case SnapTrapEventKind.AbsorbRequest:
                        if (owner)
                        {
                            float v = Eat(ToU(x.Position), _snap.P.Root, _cfg.RootBitesPerAbsorb, owner.Domain, owner.transform, "snaptrap");
                            if (v > 0f) _snap.Deposit(x.Trap, v);
                        }
                        break;
                    case SnapTrapEventKind.MouthAbsorbRequest:
                        LobesDanger(x.Trap, false);
                        if (owner)
                        {
                            float r = 0.5f * _snap.P.MouthLength * _snap.P.GeometryScale;
                            float v = Eat(ToU(x.Position), r, 64, owner.Domain, owner.HeartTransform, "snaptrap");
                            if (v > 0f) _snap.Deposit(x.Trap, v);
                        }
                        break;
                    case SnapTrapEventKind.BudRequest:
                        if (owner && Time.time >= _budRetryAt)
                        {
                            _budRetryAt = Time.time + _cfg.BudRetrySeconds;
                            owner.TryBud(ToU(x.Position), ToU(x.Axis));
                        }
                        break;
                }
            }
            ev.Clear();
        }

        Vector3 SlotLeaf(int slot)
        {
            var p = _snap.P;
            if (slot < SnapTrapCore.StalkSlots) return Thicken(_cfg.StalkLeaf, p.StalkVolume);
            return SnapTrapCore.SlotTooth[slot] ? Thicken(_cfg.ToothLeaf, p.ToothVolume) : ThickenPlate(_cfg.LobeLeaf, p.LobeVolume);
        }

        // The MASS element is authored as heavier bodies (volume x1.5): a rod grows across, a plate grows thicker,
        // so the prism's volume is the volume the core books for it.
        static Vector3 Thicken(Vector3 leaf, float volume)
        {
            float v0 = leaf.x * leaf.y * leaf.z;
            if (v0 <= 0f) return leaf;
            float k = Mathf.Sqrt(volume / v0);
            return new Vector3(leaf.x * k, leaf.y * k, leaf.z);
        }

        static Vector3 ThickenPlate(Vector3 leaf, float volume)
        {
            float v0 = leaf.x * leaf.y * leaf.z;
            return v0 <= 0f ? leaf : new Vector3(leaf.x, leaf.y * volume / v0, leaf.z);
        }

        void LaySlot(SnapTrapFlora owner, int trap, int slot)
        {
            if (!owner || owner.IsDying) { _snap.CancelLay(trap, slot); return; }
            _snap.PosedSlot(trap, slot, out NVec pos, out NVec fwd, out NVec up);
            bool tooth = SnapTrapCore.SlotTooth[slot];
            var prism = owner.LaySlotPrism(ToU(pos), Look(fwd, up), SlotLeaf(slot), tooth);
            if (!prism) { _snap.CancelLay(trap, slot); return; }
            int o = trap * SnapTrapCore.SlotCount + slot;
            EnsureSlots(trap);
            _slotPrism[o] = prism;
            _slotOf[prism] = o;
            if (_snap.GlowOn[trap] && !tooth && slot >= SnapTrapCore.StalkSlots) StampGlow(prism, true, 0.05f);
        }

        void PoseTrap(SnapTrapFlora owner, int trap, int mask, float duration)
        {
            for (int s = 0; s < SnapTrapCore.SlotCount; s++)
            {
                int bit = s < SnapTrapCore.StalkSlots ? SnapTrapCore.PoseStalk
                    : SnapTrapCore.SlotTooth[s] ? SnapTrapCore.PoseTeeth : SnapTrapCore.PoseLobes;
                if ((mask & bit) == 0) continue;
                var prism = _slotPrism[trap * SnapTrapCore.SlotCount + s];
                if (!prism) continue;
                _snap.PosedSlot(trap, s, out NVec pos, out NVec fwd, out NVec up);
                PoseAndFly(prism, ToU(pos), Look(fwd, up), duration);
            }
            if (owner) owner.PlaceHeart(ToU(_snap.HeartCrystalPosed(trap)));
        }

        void GlowTrap(int trap, bool on, float duration)
        {
            for (int s = SnapTrapCore.StalkSlots; s < SnapTrapCore.SlotCount; s++)
            {
                if (SnapTrapCore.SlotTooth[s]) continue;
                var prism = _slotPrism[trap * SnapTrapCore.SlotCount + s];
                if (prism) StampGlow(prism, on, duration);
            }
            if (!on) LobesDanger(trap, false);
        }

        /// <summary>The closing sweep stings: the lobes are danger prisms from FIRED until the jaws are SHUT.</summary>
        void LobesDanger(int trap, bool danger)
        {
            for (int s = SnapTrapCore.StalkSlots; s < SnapTrapCore.SlotCount; s++)
            {
                if (SnapTrapCore.SlotTooth[s]) continue;
                var prism = _slotPrism[trap * SnapTrapCore.SlotCount + s];
                if (prism) SetDanger(prism, danger);
            }
        }

        // ═════════════════════════════════════════════════════════════════════ physarum

        PhysarumCore _phys;
        Transform _tubeRoot;
        HealthPrism _tubePrefab;
        readonly List<PhysarumSclerotium> _hearts = new();
        readonly Dictionary<int, HealthPrism> _tube = new();
        readonly Queue<int> _layQueue = new();
        readonly HashSet<int> _queued = new();
        readonly List<Prism> _food = new();
        readonly List<int> _scratchVox = new();
        float _foodAt, _pollAt;
        bool _tubesSeeded;

        public PhysarumCore Network => _phys;

        /// <summary>A sclerotium joins the network with its planted share; returns its heart index.</summary>
        public int RegisterSclerotium(PhysarumSclerotium heart, Vector3 position, HealthPrism tubePrefab)
        {
            if (_phys == null)
            {
                _phys = new PhysarumCore(_cfg.BuildPhysarum(heart.Element), _shape, _seed ^ 0x9E3779B9u);
                _tubePrefab = tubePrefab;
            }
            int k = _phys.AddHeart(ToN(position), _cfg.PlantedVolumePerSclerotium);
            while (_hearts.Count <= k) _hearts.Add(null);
            _hearts[k] = heart;
            return k;
        }

        public Vector3 HeartPosition(int k) => _phys != null ? ToU(_phys.HeartPosition[k]) : Vector3.zero;

        public bool SpendOnShell(float volume) => _phys != null && _phys.SpendOnHeartBody(volume);
        public void ShellRefund(float volume) => _phys?.HeartBodyRefund(volume);
        public void ShellLost(float volume) => _phys?.HeartBodyLost(volume);
        public void ShellSkeleton(float volume) => _phys?.HeartBodySkeleton(volume);

        public void SclerotiumDied(int k)
        {
            _phys?.KillHeart(k);
            if (k >= 0 && k < _hearts.Count) _hearts[k] = null;
        }

        /// <summary>A random point inside the grove for a seeded sclerotium (the research plants hearts 80 u in).</summary>
        public Vector3 PickSclerotiumSite()
        {
            var rng = new ThreatRng(_seed + (ulong)(_hearts.Count * 131 + 7) + (ulong)(Time.frameCount));
            return ToU(_shape.Sample(ref rng, 40f));
        }

        void StepPhysarum(float dt)
        {
            if (_phys == null || _phys.LiveHearts == 0 && _tube.Count == 0) return;
            using (s_physMarker.Auto())
            {
                if (Time.time >= _foodAt) { _foodAt = Time.time + _cfg.FoodRefreshSeconds; RefreshFood(); }
                if (!_phys.WarmedUp)
                {
                    // the grove before you arrive: run the research's warm-up a few steps a frame, eat what it digests,
                    // and lay only the network it settles on (not every tube it tried on the way)
                    _phys.WarmupSteps(_cfg.WarmupStepsPerFrame);
                    ProcessPhysarumEvents(warm: true);
                    if (!_phys.WarmedUp) return;
                }
                if (!_tubesSeeded)
                {
                    _tubesSeeded = true;
                    foreach (int v in _phys.TubeVoxels()) Enqueue(v);
                }
                SenseVessels(0f);
                _phys.SetVessels(_vessels, _vesselCount);
                _phys.Advance(dt * FarScale());
                ProcessPhysarumEvents(warm: false);
                DrainLayQueue();
                if (Time.time >= _pollAt) { _pollAt = Time.time + _phys.P.MaterializeEvery; PollTubes(); }
            }
        }

        // ── the far cadence (round 11f-2, Docs/ECOLOGY_LOD.md §6.3) ─────────────────────────────────────
        // Flora is never collapsed; with nobody near, its network just runs on slowed time (the same mass-exact steps,
        // fewer a second). A vessel in the grove's own sense keeps it at full rate at once; the wider check (reach +
        // FarMargin, vessels and the main camera) runs at 4 Hz.
        const float FarCheckSeconds = 0.25f;
        static readonly Collider[] s_farOverlap = new Collider[256];
        readonly NVec[] _farPilots = new NVec[17];
        float _farCheckAt, _farScale = 1f;

        float FarScale()
        {
            if (_cfg.FarTimeScale >= 1f || _vesselCount > 0) { _farScale = 1f; return 1f; }
            if (Time.time < _farCheckAt) return _farScale;
            _farCheckAt = Time.time + FarCheckSeconds;
            int n = 0;
            var cam = Camera.main;
            if (cam) _farPilots[n++] = ToN(cam.transform.position);
            int hits = Physics.OverlapSphereNonAlloc(_centre, _reach + _cfg.FarMargin, s_farOverlap, _vesselMask);
            for (int h = 0; h < hits && n < _farPilots.Length; h++)
            {
                var col = s_farOverlap[h];
                if (!col) continue;
                if (!col.TryGetComponent(out IVesselStatus status)) status = col.GetComponentInParent<IVesselStatus>();
                if (status is Component c && c) _farPilots[n++] = ToN(c.transform.position);
            }
            if (hits >= s_farOverlap.Length) n = _farPilots.Length;   // a full buffer may hide a vessel: stay at full rate
            _farScale = n >= _farPilots.Length ? 1f
                : ThreatFloraMath.FarTimeScale(ToN(_centre), _reach + _cfg.FarMargin, _cfg.FarTimeScale,
                                               new System.ReadOnlySpan<NVec>(_farPilots, 0, n));
            return _farScale;
        }

        void Enqueue(int v)
        {
            if (_tube.ContainsKey(v) || !_queued.Add(v)) return;
            _layQueue.Enqueue(v);
        }

        void ProcessPhysarumEvents(bool warm)
        {
            var ev = _phys.Events;
            for (int e = 0; e < ev.Count; e++)
            {
                var x = ev[e];
                switch (x.Kind)
                {
                    case PhysarumEventKind.Digest:
                        Digest(x.Food);
                        break;
                    case PhysarumEventKind.Lay:
                        if (!warm) Enqueue(x.Voxel);
                        break;
                    case PhysarumEventKind.Resorb:
                        if (!warm) Resorb(x.Voxel);
                        break;
                    case PhysarumEventKind.DangerOn:
                    case PhysarumEventKind.DangerOff:
                        if (_tube.TryGetValue(x.Voxel, out var tp)) SetDanger(tp, x.Kind == PhysarumEventKind.DangerOn);
                        break;
                    case PhysarumEventKind.BeatGlow:
                        Heart(x.Heart)?.BeatGlow(_phys.P.BeatGlow);
                        break;
                    case PhysarumEventKind.BeatOn:
                        Heart(x.Heart)?.SetBeat(true);
                        break;
                    case PhysarumEventKind.BeatOff:
                        Heart(x.Heart)?.SetBeat(false);
                        break;
                    case PhysarumEventKind.HeartMoved:
                        Heart(x.Heart)?.MoveTo(ToU(_phys.HeartPosition[x.Heart]), _phys.P.MaterializeEvery);
                        break;
                }
            }
            ev.Clear();
        }

        PhysarumSclerotium Heart(int k) => k >= 0 && k < _hearts.Count && _hearts[k] ? _hearts[k] : null;

        Domains NetworkDomainAt(Vector3 p)
        {
            Domains d = Domains.Jade;
            float bd = float.MaxValue;
            bool any = false;
            for (int k = 0; k < _hearts.Count; k++)
            {
                var h = _hearts[k];
                if (!h || !_phys.HeartAlive[k]) continue;
                float dist = (ToU(_phys.HeartPosition[k]) - p).sqrMagnitude;
                if (dist < bd) { bd = dist; d = h.Domain; any = true; }
            }
            if (!any)
                for (int k = 0; k < _hearts.Count; k++) if (_hearts[k]) { d = _hearts[k].Domain; break; }
            return d;
        }

        Transform NearestHeartTransform(Vector3 p)
        {
            Transform best = transform;
            float bd = float.MaxValue;
            for (int k = 0; k < _hearts.Count; k++)
            {
                var h = _hearts[k];
                if (!h || !_phys.HeartAlive[k]) continue;
                float dist = (h.HeartTransform.position - p).sqrMagnitude;
                if (dist < bd) { bd = dist; best = h.HeartTransform; }
            }
            return best;
        }

        void RefreshFood()
        {
            var index = PrismSpatialIndex.Instance;
            _food.Clear();
            if (index != null)
            {
                index.QuerySphere(_centre, _reach, _query);
                Domains eater = NetworkDomainAt(_centre);
                for (int k = 0; k < _query.Count && _food.Count < _cfg.MaxFood; k++)
                {
                    var p = _query[k];
                    if (!IsEdible(p, eater) || !_shape.Contains(ToN(p.transform.position))) continue;
                    _food.Add(p);
                }
            }
            _phys.EnsureFood(_food.Count);
            for (int f = 0; f < _food.Count; f++)
            {
                _phys.FoodPos[f] = ToN(_food[f].transform.position);
                _phys.FoodVol[f] = _food[f].Volume;
                _phys.FoodAlive[f] = true;
            }
            _phys.FoodCount = _food.Count;
        }

        void Digest(int f)
        {
            if (f < 0 || f >= _food.Count) return;
            var p = _food[f];
            _food[f] = null;
            if (!p || p.destroyed) return;
            float v = p.Volume;
            Vector3 at = p.transform.position;
            p.Consume(NearestHeartTransform(at), NetworkDomainAt(at), "physarum", false, true);
            _phys.CreditDigest(v);
        }

        void DrainLayQueue()
        {
            int budget = _cfg.MaxLaysPerFrame;
            while (budget > 0 && _layQueue.Count > 0)
            {
                int v = _layQueue.Dequeue();
                _queued.Remove(v);
                if (!_phys.Tube[v] || _tube.ContainsKey(v)) continue;
                budget--;
                if (!LayTube(v)) _phys.CancelLay(v);
            }
        }

        bool LayTube(int v)
        {
            if (!_tubePrefab) return false;
            Vector3 pos = ToU(_phys.Centre(v));
            NVec dir = _phys.TubeDirection(v);
            Quaternion rot = Look(dir, NVec.UnitY);
            var prism = EnvironmentPrismPool.Get(_tubePrefab, pos, rot);
            if (!prism) return false;
            Vector3 leaf = _cfg.TubeLeaf;
            float k = Mathf.Sqrt(_phys.P.TubeVolume / Mathf.Max(1e-4f, _cfg.TubeVolume));   // MASS: thicker, not longer
            leaf = new Vector3(leaf.x * k, leaf.y * k, leaf.z);
            prism.transform.SetParent(_tubeRoot, true);
            prism.LifeForm = null;
            prism.ChangeTeam(NetworkDomainAt(pos));
            prism.AdmitTargetScale(leaf);
            prism.TargetScale = leaf;
            if (_phys.Danger[v]) prism.MakeDangerous();   // a pulse is running through this voxel as it is laid
            prism.Initialize("physarum");
            _tube[v] = prism;
            return true;
        }

        /// <summary>The trail fell under Off: the tube's mass goes back into the network (the core already credited
        /// the reserve), drawn in toward the nearest sclerotium.</summary>
        void Resorb(int v)
        {
            if (!_tube.TryGetValue(v, out var prism)) return;
            _tube.Remove(v);
            if (!prism || prism.destroyed) return;
            Vector3 at = prism.transform.position;
            prism.Consume(NearestHeartTransform(at), prism.Domain, "physarum", false, true);
        }

        /// <summary>Tubes taken by an active force (rammed, shot, grazed) leave the ledger; a re-issued pool prism
        /// (re-parented elsewhere) counts as gone too.</summary>
        void PollTubes()
        {
            _scratchVox.Clear();
            foreach (var kv in _tube)
            {
                var p = kv.Value;
                if (!p || p.destroyed || p.transform.parent != _tubeRoot) _scratchVox.Add(kv.Key);
            }
            for (int i = 0; i < _scratchVox.Count; i++)
            {
                _tube.Remove(_scratchVox[i]);
                _phys.TubeLost(_scratchVox[i]);
            }
        }

        // ═════════════════════════════════════════════════════════════════════ frame

        void Update()
        {
            float dt = Time.deltaTime;
            StepSnapTraps(dt);
            StepPhysarum(dt);
        }
    }
}
