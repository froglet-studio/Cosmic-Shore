using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Pistol's body on its ground: the Tiny Wings ride (heavy on the way down, light on the way up, thrown off a
    /// crest when it lets go, a smooth or a hard landing), the aimed jump, and its two triggers (Snap = the vortex ring,
    /// and Ghost), in the flat frame of the stretch it rides (<see cref="PistolPlane.Frame"/>: x along the stretch, y up,
    /// gravity minus y). Tiny Wings is the reference (Garrett, 2026-10-22); the ground is the dart's own trail on a flat
    /// plane, so there is nothing to unroll. No scene, no time, no device.
    /// </summary>
    public static class PistolBodyMath
    {
        /// <summary>The Pistol numbers this step reads. <c>pLaunchCd</c> is the jump's cooldown at the pair's Time level,
        /// evaluated by the caller (Time is the jump's cooldown, Garrett 2026-10-22).</summary>
        public struct Settings
        {
            public float pGravity, pDiveMul, pMinSpeed, pDrag, pGoodLand;
            /// <summary>Seconds in the air a flight needs before its landing counts (a smooth one earns the slide's speed,
            /// a hard one costs it); a shorter hop off a ripple is a skip that keeps its speed along the ground.</summary>
            public float pMinAir;
            /// <summary>Tiny Wings' float: gravity in the air while the Pistol lets go, as a share of <c>pGravity</c> (it
            /// launches higher and falls slower than it slides; the dive still pulls it down at <c>pDiveMul</c>).</summary>
            public float pAirGravity;
            /// <summary>A smooth landing's speed, times this; a hard one keeps this share of its speed along the slope
            /// (Tiny Wings forgives: a bad landing slows it, it never stops it).</summary>
            public float pSmoothBoost, pHardKeep;
            /// <summary>The fastest the Pistol goes, u/s.</summary>
            public float pMaxSpeed;
            public float pLaunchRange, pLaunchSpeed, pLaunchCharges, pLaunchCd;
            public float pGhostTime, pGhostCd, pVortexCd;
            /// <summary>A broken trail (<see cref="PistolGround"/>): a body this far under the ground's line over a hole has
            /// fallen in for good, and lands on nothing after (less: it still catches the far lip).</summary>
            public float pEdgeGrace;
            /// <summary>How far under the ground's line a fallen body drops before it is lost (<see cref="Events.Fell"/>).</summary>
            public float pFallDepth;
        }

        /// <summary>The Pistol's state in the frame it rides.</summary>
        public sealed class State
        {
            public float x, y, vx, vy, v, airT; public bool grounded = true;
            /// <summary>Fell into a hole in the trail: it lands on nothing until the caller brings it home.</summary>
            public bool falling;
            public int launch; public float launchT, lastLaunchAt = -9f;
            public float ghostAt, ghostUntil = -1f, vortexAt; public bool prevGhost, prevVortex;
            public Vector3 wpos;
        }

        /// <summary>One step's input. <c>Launch</c> and <c>SnapAim</c> are in the frame (dx along, dy up); a zero
        /// <c>SnapAim</c> fires straight ahead.</summary>
        public struct Input
        {
            public float Dive; public bool HasLaunch; public Vector2 Launch; public bool Ghost; public bool Snap; public Vector2 SnapAim;
        }

        /// <summary>What the step did that the caller must act on.</summary>
        public struct Events
        {
            public bool Landed, SmoothLanding, Launched, FireSnap, Fell; public Vector2 SnapAim; public bool GhostStarted; public float Travelled;
            /// <summary>The step flew over its ground (in the air at least <c>pMinAir</c>, not fallen in) from frame-x
            /// <see cref="FlewFrom"/> to <see cref="FlewTo"/>: the blocks under that stretch were skipped, not ridden
            /// (Garrett, 2026-10-29: "every time prisms are not ridden because the pistol is off trail then grow those prisms").</summary>
            public bool Flew; public float FlewFrom, FlewTo;
        }

        /// <summary>A Pistol standing at the start of <paramref name="f"/> with speed <paramref name="v"/>.</summary>
        public static void PlaceAtStart(State p, in PistolPlane.Frame f, float v)
        {
            p.x = 0f; p.y = PistolPlane.Ground(f, 0f); p.v = v; p.vx = p.vy = 0f; p.grounded = true; p.falling = false;
            p.wpos = f.ToWorld(p.x, p.y);
        }

        /// <summary>The ride's own step, s: every frame is ridden in steps this short, so the ride is the same at 30 fps
        /// on a phone as at 120 in the editor (the liftoff and the landing used to depend on the frame's length).</summary>
        public const float Substep = 1f / 120f;

        /// <summary>The ride, the jump, ghost and Snap for one step.</summary>
        public static Events Step(State p, in PistolPlane.Frame f, float t, float dt, in Input inp, in Settings s)
        {
            var ev = new Events();
            float x0 = p.x, y0 = p.y;
            float dive = Mathf.Clamp01(inp.Dive);
            float g = s.pGravity * (1f + (s.pDiveMul - 1f) * dive), gAir = AirGravity(s, dive);
            int n = Mathf.Max(1, Mathf.CeilToInt(dt / Substep));
            float h = dt / n;
            for (int i = 0; i < n && !ev.Fell; i++) Ride(p, f, h, g, gAir, s, ref ev);
            // the jump: one charge on Time's cooldown
            if (p.launch < s.pLaunchCharges) { p.launchT += dt; if (p.launchT >= s.pLaunchCd) { p.launch++; p.launchT = 0f; } } else p.launchT = 0f;
            if (inp.HasLaunch && p.launch > 0)
            {
                Vector2 lv = LaunchVelocity(inp.Launch, s);
                p.vx = lv.x; p.vy = lv.y; p.grounded = false; p.airT = 0f; p.launch--; p.lastLaunchAt = t; ev.Launched = true;
            }
            p.wpos = f.ToWorld(p.x, p.y);
            if (inp.Ghost && !p.prevGhost && t >= p.ghostAt) { p.ghostUntil = t + s.pGhostTime; p.ghostAt = t + s.pGhostCd; ev.GhostStarted = true; }
            p.prevGhost = inp.Ghost;
            if (inp.Snap && !p.prevVortex && t >= p.vortexAt) { ev.FireSnap = true; ev.SnapAim = inp.SnapAim; p.vortexAt = t + s.pVortexCd; }
            p.prevVortex = inp.Snap;
            ev.Travelled = Mathf.Sqrt((p.x - x0) * (p.x - x0) + (p.y - y0) * (p.y - y0));
            return ev;
        }

        /// <summary>Gravity in the air at <paramref name="dive"/> (0 let go: <c>pAirGravity</c> of <c>pGravity</c>, the float;
        /// 1 diving: <c>pDiveMul</c> of it).</summary>
        public static float AirGravity(in Settings s, float dive) =>
            s.pGravity * Mathf.Lerp(s.pAirGravity > 0f ? s.pAirGravity : 1f, s.pDiveMul, Mathf.Clamp01(dive));

        /// <summary>
        /// One substep of Tiny Wings' ride. On the ground the Pistol slides under gravity <paramref name="g"/> (heavier while
        /// it dives) and the water's drag; a CREST throws it when its speed needs more pull round the curve than the air's
        /// gravity <paramref name="gAir"/> gives (v^2 times the curvature over g cos), so holding the dive keeps it down and
        /// letting go over a crest flies it. In the air it floats (the air's gravity is lighter than the slide's while it
        /// lets go); a landing keeps its speed along the slope and loses the part into it (the game's frictionless ground):
        /// within <c>pGoodLand</c> of the slope it is a perfect slide, steeper it keeps <c>pHardKeep</c> of what is left, and
        /// a hop shorter than <c>pMinAir</c> is neither. Never faster than <c>pMaxSpeed</c>.
        /// <para>A HOLE where a trail block was destroyed (<see cref="PistolPlane.SolidAt"/>) holds nothing up: the Pistol
        /// rolls off its edge with the speed it had, flies over it if it is high and fast enough, and lands on the far side;
        /// once it sinks <c>pEdgeGrace</c> under the ground's line over a hole it has fallen in, lands on nothing after (no
        /// phantom ground), and is lost <c>pFallDepth</c> under it (<see cref="Events.Fell"/>).</para>
        /// </summary>
        static void Ride(State p, in PistolPlane.Frame f, float h, float g, float gAir, in Settings s, ref Events ev)
        {
            float vMax = s.pMaxSpeed > 0f ? s.pMaxSpeed : float.MaxValue;
            if (p.grounded)
            {
                float k = PistolPlane.Curvature(f, p.x, out float m), c = 1f / Mathf.Sqrt(1f + m * m), sn = m * c;
                p.v = Mathf.Clamp(p.v + (-g * sn - s.pDrag * p.v) * h, s.pMinSpeed, vMax);
                if (-k * p.v * p.v > gAir * c) { p.grounded = false; p.vx = p.v * c; p.vy = p.v * sn; p.airT = 0f; }
                else
                {
                    p.x += p.v * c * h;
                    if (PistolPlane.SolidAt(f, p.x)) { p.y = PistolPlane.Ground(f, p.x); return; }
                    // the block under it is gone: off the edge, with the speed it had along the slope
                    p.grounded = false; p.vx = p.v * c; p.vy = p.v * sn; p.airT = 0f;
                    return;
                }
            }
            p.vy -= gAir * h;
            float xFrom = p.x;
            float drag = 1f - s.pDrag * h; p.vx *= drag; p.vy *= drag;
            float sp2 = p.vx * p.vx + p.vy * p.vy;
            if (sp2 > vMax * vMax) { float k2 = vMax / Mathf.Sqrt(sp2); p.vx *= k2; p.vy *= k2; }
            p.x += p.vx * h; p.y += p.vy * h; p.airT += h;
            if (p.x < 0f) { p.x = 0f; if (p.vx < 0f) p.vx = 0f; }   // the perch it left is behind it: never off the start
            float yg = PistolPlane.Ground(f, p.x, out float slope);
            if (p.y > yg)
            {
                // over the ground's line: what lies under this stretch is flown over, not ridden (never while it sinks into a
                // hole: those blocks are the hole, not a jump's reward)
                if (!p.falling && p.airT >= s.pMinAir)
                {
                    if (!ev.Flew) { ev.Flew = true; ev.FlewFrom = xFrom; }
                    ev.FlewTo = p.x;
                }
                return;
            }
            if (p.falling || !PistolPlane.SolidAt(f, p.x))
            {
                // over a hole, or under the line after falling into one: nothing to land on
                if (yg - p.y > s.pEdgeGrace) { p.falling = true; ev.Flew = false; }
                if (p.falling && yg - p.y >= Mathf.Max(s.pEdgeGrace, s.pFallDepth)) ev.Fell = true;
                return;
            }
            float cl = 1f / Mathf.Sqrt(1f + slope * slope), sl = slope * cl, sp = Mathf.Sqrt(p.vx * p.vx + p.vy * p.vy), vt = p.vx * cl + p.vy * sl;
            p.grounded = true; p.y = yg;
            if (p.airT < s.pMinAir) { p.v = Mathf.Clamp(vt, s.pMinSpeed, vMax); return; }   // a skip
            float ang = sp > 1e-6f ? Mathf.Acos(Mathf.Clamp(vt / sp, -1f, 1f)) * Mathf.Rad2Deg : 0f;
            ev.Landed = true;
            // Tiny Wings' landing (frictionless, no bounce): the speed INTO the slope is lost, the speed along it kept; within
            // pGoodLand of the slope it is a perfect slide (a touch of pSmoothBoost), steeper it keeps pHardKeep of it
            if (ang <= s.pGoodLand) { p.v = Mathf.Clamp(vt * (s.pSmoothBoost > 0f ? s.pSmoothBoost : 1f), s.pMinSpeed, vMax); ev.SmoothLanding = true; }
            else p.v = Mathf.Clamp(vt * (s.pHardKeep > 0f ? s.pHardKeep : 1f), s.pMinSpeed, vMax);
        }

        /// <summary>The jump that lands the Pistol <paramref name="aim"/> away (capped at <c>pLaunchRange</c>).</summary>
        public static Vector2 LaunchVelocity(Vector2 aim, in Settings s)
        {
            float dx = aim.x, dy = aim.y, L = Mathf.Sqrt(dx * dx + dy * dy);
            if (L > s.pLaunchRange) { dx *= s.pLaunchRange / L; dy *= s.pLaunchRange / L; }
            float T = Mathf.Max(0.35f, Mathf.Sqrt(dx * dx + dy * dy) / Mathf.Max(1f, s.pLaunchSpeed));
            return new Vector2(dx / T, dy / T + 0.5f * s.pGravity * T);
        }

        /// <summary>Does a point at frame (x, y, d) touch the Pistol's body (a short cylinder across its plane)?</summary>
        public static bool Touches(State p, Vector3 frame, float halfDepth, float bodyR)
        {
            float dx = frame.x - p.x, dy = frame.y - p.y;
            return Mathf.Abs(frame.z) <= halfDepth && dx * dx + dy * dy <= (bodyR + 3f) * (bodyR + 3f);
        }
    }

    /// <summary>
    /// The Snap: a vortex ring. Prisms near its core are pulled into a toroidal flow (round the core, drifting round
    /// the ring), carried with it, and broken when it collapses (or let go). A ring fired while the Goby holds a lock on
    /// a rival vessel that is on the Pistol's screen HOMES on it (Garrett, 2026-10-22): it turns toward the target each
    /// step (<see cref="Steer"/>). The caller owns the prisms (it asks <see cref="TryCapture"/> and moves each captured one
    /// to <see cref="Flow"/>'s answer).
    /// </summary>
    public sealed class VortexRing
    {
        public struct Settings { public float pVortexR, pVortexSpeed, pVortexLife, pVortexCapture, pVortexSpin, pVortexMax, pVortexCollapse; }

        public struct Capture { public int Id; public float th, ph, rho; }

        public Vector3 C; public Vector3 N, E1, E2; public float Life, Age;
        public readonly System.Collections.Generic.List<Capture> Caps = new();

        public VortexRing(Vector3 c, Vector3 n, float life)
        {
            C = c; Life = life;
            SetAxis(n);
        }

        void SetAxis(Vector3 n)
        {
            N = n.sqrMagnitude > 1e-8f ? n.normalized : Vector3.forward;
            E1 = Vector3.Cross(N, Mathf.Abs(N.y) < 0.9f ? Vector3.up : Vector3.right).normalized; E2 = Vector3.Cross(N, E1);
        }

        public bool Full(in Settings s) => Caps.Count >= s.pVortexMax;

        /// <summary>The ring travels; true while it lives.</summary>
        public bool Advance(float dt, in Settings s) { C += N * (s.pVortexSpeed * dt); Life -= dt; Age += dt; return Life > 0f; }

        /// <summary>Turn the ring's heading toward <paramref name="target"/> by at most <paramref name="maxRadians"/>.
        /// Captured prisms keep their place in the flow (their angles are about the axis).</summary>
        public void Steer(Vector3 target, float maxRadians)
        {
            Vector3 want = target - C;
            if (want.sqrMagnitude < 1e-6f) return;
            SetAxis(Vector3.RotateTowards(N, want.normalized, Mathf.Max(0f, maxRadians), 0f));
        }

        /// <summary>A prism within <c>pVortexCapture</c> of the ring's core joins its flow.</summary>
        public bool TryCapture(int id, Vector3 pos, in Settings s)
        {
            if (Full(s)) return false;
            float R = s.pVortexR; Vector3 rel = pos - C; if (rel.sqrMagnitude > (R + s.pVortexCapture) * (R + s.pVortexCapture)) return false;
            float a = Vector3.Dot(rel, N); Vector3 q = rel - N * a; float rr = q.magnitude, core = Mathf.Sqrt((rr - R) * (rr - R) + a * a);
            if (core > s.pVortexCapture) return false;
            Caps.Add(new Capture { Id = id, th = Mathf.Atan2(Vector3.Dot(q, E2), Vector3.Dot(q, E1)), ph = Mathf.Atan2(a, rr - R), rho = Mathf.Max(2f, core) });
            return true;
        }

        /// <summary>The toroidal flow for capture <paramref name="i"/>: advance it and return where it now is.</summary>
        public Vector3 Flow(int i, float dt, in Settings s)
        {
            var c = Caps[i];
            c.ph += s.pVortexSpin * dt; c.th += 0.8f * dt; c.rho += (4f - c.rho) * (1f - Mathf.Exp(-2.5f * dt));
            Caps[i] = c;
            float ct = Mathf.Cos(c.th), st = Mathf.Sin(c.th), rad = s.pVortexR + c.rho * Mathf.Cos(c.ph), ax = c.rho * Mathf.Sin(c.ph);
            return C + N * ax + (E1 * ct + E2 * st) * rad;
        }

        /// <summary>The tube's radius as a share of the ring's (the lab's <c>TorusGeometry(1, 0.16, ...)</c>).</summary>
        public const float Tube = 0.16f;

        /// <summary>Vertices <see cref="Facets"/> writes: three per triangle, two triangles per facet.</summary>
        public static int FacetVertexCount(int seg, int side) => seg * side * 6;

        /// <summary>
        /// The vortex ring as a FACETED volume (nothing in the world looks flat, LOCKED 2026-10-18,
        /// <c>Docs/claude/SHADERS_AND_PERFORMANCE.md</c>): a unit torus in the xy plane, its axis +z, <paramref name="seg"/>
        /// facets round the ring and <paramref name="side"/> round the tube; every triangle has its own vertices and its
        /// face normal, so it shades flat.
        /// </summary>
        public static void Facets(float tube, int seg, int side, Vector3[] v, Vector3[] n)
        {
            int k = 0;
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < side; j++)
                {
                    Vector3 p00 = RingPoint(tube, seg, side, i, j), p10 = RingPoint(tube, seg, side, i + 1, j);
                    Vector3 p11 = RingPoint(tube, seg, side, i + 1, j + 1), p01 = RingPoint(tube, seg, side, i, j + 1);
                    Face(v, n, ref k, p00, p10, p11); Face(v, n, ref k, p00, p11, p01);
                }
        }

        static Vector3 RingPoint(float tube, int seg, int side, int i, int j)
        {
            float a = i / (float)seg * Mathf.PI * 2f, b = j / (float)side * Mathf.PI * 2f, rr = 1f + tube * Mathf.Cos(b);
            return new Vector3(rr * Mathf.Cos(a), rr * Mathf.Sin(a), tube * Mathf.Sin(b));
        }

        static void Face(Vector3[] v, Vector3[] n, ref int k, Vector3 a, Vector3 b, Vector3 c)
        {
            var f = Vector3.Cross(b - a, c - a).normalized;
            v[k] = a; v[k + 1] = b; v[k + 2] = c; n[k] = n[k + 1] = n[k + 2] = f; k += 3;
        }
    }
}
