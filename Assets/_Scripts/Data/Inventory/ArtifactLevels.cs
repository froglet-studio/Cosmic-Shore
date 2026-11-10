using System;

namespace CosmicShore.Data
{
    /// <summary>
    /// An artifact's four track levels, Mass, Space, Time and Charge, on the 0-10 band every element shares
    /// (Docs/Pilots/ARTIFACTS.md §4). Each payload transform reads its numbers from them (<c>ArtifactSO.Value</c>) and
    /// its twists from <see cref="Twisted"/>.
    ///
    /// <para><b>2026-11-07: levels are continuous.</b> A unique artifact (Docs/Pilots/INVENTORY.md §2) accepts ONE
    /// element without limit, with diminishing returns (<see cref="ArtifactCurve"/>), so its attuned track is a real
    /// number that creeps toward its ceiling; the other three sit at the levels it was rolled with. The struct moved here
    /// from <c>ArtifactSO.cs</c> so the pure inventory can compute it.</para>
    /// </summary>
    public readonly struct ArtifactLevels : IEquatable<ArtifactLevels>
    {
        /// <summary>The top of the band (an element's base band also runs 0-10).</summary>
        public const float MaxLevel = 10f;
        /// <summary>The level whose crossing adds a track's one qualitative twist.</summary>
        public const float TwistLevel = 5f;

        public readonly float Mass;
        public readonly float Space;
        public readonly float Time;
        public readonly float Charge;

        public ArtifactLevels(float mass, float space, float time, float charge)
        {
            Mass = Clamp(mass);
            Space = Clamp(space);
            Time = Clamp(time);
            Charge = Clamp(charge);
        }

        public float Of(Element element) => element switch
        {
            Element.Mass => Mass,
            Element.Space => Space,
            Element.Time => Time,
            Element.Charge => Charge,
            _ => 0f,
        };

        /// <summary>These levels with <paramref name="element"/>'s replaced.</summary>
        public ArtifactLevels With(Element element, float level) => element switch
        {
            Element.Mass => new ArtifactLevels(level, Space, Time, Charge),
            Element.Space => new ArtifactLevels(Mass, level, Time, Charge),
            Element.Time => new ArtifactLevels(Mass, Space, level, Charge),
            Element.Charge => new ArtifactLevels(Mass, Space, Time, level),
            _ => this,
        };

        /// <summary>True once <paramref name="element"/>'s track has reached its twist.</summary>
        public bool Twisted(Element element) => Of(element) >= TwistLevel;

        /// <summary>The four elements a track can belong to, in the order tracks are listed.</summary>
        public static readonly Element[] TrackElements = { Element.Mass, Element.Space, Element.Time, Element.Charge };

        public static bool IsTrackElement(Element e) => e is Element.Mass or Element.Space or Element.Time or Element.Charge;

        static float Clamp(float v) => float.IsNaN(v) ? 0f : v < 0f ? 0f : v > MaxLevel ? MaxLevel : v;

        public bool Equals(ArtifactLevels o) => Mass == o.Mass && Space == o.Space && Time == o.Time && Charge == o.Charge;
        public override bool Equals(object obj) => obj is ArtifactLevels o && Equals(o);
        public override int GetHashCode() => HashCode.Combine(Mass, Space, Time, Charge);
        public override string ToString() => $"M{Mass:0.00} S{Space:0.00} T{Time:0.00} C{Charge:0.00}";
    }
}
