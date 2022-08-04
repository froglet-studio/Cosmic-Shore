using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SnowChanger : MonoBehaviour
{
    [SerializeField] GameObject muton;
    [SerializeField] GameObject snow;

    GameObject[,,] crystalLattice;

    [SerializeField] int nodesPerSide = 5;
    float nodeScaler = 4;
    int crystalSideLength = 500;
    int nodedistance;
    float nodeSize = .15f;
    float sphereScaler = 2;
    float sphereDiameter;

    private void OnEnable()
    {
        MutonPopUp.OnMutonMove += ChangeSnowSize;
    }

    private void OnDisable()
    {
        MutonPopUp.OnMutonMove -= ChangeSnowSize;
    }

    // Start is called before the first frame update
    void Start()
    {
        nodedistance = crystalSideLength/nodesPerSide;

        sphereDiameter = sphereScaler * muton.GetComponent<MutonPopUp>().sphereRadius;

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
                    tempSnow.transform.position = new Vector3(x * nodedistance + Random.Range(-nodedistance/2, nodedistance / 2),
                                                              y * nodedistance + Random.Range(-nodedistance / 2, nodedistance / 2),
                                                              z * nodedistance + Random.Range(-nodedistance / 2, nodedistance / 2));
                    
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
                        (node.transform.position - muton.transform.position).magnitude, 0, sphereDiameter);

                    float normalizedDistance = clampedDistance / sphereDiameter;

                    node.transform.localScale =
                        Vector3.forward * (normalizedDistance  * nodeScaler + nodeSize) + 
                        Vector3.one * (normalizedDistance * nodeScalerOverThree + nodeSize);

                    node.transform.LookAt(muton.transform);
                }
            }
        }
    }
}
