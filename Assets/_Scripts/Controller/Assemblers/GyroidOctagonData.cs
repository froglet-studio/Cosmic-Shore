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
        /// <summary>Octagon ring radius in world units at the shipped separationDistance (3).</summary>
        public const float RingRadius = 10.03f;

        /// <summary>Closest spacing between adjacent octagon centres (measured min 35.87).</summary>
        public const float CenterSpacing = 35.87f;

        /// <summary>
        /// Two centre claims closer than this are the same octagon. Half the min spacing, with
        /// margin for the bond table's small float drift (~0.3u per 100u of lattice).
        /// </summary>
        public const float CenterDedupeRadius = 12f;

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
                    new OctagonNeighbor(new Vector3(26.7364f, -0.1734f, -4.7729f), new Vector3(17.0325f, -0.6394f, -3.3230f), new Quaternion(0.706593f, -0.020536f, 0.706980f, -0.024420f), GyroidBlockType.EG),
                    new OctagonNeighbor(new Vector3(-13.7387f, 22.9612f, 26.4111f), new Vector3(-13.9767f, 17.0561f, 17.8210f), new Quaternion(-0.223970f, 0.529710f, -0.302267f, 0.760081f), GyroidBlockType.EsD),
                    new OctagonNeighbor(new Vector3(-16.2932f, -36.1611f, -5.2242f), new Vector3(-11.2954f, -26.6316f, -4.7480f), new Quaternion(-0.061706f, -0.235717f, 0.826063f, 0.508281f), GyroidBlockType.EsD),
                    new OctagonNeighbor(new Vector3(-36.2270f, 12.7841f, -23.9740f), new Vector3(-27.7069f, 8.4148f, -19.5136f), new Quaternion(0.653435f, 0.253352f, -0.302862f, 0.645774f), GyroidBlockType.EG),
                } },
            { GyroidBlockType.EsD, new[]
                {
                    new OctagonNeighbor(new Vector3(-13.7376f, 22.9599f, -26.4102f), new Vector3(-13.9756f, 17.0549f, -17.8202f), new Quaternion(0.223963f, 0.529712f, -0.302265f, -0.760082f), GyroidBlockType.EG),
                    new OctagonNeighbor(new Vector3(26.7343f, -0.1723f, 4.7716f), new Vector3(17.0304f, -0.6383f, 3.3218f), new Quaternion(-0.706594f, -0.020536f, 0.706978f, 0.024419f), GyroidBlockType.EsD),
                    new OctagonNeighbor(new Vector3(-16.2921f, -36.1663f, 5.2253f), new Vector3(-11.2942f, -26.6363f, 4.7489f), new Quaternion(0.061707f, -0.235716f, 0.826064f, -0.508280f), GyroidBlockType.EG),
                    new OctagonNeighbor(new Vector3(-36.2325f, 12.7846f, 23.9776f), new Vector3(-27.7124f, 8.4152f, 19.5171f), new Quaternion(-0.653436f, 0.253352f, -0.302862f, -0.645773f), GyroidBlockType.EsD),
                } },
            { GyroidBlockType.GEs, new[]
                {
                    new OctagonNeighbor(new Vector3(-42.8776f, -13.0721f, 5.5350f), new Vector3(-33.1783f, -12.0606f, 3.0567f), new Quaternion(-0.691788f, 0.019690f, 0.185680f, -0.697541f), GyroidBlockType.EsD),
                    new OctagonNeighbor(new Vector3(-17.5474f, 17.4485f, -34.1218f), new Vector3(-18.0281f, 12.9576f, -25.1240f), new Quaternion(0.150401f, 0.627901f, -0.190090f, -0.739585f), GyroidBlockType.EG),
                    new OctagonNeighbor(new Vector3(10.7357f, -29.5559f, 4.5064f), new Vector3(6.5551f, -20.3970f, 4.6006f), new Quaternion(0.122622f, -0.350533f, 0.796572f, -0.477037f), GyroidBlockType.EG),
                    new OctagonNeighbor(new Vector3(11.2337f, 25.8383f, 13.2937f), new Vector3(6.1809f, 20.1719f, 6.6923f), new Quaternion(-0.196263f, 0.193196f, 0.860423f, 0.428752f), GyroidBlockType.EsD),
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
