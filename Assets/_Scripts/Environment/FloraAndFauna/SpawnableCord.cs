using StarWriter.Core;
using System.Collections.Generic;
using UnityEngine;

public class SpawnableCord : SpawnableAbstractBase
{
    [SerializeField] TrailBlock healthBlock;
    [SerializeField] Vector3 blockScale;
    [SerializeField] Material lineMaterial;

    [SerializeField] int blockCount = 50;
    [SerializeField] int verticesCount = 200;
    [SerializeField] float length = 150;
    static int ObjectsSpawned = 0;

    List<List<Bone>> ListofBoneLists = new List<List<Bone>>();
    List <LineRenderer> lineRenderers= new List<LineRenderer>();

    List<Bone> bones;
    GameObject container;
    LineRenderer lineRenderer = new();
    int verticesPerBone = 1;



    public override GameObject Spawn()
    {
        bones = new List<Bone>();
        container = new GameObject();
        container.name = "Cord" + ObjectsSpawned++;

        var trail = new Trail();
        Vector3[] vertices = new Vector3[verticesCount];

        // Create the bone structure
        int bonesCount = verticesCount / verticesPerBone;
        
        Bone previousBone = null;
        for (int i = 0; i < bonesCount; i++)
        {
            Transform boneTransform = new GameObject("Bone" + i).transform;
            boneTransform.SetParent(container.transform);
            Bone bone = new Bone(boneTransform, previousBone);
            bones.Add(bone);
            previousBone = bone;
        }

        var xc1 = Random.Range(4, 16);
        var xc2 = Random.Range(.2f, 2);
        var xc3 = Random.Range(-5, 5);
        var xc4 = Random.Range(1, 3);
        var yc1 = Random.Range(4, 16);
        var yc2 = Random.Range(.2f, 2);
        var yc3 = Random.Range(-5, 5);
        var yc4 = Random.Range(1, 3);
    
        lineRenderer = container.AddComponent<LineRenderer>();
        //lineRenderer.material = lineMaterial;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.green;
        lineRenderer.endColor = Color.blue;
        lineRenderer.startWidth = 0.5f;
        lineRenderer.endWidth = 2f;
        lineRenderer.positionCount = vertices.Length;
        lineRenderer.transform.SetParent(container.transform, false);
        lineRenderer.useWorldSpace = false;

        for (int vertex = 0; vertex < verticesCount; vertex++)
        {
            // Determine the influencing bone based on vertex position
            Bone influencingBone = bones[vertex / verticesPerBone];

            // Get position from the bone and adjust using the original logic if needed
            

            var t = (float)vertex / verticesCount * Mathf.PI * 12;
            var x = (Mathf.Sin(t) * xc1) + (Mathf.Sin(t * xc2 + xc3) * xc4);
            var y = (Mathf.Cos(t) * yc1) + (Mathf.Cos(t * yc2 + yc3) * yc4);
            var position = new Vector3(x, y, t * length / (Mathf.PI * 12));

            influencingBone.Transform.position = position;

            lineRenderer.SetPosition(vertex, position);

            int block;
            if (vertex%(verticesCount/blockCount) == 0)
            {
                
                block = vertex / (verticesCount/blockCount);
                var lookPosition = (block == 0) ? position : trail.GetBlock(block - 1).transform.position;
                CreateBlock(position, lookPosition, container.name + "::BLOCK::" + block, trail, blockScale, healthBlock, container);
            } 

        }

        // Animate bones to create the sway effect
        foreach (var bone in bones)
        {
            bone.Animate(Time.deltaTime);
        }

        ListofBoneLists.Add(bones);
        lineRenderers.Add(lineRenderer);

        trails.Add(trail);
        return container;
        
    }

    private void Update()
    {
 
        for (var i = 0; i < ListofBoneLists.Count; i++)
        {
            foreach (var bone in ListofBoneLists[i])
            {
                bone.Animate(Time.deltaTime);
            }

            // Update the vertex positions of the LineRenderer based on the bones' positions
            for (int vertex = 0; vertex < verticesCount; vertex++)
            {
                Bone influencingBone = ListofBoneLists[i][vertex / verticesPerBone];
                Vector3 position = influencingBone.Transform.position;
                lineRenderers[i].SetPosition(vertex, position);
                lineRenderers[i].widthCurve = lineRenderer.widthCurve;
                if (vertex % (verticesCount / blockCount) == 0)
                {
                    var block = vertex / (verticesCount / blockCount);
                    trails[i].GetBlock(block).transform.localPosition = position;
                }
            }
        }
    }
}