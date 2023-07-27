using UnityEngine;
using System.Collections.Generic;
using StarWriter.Core;
using System;

public class TrailViewer : MonoBehaviour
{
    public Material TransparentMaterial;
    public Material OpaqueMaterial;
    public int radiusInBlocks = 5;
    private TrailFollower trailFollower;
    private LineRenderer lineRenderer;
    private Trail attachedTrail;

    List<TrailBlock> transparentBlocks = new List<TrailBlock>();

    void Start()
    {
        trailFollower = GetComponent<TrailFollower>();
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = lineRenderer.endColor = Color.green;
        lineRenderer.startWidth = lineRenderer.endWidth = 0.1f;
    }

    void Update()
    {
        if (transparentBlocks != null)
        {
            foreach (TrailBlock block in transparentBlocks)
            {
                if (attachedTrail.TrailList.Contains(block))
                {
                    block.GetComponent<Renderer>().material = OpaqueMaterial;
                }
            }
            transparentBlocks.Clear();
        }
        lineRenderer.enabled = false;

        if (!trailFollower.IsAttached) return;


        attachedTrail = trailFollower.AttachedTrailBlock.Trail;

        //attachedTrail.TrailList[trailFollower.AttachedTrailBlock.Index].GetComponent<Renderer>().material = TransparentMaterial;



        int attachedBlockIndex = trailFollower.AttachedTrailBlock.Index;

        // Reset materials of previously transparent blocks



        // Set materials of blocks in view distance
        for (int i = attachedBlockIndex - radiusInBlocks; i < attachedBlockIndex + radiusInBlocks; i++)
        {
            if (i >= attachedTrail.TrailList.Count - 1 || i <= 0) continue;
            attachedTrail.TrailList[i].GetComponent<Renderer>().material = TransparentMaterial;
            transparentBlocks.Add(attachedTrail.TrailList[i]);
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