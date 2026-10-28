using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The soliton's swallow (round 23, Yash, 2026-10-28: "the prism gets sucked in from this one side and gets sucked out on
    /// the other. And the prisms will be distorted while traveling through this wormhole. The prisms and all the
    /// environmental materials that are destructible or interactable ... sucked in. From a certain radius. And sucked out
    /// from the other side"; Garrett's soliton, <c>Docs/Studios/MEERKAT.md</c>). The page's <c>kitSwallow</c>,
    /// <c>kitPull</c>, <c>kitTube</c>, <c>kitFire</c>, <c>kitMovers</c> and <c>kitCrystals</c>, for one Meerkat's tunnels:
    ///
    /// <list type="bullet">
    /// <item><b>The pull.</b> Every prism (shielded too, never super-shielded) and every crystal within <c>suckRadius</c> of
    /// a standing tunnel's way in is drawn to it (<see cref="MeerkatWormholeMath.PullVelocity"/>), four looks a second.
    /// A plant's prism is plucked first (<see cref="HealthPrism.LeaveAsSkeleton"/>), as the Jacks' net does; a creature's
    /// body, a prism still growing in and the trail still coming out of our own hull are left alone.</item>
    /// <item><b>The tube.</b> Within <c>swallowShare</c> × the opening radius it is swallowed: carried at
    /// <c>carrySpeed</c>, held to the wall's narrowing, stretched along the tube and twisted
    /// (<see cref="MeerkatWormholeMath.CarryDistortion"/>), its authored scale put back at the way out.</item>
    /// <item><b>The fire.</b> Out of the way out along its last heading at <c>fireSpeed</c>, gliding to rest over
    /// <c>fireGlideSeconds</c>. A tunnel that collapses fires out all it holds at once.</item>
    /// </list>
    ///
    /// <para>A pulled or fired prism is a MOVING prism (<c>Docs/PRISM_MOTION.md</c>): one that runs into a plain prism destroys
    /// both, scored to the tunnel's maker; a shielded collision bounces. Everything this swallow moves is a mover of the same
    /// step (<see cref="PrismMotion.MoverSteps"/>), so what it pulls together never breaks itself, as the Jacks' net's catch
    /// does not. Mass is moved, never deleted.</para>
    ///
    /// <para><b>Where it runs.</b> Every peer runs every Meerkat's swallow on the tunnels it has (each owner publishes its
    /// tunnels, <see cref="MeerkatWormholeNetworkRelay"/>), so each machine moves the prisms and loose crystals it simulates,
    /// as the Jacks' net does; the machine that decides the hull puts the cell's omni crystal where it comes to rest for
    /// everyone (<see cref="CrystalManager.RelocateCrystal"/>).</para>
    /// </summary>
    sealed class MeerkatSwallow
    {
        enum Stage { Pull, Tube, Glide }

        sealed class Body
        {
            public Prism Prism;
            public Stage Stage;
            public MeerkatWormholeExecutor.Tunnel Tunnel;
            public float S, Age;
            public Vector3 Off;     // across the way in, as a share of the opening radius (the page's k.off / wh.M)
            public Vector3 V;
            public Vector3 Scale;   // its authored scale, put back at the way out
            public int Seed;
        }

        sealed class Carried
        {
            public Crystal Crystal;
            public bool Omni;
            public Stage Stage;
            public MeerkatWormholeExecutor.Tunnel Tunnel;
            public float S;
            public Vector3 Off, V, Placed, Scale;
            public int Seed;
        }

        // the page's KP_MAX share of a frame: the prisms past it wait for the next look
        const float LookSeconds = 0.25f;
        // a pulled or fired thing is at rest under this speed (the page's v^2 < 9), or after this long (its m.age > 25)
        const float RestSpeedSqr = 9f, RestAge = 25f;
        // a carried crystal further than this from where we left it was moved by something else (the net's rule)
        const float ExternalMoveSqr = 15f * 15f;

        readonly List<Body> _bodies = new();
        readonly HashSet<Prism> _taken = new();
        readonly List<Carried> _crystals = new();
        readonly List<Prism> _found = new();
        readonly Dictionary<Crystal, CrystalImpactor> _impactorOf = new();
        NativeParallelHashMap<int, int> _moverOfSlot;
        NativeArray<float3> _from, _to;
        PrismMotionLevel _level = PrismMotionLevel.Showcase;
        int _seed;

        /// <summary>How many prisms this Meerkat's tunnels are moving (pulled, carried or gliding).</summary>
        internal int PrismCount => _bodies.Count;
        /// <summary>How many crystals they are moving.</summary>
        internal int CrystalCount => _crystals.Count;

        /// <summary>The page's <c>standing</c>: a formed tunnel, whole from mouth to mouth, not collapsing.</summary>
        internal static bool Standing(MeerkatWormholeExecutor.Tunnel t) =>
            t != null && !t.Dying && (t.W.Phase == MeerkatWormholeMath.Phase.Open || t.W.Phase == MeerkatWormholeMath.Phase.Stay)
            && t.W.S0 <= 0.5f && t.W.S1 >= t.L - 0.5f;

        static bool Alive(MeerkatWormholeExecutor.Tunnel t, MeerkatWormholeExecutor maker)
        {
            if (t == null || t.Dying || t.W.Phase == MeerkatWormholeMath.Phase.Collapse) return false;
            var live = MeerkatWormholeExecutor.Live;
            for (int i = 0; i < live.Count; i++)
                if (live[i] == t) return t.Maker == maker;
            return false;
        }

        /// <summary>One frame: look round each standing tunnel's way in, then move all it pulls, carries and fires.</summary>
        internal void Step(MeerkatWormholeExecutor ex, float dt)
        {
            var cfg = ex.Config;
            if (!cfg || dt <= 0f) return;
            var live = MeerkatWormholeExecutor.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var t = live[i];
                if (t.Maker != ex || !Standing(t) || Time.time < t.SwallowLook) continue;
                t.SwallowLook = Time.time + LookSeconds;
                CatchPrisms(ex, t);
                CatchCrystals(cfg, t);
            }
            if (_bodies.Count > 0) MovePrisms(ex, dt);
            if (_crystals.Count > 0) MoveCrystals(ex, dt);
        }

        /// <summary>The hull goes (a swap or despawn): what is in a tube is set down where it is with its own scale, what moves
        /// stops, and the cell's omni crystal is put where it is for everyone.</summary>
        internal void Release(MeerkatWormholeExecutor ex)
        {
            for (int i = 0; i < _bodies.Count; i++)
            {
                var b = _bodies[i];
                if (!b.Prism || b.Prism.destroyed || b.Stage != Stage.Tube) continue;
                b.Prism.transform.localScale = b.Scale;
                b.Prism.NotifyPositionChanged();
            }
            _bodies.Clear();
            _taken.Clear();
            bool decides = ex != null && ex.Status != null && ElementalTransfer.IsDecidedHere(ex.Status);
            for (int i = 0; i < _crystals.Count; i++)
            {
                var c = _crystals[i];
                if (!c.Crystal) continue;
                c.Crystal.transform.localScale = c.Scale;
                if (c.Omni && decides && !c.Crystal.IsExploding && c.Crystal.CrystalManager)
                    c.Crystal.CrystalManager.RelocateCrystal(c.Crystal.Id, c.Crystal.transform.position);
            }
            _crystals.Clear();
            DisposeNative();
        }

        // ------------------------------------------------------------------ prisms

        void CatchPrisms(MeerkatWormholeExecutor ex, MeerkatWormholeExecutor.Tunnel t)
        {
            var cfg = ex.Config;
            int cap = Mathf.Max(1, cfg.SwallowPrismCap);
            if (_bodies.Count >= cap) return;
            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return;
            var status = ex.Status;
            float rz = Mathf.Max(0f, cfg.SwallowShare) * t.M, rs = Mathf.Max(rz, cfg.SuckRadius);
            if (rs <= 0f) return;
            int found = index.QuerySphere(t.A, rs, _found);
            for (int i = 0; i < found && _bodies.Count < cap; i++)
            {
                var p = _found[i];
                if (!p || p.destroyed || p.SpatialIndexId < 0 || _taken.Contains(p)) continue;
                if (!p.IsCreationComplete || p.IsGrowing || p.prismProperties is { IsSuperShielded: true }) continue;
                if (status != null && SelfTrailContactConfigSO.SuppressesHullContact(p, status)) continue;
                var living = p as HealthPrism;
                if (living && living.ResolveOwnerFauna()) continue;   // a creature's body: the creature owns it
                if ((p.transform.position - t.A).sqrMagnitude > rs * rs) continue;
                if (living)
                {
                    // plucked: free cell mass from here on (the Jacks' net's rule), then moved like any other
                    var cell = Cell.FindNearestActiveCell(p.transform.position);
                    living.LeaveAsSkeleton(cell ? cell.transform : null);
                    if (p.destroyed || p.SpatialIndexId < 0) continue;
                }
                _bodies.Add(new Body { Prism = p, Stage = Stage.Pull, Tunnel = t, Scale = p.transform.localScale, Seed = _seed++ });
                _taken.Add(p);
            }
            _found.Clear();
        }

        void MovePrisms(MeerkatWormholeExecutor ex, float dt)
        {
            for (int i = _bodies.Count - 1; i >= 0; i--)
            {
                var b = _bodies[i];
                if (b.Prism && !b.Prism.destroyed && b.Prism.SpatialIndexId >= 0) continue;
                _taken.Remove(b.Prism);
                _bodies.RemoveAt(i);
            }
            int n = _bodies.Count;
            if (n == 0) return;
            var index = PrismSpatialIndex.Instance;
            if (index == null || !index.IsAvailable) return;
            var cfg = ex.Config;
            EnsureNative(Mathf.Max(n, cfg.SwallowPrismCap));
            _moverOfSlot.Clear();

            // where each one goes this step: pulled to the way in, carried through, or gliding out
            for (int i = 0; i < n; i++)
            {
                var b = _bodies[i];
                var tr = b.Prism.transform;
                _from[i] = tr.position;
                _moverOfSlot.TryAdd(b.Prism.SpatialIndexId, i);
                var t = b.Tunnel;
                if (b.Stage == Stage.Pull)
                {
                    if (!Standing(t)) b.Stage = Stage.Glide;   // it collapsed under it: it glides on, and rests where it stops
                    else if ((tr.position - t.A).sqrMagnitude <= Sq(Mathf.Max(0f, cfg.SwallowShare) * t.M)) Swallow(b, t, ex);
                    else b.V = MeerkatWormholeMath.PullVelocity(t.A, t.T0, tr.position, cfg.SuckSpeed, cfg.SuckRadius, cfg.CarrySpeed);
                }
                if (b.Stage == Stage.Tube)
                {
                    if (!Alive(t, ex)) Fire(cfg, b, ex);   // it collapses: all of it out at once
                    else
                    {
                        b.S += Mathf.Max(1f, cfg.CarrySpeed) * dt;
                        if (b.S >= t.L) Fire(cfg, b, ex);
                        else
                        {
                            Pose(cfg, b, t);
                            _to[i] = tr.position;
                            continue;
                        }
                    }
                }
                if (b.Stage == Stage.Glide)
                {
                    b.V *= Mathf.Exp(-dt / Mathf.Max(0.1f, cfg.FireGlideSeconds));
                    b.Age += dt;
                }
                _to[i] = tr.position + b.V * dt;
                if (b.V.sqrMagnitude > 1e-6f) tr.rotation = Quaternion.LookRotation(b.V);
                tr.position = _to[i];
                b.Prism.NotifyPositionChanged();
            }

            // what the pulled and gliding ones run into (the prism-motion fundamental); the carried ones are inside the tube
            var mcfg = PrismMotionConfigSO.Instance;
            _level = PrismMotion.NextLevel(_level, n,
                mcfg != null ? mcfg.ShowcaseMaxMovers : PrismMotionConfigSO.DefaultShowcaseMaxMovers,
                mcfg != null ? mcfg.BoxMaxMovers : PrismMotionConfigSO.DefaultBoxMaxMovers);
            var movers = new PrismMotion.MoverSteps(_moverOfSlot, _from, _to);
            var status = ex.Status;
            for (int i = 0; i < n; i++)
            {
                var b = _bodies[i];
                var p = b.Prism;
                if (!p || p.destroyed || b.Stage == Stage.Tube) continue;
                float3 step = _to[i] - _from[i];
                if (math.lengthsq(step) < 1e-12f || !PrismMotion.TryGetBox(p, out var box)) continue;
                box.Center -= step;   // the transform already stands at the end of the step
                if (!PrismMotion.TryFirstContact(index, p, in box, step, in movers, out int slot, out float hit)) continue;
                if (_moverOfSlot.ContainsKey(slot)) continue;   // another of ours: they travel together
                var struck = index.ResolvePrism(slot, materialise: true);
                if (!struck || struck == p || struck.destroyed) continue;
                if (PrismMotion.Bounces(p, struck))
                {
                    // 1a: a shielded collision bounces, the mover rebounding off the struck prism (which keeps still)
                    p.transform.position = math.lerp(_from[i], _to[i], hit);
                    var normal = PrismBounce.Normal(p.transform.position, struck.transform.position, b.V);
                    b.V += PrismBounce.Impulse(b.V, normal, PrismMotion.BounceRestitution, 1f);
                    if (b.Stage == Stage.Pull) b.Stage = Stage.Glide;   // knocked out of the pull
                    p.NotifyPositionChanged();
                    continue;
                }
                PrismMotion.Annihilate(p, struck, b.V, status != null ? status.Domain : p.Domain,
                    status != null ? status.PlayerName : string.Empty, _level);
            }

            // at rest where it stopped: moved, never deleted
            for (int i = _bodies.Count - 1; i >= 0; i--)
            {
                var b = _bodies[i];
                if (b.Stage != Stage.Glide || (b.V.sqrMagnitude >= RestSpeedSqr && b.Age <= RestAge)) continue;
                _taken.Remove(b.Prism);
                _bodies.RemoveAt(i);
            }
        }

        /// <summary>The page's <c>take</c>: into the tube, its place across the way in kept as a share of the opening.</summary>
        static void Swallow(Body b, MeerkatWormholeExecutor.Tunnel t, MeerkatWormholeExecutor ex)
        {
            b.Off = Across(b.Prism.transform.position, t);
            b.S = 0f;
            b.V = Vector3.zero;
            b.Stage = Stage.Tube;
            // [Garrett's kit hook] Space L5, Steal: the prism takes its maker's domain as its run starts, so it leaves the way
            // out already theirs. The bit is the one the tunnel's owner stamped and published (MeerkatSolitonKit.Stamp), so
            // every peer steals the same prisms.
            var maker = ex.Status;
            if ((t.Kit & MeerkatSolitonKit.KitSteal) != 0 && maker != null && b.Prism.Domain != maker.Domain)
                b.Prism.Steal(maker.PlayerName, maker.Domain);
        }

        static Vector3 Across(Vector3 p, MeerkatWormholeExecutor.Tunnel t)
        {
            var off = p - t.A;
            off -= t.T0 * Vector3.Dot(off, t.T0);
            float m = off.magnitude, lim = 0.5f * t.B;
            if (m > lim) off *= lim / m;
            return off / Mathf.Max(0.25f, t.M);
        }

        /// <summary>Carried: on the centreline at s plus its share of the wall's radius there, stretched and twisted.</summary>
        static void Pose(MeerkatWormholeConfigSO cfg, Body b, MeerkatWormholeExecutor.Tunnel t)
        {
            var tr = b.Prism.transform;
            var at = t.At(b.S, out var tangent);
            MeerkatWormholeMath.CarryDistortion(b.S, t.L, cfg.CarryStretch, cfg.CarryTwist, b.Seed, out float along,
                out float across, out float turn);
            tr.SetPositionAndRotation(at + b.Off * t.Radius(b.S),
                Quaternion.LookRotation(tangent) * Quaternion.AngleAxis(turn * Mathf.Rad2Deg, Vector3.forward));
            tr.localScale = new Vector3(b.Scale.x * across, b.Scale.y * across, b.Scale.z * along);
            b.Prism.NotifyPositionChanged();
        }

        /// <summary>The page's <c>kitFire</c>: out of the way out along its last heading, a little apart from the line.</summary>
        static void Fire(MeerkatWormholeConfigSO cfg, Body b, MeerkatWormholeExecutor ex)
        {
            var t = b.Tunnel;
            var exit = t.At(t.L, out var heading);
            var tr = b.Prism.transform;
            tr.position = exit + b.Off * t.Radius(t.L);
            tr.localScale = b.Scale;
            float speed = Mathf.Max(0f, cfg.FireSpeed);
            b.V = heading * speed;
            if (b.Off.sqrMagnitude > 1e-6f) b.V += b.Off.normalized * (0.06f * speed);
            b.Stage = Stage.Glide;
            b.Age = 0f;
            var kit = ex.Kit;   // [Garrett's kit hook] the prism-fired sound (an empty FMOD slot until sound design wires it)
            if (kit) kit.OnPrismFired(exit);
        }

        // ------------------------------------------------------------------ crystals

        void CatchCrystals(MeerkatWormholeConfigSO cfg, MeerkatWormholeExecutor.Tunnel t)
        {
            float rs = Mathf.Max(Mathf.Max(0f, cfg.SwallowShare) * t.M, cfg.SuckRadius), rs2 = rs * rs;
            if (rs <= 0f) return;
            var active = Crystal.Active;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (i >= active.Count) continue;
                var c = active[i];
                if (!c || !c.isActiveAndEnabled || c.IsExploding || c.IsEmbedded || Carrying(c)) continue;
                if ((c.transform.position - t.A).sqrMagnitude > rs2) continue;
                var impactor = ImpactorOf(c);
                bool omni = impactor is OmniCrystalImpactor;
                if (!omni && !(impactor is ElementalCrystalImpactor e && !e.HasBeenCollected)) continue;
                if (c.TryGetComponent(out EjectedCrystal thrown)) thrown.enabled = false;   // a thrown crystal is ours now
                _crystals.Add(new Carried
                {
                    Crystal = c, Omni = omni, Stage = Stage.Pull, Tunnel = t, Placed = c.transform.position,
                    Scale = c.transform.localScale, Seed = _seed++,
                });
            }
        }

        void MoveCrystals(MeerkatWormholeExecutor ex, float dt)
        {
            var cfg = ex.Config;
            bool decides = ex.Status != null && ElementalTransfer.IsDecidedHere(ex.Status);
            float glide = Mathf.Max(0.1f, cfg.FireGlideSeconds);
            for (int i = _crystals.Count - 1; i >= 0; i--)
            {
                var c = _crystals[i];
                var cr = c.Crystal;
                // gone, collected, or moved by something else (a respawn, a pickup, a net): no longer ours
                if (!cr || !cr.isActiveAndEnabled || cr.IsExploding || (cr.transform.position - c.Placed).sqrMagnitude > ExternalMoveSqr)
                {
                    if (cr) cr.transform.localScale = c.Scale;
                    _crystals.RemoveAt(i);
                    continue;
                }
                var tr = cr.transform;
                var t = c.Tunnel;
                if (c.Stage == Stage.Pull)
                {
                    if (!Standing(t)) c.Stage = Stage.Glide;
                    else if ((tr.position - t.A).sqrMagnitude <= Sq(Mathf.Max(0f, cfg.SwallowShare) * t.M))
                    {
                        c.Off = Across(tr.position, t);
                        c.S = 0f;
                        c.V = Vector3.zero;
                        c.Stage = Stage.Tube;
                    }
                    else c.V = MeerkatWormholeMath.PullVelocity(t.A, t.T0, tr.position, cfg.SuckSpeed, cfg.SuckRadius, cfg.CarrySpeed);
                }
                if (c.Stage == Stage.Tube)
                {
                    c.S += Mathf.Max(1f, cfg.CarrySpeed) * dt;
                    if (!Alive(t, ex) || c.S >= t.L)
                    {
                        // out of the way out, at half the fire speed (the page's crystals), its own shape back
                        var exit = t.At(t.L, out var heading);
                        tr.position = exit + c.Off * t.Radius(t.L);
                        tr.localScale = c.Scale;
                        c.V = heading * (0.5f * Mathf.Max(0f, cfg.FireSpeed));
                        c.Stage = Stage.Glide;
                    }
                    else
                    {
                        var at = t.At(c.S, out var tangent);
                        MeerkatWormholeMath.CarryDistortion(c.S, t.L, cfg.CarryStretch, cfg.CarryTwist, c.Seed, out float along,
                            out float across, out float turn);
                        tr.SetPositionAndRotation(at + c.Off * t.Radius(c.S),
                            Quaternion.LookRotation(tangent) * Quaternion.AngleAxis(turn * Mathf.Rad2Deg, Vector3.forward));
                        tr.localScale = new Vector3(c.Scale.x * across, c.Scale.y * across, c.Scale.z * along);
                        c.Placed = tr.position;
                        continue;
                    }
                }
                if (c.Stage == Stage.Glide)
                {
                    c.V *= Mathf.Exp(-dt / glide);
                    if (c.V.sqrMagnitude < RestSpeedSqr)
                    {
                        // at rest: the machine that decides the hull puts the cell's omni crystal here for everyone
                        if (c.Omni && decides && cr.CrystalManager) cr.CrystalManager.RelocateCrystal(cr.Id, tr.position);
                        _crystals.RemoveAt(i);
                        continue;
                    }
                }
                tr.position += c.V * dt;
                c.Placed = tr.position;
            }
        }

        bool Carrying(Crystal c)
        {
            for (int i = 0; i < _crystals.Count; i++)
                if (_crystals[i].Crystal == c) return true;
            return false;
        }

        CrystalImpactor ImpactorOf(Crystal c)
        {
            if (_impactorOf.TryGetValue(c, out var impactor) && impactor) return impactor;
            impactor = c.GetComponentInChildren<CrystalImpactor>(true);
            _impactorOf[c] = impactor;
            return impactor;
        }

        static float Sq(float x) => x * x;

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
    }
}
