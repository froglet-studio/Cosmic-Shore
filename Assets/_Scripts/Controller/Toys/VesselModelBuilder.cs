using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Builds a lightweight, display-only 3D model of a vessel by reading the mesh data directly off
    /// the ship <b>prefab asset</b> - it never instantiates the gameplay prefab, so none of its
    /// NetworkObject / VesselStatus / controller components ever Awake (no side effects, no collider
    /// LOD registration, no RequireComponent destroy-order problems).
    ///
    /// It extracts only the vessel's <b>hull</b>: the skimmer sphere (a builtin primitive scaled 15–60×),
    /// trails, jets and VFX are skipped - otherwise the giant skimmer sphere dominates
    /// <see cref="NormalizeToRadius"/> and crushes the real hull to an invisible speck (the bug where
    /// only Rhino - the one vessel whose skimmer has no builtin sphere - rendered). Inactive / disabled
    /// renderers are skipped too (e.g. a hidden duplicate skinned mesh authored at a different scale).
    ///
    /// Skinned meshes are shown static in their authored (bind) pose - fine for a recognisable ship
    /// silhouette. Every hull mesh is painted with one opaque, self-lit preview material (the ship's own
    /// hull material is a transparent, runtime-theme-driven shader that renders dim/invisible at rest),
    /// so the model reads as a solid, domain-tinted silhouette regardless of scene lighting. The result
    /// is centred at its own origin and scaled so its largest dimension is ~<c>targetRadius * 2</c>.
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
            model = null;
            if (!prefabRoot) return false;

            var root = new GameObject("VesselModel");
            var previewMat = BuildPreviewMaterial(previewColor);
            bool any = false;

            foreach (var mf in prefabRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf || !mf.sharedMesh) continue;
                var mr = mf.GetComponent<MeshRenderer>();
                if (!mr) continue; // a MeshFilter with no renderer isn't visible geometry
                if (!IsHullRenderer(prefabRoot, mf.transform, mf.sharedMesh, mr)) continue;
                AddMesh(root.transform, prefabRoot, mf.transform, mf.sharedMesh, previewMat);
                any = true;
            }

            foreach (var smr in prefabRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!smr || !smr.sharedMesh) continue;
                if (!IsHullRenderer(prefabRoot, smr.transform, smr.sharedMesh, smr)) continue;
                AddMesh(root.transform, prefabRoot, smr.transform, smr.sharedMesh, previewMat);
                any = true;
            }

            if (!any)
            {
                Object.Destroy(root);
                return false;
            }

            NormalizeToRadius(root.transform, targetRadius);
            model = root;
            return true;
        }

        /// <summary>
        /// Whether this renderer is part of the ship hull we want to display (vs. skimmer/trail/vfx,
        /// or an inactive/disabled authoring duplicate). Everything is evaluated against the prefab
        /// asset, so activeness is read via <c>activeSelf</c> up the chain - <c>activeInHierarchy</c>
        /// is always false for a prefab that isn't in a loaded scene.
        /// </summary>
        static bool IsHullRenderer(Transform prefabRoot, Transform t, Mesh mesh, Renderer renderer)
        {
            if (renderer && !renderer.enabled) return false;
            if (!IsActiveInPrefab(t, prefabRoot)) return false;
            if (mesh && PrimitiveMeshNames.Contains(mesh.name)) return false;

            for (var c = t; c != null; c = c.parent)
            {
                string n = c.name.ToLowerInvariant();
                foreach (var hint in NonHullNameHints)
                    if (n.Contains(hint)) return false;
                if (c == prefabRoot) break;
            }
            return true;
        }

        static bool IsActiveInPrefab(Transform t, Transform root)
        {
            for (var c = t; c != null; c = c.parent)
            {
                if (!c.gameObject.activeSelf) return false;
                if (c == root) break;
            }
            return true;
        }

        /// <summary>
        /// One opaque, self-lit preview material shared across the whole model. Self-illuminated
        /// (emission) so the silhouette is visible even in an unlit menu, tinted to the player's
        /// domain colour.
        /// </summary>
        static Material BuildPreviewMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader) { color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                mat.SetColor("_EmissionColor", color * 0.6f);
            }
            return mat;
        }

        static void AddMesh(Transform parent, Transform prefabRoot, Transform src, Mesh mesh, Material previewMat)
        {
            var go = new GameObject(src ? src.name : "Mesh");
            go.transform.SetParent(parent, false);

            // Place this mesh at the same pose it has relative to the prefab root.
            go.transform.localPosition = prefabRoot.InverseTransformPoint(src.position);
            go.transform.localRotation = Quaternion.Inverse(prefabRoot.rotation) * src.rotation;
            go.transform.localScale = RelativeLossyScale(prefabRoot.lossyScale, src.lossyScale);

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();

            // One preview material per submesh so multi-submesh hulls render fully (and solidly).
            int sub = Mathf.Max(1, mesh.subMeshCount);
            var mats = new Material[sub];
            for (int i = 0; i < sub; i++) mats[i] = previewMat;
            mr.sharedMaterials = mats;

            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        static Vector3 RelativeLossyScale(Vector3 rootScale, Vector3 childScale) => new(
            SafeDiv(childScale.x, rootScale.x),
            SafeDiv(childScale.y, rootScale.y),
            SafeDiv(childScale.z, rootScale.z));

        static float SafeDiv(float a, float b) => Mathf.Abs(b) > 1e-6f ? a / b : a;

        /// <summary>Recentres child meshes on the model origin and scales so max dimension ≈ radius*2.</summary>
        static void NormalizeToRadius(Transform root, float targetRadius)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);

            // root is at origin, unrotated, unit scale, so world offsets equal local offsets.
            Vector3 center = b.center;
            foreach (Transform child in root)
                child.localPosition -= center;

            float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            float scale = maxDim > 1e-4f ? (targetRadius * 2f) / maxDim : 1f;
            root.localScale = Vector3.one * scale;
        }
    }
}
