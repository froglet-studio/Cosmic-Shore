using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One neighbouring octagon, expressed in the local frame of a danger prism on THIS
    /// octagon's ring: where the neighbour's centre is, and the full pose + block type of one
    /// member of the neighbour's ring (its member nearest to the frame prism) - everything a
    /// parent colony needs to plant a daughter whose lattice continues its own.
    /// </summary>
    public readonly struct OctagonNeighbor
    {
        /// <summary>The neighbouring octagon's centre, in the frame prism's local space.</summary>
        public readonly Vector3 Center;

        /// <summary>Local position of the seed prism - a real ring member of the neighbour.</summary>
        public readonly Vector3 SeedPosition;

        /// <summary>Local rotation of that seed prism.</summary>
        public readonly Quaternion SeedRotation;

        /// <summary>The block type that ring site carries (always one of the danger types).</summary>
        public readonly GyroidBlockType SeedType;

        public OctagonNeighbor(Vector3 center, Vector3 seedPosition, Quaternion seedRotation, GyroidBlockType seedType)
        {
            Center = center;
            SeedPosition = seedPosition;
            SeedRotation = seedRotation;
            SeedType = seedType;
        }
    }

    /// <summary>
    /// The gyroid's octagon geometry, MEASURED off <see cref="GyroidBondMateDataContainer"/>'s own
    /// bond table - not designed by hand (Docs/ECOSYSTEM.md §32.2, regeneration:
    /// <c>Tools/Build/measure_gyroid_octagons.py</c>).
    ///
    /// <para>The facts these tables encode: the four danger block types (DE, EG, EsD, GEs - two
    /// mirror pairs) form closed rings of exactly EIGHT danger prisms and nothing else (the
    /// danger-only bond subgraph contains ONLY 8-cycles), radius ~10u at separation 3. Danger
    /// types are 4 of 12 equidistributed types, so each octagon owns <b>24 prisms</b> of the
    /// surface (8 ring + 16 between). Adjacent octagon centres sit 35.9-42.4u apart. Each danger
    /// type sees exactly FOUR neighbouring octagons at fixed local offsets, each with one
    /// deterministic seed pose (measured seed-type purity 1.00, position std ≤ 0.25u).</para>
    ///
    /// <para>That is the whole mechanism of the octagon colony: a danger prism knows its own
    /// octagon's centre (<see cref="TryGetOwnCenterOffset"/>), and a completed colony knows where
    /// every neighbouring octagon's centre and one of its ring prisms are
    /// (<see cref="TryGetNeighbors"/>) - so reproduction is a table lookup, and the
    /// superstructure emerges from each plant continuing the lattice its parent handed it.</para>
    /// </summary>
    public static class GyroidOctagonData
    {
        /// <summary>
        /// The separationDistance every distance in this file was MEASURED at. A plant whose
        /// element authors a different one (FloraVariantTuning.SeparationDistance) scales the
        /// whole lattice, so every length here scales with it - see
        /// <c>AssembledFlora.LatticeScale</c>, which is the single place that ratio is formed.
        /// Rotations do NOT scale and are used unchanged.
        /// </summary>
        public const float MeasuredSeparation = 3f;

        /// <summary>Octagon ring radius in world units at <see cref="MeasuredSeparation"/>.</summary>
        public const float RingRadius = 10.03f;

        /// <summary>Closest spacing between adjacent octagon centres (measured min 35.87).</summary>
        public const float CenterSpacing = 35.87f;

        /// <summary>
        /// Two centre claims closer than this are the same octagon. Half the min spacing, with
        /// margin for the bond table's small float drift (~0.3u per 100u of lattice).
        /// </summary>
        public const float CenterDedupeRadius = 12f;

        /// <summary>
        /// How close a danger prism's SELF-COMPUTED centre must land to a plant's claimed centre
        /// for that prism to join the plant's ring. A genuine ring member computes its centre to
        /// well under 1u (measured founder coherence 0.08); a danger prism from a MISALIGNED
        /// lattice frame (an independent founder, a toy planting) can land anywhere up to the
        /// dedupe radius. The fifth Lab playtest recorded an 11.79u admission - a foreign-frame
        /// prism poisoning a ring, whose pose reproduction then projected the neighbour tables
        /// from, planting daughters in the foreign frame. Membership is a COHERENCE test, and
        /// must be far tighter than claim identity (CenterDedupeRadius).
        /// </summary>
        public const float RingMemberToleranceRadius = 2.5f;

        /// <summary>
        /// The farthest any prism of an octagon's 24-prism patch sits from the octagon centre
        /// (measured max 25.84). The ownership gate refuses growth beyond this.
        /// </summary>
        public const float TerritoryRadius = 26.5f;

        /// <summary>
        /// Slack on the "my centre is nearest" ownership test. Boundary prisms sit EXACTLY
        /// equidistant between two centres (measured margin min 0.00), so a strict test would
        /// orphan them; with slack, both owners may try and the spatial-index reservation
        /// dedupes. First to grow wins the boundary prism - patches measure 22-28.
        /// </summary>
        public const float OwnershipEpsilon = 0.75f;

        /// <summary>Average prisms per octagon patch: 8 danger ÷ (4 of 12 types) = 24 exactly.</summary>
        public const int PatchPrisms = 24;

        // OWN octagon centre, in the danger prism's local frame (|v| = the ring radius, ~10u).
        static readonly Dictionary<GyroidBlockType, Vector3> OwnCenter = new()
        {
            { GyroidBlockType.DE, new Vector3(-9.6171f, 0.1648f, 2.7058f) },
            { GyroidBlockType.EG, new Vector3(-9.8818f, -0.1409f, -1.8924f) },
            { GyroidBlockType.EsD, new Vector3(-9.8867f, -0.1522f, 1.8957f) },
            { GyroidBlockType.GEs, new Vector3(-9.6143f, 0.1610f, -2.6946f) },
        };

        // Every row below is a MEASUREMENT pasted verbatim from the emit of
        // Tools/Build/measure_gyroid_octagons.py - never a symmetry construction. The first
        // shipped version filled the EG/EsD/GEs rows from the DE/EG measurements by a z-mirror
        // ansatz (centres z-negated, quaternions (x,w)-negated); the gyroid's enantiomer
        // conjugation does NOT act on the LookRotation frames that way, so 12 of 16 seed
        // rotations were wrong by up to 179 deg - daughters seeded through those rows grew
        // internally-perfect lattices that could not mate with their parents (the "twinning"
        // playtests). The regeneration script now asserts self-centre coherence on the values
        // it prints; do not hand-derive any row from another.
        static readonly Dictionary<GyroidBlockType, OctagonNeighbor[]> Neighbors = new()
        {
            { GyroidBlockType.DE, new[]
                {
                    new OctagonNeighbor(new Vector3(11.2409f, 25.8356f, -13.2941f), new Vector3(6.1877f, 20.1696f, -6.6923f), new Quaternion(0.196263f, 0.193195f, 0.860423f, -0.428753f), GyroidBlockType.EG),
                    new OctagonNeighbor(new Vector3(-17.5437f, 17.4509f, 34.1344f), new Vector3(-18.0243f, 12.9599f, 25.1361f), new Quaternion(-0.150400f, 0.627903f, -0.190088f, 0.739584f), GyroidBlockType.EsD),
                    new OctagonNeighbor(new Vector3(10.7376f, -29.5544f, -4.5000f), new Vector3(6.5569f, -20.3957f, -4.5946f), new Quaternion(-0.122621f, -0.350533f, 0.796572f, 0.477037f), GyroidBlockType.EsD),
                    new OctagonNeighbor(new Vector3(-42.8734f, -13.0707f, -5.5211f), new Vector3(-33.1743f, -12.0594f, -3.0460f), new Quaternion(0.691788f, 0.019690f, 0.185681f, 0.697541f), GyroidBlockType.EG),
                } },
            { GyroidBlockType.EG, new[]
                {
                    new OctagonNeighbor(new Vector3(26.7410f, -0.1713f, -4.7677f), new Vector3(17.2671f, 0.7072f, -1.4970f), new Quaternion(0.076997f, 0.191470f, 0.978474f, -0.000000f), GyroidBlockType.EG),
                    new OctagonNeighbor(new Vector3(-13.7433f, 22.9609f, 26.4131f), new Vector3(-13.9584f, 18.6030f, 17.3395f), new Quaternion(0.147854f, 0.465907f, -0.500247f, 0.714719f), GyroidBlockType.EsD),
                    new OctagonNeighbor(new Vector3(-16.2897f, -36.1594f, -5.2223f), new Vector3(-12.2222f, -26.9543f, -5.5216f), new Quaternion(-0.418360f, -0.356812f, 0.500254f, 0.668884f), GyroidBlockType.EsD),
                    new OctagonNeighbor(new Vector3(-36.2347f, 12.7814f, -23.9665f), new Vector3(-30.6069f, 7.0722f, -17.8873f), new Quaternion(0.581656f, -0.400125f, 0.032563f, 0.707471f), GyroidBlockType.EG),
                } },
            { GyroidBlockType.EsD, new[]
                {
                    new OctagonNeighbor(new Vector3(-13.7432f, 22.9573f, -26.4120f), new Vector3(-13.9655f, 18.5955f, -17.3485f), new Quaternion(-0.147793f, -0.465886f, -0.500247f, 0.714745f), GyroidBlockType.EG),
                    new OctagonNeighbor(new Vector3(26.7334f, -0.1743f, 4.7722f), new Vector3(17.2537f, 0.6961f, 1.4939f), new Quaternion(-0.077000f, -0.191444f, 0.978478f, 0.000000f), GyroidBlockType.EsD),
                    new OctagonNeighbor(new Vector3(-16.2929f, -36.1669f, 5.2340f), new Vector3(-12.2262f, -26.9690f, 5.5219f), new Quaternion(0.418440f, 0.356924f, 0.500193f, 0.668820f), GyroidBlockType.EG),
                    new OctagonNeighbor(new Vector3(-36.2314f, 12.7834f, 23.9777f), new Vector3(-30.6086f, 7.0690f, 17.8867f), new Quaternion(-0.581617f, 0.400266f, 0.032410f, 0.707431f), GyroidBlockType.EsD),
                } },
            { GyroidBlockType.GEs, new[]
                {
                    new OctagonNeighbor(new Vector3(-42.8756f, -13.0708f, 5.5427f), new Vector3(-33.1720f, -12.0618f, 3.0550f), new Quaternion(-0.691745f, -0.019610f, 0.185685f, 0.697586f), GyroidBlockType.EsD),
                    new OctagonNeighbor(new Vector3(-17.5542f, 17.4525f, -34.1210f), new Vector3(-18.0296f, 12.9556f, -25.1335f), new Quaternion(0.150457f, -0.627775f, -0.189999f, 0.739704f), GyroidBlockType.EG),
                    new OctagonNeighbor(new Vector3(10.7408f, -29.5572f, 4.5150f), new Vector3(6.5676f, -20.4031f, 4.6011f), new Quaternion(0.122638f, 0.350629f, 0.796571f, 0.476963f), GyroidBlockType.EG),
                    new OctagonNeighbor(new Vector3(11.2342f, 25.8373f, 13.2938f), new Vector3(6.1866f, 20.1613f, 6.6851f), new Quaternion(-0.196213f, -0.193167f, 0.860404f, -0.428825f), GyroidBlockType.EsD),
                } },
        };

        /// <summary>
        /// The centre of the octagon this danger prism belongs to, in ITS local frame. False for
        /// a non-danger type - only ring prisms know their octagon exactly.
        /// </summary>
        public static bool TryGetOwnCenterOffset(GyroidBlockType type, out Vector3 localOffset) =>
            OwnCenter.TryGetValue(type, out localOffset);

        /// <summary>The four neighbouring octagons visible from a ring prism of this type.</summary>
        public static bool TryGetNeighbors(GyroidBlockType type, out OctagonNeighbor[] neighbors) =>
            Neighbors.TryGetValue(type, out neighbors);
    }
}
