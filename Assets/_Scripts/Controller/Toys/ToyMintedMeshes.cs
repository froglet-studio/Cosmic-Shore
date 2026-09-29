using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Owns the meshes a toy model MINTED for itself (a procedural hull's pieces, built from the
    /// prefab asset by <see cref="ToyModelBuilder.HarvestProceduralHulls"/>). A harvested model
    /// otherwise draws only shared project meshes and owns nothing; a minted mesh with no owner
    /// outlives every model that used it. Destroyed with the model, in play mode and in the
    /// editor alike.
    /// </summary>
    public sealed class ToyMintedMeshes : MonoBehaviour
    {
        readonly List<Mesh> _meshes = new();

        public void Adopt(List<Mesh> meshes)
        {
            if (meshes == null) return;
            for (int i = 0; i < meshes.Count; i++)
                if (meshes[i]) _meshes.Add(meshes[i]);
        }

        void OnDestroy()
        {
            for (int i = 0; i < _meshes.Count; i++)
            {
                if (!_meshes[i]) continue;
                if (Application.isPlaying) Destroy(_meshes[i]);
                else DestroyImmediate(_meshes[i]);
            }
            _meshes.Clear();
        }
    }
}
