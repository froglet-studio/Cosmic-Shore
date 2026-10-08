using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Swaps this renderer's mesh for the crease-edge-baked twin that the charge discharge needs to
    /// run vertex to vertex (see <see cref="CrystalEdgeArcMeshBaker"/> for the channel contract).
    /// Two shaders read it: <c>Shader Graphs/ChargeCrystal</c> on the charge crystal, and
    /// <c>Custom/OmniChargeEdgesShader</c> on the omni crystal's pentagons.
    ///
    /// The baked mesh is built once per (source mesh, plate filter) and SHARED, so a scene full of
    /// crystals pays for one bake and keeps one mesh — no per-instance geometry, no batching loss, and
    /// nothing per frame. The swap is a one-shot in <c>Awake</c>; the crystal itself is static.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [DisallowMultipleComponent]
    public class CrystalEdgeArcs : MonoBehaviour
    {
        [Tooltip("0 bakes every plate (the charge crystal). Otherwise only the plates with exactly this many distinct corners are kept - a k-gonal prism has 2k - so the discharge runs on one family of an exploded crystal's plates. The omni crystal uses 10: its 12 pentagonal prisms, the shapes that stand for Charge.")]
        [SerializeField, Min(0)] int plateCorners;

        /// <summary>The plate filter this renderer bakes with, so a reader of the PREFAB (which has
        /// not run <c>Awake</c>) can ask the baker for the same cached twin an instance draws.</summary>
        public int PlateCorners => plateCorners;

        void Awake()
        {
            var filter = GetComponent<MeshFilter>();
            var source = filter.sharedMesh;
            if (source == null) return;

            // Already baked (a pooled crystal re-awakening on the shared mesh).
            if (source.name.EndsWith(CrystalEdgeArcMeshBaker.BakedSuffix)) return;

            var baked = CrystalEdgeArcMeshBaker.GetOrBake(source, plateCorners);
            if (baked != null) filter.sharedMesh = baked;
        }
    }
}
