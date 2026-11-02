using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>One prism a Switchback feature lays: pose, size and kind.</summary>
    public readonly struct SwitchbackFeaturePrism
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Scale;
        public readonly PrismKind Kind;

        public SwitchbackFeaturePrism(Vector3 position, Quaternion rotation, Vector3 scale, PrismKind kind)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
            Kind = kind;
        }
    }

    /// <summary>Everything Blast Run (<see cref="SwitchbackBlastRun"/>) hangs on one course: its prisms, its blocked
    /// gates and the rigid groups of prisms that move on the shared clock.</summary>
    public sealed class SwitchbackFeatureLayout
    {
        public readonly List<SwitchbackFeaturePrism> Prisms = new(256);
        /// <summary>The blocked gates, in course order.</summary>
        public readonly List<BlastRunStation> Stations = new();
        /// <summary>The rigid groups of the prisms that move on the shared clock (I2+).</summary>
        public readonly List<BlastRunMover> Movers = new();
    }

    /// <summary>The clearances every Switchback course prism keeps, and the distance helper the layout and the tests share.</summary>
    public static class SwitchbackFeatures
    {
        public const float MouthClearance = 30f;    // u a course prism keeps outside every gate's rim

        /// <summary>The distance from <paramref name="p"/> to the segment a-b: what the tests measure clearance with.</summary>
        public static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return (p - (a + ab * t)).magnitude;
        }
    }
}
