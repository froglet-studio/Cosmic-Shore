using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// An icon for a FLORA species. Flora have no art model - a species IS its growth rule - so
    /// there is nothing to harvest the way <see cref="ToyModelBuilder"/> harvests a hull or a
    /// creature. Instead this asks the species to <b>run its own growth pattern in the abstract</b>
    /// (<see cref="Flora.TryPreviewGrowth"/>: no prism, no spindle, no GameObject, no cell) and
    /// renders the poses it reports.
    ///
    /// The render half is the toybox's existing icon pipeline, unchanged: the poses become
    /// <see cref="PrismLay"/>s, <see cref="CellMiniatureBuilder.BuildFromLays"/> assembles one mesh
    /// with a submesh per domain, and <see cref="ToyFactory.AddMiniatureBody"/> paints it in the
    /// real domain prism materials. So a flora icon is made of the same stuff, and reads in the
    /// same visual language, as a mini-cell or a microscene.
    ///
    /// The caller OWNS the returned mesh (each icon builds its own) and must destroy it.
    /// </summary>
    public static class FloraIconBuilder
    {
        /// <summary>Prisms simulated per icon. The silhouette is what reads at icon size.</summary>
        const int DefaultPrismBudget = 220;

        public static bool TryBuild(Flora prefab, float radius, ToyContext context, Domains domain,
            out GameObject icon, out Mesh mesh, int prismBudget = DefaultPrismBudget, int seed = 12345)
        {
            icon = null;
            mesh = null;
            if (!prefab || radius <= 0f) return false;

            var poses = new List<SpawnPoint>(prismBudget);
            if (!prefab.TryPreviewGrowth(prismBudget, seed, poses) || poses.Count == 0) return false;

            var lays = new List<PrismLay>(poses.Count);
            foreach (var pose in poses)
                lays.Add(new PrismLay(pose, domain));

            // Coverage 1: a flora IS its branching, and the signature filter that helps a 34k-prism
            // world read at thumbnail size would eat the thin structure that makes this legible.
            var miniature = CellMiniatureBuilder.BuildFromLays(lays, radius, prismBudget, 1f,
                $"FloraIcon_{prefab.name}");
            if (!miniature.IsValid) return false;

            icon = ToyFactory.AddMiniatureBody(null, miniature, context, $"Flora_{prefab.name}");
            if (!icon) return false;

            mesh = miniature.Mesh;
            return true;
        }
    }
}
