using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Hedgerow's field, kept pure so the edit-mode tests and the generator's twin can hold it without a scene.
    ///
    /// <para><b>A field is HEDGES of neutral prisms</b> laid by the mode on every peer from the intensity alone (no seed,
    /// no random draw). A hedge is a straight row of <see cref="Rung.RowLength"/> prisms, <see cref="Spacing"/> apart,
    /// <see cref="Rung.Ranks"/> rows high, tangent to a sphere a band of the way from the nucleus to the membrane, its
    /// midpoint on a golden-angle spiral. Every prism is the Serpent trail's own brick (<see cref="BrickScale"/>), so a
    /// wall that eats one credits exactly what a trail prism is worth.</para>
    ///
    /// <para><b>Why rows.</b> A Serpent's seed wall grows in the plane across its heading and steals only from its EDGE
    /// (SERPENT_SEED_WALL.md, "Growth"), so it creeps along a line of loose mass it is eating. A hedge is that line: a
    /// Serpent that stops just short of one, flying radially out (across the row), grows a wall that eats its way along
    /// the hedge. The row direction is the hedge's <see cref="Piece.Rotation"/> up, and the way to come at it is its
    /// <see cref="Approach"/> (outward from the core).</para>
    /// </summary>
    public static class HedgerowField
    {
        /// <summary>The Serpent trail's brick (Serpent.prefab VesselPrismController.BaseScale) - every hedge prism.</summary>
        public static readonly Vector3 BrickScale = new(3f, 6f, 0.5f);

        /// <summary>Centre-to-centre distance between neighbouring hedge prisms, along the row and between ranks, u.</summary>
        public const float Spacing = 10f;

        /// <summary>Hedges keep this far outside the nucleus, so the run-up from the core side clears it.</summary>
        public const float InnerClearance = 200f;

        /// <summary>Hedges keep this far inside the membrane.</summary>
        public const float OuterClearance = 100f;

        /// <summary>One rung of the field.</summary>
        public readonly struct Rung
        {
            public readonly int RowCount;
            public readonly int RowLength;
            public readonly int Ranks;
            public readonly float BandFraction;

            public Rung(int rowCount, int rowLength, int ranks, float bandFraction)
            {
                RowCount = rowCount;
                RowLength = rowLength;
                Ranks = ranks;
                BandFraction = bandFraction;
            }
        }

        /// <summary>I1 Hedgerow, I2 Bocage, I3 Thicket, I4 Labyrinth (HEDGEROW.md). The generator mirrors this table.</summary>
        public static Rung ForIntensity(int intensity) => Mathf.Clamp(intensity, 1, 4) switch
        {
            1 => new Rung(8, 32, 2, 0.30f),
            2 => new Rung(12, 32, 2, 0.45f),
            3 => new Rung(12, 40, 3, 0.45f),
            _ => new Rung(16, 40, 3, 0.60f),
        };

        /// <summary>Every prism the rung lays.</summary>
        public static int TotalPrisms(Rung rung) => rung.RowCount * rung.RowLength * rung.Ranks;

        /// <summary>The radius every hedge's midpoint sits at, from the cell's centre.</summary>
        public static float RowRadius(Rung rung, float nucleusRadius, float membraneRadius)
        {
            float inner = nucleusRadius + InnerClearance;
            float outer = Mathf.Max(inner, membraneRadius - OuterClearance);
            return Mathf.Lerp(inner, outer, Mathf.Clamp01(rung.BandFraction));
        }

        /// <summary>The way to come at hedge <paramref name="row"/>: out from the core (unit). A wall seeded flying
        /// this way stands across it, in the plane the hedge lies in.</summary>
        public static Vector3 Approach(Rung rung, int row) => ChumReef.SpiralDirection(row, rung.RowCount);

        /// <summary>Hedge <paramref name="row"/>'s midpoint, relative to the cell's centre.</summary>
        public static Vector3 RowMidpoint(Rung rung, int row, float nucleusRadius, float membraneRadius) =>
            Approach(rung, row) * RowRadius(rung, nucleusRadius, membraneRadius);

        /// <summary>The way hedge <paramref name="row"/> runs (unit, tangent to the sphere): a reference tangent turned
        /// about the approach by the golden angle per row, so neighbouring hedges do not run parallel.</summary>
        public static Vector3 RowDirection(Rung rung, int row)
        {
            const float golden = 2.39996323f;
            Vector3 n = Approach(rung, row);
            Vector3 axis = Mathf.Abs(n.y) > 0.9f ? Vector3.right : Vector3.up;
            Vector3 t = Vector3.Cross(n, axis).normalized;
            Vector3 b = Vector3.Cross(n, t);
            float a = row * golden;
            return (t * Mathf.Cos(a) + b * Mathf.Sin(a)).normalized;
        }

        /// <summary>One prism to lay, relative to the cell's centre.</summary>
        public readonly struct Piece
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly int Row;

            public Piece(Vector3 position, Quaternion rotation, int row)
            {
                Position = position;
                Rotation = rotation;
                Row = row;
            }
        }

        /// <summary>Every prism of the rung, hedge by hedge (cleared first). Pure: the same list on every peer. A
        /// brick's long side runs along the hedge and its flat face looks back at the core.</summary>
        public static void Lay(Rung rung, float nucleusRadius, float membraneRadius, List<Piece> into)
        {
            into.Clear();
            for (int r = 0; r < rung.RowCount; r++)
            {
                Vector3 mid = RowMidpoint(rung, r, nucleusRadius, membraneRadius);
                Vector3 n = Approach(rung, r);
                Vector3 along = RowDirection(rung, r);
                Vector3 up = Vector3.Cross(n, along);   // the hedge's height, in the wall's plane
                var rotation = Quaternion.LookRotation(n, along);
                float halfLength = (rung.RowLength - 1) * 0.5f;
                float halfRanks = (rung.Ranks - 1) * 0.5f;
                for (int k = 0; k < rung.RowLength; k++)
                for (int h = 0; h < rung.Ranks; h++)
                {
                    Vector3 at = mid + along * ((k - halfLength) * Spacing) + up * ((h - halfRanks) * Spacing);
                    into.Add(new Piece(at, rotation, r));
                }
            }
        }

        /// <summary>The farthest any hedge prism sits from the cell's centre - the generator keeps it inside the
        /// membrane.</summary>
        public static float OuterReach(Rung rung, float nucleusRadius, float membraneRadius)
        {
            float r = RowRadius(rung, nucleusRadius, membraneRadius);
            float half = (rung.RowLength - 1) * 0.5f * Spacing;
            float height = (rung.Ranks - 1) * 0.5f * Spacing;
            return Mathf.Sqrt(r * r + half * half + height * height);
        }
    }
}
