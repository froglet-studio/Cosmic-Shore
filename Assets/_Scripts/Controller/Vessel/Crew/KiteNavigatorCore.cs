using System;
using System.Collections.Generic;

namespace CosmicShore.Gameplay
{
    /// <summary>A point on the navigator's board: x right, z forward, in world units, in the hull's x-z plane.
    /// The board's own vector type, so the kite core compiles with no engine (the parity harness builds it with plain .NET).</summary>
    public struct KiteVec
    {
        public float X, Z;
        public KiteVec(float x, float z) { X = x; Z = z; }
        public override string ToString() => $"({X:0.###}, {Z:0.###})";
    }

    /// <summary>What a closed loop does with its motes when they are not all one element.</summary>
    public enum KiteMixedLoopRule
    {
        /// <summary>A mixed loop takes the motes and fires nothing (the signed-off rule).</summary>
        Fizzle = 0,
        /// <summary>The most common element fires, one mote weaker for every other mote in the loop.</summary>
        Majority = 1,
    }

    /// <summary>How a pointer (a thumb or a mouse) holds the kite.</summary>
    public enum KiteControl
    {
        /// <summary>The kite flies to a point above the thumb (round 3, the Crew Vessel Lab).</summary>
        Follow = 0,
        /// <summary>The thumb holds an invisible rope: the kite trails it and small circles whirl it round in big ones
        /// (Wrasse Lasso Lab round 1, Garrett 2026-10-20: "Sling is excellent. bring it to unity").</summary>
        Sling = 1,
    }

    /// <summary>The kite numbers the core reads, from <see cref="KiteNavigatorConfigSO"/>. A plain struct so the core and its
    /// tests need no ScriptableObject.</summary>
    public struct KiteSettings
    {
        /// <summary>Board radius (u). The board's centre sits <see cref="Anchor"/> × radius AHEAD of the hull.</summary>
        public float BoardRadius, Anchor;
        /// <summary>Top speed toward the aim point (u/s) and how fast the kite reaches it (1/s).</summary>
        public float KiteSpeed, KiteAccel;
        /// <summary>Tail length in board radii.</summary>
        public float TailLength;
        /// <summary>Sling: the rope's length (u), its stiffness when stretched (1/s²) and damping of the stretch (1/s).</summary>
        public float SlingRope, SlingStiffness, SlingDamping;
        /// <summary>Sling: speed lost per second while the thumb holds the rope, and after it lets go (1/s).</summary>
        public float SlingDrag, SlingFreeDrag;
        /// <summary>Sling: the kite's top speed (u/s).</summary>
        public float SlingMaxSpeed;
        /// <summary>
        /// The kite's own bounds as a RECTANGLE - the kite view's screen (Garrett, 2026-10-20: "Let the wrasse travel the whole
        /// screen space") - when both half extents are &gt; 0: |x| ≤ HalfWidth, |z − BoundsCentreZ| ≤ HalfDepth (u). Zero
        /// keeps the round board of radius <see cref="BoardRadius"/> about the board's centre (the labs' bounds).
        /// </summary>
        public float HalfWidth, HalfDepth, BoundsCentreZ;

        /// <summary>True when the kite is bounded by a rectangle (the screen) rather than the round board.</summary>
        public readonly bool Rectangular => HalfWidth > 0f && HalfDepth > 0f;
    }

    /// <summary>
    /// The navigator's kite (Crew Vessel Lab round 3, signed off 2026-10-17; <c>Docs/Studios/CREW_VESSEL_LAB.md</c>): the
    /// thumb's aim point drives the kite, the kite drags a tail, and crossing the tail closes a loop. Everything here is the
    /// lab's <c>kiteMove</c> / <c>tailAdvance</c> / <c>resolveLoop</c> / <c>grow</c> / <c>blastRadius</c>, ported line for line
    /// (float, where the lab is float64) and held to the page frame by frame by <c>Tools/Build/kite_navigator_lab/parity.cjs</c>.
    /// It knows nothing about prisms, hulls or screens: <see cref="KiteNavigator"/> is the glue to the scene.
    /// </summary>
    public sealed class KiteNavigatorCore
    {
        /// <summary>The lab's constants. A tail point is laid each time the kite has moved this far (u).</summary>
        public const float TailStep = 2.5f;
        /// <summary>With the thumb up, the kite's speed decays at this rate (1/s).</summary>
        public const float CoastRate = 4f;
        /// <summary>The kite slows into its aim point: its wanted speed is min(top speed, distance × this).</summary>
        public const float ApproachGain = 7f;
        /// <summary>The kite and every target the thumb sets are kept inside this fraction of the board radius
        /// (a target past the rim could never be reached: L-STU-109).</summary>
        public const float RimFraction = 0.96f;
        /// <summary>A loop's crossing ignores the newest few tail segments, which always touch the head.</summary>
        const int SkipNewest = 3;

        /// <summary>A sling's kite bounces off the board's rim with this much of its outward speed reversed (the lab's rimBounce).</summary>
        public const float RimBounce = 1.4f;

        public KiteVec Position;
        public KiteVec Velocity;
        // the rope's hand last frame (its velocity damps the stretch) and whether the thumb held it last frame
        KiteVec _hand;
        bool _wasDown;
        /// <summary>Oldest point first; the newest is where the kite was when it last laid a point.</summary>
        public readonly List<KiteVec> Tail = new();

        public void Reset(KiteVec at)
        {
            Position = at;
            Velocity = default;
            Tail.Clear();
            _hand = default;
            _wasDown = false;
        }

        /// <summary>The board's centre, in board units.</summary>
        public static KiteVec Centre(in KiteSettings s) => new(0f, s.Anchor * s.BoardRadius);

        /// <summary>A point clamped inside the board's reachable disc (the lab's <c>onBoard</c>), or inside the screen's
        /// rectangle when the settings carry one.</summary>
        public static KiteVec OnBoard(in KiteSettings s, float x, float z)
        {
            if (s.Rectangular)
            {
                float hx = s.HalfWidth * RimFraction, hz = s.HalfDepth * RimFraction, c = s.BoundsCentreZ;
                return new KiteVec(x < -hx ? -hx : x > hx ? hx : x, z < c - hz ? c - hz : z > c + hz ? c + hz : z);
            }
            float a = s.Anchor * s.BoardRadius, r = s.BoardRadius * RimFraction, dz = z - a, d = MathF.Sqrt(x * x + dz * dz);
            return d <= r ? new KiteVec(x, z) : new KiteVec(x / d * r, a + dz / d * r);
        }

        /// <summary>Is this point on the board at all (drawn, and so able to be touched)?</summary>
        public static bool InDisc(in KiteSettings s, float x, float z)
        {
            float dz = z - s.Anchor * s.BoardRadius;
            return x * x + dz * dz <= s.BoardRadius * s.BoardRadius;
        }

        /// <summary>Is this point where the kite can go: inside the screen's rectangle, or the disc when there is none.</summary>
        public static bool InBounds(in KiteSettings s, float x, float z) =>
            s.Rectangular ? MathF.Abs(x) <= s.HalfWidth && MathF.Abs(z - s.BoundsCentreZ) <= s.HalfDepth : InDisc(s, x, z);

        /// <summary>The farthest the kite can be from where the bounds are centred (u): the rectangle's half diagonal, or the
        /// board's radius.</summary>
        public static float BoundsReach(in KiteSettings s) =>
            s.Rectangular ? MathF.Sqrt(s.HalfWidth * s.HalfWidth + s.HalfDepth * s.HalfDepth) : s.BoardRadius;

        /// <summary>
        /// One step of the kite (the lab's <c>kiteMove</c>): with the thumb down it accelerates toward the aim point (clamped
        /// onto the board) and slows into it; with the thumb up it coasts to a stop. Returns where it was, so the caller can
        /// sweep the move for head contacts.
        /// </summary>
        public KiteVec Move(in KiteSettings s, float aimX, float aimZ, bool down, float dt)
        {
            if (down)
            {
                var g = OnBoard(s, aimX, aimZ);
                float dx = g.X - Position.X, dz = g.Z - Position.Z, d = MathF.Sqrt(dx * dx + dz * dz);
                float speed = MathF.Min(s.KiteSpeed, d * ApproachGain);
                float wx = d > 1e-6f ? dx / d * speed : 0f, wz = d > 1e-6f ? dz / d * speed : 0f;
                float a = 1f - MathF.Exp(-s.KiteAccel * dt);
                Velocity.X += (wx - Velocity.X) * a;
                Velocity.Z += (wz - Velocity.Z) * a;
            }
            else
            {
                float k = MathF.Exp(-CoastRate * dt);
                Velocity.X *= k;
                Velocity.Z *= k;
            }
            var prev = Position;
            Position = OnBoard(s, Position.X + Velocity.X * dt, Position.Z + Velocity.Z * dt);
            _wasDown = down;
            return prev;
        }

        /// <summary>
        /// One step of the SLING (the Wrasse Lasso Lab's <c>stepKite</c> case 'sling' and <c>ropeForce</c>, line for line).
        /// The thumb at (<paramref name="handX"/>, <paramref name="handZ"/>) holds an invisible rope: stretched past its length
        /// it pulls the kite back like a spring, damped on the stretch's rate measured against the moving hand, so a hand that
        /// leads the kite round a circle whirls it. With the thumb up the kite flies free and slows. It bounces off the
        /// board's rim. Returns where the kite was, so the caller can sweep the move for head contacts.
        /// </summary>
        public KiteVec Sling(in KiteSettings s, float handX, float handZ, bool down, float dt)
        {
            if (down && !_wasDown) _hand = new KiteVec(handX, handZ);
            if (down)
            {
                RopeForce(s, handX, handZ, dt);
                float c = MathF.Exp(-s.SlingDrag * dt);
                Velocity.X *= c; Velocity.Z *= c;
            }
            else
            {
                float c = MathF.Exp(-s.SlingFreeDrag * dt);
                Velocity.X *= c; Velocity.Z *= c;
            }
            float sp = MathF.Sqrt(Velocity.X * Velocity.X + Velocity.Z * Velocity.Z);
            if (sp > s.SlingMaxSpeed) { Velocity.X *= s.SlingMaxSpeed / sp; Velocity.Z *= s.SlingMaxSpeed / sp; }
            var prev = Position;
            Position = new KiteVec(Position.X + Velocity.X * dt, Position.Z + Velocity.Z * dt);
            Bounce(s);
            _wasDown = down;
            return prev;
        }

        // the lab's ropeForce: a rope pulls only when stretched
        void RopeForce(in KiteSettings s, float ax, float az, float dt)
        {
            float dx = Position.X - ax, dz = Position.Z - az, d = MathF.Sqrt(dx * dx + dz * dz);
            float avx = (ax - _hand.X) / dt, avz = (az - _hand.Z) / dt;
            _hand = new KiteVec(ax, az);
            if (d <= s.SlingRope || d < 1e-6f) return;
            float nx = dx / d, nz = dz / d, vr = (Velocity.X - avx) * nx + (Velocity.Z - avz) * nz;
            float a = s.SlingStiffness * (d - s.SlingRope) + s.SlingDamping * vr;
            Velocity.X -= nx * a * dt;
            Velocity.Z -= nz * a * dt;
        }

        // the lab's rimBounce, about the board's centre; on a rectangle each edge bounces its own axis
        void Bounce(in KiteSettings s)
        {
            if (s.Rectangular)
            {
                float hx = s.HalfWidth * RimFraction, hz = s.HalfDepth * RimFraction, c = s.BoundsCentreZ;
                float px = Position.X, pz = Position.Z;
                if (px > hx) { px = hx; if (Velocity.X > 0f) Velocity.X -= RimBounce * Velocity.X; }
                else if (px < -hx) { px = -hx; if (Velocity.X < 0f) Velocity.X -= RimBounce * Velocity.X; }
                if (pz > c + hz) { pz = c + hz; if (Velocity.Z > 0f) Velocity.Z -= RimBounce * Velocity.Z; }
                else if (pz < c - hz) { pz = c - hz; if (Velocity.Z < 0f) Velocity.Z -= RimBounce * Velocity.Z; }
                Position = new KiteVec(px, pz);
                return;
            }
            float a = s.Anchor * s.BoardRadius, r = s.BoardRadius * RimFraction, dx = Position.X, dz = Position.Z - a, d = MathF.Sqrt(dx * dx + dz * dz);
            if (d <= r) return;
            float nx = dx / d, nz = dz / d;
            Position = new KiteVec(nx * r, a + nz * r);
            float vn = Velocity.X * nx + Velocity.Z * nz;
            if (vn > 0f) { Velocity.X -= RimBounce * vn * nx; Velocity.Z -= RimBounce * vn * nz; }
        }

        /// <summary>
        /// Lay the tail (the lab's <c>tailAdvance</c>): once the kite has moved <see cref="TailStep"/> from the newest point,
        /// test the new segment against the older ones. A crossing closes a loop: <paramref name="loopOut"/> receives the
        /// polygon (crossing point, the tail after it, the kite) and the tail restarts at the kite. The tail is then trimmed
        /// to <see cref="KiteSettings.TailLength"/> board radii. True when a loop closed.
        /// </summary>
        public bool AdvanceTail(in KiteSettings s, List<KiteVec> loopOut)
        {
            loopOut?.Clear();
            var q = Position;
            int n = Tail.Count;
            if (n > 0)
            {
                var last = Tail[n - 1];
                float mx = q.X - last.X, mz = q.Z - last.Z;
                if (mx * mx + mz * mz <= TailStep * TailStep) return false;
            }

            bool closed = false;
            if (n >= 3)
            {
                var a = Tail[n - 1];
                for (int i = 0; i < n - SkipNewest; i++)
                {
                    if (!SegmentCross(a, q, Tail[i], Tail[i + 1], out var x)) continue;
                    if (loopOut != null)
                    {
                        loopOut.Add(x);
                        for (int j = i + 1; j < n; j++) loopOut.Add(Tail[j]);
                        loopOut.Add(q);
                    }
                    Tail.Clear();
                    Tail.Add(q);
                    closed = true;
                    break;
                }
            }
            if (!closed) Tail.Add(q);

            float limit = s.TailLength * s.BoardRadius, total = 0f;
            for (int i = Tail.Count - 1; i > 0; i--)
            {
                float dx = Tail[i].X - Tail[i - 1].X, dz = Tail[i].Z - Tail[i - 1].Z;
                total += MathF.Sqrt(dx * dx + dz * dz);
                if (total > limit) { Tail.RemoveRange(0, i); break; }
            }
            return closed;
        }

        /// <summary>Where segment ab crosses segment cd (the lab's <c>segX</c>: a hit exactly at a counts as no hit).</summary>
        public static bool SegmentCross(KiteVec a, KiteVec b, KiteVec c, KiteVec d, out KiteVec x)
        {
            x = default;
            float r0 = b.X - a.X, r1 = b.Z - a.Z, s0 = d.X - c.X, s1 = d.Z - c.Z, den = r0 * s1 - r1 * s0;
            if (MathF.Abs(den) < 1e-9f) return false;
            float t = ((c.X - a.X) * s1 - (c.Z - a.Z) * s0) / den, u = ((c.X - a.X) * r1 - (c.Z - a.Z) * r0) / den;
            if (!(t > 0f && t <= 1f && u >= 0f && u <= 1f)) return false;
            x = new KiteVec(a.X + r0 * t, a.Z + r1 * t);
            return true;
        }

        /// <summary>Even-odd point in polygon (the lab's <c>pointInPoly</c>).</summary>
        public static bool PointInPolygon(float x, float z, List<KiteVec> poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                var a = poly[i]; var b = poly[j];
                if ((a.Z > z) != (b.Z > z) && x < (b.X - a.X) * (z - a.Z) / (b.Z - a.Z + 1e-9f) + a.X) inside = !inside;
            }
            return inside;
        }

        /// <summary>The polygon's vertex mean: where a loop's effect fires.</summary>
        public static KiteVec Centroid(List<KiteVec> poly)
        {
            float x = 0f, z = 0f;
            for (int i = 0; i < poly.Count; i++) { x += poly[i].X; z += poly[i].Z; }
            return poly.Count > 0 ? new KiteVec(x / poly.Count, z / poly.Count) : default;
        }

        /// <summary>Squared distance from (x, z) to segment ab: the head's swept contact test.</summary>
        public static float DistanceToSegmentSq(float x, float z, KiteVec a, KiteVec b)
        {
            float vx = b.X - a.X, vz = b.Z - a.Z, l2 = vx * vx + vz * vz;
            float t = l2 > 0f ? ((x - a.X) * vx + (z - a.Z) * vz) / l2 : 0f;
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            float px = a.X + vx * t - x, pz = a.Z + vz * t - z;
            return px * px + pz * pz;
        }

        /// <summary>
        /// Which effect a loop fires (the lab's <c>resolveLoop</c>). <paramref name="counts"/> holds the loop's motes per
        /// element, in the HUD's order: Charge, Mass, Space, Time (index = <c>Element</c> − 1). A loop of one element fires
        /// it at n = that count. A mixed loop fires nothing under <see cref="KiteMixedLoopRule.Fizzle"/>; under
        /// <see cref="KiteMixedLoopRule.Majority"/> the most common element fires at n = its count − every other mote.
        /// Ties go to the first element in that order, as in the lab.
        /// </summary>
        public static bool ResolveLoop(int charge, int mass, int space, int time, KiteMixedLoopRule rule, out int element, out int n)
        {
            element = -1; n = 0;
            int total = charge + mass + space + time;
            if (total <= 0) return false;
            int kinds = (charge > 0 ? 1 : 0) + (mass > 0 ? 1 : 0) + (space > 0 ? 1 : 0) + (time > 0 ? 1 : 0);
            int best = 0, bestCount = charge;
            if (mass > bestCount) { best = 1; bestCount = mass; }
            if (space > bestCount) { best = 2; bestCount = space; }
            if (time > bestCount) { best = 3; bestCount = time; }
            int strength = bestCount;
            if (kinds > 1)
            {
                if (rule != KiteMixedLoopRule.Majority) return false;
                strength = bestCount - (total - bestCount);
                if (strength < 1) return false;
            }
            element = best; n = strength;
            return true;
        }

        /// <summary>The signed-off growth law: <c>base × growth^(n − 1)</c>, never past <paramref name="cap"/>.</summary>
        public static float Grow(float baseValue, int n, float growth, float cap)
            => MathF.Min(cap, baseValue * MathF.Pow(growth, n - 1));

        /// <summary>
        /// The radius an <c>AOEExplosion</c> of this MaxScale has reached <paramref name="since"/> seconds after it was
        /// spawned: nothing during the delay, then <c>0.5 × MaxScale × sin(t / duration · π/2)</c> (its 0.5 sphere collider
        /// scaled by <c>Vector3.Lerp(0, MaxScale, sin ease)</c>; <c>AOEExplosion.ExplodeAsync</c>).
        /// </summary>
        public static float BlastRadius(float maxScale, float since, float delay, float duration)
        {
            float t = since - delay;
            if (t < 0f) return 0f;
            float k = MathF.Min(1f, t / duration);
            return 0.5f * maxScale * MathF.Sin(k * MathF.PI / 2f);
        }
    }
}
