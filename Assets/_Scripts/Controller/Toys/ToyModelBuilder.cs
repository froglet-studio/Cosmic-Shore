using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Builds a lightweight, display-only 3D model of ANY prefab by reading mesh data straight off
    /// the <b>prefab asset</b> - it never instantiates the prefab, so none of its gameplay
    /// components ever Awake (no NetworkObject, no registry entries, no collider LOD, no
    /// RequireComponent destroy-order problems).
    ///
    /// This is the shared engine behind every toy icon that is "the thing you are choosing, small":
    /// <see cref="VesselModelBuilder"/> (mini ships, hull-filtered) and the lifeform bench's
    /// species stations (mini creatures). A station that shows the actual thing needs no text to
    /// explain itself, which is the direction the whole toybox is heading.
    ///
    /// Skinned meshes are shown static in their authored (bind) pose - fine for a recognisable
    /// silhouette. Everything is painted with one opaque, self-lit preview material, because the
    /// real gameplay materials are transparent runtime-theme shaders that render dim or invisible
    /// at rest. The result is centred on its own origin and scaled so its largest dimension is
    /// ~<c>targetRadius * 2</c>.
    /// </summary>
    public static class ToyModelBuilder
    {
        /// <summary>
        /// Per-renderer filter: return false to leave that mesh out of the model (e.g. a vessel's
        /// skimmer sphere, which would otherwise dominate the bounds). Null accepts everything
        /// visible.
        /// </summary>
        public delegate bool RendererFilter(Transform prefabRoot, Transform node, Mesh mesh, Renderer renderer);

        /// <summary>
        /// Harvest <paramref name="prefabRoot"/>'s meshes into a display-only model tinted
        /// <paramref name="previewColor"/> and fitted to <paramref name="targetRadius"/>.
        /// Returns false (and builds nothing) when the prefab has no eligible visible geometry -
        /// callers keep their fallback body.
        /// </summary>
        public static bool TryBuild(Transform prefabRoot, float targetRadius, Color previewColor,
            out GameObject model, RendererFilter filter = null)
            => TryBuild(prefabRoot, targetRadius, BuildPreviewMaterial(previewColor), out model, filter);

        /// <summary>
        /// As above, but painted with a material the CALLER owns. Prefer this when one owner builds
        /// several models (a toy emblem's core + satellites): they then share one material, a
        /// re-tint is a handful of writes rather than a walk, and the owner can destroy it - the
        /// colour overload allocates a `Material` per call that nothing frees.
        /// </summary>
        public static bool TryBuild(Transform prefabRoot, float targetRadius, Material sharedMaterial,
            out GameObject model, RendererFilter filter = null)
        {
            model = null;
            if (!prefabRoot) return false;

            var root = new GameObject("ToyModel");
            var previewMat = sharedMaterial ? sharedMaterial : BuildPreviewMaterial(Color.white);
            bool any = false;

            foreach (var mf in prefabRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf || !mf.sharedMesh) continue;
                var mr = mf.GetComponent<MeshRenderer>();
                if (!mr) continue; // a MeshFilter with no renderer isn't visible geometry
                if (!Accept(prefabRoot, mf.transform, mf.sharedMesh, mr, filter)) continue;
                AddMesh(root.transform, prefabRoot, mf.transform, mf.sharedMesh, previewMat);
                any = true;
            }

            foreach (var smr in prefabRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!smr || !smr.sharedMesh) continue;
                if (!Accept(prefabRoot, smr.transform, smr.sharedMesh, smr, filter)) continue;
                AddMesh(root.transform, prefabRoot, smr.transform, smr.sharedMesh, previewMat);
                any = true;
            }

            if (!any)
            {
                UnityEngine.Object.Destroy(root);
                return false;
            }

            NormalizeToRadius(root.transform, targetRadius);
            model = root;
            return true;
        }

        static bool Accept(Transform prefabRoot, Transform node, Mesh mesh, Renderer renderer, RendererFilter filter)
        {
            if (renderer && !renderer.enabled) return false;
            // Activeness is read via activeSelf up the chain: activeInHierarchy is always false
            // for a prefab asset that isn't in a loaded scene.
            if (!IsActiveInPrefab(node, prefabRoot)) return false;
            return filter == null || filter(prefabRoot, node, mesh, renderer);
        }

        public static bool IsActiveInPrefab(Transform t, Transform root)
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
        /// (emission) so the silhouette is visible even in an unlit menu.
        /// </summary>
        public static Material BuildPreviewMaterial(Color color)
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

            // One preview material per submesh so multi-submesh models render fully (and solidly).
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

        /// <summary>Convenience for filters that need to walk node → prefab root by name.</summary>
        public static bool AnyAncestorNameContains(Transform node, Transform root, string[] hints)
        {
            for (var c = node; c != null; c = c.parent)
            {
                string n = c.name.ToLowerInvariant();
                foreach (var hint in hints)
                    if (n.Contains(hint)) return true;
                if (c == root) break;
            }
            return false;
        }
    }
}
