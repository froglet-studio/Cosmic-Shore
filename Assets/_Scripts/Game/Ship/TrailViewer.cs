using UnityEngine;
using System.Collections.Generic;
using CosmicShore.Core;
using System;
using System.Linq;


public class TrailViewer : MonoBehaviour
{
    public Material TransparentMaterial;
    public int radiusInBlocks = 5;
    private TrailFollower trailFollower;
    private LineRenderer lineRenderer;
    [SerializeField] Material trailViewerMaterial;
    private Trail attachedTrail;

    List<TrailBlock> transparentBlocks = new();
    //List<Material> savedMaterials = new();
    Dictionary<TrailBlock, Material> originalMaterials = new Dictionary<TrailBlock, Material>();


    void Start()
    {
        trailFollower = GetComponent<TrailFollower>();
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.material = trailViewerMaterial;
        lineRenderer.startWidth = lineRenderer.endWidth = 0.1f;
    }


    void Update()
    {
        if (transparentBlocks != null)
        {
            Debug.Log("Number of entries in originalMaterials: " + originalMaterials.Count);
            foreach (TrailBlock block in transparentBlocks)
            {
                if (block.GetComponent<Renderer>().sharedMaterial.Equals(TransparentMaterial))
                {
                    Debug.Log("restoring material");
                    block.GetComponent<Renderer>().sharedMaterial = originalMaterials[block];  // Retrieve the original material
                }
            }
            transparentBlocks.Clear();
        }

        lineRenderer.enabled = false;

        if (!trailFollower.IsAttached) return;


        attachedTrail = trailFollower.AttachedTrailBlock.Trail;
        int attachedBlockIndex = trailFollower.AttachedTrailBlock.Index;


        // Set materials of blocks in view distance
        for (int i = attachedBlockIndex - radiusInBlocks; i < attachedBlockIndex + radiusInBlocks; i++)
        {
            Material tempMaterial;
            if (i >= attachedTrail.TrailList.Count - 1 || i <= 0) continue;
            var block = attachedTrail.TrailList[i];
            if (!block.GetComponent<Renderer>().sharedMaterial.Equals(TransparentMaterial))
            {
                Debug.Log("set material");
                tempMaterial = (block.GetComponent<Renderer>().material);
                block.GetComponent<Renderer>().sharedMaterial = TransparentMaterial;
                transparentBlocks.Add(block);
                originalMaterials[block] = tempMaterial;
            }
        }

        // Draw line
        lineRenderer.positionCount = transparentBlocks.Count;
        foreach (TrailBlock block in transparentBlocks)
        {
            lineRenderer.SetPosition(transparentBlocks.IndexOf(block), block.transform.position);
        }
        lineRenderer.enabled = true;

    }
}