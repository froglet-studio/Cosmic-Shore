using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ECS;
using CosmicShore.Utility;
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
    /// </summary>
    public sealed class BuilderPrismWorld : IBuilderWorld
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
        }
    }
}
