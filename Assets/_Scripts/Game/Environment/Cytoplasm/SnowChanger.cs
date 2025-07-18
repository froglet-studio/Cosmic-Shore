using CosmicShore.Environment.FlowField;
using UnityEngine;

public class SnowChanger : MonoBehaviour
{
    public GameObject Crystal;
    [SerializeField] GameObject snow;
    [SerializeField] Vector3 crystalSize = new Vector3(500, 500, 500);
    [SerializeField] int shardDistance = 100;

    [Header("Optional Fields")]
    [SerializeField] bool lookAt;
    [SerializeField] Vector3 targetAxis;
    [SerializeField] Vector3 newOrigin;
    

    GameObject[,,] crystalLattice;
    readonly float nodeScaler = 10;
    readonly float nodeSize = .25f;
    readonly float sphereScaler = 2;
    int shardsX;
    int shardsY;
    int shardsZ;
    float sphereDiameter;
    Vector3 origin = Vector3.zero;

    void OnEnable()
    {
        global::CosmicShore.Environment.FlowField.Crystal.OnCrystalMove += ChangeSnowSize;
    }

    void OnDisable()
    {
        global::CosmicShore.Environment.FlowField.Crystal.OnCrystalMove -= ChangeSnowSize;
    }

    void Start()
    {
        // TODO: this should be injected by the node, but that's not working at the moment :/
        origin = newOrigin;

        shardsX = (int)(crystalSize.x / shardDistance);
        shardsY = (int)(crystalSize.y / shardDistance);
        shardsZ = (int)(crystalSize.z / shardDistance);

        if (Crystal != null) sphereDiameter = sphereScaler * Crystal.GetComponent<Crystal>().sphereRadius;

        crystalLattice = new GameObject[shardsX * 2 + 1, shardsY * 2 + 1, shardsZ * 2 + 1]; // both sides of each axis plus the midplane

        for (int x = -shardsX; x <= shardsX; x++)
        {
            for (int y = -shardsY; y <= shardsY; y++)
            {
                for (int z = -shardsZ; z <= shardsZ; z++)
                {
                    GameObject tempSnow = Instantiate(snow, transform, true);
                    tempSnow.transform.localScale = Vector3.one * nodeScaler;
                    tempSnow.transform.position = origin + new Vector3(x * shardDistance + Random.Range(-shardDistance / 2, shardDistance / 2),
                                                                       y * shardDistance + Random.Range(-shardDistance / 2, shardDistance / 2),
                                                                       z * shardDistance + Random.Range(-shardDistance / 2, shardDistance / 2));
                    
                    crystalLattice[x + shardsX, y + shardsY, z + shardsZ] = tempSnow;
                }
            }
        }
        ChangeSnowSize();
    }

    void ChangeSnowSize()
    {
        float nodeScalerOverThree = nodeScaler / 3;

        for (int x = 0; x < shardsX * 2 +1; x++)
        {
            for (int y = 0; y < shardsY * 2 + 1; y++)
            {
                for (int z = 0; z < shardsZ * 2 + 1; z++)
                {
                    var shard = crystalLattice[x, y, z];
                    float normalizedDistance;
                    if (Crystal != null)
                    { 
                        float clampedDistance = Mathf.Clamp(
                        (shard.transform.position - Crystal.transform.position).magnitude, 0, sphereDiameter);
                        normalizedDistance = clampedDistance / sphereDiameter;

                        shard.transform.LookAt(Crystal.transform);
                    }
                    else
                    {
                        var reject = shard.transform.position - (Vector3.Dot(shard.transform.position, targetAxis.normalized) * targetAxis.normalized);
                        var maxDistance = Mathf.Max(shardsX, shardsY) * shardDistance;
                        float clampedDistance = Mathf.Clamp(reject.magnitude, 0, maxDistance);
                        normalizedDistance = clampedDistance / maxDistance;

                        if (lookAt) shard.transform.rotation = Quaternion.LookRotation(-reject.normalized);
                        else shard.transform.rotation = Quaternion.LookRotation(targetAxis);
                    }
                    shard.transform.localScale =
                        Vector3.forward * (normalizedDistance * nodeScaler + nodeSize) +
                        Vector3.one * (normalizedDistance * nodeScalerOverThree + nodeSize);

                }       
            }
        }
    }
    
    public void SetOrigin(Vector3 origin)
    {
        this.origin = origin;
    }
}