using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ECS;
using CosmicShore.Utility;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using SVector3 = System.Numerics.Vector3;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The game's half of <see cref="IBuilderWorld"/> (Docs/BUILDERS_AND_THIEVES.md §2): the colony cores' integer mass
    /// handles over real <see cref="Prism"/>s, through the platform's existing verbs only - PrismSpatialIndex.QuerySphere /
    /// TryReserve, Prism.Steal, the mover contract (transform + NotifyPositionChanged), a clock-stamped flight for the
    /// settle (PrismRenderService.StampFlight), Prism.Consume for eating. It never creates or destroys a prism of its own
    /// accord, so the colony's structures add no collider.
    ///
    /// Handles are slots in a table keyed by (prism, its TimeCreated): a pooled prism that comes back as new mass gets a
    /// NEW handle, and the old one reads dead. A dead slot nobody pins (built / carried) is recycled after a quarantine
    /// long enough that no core still aims a worker at it.
    ///
    /// A WEARER's body (<see cref="IWearWorld"/>, Docs/BUILDERS_AND_THIEVES.md §10): every worn prism is parented under ONE
    /// container transform per creature, so its collider rides the creature with no per-prism transform write; per frame the
    /// container gets one pose and every worn prism's render matrix and spatial-index point go out in ONE batched pass each
    /// (<see cref="PrismRenderService.SetTransformsBatch(NativeArray{PrismRenderHandle}, NativeArray{float4x4}, int)"/>,
    /// <see cref="PrismSpatialIndex.UpdatePositionsBatch"/>). Per-prism transform writes happen only when the body's squash
    /// changes (the rear / lunge / recover - the per-phase notify of PORT.md §4) and on wear / unwear.
    /// </summary>
    public sealed class BuilderPrismWorld : IWearWorld
    {
        struct Slot
        {
            public Prism Prism;
            public float Born;            // prismProperties.TimeCreated when the handle was issued
            public string PrevPlayer;     // who it was stolen from (GiveBack)
            public Domains PrevDomain;
            public bool Carried, Built;
            public Vector3 From, To;      // carry interpolation: last tick's and this tick's grip
            public float DeadSince;       // -1 while alive
            public bool Worn;             // hangs on a wearer's body (WornBy), parented under its container
            public int WornBy;
            public Vector3 Local;         // the unsquashed body-frame site offset
            public Transform Parent;      // where it hung before it was worn (it goes back there when it leaves)
            public bool Parented;         // hangs under its body's container now
            public Quaternion LocalRot;   // its own rotation and scale under the container, cached at parenting
            public Vector3 LocalScale;
            public bool Danger;           // made dangerous by its body's lunge
            public float Debuff;          // its speed debuff before the lunge
        }

        /// <summary>One wearer creature's body: a container transform and the handles hanging on it.</summary>
        sealed class Body
        {
            public Transform T;
            public readonly List<int> Handles = new(64);
            public readonly List<int> Pending = new(8);
            public Vector3 PrevPos, Pos;
            public Quaternion PrevRot = Quaternion.identity, Rot = Quaternion.identity;
            public float Squash = 1f, Applied = 1f;
            public bool Posed, Danger;
        }

        struct PendingSettle { public Prism Prism; public float At; }

        const float QuarantineSeconds = 6f;
        const float SweepEverySeconds = 2f;

        readonly List<Slot> _slots = new(512);
        readonly Dictionary<Prism, int> _handleOf = new(512);
        readonly Queue<int> _free = new();
        readonly List<int> _carried = new(64);
        readonly List<PendingSettle> _settling = new(32);
        readonly List<Prism> _query = new(512);
        readonly List<(int handle, float since)> _quarantine = new(128);
        readonly Transform[] _mouths;
        int _mouthCursor;
        float _nextSweep;

        readonly Dictionary<int, Body> _bodies = new();
        readonly List<Body> _bodyList = new();
        NativeArray<PrismRenderHandle> _wornHandles;
        NativeArray<float4x4> _wornMatrices;
        NativeArray<int> _wornIds;
        NativeArray<float3> _wornPoints;
        int _wornCapacity;

        /// <summary>Worn prisms posed last frame (render matrices / index points in one batched pass each).</summary>
        public int WornPosedLastFrame { get; private set; }
        /// <summary>Per-prism transform writes caused by squash changes (the per-phase notify).</summary>
        public long SquashWrites { get; private set; }

        readonly string _colonyName;
        readonly int _colonyId;
        readonly Domains _colonyDomain;

        /// <summary>The colony's lattice address of a site index (a fortress's lattice; a hoard's negative ids map to x).</summary>
        public System.Func<int, Vector3Int> SiteAddress;

        /// <summary>Who each sensed vessel id is, for a raid (refreshed by the glue every tick).</summary>
        public readonly List<(string name, Domains domain)> Vessels = new(8);

        public BuilderPrismWorld(string colonyName, int colonyId, Domains colonyDomain, Transform[] mouths)
        {
            _colonyName = colonyName;
            _colonyId = colonyId;
            _colonyDomain = colonyDomain;
            _mouths = mouths;
        }

        public int HandleCount => _slots.Count;

        static Vector3 U(SVector3 v) => new(v.X, v.Y, v.Z);
        static SVector3 S(Vector3 v) => new(v.x, v.y, v.z);

        bool Live(int h, out Prism p)
        {
            p = null;
            if (h < 0 || h >= _slots.Count) return false;
            var s = _slots[h];
            p = s.Prism;
            return p && !p.destroyed && p.prismProperties != null && p.prismProperties.TimeCreated == s.Born;
        }

        int HandleOf(Prism p)
        {
            if (_handleOf.TryGetValue(p, out int h))
            {
                if (_slots[h].Born == p.prismProperties.TimeCreated) return h;
                // the pool brought this prism back as new mass: the old handle stays dead, the new mass is a new handle
                _handleOf.Remove(p);
                Quarantine(h);
            }
            var slot = new Slot { Prism = p, Born = p.prismProperties.TimeCreated, DeadSince = -1f };
            if (_free.Count > 0) { h = _free.Dequeue(); _slots[h] = slot; }
            else { h = _slots.Count; _slots.Add(slot); }
            _handleOf[p] = h;
            return h;
        }

        void Quarantine(int h)
        {
            var s = _slots[h];
            if (s.DeadSince >= 0f) return;
            s.DeadSince = Time.time;
            _slots[h] = s;
            _quarantine.Add((h, Time.time));
        }

        /// <summary>Living tissue is never loot: a flora's or a creature's body prism belongs to a lifeform, and a
        /// physarum tube belongs to its grove's network although no single LifeForm owns it (ThreatGrove.IsGroveTissue,
        /// round 11c).</summary>
        static bool IsLivingTissue(Prism p) =>
            p is HealthPrism hp && (hp.LifeForm || hp.ResolveOwnerFauna() || ThreatGrove.IsGroveTissue(hp));

        // ── IBuilderWorld ────────────────────────────────────────────────────────────────────────

        public int QuerySphere(SVector3 centre, float radius, List<int> results)
        {
            results.Clear();
            var index = PrismSpatialIndex.Instance;
            if (index == null || !index.IsAvailable) return 0;
            index.QuerySphere(U(centre), radius, _query);
            for (int i = 0; i < _query.Count; i++)
            {
                var p = _query[i];
                if (!p || p.destroyed || p.prismProperties == null || p.OwnerHidden) continue;
                results.Add(HandleOf(p));
            }
            return results.Count;
        }

        public bool Alive(int h) => Live(h, out _);

        public SVector3 Position(int h) => Live(h, out var p) ? S(p.transform.position) : default;

        public int Domain(int h) => Live(h, out var p) ? (int)p.Domain : -1;

        public bool Shielded(int h)
        {
            if (!Live(h, out var p)) return true;
            var pp = p.prismProperties;
            return pp.IsShielded || pp.IsSuperShielded;
        }

        public bool IsTrail(int h) => Live(h, out var p) && (p.Trail != null || p.prismProperties.Trail != null);

        public float Age(int h) => Live(h, out var p) ? Time.time - p.prismProperties.TimeCreated : float.MaxValue;

        public float Volume(int h) => Live(h, out var p) ? p.Volume : 0f;

        public bool Loose(int h) =>
            Live(h, out var p) && !BuilderRegistry.IsBuilt(p) && !BuilderRegistry.IsCarried(p) && !IsLivingTissue(p);

        public bool Steal(int h, int domain)
        {
            if (!Live(h, out var p)) return false;
            var pp = p.prismProperties;
            if (pp.IsShielded || pp.IsSuperShielded) return false;   // never: shielded mass is not a target
            var s = _slots[h];
            s.PrevPlayer = p.PlayerName;
            s.PrevDomain = p.Domain;
            p.Steal(_colonyName, (Domains)domain, false);
            if ((int)p.Domain != domain) return false;
            s.Carried = true;
            s.From = s.To = p.transform.position;
            _slots[h] = s;
            BuilderRegistry.MarkCarried(p, true);
            if (!_carried.Contains(h)) _carried.Add(h);
            return true;
        }

        public void Carry(int h, SVector3 position)
        {
            if (!Live(h, out var p)) return;
            var s = _slots[h];
            // the grip is set once per tick; the frame interpolates last tick's grip to this one (Animate)
            s.From = s.Carried ? p.transform.position : U(position);
            s.To = U(position);
            s.Carried = true;
            _slots[h] = s;
            if (!_carried.Contains(h)) _carried.Add(h);
        }

        public void Drop(int h)
        {
            if (h < 0 || h >= _slots.Count) return;
            var s = _slots[h];
            s.Carried = false;
            _slots[h] = s;
            _carried.Remove(h);
            if (s.Prism) BuilderRegistry.MarkCarried(s.Prism, false);
            if (Live(h, out var p)) p.NotifyPositionChanged();
        }

        public bool TryReserve(SVector3 site, float clearRadius)
        {
            var index = PrismSpatialIndex.Instance;
            return index != null && index.IsAvailable && index.TryReserve(U(site), clearRadius);
        }

        public void Settle(int h, SVector3 from, SVector3 site, float seconds)
        {
            var at = U(site);
            PrismSpatialIndex.Instance?.ReleaseReservation(at);
            if (!Live(h, out var p)) return;
            var s = _slots[h];
            s.Carried = false;
            _slots[h] = s;
            _carried.Remove(h);
            BuilderRegistry.MarkCarried(p, false);

            // The FINAL pose now - collider, spatial index and volume are final from this frame - and the short hop from
            // the grip is the prism clock's flight (animation only, Docs/PRISM_ANIMATION.md §5 C5).
            var start = U(from);
            p.transform.position = at;
            p.NotifyPositionChanged();
            float T = Mathf.Max(0.05f, seconds);
            var flight = at - start;
            if (flight.sqrMagnitude > 1e-4f)
            {
                var v = flight * (Mathf.PI / (2f * T));
                if (PrismRenderService.StampFlight(in p.RenderHandle, PrismClock.Now, T, new float3(v.x, v.y, v.z)))
                {
                    var local = p.transform.InverseTransformPoint(start);
                    PrismRenderService.EncapsulateBoundsPoint(in p.RenderHandle, new float3(local.x, local.y, local.z), 1f);
                    _settling.Add(new PendingSettle { Prism = p, At = Time.time + T });
                }
            }
        }

        public void SetBuilt(int h, int colony, int site, bool built)
        {
            if (h < 0 || h >= _slots.Count) return;
            var s = _slots[h];
            s.Built = built;
            _slots[h] = s;
            if (!s.Prism) return;
            if (built)
            {
                var address = site >= 0 && SiteAddress != null ? SiteAddress(site) : new Vector3Int(site, 0, 0);
                BuilderRegistry.Register(s.Prism, _colonyId, address);
            }
            else BuilderRegistry.Unregister(s.Prism);
        }

        public float Consume(int h, SVector3 mouth)
        {
            if (!Live(h, out var p)) return 0f;
            float v = p.Volume;
            BuilderRegistry.Unregister(p);
            BuilderRegistry.MarkCarried(p, false);
            Transform m = null;
            if (_mouths is { Length: > 0 })
            {
                m = _mouths[_mouthCursor];
                _mouthCursor = (_mouthCursor + 1) % _mouths.Length;
                m.position = U(mouth);
            }
            // the food web's own exit: suction into the mouth, the volume leaves the cell's sums with the prism
            p.Consume(m ? m : p.transform, _colonyDomain, _colonyName, false, true);
            Quarantine(h);
            return v;
        }

        public void GiveBack(int h)
        {
            if (!Live(h, out var p)) return;
            var s = _slots[h];
            if (string.IsNullOrEmpty(s.PrevPlayer)) return;
            p.Steal(s.PrevPlayer, s.PrevDomain, false);
        }

        public void Reclaim(int h, int vesselId)
        {
            if (!Live(h, out var p) || vesselId < 0 || vesselId >= Vessels.Count) return;
            var (name, d) = Vessels[vesselId];
            BuilderRegistry.Unregister(p);
            var s = _slots[h];
            s.Built = false;
            _slots[h] = s;
            p.Steal(name, d, false);
        }

        // ── per frame ────────────────────────────────────────────────────────────────────────────

        /// <summary>Per FRAME: every carried prism rides its carrier between ticks (the mover contract each frame), and
        /// settled flights clear their stamps on arrival.</summary>
        public void Animate(float alpha)
        {
            for (int i = _carried.Count - 1; i >= 0; i--)
            {
                int h = _carried[i];
                if (!Live(h, out var p)) { _carried.RemoveAt(i); continue; }
                var s = _slots[h];
                if (!s.Carried) { _carried.RemoveAt(i); continue; }
                p.transform.position = Vector3.LerpUnclamped(s.From, s.To, alpha);
                p.NotifyPositionChanged();
            }
            float now = Time.time;
            for (int i = _settling.Count - 1; i >= 0; i--)
            {
                var e = _settling[i];
                if (now < e.At && e.Prism) continue;
                if (e.Prism && !e.Prism.destroyed) PrismRenderService.ClearFlightStamp(in e.Prism.RenderHandle);
                _settling.RemoveAt(i);
            }
            if (now >= _nextSweep) Sweep(now);
            if (_bodyList.Count > 0) PoseWorn(alpha);
        }

        /// <summary>Dead, unpinned handles go to quarantine; quarantined ones past the wait are reused.</summary>
        void Sweep(float now)
        {
            _nextSweep = now + SweepEverySeconds;
            for (int h = 0; h < _slots.Count; h++)
            {
                var s = _slots[h];
                if (s.DeadSince >= 0f || s.Prism == null) continue;
                if (Live(h, out _)) continue;
                if (s.Prism) BuilderRegistry.MarkCarried(s.Prism, false);
                if (!ReferenceEquals(s.Prism, null) && _handleOf.TryGetValue(s.Prism, out int cur) && cur == h)
                    _handleOf.Remove(s.Prism);
                Quarantine(h);
            }
            for (int i = _quarantine.Count - 1; i >= 0; i--)
            {
                var (h, since) = _quarantine[i];
                if (now - since < QuarantineSeconds) continue;
                _quarantine.RemoveAt(i);
                var s = _slots[h];
                if (s.Prism && _handleOf.TryGetValue(s.Prism, out int cur) && cur == h) _handleOf.Remove(s.Prism);
                _slots[h] = default;
                _free.Enqueue(h);
            }
        }

        /// <summary>The colony went away: every prism in a grip falls loose where it is (nothing pops).</summary>
        /// <summary>True while a deposited prism is still in its settle flight (round 11f-2: a colony never collapses mid-flight).</summary>
        public bool Settling => _settling.Count > 0;

        public void ReleaseAll()
        {
            for (int i = 0; i < _carried.Count; i++)
            {
                int h = _carried[i];
                if (h < 0 || h >= _slots.Count) continue;
                var p = _slots[h].Prism;
                if (p) BuilderRegistry.MarkCarried(p, false);
            }
            _carried.Clear();
            for (int i = 0; i < _settling.Count; i++)
                if (_settling[i].Prism && !_settling[i].Prism.destroyed)
                    PrismRenderService.ClearFlightStamp(in _settling[i].Prism.RenderHandle);
            _settling.Clear();
            for (int b = 0; b < _bodyList.Count; b++)
            {
                var body = _bodyList[b];
                for (int i = body.Handles.Count - 1; i >= 0; i--) Unwear(body.Handles[i]);
                if (body.T) Object.Destroy(body.T.gameObject);
            }
            _bodyList.Clear();
            _bodies.Clear();
            if (_wornHandles.IsCreated) _wornHandles.Dispose();
            if (_wornMatrices.IsCreated) _wornMatrices.Dispose();
            if (_wornIds.IsCreated) _wornIds.Dispose();
            if (_wornPoints.IsCreated) _wornPoints.Dispose();
            _wornCapacity = 0;
        }

        // ── IWearWorld (a wearer's body) ─────────────────────────────────────────────────────────

        Body BodyOf(int creature)
        {
            if (_bodies.TryGetValue(creature, out var b) && b.T) return b;
            if (b == null)
            {
                b = new Body();
                _bodies[creature] = b;
                _bodyList.Add(b);
            }
            // a root object, never a child of the anchor: tearing the anchor down must not take stolen prisms with it
            var go = new GameObject($"WearerBody {_colonyId}.{creature}");
            b.T = go.transform;
            b.T.SetPositionAndRotation(b.Pos, b.Rot);
            return b;
        }

        public void Wear(int h, int creature, SVector3 local)
        {
            if (!Live(h, out var p)) return;
            var s = _slots[h];
            if (s.Worn && s.WornBy != creature && _bodies.TryGetValue(s.WornBy, out var was))
            {
                was.Handles.Remove(h);
                was.Pending.Remove(h);
            }
            if (!s.Worn)
            {
                s.Parent = p.transform.parent;
                s.Carried = false;
                _carried.Remove(h);
                BuilderRegistry.MarkCarried(p, true);   // never loose while worn: no other colony takes it off a body
            }
            s.Worn = true;
            s.WornBy = creature;
            s.Local = U(local);
            _slots[h] = s;
            var body = BodyOf(creature);
            if (!body.Handles.Contains(h)) body.Handles.Add(h);
            // parented at the next PoseBody, once the body's pose for this tick is known
            if (!body.Pending.Contains(h)) body.Pending.Add(h);
            if (s.Danger != body.Danger) SetPrismDanger(h, body.Danger);
        }

        public void Unwear(int h)
        {
            if (h < 0 || h >= _slots.Count) return;
            var s = _slots[h];
            if (!s.Worn) return;
            if (_bodies.TryGetValue(s.WornBy, out var body)) { body.Handles.Remove(h); body.Pending.Remove(h); }
            if (s.Danger) SetPrismDanger(h, false);
            s = _slots[h];
            s.Worn = false;
            s.Parented = false;
            _slots[h] = s;
            var p = s.Prism;
            if (p) BuilderRegistry.MarkCarried(p, false);
            if (!Live(h, out p)) return;
            // back where it hung before (its trail's container), world pose kept - unless the pool already took it home
            if (body != null && p.transform.parent == body.T)
                p.transform.SetParent(s.Parent ? s.Parent : null, true);
            p.NotifyPositionChanged();
        }

        public void PoseBody(int creature, SVector3 pos, SVector3 right, SVector3 up, SVector3 forward, float squash)
        {
            var body = BodyOf(creature);
            var at = U(pos);
            var f = U(forward);
            var rot = f.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(f, U(up)) : body.Rot;
            body.PrevPos = body.Posed ? body.Pos : at;
            body.PrevRot = body.Posed ? body.Rot : rot;
            body.Pos = at;
            body.Rot = rot;
            body.Squash = squash;
            if (!body.Posed) { body.T.SetPositionAndRotation(at, rot); body.Posed = true; }
            for (int i = 0; i < body.Pending.Count; i++)
            {
                int h = body.Pending[i];
                if (!Live(h, out var p) || !_slots[h].Worn || _slots[h].WornBy != creature) continue;
                var t = p.transform;
                t.SetParent(body.T, true);   // keeps its own rotation and scale: a creature made of what it took
                var s = _slots[h];
                t.localPosition = s.Local * body.Applied;
                s.Parented = true;
                s.LocalRot = t.localRotation;
                s.LocalScale = t.localScale;
                _slots[h] = s;
            }
            body.Pending.Clear();
        }

        public void SetBodyDanger(int creature, bool danger)
        {
            if (!_bodies.TryGetValue(creature, out var body)) return;
            body.Danger = danger;
            for (int i = 0; i < body.Handles.Count; i++) SetPrismDanger(body.Handles[i], danger);
        }

        void SetPrismDanger(int h, bool danger)
        {
            var s = _slots[h];
            if (s.Danger == danger) return;
            if (!Live(h, out var p)) { s.Danger = false; _slots[h] = s; return; }
            var pp = p.prismProperties;
            if (danger)
            {
                s.Debuff = pp.speedDebuffAmount;
                p.MakeDangerous();
            }
            else
            {
                pp.IsDangerous = false;
                pp.speedDebuffAmount = s.Debuff;
                p.DeactivateShields();   // the plain look again (no shield was up: nothing is shed)
            }
            s.Danger = danger;
            _slots[h] = s;
        }

        /// <summary>Per FRAME: each body's container gets ONE pose (its colliders ride the hierarchy); a squash change
        /// rewrites its prisms' local offsets (rear / lunge / recover only); then every worn prism's render matrix and index
        /// point go out in one batched pass each.</summary>
        void PoseWorn(float alpha)
        {
            int total = 0;
            for (int b = 0; b < _bodyList.Count; b++) total += _bodyList[b].Handles.Count;
            if (total > _wornCapacity)
            {
                int cap = Mathf.Max(64, Mathf.NextPowerOfTwo(total));
                if (_wornHandles.IsCreated) _wornHandles.Dispose();
                if (_wornMatrices.IsCreated) _wornMatrices.Dispose();
                if (_wornIds.IsCreated) _wornIds.Dispose();
                if (_wornPoints.IsCreated) _wornPoints.Dispose();
                _wornHandles = new NativeArray<PrismRenderHandle>(cap, Allocator.Persistent);
                _wornMatrices = new NativeArray<float4x4>(cap, Allocator.Persistent);
                _wornIds = new NativeArray<int>(cap, Allocator.Persistent);
                _wornPoints = new NativeArray<float3>(cap, Allocator.Persistent);
                _wornCapacity = cap;
            }
            int nr = 0, ni = 0;
            for (int b = 0; b < _bodyList.Count; b++)
            {
                var body = _bodyList[b];
                if (body.Handles.Count == 0 || !body.T || !body.Posed) continue;
                body.T.SetPositionAndRotation(Vector3.LerpUnclamped(body.PrevPos, body.Pos, alpha),
                                              Quaternion.SlerpUnclamped(body.PrevRot, body.Rot, alpha));
                bool squash = Mathf.Abs(body.Squash - body.Applied) > 0.01f;
                if (squash) body.Applied = body.Squash;
                var c = body.T.localToWorldMatrix;
                for (int i = 0; i < body.Handles.Count; i++)
                {
                    int h = body.Handles[i];
                    if (!Live(h, out var p)) continue;
                    var s = _slots[h];
                    if (!s.Parented) continue;   // not parented yet (this tick's PoseBody does it)
                    var local = s.Local * body.Applied;
                    if (squash) { p.transform.localPosition = local; SquashWrites++; }
                    // from the cached local pose: no per-prism transform read or write in a steady frame
                    var m = c * Matrix4x4.TRS(local, s.LocalRot, s.LocalScale);
                    if (PrismRenderService.IsHandleUsable(in p.RenderHandle))
                    {
                        _wornHandles[nr] = p.RenderHandle;
                        _wornMatrices[nr] = new float4x4(m.m00, m.m01, m.m02, m.m03, m.m10, m.m11, m.m12, m.m13,
                                                         m.m20, m.m21, m.m22, m.m23, m.m30, m.m31, m.m32, m.m33);
                        nr++;
                    }
                    if (p.SpatialIndexId >= 0)
                    {
                        _wornIds[ni] = p.SpatialIndexId;
                        var at = m.GetColumn(3);
                        _wornPoints[ni] = new float3(at.x, at.y, at.z);
                        ni++;
                    }
                }
            }
            WornPosedLastFrame = nr;
            if (nr > 0) PrismRenderService.SetTransformsBatch(_wornHandles, _wornMatrices, nr);
            var index = PrismSpatialIndex.Instance;
            if (ni > 0 && index != null && index.IsAvailable) index.UpdatePositionsBatch(_wornIds, _wornPoints, ni);
        }
    }
}
