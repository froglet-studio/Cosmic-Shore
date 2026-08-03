using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A display-only 3D model of a VESSEL: <see cref="ToyModelBuilder"/> (which does the
    /// prefab-asset mesh harvesting, preview material and fitting for every toy icon) plus the one
    /// thing that is vessel-specific - the <b>hull filter</b>.
    ///
    /// Only the hull is shown: the skimmer sphere (a builtin primitive scaled 15-60x), trails, jets
    /// and VFX are skipped - otherwise the giant skimmer sphere dominates the fit and crushes the
    /// real hull to an invisible speck (the bug where only Rhino - the one vessel whose skimmer has
    /// no builtin sphere - rendered). Inactive / disabled renderers are skipped by the shared
    /// builder (e.g. a hidden duplicate skinned mesh authored at a different scale).
    /// </summary>
    public static class VesselModelBuilder
    {
        // Builtin primitive mesh names - the skimmer bodies are huge builtin Spheres; drop them so
        // they don't pollute the bounds. Name-based so it's independent of the GameObject's name.
        static readonly HashSet<string> PrimitiveMeshNames = new()
        { "Sphere", "Cube", "Cylinder", "Capsule", "Plane", "Quad" };

        // Non-hull subsystems, matched anywhere up the chain to the prefab root (belt-and-suspenders
        // for a skimmer/vfx whose mesh isn't a builtin primitive).
        static readonly string[] NonHullNameHints =
        { "skimmer", "trail", "jet", "forcefield", "crackle", "pip", "vfx" };

        public static bool TryBuild(Transform prefabRoot, float targetRadius, Color previewColor, out GameObject model)
        {
            bool built = ToyModelBuilder.TryBuild(prefabRoot, targetRadius, previewColor, out model, IsHull);
            if (built && model) model.name = "VesselModel";
            return built;
        }

        /// <summary>As above, painted with a material the caller owns (see the ToyModelBuilder overload).</summary>
        public static bool TryBuild(Transform prefabRoot, float targetRadius, Material sharedMaterial, out GameObject model)
        {
            bool built = ToyModelBuilder.TryBuild(prefabRoot, targetRadius, sharedMaterial, out model, IsHull);
            if (built && model) model.name = "VesselModel";
            return built;
        }

        /// <summary>Whether this renderer is part of the ship hull we want to display.</summary>
        static bool IsHull(Transform prefabRoot, Transform node, Mesh mesh, Renderer renderer)
        {
            if (mesh && PrimitiveMeshNames.Contains(mesh.name)) return false;
            return !ToyModelBuilder.AnyAncestorNameContains(node, prefabRoot, NonHullNameHints);
        }
    }
}
