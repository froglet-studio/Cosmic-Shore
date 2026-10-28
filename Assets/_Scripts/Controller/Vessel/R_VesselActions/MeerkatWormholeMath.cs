using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The pure arithmetic of the Meerkat's WORMHOLE (<c>R_VesselActions/MEERKAT.md</c>, <c>Docs/Studios/MEERKAT.md</c>):
    /// the Vessel Studio's <c>shapeFor</c> / <c>whAt</c> / <c>whFrame</c> / <c>whRadius</c> / <c>stepTunnel</c> /
    /// <c>gateTransit</c> / <c>aiGatePlan</c> and the lens's ripple (<c>Docs/Studios/VesselStudio/meerkat.html</c>), line
    /// for line. No scene, no time, no input device: <c>MeerkatWormholeMathTests</c> holds every function to numbers taken
    /// from running the page's own code.
    ///
    /// <para><b>The tunnel</b> (round 22, Yash: "it should always bend from the center rather than bending from one of the ends
    /// to a small knot"): ONE conic arc (<see cref="Conic"/>) from its way in <c>A</c>, heading <c>t0</c> there, turning
    /// toward <c>side</c> through <c>theta</c> radians in all, symmetric about its middle - the conic's vertex, where it turns
    /// sharpest - and as oval as its length asks (<see cref="OvalOf"/>). Its length is still round 7's
    /// <c>L = S + R theta</c> (LT's shaft plus the bend radius × the turn). <c>s</c> runs 0 at the way in to <c>L</c> at the
    /// way out. Its wall is a hyperboloid of FIXED size: throat radius
    /// <c>b</c> at the middle, mouth radius <c>M</c> at both ends, over the window <c>[s0, s1]</c> that has formed (it ripples
    /// out of its centre, and collapses back to it).</para>
    ///
    /// <para><b>Axes.</b> The page is three.js (x right, y up, z BACK); this class is Unity's (x right, y up, z forward),
    /// so the page's <c>-loc.z</c> is <c>local.z</c> here. Everything else is the same.</para>
    /// </summary>
    public static class MeerkatWormholeMath
    {
        /// <summary>The shape rows (<see cref="ScriptableObjects.MeerkatWormholeConfigSO"/>'s shape group).</summary>
        public struct ShapeSettings
        {
            public float ShaftMin;         // u, LT released
            public float ShaftMax;         // u, LT squeezed
            public float BendRadius;       // u
            public float BendMaxDegrees;   // deg
            public float ThroatRadius;     // u
            public float MouthRadius;      // u
            public float OvalShortest;     // eccentricity at the shortest tunnel (round 22)
            public float OvalLongest;      // ...at the longest
            public float OvalMaxSharpness; // the middle turns at most this many times as sharply as the ends
            public float Area;             // u^2: the soliton's surface area (round 23, Garrett); 0 keeps the fixed size
        }

        /// <summary>What the sticks shape (the page's <c>shapeFor</c> return): L mouth to mouth, S the shaft, R the bend's radius.</summary>
        public struct Shape
        {
            public float L, S, R, B, M, Theta, Fat, Stick, Dx, Dy, Oval;
        }

        public enum Phase { Form = 0, Open = 1, Stay = 2, Collapse = 3, Gone = 4 }

        /// <summary>A tunnel's live window and life (the page's <c>wh</c> minus its geometry).</summary>
        public struct Window
        {
            public Phase Phase;
            public float Grow;    // 0..1 while forming
            public float S0, S1;  // the formed window along [0, L]
            public float Stay;    // seconds stayed since its rider came out
            public bool Out;      // its rider is out
        }

        /// <summary>The page's <c>FADE_S</c>: a collapsed tunnel fades out over this long.</summary>
        public const float FadeSeconds = 0.7f;
        /// <summary>The page's <c>DENT_NEAR</c>: a mouth's dent fades within this many mouth radii of the camera.</summary>
        public const float DentNear = 3f;

        public static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;

        /// <summary>The page's <c>easeOutC</c>.</summary>
        public static float EaseOutCubic(float x)
        {
            float t = 1f - Clamp01(x);
            return 1f - t * t * t;
        }

        /// <summary>The page's <c>smooth</c> (smoothstep from <paramref name="a"/> to <paramref name="b"/>).</summary>
        public static float Smooth(float a, float b, float x)
        {
            float t = Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>The game's stick ease (<c>BaseInputStrategy.Ease</c>, the page's <c>ease</c>).</summary>
        public static float Ease(float x) =>
            x < 0f ? Mathf.Cos(x * Mathf.PI / 4f) - 1f : -(Mathf.Cos(x * Mathf.PI / 4f) - 1f);

        /// <summary>The page's <c>easeInv</c>: the stick that <see cref="Ease"/>s to <paramref name="y"/> (× 2).</summary>
        public static float EaseInverse(float y)
        {
            float a = Mathf.Min(0.9999f, Mathf.Abs(y));
            return Mathf.Sign(y) * 4f / Mathf.PI * Mathf.Acos(1f - a);
        }

        /// <summary>
        /// The page's <c>shapeFor</c> (round 7): a straight shaft, <see cref="ShapeSettings.ShaftMin"/> with LT
        /// (<paramref name="lq"/>) released to <see cref="ShapeSettings.ShaftMax"/> squeezed, then a bend round
        /// <see cref="ShapeSettings.BendRadius"/> as far as the right stick (eased, <paramref name="sx"/>, <paramref name="sy"/>)
        /// is pushed, up to <see cref="ShapeSettings.BendMaxDegrees"/>, the way it is pushed. Its size is the surface area's
        /// (<see cref="SurfaceAreaThroat"/>, round 23) when <see cref="ShapeSettings.Area"/> is set, else fixed.
        /// </summary>
        public static Shape ShapeFor(in ShapeSettings p, float lq, float sx, float sy)
        {
            float tx = Mathf.Max(-1f, Mathf.Min(1f, sx)), ty = Mathf.Max(-1f, Mathf.Min(1f, sy));
            float mRaw = Mathf.Sqrt(tx * tx + ty * ty), m = Mathf.Min(1f, mRaw), t = Clamp01((m - 0.05f) / 0.95f);
            float S = Mathf.Max(0f, p.ShaftMin + (Mathf.Max(p.ShaftMin, p.ShaftMax) - p.ShaftMin) * Clamp01(lq));
            float theta = Mathf.Max(0f, p.BendMaxDegrees) * Mathf.PI / 180f * t, R = Mathf.Max(1f, p.BendRadius);
            float b = Mathf.Max(0.5f, p.ThroatRadius), L = Mathf.Max(10f, S + R * theta), M = Mathf.Max(b, p.MouthRadius);
            if (p.Area > 0f) SurfaceAreaThroat(p.ThroatRadius, p.MouthRadius, p.Area, L, out b, out M);   // round 23: Garrett's area
            return new Shape
            {
                L = L, S = S, R = R, B = b, M = M, Theta = theta, Fat = t,
                Stick = m, Dx = mRaw > 1e-6f ? tx / mRaw : 0f, Dy = mRaw > 1e-6f ? ty / mRaw : 0f, Oval = OvalOf(p, L, theta),
            };
        }

        /// <summary>
        /// The page's <c>kitThroat</c> (round 23, Garrett's soliton: "at a given Space level a soliton keeps one surface area A,
        /// so a longer tube is thinner and a shorter one fatter"). The wall is the hyperboloid with the mouth a fixed k × the
        /// throat (k the authored mouth / throat), whose area is A = 2 pi L b g(k), g(k) = (k + asinh(sqrt(k^2 - 1)) /
        /// sqrt(k^2 - 1)) / 2, so the throat's radius is b = A / (2 pi L g(k)), with a 0.25 u safety floor, and the mouth's k b.
        /// </summary>
        public static void SurfaceAreaThroat(float throatRadius, float mouthRadius, float area, float L, out float b, out float m)
        {
            double k = Math.Max(1.05, mouthRadius / Math.Max(0.25, throatRadius)), q = Math.Sqrt(k * k - 1);
            double g = 0.5 * (k + Math.Log(q + Math.Sqrt(q * q + 1)) / q), A = Math.Max(1.0, area);
            double bb = Math.Max(0.25, A / (2 * Math.PI * Math.Max(10.0, L) * g));
            b = (float)bb;
            m = (float)(k * bb);
        }

        /// <summary>The page's <c>lerpLv</c>: an element's L0 value to its L10 value by its level (0-10).</summary>
        public static float ByLevel(float atL0, float atL10, float level) => atL0 + (atL10 - atL0) * Clamp01(level / 10f);

        /// <summary>
        /// The page's <c>pullVel</c> (round 23, Yash: "they need to get in. Sucked in. From a certain radius"): the velocity
        /// a standing soliton draws a prism or crystal at <paramref name="p"/> to its way in <paramref name="a"/> with,
        /// <paramref name="suckSpeed"/> at <paramref name="suckRadius"/> and faster as it nears (suckSpeed × suckRadius /
        /// distance, at most <paramref name="carrySpeed"/>), swirling round the tunnel's axis <paramref name="t0"/> at a third
        /// of that, fading to none at the mouth. The page's swirl is t0 × d in three.js's axes, which are this one's mirrored
        /// in z, so here it is d × t0: the same way round on screen.
        /// </summary>
        public static Vector3 PullVelocity(Vector3 a, Vector3 t0, Vector3 p, float suckSpeed, float suckRadius, float carrySpeed)
        {
            var d = a - p;
            float dist = Mathf.Max(1e-3f, d.magnitude);
            float sp = Mathf.Min(Mathf.Max(1f, carrySpeed), Mathf.Max(0f, suckSpeed) * Mathf.Max(1f, suckRadius) / Mathf.Max(dist, 1f));
            d /= dist;
            var swirl = Vector3.Cross(d, t0);
            return d * sp + swirl * (sp * 0.33f * Clamp01(dist / Mathf.Max(1f, suckRadius)));
        }

        /// <summary>
        /// The page's <c>carryPose</c> (round 23, Yash: "the prisms will be distorted while traveling through this wormhole"):
        /// at <paramref name="s"/> along a tunnel <paramref name="L"/> long a carried thing is stretched along the tube's
        /// heading to <paramref name="along"/> = 1 + <paramref name="stretch"/> sin(pi s / L) (none at the mouths), squeezed
        /// across to <paramref name="across"/> = 1 / sqrt(along) so its volume is kept, and turned <paramref name="turn"/>
        /// radians round the heading: <paramref name="twist"/> turns mouth to mouth, plus 0.137 of a turn per
        /// <paramref name="seed"/> so a stream is not in lockstep.
        /// </summary>
        public static void CarryDistortion(float s, float L, float stretch, float twist, int seed, out float along,
            out float across, out float turn)
        {
            float u = Clamp01(s / Mathf.Max(1e-3f, L));
            along = 1f + Mathf.Max(0f, stretch) * Mathf.Sin(Mathf.PI * u);
            across = 1f / Mathf.Sqrt(along);
            turn = 2f * Mathf.PI * (Mathf.Max(0f, twist) * u + 0.137f * seed);
        }

        /// <summary>
        /// The page's <c>ovalOf</c> (round 22): how oval a tunnel <paramref name="L"/> long turning <paramref name="theta"/>
        /// bends - its eccentricity, from <see cref="ShapeSettings.OvalShortest"/> at the shortest tunnel to
        /// <see cref="ShapeSettings.OvalLongest"/> at the longest (0 a circle's arc, 1 a parabola, over 1 a hyperbola). The
        /// turn's sharpness at the ends against the middle is ((1 + k m^2) / (1 + m^2))^1.5 (k = 1 - e^2, m the ends' slope),
        /// held to at most <see cref="ShapeSettings.OvalMaxSharpness"/> to one, and a hyperbola opens no wider than the turn
        /// allows.
        /// </summary>
        public static float OvalOf(in ShapeSettings p, float L, float theta)
        {
            double Lmin = Math.Max(10.0, p.ShaftMin);
            double Lmax = Math.Max(Lmin + 1, Math.Max(p.ShaftMin, p.ShaftMax) + Math.Max(1.0, p.BendRadius) * Math.Max(0.0, p.BendMaxDegrees) * Math.PI / 180);
            double g = Math.Max(0, Math.Min(1, (L - Lmin) / (Lmax - Lmin)));
            double e = Math.Max(0, p.OvalShortest + (p.OvalLongest - p.OvalShortest) * g);
            double m = Math.Tan(Math.Min(1.55, theta / 2.0)), m2 = m * m;
            if (m2 < 1e-12) return (float)e;
            double kMin = Math.Max((Math.Pow(Math.Max(1.0, p.OvalMaxSharpness), -2.0 / 3.0) * (1 + m2) - 1) / m2, -0.9 / m2);
            return (float)Math.Min(e, Math.Sqrt(Math.Max(0, 1 - kMin)));
        }

        /// <summary>
        /// A tunnel's centreline (round 22, the page's <c>conicOf</c> / <c>conicAt</c>): one conic arc of length L turning
        /// theta, symmetric about its middle. In the conic's own frame (its vertex at the origin, x along the heading there, y
        /// toward the bend) x^2 = 2 rho y - (1 - e^2) y^2; the point heading psi (m = tan psi) is
        /// (m / q, m^2 / (q (1 + q))) × rho, q = sqrt(1 + (1 - e^2) m^2), and rho scales the arc to L. A table of headings and
        /// lengths (<see cref="N"/> + 1) turns s into the heading there; the point follows exactly. Rebuilt in place only when
        /// L, theta or the eccentricity change.
        /// </summary>
        public sealed class Conic
        {
            public const int N = 96;
            public float L { get; private set; }
            public float Theta { get; private set; }
            public float E { get; private set; } = -1f;
            double _k, _rho, _x0, _y0, _c, _sn;
            readonly double[] _psi = new double[N + 1], _cum = new double[N + 1];

            public Conic() { }
            public Conic(float L, float theta, float e) { Set(L, theta, e); }

            /// <summary>True when the tunnel does not bend (it runs straight along t0).</summary>
            public bool Straight => Theta < 1e-4f;

            public void Set(float L, float theta, float e)
            {
                if (L == this.L && theta == Theta && e == E) return;
                this.L = L; Theta = theta; E = e;
                if (Straight) return;
                _k = 1 - (double)e * e;
                double h = theta / 2.0, px = 0, py = 0;
                for (int i = 0; i <= N; i++)
                {
                    _psi[i] = -h + theta * (double)i / N;
                    XY(_k, _psi[i], out double x, out double y);
                    _cum[i] = i > 0 ? _cum[i - 1] + Math.Sqrt((x - px) * (x - px) + (y - py) * (y - py)) : 0;
                    px = x; py = y;
                }
                _rho = L / Math.Max(1e-9, _cum[N]);
                for (int i = 0; i <= N; i++) _cum[i] *= _rho;
                XY(_k, -h, out _x0, out _y0);
                _c = Math.Cos(h); _sn = Math.Sin(h);
            }

            static void XY(double k, double psi, out double x, out double y)
            {
                double m = Math.Tan(psi), q = Math.Sqrt(Math.Max(1e-9, 1 + k * m * m));
                x = m / q; y = m * m / (q * (1 + q));
            }

            /// <summary>s along the arc (clamped to it): how far ahead of the way in (along t0) and across (toward side), and how
            /// far it has turned (0 at the way in, theta at the way out).</summary>
            public void At(float s, out float ahead, out float across, out float turned)
            {
                if (Straight) { ahead = s; across = 0f; turned = 0f; return; }
                double sc = Math.Max(0, Math.Min(_cum[N], s));
                int lo = 0, hi = N;
                while (hi - lo > 1) { int mid = (lo + hi) >> 1; if (_cum[mid] <= sc) lo = mid; else hi = mid; }
                double psi = _psi[lo] + (_psi[hi] - _psi[lo]) * (sc - _cum[lo]) / Math.Max(1e-12, _cum[hi] - _cum[lo]);
                XY(_k, psi, out double x, out double y);
                double lx = _rho * (x - _x0), ly = _rho * (y - _y0);
                ahead = (float)(lx * _c - ly * _sn);
                across = (float)(lx * _sn + ly * _c);
                turned = (float)(psi + 0.5 * Theta);
            }
        }

        /// <summary>The page's <c>bendAngle</c>: how far the centreline has turned at <paramref name="s"/> (round 22: along the
        /// whole arc, fastest at its middle).</summary>
        public static float BendAngle(Conic c, float s)
        {
            if (c.Straight) return 0f;
            c.At(s, out _, out _, out float turned);
            return turned;
        }

        /// <summary>The page's <c>whAt</c>: the point <paramref name="s"/> along the tunnel, and its heading there (before the way
        /// in, straight on along t0).</summary>
        public static Vector3 At(Vector3 a, Vector3 t0, Vector3 side, Conic c, float s, out Vector3 tangent)
        {
            if (c.Straight || s <= 0f)
            {
                tangent = t0;
                return a + t0 * s;
            }
            c.At(s, out float ahead, out float across, out float ang);
            tangent = t0 * Mathf.Cos(ang) + side * Mathf.Sin(ang);
            return a + t0 * ahead + side * across;
        }

        /// <summary>The page's <c>whFrame</c>: the wall's two normals at <paramref name="s"/> (in the bend's plane, and across it).</summary>
        public static void Frame(Vector3 t0, Vector3 side, Conic c, float s, out Vector3 normal, out Vector3 binormal)
        {
            float ang = BendAngle(c, s);
            normal = side * Mathf.Cos(ang) - t0 * Mathf.Sin(ang);
            binormal = Vector3.Cross(t0, side).normalized;
        }

        /// <summary>
        /// The page's <c>whRadius</c>: the wall's radius at <paramref name="s"/>, a hyperboloid over the formed window, narrower
        /// while the window is short (it grows to its full throat and mouths as it forms).
        /// </summary>
        public static float Radius(float b0, float m0, float L, float s0, float s1, float s)
        {
            float span = Mathf.Max(1e-3f, s1 - s0), u = (s - s0) / span, k = Mathf.Min(1f, span / L);
            float b = b0 * k, M = Mathf.Max(b, m0 * k), w = 2f * u - 1f;
            return Mathf.Sqrt(b * b + (M * M - b * b) * w * w);
        }

        /// <summary>
        /// The contour field's frame (Garrett, 2026-10-27, the Tube Lens Lab): world to the tunnel's own frame, the way in
        /// <paramref name="a"/> at the origin, its first heading <paramref name="t0"/> along +z and the side it bends toward
        /// along +x, so <see cref="Frame"/>'s binormal is +y and the arc (round 22) lies in the x-z plane.
        /// <c>MeerkatWormholeLens.shader</c> measures the wall in this frame.
        /// </summary>
        public static Matrix4x4 TubeWorldToLocal(Vector3 a, Vector3 t0, Vector3 side)
        {
            var z = t0.normalized;
            var x = side - z * Vector3.Dot(side, z);
            if (x.sqrMagnitude < 1e-10f) x = Vector3.Cross(Mathf.Abs(z.y) < 0.9f ? Vector3.up : Vector3.right, z);
            x.Normalize();
            var y = Vector3.Cross(z, x);
            var m = Matrix4x4.identity;
            m.SetRow(0, new Vector4(x.x, x.y, x.z, -Vector3.Dot(x, a)));
            m.SetRow(1, new Vector4(y.x, y.y, y.z, -Vector3.Dot(y, a)));
            m.SetRow(2, new Vector4(z.x, z.y, z.z, -Vector3.Dot(z, a)));
            return m;
        }

        /// <summary>The page's reticle emerge (<c>gateHold</c>): 0 to 1 over <paramref name="growSeconds"/> after <paramref name="delaySeconds"/>.</summary>
        public static float ReticleEmerge(float age, float delaySeconds, float growSeconds) =>
            EaseOutCubic((age - Mathf.Max(0f, delaySeconds)) / Mathf.Max(0.05f, growSeconds));

        /// <summary>
        /// The page's <c>reticleEase</c> (round 3): the share of the way to what the sticks ask for that the reticle moves in
        /// <paramref name="dt"/>, 1 - exp(-dt / <paramref name="smoothSeconds"/>); all of it when the time is 0.
        /// </summary>
        public static float ReticleEase(float dt, float smoothSeconds)
        {
            float tau = Mathf.Max(0f, smoothSeconds);
            return tau < 1e-3f ? 1f : 1f - Mathf.Exp(-dt / tau);
        }

        /// <summary>
        /// The page's <c>reticleSettled</c>: the eased reticle (<paramref name="eased"/>, <paramref name="easedLq"/>) has caught up
        /// with the sticks (<paramref name="stick"/>, <paramref name="lqWant"/>) to within 0.03 - the AI pulls RT only then (or
        /// 1 s past its shaping time, <see cref="AiSettleTimeout"/>).
        /// </summary>
        public static bool ReticleSettled(Vector2 stick, Vector2 eased, float lqWant, float easedLq) =>
            Mathf.Abs(stick.x - eased.x) + Mathf.Abs(stick.y - eased.y) < 0.03f && Mathf.Abs(lqWant - easedLq) < 0.03f;

        /// <summary>The AI pulls RT at the latest this long past its shaping time, settled or not.</summary>
        public const float AiSettleTimeout = 1f;

        /// <summary>RT forms a tunnel only once its reticle has rippled at least this far into shape (<c>gateForm</c>).</summary>
        public const float FormableEmerge = 0.6f;

        /// <summary>The page's <c>gateCommit</c> forming time: <paramref name="formSeconds"/>, never longer than 0.9 × the dive.</summary>
        public static float FormTime(float formSeconds, float diveSeconds) =>
            Mathf.Min(Mathf.Max(0.05f, formSeconds), Mathf.Max(0.1f, 0.9f * diveSeconds));

        /// <summary>A new tunnel's window: nothing yet, at its centre (<c>gateCommit</c>).</summary>
        public static Window NewWindow(float L) => new Window { Phase = Phase.Form, S0 = 0.5f * L, S1 = 0.5f * L };

        /// <summary>
        /// The page's <c>stepTunnel</c>: form (ripple out of the centre over <paramref name="formT"/>), open while its rider is
        /// in, stay <paramref name="staySeconds"/> once out, collapse to its centre over <paramref name="collapseSeconds"/>.
        /// True on the step it finishes collapsing (the page then fades it and throws the screen-edge ripple at its centre).
        /// </summary>
        public static bool Step(ref Window w, float L, float formT, float staySeconds, float collapseSeconds, float dt)
        {
            switch (w.Phase)
            {
                case Phase.Form:
                {
                    w.Grow = Mathf.Min(1f, w.Grow + dt / Mathf.Max(0.05f, formT));
                    float h = 0.5f * L * EaseOutCubic(w.Grow);
                    w.S0 = 0.5f * L - h;
                    w.S1 = 0.5f * L + h;
                    if (w.Grow >= 1f)
                    {
                        w.S0 = 0f;
                        w.S1 = L;
                        w.Phase = w.Out ? Phase.Stay : Phase.Open;
                    }
                    return false;
                }
                case Phase.Stay:
                    w.Stay += dt;
                    if (w.Stay >= Mathf.Max(0f, staySeconds)) w.Phase = Phase.Collapse;
                    return false;
                case Phase.Collapse:
                {
                    float rate = L / Mathf.Max(0.05f, collapseSeconds);
                    w.S0 += 0.5f * rate * dt;
                    w.S1 -= 0.5f * rate * dt;
                    if (w.S1 - w.S0 > 2f) return false;
                    float mid = 0.5f * (w.S0 + w.S1);
                    w.S0 = w.S1 = mid;
                    w.Phase = Phase.Gone;
                    return true;
                }
                default:
                    return false;
            }
        }

        /// <summary>Forming a new tunnel collapses the last one at once (<c>gateCommit</c>), unless it is collapsing or gone.</summary>
        public static void CollapseNow(ref Window w)
        {
            if (w.Phase is Phase.Collapse or Phase.Gone) return;
            w.Phase = Phase.Collapse;
            w.Grow = 1f;
        }

        /// <summary>Its rider came out (<c>gateOut</c>): an open tunnel starts its stay.</summary>
        public static void RiderOut(ref Window w)
        {
            w.Out = true;
            if (w.Phase == Phase.Open) w.Phase = Phase.Stay;
        }

        /// <summary>
        /// The page's <c>rideMultiplier</c> (round 6, Yash: "enter the wormhole with v and then it will speed up to v plus dv,
        /// and then while exiting it will exit at little bit boosted"): at <paramref name="u"/> (0 the way in, 1 the way out) the
        /// multiplier on the speed before the tunnel rises from <paramref name="entry"/> to <paramref name="mid"/> at the throat
        /// and eases to <paramref name="exit"/> at the way out, smoothly (sin(pi u) on each half: its slope is 0 at the throat).
        /// </summary>
        public static float RideMultiplier(float entry, float mid, float exit, float u)
        {
            u = Clamp01(u);
            float w = Mathf.Sin(Mathf.PI * u), e = Mathf.Max(0.05f, entry), m = Mathf.Max(0.05f, mid), x = Mathf.Max(0.05f, exit);
            return u < 0.5f ? e + (m - e) * w : x + (m - x) * w;
        }

        /// <summary>
        /// The page's <c>gateTransit</c> speed: the speed before the tunnel <paramref name="vIn"/> × <see cref="RideMultiplier"/>;
        /// the dive to the mouth (<paramref name="s"/> &lt; 0) is at the entry multiplier.
        /// </summary>
        public static float RideSpeed(float vIn, float entry, float mid, float exit, float s, float L) =>
            vIn * RideMultiplier(entry, mid, exit, s < 0f ? 0f : s / Mathf.Max(1e-3f, L));

        /// <summary>The ride's facing: the entry facing <paramref name="q0"/> turned about the binormal by the angle the
        /// centreline has turned since the way in (<see cref="BendAngle"/> at s minus at the entry).</summary>
        public static Quaternion RideRotation(Vector3 binormal, float turnedRadians, Quaternion q0) =>
            Quaternion.AngleAxis(turnedRadians * Mathf.Rad2Deg, binormal) * q0;

        /// <summary>The page's <c>meanMul</c>: the mean of <see cref="RideMultiplier"/> over the ride (each half's sin averages 2/pi).</summary>
        public static float MeanRideMultiplier(float entry, float mid, float exit)
        {
            float ends = 0.5f * (RideMultiplier(entry, mid, exit, 0f) + RideMultiplier(entry, mid, exit, 1f));
            return ends + 2f / Mathf.PI * (Mathf.Max(0.05f, mid) - ends);
        }

        /// <summary>The AI rows (<see cref="ScriptableObjects.MeerkatWormholeConfigSO"/>'s ai group, with the shape and ride rows it reads).</summary>
        public struct PlanSettings
        {
            public float ReticleAhead, ShapeSeconds, MeanRideMultiplier, TurnRateRadians;
            public ShapeSettings Shape;
        }

        /// <summary>
        /// The page's <c>aiGatePlan</c> (round 7): try every bend (21) and LT squeeze - the shaft before the bend - (9), keep the
        /// one whose way out heads within 0.26 rad of the target with 60 u to spare and reaches it SOONEST: the ride at its mean
        /// speed, the flight from the way out to the target at <paramref name="speed"/>, and the turn its error leaves, with a
        /// hair's preference for straighter. <paramref name="local"/> is the target in the hull's frame (Unity axes).
        /// <paramref name="noise"/> × (<paramref name="rand01"/> − 0.5) bends it off the mark (a rookie). <paramref name="turnOnly"/>:
        /// only a tunnel that bends at least 0.35 rad (a near ring). False: none fits.
        /// </summary>
        static readonly Conic s_plan = new();

        public static bool TryPlan(in PlanSettings p, Vector3 local, float speed, float noise, float rand01, bool turnOnly,
            out float lq, out float m)
        {
            float ahead = Mathf.Max(0f, p.ReticleAhead) + speed * Mathf.Max(0f, p.ShapeSeconds);
            float tx = local.z, ty = Mathf.Sqrt(local.x * local.x + local.y * local.y), v = Mathf.Max(1f, speed);
            float turnRate = Mathf.Max(0.2f, p.TurnRateRadians), meanMul = Mathf.Max(0.05f, p.MeanRideMultiplier);
            bool found = false;
            float bestScore = 0f, bestT = 0f, bestSq = 0f;
            for (int j = 0; j <= 20; j++)
            for (int i = 0; i <= 8; i++)
            {
                float t = j / 20f, sq = i / 8f;
                var sh = ShapeFor(p.Shape, sq, 0.05f + 0.95f * t, 0f);
                float th = sh.Theta;
                if (turnOnly && th < 0.35f) continue;
                s_plan.Set(sh.L, th, sh.Oval);   // round 22: the arc's way out
                s_plan.At(sh.L, out float endAhead, out float endAcross, out _);
                float x = ahead + endAhead, y = endAcross;
                float hx = Mathf.Cos(th), hy = Mathf.Sin(th), dx = tx - x, dy = ty - y, along = dx * hx + dy * hy;
                if (along < 60f) continue;
                float err = Mathf.Atan2(Mathf.Abs(dx * hy - dy * hx), along);
                float score = sh.L / (v * meanMul) + Mathf.Sqrt(dx * dx + dy * dy) / v + err / turnRate + 0.05f * t;
                if (err < 0.26f && (!found || score < bestScore))
                {
                    found = true;
                    bestScore = score;
                    bestT = t;
                    bestSq = sq;
                }
            }
            lq = m = 0f;
            if (!found) return false;
            lq = bestSq;
            m = bestT > 0f ? Clamp01((0.05f + 0.95f * bestT) * (1f + noise * (rand01 - 0.5f))) : 0f;
            return true;
        }

        /// <summary>
        /// The right stick the AI holds for a plan (<c>aiInput</c>): toward the target's sideways offset, as far as
        /// <paramref name="m"/> - eased values, what <c>EasedRightJoystickPosition</c> carries.
        /// </summary>
        public static Vector2 PlanStick(Vector3 local, float m)
        {
            float lh = Mathf.Sqrt(local.x * local.x + local.y * local.y);
            return lh > 1e-6f ? new Vector2(local.x / lh * m, local.y / lh * m) : Vector2.zero;
        }

        /// <summary>
        /// The collapse's ripple at the screen's edges (<c>rippleUniform</c>): its strength in screen units at
        /// <paramref name="u"/> of <c>rippleSeconds</c>, for a camera <paramref name="distance"/> from it (full within 300 u).
        /// </summary>
        public static float RippleAmplitude(float strength, float u, float distance)
        {
            if (u < 0f || u >= 1f) return 0f;
            return Mathf.Max(0f, strength) * 0.012f * (1f - u) * (1f - u) * Mathf.Min(1f, 300f / Mathf.Max(1f, distance));
        }

        /// <summary>...and its front, as a share of the half diagonal: from <paramref name="edge"/> out past the corners (1.25).</summary>
        public static float RippleFront(float edge, float u) => Clamp01(edge) + (1.25f - Clamp01(edge)) * EaseOutCubic(u);

        /// <summary>The page's lens ripple in a pixel (<c>lensMat</c>): the displacement at screen point <paramref name="p"/>.</summary>
        public static Vector2 RippleDisplacement(Vector2 p, float aspect, float amplitude, float front, float edge)
        {
            if (amplitude <= 0f) return Vector2.zero;
            float hd = 0.5f * Mathf.Sqrt(aspect * aspect + 1f), r = p.magnitude / hd;
            float mask = Smooth(edge, Mathf.Min(1f, edge + 0.25f), r), x = (r - front) / 0.07f;
            return p / Mathf.Max(p.magnitude, 1e-4f) * (amplitude * mask * Mathf.Exp(-x * x) * Mathf.Sin(x * 3.14159f));
        }

        /// <summary>A bridge ring's place along the tunnel (<c>drawBridge</c>'s <c>ringS</c>).</summary>
        public static float BridgeRingS(float L, int i, int rings) => L * (i + 0.5f) / Mathf.Max(1, rings);

        /// <summary>How condensed a bridge spindle at <paramref name="s"/> is: it grows in at the formed window's fronts.</summary>
        public static float BridgeGrow(float L, float s0, float s1, float s) =>
            Smooth(0f, Mathf.Max(4f, 0.06f * L), Mathf.Min(s - s0, s1 - s));

        /// <summary>The bridge's brightness at <paramref name="u"/> along it, with the shimmer running way in to way out.</summary>
        public static float BridgeGlow(float u, float shimmer, float time) =>
            0.85f * (1f + Clamp01(shimmer) * 1.4f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * (u * 1.5f - time * 0.5f))), 4f));

        /// <summary>A tunnel's brightness as it fades after collapsing (<c>FADE_S</c>).</summary>
        public static float Fade(bool dying, float dieT) => dying ? Mathf.Max(0f, 1f - dieT / FadeSeconds) : 1f;
    }
}
