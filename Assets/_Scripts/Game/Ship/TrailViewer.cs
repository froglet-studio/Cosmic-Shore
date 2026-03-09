using UnityEngine;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Utility;


public class TrailViewer : MonoBehaviour
{
    public Material TransparentMaterial;
    public int radiusInBlocks = 5;
    private TrailFollower trailFollower;
    private LineRenderer lineRenderer;
    [SerializeField] Material trailViewerMaterial;
    private Trail attachedTrail;

    List<Prism> transparentBlocks = new();
    Dictionary<Prism, Material> originalMaterials = new Dictionary<Prism, Material>();

    // Cache renderers to avoid repeated GetComponent calls every frame
    Dictionary<Prism, Renderer> rendererCache = new Dictionary<Prism, Renderer>();

    void Start()
    {
        trailFollower = GetComponent<TrailFollower>();
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.material = trailViewerMaterial;
        lineRenderer.startWidth = lineRenderer.endWidth = 0.1f;
    }

    Renderer GetCachedRenderer(Prism block)
    {
        if (!rendererCache.TryGetValue(block, out var rend))
        {
            rend = block.GetComponent<Renderer>();
            if (rend) rendererCache[block] = rend;
        }
        return rend;
    }

    void Update()
    {
        if (transparentBlocks != null)
        {
            foreach (Prism block in transparentBlocks)
            {
                var rend = GetCachedRenderer(block);
                if (rend && rend.sharedMaterial.Equals(TransparentMaterial))
                {
                    rend.sharedMaterial = originalMaterials[block];
                }
            }
            transparentBlocks.Clear();
        }

        lineRenderer.enabled = false;

        if (!trailFollower.IsAttached) return;


        attachedTrail = trailFollower.AttachedPrism.Trail;
        int attachedBlockIndex = trailFollower.AttachedPrism.prismProperties.Index;


        // Set materials of blocks in view distance
        for (int i = attachedBlockIndex - radiusInBlocks; i < attachedBlockIndex + radiusInBlocks; i++)
        {
            if (i >= attachedTrail.TrailList.Count - 1 || i <= 0) continue;
            var block = attachedTrail.TrailList[i];
            var rend = GetCachedRenderer(block);
            if (rend && !rend.sharedMaterial.Equals(TransparentMaterial))
            {
                var tempMaterial = rend.material;
                rend.sharedMaterial = TransparentMaterial;
                transparentBlocks.Add(block);
                originalMaterials[block] = tempMaterial;
            }
        }

        // Draw line — use indexed loop instead of IndexOf which is O(n) per call
        lineRenderer.positionCount = transparentBlocks.Count;
        for (int i = 0; i < transparentBlocks.Count; i++)
        {
            lineRenderer.SetPosition(i, transparentBlocks[i].transform.position);
        }
        lineRenderer.enabled = true;
    }
}
