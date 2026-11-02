using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The shape standing at every Serpentine ring (Shombith, 2026-10-31, after Breakwater's stations: "such a cool big
    /// shape around the ring ... there are places to leave from but hitting the middle disintegrates it"). Three parts, in
    /// the order a pilot meets them:
    /// <list type="bullet">
    /// <item><b>The shell</b>, plain: rings of plates outside the ring, flaring back toward the pilot as a shallow bowl
    /// out to <see cref="ShellRatio"/> of the ring's radius. A landmark you see from far away, and it cannot hurt you.</item>
    /// <item><b>The weave</b>, danger, across the ring's mouth: a pattern of bars a hull cannot slip through anywhere but
    /// at the <b>exits</b> (2 or 3 holes carved out of it, sized for the Serpent), the places to leave from.</item>
    /// <item><b>The core</b>, plain, at the ring's centre: it shrinks with intensity so the shot gets harder (Shombith,
    /// 2026-10-31, "make shots harder on higher levels", picked "smaller core only"): seven 6 u blocks at I1, three 5 u
    /// blocks at I2, one 5 u block at I3, one 3.5 u block at I4 (<see cref="CoreFor"/>). Break any core block (a sniper
    /// round, a ram) and the mode disintegrates the whole shape (<see cref="SerpentTimeController"/>).</item>
    /// </list>
    /// <para>Three shapes, one per intensity band (picked by Shombith): <see cref="Shape.Coil"/> at I1-I2 (six scaled arms
    /// spiral in to the core, a horn of scale rings behind), <see cref="Shape.Maw"/> at I3 (fangs from the rim, a web of
    /// rings behind them, a wide cobra hood), <see cref="Shape.Iris"/> at I4 (nine wide blades swept in like a camera
    /// iris, a housing ring of blocks).</para>
    /// <para><b>Closed form, zero random draws</b>, Breakwater's rule (<see cref="BreakwaterStationBuilder"/>): every peer
    /// builds the same shape from the broadcast course, the gate's index turning each gate's pattern so neighbouring
    /// gates differ. Every part is <see cref="PrismKind.Plain"/> or <see cref="PrismKind.Danger"/> (a shield's reach
    /// would weld the weave shut, Breakwater's reasoning). <c>SerpentineGateTests</c> holds the exits clear, the weave
    /// closed to a hull everywhere else, and the prism budget.</para>
    /// </summary>
    public static class SerpentineGate
    {
        public enum Shape { Coil = 0, Maw = 1, Iris = 2 }

        /// <summary>One prism of a gate's shape, in world space.</summary>
        public readonly struct Part
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            /// <summary>x across, y along the ring's axis (thickness), z the part's length.</summary>
            public readonly Vector3 Scale;
            public readonly PrismKind Kind;
            public readonly bool Core;
            public readonly bool Weave;

            public Part(Vector3 position, Quaternion rotation, Vector3 scale, PrismKind kind, bool core, bool weave)
            {
                Position = position; Rotation = rotation; Scale = scale; Kind = kind; Core = core; Weave = weave;
            }
        }

        // ── The core ──────────────────────────────────────────────────────────────────────────
        /// <summary>The core at an intensity: how many blocks (1 at the centre, 3 or 7 round it), their size, and how far
        /// the outer ones stand from the centre.</summary>
        public readonly struct Core
        {
            public readonly int Blocks;
            public readonly float Cube;
            public readonly float Spacing;
            public Core(int blocks, float cube, float spacing) { Blocks = blocks; Cube = cube; Spacing = spacing; }

            /// <summary>Radius of the circle that contains every core block.</summary>
            public float Reach => (Blocks > 1 ? Spacing : 0f) + Cube * 0.5f * Mathf.Sqrt(2f);
        }

        /// <summary>The core shrinks as the intensity rises: 7 x 6 u, 3 x 5 u, 1 x 5 u, 1 x 3.5 u.</summary>
        public static Core CoreFor(int intensity) => Mathf.Clamp(intensity, 1, 4) switch
        {
            1 => new Core(7, 6f, 7f),
            2 => new Core(3, 5f, 4f),
            3 => new Core(1, 5f, 0f),
            _ => new Core(1, 3.5f, 0f),
        };

        /// <summary>The weave's curves start here: clear of the core, and never closer in than 10 u, where a dozen arms
        /// or blades would crowd into one another.</summary>
        public static float WeaveStart(int intensity) => Mathf.Max(CoreFor(intensity).Reach + 1.5f, 10f);

        /// <summary>The exits are laid out round the I1 core at every intensity, so a smaller core makes the SHOT harder
        /// and leaves the exits as they were.</summary>
        static readonly float ExitBandStart = new Core(7, 6f, 7f).Reach + 1.5f;

        // ── The weave ─────────────────────────────────────────────────────────────────────────
        /// <summary>Distance between neighbouring passes of the weave: Breakwater's rake pitch, against which a hull
        /// finds no hole but the exits.</summary>
        public const float Pitch = 12f;
        public const float BarCross = 3f;
        /// <summary>Longest chord a curve is cut into.</summary>
        public const float ChordMax = 12f;
        /// <summary>The weave stops this far inside the ring's rim.</summary>
        const float RimMargin = 2f;
        const int CoilArms = 6;
        const float IrisSweep = 1.6f;
        const float IrisPlateWidth = 7f;
        const float IrisPlateThickness = 1.5f;
        /// <summary>Angular spacing of the Maw's fangs at the rim, and the radial pitch of the web rings behind them.</summary>
        const float MawFangSpacing = 18f;
        const float MawRingPitch = 10f;
        /// <summary>The Maw's short fangs reach in to this share of the radius; the long ones to the core.</summary>
        const float MawShortFang = 0.55f;

        // ── The exits ─────────────────────────────────────────────────────────────────────────
        const float ExitMaxRadius = 26f;
        const int MaxExits = 3;
        const float ExitMargin = 1.5f;

        // ── The shell ─────────────────────────────────────────────────────────────────────────
        /// <summary>The shell reaches out to this multiple of the ring's radius (Breakwater's dish ratio).</summary>
        public const float ShellRatio = 1.75f;
        /// <summary>How far the shell flares back toward the pilot per unit of radius beyond the rim: a shallow bowl.</summary>
        public const float ShellFlare = 0.6f;
        const float ShellPitch = 16f;
        const float ShellPlateWidth = 8.5f;
        const float ShellPlateLength = 12f;
        const float ShellPlateThickness = 1.5f;
        /// <summary>The Maw's hood is this much wider across (its first in-plane direction) than it is tall.</summary>
        const float HoodStretch = 1.3f;
        const float HousingCube = 6f;

        /// <summary>The shape at <paramref name="intensity"/>: Coil at I1-I2, Maw at I3, Iris at I4.</summary>
        public static Shape ShapeFor(int intensity) =>
            intensity <= 2 ? Shape.Coil : intensity == 3 ? Shape.Maw : Shape.Iris;

        public static int ExitCount(Shape shape) => shape == Shape.Coil ? 2 : 3;

        public static int ExitCount(int intensity) => ExitCount(ShapeFor(intensity));

        /// <summary>An exit's radius: half the band between the weave's start and the rim, less a margin, at most 26 u.</summary>
        public static float ExitRadius(float ringRadius) =>
            Mathf.Min(ExitMaxRadius, (ringRadius - RimMargin - ExitBandStart) * 0.5f - ExitMargin);

        /// <summary>How far an exit's centre stands from the ring's centre: the middle of the weave's band.</summary>
        public static float ExitDistance(float ringRadius) => (ExitBandStart + ringRadius - RimMargin) * 0.5f;

        /// <summary>The farthest any part of a gate's shape reaches from the ring's centre (for keeping debris clear).</summary>
        public static float OuterReach(float ringRadius, int intensity)
        {
            var shape = ShapeFor(intensity);
            float r = ringRadius * ShellRatio;
            if (shape == Shape.Maw) r *= HoodStretch;
            float back = (ringRadius * ShellRatio - ringRadius) * ShellFlare;
            return Mathf.Sqrt(r * r + back * back) + ShellPlateLength;
        }

        /// <summary>Exit <paramref name="exit"/> of gate <paramref name="gateIndex"/>, as a ring the AI can aim through
        /// (same axis as the gate; it lies inside the gate's mouth, so threading it threads the gate).</summary>
        public static RaceGate Exit(in RaceGate ring, int gateIndex, int intensity, int exit)
        {
            var shape = ShapeFor(intensity);
            Frame(ring, gateIndex, out Vector3 s, out Vector3 u, out Vector3 axis, out float twist);
            float a = twist + 0.5f + exit * 2f * Mathf.PI / ExitCount(shape);
            Vector3 c = ring.Position + (Mathf.Cos(a) * s + Mathf.Sin(a) * u) * ExitDistance(ring.Radius);
            return new RaceGate(c, axis, ExitRadius(ring.Radius));
        }

        /// <summary>Every part of gate <paramref name="gateIndex"/>'s shape at <paramref name="ring"/>, appended to
        /// <paramref name="into"/>: core first, then the weave, then the shell.</summary>
        public static void Build(in RaceGate ring, int gateIndex, int intensity, List<Part> into)
        {
            var shape = ShapeFor(intensity);
            var core = CoreFor(intensity);
            float weaveStart = WeaveStart(intensity);
            Frame(ring, gateIndex, out Vector3 s, out Vector3 u, out Vector3 axis, out float twist);
            var g = new Geometry(ring.Position, s, u, axis, ring.Radius, gateIndex, shape, weaveStart, into);

            // The core: one block at the centre (I3, I4), or blocks round it (three at I2, seven at I1 with one central).
            if (core.Blocks == 1 || core.Blocks == 7)
                g.Block(Vector2.zero, 0f, core.Cube, PrismKind.Plain, core: true);
            int outerBlocks = core.Blocks == 7 ? 6 : core.Blocks == 3 ? 3 : 0;
            for (int k = 0; k < outerBlocks; k++)
            {
                float a = twist + k * 2f * Mathf.PI / outerBlocks;
                g.Block(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * core.Spacing, 0f, core.Cube, PrismKind.Plain, core: true);
            }

            switch (shape)
            {
                case Shape.Coil:
                {
                    // Six Archimedean arms, r = start + b t, theta = arm + t: neighbouring passes are 2 pi b / 6 = Pitch apart.
                    float b = Pitch * CoilArms / (2f * Mathf.PI);
                    float tEnd = (ring.Radius - RimMargin - weaveStart) / b;
                    for (int arm = 0; arm < CoilArms; arm++)
                        g.Spiral(twist + arm * 2f * Mathf.PI / CoilArms, b, tEnd, BarCross, BarCross);
                    g.Circle(ring.Radius - RimMargin - BarCross * 0.5f, 0f);
                    g.ShellRings(stagger: true, stretch: 1f);
                    break;
                }
                case Shape.Iris:
                {
                    // Blades swept IrisSweep radians from rim to core; enough of them that neighbours are a Pitch apart.
                    float b = (ring.Radius - RimMargin - weaveStart) / IrisSweep;
                    int blades = Mathf.CeilToInt(2f * Mathf.PI * b / Pitch);
                    for (int k = 0; k < blades; k++)
                        g.Spiral(twist + k * 2f * Mathf.PI / blades, b, IrisSweep, IrisPlateWidth, IrisPlateThickness);
                    g.Circle(ring.Radius - RimMargin - BarCross * 0.5f, 0f);
                    g.Housing();
                    g.ShellRings(stagger: false, stretch: 1f, firstRing: 1);
                    break;
                }
                default: // Maw
                {
                    float rim = ring.Radius - RimMargin;
                    int fangs = Mathf.CeilToInt(2f * Mathf.PI * ring.Radius / MawFangSpacing);
                    if (fangs % 2 == 1) fangs++;
                    for (int k = 0; k < fangs; k++)
                    {
                        float a = twist + k * 2f * Mathf.PI / fangs;
                        float inner = k % 2 == 0 ? weaveStart : ring.Radius * MawShortFang;
                        g.Radial(a, inner, rim, -BarCross * 0.5f);
                    }
                    for (float r = weaveStart + MawRingPitch * 0.5f; r <= rim - BarCross; r += MawRingPitch)
                        g.Circle(r, BarCross * 0.5f);
                    g.ShellRings(stagger: true, stretch: HoodStretch);
                    break;
                }
            }
        }

        /// <summary>The ring's in-plane basis (the same construction <see cref="SerpentTimeCluster.Ring"/> uses), its axis,
        /// and this gate's twist, so each gate's pattern and exits are turned from the last.</summary>
        static void Frame(in RaceGate ring, int gateIndex, out Vector3 s, out Vector3 u, out Vector3 axis, out float twist)
        {
            axis = ring.Axis.sqrMagnitude > 1e-6f ? ring.Axis.normalized : Vector3.forward;
            s = Vector3.Cross(axis, Vector3.up);
            if (s.sqrMagnitude < 1e-4f) s = Vector3.Cross(axis, Vector3.right);
            s.Normalize();
            u = Vector3.Cross(s, axis);
            twist = gateIndex * SerpentTimeCluster.TwistPerGateRadians;
        }

        /// <summary>The builder's working state: the frame, the exits it carves round, and the list it fills.</summary>
        readonly struct Geometry
        {
            readonly Vector3 _centre, _s, _u, _axis;
            readonly float _radius;
            readonly Vector2[] _exits;
            readonly float _exitRadius;
            readonly float _weaveStart;
            readonly List<Part> _into;

            public Geometry(Vector3 centre, Vector3 s, Vector3 u, Vector3 axis, float radius, int gateIndex, Shape shape,
                            float weaveStart, List<Part> into)
            {
                _centre = centre; _s = s; _u = u; _axis = axis; _radius = radius; _into = into; _weaveStart = weaveStart;
                int n = ExitCount(shape);
                _exits = new Vector2[n];
                float twist = gateIndex * SerpentTimeCluster.TwistPerGateRadians;
                for (int e = 0; e < n; e++)
                {
                    float a = twist + 0.5f + e * 2f * Mathf.PI / n;
                    _exits[e] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ExitDistance(radius);
                }
                _exitRadius = ExitRadius(radius);
            }

            Vector3 World(Vector2 p, float along) => _centre + _s * p.x + _u * p.y + _axis * along;
            Vector3 Dir(Vector2 d) => _s * d.x + _u * d.y;

            /// <summary>Is <paramref name="p"/> within <paramref name="clear"/> of an exit's edge (inside the exit grown by it)?</summary>
            bool InExit(Vector2 p, float clear)
            {
                for (int e = 0; e < _exits.Length; e++)
                    if ((p - _exits[e]).sqrMagnitude < (_exitRadius + clear) * (_exitRadius + clear)) return true;
                return false;
            }

            public void Block(Vector2 p, float along, float size, PrismKind kind, bool core)
            {
                _into.Add(new Part(World(p, along), Quaternion.LookRotation(_axis, Dir(new Vector2(1f, 0f))),
                                   new Vector3(size, size, size), kind, core, weave: false));
            }

            /// <summary>
            /// A weave bar across the mouth from <paramref name="a"/> to <paramref name="b"/> in the plane, TRIMMED at every
            /// exit's edge (grown by half the bar's width): the pieces outside the exits are laid, the rest is the hole.
            /// Trimming rather than dropping a whole chord keeps the weave closed right up to each exit.
            /// </summary>
            void Bar(Vector2 a, Vector2 b, float along, float width, float thickness)
            {
                Vector2 d = b - a;
                if (d.magnitude < 0.5f) return;
                float clear = width * 0.5f;

                // Where the segment enters or leaves each exit's circle, as fractions of it, plus both ends.
                Span<float> cuts = stackalloc float[2 + 2 * MaxExits];
                int n = 0;
                cuts[n++] = 0f;
                cuts[n++] = 1f;
                float rr = _exitRadius + clear;
                for (int e = 0; e < _exits.Length; e++)
                {
                    Vector2 f = a - _exits[e];
                    float qa = Vector2.Dot(d, d), qb = 2f * Vector2.Dot(f, d), qc = Vector2.Dot(f, f) - rr * rr;
                    float disc = qb * qb - 4f * qa * qc;
                    if (disc <= 0f) continue;
                    float sq = Mathf.Sqrt(disc);
                    float t1 = (-qb - sq) / (2f * qa), t2 = (-qb + sq) / (2f * qa);
                    if (t1 > 0f && t1 < 1f) cuts[n++] = t1;
                    if (t2 > 0f && t2 < 1f) cuts[n++] = t2;
                }
                for (int i = 1; i < n; i++)   // insertion sort: at most eight entries
                {
                    float v = cuts[i];
                    int j = i - 1;
                    while (j >= 0 && cuts[j] > v) { cuts[j + 1] = cuts[j]; j--; }
                    cuts[j + 1] = v;
                }

                for (int i = 0; i + 1 < n; i++)
                {
                    float t0 = cuts[i], t1 = cuts[i + 1];
                    Vector2 p0 = a + d * t0, p1 = a + d * t1;
                    float len = (p1 - p0).magnitude;
                    if (len < 1f || InExit((p0 + p1) * 0.5f, clear)) continue;
                    var rot = Quaternion.LookRotation(Dir((p1 - p0) / len), _axis);
                    _into.Add(new Part(World((p0 + p1) * 0.5f, along), rot, new Vector3(width, thickness, len),
                                       PrismKind.Danger, core: false, weave: true));
                }
            }

            /// <summary>An Archimedean curve r = start + b t, theta = <paramref name="theta0"/> + t, t from 0 to
            /// <paramref name="tEnd"/>, cut into chords of at most <see cref="ChordMax"/>.</summary>
            public void Spiral(float theta0, float b, float tEnd, float width, float thickness)
            {
                float t = 0f;
                Vector2 prev = Polar(_weaveStart, theta0);
                while (t < tEnd - 1e-4f)
                {
                    float r = _weaveStart + b * t;
                    float dt = Mathf.Min(tEnd - t, ChordMax / Mathf.Sqrt(r * r + b * b));
                    t += dt;
                    Vector2 next = Polar(_weaveStart + b * t, theta0 + t);
                    Bar(prev, next, 0f, width, thickness);
                    prev = next;
                }
            }

            /// <summary>A straight fang from radius <paramref name="r0"/> to <paramref name="r1"/> at bearing
            /// <paramref name="a"/>, layered <paramref name="along"/> the axis (so a fang and a web ring meet face to
            /// face rather than passing through each other).</summary>
            public void Radial(float a, float r0, float r1, float along)
            {
                Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                int pieces = Mathf.Max(1, Mathf.CeilToInt((r1 - r0) / 60f));
                for (int i = 0; i < pieces; i++)
                    Bar(d * Mathf.Lerp(r0, r1, i / (float)pieces), d * Mathf.Lerp(r0, r1, (i + 1) / (float)pieces),
                        along, BarCross, BarCross);
            }

            /// <summary>A circle of radius <paramref name="r"/> in the plane, cut into chords.</summary>
            public void Circle(float r, float along)
            {
                int n = Mathf.Max(6, Mathf.CeilToInt(2f * Mathf.PI * r / ChordMax));
                for (int i = 0; i < n; i++)
                    Bar(Polar(r, i * 2f * Mathf.PI / n), Polar(r, (i + 1) * 2f * Mathf.PI / n), along, BarCross, BarCross);
            }

            /// <summary>The Iris's housing: a ring of blocks just outside the rim.</summary>
            public void Housing()
            {
                float r = _radius + HousingCube;
                int n = Mathf.CeilToInt(2f * Mathf.PI * r / (HousingCube * 1.25f));
                for (int i = 0; i < n; i++)
                {
                    Vector2 p = Polar(r, i * 2f * Mathf.PI / n);
                    _into.Add(new Part(World(p, 0f), Quaternion.LookRotation(_axis, Dir(p.normalized)),
                                       new Vector3(HousingCube, HousingCube, HousingCube), PrismKind.Plain, core: false,
                                       weave: false));
                }
            }

            /// <summary>
            /// The shell: rings of plain plates from just outside the rim out to <see cref="ShellRatio"/> of the radius,
            /// each <see cref="ShellFlare"/> per unit further back toward the pilot (a shallow bowl). Staggered rings read
            /// as scales; a <paramref name="stretch"/> over 1 widens it across into the Maw's hood.
            /// </summary>
            public void ShellRings(bool stagger, float stretch, int firstRing = 0)
            {
                int ring = 0;
                for (float rho = _radius + ShellPitch * 0.5f; rho <= _radius * ShellRatio; rho += ShellPitch, ring++)
                {
                    if (ring < firstRing) continue;
                    int n = Mathf.CeilToInt(2f * Mathf.PI * rho / ShellPitch);
                    float offset = stagger && ring % 2 == 1 ? Mathf.PI / n : 0f;
                    float back = -(rho - _radius) * ShellFlare;
                    for (int i = 0; i < n; i++)
                    {
                        float a = offset + i * 2f * Mathf.PI / n;
                        Vector2 radial = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        Vector2 p = new Vector2(radial.x * rho * stretch, radial.y * rho);
                        // The plate lies along the bowl's slope (out, and back toward the pilot), face to the flow.
                        Vector3 slope = Dir(radial) - _axis * ShellFlare;
                        _into.Add(new Part(World(p, back), Quaternion.LookRotation(slope, _axis),
                                           new Vector3(ShellPlateWidth, ShellPlateThickness, ShellPlateLength),
                                           PrismKind.Plain, core: false, weave: false));
                    }
                }
            }
        }

        static Vector2 Polar(float r, float a) => new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;

        /// <summary>Distance from <paramref name="p"/> to the segment <paramref name="a"/>-<paramref name="b"/>.</summary>
        public static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
            return (p - (a + ab * t)).magnitude;
        }
    }
}
