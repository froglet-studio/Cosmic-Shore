using UnityEngine;

namespace CosmicShore.Splats
{
    // Spawns one GameObject per baked splat point. Each prism gets a per-instance Material
    // and a collider so the cloud is fly-through-able with physics. Intended as a desktop
    // prototype harness; 10k separate Materials + colliders is heavy.
    // TODO: For shipping, batch prisms into GPU-instanced renderers and replace per-prism
    // colliders with a single compound / spatial-partitioned collider tree.
    // NOTE: When using a real .ply derived from "Orchid Flower" (danylyon, SuperSplat),
    // a shipping derivative should get the creator's okay before public release.
    [DisallowMultipleComponent]
    public class SplatPrismSpawner : MonoBehaviour
    {
        public enum ColliderMode
        {
            Box = 0,
            ConvexMesh = 1,
            None = 2
        }

        [Header("Data")]
        [Tooltip("Baked, curated splat point set produced by Tools/Splats/Bake Prism Set.")]
        [SerializeField] private SplatPrismSet set;

        [Header("Prism Appearance")]
        [Tooltip("Mesh instanced per splat. If null, a unit triangular prism is generated procedurally.")]
        [SerializeField] private Mesh prismMesh;
        [Tooltip("Material instanced per splat. If null, a default URP/Lit (or Standard) material is created.")]
        [SerializeField] private Material baseMaterial;
        [Tooltip("Global multiplier on per-splat scale. Increase to make the cloud thicker, decrease for filigree.")]
        [SerializeField, Min(0f)] private float scaleMultiplier = 1f;
        [Tooltip("Multiplies decoded opacity into the material color alpha. 1 = use as-is.")]
        [SerializeField, Min(0f)] private float opacityToAlpha = 1f;
        [Tooltip("Multiplies decoded opacity into HDR emission intensity. 0 = no emission.")]
        [SerializeField, Min(0f)] private float opacityToEmission = 0f;

        [Header("Colliders")]
        [SerializeField] private ColliderMode colliderMode = ColliderMode.Box;

        [Header("Spawn Behavior")]
        [Tooltip("Spawn on Start automatically.")]
        [SerializeField] private bool spawnOnStart = true;
        [Tooltip("Parent for spawned prisms. Defaults to this transform when null.")]
        [SerializeField] private Transform container;

        private Mesh _generatedFallbackMesh;
        private GameObject _spawnRoot;

        public void SetSourceSet(SplatPrismSet newSet)
        {
            set = newSet;
        }

        private void Start()
        {
            if (spawnOnStart) Spawn();
        }

        [ContextMenu("Spawn")]
        public void Spawn()
        {
            if (set == null)
            {
                // Soft skip — common during scene boot when the set is assigned a frame later
                // (e.g. by the runtime loader). Other callers should still see one warning.
                Debug.LogWarning($"[SplatPrismSpawner] No SplatPrismSet assigned on {name}; skipping spawn.", this);
                return;
            }
            if (set.points == null || set.points.Length == 0)
            {
                Debug.LogWarning($"[SplatPrismSpawner] SplatPrismSet '{set.name}' has no points.", this);
                return;
            }

            int count = set.points.Length;
            if (count > SplatPrismSet.MaxPrisms)
            {
                Debug.LogError($"[SplatPrismSpawner] SplatPrismSet '{set.name}' contains {count} points, exceeding hard cap {SplatPrismSet.MaxPrisms}. Aborting spawn.", this);
                return;
            }

            Clear();

            var parent = container != null ? container : transform;
            _spawnRoot = new GameObject($"SplatPrisms ({set.name}, {count})");
            _spawnRoot.transform.SetParent(parent, false);

            var mesh = prismMesh != null ? prismMesh : GetOrCreateFallbackPrismMesh();
            var srcMat = baseMaterial != null ? baseMaterial : CreateDefaultMaterial();
            bool emissionEnabled = opacityToEmission > 0f;

            for (int i = 0; i < count; i++)
            {
                SpawnOne(set.points[i], mesh, srcMat, emissionEnabled, _spawnRoot.transform, i);
            }

            Debug.Log($"[SplatPrismSpawner] Spawned {count} prisms from '{set.name}'.", this);
        }

        [ContextMenu("Clear")]
        public void Clear()
        {
            if (_spawnRoot == null) return;
            if (Application.isPlaying) Destroy(_spawnRoot);
            else DestroyImmediate(_spawnRoot);
            _spawnRoot = null;
        }

        private void SpawnOne(SplatPoint p, Mesh mesh, Material srcMat, bool emissionEnabled, Transform parent, int index)
        {
            var go = new GameObject($"Prism_{index:00000}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = p.position;
            go.transform.localRotation = p.rotation;
            go.transform.localScale = p.scale * scaleMultiplier;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            var mat = new Material(srcMat);
            var col = p.color;
            col.a = Mathf.Clamp01(p.opacity * opacityToAlpha);
            mat.color = col;
            if (emissionEnabled)
            {
                mat.EnableKeyword("_EMISSION");
                float intensity = p.opacity * opacityToEmission;
                var emissive = new Color(col.r * intensity, col.g * intensity, col.b * intensity, 1f);
                mat.SetColor("_EmissionColor", emissive);
            }
            mr.sharedMaterial = mat;

            switch (colliderMode)
            {
                case ColliderMode.Box:
                {
                    var box = go.AddComponent<BoxCollider>();
                    box.center = mesh.bounds.center;
                    box.size = mesh.bounds.size;
                    break;
                }
                case ColliderMode.ConvexMesh:
                {
                    var mc = go.AddComponent<MeshCollider>();
                    mc.sharedMesh = mesh;
                    mc.convex = true;
                    break;
                }
                case ColliderMode.None:
                    break;
            }
        }

        private Material CreateDefaultMaterial()
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit != null) return new Material(urpLit);
            var standard = Shader.Find("Standard");
            if (standard != null) return new Material(standard);
            var fallback = Shader.Find("Sprites/Default");
            return new Material(fallback);
        }

        private Mesh GetOrCreateFallbackPrismMesh()
        {
            if (_generatedFallbackMesh != null) return _generatedFallbackMesh;
            _generatedFallbackMesh = BuildTriangularPrismMesh();
            return _generatedFallbackMesh;
        }

        // Procedural unit triangular prism centred on origin, axis along Z, occupying [-0.5, 0.5] on each axis.
        private static Mesh BuildTriangularPrismMesh()
        {
            var mesh = new Mesh { name = "SplatFallbackTriPrism" };

            float h = 0.5f;
            float r = 0.5f;
            const float kSin60 = 0.8660254f;

            Vector3 a0 = new Vector3(0f, r, -h);
            Vector3 b0 = new Vector3(-r * kSin60, -r * 0.5f, -h);
            Vector3 c0 = new Vector3( r * kSin60, -r * 0.5f, -h);
            Vector3 a1 = new Vector3(0f, r, h);
            Vector3 b1 = new Vector3(-r * kSin60, -r * 0.5f, h);
            Vector3 c1 = new Vector3( r * kSin60, -r * 0.5f, h);

            var verts = new Vector3[]
            {
                // Back cap
                a0, b0, c0,
                // Front cap
                a1, c1, b1,
                // Side AB (a0,a1,b1,b0)
                a0, a1, b1, b0,
                // Side BC (b0,b1,c1,c0)
                b0, b1, c1, c0,
                // Side CA (c0,c1,a1,a0)
                c0, c1, a1, a0,
            };

            var tris = new int[]
            {
                0, 1, 2,
                3, 4, 5,
                6, 7, 8, 6, 8, 9,
                10, 11, 12, 10, 12, 13,
                14, 15, 16, 14, 16, 17,
            };

            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            if (_generatedFallbackMesh != null)
            {
                if (Application.isPlaying) Destroy(_generatedFallbackMesh);
                else DestroyImmediate(_generatedFallbackMesh);
                _generatedFallbackMesh = null;
            }
        }
    }
}
