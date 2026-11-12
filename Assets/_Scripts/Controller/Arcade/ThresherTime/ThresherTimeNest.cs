using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The target standing at every Thresher Time gate (Yash, 2026-11-04: "it should not have rings, it should have
    /// objects that it can break as target, and then move to another object"). A NEST: a core of blocks at the gate's
    /// centre inside a cage of plates, armoured more at every intensity. Break any core block (the ball, a chain cut, a
    /// blast, a ram, a steal) and the mode bursts the whole nest and opens the gate (<see cref="ThresherTimeController"/>).
    /// <list type="bullet">
    /// <item><b>I1</b>: seven 6 u core blocks, no cage. Learn to hit things.</item>
    /// <item><b>I2</b>: three 5 u core blocks inside two crossed rings of plain plates.</item>
    /// <item><b>I3</b>: one 5 u block inside three rings, every third plate a DANGER plate.</item>
    /// <item><b>I4</b>: one 3.5 u block inside four SHIELDED rings and two latitude rings, every fourth plate danger, and
    /// a danger halo across the approach. A shielded plate takes two hits; the chain's sweep is what gets through a cage
    /// fast, and every cut banks the ball's energy for the blast that opens the core.</item>
    /// </list>
    /// <para><b>Closed form, zero random draws</b> (Breakwater's and Serpentine's rule): every peer builds the same nest
    /// from the broadcast course, the gate's index turning each cage so neighbouring nests differ. The core is listed
    /// first. <c>ThresherTimeNestTests</c> holds the core-first order, the armour ladder and the budget.</para>
    /// </summary>
    public static class ThresherTimeNest
    {
        /// <summary>One prism of a nest, in world space.</summary>
        public readonly struct Part
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            /// <summary>x across, y radial (thickness), z along the ring.</summary>
            public readonly Vector3 Scale;
            public readonly PrismKind Kind;
            public readonly bool Core;

            public Part(Vector3 position, Quaternion rotation, Vector3 scale, PrismKind kind, bool core)
            {
                Position = position; Rotation = rotation; Scale = scale; Kind = kind; Core = core;
            }
        }

        /// <summary>A nest's numbers at one intensity.</summary>
        public readonly struct Spec
        {
            public readonly int CoreBlocks;
            public readonly float CoreCube;
            public readonly float CoreSpacing;
            /// <summary>Rings through the gate's axis (meridians): the cage the core sits in.</summary>
            public readonly int Meridians;
            /// <summary>Rings across the axis, above and below the core's middle.</summary>
            public readonly int Latitudes;
            public readonly float CageRadius;
            /// <summary>Every Nth cage plate is danger (0 = none).</summary>
            public readonly int DangerEvery;
            public readonly bool ShieldedCage;
            /// <summary>Radius of the danger halo across the approach (0 = none).</summary>
            public readonly float HaloRadius;

            public Spec(int coreBlocks, float coreCube, float coreSpacing, int meridians, int latitudes, float cageRadius,
                        int dangerEvery, bool shieldedCage, float haloRadius)
            {
                CoreBlocks = coreBlocks; CoreCube = coreCube; CoreSpacing = coreSpacing; Meridians = meridians;
                Latitudes = latitudes; CageRadius = cageRadius; DangerEvery = dangerEvery; ShieldedCage = shieldedCage;
                HaloRadius = haloRadius;
            }

            public bool HasCage => Meridians > 0 || Latitudes > 0;
        }

        /// <summary>The ladder: the core shrinks, the cage closes and arms itself as the intensity rises.</summary>
        public static Spec SpecFor(int intensity) => Mathf.Clamp(intensity, 1, 4) switch
        {
            1 => new Spec(7, 6f, 7f, 0, 0, 0f, 0, false, 0f),
            2 => new Spec(3, 5f, 4f, 2, 0, 18f, 0, false, 0f),
            3 => new Spec(1, 5f, 0f, 3, 0, 16f, 3, false, 0f),
            _ => new Spec(1, 3.5f, 0f, 4, 2, 14f, 4, true, 30f),
        };

        public static readonly Vector3 PlateScale = new(3.5f, 1.2f, 5f);
        const float PlateGap = 1.5f;

        /// <summary>The farthest any part of a nest reaches from the gate's centre (a hull inside it blocks a regrow).</summary>
        public static float OuterReach(int intensity)
        {
            var s = SpecFor(intensity);
            float core = (s.CoreBlocks > 1 ? s.CoreSpacing : 0f) + s.CoreCube;
            float cage = s.HasCage ? s.CageRadius + PlateScale.z : 0f;
            float halo = s.HaloRadius > 0f ? s.HaloRadius + PlateScale.z : 0f;
            return Mathf.Max(core, Mathf.Max(cage, halo));
        }

        /// <summary>Does a pilot's move from <paramref name="prev"/> to <paramref name="cur"/> come within
        /// <paramref name="radius"/> of a nest at <paramref name="centre"/>? The credit rule for an opened gate: the
        /// pilot who broke it is beside it, and one following through its wreck passes it.</summary>
        public static bool PassesNear(Vector3 centre, Vector3 prev, Vector3 cur, float radius)
        {
            Vector3 d = cur - prev;
            float len2 = d.sqrMagnitude;
            float t = len2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(centre - prev, d) / len2) : 0f;
            return (prev + d * t - centre).sqrMagnitude <= radius * radius;
        }

        /// <summary>Lay the nest standing at <paramref name="gate"/> into <paramref name="into"/>, core first.</summary>
        public static void Build(in RaceGate gate, int gateIndex, int intensity, List<Part> into)
        {
            into.Clear();
            var s = SpecFor(intensity);
            Vector3 c = gate.Position;
            Vector3 axis = gate.Axis.sqrMagnitude > 0.5f ? gate.Axis.normalized : Vector3.forward;
            // A frame across the axis, turned by the gate's index so neighbouring nests differ.
            Vector3 across = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            across = Quaternion.AngleAxis(gateIndex * 37f, axis) * across;
            Vector3 up = Vector3.Cross(across, axis).normalized;

            // The core: one block at the centre, the rest round it across the axis.
            var cube = Vector3.one * s.CoreCube;
            var coreRot = Quaternion.LookRotation(axis, up);
            into.Add(new Part(c, coreRot, cube, PrismKind.Plain, core: true));
            for (int k = 1; k < s.CoreBlocks; k++)
            {
                float a = (k - 1) * Mathf.PI * 2f / (s.CoreBlocks - 1);
                Vector3 p = c + (across * Mathf.Cos(a) + up * Mathf.Sin(a)) * s.CoreSpacing;
                into.Add(new Part(p, coreRot, cube, PrismKind.Plain, core: true));
            }

            // The cage: rings through the axis (meridians), then rings across it (latitudes).
            int plate = 0;
            for (int m = 0; m < s.Meridians; m++)
            {
                float turn = m * Mathf.PI / s.Meridians;
                Vector3 u = axis;
                Vector3 v = across * Mathf.Cos(turn) + up * Mathf.Sin(turn);
                AddRing(into, c, u, v, s.CageRadius, s, ref plate);
            }
            for (int l = 0; l < s.Latitudes; l++)
            {
                float h = (l % 2 == 0 ? 1f : -1f) * s.CageRadius * 0.5f;
                float r = Mathf.Sqrt(s.CageRadius * s.CageRadius - h * h);
                AddRing(into, c + axis * h, across, up, r, s, ref plate);
            }

            // The halo: a danger ring across the approach, out past the cage.
            if (s.HaloRadius > 0f)
            {
                int none = 0;
                AddRing(into, c - axis * (s.CageRadius * 0.5f), across, up, s.HaloRadius,
                        new Spec(0, 0f, 0f, 0, 0, 0f, 1, false, 0f), ref none);
            }
        }

        static void AddRing(List<Part> into, Vector3 centre, Vector3 u, Vector3 v, float radius, in Spec s, ref int plate)
        {
            int n = Mathf.Max(6, Mathf.RoundToInt(2f * Mathf.PI * radius / (PlateScale.z + PlateGap)));
            for (int i = 0; i < n; i++, plate++)
            {
                float a = i * Mathf.PI * 2f / n;
                Vector3 radial = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                Vector3 tangent = -u * Mathf.Sin(a) + v * Mathf.Cos(a);
                var kind = s.DangerEvery > 0 && plate % s.DangerEvery == 0 ? PrismKind.Danger
                    : s.ShieldedCage ? PrismKind.Shielded : PrismKind.Plain;
                into.Add(new Part(centre + radial * radius, Quaternion.LookRotation(tangent, radial), PlateScale, kind,
                                  core: false));
            }
        }
    }
}
