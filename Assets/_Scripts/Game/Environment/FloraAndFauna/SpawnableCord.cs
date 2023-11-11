using CosmicShore.Core;
using System.Collections.Generic;
using UnityEngine;

public struct Cord
{
    public Queue<int> EnergizedQueue;
    public Vector3[] Vertices;
    public LineRenderer LineRendererInstance;
    public Vector3[] Velocities; // Added for momentum

    public Cord(int verticesCount)
    {
        EnergizedQueue = new Queue<int>();
        Vertices = new Vector3[verticesCount];
        LineRendererInstance = null; // This can be initialized elsewhere
        Velocities = new Vector3[verticesCount];
    }

    // You can add methods related to the Cord behavior here, for example:
    public void UpdateVertexPosition(int vertexIndex, Vector3 offset)
    {
        // add momentum
        Vertices[vertexIndex] += offset + .5f*Velocities[vertexIndex];

        // conserve momentum
        Velocities[vertexIndex] += offset;

        // Dampen momentum
        Velocities[vertexIndex] *= 0.85f;
    }
}

public class SpawnableCord : SpawnableAbstractBase
{
    [SerializeField] TrailBlock healthBlock;
    [SerializeField] Vector3 blockScale;
    [SerializeField] Material lineMaterial;

    [SerializeField] int blockCount = 50;
    [SerializeField] int verticesCount = 200;
    [SerializeField] float length = 150;
    static int ObjectsSpawned = 0;

    List<Cord> Cords = new List<Cord>();

    float equilibriumDistanceSqr;
    float tolerance = .01f;

    GameObject container;
    LineRenderer lineRenderer = new();

    private void Start()
    {
        equilibriumDistanceSqr = Mathf.Pow(length / verticesCount, 2);
    }

    public override GameObject Spawn()
    {
        //List<Vector3> vertices;
        

        container = new GameObject();
        container.name = "Cord" + ObjectsSpawned++;

        Cord newCord = new Cord(verticesCount);

        var trail = new Trail();
        Vector3[] vertices = new Vector3[verticesCount];

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
           
            var t = (float)vertex / verticesCount * Mathf.PI * 12;
            var x = (Mathf.Sin(t) * xc1) + (Mathf.Sin(t * xc2 + xc3) * xc4);
            var y = (Mathf.Cos(t) * yc1) + (Mathf.Cos(t * yc2 + yc3) * yc4);
            var position = new Vector3(x, y, t * length / (Mathf.PI * 12));

            vertices[vertex] = position;

            lineRenderer.SetPosition(vertex, position);

            int block;
            if (vertex%(verticesCount/blockCount) == 0)
            {
                
                block = vertex / (verticesCount/blockCount);
                var lookPosition = (block == 0) ? position : trail.GetBlock(block - 1).transform.position;
                CreateBlock(position, lookPosition, container.name + "::BLOCK::" + block, trail, blockScale, healthBlock, container);
            } 
             
        }
        newCord.LineRendererInstance = lineRenderer;
        newCord.Vertices = vertices;
        Cords.Add(newCord);

        trails.Add(trail);
        return container;
    }

    void CheckForEnergy(int cord, int vertexIndex)
    {
        bool qued = false;

        Vector3 previousVector = Cords[cord].Vertices[vertexIndex - 1] - Cords[cord].Vertices[vertexIndex];
        float previousDistanceSqr = previousVector.sqrMagnitude;

        Vector3 nextVector = Cords[cord].Vertices[vertexIndex + 1] - Cords[cord].Vertices[vertexIndex];
        float nextDistanceSqr = nextVector.sqrMagnitude;

        Vector3 offset = Vector3.zero;

        if (previousDistanceSqr - equilibriumDistanceSqr > tolerance)
        {
            offset += previousVector;
            if (vertexIndex > 1) Cords[cord].EnergizedQueue.Enqueue(vertexIndex - 1);
            Cords[cord].EnergizedQueue.Enqueue(vertexIndex);
            qued = true;  
        }
        else if (previousDistanceSqr - equilibriumDistanceSqr < -tolerance)
        {
            offset -= previousVector;
            if (vertexIndex > 1) Cords[cord].EnergizedQueue.Enqueue(vertexIndex - 1);
            Cords[cord].EnergizedQueue.Enqueue(vertexIndex);
            qued = true;
        }

        if (nextDistanceSqr - equilibriumDistanceSqr > tolerance)
        {
            offset += nextVector;
            if (vertexIndex < Cords[cord].Vertices.Length - 1) Cords[cord].EnergizedQueue.Enqueue(vertexIndex + 1);
            if (!qued)
            {
                qued = true;
                Cords[cord].EnergizedQueue.Enqueue(vertexIndex);
            }
        }
        else if (nextDistanceSqr - equilibriumDistanceSqr < -tolerance)
        {
            offset -= nextVector;
            if (vertexIndex < Cords[cord].Vertices.Length - 1) Cords[cord].EnergizedQueue.Enqueue(vertexIndex + 1);
            if (!qued)
            {
                qued = true;
                Cords[cord].EnergizedQueue.Enqueue(vertexIndex);
            }
        }

        if (qued)
        {
            Cords[cord].UpdateVertexPosition(vertexIndex, offset * Time.deltaTime);
            var newPosition = Cords[cord].Vertices[vertexIndex];
            Cords[cord].LineRendererInstance.SetPosition(vertexIndex, newPosition);
            if (vertexIndex % (verticesCount / blockCount) == 0)
            {
                var blockIndex = vertexIndex / (verticesCount / blockCount);
                trails[cord].GetBlock(blockIndex).transform.localPosition = newPosition;
            }
        }
    }

    private void Update()
    {
        
        for (var cord = 0; cord < Cords.Count; cord++)
        {
            // Driven vertex is first
            int drivenIndex = 15;
            float amplitude = 20;
            Cords[cord].Vertices[drivenIndex] = amplitude * Mathf.Sin((float)Time.frameCount / 100) * Vector3.forward;
            Cords[cord].LineRendererInstance.SetPosition(drivenIndex, Cords[cord].Vertices[drivenIndex]);
            //// check adjacent to driven
            if (Mathf.Abs((Cords[cord].Vertices[drivenIndex + 1] - Cords[cord].Vertices[drivenIndex]).sqrMagnitude - equilibriumDistanceSqr) > tolerance )
            {
                CheckForEnergy(cord, drivenIndex + 1);
            }
            //check the queue
            int initialCount = Mathf.Min(Cords[cord].EnergizedQueue.Count, 300);
            
            for (int i = 0; i < initialCount; i++)
            {
                int item = Cords[cord].EnergizedQueue.Dequeue();
                CheckForEnergy(cord, item);
            }
        }
    }
}