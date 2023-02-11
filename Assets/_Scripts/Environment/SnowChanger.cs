using UnityEngine;

public class SnowChanger : MonoBehaviour
{
    [SerializeField] GameObject Crystal;
    [SerializeField] GameObject snow;
    [SerializeField] int nodesPerSide = 7;

    GameObject[,,] crystalLattice;
    readonly float nodeScaler = 10;
    readonly int crystalSideLength = 500;
    readonly float nodeSize = .25f;
    readonly float sphereScaler = 2;
    int nodeDistance;
    float sphereDiameter;
    Vector3 origin = Vector3.zero;

    void OnEnable()
    {
        global::Crystal.OnCrystalMove += ChangeSnowSize;
    }

    void OnDisable()
    {
        global::Crystal.OnCrystalMove -= ChangeSnowSize;
    }

    void Start()
    {
        // TODO: this should be injected by the node, but that's not working at the moment :/
        origin = transform.parent.position;

        nodeDistance = crystalSideLength/nodesPerSide;

        sphereDiameter = sphereScaler * Crystal.GetComponent<Crystal>().sphereRadius;

        crystalLattice = new GameObject[nodesPerSide * 2, nodesPerSide * 2, nodesPerSide * 2];

        for (int x = -nodesPerSide; x < nodesPerSide; x++)
        {
            for (int y = -nodesPerSide; y < nodesPerSide; y++)
            {
                for (int z = -nodesPerSide; z < nodesPerSide; z++)
                {
                    GameObject tempSnow = Instantiate(snow);
                    tempSnow.transform.SetParent(transform, true);
                    tempSnow.transform.localScale = Vector3.one * nodeScaler;
                    tempSnow.transform.position = origin + new Vector3(x * nodeDistance + Random.Range(-nodeDistance/2, nodeDistance / 2),
                                                                       y * nodeDistance + Random.Range(-nodeDistance / 2, nodeDistance / 2),
                                                                       z * nodeDistance + Random.Range(-nodeDistance / 2, nodeDistance / 2));
                    
                    crystalLattice[x + nodesPerSide, y + nodesPerSide, z + nodesPerSide] = tempSnow;
                }
            }
        }
        ChangeSnowSize();
    }

    void ChangeSnowSize()
    {
        float nodeScalerOverThree = nodeScaler / 3;

        for (int x = 0; x < nodesPerSide*2; x++)
        {
            for (int y = 0; y < nodesPerSide*2; y++)
            {
                for (int z = 0; z < nodesPerSide*2; z++)
                {
                    var node = crystalLattice[x, y, z];
                    float clampedDistance = Mathf.Clamp(
                        (node.transform.position - Crystal.transform.position).magnitude, 0, sphereDiameter);

                    float normalizedDistance = clampedDistance / sphereDiameter;

                    node.transform.localScale =
                        Vector3.forward * (normalizedDistance  * nodeScaler + nodeSize) + 
                        Vector3.one * (normalizedDistance * nodeScalerOverThree + nodeSize);

                    node.transform.LookAt(Crystal.transform);
                }
            }
        }
    }
    
    public void SetOrigin(Vector3 origin)
    {
        this.origin = origin;
    }
}