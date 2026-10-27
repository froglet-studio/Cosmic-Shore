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
    /// <para><b>The tunnel</b> (round 7, an umbrella handle): from its way in <c>A</c> a straight SHAFT of length <c>S</c> along
    /// <c>t0</c>, then a BEND of radius <c>R</c> toward <c>side</c> through <c>theta</c> radians; <c>L = S + R theta</c>.
    /// <c>s</c> runs 0 at the way in to <c>L</c> at the way out. Its wall is a hyperboloid of FIXED size: throat radius
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
        }

        /// <summary>What the sticks shape (the page's <c>shapeFor</c> return): L mouth to mouth, S the shaft, R the bend's radius.</summary>
        public struct Shape
        {
            public float L, S, R, B, M, Theta, Fat, Stick, Dx, Dy;
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
        /// is pushed, up to <see cref="ShapeSettings.BendMaxDegrees"/>, the way it is pushed. Its size is fixed.
        /// </summary>
        public static Shape ShapeFor(in ShapeSettings p, float lq, float sx, float sy)
        {
            float tx = Mathf.Max(-1f, Mathf.Min(1f, sx)), ty = Mathf.Max(-1f, Mathf.Min(1f, sy));
            float mRaw = Mathf.Sqrt(tx * tx + ty * ty), m = Mathf.Min(1f, mRaw), t = Clamp01((m - 0.05f) / 0.95f);
            float S = Mathf.Max(0f, p.ShaftMin + (Mathf.Max(p.ShaftMin, p.ShaftMax) - p.ShaftMin) * Clamp01(lq));
            float theta = Mathf.Max(0f, p.BendMaxDegrees) * Mathf.PI / 180f * t, R = Mathf.Max(1f, p.BendRadius);
            float b = Mathf.Max(0.5f, p.ThroatRadius);
            return new Shape
            {
                L = Mathf.Max(10f, S + R * theta), S = S, R = R, B = b, M = Mathf.Max(b, p.MouthRadius), Theta = theta, Fat = t,
                Stick = m, Dx = mRaw > 1e-6f ? tx / mRaw : 0f, Dy = mRaw > 1e-6f ? ty / mRaw : 0f,
            };
        }

        /// <summary>The page's <c>bendAngle</c>: how far the centreline has turned at <paramref name="s"/> - nothing along the shaft, then (s - S) / R.</summary>
        public static float BendAngle(float S, float R, float theta, float s) =>
            theta < 1e-4f ? 0f : Mathf.Max(0f, Mathf.Min(theta, (s - S) / Mathf.Max(1e-6f, R)));

        /// <summary>The page's <c>whAt</c>: the point <paramref name="s"/> along the tunnel, and its heading there.</summary>
        public static Vector3 At(Vector3 a, Vector3 t0, Vector3 side, float S, float R, float theta, float s, out Vector3 tangent)
        {
            if (theta < 1e-4f || s <= S)
            {
                tangent = t0;
                return a + t0 * s;
            }
            float ang = BendAngle(S, R, theta, s);
            tangent = t0 * Mathf.Cos(ang) + side * Mathf.Sin(ang);
            return a + t0 * (S + R * Mathf.Sin(ang)) + side * (R * (1f - Mathf.Cos(ang)));
        }

        /// <summary>The page's <c>whFrame</c>: the wall's two normals at <paramref name="s"/> (in the bend's plane, and across it).</summary>
        public static void Frame(Vector3 t0, Vector3 side, float S, float R, float theta, float s, out Vector3 normal, out Vector3 binormal)
        {
            float ang = BendAngle(S, R, theta, s);
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
                float x = ahead + sh.S + sh.R * Mathf.Sin(th), y = sh.R * (1f - Mathf.Cos(th));
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
