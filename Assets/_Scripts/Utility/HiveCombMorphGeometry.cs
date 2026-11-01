using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The pure rule behind the Honeyguide's omni-crystal morph (<c>R_VesselActions/HONEYGUIDE_CRYSTAL_MORPH.md</c>): which
    /// cells of a freshly laid hive comb the crystal's plates land on. Kept free of any scene object so the headless harness
    /// (<c>Tools/Build/crystal_morph_harness</c>) runs it.
    ///
    /// The omni cage is 64 panels, eight octahedra's worth (the panel census, <c>CrystalMorphMeshBuilder</c>), and a full
    /// comb is 37 cells. So the crystal breaks into the comb's HEART: the eight cells nearest its middle, the ones that
    /// grow first in the eye; the rest of the comb blooms in around them as it always did.
    /// </summary>
    public static class HiveCombMorphGeometry
    {
        /// <summary>Panels per octahedron, and octahedra the omni cage's 64 panels fill.</summary>
        public const int CellsCrystalFills = 8;

        /// <summary>
        /// The indices of the <paramref name="count"/> cells nearest the comb's centroid, nearest first; ties keep the lower
        /// index, so every peer that lists the same comb agrees. Empty when the comb has fewer cells than asked for: a
        /// partial census is no census.
        /// </summary>
        public static List<int> CentralCells(IReadOnlyList<Vector3> cells, int count)
        {
            var picked = new List<int>(count);
            if (cells == null || count <= 0 || cells.Count < count) return picked;

            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < cells.Count; i++) centroid += cells[i];
            centroid /= cells.Count;

            var order = new List<int>(cells.Count);
            for (int i = 0; i < cells.Count; i++) order.Add(i);
            order.Sort((a, b) =>
            {
                int c = (cells[a] - centroid).sqrMagnitude.CompareTo((cells[b] - centroid).sqrMagnitude);
                return c != 0 ? c : a.CompareTo(b);
            });
            for (int i = 0; i < count; i++) picked.Add(order[i]);
            return picked;
        }
    }
}
