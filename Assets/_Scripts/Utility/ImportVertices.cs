using UnityEngine;
using System.IO;
using Newtonsoft.Json;

public class ImportVertices : MonoBehaviour
{
    public string path = "C:\\Users\\gradi\\Documents\\Froglet\\test\\vertices.json"; // Path to the exported vertices
    public Material lineMaterial; // Material for Line Renderer

    void Start()
    {
        string json = File.ReadAllText(path);
        float[][] verticesArray = JsonConvert.DeserializeObject<float[][]>(json);
        Vector3[] vertices = new Vector3[verticesArray.Length];

        for (int i = 0; i < verticesArray.Length; i++)
        {
            vertices[i] = new Vector3(verticesArray[i][0], verticesArray[i][1], verticesArray[i][2]);
        }

        LineRenderer lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.material = lineMaterial;
        lineRenderer.startWidth = 0.01f;
        lineRenderer.endWidth = 0.01f;
        lineRenderer.positionCount = vertices.Length;

        for (int i = 0; i < vertices.Length; i++)
        {
            lineRenderer.SetPosition(i, vertices[i]);
        }
    }
}