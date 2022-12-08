using UnityEngine;

public class WarpFieldView : MonoBehaviour
{
    public int seed;

    [SerializeField] GameObject snow;
    [SerializeField] bool reset;
    [SerializeField] int nodesPerSide = 4;
    [SerializeField] float nodeScaler = 5f;

    GameObject TemporaryContainer;
    GameObject[,,] crystalLattice;
    WarpFieldData warpFieldData;
    Vector3 nodeDistance = Vector3.zero;

    private void OnEnable()
    {
        //Crystal.OnCrystalMove += ChangeSnowSize;
    }

    private void OnDisable()
    {
        //Crystal.OnCrystalMove -= ChangeSnowSize;
    }

    // Start is called before the first frame update
    void OnDrawGizmosSelected()
    {
        if (TemporaryContainer == null)
        {
            TemporaryContainer = new GameObject();
        }
        else
        {

            foreach (var element in TemporaryContainer.GetComponentsInChildren<Transform>())
            {
                if (element != TemporaryContainer.transform)
                {
                    element.gameObject.GetComponent<Renderer>().material = null;
                }
            }
        }

        warpFieldData = GetComponent<WarpFieldData>();
        warpFieldData.Init();
        warpFieldData.UpdateScriptableObjectValues();

        nodeDistance.x = warpFieldData.fieldWidth / nodesPerSide;
        nodeDistance.y = warpFieldData.fieldHeight / nodesPerSide;
        nodeDistance.z = warpFieldData.fieldThickness / nodesPerSide;

        MakeShards();
        ChangeShards();
    }

    void MakeShards()
    {
        if (crystalLattice == null || reset)
        {

            foreach (var element in GetComponentsInChildren<Transform>())
            {
                if (element != transform && element != snow.transform)
                {
                    element.SetParent(TemporaryContainer.transform);
                }
            }

            crystalLattice = new GameObject[nodesPerSide * 2, nodesPerSide * 2, nodesPerSide * 2];
            for (int x = -nodesPerSide; x < nodesPerSide; x++)
            {
                for (int y = -nodesPerSide; y < nodesPerSide; y++)
                {
                    for (int z = -nodesPerSide; z < nodesPerSide; z++)
                    {
                        GameObject tempSnow = Instantiate(snow);
                        tempSnow.transform.SetParent(transform, true);
                        crystalLattice[x + nodesPerSide, y + nodesPerSide, z + nodesPerSide] = tempSnow;
                    }
                }
            }
            reset = false;
        }
    }

    void ChangeShards()
    {
        int newSeed = seed;
        for (int x = 0; x < nodesPerSide * 2; x++)
        {
            for (int y = 0; y < nodesPerSide * 2; y++)
            {
                for (int z = 0; z < nodesPerSide * 2; z++)
                {
                    var node = crystalLattice[x, y, z].transform;
                    newSeed++;
                    Random.InitState(newSeed);

                    node.transform.position = new Vector3((x - nodesPerSide) * nodeDistance.x + Random.Range(-nodeDistance.x / 2, nodeDistance.x / 2),
                                                          (y - nodesPerSide) * nodeDistance.y + Random.Range(-nodeDistance.y / 2, nodeDistance.y / 2),
                                                          (z - nodesPerSide) * nodeDistance.z + Random.Range(-nodeDistance.z / 2, nodeDistance.z / 2));

                    Vector3 hybridVector = warpFieldData.HybridVector(node);

                    node.transform.localScale = Vector3.forward * nodeScaler * 3 +
                        Vector3.one * (hybridVector.magnitude * nodeScaler);
                    node.forward = -hybridVector;
                }
            }
        }
    }
}
