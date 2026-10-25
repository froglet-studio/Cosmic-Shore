using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Jacks' NET (Garrett, 2026-10-24: "the spinning jack in the middle doesn't work. lets make it a net that opens
    /// wider with xdiff"; design <c>R_VesselActions/JACKS.md</c> §1). While the jacks net (they fly the hull) a net hangs
    /// between the two fish: its mouth is the line between them, it opens taller and wider as the sticks go apart, and its
    /// bag trails behind (<see cref="JacksMath.NetFrame"/>). What crosses the mouth:
    ///
    /// <list type="bullet">
    /// <item><b>Crystals</b>, all five ("all five crystals can be relocated using the net"): caught and CARRIED, settling into
    /// the bag. The netting hull's own touch leaves them alone (<see cref="ICrystalPickupGate"/>, <c>JacksBodies</c>), so
    /// the omni crystal the hull flies into is netted rather than collected (Garrett, 2026-10-25). When the jacks land (switch to herding) the net goes away and the crystals stay where they were; the cell's
    /// omni crystal is put there on every machine (<see cref="CrystalManager.RelocateCrystal"/>). Herding, a jack that
    /// touches a crystal collects it (<see cref="CollectByJacks"/>).</item>
    /// <item><b>Prisms</b> (Garrett, 2026-10-25: "lets have prisms move and be dragged by the net. they should only be
    /// destroyed when they collide with other prisms"): caught and DRAGGED, a mover of the prism-motion fundamental, so a
    /// dragged prism that runs into another prism destroys both (<see cref="DragPrisms"/>). Living mass (a creature's or a
    /// plant's body) and super-shielded mass are never dragged.</item>
    /// <item><b>Creatures</b>: one of another domain is STOLEN into ours (<see cref="Fauna.StealInto"/>), and then, like any
    /// creature of ours that goes through the net, its body is shielded (<see cref="Fauna.ShieldBody"/>) and it joins the
    /// herd, so it wears the herd's mark and glow.</item>
    /// <item><b>Rival hulls</b> ("if an opposing vessel is netted it should remove some of their crystals which remain in the
    /// net"): each element loses <c>netRivalTake</c>, knocked loose as crystals at the hull (<see cref="ElementalTransfer"/>'s
    /// networked Eject), which the net then catches.</item>
    /// </list>
    ///
    /// <para>The executor runs the net on every peer from the jacks it has there (the owner's own, or the published ones), so
    /// each machine nets what it simulates: its own prisms and loose crystals, the creatures it simulates (a stolen or
    /// shielded creature replicates through <see cref="FaunaNetworkSync"/>), and - on the machine that decides the hull only
    /// - the rival take and the omni crystal's final place.</para>
    /// </summary>
    sealed class JacksNet
    {
        sealed class Item
        {
            public Crystal Crystal;
            public bool Omni;
            public float S, T, W;
            public Vector3 Placed;
        }

        readonly List<Item> _items = new();
        readonly List<Prism> _prisms = new();

        // the prisms the net drags (Garrett, 2026-10-25: "lets have prisms move and be dragged by the net. they should only be
        // destroyed when they collide with other prisms"): a mover of the prism-motion fundamental (Docs/PRISM_MOTION.md)
        sealed class Dragged
        {
            public Prism Prism;
            public float S, T, W;
        }

        readonly List<Dragged> _dragged = new();
        readonly HashSet<Prism> _draggedSet = new();
        NativeParallelHashMap<int, int> _moverOfSlot;
        NativeArray<float3> _from, _to;
        PrismMotionLevel _level = PrismMotionLevel.Showcase;
        HashSet<Fauna> _inside = new(), _insideNext = new();
        readonly Dictionary<IVesselStatus, float> _rivalReadyAt = new();
        readonly Dictionary<Crystal, CrystalImpactor> _impactorOf = new();
        readonly Dictionary<Crystal, float> _collectedAt = new();

        /// <summary>The net as it hangs this frame (valid while <see cref="Open"/> is true).</summary>
        internal JacksMath.NetFrame Frame { get; private set; }

        /// <summary>True while the jacks net (the net hangs between them).</summary>
        internal bool Open { get; private set; }

        /// <summary>How many crystals the net carries.</summary>
        internal int CrystalCount => _items.Count;

        /// <summary>A crystal the net carries, in the world.</summary>
        internal Vector3 ItemPosition(int i) => _items[i].Placed;

        /// <summary>The net between jack <paramref name="a"/> and jack <paramref name="b"/>, opened by how far apart they
        /// are (every peer reads the same opening back from the published fish).</summary>
        internal static JacksMath.NetFrame FrameFor(JacksConfigSO cfg, Vector3 a, Vector3 b, Vector3 motion, Vector3 up)
        {
            float open = JacksMath.NetOpenFromGap(Vector3.Distance(a, b), cfg.NetClosedGap, cfg.NetOpenGap);
            float height = Mathf.Lerp(cfg.NetMinHeight, cfg.NetMaxHeight, open);
            return JacksMath.NetFrame.Between(a, b, motion, up, height, cfg.NetDepth);
        }

        /// <summary>
        /// The way a net between <paramref name="a"/> and <paramref name="b"/> is travelling, eased
        /// (<see cref="JacksMath.EaseTrail"/>): its bag hangs back against it. Each net that is drawn or run keeps its own.
        /// </summary>
        internal struct Trail
        {
            Vector3 _dir, _prevCentre;
            bool _has;

            internal Vector3 Step(JacksConfigSO cfg, Vector3 a, Vector3 b, Vector3 fallback, float dt)
            {
                var centre = 0.5f * (a + b);
                var velocity = _has && dt > 0f ? (centre - _prevCentre) / dt : Vector3.zero;
                if (!_has) _dir = fallback;
                _dir = JacksMath.EaseTrail(_dir, velocity, fallback, TrailMinSpeed, cfg.NetTrailEase, dt);
                _prevCentre = centre;
                _has = true;
                return _dir;
            }

            internal void Reset() => _has = false;
        }

        // below this speed (u/s) the bag holds the way it was hanging
        const float TrailMinSpeed = 2f;

        Trail _trail;

        /// <summary>Whether the fish are close enough to hold a net: at take-off they swim in from the herding board, and a
        /// net strung across that whole gap would sweep the board. The net opens once they are in formation.</summary>
        internal static bool Formed(JacksConfigSO cfg, Vector3 a, Vector3 b) =>
            (a - b).sqrMagnitude <= Sq(cfg.NetOpenGap * FormedSlack);

        // the fish are "in formation" this far past the net's widest gap
        const float FormedSlack = 1.25f;

        static float Sq(float x) => x * x;

        /// <summary>One netting frame: place the net, carry its crystals, and take what crosses its mouth.</summary>
        internal void Step(JacksExecutor ex, Vector3 a, Vector3 b, float dt)
        {
            var cfg = ex.Config;
            if (!Formed(cfg, a, b))
            {
                if (Open) Release(ex);
                return;
            }
            var status = ex.Status;
            var hull = status.Transform;
            Frame = FrameFor(cfg, a, b, _trail.Step(cfg, a, b, hull.forward, dt), hull.up);
            Open = true;

            Carry(cfg, dt);
            CatchCrystals();
            DragPrisms(ex, dt);
            CatchPrisms(ex);
            NetFauna(ex);
            if (ElementalTransfer.IsDecidedHere(status)) TakeFromRivals(ex);
        }

        /// <summary>
        /// The net goes away (the jacks land, or stop): what it carried stays where it is. The machine that decides the hull
        /// puts the cell's omni crystal there for everyone; a loose crystal is local to each machine and simply stays.
        /// </summary>
        internal void Release(JacksExecutor ex)
        {
            bool decides = ex.Status != null && ElementalTransfer.IsDecidedHere(ex.Status);
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                var c = item.Crystal;
                if (!c || !item.Omni || !decides || c.IsExploding || !c.CrystalManager) continue;
                c.CrystalManager.RelocateCrystal(c.Id, c.transform.position);
            }
            _items.Clear();
            _dragged.Clear();
            _draggedSet.Clear();
            _inside.Clear();
            _trail.Reset();
            Open = false;
        }

        // ------------------------------------------------------------------ crystals

        void Carry(JacksConfigSO cfg, float dt)
        {
            float settle = JacksMath.Ease(cfg.NetSettleEase, dt);
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var item = _items[i];
                var c = item.Crystal;
                // gone, collected, or moved by something else (a respawn, another hull's pickup): no longer ours
                if (!c || !c.isActiveAndEnabled || c.IsExploding
                    || (c.transform.position - item.Placed).sqrMagnitude > ExternalMoveSqr)
                {
                    _items.RemoveAt(i);
                    continue;
                }
                item.W += (cfg.NetSettle - item.W) * settle;
                item.Placed = Frame.Point(item.S, item.T, item.W);
                c.transform.position = item.Placed;
            }
        }

        // a carried crystal further than this from where the net left it was moved by something else
        const float ExternalMoveSqr = 15f * 15f;

        void CatchCrystals()
        {
            var active = Crystal.Active;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (i >= active.Count) continue;
                var c = active[i];
                if (!c || !c.isActiveAndEnabled || c.IsExploding || c.IsEmbedded || Carrying(c)) continue;
                var impactor = ImpactorOf(c);
                bool omni = impactor is OmniCrystalImpactor;
                if (!omni && !(impactor is ElementalCrystalImpactor e && !e.HasBeenCollected)) continue;
                float margin = 0.5f * c.transform.lossyScale.x;
                if (!Frame.Contains(c.transform.position, margin, out float s, out float t, out float w)) continue;
                // a crystal knocked out of a hull stops being thrown: the net holds it
                if (c.TryGetComponent(out EjectedCrystal thrown)) thrown.enabled = false;
                _items.Add(new Item
                {
                    Crystal = c,
                    Omni = omni,
                    S = Mathf.Clamp(s, 0.08f, 0.92f),
                    T = Mathf.Clamp(t, -0.8f, 0.8f),
                    W = Mathf.Clamp01(w),
                    Placed = c.transform.position,
                });
            }
        }

        bool Carrying(Crystal c)
        {
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].Crystal == c) return true;
            return false;
        }

        CrystalImpactor ImpactorOf(Crystal c)
        {
            if (_impactorOf.TryGetValue(c, out var impactor) && impactor) return impactor;
            impactor = c.GetComponentInChildren<CrystalImpactor>(true);
            _impactorOf[c] = impactor;
            return impactor;
        }

        /// <summary>
        /// Herding (Garrett, 2026-10-24: "the net goes away and they can collect the crystals netted"): a jack that touches a
        /// crystal collects it, the two halves a physics contact would dispatch (the Starlings' and the race rings' seam).
        /// An elemental crystal is taken by the hull's near-field skimmer on every peer, as a skimmer contact is; the omni
        /// crystal's pickup is decided on the machine that decides the hull and credited by the crystal's authority
        /// (<see cref="CrystalManager.ReportRingThreaded"/>, the race ring's report of a crystal taken off the hull).
        /// </summary>
        internal void CollectByJacks(JacksExecutor ex)
        {
            var status = ex.Status;
            var cfg = ex.Config;
            float reach2 = cfg.JackCrystalReach * cfg.JackCrystalReach, height = cfg.JackCrystalHeight;
            var up = ex.BoardUp;
            var skimmerOwner = status.NearFieldSkimmer;
            var skimmer = skimmerOwner ? skimmerOwner.GetComponentInChildren<SkimmerImpactor>(true) : null;
            var vessel = ex.HullImpactor;
            bool decides = ElementalTransfer.IsDecidedHere(status);
            var active = Crystal.Active;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (i >= active.Count) continue;
                var c = active[i];
                if (!c || !c.isActiveAndEnabled || c.IsExploding || c.IsEmbedded || !c.CanBeCollected(status.Domain)) continue;
                var p = c.transform.position;
                // across the board, as the herding view shows it: a crystal under a jack is the jack's, however high it sits
                if (!Under(p, ex.JackWorld(0), up, reach2, height) && !Under(p, ex.JackWorld(1), up, reach2, height)) continue;
                if (_collectedAt.TryGetValue(c, out float at) && Time.time - at < 0.5f) continue;
                var impactor = ImpactorOf(c);
                if (impactor is ElementalCrystalImpactor elemental && !elemental.HasBeenCollected && skimmer)
                {
                    _collectedAt[c] = Time.time;
                    elemental.CollectBy(skimmer);
                }
                else if (impactor is OmniCrystalImpactor omni && decides && vessel && c.CrystalManager)
                {
                    _collectedAt[c] = Time.time;
                    vessel.AcceptImpacteeFromSweep(omni);
                    c.CrystalManager.ReportRingThreaded(c.Id, p, vessel);
                }
            }
        }

        static bool Under(Vector3 crystal, Vector3 jack, Vector3 up, float reach2, float height)
        {
            var d = crystal - jack;
            float h = Vector3.Dot(d, up);
            return Mathf.Abs(h) <= height && (d - up * h).sqrMagnitude <= reach2;
        }

        // ------------------------------------------------------------------ prisms: dragged, broken only by prisms

        /// <summary>
        /// The net's prisms ride where it holds them, and each step is a MOVE of the prism-motion fundamental
        /// (<see cref="PrismMotion"/>, <c>Docs/PRISM_MOTION.md</c>): the transform, the collider, the index and the shell agree
        /// the frame it is made, and a dragged prism that runs into another prism destroys both, scored to us. The net's own
        /// prisms are movers of the same step, so the ones it carries side by side never meet (a pair already touching is at
        /// rest), and one netted prism never breaks another. Super-shielded mass is never dragged (no force moves it).
        /// </summary>
        void DragPrisms(JacksExecutor ex, float dt)
        {
            for (int i = _dragged.Count - 1; i >= 0; i--)
                if (!_dragged[i].Prism || _dragged[i].Prism.destroyed || _dragged[i].Prism.SpatialIndexId < 0)
                {
                    _draggedSet.Remove(_dragged[i].Prism);
                    _dragged.RemoveAt(i);
                }
            int n = _dragged.Count;
            if (n == 0) return;
            var index = PrismSpatialIndex.Instance;
            if (index == null || !index.IsAvailable) return;
            EnsureNative(ex.Config.NetPrismCap);

            _moverOfSlot.Clear();
            for (int i = 0; i < n; i++)
            {
                var d = _dragged[i];
                var p = d.Prism;
                _from[i] = p.transform.position;
                _to[i] = Frame.Point(d.S, d.T, d.W);
                _moverOfSlot.TryAdd(p.SpatialIndexId, i);
                p.transform.position = _to[i];
                p.NotifyPositionChanged();
            }

            var cfg = PrismMotionConfigSO.Instance;
            _level = PrismMotion.NextLevel(_level, n,
                cfg != null ? cfg.ShowcaseMaxMovers : PrismMotionConfigSO.DefaultShowcaseMaxMovers,
                cfg != null ? cfg.BoxMaxMovers : PrismMotionConfigSO.DefaultBoxMaxMovers);
            var movers = new PrismMotion.MoverSteps(_moverOfSlot, _from, _to);
            var status = ex.Status;
            for (int i = 0; i < n; i++)
            {
                var p = _dragged[i].Prism;
                if (!p || p.destroyed) continue;
                float3 step = _to[i] - _from[i];
                if (math.lengthsq(step) < 1e-12f || !PrismMotion.TryGetBox(p, out var box)) continue;
                box.Center -= step;   // the transform already stands at the end of the step
                if (!PrismMotion.TryFirstContact(index, p, in box, step, in movers, out int slot, out _)) continue;
                if (_moverOfSlot.ContainsKey(slot)) continue;   // another of the net's own: they travel together
                var struck = index.ResolvePrism(slot, materialise: true);
                if (!struck || struck == p || struck.destroyed) continue;
                var closing = dt > 0f ? (Vector3)(step / dt) : Vector3.zero;
                PrismMotion.Annihilate(p, struck, closing, status.Domain, status.PlayerName, _level);
            }
        }

        /// <summary>A prism the net's mouth crosses is caught and dragged (up to <c>netPrismCap</c>); not a creature's body
        /// or a plant's (living mass is posed by its owner), not super-shielded mass, not an environment-owned track, and
        /// not the trail still coming out of our own hull.</summary>
        void CatchPrisms(JacksExecutor ex)
        {
            int cap = ex.Config.NetPrismCap;
            if (_dragged.Count >= cap) return;
            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return;
            var f = Frame;
            var status = ex.Status;
            float radius = 0.5f * Mathf.Sqrt(f.Width * f.Width + f.Height * f.Height + f.Depth * f.Depth) + 4f;
            int found = index.QuerySphere(f.Centre, radius, _prisms);
            for (int i = 0; i < found && _dragged.Count < cap; i++)
            {
                var p = _prisms[i];
                if (!p || p.destroyed || p.SpatialIndexId < 0 || _draggedSet.Contains(p)) continue;
                if (p is HealthPrism || p.IsEnvironmentOwned || p.prismProperties is { IsSuperShielded: true }) continue;
                if (SelfTrailContactConfigSO.SuppressesHullContact(p, status)) continue;
                if (!f.Contains(p.transform.position, 0.5f * p.transform.lossyScale.magnitude, out float s, out float t, out float w))
                    continue;
                _dragged.Add(new Dragged
                {
                    Prism = p,
                    S = Mathf.Clamp(s, 0.05f, 0.95f),
                    T = Mathf.Clamp(t, -0.9f, 0.9f),
                    W = Mathf.Clamp01(w),
                });
                _draggedSet.Add(p);
            }
            _prisms.Clear();
        }

        void EnsureNative(int cap)
        {
            cap = Mathf.Max(1, cap);
            if (_from.IsCreated && _from.Length >= cap) return;
            DisposeNative();
            _moverOfSlot = new NativeParallelHashMap<int, int>(cap, Allocator.Persistent);
            _from = new NativeArray<float3>(cap, Allocator.Persistent);
            _to = new NativeArray<float3>(cap, Allocator.Persistent);
        }

        void DisposeNative()
        {
            if (_moverOfSlot.IsCreated) _moverOfSlot.Dispose();
            if (_from.IsCreated) _from.Dispose();
            if (_to.IsCreated) _to.Dispose();
        }

        /// <summary>The executor is gone: the native buffers go with it.</summary>
        internal void Dispose() => DisposeNative();

        // ------------------------------------------------------------------ creatures: stolen, shielded, herded

        void NetFauna(JacksExecutor ex)
        {
            var own = ex.Status.Domain;
            var f = Frame;
            _insideNext.Clear();
            var cells = Cell.ActiveCellsSnapshot;
            for (int ci = 0; ci < cells.Count; ci++)
            {
                var cell = cells[ci];
                if (!cell) continue;
                var live = cell.LiveFauna;
                for (int i = 0; i < live.Count; i++)
                {
                    var creature = live[i];
                    if (!creature || creature.IsDying || !creature.IsSimAuthority) continue;
                    if (!f.Contains(creature.transform.position, 2f, out _, out _, out _)) continue;
                    _insideNext.Add(creature);
                    if (_inside.Contains(creature)) continue;   // once each time through the net
                    if (creature.Domain != own) creature.StealInto(own);
                    if (creature.Domain != own) continue;
                    creature.ShieldBody();
                    ex.TryHerd(creature);
                }
            }
            (_inside, _insideNext) = (_insideNext, _inside);
        }

        // ------------------------------------------------------------------ rival hulls: crystals knocked into the net

        void TakeFromRivals(JacksExecutor ex)
        {
            var cfg = ex.Config;
            if (cfg.NetRivalTake <= 0f) return;
            var status = ex.Status;
            if (status.Player is { IsNetworkOwner: false }) return;
            var rivals = ex.Vessels;
            for (int i = 0; i < rivals.Count; i++)
            {
                var v = rivals[i];
                if (!v || v.Player == null) continue;
                IVesselStatus victim = v;
                if (victim.Domain == status.Domain || victim.Transform == status.Transform) continue;
                if (!Frame.Contains(v.transform.position, RivalMargin, out _, out _, out _)) continue;
                if (_rivalReadyAt.TryGetValue(victim, out float ready) && Time.time < ready) continue;
                _rivalReadyAt[victim] = Time.time + cfg.NetRivalCooldown;
                // knocked loose backward, into the bag: the net catches them as they come out of the hull
                ElementalTransfer.ApplyAllAuthoritative(ElementalTransferForm.Eject, victim, status, cfg.NetRivalTake,
                                                        -Frame.Z * RivalThrowSpeed, ElementalDebuffSources.VesselContact);
            }
        }

        // a rival hull is netted this far outside the mouth too (its hull is not a point)
        const float RivalMargin = 6f;
        // the speed the crystals are knocked out of a netted hull (u/s), back into the bag
        const float RivalThrowSpeed = 15f;
    }
}
