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
    /// silhouette. The result is centred on its own origin and scaled so its largest dimension is
    /// ~<c>targetRadius * 2</c>.
    ///
    /// By default everything is painted with one opaque, self-lit preview material, because the
    /// real gameplay materials are dark unlit theme shaders that read as a black blob at glyph
    /// size. That is still right for a GLYPH. It stopped being the only option for a PREVIEW once
    /// the vessel vision band shipped: pass a <see cref="MaterialResolver"/> and the model keeps
    /// the source's own materials, so a station shows the actual ship and the band supplies the
    /// at-a-glance domain read that the flat fill used to.
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
        /// Per-renderer material choice: return the materials the harvested copy should draw with,
        /// given the ones the source renderer actually wears. Null (or a null return) falls back to
        /// the flat preview material.
        ///
        /// This is what lets a model be built from the REAL thing rather than as a silhouette of
        /// it. The array is padded or truncated to the mesh's submesh count by the builder, so a
        /// resolver only has to answer the question, not do the bookkeeping.
        /// </summary>
        public delegate Material[] MaterialResolver(Transform node, Renderer source, Material[] authored);

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
            => TryBuild(prefabRoot, targetRadius, sharedMaterial, out model, filter, null);

        /// <summary>
        /// As above, with a <see cref="MaterialResolver"/> that can keep the SOURCE's own materials
        /// instead of flattening everything to the preview colour. That is the difference between a
        /// silhouette of the thing and the thing itself — worth having once something else (the
        /// vessel vision band) is supplying the at-a-glance read that the flat fill used to.
        /// </summary>
        public static bool TryBuild(Transform prefabRoot, float targetRadius, Material sharedMaterial,
            out GameObject model, RendererFilter filter, MaterialResolver materials)
        {
            model = null;
            if (!prefabRoot) return false;

            var root = new GameObject("ToyModel");
            bool any = false;

            // Built LAZILY, and that matters: a model whose resolver supplies every material never
            // needs one, and the eager version allocated a white Material per model that nothing
            // ever freed. Still eager in effect for the flat path, where the first mesh asks for it.
            Material lazyPreview = sharedMaterial;
            Material Preview()
            {
                if (!lazyPreview) lazyPreview = BuildPreviewMaterial(Color.white);
                return lazyPreview;
            }

            foreach (var mf in prefabRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf || !mf.sharedMesh) continue;
                var mr = mf.GetComponent<MeshRenderer>();
                if (!mr) continue; // a MeshFilter with no renderer isn't visible geometry
                if (!Accept(prefabRoot, mf.transform, mf.sharedMesh, mr, filter)) continue;
                AddMesh(root.transform, prefabRoot, mf.transform, mf.sharedMesh, Preview,
                        Resolve(materials, mf.transform, mr));
                any = true;
            }

            foreach (var smr in prefabRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!smr || !smr.sharedMesh) continue;
                if (!Accept(prefabRoot, smr.transform, smr.sharedMesh, smr, filter)) continue;
                AddMesh(root.transform, prefabRoot, smr.transform, smr.sharedMesh, Preview,
                        Resolve(materials, smr.transform, smr));
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

        static Material[] Resolve(MaterialResolver resolver, Transform node, Renderer source)
            => resolver?.Invoke(node, source, source ? source.sharedMaterials : null);

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

        static void AddMesh(Transform parent, Transform prefabRoot, Transform src, Mesh mesh,
            System.Func<Material> preview, Material[] resolved)
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

            // One material per submesh so multi-submesh models render fully (and solidly). A
            // resolver's answer is padded/truncated to the submesh count here rather than at the
            // call site: a renderer's material array and its mesh's submesh count are allowed to
            // disagree, and an unfilled slot renders as Unity's magenta error material.
            int sub = Mathf.Max(1, mesh.subMeshCount);
            var mats = new Material[sub];
            for (int i = 0; i < sub; i++)
            {
                Material chosen = resolved != null && resolved.Length > 0
                    ? resolved[Mathf.Min(i, resolved.Length - 1)]
                    : null;
                mats[i] = chosen ? chosen : preview();
            }
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
