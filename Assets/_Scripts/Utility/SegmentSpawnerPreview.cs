using UnityEngine;
using CosmicShore.Gameplay;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CosmicShore.Utility
{
    /// <summary>
    /// Editor-only preview of the prism layout that a <see cref="SegmentSpawner"/>
    /// will produce at runtime. Drop onto the same GameObject as a SegmentSpawner
    /// and pick a preview shape in the inspector - the scene gets a child
    /// <c>[SegmentSpawnerPreview]</c> GameObject populated with proxy
    /// MeshRenderers at every block position the spawner would instantiate.
    ///
    /// Real GameObjects (rather than gizmos) so the preview is visible in both
    /// Scene and Game views and is independent of the Gizmos toolbar toggle.
    /// The proxy hierarchy uses <see cref="HideFlags.DontSave"/> so it never
    /// persists to the scene asset and is destroyed on disable / mode change.
    ///
    /// Currently understands <see cref="SpawnableWaypointTrack"/> (the HexRace
    /// track spawnable). Other <see cref="SpawnableBase"/> subclasses can be
    /// added by extending <c>BuildPreview</c>.
    ///
    /// Component is wrapped in <c>#if UNITY_EDITOR</c> - a no-op MonoBehaviour
    /// in player builds.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SegmentSpawner))]
    public class SegmentSpawnerPreview : MonoBehaviour
    {
        public enum PreviewShape
        {
            None = 0,           // Off - proxy hierarchy is destroyed.
            Cuboid = 1,         // Plain prism box.
            Shielded = 2,       // Octahedron (PrismOctahedronShield runtime visual).
            SuperShielded = 3,  // Stellated octahedron (PrismStellatedOctahedronShield).
        }

        [Tooltip("Mesh shape drawn at each block position. None tears down the " +
                 "preview entirely. SuperShielded matches the HexRace default.")]
        [SerializeField] private PreviewShape preview = PreviewShape.SuperShielded;

        [Tooltip("Intensity level (1-4) to preview. Track shape varies per intensity.")]
        [SerializeField, Range(1, 4)] private int intensity = 1;

        [Tooltip("Color used for regular track block proxies.")]
        [SerializeField] private Color blockColor = new(0.3f, 0.7f, 1f, 1f);

        [Tooltip("Color used for waypoint marker proxies (the larger blocks at each waypoint).")]
        [SerializeField] private Color markerColor = new(1f, 0.7f, 0.2f, 1f);

        [Tooltip("Circumscribing scale factor for the octahedron / stellation. Match the runtime " +
                 "PrismOctahedronShield / PrismStellatedOctahedronShield shieldScale to keep the " +
                 "preview honest.")]
        [SerializeField] private float shieldScale = OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;

        private const string PreviewRootName = "[SegmentSpawnerPreview]";

#if UNITY_EDITOR
        private GameObject _previewRoot;
        private Mesh _cuboidMesh;
        private Mesh _octahedronMesh;
        private Mesh _stellationMesh;
        private Material _blockMaterial;
        private Material _markerMaterial;
        private float _meshShieldScale = -1f;

        private void OnEnable()
        {
            EditorApplication.delayCall += Rebuild;
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= Rebuild;
            DestroyPreviewHierarchy();
            // Don't destroy meshes/materials here - they're cached for the
            // next OnEnable. Final cleanup happens in OnDestroy.
        }

        private void OnDestroy()
        {
            DestroyPreviewHierarchy();
            DestroyOwnedAssets();
        }

        private void OnValidate()
        {
            // OnValidate runs during serialization; defer mesh / GameObject ops.
            if (!Mathf.Approximately(_meshShieldScale, shieldScale))
                _meshShieldScale = -1f; // force regen on next Rebuild
            EditorApplication.delayCall += Rebuild;
        }

        private void Rebuild()
        {
            if (this == null) return;             // component destroyed
            if (Application.isPlaying) return;     // runtime spawn handles this

            DestroyPreviewHierarchy();
            if (preview == PreviewShape.None) return;

            var spawner = GetComponent<SegmentSpawner>();
            if (spawner == null) return;

            EnsureAssets();

            _previewRoot = new GameObject(PreviewRootName);
            _previewRoot.hideFlags = HideFlags.DontSave;
            _previewRoot.transform.SetParent(transform, worldPositionStays: false);

            Vector3 worldOrigin = spawner.transform.position + spawner.origin;
            int count = 0;

            foreach (var spawnable in spawner.EnumerateSpawnables())
                count += BuildPreview(spawnable, worldOrigin);

            // Surface the count so the user has a quick sanity check that
            // proxies were actually created. (Not spammed every frame -
            // Rebuild only fires on enable / OnValidate.)
            if (count == 0)
            {
                Debug.LogWarning(
                    $"[SegmentSpawnerPreview] No preview blocks generated. The SegmentSpawner has " +
                    $"no SpawnableWaypointTrack in any of its lists, or its waypoint list for " +
                    $"intensity {intensity} is empty.", this);
            }
        }

        private int BuildPreview(SpawnableBase spawnable, Vector3 worldOrigin)
        {
            int count = 0;
            if (spawnable is SpawnableWaypointTrack waypointTrack)
            {
                foreach (var block in waypointTrack.GetPreviewBlocks(intensity))
                {
                    CreateBlockProxy(block, worldOrigin);
                    count++;
                }
            }
            // Other SpawnableBase subclasses can hook in here when they grow
            // their own preview helpers. Falling through silently is intentional.
            return count;
        }

        private void CreateBlockProxy(in SpawnableWaypointTrack.PreviewBlock block, Vector3 worldOrigin)
        {
            var mesh = preview switch
            {
                PreviewShape.Cuboid => _cuboidMesh,
                PreviewShape.Shielded => _octahedronMesh,
                PreviewShape.SuperShielded => _stellationMesh,
                _ => null,
            };
            if (mesh == null) return;

            var go = new GameObject(block.IsMarker ? "Marker" : "Block");
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(_previewRoot.transform, worldPositionStays: false);
            // World-space placement to match what SpawnableWaypointTrack.Spawn would emit.
            go.transform.position = worldOrigin + block.Position;
            go.transform.rotation = block.Rotation;
            go.transform.localScale = block.Scale;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = block.IsMarker ? _markerMaterial : _blockMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        private void EnsureAssets()
        {
            Vector3 unitHalfExtents = Vector3.one * 0.5f;

            if (_cuboidMesh == null)
            {
                _cuboidMesh = GenerateCuboidMesh(unitHalfExtents);
                _cuboidMesh.name = "SegmentSpawnerPreview_Cuboid";
                _cuboidMesh.hideFlags = HideFlags.DontSave;
            }
            if (_octahedronMesh == null)
            {
                _octahedronMesh = OctahedronMeshGenerator.Generate(unitHalfExtents, shieldScale);
                _octahedronMesh.name = "SegmentSpawnerPreview_Octahedron";
                _octahedronMesh.hideFlags = HideFlags.DontSave;
            }
            if (_stellationMesh == null)
            {
                _stellationMesh = StellatedOctahedronMeshGenerator.Generate(unitHalfExtents, shieldScale);
                _stellationMesh.name = "SegmentSpawnerPreview_StellatedOctahedron";
                _stellationMesh.hideFlags = HideFlags.DontSave;
            }
            _meshShieldScale = shieldScale;

            EnsureMaterial(ref _blockMaterial, "SegmentSpawnerPreview_Block", blockColor);
            EnsureMaterial(ref _markerMaterial, "SegmentSpawnerPreview_Marker", markerColor);
        }

        private static void EnsureMaterial(ref Material mat, string name, Color color)
        {
            if (mat == null)
            {
                // URP first, then built-in unlit, then Standard, then magenta error.
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader == null) shader = Shader.Find("Standard");
                mat = new Material(shader != null ? shader : Shader.Find("Hidden/InternalErrorShader"))
                {
                    name = name,
                    hideFlags = HideFlags.DontSave,
                };
            }
            // URP exposes color via _BaseColor; built-in shaders use the legacy "color" property.
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
        }

        private static Mesh GenerateCuboidMesh(Vector3 halfExtents)
        {
            var mesh = new Mesh();
            float ex = halfExtents.x, ey = halfExtents.y, ez = halfExtents.z;
            mesh.vertices = new[]
            {
                new Vector3(-ex, -ey, -ez), new Vector3( ex, -ey, -ez),
                new Vector3( ex,  ey, -ez), new Vector3(-ex,  ey, -ez),
                new Vector3(-ex, -ey,  ez), new Vector3( ex, -ey,  ez),
                new Vector3( ex,  ey,  ez), new Vector3(-ex,  ey,  ez),
            };
            mesh.triangles = new[]
            {
                0, 2, 1, 0, 3, 2,   // -Z
                4, 5, 6, 4, 6, 7,   // +Z
                0, 1, 5, 0, 5, 4,   // -Y
                2, 3, 7, 2, 7, 6,   // +Y
                0, 4, 7, 0, 7, 3,   // -X
                1, 2, 6, 1, 6, 5,   // +X
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void DestroyPreviewHierarchy()
        {
            // Find by name in case the cached reference was lost (script reload, etc.).
            if (_previewRoot == null)
            {
                for (int i = transform.childCount - 1; i >= 0; i--)
                {
                    var child = transform.GetChild(i);
                    if (child != null && child.name == PreviewRootName)
                        DestroyImmediate(child.gameObject);
                }
                return;
            }
            DestroyImmediate(_previewRoot);
            _previewRoot = null;
        }

        private void DestroyOwnedAssets()
        {
            if (_cuboidMesh != null) DestroyImmediate(_cuboidMesh);
            if (_octahedronMesh != null) DestroyImmediate(_octahedronMesh);
            if (_stellationMesh != null) DestroyImmediate(_stellationMesh);
            if (_blockMaterial != null) DestroyImmediate(_blockMaterial);
            if (_markerMaterial != null) DestroyImmediate(_markerMaterial);
            _cuboidMesh = _octahedronMesh = _stellationMesh = null;
            _blockMaterial = _markerMaterial = null;
            _meshShieldScale = -1f;
        }
#endif
    }
}
