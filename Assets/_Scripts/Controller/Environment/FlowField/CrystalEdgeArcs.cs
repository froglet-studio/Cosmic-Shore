using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Swaps this renderer's mesh for the crease-edge-baked twin that the
    /// <c>Shader Graphs/ChargeCrystal</c> shader needs to run its vertex-to-vertex plasma
    /// discharge (see <see cref="CrystalEdgeArcMeshBaker"/> for the channel contract).
    ///
    /// The baked mesh is built once per source mesh and SHARED, so a scene full of crystals
    /// pays for one bake and keeps one mesh — no per-instance geometry, no batching loss, and
    /// nothing per frame. The swap is a one-shot in <c>Awake</c>; the crystal itself is static
    /// (its spread is the model's own 60 pentagonal prisms, not a shader displacement).
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [DisallowMultipleComponent]
    public class CrystalEdgeArcs : MonoBehaviour
    {
        void Awake()
        {
            var filter = GetComponent<MeshFilter>();
            var source = filter.sharedMesh;
            if (source == null) return;

            // Already baked (a pooled crystal re-awakening on the shared mesh).
            if (source.name.EndsWith("(EdgeArcs)")) return;

            var baked = CrystalEdgeArcMeshBaker.GetOrBake(source);
            if (baked != null) filter.sharedMesh = baked;
        }
    }
}
