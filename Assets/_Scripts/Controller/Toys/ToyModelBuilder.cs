using System.Collections.Generic;
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

            // What the ASSET shows is not always what the SHIP shows: a procedural hull hides its
            // inherited model at Awake, so on the asset that model's renderers are still enabled
            // and the real hull is an empty MeshFilter. Read the one, never the other.
            var hiddenRoot = HiddenLegacyModelRoot(prefabRoot);

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
                if (!Accept(prefabRoot, mf.transform, mf.sharedMesh, mr, filter, hiddenRoot)) continue;
                AddMesh(root.transform, prefabRoot, mf.transform, mf.sharedMesh, Preview,
                        Resolve(materials, mf.transform, mr));
                any = true;
            }

            foreach (var smr in prefabRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!smr || !smr.sharedMesh) continue;
                if (!Accept(prefabRoot, smr.transform, smr.sharedMesh, smr, filter, hiddenRoot)) continue;
                AddMesh(root.transform, prefabRoot, smr.transform, smr.sharedMesh, Preview,
                        Resolve(materials, smr.transform, smr));
                any = true;
            }

            var minted = new List<Mesh>();
            if (HarvestProceduralHulls(prefabRoot, root.transform, Preview, materials, minted))
            {
                any = true;
                root.AddComponent<ToyMintedMeshes>().Adopt(minted);
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

        static bool Accept(Transform prefabRoot, Transform node, Mesh mesh, Renderer renderer, RendererFilter filter,
            Transform hiddenRoot)
        {
            if (renderer && !renderer.enabled) return false;
            if (IsUnderHiddenLegacyModel(node, hiddenRoot)) return false;
            // Activeness is read via activeSelf up the chain: activeInHierarchy is always false
            // for a prefab asset that isn't in a loaded scene.
            if (!IsActiveInPrefab(node, prefabRoot)) return false;
            return filter == null || filter(prefabRoot, node, mesh, renderer);
        }

        /// <summary>
        /// The root of the legacy model a procedural hull SWITCHES OFF at runtime, or null. On the
        /// prefab asset those renderers are still enabled - `ScarabHullBuilder.HideLegacyModel`
        /// runs in Awake - so an asset-reading harvester has to be told, or it photographs the
        /// hidden ship (which is how the Scarab's codex portrait came out byte-identical to the
        /// Sparrow's).
        /// </summary>
        public static Transform HiddenLegacyModelRoot(Transform prefabRoot)
        {
            if (!prefabRoot) return null;
            var source = prefabRoot.GetComponentInChildren<IProceduralElementMorphSource>(true);
            return source?.HiddenLegacyModelRoot;
        }

        /// <summary>True when <paramref name="node"/> draws under a hidden legacy model root.</summary>
        public static bool IsUnderHiddenLegacyModel(Transform node, Transform hiddenRoot)
            => hiddenRoot && node && node.IsChildOf(hiddenRoot);

        /// <summary>
        /// Emit every <see cref="IProceduralHullSource"/> under <paramref name="prefabRoot"/> as
        /// posed mesh children of <paramref name="root"/>, built from the ASSET's authored
        /// settings. The meshes minted here are appended to <paramref name="minted"/> and belong
        /// to the CALLER (a runtime model hangs a <see cref="ToyMintedMeshes"/> on itself; an
        /// editor bake adds them to its temporaries). A source is the hull by declaration, so no
        /// renderer filter is consulted; the source component's own renderer supplies the
        /// materials the resolver sees, exactly as it does for the live ship.
        /// </summary>
        public static bool HarvestProceduralHulls(Transform prefabRoot, Transform root,
            System.Func<Material> preview, MaterialResolver materials, List<Mesh> minted)
        {
            if (!prefabRoot || !root) return false;
            bool any = false;
            var pieces = new List<ProceduralHullPiece>();

            foreach (var source in prefabRoot.GetComponentsInChildren<IProceduralHullSource>(true))
            {
                if (source is not Component component || !component) continue;
                var node = component.transform;
                if (!IsActiveInPrefab(node, prefabRoot)) continue;

                pieces.Clear();
                source.BuildPreviewPieces(pieces);
                var resolved = Resolve(materials, node, component.GetComponent<Renderer>());

                foreach (var piece in pieces)
                {
                    var mesh = BuildPieceMesh(piece);
                    minted?.Add(mesh);
                    AddMesh(root, prefabRoot, node, mesh, preview, resolved, piece.LocalPosition, piece.Name);
                    any = true;
                }
            }
            return any;
        }

        static Mesh BuildPieceMesh(ProceduralHullPiece piece)
        {
            var mesh = new Mesh { name = piece.Name ?? "ProceduralHull" };
            mesh.SetVertices(piece.Vertices);
            if (piece.Normals != null && piece.Normals.Length == piece.Vertices.Length) mesh.SetNormals(piece.Normals);
            if (piece.Uvs != null && piece.Uvs.Length == piece.Vertices.Length) mesh.SetUVs(0, piece.Uvs);
            int subs = piece.Submeshes?.Length ?? 0;
            mesh.subMeshCount = Mathf.Max(1, subs);
            for (int i = 0; i < subs; i++) mesh.SetTriangles(piece.Submeshes[i], i);
            if (piece.Normals == null || piece.Normals.Length != piece.Vertices.Length) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
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
            System.Func<Material> preview, Material[] resolved,
            Vector3 localOffset = default, string name = null)
        {
            var go = new GameObject(name ?? (src ? src.name : "Mesh"));
            go.transform.SetParent(parent, false);
            go.hideFlags = parent.gameObject.hideFlags;   // an editor bake's HideAndDontSave root keeps its children out of the scene

            // Place this mesh at the same pose it has relative to the prefab root (plus a piece's
            // own seat under its source, for a procedural hull's re-seated parts).
            go.transform.localPosition = prefabRoot.InverseTransformPoint(src.TransformPoint(localOffset));
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
