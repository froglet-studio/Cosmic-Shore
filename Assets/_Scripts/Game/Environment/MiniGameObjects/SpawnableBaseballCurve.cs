using CosmicShore.Core;
using UnityEngine;

public class SpawnableBaseballCurve : SpawnableAbstractBase
{
    [SerializeField] TrailBlock trailBlock;
    static int SpawnedCount = 0;

    public float radius = 1.0f;
    public int numSegments = 16;
    public float seamWidth = 0.2f;
    public GameObject blockPrefab;

    public float b = 0.5f;
    public float c = 0.75f;


    public override GameObject Spawn()
    {
        SpawnedCount++;
        GameObject container = new GameObject();
        container.name = "Baseball" + SpawnedCount++;

        for (int i = 0; i < numSegments; i++)
        {
            float t = i / (float)numSegments * 2.0f * Mathf.PI;
            float x = radius * Mathf.Cos(Mathf.PI / 2.0f - c) * Mathf.Cos(t) * Mathf.Cos(t / 2.0f + c * Mathf.Sin(2.0f * t));
            float y = radius * Mathf.Cos(Mathf.PI / 2.0f - c) * Mathf.Cos(t) * Mathf.Sin(t / 2.0f + c * Mathf.Sin(2.0f * t));
            float z = radius * Mathf.Sin(Mathf.PI / 2.0f - c) * Mathf.Cos(t);

            var go = Instantiate(blockPrefab);
            go.transform.position = new Vector3(x, y, z);
            go.transform.SetParent(container.transform, false);

            go = Instantiate(blockPrefab);
            go.transform.position = new Vector3(x, y, z + seamWidth);
            go.transform.SetParent(container.transform, false);

        }

        return container;
    }
}