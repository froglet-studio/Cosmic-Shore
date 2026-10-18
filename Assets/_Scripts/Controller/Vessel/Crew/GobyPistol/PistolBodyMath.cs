using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Pistol's body on its ground: the Tiny Wings ride (heavy on the way down, light on the way up, a smooth or a
    /// hard landing), the aimed launch, and its two triggers (Snap = the vortex ring, and Ghost). Ported LINE FOR LINE
    /// from the lab's <c>pistolStep</c> (<c>Docs/Studios/VesselStudio/goby.html</c>), minus its body collisions, which
    /// need the scene (the harness does them with <c>PrismSpatialIndex</c>). No scene, no time, no device.
    /// </summary>
    public static class PistolBodyMath
    {
        /// <summary>The lab's Pistol numbers this step reads (the lab's keys, verbatim).</summary>
        public struct Settings
        {
            public float pGravity, pDiveMul, pMinSpeed, pDrag, pGoodLand;
            public float pLaunchRange, pLaunchSpeed, pLaunchCharges, pLaunchCd;
            public float pGhostTime, pGhostCd, pVortexCd;
        }

        /// <summary>The Pistol's state on the ribbon (the lab's <c>W.p</c>).</summary>
        public sealed class State
        {
            public float x, y, vx, vy, v; public bool grounded = true;
            public int launch; public float launchT, lastLaunchAt = -9f;
            public float ghostAt, ghostUntil = -1f, vortexAt; public bool prevGhost, prevVortex, freeSnap;
            public Vector3 wpos;
        }

        /// <summary>One step's input (the lab's Pistol input object). <c>Launch</c> and <c>Snap</c> carry an aim in the
        /// Pistol's plane (dx along the path, dy up); <c>Snap</c> with a zero aim fires straight ahead.</summary>
        public struct Input
        {
            public float Dive; public bool HasLaunch; public Vector2 Launch; public bool Ghost; public bool Snap; public Vector2 SnapAim;
        }

        /// <summary>What the step did that the caller must act on.</summary>
        public struct Events { public bool Landed, SmoothLanding, Launched, FireSnap; public Vector2 SnapAim; public bool GhostStarted; }

        public static State Spawn(PistolRibbon rb, float x, float v, in Settings s)
        {
            var p = new State { x = x, y = rb.Ground(x), v = v, launch = Mathf.RoundToInt(s.pLaunchCharges) };
            p.wpos = rb.Unmap(p.x, p.y, 0f); return p;
        }

        /// <summary>The lab's <c>pistolStep</c> (motion, the clamp at the Goby, launch, ghost, snap).</summary>
        public static Events Step(State p, PistolRibbon rb, float gobyX, float gobySpeed, float t, float dt, in Input inp, in Settings s)
        {
            var ev = new Events();
            float dive = Mathf.Clamp01(inp.Dive), g = s.pGravity * (1f + (s.pDiveMul - 1f) * dive);
            if (p.grounded)
            {
                rb.Ground(p.x, out float m); float c = 1f / Mathf.Sqrt(1f + m * m), sn = m * c;
                p.v += (-g * sn - s.pDrag * p.v) * dt; if (p.v < s.pMinSpeed) p.v = s.pMinSpeed;
                float nx = p.x + p.v * c * dt, yb = p.y + p.v * sn * dt - 0.5f * g * dt * dt, yg = rb.Ground(nx);
                if (dive < 0.5f && yb > yg + 0.05f) { p.grounded = false; p.vx = p.v * c; p.vy = p.v * sn; p.x = nx; p.y = yb; }
                else { p.x = nx; p.y = yg; }
            }
            else
            {
                p.vy -= g * dt; p.x += p.vx * dt; p.y += p.vy * dt;
                float yg = rb.Ground(p.x, out float m);
                if (p.y <= yg)
                {
                    float c = 1f / Mathf.Sqrt(1f + m * m), sn = m * c, sp = Mathf.Sqrt(p.vx * p.vx + p.vy * p.vy), vt = p.vx * c + p.vy * sn;
                    float ang = sp > 1e-6f ? Mathf.Acos(Mathf.Clamp(vt / sp, -1f, 1f)) * Mathf.Rad2Deg : 0f;
                    ev.Landed = true;
                    if (ang <= s.pGoodLand) { p.v = Mathf.Max(vt, sp * 0.98f) * 1.04f; ev.SmoothLanding = true; } else p.v = Mathf.Max(s.pMinSpeed, vt * 0.55f);
                    p.grounded = true; p.y = yg;
                }
            }
            // the Pistol cannot pass the Goby: there is no ground ahead of it yet
            float lim = gobyX - 6f;
            if (p.x > lim) { p.x = lim; if (p.grounded) { p.v = Mathf.Min(p.v, gobySpeed); p.y = rb.Ground(p.x); } else p.vx = Mathf.Min(p.vx, gobySpeed); }
            // abilities
            if (p.launch < s.pLaunchCharges) { p.launchT += dt; if (p.launchT >= s.pLaunchCd) { p.launch++; p.launchT = 0f; } } else p.launchT = 0f;
            if (inp.HasLaunch && p.launch > 0)
            {
                float dx = inp.Launch.x, dy = inp.Launch.y, L = Mathf.Sqrt(dx * dx + dy * dy);
                if (L > s.pLaunchRange) { dx *= s.pLaunchRange / L; dy *= s.pLaunchRange / L; }
                float T = Mathf.Max(0.35f, Mathf.Sqrt(dx * dx + dy * dy) / s.pLaunchSpeed);
                p.vx = dx / T; p.vy = dy / T + 0.5f * s.pGravity * T; p.grounded = false; p.launch--; p.lastLaunchAt = t; ev.Launched = true;
            }
            p.wpos = rb.Unmap(p.x, p.y, 0f);
            if (inp.Ghost && !p.prevGhost && t >= p.ghostAt) { p.ghostUntil = t + s.pGhostTime; p.ghostAt = t + s.pGhostCd; ev.GhostStarted = true; }
            p.prevGhost = inp.Ghost;
            if (inp.Snap && !p.prevVortex && t >= p.vortexAt) { ev.FireSnap = true; ev.SnapAim = inp.SnapAim; p.vortexAt = t + s.pVortexCd; }
            p.prevVortex = inp.Snap;
            return ev;
        }

        /// <summary>The lab's body test: does a point mapped to (x, h, d) touch the Pistol's cylinder?</summary>
        public static bool Touches(State p, in PistolRibbon.MapResult m, float pVision, float pBodyR)
        {
            float half = pVision + 4f;
            return m.Ok && Mathf.Abs(m.D) <= half && Mathf.Sqrt((m.X - p.x) * (m.X - p.x) + (m.H - p.y) * (m.H - p.y)) <= pBodyR + 3f;
        }
    }

    /// <summary>
    /// The Snap: a vortex ring. Prisms near its core are pulled into a toroidal flow (round the core, drifting round
    /// the ring), carried with it, and broken when it collapses (or let go). Ported LINE FOR LINE from the lab's
    /// <c>fireVortex</c> and <c>vortexStep</c>; the caller owns the prisms (it asks <see cref="TryCapture"/> and moves
    /// each captured one to <see cref="Flow"/>'s answer).
    /// </summary>
    public sealed class VortexRing
    {
        public struct Settings { public float pVortexR, pVortexSpeed, pVortexLife, pVortexCapture, pVortexSpin, pVortexMax, pVortexCollapse; }

        public struct Capture { public int Id; public float th, ph, rho; }

        public Vector3 C; public readonly Vector3 N, E1, E2; public float Life, Age;
        public readonly System.Collections.Generic.List<Capture> Caps = new();

        public VortexRing(Vector3 c, Vector3 n, float life)
        {
            C = c; N = n.normalized; Life = life;
            E1 = Vector3.Cross(N, Mathf.Abs(N.y) < 0.9f ? Vector3.up : Vector3.right).normalized; E2 = Vector3.Cross(N, E1);
        }

        /// <summary>The lab's <c>fireVortex</c>: the ring leaves the Pistol along its aim in the plane (straight ahead
        /// along the path when the aim is zero).</summary>
        public static VortexRing Fire(PistolRibbon rb, PistolBodyMath.State p, Vector2 aim, float life)
        {
            float dx = 1f, dy = 0f, l = aim.magnitude; if (l > 1f) { dx = aim.x / l; dy = aim.y / l; }
            rb.FrameAt(p.x, out Vector3 T, out Vector3 U, out _);
            return new VortexRing(p.wpos, (T * dx + U * dy).normalized, life);
        }

        public bool Full(in Settings s) => Caps.Count >= s.pVortexMax;

        /// <summary>The ring travels; true while it lives.</summary>
        public bool Advance(float dt, in Settings s) { C += N * (s.pVortexSpeed * dt); Life -= dt; Age += dt; return Life > 0f; }

        /// <summary>The lab's capture test: a prism within <c>pVortexCapture</c> of the ring's core joins its flow.</summary>
        public bool TryCapture(int id, Vector3 pos, in Settings s)
        {
            if (Full(s)) return false;
            float R = s.pVortexR; Vector3 rel = pos - C; if (rel.sqrMagnitude > (R + s.pVortexCapture) * (R + s.pVortexCapture)) return false;
            float a = Vector3.Dot(rel, N); Vector3 q = rel - N * a; float rr = q.magnitude, core = Mathf.Sqrt((rr - R) * (rr - R) + a * a);
            if (core > s.pVortexCapture) return false;
            Caps.Add(new Capture { Id = id, th = Mathf.Atan2(Vector3.Dot(q, E2), Vector3.Dot(q, E1)), ph = Mathf.Atan2(a, rr - R), rho = Mathf.Max(2f, core) });
            return true;
        }

        /// <summary>The lab's toroidal flow for capture <paramref name="i"/>: advance it and return where it now is.</summary>
        public Vector3 Flow(int i, float dt, in Settings s)
        {
            var c = Caps[i];
            c.ph += s.pVortexSpin * dt; c.th += 0.8f * dt; c.rho += (4f - c.rho) * (1f - Mathf.Exp(-2.5f * dt));
            Caps[i] = c;
            float ct = Mathf.Cos(c.th), st = Mathf.Sin(c.th), rad = s.pVortexR + c.rho * Mathf.Cos(c.ph), ax = c.rho * Mathf.Sin(c.ph);
            return C + N * ax + (E1 * ct + E2 * st) * rad;
        }
    }
}
