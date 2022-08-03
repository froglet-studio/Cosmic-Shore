using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SnowChanger : MonoBehaviour
{
    [SerializeField] GameObject muton;
    [SerializeField] GameObject snow;

    GameObject[,,] crystalLattice;

    int crystalSize = 10;
    float nodeScaler = 4;
    int nodedistance = 50;
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
        sphereDiameter = sphereScaler * muton.GetComponent<MutonPopUp>().sphereRadius;

        crystalLattice = new GameObject[crystalSize * 2, crystalSize * 2, crystalSize * 2];

        for (int x = -crystalSize; x < crystalSize; x++)
        {
            for (int y = -crystalSize; y < crystalSize; y++)
            {
                for (int z = -crystalSize; z < crystalSize; z++)
                {
                    GameObject tempSnow = Instantiate(snow);
                    tempSnow.transform.SetParent(transform, true);
                    tempSnow.transform.localScale = Vector3.one * nodeScaler;
                    tempSnow.transform.position = new Vector3(x * nodedistance + Random.Range(-nodedistance/2, nodedistance / 2),
                                                              y * nodedistance + Random.Range(-nodedistance / 2, nodedistance / 2),
                                                              z * nodedistance + Random.Range(-nodedistance / 2, nodedistance / 2));
                    
                    crystalLattice[x + crystalSize, y + crystalSize, z + crystalSize] = tempSnow;
                }
            }
        }
        ChangeSnowSize();
    }

    void ChangeSnowSize()
    {
        float nodeScalerOverThree = nodeScaler / 3;

        for (int x = 0; x < crystalSize*2; x++)
        {
            for (int y = 0; y < crystalSize*2; y++)
            {
                for (int z = 0; z < crystalSize*2; z++)
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
