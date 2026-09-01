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

        /// <summary>
        /// A mini hull wearing the ship's OWN materials — the actual model, not a silhouette of it.
        ///
        /// <para>The flat preview fill existed because a vessel's real materials are dark unlit
        /// theme shaders that read as a black blob with nothing to say which team they belong to.
        /// The vessel vision band answers both halves of that now (Docs/VESSEL_VISION.md): a
        /// station wears its domain in a flat cel silhouette while you are choosing at range, and
        /// resolves into the real hull as you close on it. So the fill can go, and a station can
        /// show the ship.</para>
        ///
        /// <para><paramref name="domainMaterial"/> replaces the prefab's authored DOMAIN-role
        /// slots, resolved from the prefab's own <see cref="VesselCustomization"/> exactly the way
        /// the live ship resolves them. Without it a mini hull would wear the jade placeholder the
        /// prefab is authored with — the vessel's Body and Window roles are domain-agnostic, but
        /// the accent is a stand-in, not a colour choice, and a Ruby pilot previewing jade accents
        /// is the flat fill's job coming back undone. Pass null to keep the authored materials.</para>
        /// </summary>
        public static bool TryBuildLive(Transform prefabRoot, float targetRadius, Material domainMaterial,
            out GameObject model)
        {
            var custom = prefabRoot ? prefabRoot.GetComponent<VesselCustomization>() : null;

            bool built = ToyModelBuilder.TryBuild(
                prefabRoot, targetRadius, sharedMaterial: null, out model, IsHull,
                (node, source, authored) => ResolveMaterials(authored, custom, domainMaterial));

            if (built && model)
            {
                model.name = "VesselModel";
                model.AddComponent<ToyLiveHull>();
            }
            return built;
        }

        /// <summary>
        /// The source renderer's own materials, with the domain-role slots swapped for
        /// <paramref name="domainMaterial"/>.
        ///
        /// Mirrors <see cref="VesselCustomization.ApplyShipMaterial"/>'s two modes: when the vessel
        /// names the materials the domain REPLACES, every slot wearing one is swapped whatever
        /// index it sits at; otherwise the platform's default slot index is used. Reproducing the
        /// rule rather than inventing one keeps a mini hull painted the same way its ship is — and
        /// a vessel that changes which slot carries its domain changes both at once.
        /// </summary>
        static Material[] ResolveMaterials(Material[] authored, VesselCustomization custom,
            Material domainMaterial)
        {
            if (authored == null || authored.Length == 0) return authored;
            if (!domainMaterial || custom == null) return authored;

            var identities = custom.DomainReplacesMaterials;
            bool byIdentity = false;
            if (identities != null)
                for (int i = 0; i < identities.Count && !byIdentity; i++)
                    if (identities[i]) byIdentity = true;

            // Copied, never mutated in place: `authored` is the PREFAB ASSET's own array and writing
            // through it would repaint the ship for the whole project.
            var result = (Material[])authored.Clone();

            if (byIdentity)
            {
                for (int slot = 0; slot < result.Length; slot++)
                    if (IsDomainMaterial(result[slot], identities))
                        result[slot] = domainMaterial;
                return result;
            }

            int index = Mathf.Clamp(custom.DomainMaterialSlot, 0, result.Length - 1);
            result[index] = domainMaterial;
            return result;
        }

        static bool IsDomainMaterial(Material candidate, IReadOnlyList<Material> identities)
        {
            if (!candidate || identities == null) return false;
            for (int i = 0; i < identities.Count; i++)
                if (candidate == identities[i]) return true;
            return false;
        }

        /// <summary>Whether this renderer is part of the ship hull we want to display.</summary>
        static bool IsHull(Transform prefabRoot, Transform node, Mesh mesh, Renderer renderer)
        {
            if (mesh && PrimitiveMeshNames.Contains(mesh.name)) return false;
            return !ToyModelBuilder.AnyAncestorNameContains(node, prefabRoot, NonHullNameHints);
        }
    }
}
