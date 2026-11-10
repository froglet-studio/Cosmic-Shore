using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Hood's arena, in closed form (zero random draws, Breakwater's and Serpentine's rule): every peer lays the same
    /// covers from the intensity alone. See <c>Assets/_Scripts/Controller/Arcade/SERPENTHOOD.md</c>.
    ///
    /// <para><b>A cover is a Serpentine ring shape bent into a hood</b> (Shombith, 2026-11-09: "I like the shapes that we
    /// have around the rings in its time game, those can be covers"; the cobra-faced drafts were "too mythical"). The
    /// shape is <see cref="SerpentineGate.Build"/> unchanged (its plain shell, its danger weave and exits, its plain core),
    /// built at a 72 u ring, then <see cref="Warp"/>ed: flared wider across, tapering toward one end, and the sides
    /// curled back into a cup. The mouth (the weave) faces the crystal heart; the sniper sits in the cup on the far side
    /// and fires out through the exits. The Sniper Shot pierces everything (<c>pierceCount</c> 0), so a hood is
    /// concealment, not armour.</para>
    ///
    /// <para><b>The perches</b> stand on a Fibonacci sphere round the heart, every mouth turned to it, so every perch sees
    /// every other across the open middle. At I3-I4 <b>shutters</b> (the same hood, turned across) stand near the heart
    /// and break the straight lines through it. The ladder (<see cref="RungFor"/>): Coil hoods on 1,500 u at I1, Coil on
    /// 1,350 u at I2, Maw on 1,250 u with six shutters at I3, Iris on 1,120 u with six shutters at I4 - shots get shorter,
    /// the cup deeper, the core smaller.</para>
    ///
    /// <para><b>Mirrored, and parity-held, in Python</b>: <c>Tools/Build/serpent_hood_budget.py</c> builds the same parts
    /// (positions and orientations), and the generator compares the two on every run. Change a number here and there
    /// together.</para>
    /// </summary>
    public static class SerpentHoodLayout
    {
        /// <summary>The Serpentine ring every cover is built from: I1's port size, so every rung keeps the 26 u exits.</summary>
        public const float CoverRingRadius = 72f;

        /// <summary>One rung of the ladder.</summary>
        public readonly struct Rung
        {
            public readonly int Perches;
            public readonly float PerchRadius;
            public readonly int Shutters;
            public readonly float ShutterRadius;
            /// <summary>The hood's widest stretch across, its stretch at the narrow end, and how hard its sides curl back.</summary>
            public readonly float Wide, Narrow, Cup;

            public Rung(int perches, float perchRadius, int shutters, float shutterRadius, float wide, float narrow, float cup)
            {
                Perches = perches; PerchRadius = perchRadius; Shutters = shutters; ShutterRadius = shutterRadius;
                Wide = wide; Narrow = narrow; Cup = cup;
            }
        }

        public static Rung RungFor(int intensity) => Mathf.Clamp(intensity, 1, 4) switch
        {
            1 => new Rung(8, 1500f, 0, 0f, 1.35f, 0.55f, 0.18f),
            2 => new Rung(12, 1350f, 0, 0f, 1.45f, 0.50f, 0.24f),
            3 => new Rung(18, 1250f, 6, 700f, 1.50f, 0.45f, 0.30f),
            _ => new Rung(20, 1120f, 6, 660f, 1.60f, 0.40f, 0.36f),
        };

        /// <summary>Where a cover stands and which way its mouth looks. A perch's mouth looks at the heart; a shutter's
        /// is turned across it.</summary>
        public readonly struct Cover
        {
            public readonly int Index;
            public readonly Vector3 Centre;
            public readonly Vector3 Axis;
            public readonly bool Shutter;

            public Cover(int index, Vector3 centre, Vector3 axis, bool shutter)
            {
                Index = index; Centre = centre; Axis = axis; Shutter = shutter;
            }

            /// <summary>The side of the hood the sniper sits on: away from where its mouth looks.</summary>
            public Vector3 CupSide => -Axis;
        }

        const float GoldenAngle = 2.39996323f;

        /// <summary>Point <paramref name="i"/> of <paramref name="n"/> spread evenly over the unit sphere.</summary>
        public static Vector3 Fibonacci(int i, int n)
        {
            float y = 1f - 2f * (i + 0.5f) / n;
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float a = i * GoldenAngle;
            return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
        }

        /// <summary>Every cover at <paramref name="intensity"/> round <paramref name="centre"/>: the perches first, then
        /// the shutters. The index is each cover's twist (its shape's pattern turns from the last, as Serpentine's gates do).</summary>
        public static void Covers(Vector3 centre, int intensity, List<Cover> into)
        {
            var rung = RungFor(intensity);
            int index = 0;
            for (int i = 0; i < rung.Perches; i++)
            {
                var d = Fibonacci(i, rung.Perches);
                into.Add(new Cover(index++, centre + d * rung.PerchRadius, -d, shutter: false));
            }
            for (int i = 0; i < rung.Shutters; i++)
            {
                var d = Fibonacci(i, rung.Shutters);
                var across = Vector3.Cross(d, Fibonacci((i + 1) % rung.Shutters, rung.Shutters)).normalized;
                into.Add(new Cover(index++, centre + d * rung.ShutterRadius, across, shutter: true));
            }
        }

        /// <summary>The Serpentine shape this rung's hoods are made of: Coil at I1-I2, Maw at I3, Iris at I4.</summary>
        public static SerpentineGate.Shape ShapeFor(int intensity) => SerpentineGate.ShapeFor(intensity);

        /// <summary>
        /// The hood warp, in the shape's own frame (x across, y along the hood, z the mouth's axis): x is stretched by
        /// <see cref="Rung.Wide"/> where the hood is widest, falling to <see cref="Rung.Narrow"/> at the tapered end
        /// (y below 0, which is also drawn 1.25x longer); the sides and the crown curl back toward the cup (-z).
        /// <paramref name="reach"/> is the unwarped shape's outer reach (<see cref="SerpentineGate.OuterReach"/>).
        /// </summary>
        public static Vector3 Warp(Vector3 p, in Rung rung, float reach)
        {
            float v = p.y / reach;
            float k = v < 0f
                ? rung.Wide - (rung.Wide - rung.Narrow) * Mathf.Pow(Mathf.Min(1f, -v), 1.3f)
                : rung.Wide - 0.3f * v * v;
            float y = p.y * (v < 0f ? 1.25f : 1f);
            float x = p.x * k;
            float vp = Mathf.Max(0f, v);
            float z = p.z - rung.Cup * x * x / reach - 0.10f * vp * vp * reach;
            return new Vector3(x, y, z);
        }

        /// <summary>The warp's derivative along <paramref name="d"/> at <paramref name="p"/> (central difference): how a
        /// part's own axes bend with it.</summary>
        static Vector3 Bend(Vector3 p, Vector3 d, in Rung rung, float reach)
        {
            const float e = 0.25f;
            return (Warp(p + d * e, rung, reach) - Warp(p - d * e, rung, reach)) / (2f * e);
        }

        /// <summary>The frame a cover's local shape is placed in (<see cref="SerpentineGate"/>'s own construction):
        /// local x on <paramref name="s"/>, y on <paramref name="u"/>, z on the axis.</summary>
        public static void Frame(Vector3 axis, out Vector3 s, out Vector3 u, out Vector3 f)
        {
            f = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.forward;
            s = Vector3.Cross(f, Vector3.up);
            if (s.sqrMagnitude < 1e-4f) s = Vector3.Cross(f, Vector3.right);
            s.Normalize();
            u = Vector3.Cross(s, f);
        }

        static readonly List<SerpentineGate.Part> s_local = new();
        static readonly List<SerpentineGate.Part> s_warped = new();

        /// <summary>
        /// Cover <paramref name="index"/>'s prisms in the SHAPE's own frame (x across, y along the hood, z the mouth's
        /// axis), warped into the hood, in Serpentine's order (core, then weave, then shell). <see cref="Build"/> places
        /// these; the tests read them here, where the mouth is the z = 0 plane.
        /// </summary>
        public static void BuildLocal(int index, int intensity, List<SerpentineGate.Part> into)
        {
            var rung = RungFor(intensity);
            float reach = SerpentineGate.OuterReach(CoverRingRadius, intensity);
            s_local.Clear();
            SerpentineGate.Build(new RaceGate(Vector3.zero, Vector3.forward, CoverRingRadius), index, intensity, s_local);
            for (int i = 0; i < s_local.Count; i++)
            {
                var part = s_local[i];
                Vector3 p = part.Position;
                Vector3 fwd = Bend(p, part.Rotation * Vector3.forward, rung, reach);
                Vector3 up = Bend(p, part.Rotation * Vector3.up, rung, reach);
                var rotation = fwd.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(fwd.normalized, up) : Quaternion.identity;
                into.Add(new SerpentineGate.Part(Warp(p, rung, reach), rotation, part.Scale, part.Kind, part.Core, part.Weave));
            }
        }

        /// <summary>
        /// Every prism of <paramref name="cover"/>, in world space, appended to <paramref name="into"/> in Serpentine's
        /// order (core first, then the weave, then the shell, so a core check can stop at the first non-core part).
        /// Returns the farthest any part stands from the cover's centre.
        /// </summary>
        public static float Build(in Cover cover, int intensity, List<SerpentineGate.Part> into)
        {
            s_warped.Clear();
            BuildLocal(cover.Index, intensity, s_warped);
            Frame(cover.Axis, out var s, out var u, out var f);

            float farthest = 0f;
            for (int i = 0; i < s_warped.Count; i++)
            {
                var part = s_warped[i];
                Vector3 q = part.Position;
                Vector3 lf = part.Rotation * Vector3.forward, lu = part.Rotation * Vector3.up;
                Vector3 world = cover.Centre + s * q.x + u * q.y + f * q.z;
                Vector3 wf = s * lf.x + u * lf.y + f * lf.z;
                Vector3 wu = s * lu.x + u * lu.y + f * lu.z;
                into.Add(new SerpentineGate.Part(world, Quaternion.LookRotation(wf, wu), part.Scale, part.Kind, part.Core,
                                                 part.Weave));
                farthest = Mathf.Max(farthest, (world - cover.Centre).magnitude + part.Scale.z * 0.5f);
            }
            return farthest;
        }

        /// <summary>Where a sniper sits in a perch's cup: on its axis, <paramref name="depth"/> back from the mouth.</summary>
        public static Vector3 CupPoint(in Cover cover, float depth) => cover.Centre + cover.CupSide * depth;

        /// <summary>The crystal heart: <paramref name="count"/> slots spread evenly through a ball of
        /// <paramref name="radius"/> (Fibonacci directions, cube-root radii from the golden sequence), so the field is
        /// closed form and the drawing and the game agree on where every crystal floats.</summary>
        public static Vector3 FieldSlot(Vector3 centre, int i, int count, float radius)
        {
            const float Phi = 1.6180339887f;
            float t = i * Phi + 0.3f;
            t -= Mathf.Floor(t);
            return centre + Fibonacci(i, count) * (radius * Mathf.Pow(t, 1f / 3f));
        }
    }
}
