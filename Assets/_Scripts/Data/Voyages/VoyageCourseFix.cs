using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// Where an Ark is on the leg it sails (Docs/Voyages/MILESTONE_2.md §5): the places it sails between, the route of
    /// location ids, the leg cell it is bound for (1-based; 0 before the first) of how many, and how far along the whole
    /// leg it is (0 at cast-off, 1 at the stop). The pause screen's map reads it; pure data.
    /// </summary>
    public struct VoyageCourseFix
    {
        public string From;
        public string To;
        public List<string> Route;
        public int Cell;
        public int Cells;
        public float Progress;

        /// <summary>The share of a <paramref name="cells"/>-cell leg sailed when bound for cell <paramref name="cell"/>
        /// (1-based) and <paramref name="within"/> (0-1) of the way from the last core to its core.</summary>
        public static float ProgressOf(int cell, int cells, float within)
        {
            if (cells <= 0) return 0f;
            float done = Math.Max(0, cell - 1) + Math.Max(0f, Math.Min(1f, within));
            return Math.Max(0f, Math.Min(1f, done / cells));
        }
    }
}
