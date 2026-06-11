using UnityEngine;
using System.Collections.Generic;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    public class TrailViewer : MonoBehaviour
    {
        public Material TransparentMaterial;
        public int radiusInBlocks = 5;
        private TrailFollower trailFollower;
        private LineRenderer lineRenderer;
        [SerializeField] Material trailViewerMaterial;
        private Trail attachedTrail;

        List<Prism> transparentBlocks = new();
        //List<Material> savedMaterials = new();
        Dictionary<Prism, Material> originalMaterials = new Dictionary<Prism, Material>();


        void Start()
        {
            trailFollower = GetComponent<TrailFollower>();
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.material = trailViewerMaterial;
            lineRenderer.startWidth = lineRenderer.endWidth = 0.1f;
        }


        void Update()
        {
            // Restore last frame's transparent blocks to their original materials.
            foreach (Prism block in transparentBlocks)
            {
                if (block == null || !block.TryGetComponent(out Renderer renderer)) continue;
                if (renderer.sharedMaterial.Equals(TransparentMaterial) && originalMaterials.TryGetValue(block, out var original))
                    renderer.sharedMaterial = original;
            }
            transparentBlocks.Clear();

            lineRenderer.enabled = false;

            if (!trailFollower.IsAttached) return;


            attachedTrail = trailFollower.AttachedPrism.Trail;
            int attachedBlockIndex = trailFollower.AttachedPrism.prismProperties.Index;


            // Set materials of blocks in view distance
            for (int i = attachedBlockIndex - radiusInBlocks; i < attachedBlockIndex + radiusInBlocks; i++)
            {
                if (i >= attachedTrail.TrailList.Count - 1 || i <= 0) continue;
                var block = attachedTrail.TrailList[i];
                if (!block.TryGetComponent(out Renderer renderer)) continue;
                if (!renderer.sharedMaterial.Equals(TransparentMaterial))
                {
                    originalMaterials[block] = renderer.sharedMaterial;
                    renderer.sharedMaterial = TransparentMaterial;
                    transparentBlocks.Add(block);
                }
            }

            // Draw line
            lineRenderer.positionCount = transparentBlocks.Count;
            for (int i = 0; i < transparentBlocks.Count; i++)
            {
                lineRenderer.SetPosition(i, transparentBlocks[i].transform.position);
            }
            lineRenderer.enabled = true;

        }
    }
}
