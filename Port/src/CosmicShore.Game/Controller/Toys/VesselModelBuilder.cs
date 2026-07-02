using CosmicShore.Engine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Builds a lightweight, display-only 3D model of a vessel by reading the mesh data directly off
    /// the ship <b>prefab asset</b> — it never instantiates the gameplay prefab, so none of its
    /// NetworkObject / VesselStatus / controller components ever Awake (no side effects, no collider
    /// LOD registration, no RequireComponent destroy-order problems).
    ///
    /// Skinned meshes are shown static in their authored (bind) pose — fine for a recognisable ship
    /// silhouette. The result is centred at its own origin and scaled so its largest dimension is
    /// ~<c>targetRadius * 2</c>.
    /// </summary>
    public static class VesselModelBuilder
    {
        public static bool TryBuild(Transform prefabRoot, float targetRadius, out GameObject model)
        {
            model = null;
            if (!prefabRoot) return false;

            var root = new GameObject("VesselModel");
            bool any = false;

            // The headless engine carries no mesh data yet (the Mesh/MeshFilter arc), so the
            // mesh harvest below is deviated — TryBuild reports false and callers fall back to
            // the procedural sphere body.
            // PORT Deviation (mesh arc, restore when engine Mesh/MeshFilter land): foreach (var mf in prefabRoot.GetComponentsInChildren<MeshFilter>(true))
            // PORT Deviation (mesh arc, restore when engine Mesh/MeshFilter land): {
            // PORT Deviation (mesh arc, restore when engine Mesh/MeshFilter land):     if (!mf || !mf.sharedMesh) continue;
            // PORT Deviation (mesh arc, restore when engine Mesh/MeshFilter land):     var mr = mf.GetComponent<MeshRenderer>();
            // PORT Deviation (mesh arc, restore when engine Mesh/MeshFilter land):     AddMesh(root.transform, prefabRoot, mf.transform, mf.sharedMesh, mr ? mr.sharedMaterials : null);
            // PORT Deviation (mesh arc, restore when engine Mesh/MeshFilter land):     any = true;
            // PORT Deviation (mesh arc, restore when engine Mesh/MeshFilter land): }

            // PORT Deviation (mesh arc, restore when engine Mesh/MeshFilter land): foreach (var smr in prefabRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            // PORT Deviation (mesh arc, restore when engine Mesh/MeshFilter land): {
            // PORT Deviation (mesh arc, restore when engine Mesh/MeshFilter land):     if (!smr || !smr.sharedMesh) continue;
            // PORT Deviation (mesh arc, restore when engine Mesh/MeshFilter land):     AddMesh(root.transform, prefabRoot, smr.transform, smr.sharedMesh, smr.sharedMaterials);
            // PORT Deviation (mesh arc, restore when engine Mesh/MeshFilter land):     any = true;
            // PORT Deviation (mesh arc, restore when engine Mesh/MeshFilter land): }

            if (!any)
            {
                Object.Destroy(root);
                return false;
            }

            NormalizeToRadius(root.transform, targetRadius);
            model = root;
            return true;
        }

        // PORT Deviation (mesh arc, restore when engine Mesh/MeshFilter land):
        // PORT Deviation (mesh arc): static void AddMesh(Transform parent, Transform prefabRoot, Transform src, Mesh mesh, Material[] materials)
        // PORT Deviation (mesh arc): {
        // PORT Deviation (mesh arc):     var go = new GameObject(src ? src.name : "Mesh");
        // PORT Deviation (mesh arc):     go.transform.SetParent(parent, false);
        // PORT Deviation (mesh arc):
        // PORT Deviation (mesh arc):     // Place this mesh at the same pose it has relative to the prefab root.
        // PORT Deviation (mesh arc):     go.transform.localPosition = prefabRoot.InverseTransformPoint(src.position);
        // PORT Deviation (mesh arc):     go.transform.localRotation = Quaternion.Inverse(prefabRoot.rotation) * src.rotation;
        // PORT Deviation (mesh arc):     go.transform.localScale = RelativeLossyScale(prefabRoot.lossyScale, src.lossyScale);
        // PORT Deviation (mesh arc):
        // PORT Deviation (mesh arc):     var mf = go.AddComponent<MeshFilter>();
        // PORT Deviation (mesh arc):     mf.sharedMesh = mesh;
        // PORT Deviation (mesh arc):     var mr = go.AddComponent<MeshRenderer>();
        // PORT Deviation (mesh arc):     if (materials is { Length: > 0 }) mr.sharedMaterials = materials;
        // PORT Deviation (mesh arc):     mr.shadowCastingMode = CosmicShore.Engine.Rendering.ShadowCastingMode.Off;
        // PORT Deviation (mesh arc):     mr.receiveShadows = false;
        // PORT Deviation (mesh arc): }

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
