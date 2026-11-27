using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>One lance prism: where it stands, how it is turned (forward and up, as <c>Quaternion.LookRotation</c>
    /// takes them), its size (long along forward, its scale's z), its kind and its colour.</summary>
    public readonly struct CleaveLancePart
    {
        public readonly Vector3 Position, Forward, Up, Scale;
        public readonly PrismKind Kind;
        public readonly Domains Domain;
        /// <summary>Which lance (0..<see cref="CleaveLances.Directions"/>-1) and which segment along it.</summary>
        public readonly int Lance, Segment;

        public CleaveLancePart(Vector3 position, Vector3 forward, Vector3 up, Vector3 scale, PrismKind kind,
            Domains domain, int lance, int segment)
        {
            Position = position; Forward = forward; Up = up; Scale = scale;
            Kind = kind; Domain = domain; Lance = lance; Segment = segment;
        }
    }

    /// <summary>One rung's lances (CLEAVE.md "Lances").</summary>
    public readonly struct CleaveLanceRung
    {
        /// <summary>Where a lance starts and ends, as fractions of the rung's arena radius.</summary>
        public readonly float InnerFraction, OuterFraction;
        /// <summary>The bar's section in prisms: 2 is a 2x2 bar, 3 a 3x3 bundle.</summary>
        public readonly int Bundle;
        /// <summary>Segments at the OUTER end laid as danger (0: none): overshoot the end at ramp speed and pay.</summary>
        public readonly int DangerTips;
        /// <summary>0 none, 1 every segment super-shielded, 2 every other segment (a seam).</summary>
        public readonly int Armour;

        public CleaveLanceRung(float inner, float outer, int bundle, int dangerTips, int armour)
        {
            InnerFraction = inner; OuterFraction = outer; Bundle = bundle; DangerTips = dangerTips; Armour = armour;
        }
    }

    /// <summary>
    /// Cleave's lances: straight bars of small prisms along the six axis directions through the arena, one set per
    /// rung (Yash, 2026-11-25: every piece of the environment gives the Swordfish an advantage or a cost, easier at I1
    /// and harder at I4). A lance is a straight line through mass, the one place the Swordfish's ramp pays: hold the
    /// line, the slices refuel the ramp and the ramp runs to 16.8x. The rungs differ in what the line costs:
    ///
    /// <list type="bullet">
    /// <item><b>I1 Panes</b>: plain 2x2 bars, 0.12-0.95 of the arena radius. Help only: line up and commit.</item>
    /// <item><b>I2 Swell</b>: plain 3x3 bundles with danger at the outer end. The wide bundle is what the Space 5 bill (every
    /// hull starts there) cuts more of in one pass; overshooting the outer end at ramp speed hits danger.</item>
    /// <item><b>I3 Cage</b>: short super-shielded bars in the cage's heart (0.1-0.6, inside the innermost rind, with
    /// the team Space crystals). Shield Breaker: an energized blade at Space 5 pops a run of them; a cold one binds.</item>
    /// <item><b>I4 Twistbands</b>: 2x2 bars from 0.15 to 0.98, plain and super-shielded segments alternating (a seam),
    /// danger at the outer end.</item>
    /// </list>
    ///
    /// <para>Pure: a function of the rung and the arena centre, so every peer computes the same parts. Prisms are
    /// flush (a segment is exactly its step long, a bar's prisms exactly one section apart), and the controller skips
    /// any part whose spot the arena already fills, so no lance prism passes through an arena prism. Colours cycle
    /// Jade, Ruby, Gold, Blue by segment, so three quarters of every lance scores for any team. Nothing here touches
    /// the vessel.</para>
    /// </summary>
    public static class CleaveLances
    {
        /// <summary>The six axis directions: one lance each.</summary>
        public static readonly Vector3[] Directions =
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back,
        };

        /// <summary>A lance prism's section (the arenas' plank section, 6.8) and its length along the lance.</summary>
        public const float Section = 6.8f, SegmentLength = 30f;

        /// <summary>The colours segments cycle through. Blue is no team's, so it scores for everyone.</summary>
        public static readonly Domains[] SegmentDomains = { Domains.Jade, Domains.Ruby, Domains.Gold, Domains.Blue };

        public static CleaveLanceRung ForIntensity(int intensity) => intensity switch
        {
            <= 1 => new CleaveLanceRung(0.12f, 0.95f, 2, 0, 0),
            2 => new CleaveLanceRung(0.12f, 0.95f, 3, 2, 0),
            3 => new CleaveLanceRung(0.10f, 0.60f, 2, 0, 1),
            _ => new CleaveLanceRung(0.15f, 0.98f, 2, 2, 2),
        };

        /// <summary>Segments along one lance on this rung.</summary>
        public static int SegmentsFor(int intensity)
        {
            var rung = ForIntensity(intensity);
            float length = SliceArenaGeometry.OuterRadiusFor(intensity) * (rung.OuterFraction - rung.InnerFraction);
            return Mathf.Max(1, Mathf.FloorToInt(length / SegmentLength));
        }

        /// <summary>Every lance prism on this rung, before the controller drops the ones the arena already fills.</summary>
        public static int PartCountFor(int intensity)
        {
            int b = ForIntensity(intensity).Bundle;
            return Directions.Length * SegmentsFor(intensity) * b * b;
        }

        /// <summary>The kind of segment <paramref name="segment"/> of <paramref name="segments"/>.</summary>
        public static PrismKind KindOf(CleaveLanceRung rung, int segment, int segments)
        {
            // Outer end only: the inner ends sit near the centre, where every chord through the arena passes, so a
            // tip there punished crossing the middle rather than overshooting a lance (I2 ran 408 -> 580 s for the AI).
            if (rung.DangerTips > 0 && segment >= segments - rung.DangerTips)
                return PrismKind.Danger;
            return rung.Armour switch
            {
                1 => PrismKind.SuperShielded,
                2 => segment % 2 == 1 ? PrismKind.SuperShielded : PrismKind.Plain,
                _ => PrismKind.Plain,
            };
        }

        /// <summary>Appends every lance prism of <paramref name="intensity"/> around <paramref name="centre"/>.</summary>
        public static void Lay(int intensity, Vector3 centre, List<CleaveLancePart> into)
        {
            if (into == null) return;
            var rung = ForIntensity(intensity);
            float arena = SliceArenaGeometry.OuterRadiusFor(intensity);
            float start = arena * rung.InnerFraction;
            int segments = SegmentsFor(intensity);
            int b = rung.Bundle;
            var scale = new Vector3(Section, Section, SegmentLength);

            for (int l = 0; l < Directions.Length; l++)
            {
                Vector3 forward = Directions[l];
                // Any fixed perpendicular pair: the bar's cross-section is square.
                Vector3 up = Mathf.Abs(forward.y) > 0.5f ? Vector3.forward : Vector3.up;
                Vector3 side = Vector3.Cross(up, forward).normalized;
                for (int s = 0; s < segments; s++)
                {
                    float along = start + (s + 0.5f) * SegmentLength;
                    var kind = KindOf(rung, s, segments);
                    var domain = SegmentDomains[(s + l) % SegmentDomains.Length];
                    for (int i = 0; i < b; i++)
                    for (int j = 0; j < b; j++)
                    {
                        // Prisms exactly one section apart across the bar: flush, never overlapping.
                        float u = (i - (b - 1) * 0.5f) * Section;
                        float v = (j - (b - 1) * 0.5f) * Section;
                        Vector3 position = centre + forward * along + side * u + up * v;
                        into.Add(new CleaveLancePart(position, forward, up, scale, kind, domain, l, s));
                    }
                }
            }
        }

        /// <summary>The gap (u) a lance prism's bounding sphere keeps from any standing prism's.</summary>
        public const float ArenaMargin = 1f;

        /// <summary>A lance prism's bounding sphere: half its diagonal.</summary>
        public static readonly float PartRadius = new Vector3(Section, Section, SegmentLength).magnitude * 0.5f;
    }
}
