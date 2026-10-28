using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Meerkat's Space ability, the <b>soliton</b>'s swallow and fire (Garrett's design, approved by Yash 2026-10-28;
    /// <c>R_VesselActions/MEERKAT.md</c> §8.1), ported from the page's <c>kitSwallow</c>, <c>kitTube</c>, <c>kitFire</c> and
    /// <c>kitMovers</c>. A mover of the prism-motion fundamental (<see cref="PrismMotion"/>, <c>Docs/PRISM_MOTION.md</c>).
    /// <list type="bullet">
    /// <item><b>Swallow.</b> A standing soliton of this Meerkat's takes the prisms within <c>swallowShare</c> (1.5) × its opening
    /// radius of its way in, four looks a second, shielded ones too; super-shielded mass is never moved, a creature's body is
    /// the creature's, and the Meerkat's own trail just laid is left alone (the self-trail grace). A moving prism flying into a
    /// way in is swallowed again. Space L5, <b>Steal</b>: each is stolen into the Meerkat's domain the moment its run starts,
    /// so it leaves the way out already theirs.</item>
    /// <item><b>Carry.</b> It runs the tube at <c>carrySpeed</c>, keeping its offset from the axis (inside half the throat).
    /// Posed, like a net's catch: it does not run the collision rule inside the tube.</item>
    /// <item><b>Fire.</b> Out of the way out along its heading at <c>fireSpeed</c>, a MOVING prism gliding to rest over
    /// <c>fireGlideSeconds</c>. A collapsing soliton fires everything it holds at once. Each step is a move of the fundamental:
    /// the transform, the index and the shell agree the frame it is made (<see cref="Prism.NotifyPositionChanged"/>); a moving
    /// prism that runs into a plain prism destroys BOTH, scored to the Meerkat (<see cref="PrismMotion.Annihilate"/>); a
    /// collision a shielded prism is part of BOUNCES (<see cref="PrismMotion.Bounces"/>, rule 1a); a plain mover against
    /// super-shielded mass goes. At rest it stays where it stopped: moved, never deleted.</item>
    /// </list>
    /// Every peer runs it on its own prisms from the same replicated solitons (the fundamental's per-peer stance, §4).
    /// At most <c>solitonPrismCap</c> are carried or moving per Meerkat; the rest stay where they are.
    /// </summary>
    sealed class MeerkatSolitonPrisms
    {
        sealed class Item
        {
            public Prism Prism;
            public float Born;                          // its life's creation stamp (vessel rule 4.x)
            public MeerkatWormholeExecutor.Tunnel Tube; // carrying it, or null once fired
            public float S;                             // how far along the tube
            public Vector3 Offset, Velocity;
            public float Age;
        }

        const float LookSeconds = 0.25f;   // the page's four looks a second

        // a prism one Meerkat carries is no other's (two solitons' mouths can overlap)
        static readonly HashSet<Prism> s_claimed = new();

        readonly List<Item> _items = new();
        readonly List<Prism> _query = new();
        readonly Dictionary<MeerkatWormholeExecutor.Tunnel, float> _nextLook = new();
        readonly List<MeerkatWormholeExecutor.Tunnel> _staleLooks = new();
        NativeParallelHashMap<int, int> _moverOfSlot;
        NativeArray<float3> _from, _to;
        PrismMotionLevel _level = PrismMotionLevel.Showcase;

        /// <summary>Prisms in a tube or moving now.</summary>
        public int Count => _items.Count;

        public void Step(MeerkatSolitonKit kit, float dt)
        {
            var cfg = kit.Config;
            Prune();
            Swallow(kit, cfg);
            Carry(kit, cfg, dt);
            Move(kit, cfg, dt);
        }

        // ------------------------------------------------------------------ swallow

        void Swallow(MeerkatSolitonKit kit, MeerkatWormholeConfigSO cfg)
        {
            var index = PrismSpatialIndex.Instance;
            if (index == null || !index.IsAvailable) return;
            var maker = kit.Status;
            float now = Time.time;
            var live = MeerkatWormholeExecutor.Live;
            for (int ti = 0; ti < live.Count; ti++)
            {
                var t = live[ti];
                if (t.Maker != kit.Wormhole || !MeerkatSolitonKit.IsStanding(t)) continue;
                float mouth = Mathf.Max(0f, cfg.SwallowShare) * t.M;
                if (mouth <= 0f) continue;
                // a moving prism flying into its mouth
                for (int i = 0; i < _items.Count; i++)
                {
                    var it = _items[i];
                    if (it.Tube == null && (it.Prism.transform.position - t.A).sqrMagnitude <= mouth * mouth &&
                        it.Prism.prismProperties is not { IsSuperShielded: true })
                        Take(kit, it, t);
                }
                if (_nextLook.TryGetValue(t, out float next) && now < next) continue;
                _nextLook[t] = now + LookSeconds;
                int n = index.QuerySphere(t.A, mouth, _query);
                for (int i = 0; i < n && _items.Count < cfg.SolitonPrismCap; i++)
                {
                    var p = _query[i];
                    if (!Swallowable(p, maker)) continue;
                    var it = new Item { Prism = p, Born = p.prismProperties.TimeCreated };
                    _items.Add(it);
                    s_claimed.Add(p);
                    Take(kit, it, t);
                }
                _query.Clear();
            }
            // forget the looks of solitons that are gone
            _staleLooks.Clear();
            foreach (var kv in _nextLook)
                if (!MeerkatSolitonKit.IsStanding(kv.Key)) _staleLooks.Add(kv.Key);
            for (int i = 0; i < _staleLooks.Count; i++) _nextLook.Remove(_staleLooks[i]);
        }

        static bool Swallowable(Prism p, IVesselStatus maker)
        {
            if (!p || p.destroyed || p.SpatialIndexId < 0 || !p.IsCreationComplete || s_claimed.Contains(p)) return false;
            var props = p.prismProperties;
            if (props == null || props.IsSuperShielded) return false;               // super-shielded mass is never moved
            if (p is HealthPrism living && living.ResolveOwnerFauna()) return false; // a creature's body is the creature's
            return !SelfTrailContactConfigSO.SuppressesHullContact(p, maker);       // the trail just laid stays behind
        }

        static void Take(MeerkatSolitonKit kit, Item it, MeerkatWormholeExecutor.Tunnel t)
        {
            it.Tube = t;
            it.S = 0f;
            it.Velocity = Vector3.zero;
            it.Offset = MeerkatSolitonMath.CarryOffset(it.Prism.transform.position, t.A, t.T0, t.B);
            if ((t.Kit & MeerkatSolitonKit.KitSteal) != 0)
            {
                var maker = kit.Status;
                if (it.Prism.Domain != maker.Domain) it.Prism.Steal(maker.PlayerName, maker.Domain);   // Space L5: theirs from here
            }
        }

        // ------------------------------------------------------------------ carry and fire

        void Carry(MeerkatSolitonKit kit, MeerkatWormholeConfigSO cfg, float dt)
        {
            var live = MeerkatWormholeExecutor.Live;
            for (int i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                var t = it.Tube;
                if (t == null) continue;
                bool open = !t.Dying && t.W.Phase is not (MeerkatWormholeMath.Phase.Collapse or MeerkatWormholeMath.Phase.Gone) &&
                            Contains(live, t);
                it.S += Mathf.Max(1f, cfg.CarrySpeed) * dt;
                if (!open || it.S >= t.L)
                {
                    Fire(kit, cfg, it, t);   // out of the way out (a collapsing soliton fires all it holds at once)
                    continue;
                }
                Pose(it.Prism, t.At(it.S, out _) + it.Offset);
            }
        }

        static void Fire(MeerkatSolitonKit kit, MeerkatWormholeConfigSO cfg, Item it, MeerkatWormholeExecutor.Tunnel t)
        {
            var exit = t.At(t.L, out var tangent);
            Pose(it.Prism, exit + it.Offset);
            it.Velocity = MeerkatSolitonMath.FireVelocity(tangent, it.Offset, cfg.FireSpeed);
            it.Tube = null;
            it.Age = 0f;
            kit.OnPrismFired(exit);
        }

        static void Pose(Prism p, Vector3 at)
        {
            p.transform.position = at;
            p.prismProperties.position = at;
            p.NotifyPositionChanged();
        }

        static bool Contains(IReadOnlyList<MeerkatWormholeExecutor.Tunnel> list, MeerkatWormholeExecutor.Tunnel t)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == t) return true;
            return false;
        }

        // ------------------------------------------------------------------ the moving prisms (the fundamental)

        void Move(MeerkatSolitonKit kit, MeerkatWormholeConfigSO cfg, float dt)
        {
            int movers = 0;
            for (int i = 0; i < _items.Count; i++) if (_items[i].Tube == null) movers++;
            if (movers == 0 || dt <= 0f) return;
            var index = PrismSpatialIndex.Instance;
            EnsureNative(Mathf.Max(movers, cfg.SolitonPrismCap));
            _moverOfSlot.Clear();
            // every mover steps first, so the movers of one step meet each other along their own steps (MoverSteps)
            int j = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                if (it.Tube != null) continue;
                var p = it.Prism;
                Vector3 from = p.transform.position, to = from + it.Velocity * dt;
                _from[j] = from;
                _to[j] = to;
                _moverOfSlot.TryAdd(p.SpatialIndexId, j);
                Pose(p, to);
                j++;
            }
            var mcfg = PrismMotionConfigSO.Instance;
            _level = PrismMotion.NextLevel(_level, movers,
                mcfg != null ? mcfg.ShowcaseMaxMovers : PrismMotionConfigSO.DefaultShowcaseMaxMovers,
                mcfg != null ? mcfg.BoxMaxMovers : PrismMotionConfigSO.DefaultBoxMaxMovers);
            var steps = new PrismMotion.MoverSteps(_moverOfSlot, _from, _to);
            var maker = kit.Status;
            float restitution = PrismMotion.BounceRestitution;
            j = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                if (it.Tube != null) continue;
                int k = j++;
                var p = it.Prism;
                if (!p || p.destroyed) continue;
                float3 step = _to[k] - _from[k];
                if (index != null && index.IsAvailable && math.lengthsq(step) > 1e-12f && PrismMotion.TryGetBox(p, out var box))
                {
                    box.Center -= step;   // the transform already stands at the end of the step
                    if (PrismMotion.TryFirstContact(index, p, in box, step, in steps, out int slot, out float hit))
                    {
                        var struck = index.ResolvePrism(slot, materialise: true);
                        if (struck && struck != p && !struck.destroyed)
                        {
                            bool struckMoves = _moverOfSlot.TryGetValue(slot, out int sj);
                            Vector3 otherV = struckMoves ? (Vector3)((_to[sj] - _from[sj]) / dt) : Vector3.zero;
                            Vector3 closing = it.Velocity - otherV;
                            if (PrismMotion.Bounces(p, struck))
                            {
                                // rule 1a: the shielded collision bounces, neither goes; it rebounds off the contact
                                Vector3 contact = math.lerp(_from[k], _to[k], hit);
                                var n = PrismBounce.Normal(contact, struck.transform.position, closing);
                                it.Velocity += PrismBounce.Impulse(closing, n, restitution, struckMoves ? 0.5f : 1f);
                                Pose(p, contact + n * 0.01f);
                            }
                            else
                            {
                                // rule 1: both go, scored to the Meerkat (a plain mover into super-shielded mass goes alone)
                                PrismMotion.Annihilate(p, struck, closing, maker.Domain, maker.PlayerName, _level);
                                continue;
                            }
                        }
                    }
                }
                it.Velocity = MeerkatSolitonMath.Glide(it.Velocity, dt, cfg.FireGlideSeconds);
                it.Age += dt;
            }
            // at rest where it stopped: moved, never deleted; it is free mass again
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var it = _items[i];
                if (it.Tube != null || !MeerkatSolitonMath.AtRest(it.Velocity, it.Age)) continue;
                s_claimed.Remove(it.Prism);
                _items.RemoveAt(i);
            }
        }

        /// <summary>Drops a prism that died, went back to the pool or was handed to another life (identity, vessel rule 30).</summary>
        void Prune()
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var p = _items[i].Prism;
                if (p && !p.destroyed && p.SpatialIndexId >= 0 && p.prismProperties != null &&
                    p.prismProperties.TimeCreated == _items[i].Born) continue;
                if (p) s_claimed.Remove(p);
                else s_claimed.RemoveWhere(q => !q);
                _items.RemoveAt(i);
            }
        }

        /// <summary>The Meerkat is gone: what it carried stays where it is, free mass.</summary>
        public void Release()
        {
            for (int i = 0; i < _items.Count; i++) if (_items[i].Prism) s_claimed.Remove(_items[i].Prism);
            _items.Clear();
            _nextLook.Clear();
        }

        void EnsureNative(int cap)
        {
            cap = Mathf.Max(1, cap);
            if (_from.IsCreated && _from.Length >= cap) return;
            Dispose();
            _moverOfSlot = new NativeParallelHashMap<int, int>(cap, Allocator.Persistent);
            _from = new NativeArray<float3>(cap, Allocator.Persistent);
            _to = new NativeArray<float3>(cap, Allocator.Persistent);
        }

        /// <summary>The kit is gone: the native buffers go with it.</summary>
        public void Dispose()
        {
            if (_moverOfSlot.IsCreated) _moverOfSlot.Dispose();
            if (_from.IsCreated) _from.Dispose();
            if (_to.IsCreated) _to.Dispose();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad() => s_claimed.Clear();
    }
}
