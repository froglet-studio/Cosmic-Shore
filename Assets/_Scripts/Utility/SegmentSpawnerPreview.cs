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
    /// and pick a preview shape in the inspector — the scene view will draw a gizmo
    /// for every block the spawner would instantiate, at the same position,
    /// rotation, and scale that <see cref="SpawnableWaypointTrack.Spawn"/> would
    /// assign.
    ///
    /// This is a pure visualization: no GameObjects are instantiated, no prism
    /// lifecycle runs, and the gizmo meshes are owned by the component (with
    /// <c>HideFlags.DontSave</c>) so they never persist in the scene.
    ///
    /// Currently understands <see cref="SpawnableWaypointTrack"/> (the HexRace
    /// track spawnable). Other <c>SpawnableBase</c> subclasses can be added by
    /// extending <see cref="DrawSpawnable"/>.
    ///
    /// Component is a no-op in player builds — body is wrapped in
    /// <c>#if UNITY_EDITOR</c>.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SegmentSpawner))]
    public class SegmentSpawnerPreview : MonoBehaviour
    {
        public enum PreviewShape
        {
            None = 0,           // Off — nothing drawn.
            Cuboid = 1,         // Plain prism box.
            Shielded = 2,       // Octahedron (PrismOctahedronShield runtime visual).
            SuperShielded = 3,  // Stellated octahedron (PrismStellatedOctahedronShield).
        }

        [Tooltip("Mesh shape drawn at each block position. None turns the preview off entirely. " +
                 "SuperShielded matches the HexRace default.")]
        [SerializeField] private PreviewShape preview = PreviewShape.SuperShielded;

        [Tooltip("Intensity level (1-4) to preview. The HexRace track shape varies per intensity.")]
        [SerializeField, Range(1, 4)] private int intensity = 1;

        [Tooltip("Color used for regular track block gizmos.")]
        [SerializeField] private Color blockColor = new(0.3f, 0.7f, 1f, 0.5f);

        [Tooltip("Color used for waypoint marker gizmos (the larger blocks at each waypoint).")]
        [SerializeField] private Color markerColor = new(1f, 0.7f, 0.2f, 0.7f);

        [Tooltip("Circumscribing scale factor for the octahedron / stellation. Match the runtime " +
                 "PrismOctahedronShield / PrismStellatedOctahedronShield to keep the preview honest.")]
        [SerializeField] private float shieldScale = OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;

#if UNITY_EDITOR
        // Cached gizmo meshes. Generated once per shape change at unit half-extents
        // (0.5) and scaled by each block's authored Scale at draw time. Owned by
        // this component so HideFlags.DontSave keeps them out of scene serialization.
        private Mesh _octahedronMesh;
        private Mesh _stellationMesh;
        private float _meshShieldScale = -1f; // sentinel: regenerate on first use

        private void OnDisable()
        {
            DestroyMeshes();
        }

        private void OnValidate()
        {
            // Regenerate meshes if the shield scale changed.
            if (!Mathf.Approximately(_meshShieldScale, shieldScale))
                DestroyMeshes();
        }

        private void OnDrawGizmos()
        {
            if (preview == PreviewShape.None) return;
            if (Application.isPlaying) return; // runtime SegmentSpawner spawns real prisms

            var spawner = GetComponent<SegmentSpawner>();
            if (spawner == null) return;

            EnsureMeshes();

            // Preview each spawnable at the SegmentSpawner's transform origin.
            // (LayoutSegment offsets per-segment at runtime, but for a clear
            // editor view we draw a single canonical layout — the user can
            // visually replicate it across the StraightLineLength axis if
            // they need to see the stacked tracks.)
            Vector3 worldOrigin = spawner.transform.position + spawner.origin;

            foreach (var spawnable in spawner.EnumerateSpawnables())
                DrawSpawnable(spawnable, worldOrigin);
        }

        private void DrawSpawnable(SpawnableBase spawnable, Vector3 worldOrigin)
        {
            if (spawnable is SpawnableWaypointTrack waypointTrack)
            {
                foreach (var block in waypointTrack.GetPreviewBlocks(intensity))
                    DrawBlock(block, worldOrigin);
            }
            // Other SpawnableBase subclasses can hook in here as their preview
            // helpers come online. Falling through silently is intentional —
            // a missing handler should not crash the editor.
        }

        private void DrawBlock(in SpawnableWaypointTrack.PreviewBlock block, Vector3 worldOrigin)
        {
            Gizmos.color = block.IsMarker ? markerColor : blockColor;
            Vector3 worldPos = worldOrigin + block.Position;

            switch (preview)
            {
                case PreviewShape.Cuboid:
                    // Use the gizmo matrix so the cube respects the block's rotation.
                    var prevMatrix = Gizmos.matrix;
                    Gizmos.matrix = Matrix4x4.TRS(worldPos, block.Rotation, Vector3.one);
                    Gizmos.DrawCube(Vector3.zero, block.Scale);
                    Gizmos.matrix = prevMatrix;
                    break;
                case PreviewShape.Shielded:
                    Gizmos.DrawMesh(_octahedronMesh, worldPos, block.Rotation, block.Scale);
                    break;
                case PreviewShape.SuperShielded:
                    Gizmos.DrawMesh(_stellationMesh, worldPos, block.Rotation, block.Scale);
                    break;
            }
        }

        private void EnsureMeshes()
        {
            // Generate at unit half-extents (0.5); per-block scale is applied via
            // Gizmos.DrawMesh's scale argument. World-space size therefore matches
            // the block's authored Scale exactly. (Runtime uses BoxCollider.size as
            // the half-extent source, which is typically ≈ 1.0 — within ~2.5% of
            // unit cube on the ShieldedSpawnablePrism prefab and exact on Manta
            // Prism. Close enough for a layout preview.)
            Vector3 unitHalfExtents = Vector3.one * 0.5f;

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
        }

        private void DestroyMeshes()
        {
            if (_octahedronMesh != null) DestroyImmediate(_octahedronMesh);
            _octahedronMesh = null;
            if (_stellationMesh != null) DestroyImmediate(_stellationMesh);
            _stellationMesh = null;
            _meshShieldScale = -1f;
        }
#endif
    }
}
