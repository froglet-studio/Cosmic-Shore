using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Builds a <b>ghost</b>: a real vessel with everything that ACTS removed, so what is left can
    /// only draw. Jets, tail and hull, flying a recorded path and touching nothing.
    ///
    /// <para><b>Why this replaced mesh harvesting.</b> The first ghost was meshes read off the
    /// prefab asset, which was safe and silent and could never have a jet: a jet is a particle
    /// system and a tail is a <c>TrailRenderer</c>, and neither survives being copied as
    /// geometry. Getting them means instantiating the real prefab, which brings two hazards worth
    /// naming precisely, because only one of them is the one people expect.</para>
    ///
    /// <para><b>Hazard 1 — the stray <c>NetworkObject</c>, and being offline does NOT remove it.</b>
    /// Netcode scans loaded scenes for un-spawned <c>NetworkObject</c>s and adopts each as an
    /// in-scene PLACED object, keyed on a hash every instance of one prefab shares — so the SECOND
    /// stray throws inside <c>PopulateScenePlacedObjects</c> and breaks synchronisation for every
    /// later joiner, on this machine, silently (<c>Docs/PartySystem/BUGS.md</c> B16). The theater
    /// sends and receives nothing, but there is always a NetworkManager running here: the project
    /// hosts even in Menu_Main, and offline mode is itself a <c>127.0.0.1</c> local host. The
    /// network layer is stripped before the ghost ever becomes active, so no stray exists to be
    /// adopted — and what survives is asserted rather than assumed.</para>
    ///
    /// <para><b>Hazard 2 — the one that would actually corrupt a match.</b> A vessel prefab carries
    /// <c>VesselPrismController</c>. An un-declawed ghost retracing a flight would lay REAL TRAIL
    /// PRISMS into the live cell: conserved mass injected into a running simulation by something
    /// that is supposed to be a picture. That is why the strip is a <b>whitelist of what may
    /// draw</b> and not a blacklist of what to remove — a blacklist forgets the next component
    /// somebody adds to a vessel, and the failure mode is silent and cumulative.</para>
    ///
    /// <para><b>Nothing ever wakes.</b> The prefab is instantiated under a DEACTIVATED holder, so
    /// <c>Awake</c> is deferred; the strip runs while the instance is inert; only the survivors are
    /// ever activated. That is also why the strip uses <c>DestroyImmediate</c>: a deferred
    /// <c>Destroy</c> lands at end of frame, which is AFTER the activation that would have run
    /// every stripped component's <c>Awake</c>.</para>
    /// </summary>
    public static class TheaterGhost
    {
        /// <summary>
        /// The components a ghost may keep: things that DRAW, plus the three tail/jet markers,
        /// whose own <c>Awake</c>s do nothing but size the FX (which is what a ghost wants).
        /// Everything else on every child is destroyed.
        /// </summary>
        static readonly HashSet<System.Type> Keep = new()
        {
            typeof(Transform), typeof(RectTransform),
            typeof(MeshFilter), typeof(MeshRenderer), typeof(SkinnedMeshRenderer),
            typeof(ParticleSystem), typeof(ParticleSystemRenderer),
            typeof(TrailRenderer), typeof(LineRenderer),
            typeof(Light), typeof(Animator),
            typeof(VesselTail), typeof(VesselJet), typeof(VesselTailAndJets),
        };

        /// <summary>
        /// Renderers under a node named for one of these are dropped: they are neither hull nor
        /// FX. The skimmer is a builtin sphere scaled 15-60x that would swallow the shot, and the
        /// PIP draws a second camera's view onto a quad. Deliberately NOT here: <c>jet</c>,
        /// <c>trail</c> and <c>vfx</c>, which <see cref="VesselModelBuilder"/> excludes because it
        /// is building a static ICON and this is building a flying ship.
        /// </summary>
        static readonly string[] DropRendererHints = { "skimmer", "forcefield", "crackle", "pip" };

        /// <summary>Builtin primitive meshes — the skimmer bodies, which must never be in shot.</summary>
        static readonly HashSet<string> PrimitiveMeshNames = new()
        { "Sphere", "Cube", "Cylinder", "Capsule", "Plane", "Quad" };

        /// <summary>What a built ghost hands back, so playback can drive and reset it.</summary>
        public class Parts
        {
            public GameObject Root;
            /// <summary>Jet particle systems, with the emission rate each was authored at.</summary>
            public readonly List<(ParticleSystem system, float authoredRate)> Jets = new();
            /// <summary>Every trail the ghost draws — cleared on a seek, or it bridges the jump.</summary>
            public readonly List<TrailRenderer> Trails = new();
        }

        public static Parts Build(Transform prefab, Domains domain, Material domainMaterial,
            Transform parent, string name)
        {
            if (prefab == null) return null;

            // Deactivated holder: an instance that is inactive in the hierarchy does not run Awake,
            // which is the whole reason the strip below is safe to do on a real vessel prefab.
            var holder = new GameObject("[TheaterGhostHolder]");
            holder.SetActive(false);

            var instance = Object.Instantiate(prefab.gameObject, holder.transform);

            // Read the paint rule BEFORE the strip destroys the component that carries it.
            var customization = instance.GetComponent<VesselCustomization>();
            var identities = customization != null ? customization.DomainReplacesMaterials : null;
            int domainSlot = customization != null ? customization.DomainMaterialSlot : 1;

            DropNonDrawingRenderers(instance.transform);
            Strip(instance);
            Paint(instance.transform, identities, domainSlot, domainMaterial);

            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.SetActive(true);

            Object.Destroy(holder);

            var parts = new Parts { Root = instance };
            Collect(instance.transform, parts);
            PaintTails(instance, domain);
            return parts;
        }

        /// <summary>
        /// Drop the renderers that are neither hull nor FX, by the same two tests
        /// <see cref="VesselModelBuilder"/> uses — a builtin primitive mesh, or a node named for a
        /// non-drawing subsystem. Whole GameObjects, so a skimmer's collider goes with its sphere.
        /// </summary>
        static void DropNonDrawingRenderers(Transform root)
        {
            var doomed = new List<GameObject>();
            var filters = root.GetComponentsInChildren<MeshFilter>(true);

            for (int i = 0; i < filters.Length; i++)
            {
                var filter = filters[i];
                if (filter == null) continue;

                bool primitive = filter.sharedMesh != null && PrimitiveMeshNames.Contains(filter.sharedMesh.name);
                if (primitive || NamedForDrop(filter.transform, root)) doomed.Add(filter.gameObject);
            }

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null) continue;
                if (NamedForDrop(renderer.transform, root) && !doomed.Contains(renderer.gameObject))
                    doomed.Add(renderer.gameObject);
            }

            for (int i = 0; i < doomed.Count; i++)
                if (doomed[i] != null) Object.DestroyImmediate(doomed[i]);
        }

        static bool NamedForDrop(Transform node, Transform root)
        {
            for (var t = node; t != null; t = t.parent)
            {
                string lower = t.name.ToLowerInvariant();
                for (int i = 0; i < DropRendererHints.Length; i++)
                    if (lower.Contains(DropRendererHints[i])) return true;
                if (t == root) break;
            }
            return false;
        }

        /// <summary>
        /// Destroy every component not on the keep list.
        ///
        /// <para>The pass REPEATS until it stops making progress, because <c>RequireComponent</c>
        /// refuses a destruction whose dependent is still present and the dependency order among a
        /// vessel's own scripts is not knowable from here. Anything still standing after that is
        /// named — a component that cannot be removed is one whose <c>Awake</c> is about to run on
        /// a ghost, which is exactly the thing this method exists to prevent, so it must not fail
        /// quietly.</para>
        /// </summary>
        static void Strip(GameObject root)
        {
            var doomed = new List<Component>();
            var all = root.GetComponentsInChildren<Component>(true);

            for (int i = 0; i < all.Length; i++)
            {
                var component = all[i];

                // A missing script resolves to null here. It draws nothing and can carry nothing,
                // so it is simply skipped rather than treated as a survivor.
                if (component == null) continue;
                if (Keep.Contains(component.GetType())) continue;
                doomed.Add(component);
            }

            bool progress = true;
            while (progress && doomed.Count > 0)
            {
                progress = false;
                for (int i = doomed.Count - 1; i >= 0; i--)
                {
                    var component = doomed[i];
                    if (component == null) { doomed.RemoveAt(i); progress = true; continue; }

                    if (!CanDestroy(component)) continue;

                    Object.DestroyImmediate(component);
                    doomed.RemoveAt(i);
                    progress = true;
                }
            }

            for (int i = 0; i < doomed.Count; i++)
            {
                if (doomed[i] == null) continue;
                CSDebug.LogWarning(
                    $"[Theater] Could not strip {doomed[i].GetType().Name} from a ghost of " +
                    $"{root.name}; it will wake on a puppet that is supposed to only draw. Add it " +
                    "to TheaterGhost.Keep if it is harmless, or find what still requires it.");
            }
        }

        /// <summary>
        /// Whether this component can be removed right now — nothing kept, and nothing still
        /// doomed, declares a <c>RequireComponent</c> dependency on its type.
        /// </summary>
        static bool CanDestroy(Component component)
        {
            var siblings = component.gameObject.GetComponents<Component>();
            var type = component.GetType();

            for (int i = 0; i < siblings.Length; i++)
            {
                var sibling = siblings[i];
                if (sibling == null || sibling == component) continue;

                var required = sibling.GetType()
                    .GetCustomAttributes(typeof(RequireComponent), inherit: true);
                for (int r = 0; r < required.Length; r++)
                {
                    var rule = (RequireComponent)required[r];
                    if (Requires(rule, type)) return false;
                }
            }
            return true;
        }

        static bool Requires(RequireComponent rule, System.Type type) =>
            (rule.m_Type0 != null && rule.m_Type0.IsAssignableFrom(type))
            || (rule.m_Type1 != null && rule.m_Type1.IsAssignableFrom(type))
            || (rule.m_Type2 != null && rule.m_Type2.IsAssignableFrom(type));

        /// <summary>
        /// Swap the domain-role material slots, reproducing <see cref="VesselCustomization"/>'s own
        /// two modes through <see cref="VesselModelBuilder.ResolveDomainMaterials"/> — one
        /// implementation, so a vessel that changes which slot carries its domain moves the ghost
        /// with it.
        /// </summary>
        static void Paint(Transform root, IReadOnlyList<Material> identities, int slot, Material domainMaterial)
        {
            if (domainMaterial == null) return;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null) continue;

                var resolved = VesselModelBuilder.ResolveDomainMaterials(
                    renderer.sharedMaterials, identities, slot, domainMaterial);
                if (resolved != null) renderer.sharedMaterials = resolved;
            }
        }

        /// <summary>
        /// Paint the tail and jets through the vessel's OWN owner component, which the keep list
        /// preserves: it discovers the markers live and composes the gradient through
        /// <c>TailGradient</c>, so a ghost's streak and a live ship's cannot drift apart.
        /// </summary>
        static void PaintTails(GameObject root, Domains domain)
        {
            var colorSet = PrismLit.ColorSet;
            if (colorSet == null) return;
            if (!colorSet.TryGetColorSetByDomain(domain, out var domainColors)) return;

            var owner = root.GetComponentInChildren<VesselTailAndJets>(true);
            if (owner != null) owner.SetColors(domainColors.TrailHighlightColor, domainColors.TrailCoreColor);
        }

        static void Collect(Transform root, Parts parts)
        {
            var trails = root.GetComponentsInChildren<TrailRenderer>(true);
            for (int i = 0; i < trails.Length; i++)
                if (trails[i] != null) parts.Trails.Add(trails[i]);

            var jets = root.GetComponentsInChildren<VesselJet>(true);
            for (int i = 0; i < jets.Length; i++)
            {
                if (jets[i] == null) continue;
                var systems = jets[i].GetComponentsInChildren<ParticleSystem>(true);
                for (int s = 0; s < systems.Length; s++)
                {
                    if (systems[s] == null) continue;
                    parts.Jets.Add((systems[s], systems[s].emission.rateOverTimeMultiplier));
                }
            }
        }
    }
}
