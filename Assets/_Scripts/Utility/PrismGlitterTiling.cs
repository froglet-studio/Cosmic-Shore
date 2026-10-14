using System.Collections.Generic;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The GLITTER's fragments (Docs/PRISM_ANIMATION.md §4.11): every face of the prism tiled by right triangles
    /// of a RANGE of sizes, nested so a coarser detail level is the finer one with its small triangles merged back
    /// into the triangle they came from.
    ///
    /// <para>Each face is a <see cref="Grid"/>×<see cref="Grid"/> grid. It is cut into square blocks by a QUADTREE:
    /// blocks of <see cref="LargeLeg"/> cells, each of which may split into four blocks of half the size, each of
    /// which may split again, down to one cell. Every block is drawn as two right triangles across a diagonal the
    /// block's hash picks, so the fragments are triangles of three sizes (legs of 1/4, 1/8 and 1/16 of the face)
    /// and the cut lines do not all run one way.</para>
    ///
    /// <para><b>Nested, so a level never has holes.</b> <see cref="Blocks"/> with a minimum size stops splitting at
    /// that size: the LARGE level is the sixteen 4-blocks per face, MEDIUM the tree cut at 2, FULL the whole tree.
    /// Every level tiles every face completely — the first frame of any level is the whole prism — and seen from
    /// far away a coarser level is what the finer one looks like with the small fragments too small to tell apart.</para>
    ///
    /// <para>Pure and integer-only (no UnityEngine), so the Collision Burst Lab's JavaScript port is held to it
    /// block for block by <c>Tools/Build/prism_collision_burst_lab/parity.py</c>, which compiles this file with the
    /// .NET SDK. Change the hash or the odds and the lab's port must change with it.</para>
    /// </summary>
    public static class PrismGlitterTiling
    {
        /// <summary>Cells per face edge.</summary>
        public const int Grid = 16;

        /// <summary>The largest fragment's leg, in cells (a quarter of the face).</summary>
        public const int LargeLeg = 4;

        /// <summary>Percent chance a 4-block splits into four 2-blocks.</summary>
        public const int SplitLargePercent = 60;

        /// <summary>Percent chance a 2-block splits into four single cells.</summary>
        public const int SplitMediumPercent = 60;

        /// <summary>
        /// Appends the blocks of face <paramref name="face"/> (0–5) as (x, y, size, diagonal) quadruples in cells —
        /// diagonal 0 cuts from (x, y) to (x+size, y+size), 1 from (x+size, y) to (x, y+size) — splitting no block
        /// below <paramref name="minSize"/> cells (4: LARGE, 2: MEDIUM, 1: FULL).
        /// </summary>
        public static void Blocks(int face, int minSize, List<int> into)
        {
            for (int by = 0; by < Grid; by += LargeLeg)
            for (int bx = 0; bx < Grid; bx += LargeLeg)
                Emit(face, bx, by, LargeLeg, minSize, into);
        }

        /// <summary>Triangles in one prism at the level <paramref name="minSize"/> (two per block, six faces).</summary>
        public static int TriangleCount(int minSize)
        {
            var blocks = new List<int>(512);
            int count = 0;
            for (int f = 0; f < 6; f++)
            {
                blocks.Clear();
                Blocks(f, minSize, blocks);
                count += blocks.Count / 4 * 2;
            }
            return count;
        }

        static void Emit(int face, int x, int y, int size, int minSize, List<int> into)
        {
            int percent = size == LargeLeg ? SplitLargePercent : SplitMediumPercent;
            if (size > 1 && size > minSize && Hash(face, size, x, y, 1) % 100u < (uint)percent)
            {
                int half = size / 2;
                Emit(face, x, y, half, minSize, into);
                Emit(face, x + half, y, half, minSize, into);
                Emit(face, x, y + half, half, minSize, into);
                Emit(face, x + half, y + half, half, minSize, into);
                return;
            }
            into.Add(x);
            into.Add(y);
            into.Add(size);
            into.Add((int)(Hash(face, size, x, y, 2) & 1u));
        }

        /// <summary>
        /// The block's random: the lowbias32 integer hash of its face, size, corner and purpose — the same at
        /// every level, so a block makes the same choice whichever level asks.
        /// </summary>
        public static uint Hash(int face, int size, int x, int y, int salt)
        {
            uint h = unchecked((uint)(((face * 8 + size) * 64 + y) * 64 + x) * 2654435761u + (uint)salt * 40503u);
            h ^= h >> 16;
            h = unchecked(h * 0x7feb352du);
            h ^= h >> 15;
            h = unchecked(h * 0x846ca68bu);
            h ^= h >> 16;
            return h;
        }
    }
}
