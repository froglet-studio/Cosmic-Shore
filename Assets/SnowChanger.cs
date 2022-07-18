using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SnowChanger : MonoBehaviour
{

    [SerializeField]
    Material material;

    [SerializeField]
    GameObject muton;

    [SerializeField]
    GameObject snow;

    GameObject[,,] crystalLattice;

    //List<Material> materials = new List<Material>();

    int crystalSize = 15;
    float nodeScaler = 4;
    int nodedistance = 40;
    //int shellCount = 20;
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

        //for (int i = 0; i < shellCount; i++)
        //{
        //    Material tempMaterial;
        //    tempMaterial = new Material(material);
        //    tempMaterial.SetFloat("_opacity", ((float)shellCount - i)/shellCount+.1f);
        //    materials.Add(tempMaterial);
        //}

        crystalLattice = new GameObject[crystalSize * 2, crystalSize * 2, crystalSize * 2];

        for (int x = -crystalSize; x < crystalSize; x++)
        {
            for (int y = -crystalSize; y < crystalSize; y++)
            {
                for (int z = -crystalSize; z < crystalSize; z++)
                {
                    //GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    GameObject tempSnow = Instantiate(snow);
                    tempSnow.transform.SetParent(transform, true);
                    tempSnow.transform.localScale = Vector3.one * nodeScaler;
                    tempSnow.transform.position = new Vector3(x * nodedistance, y * nodedistance, z * nodedistance);
                    tempSnow.GetComponent<Renderer>().material = material;
                    crystalLattice[x + crystalSize, y + crystalSize, z + crystalSize] = tempSnow;
                }
            }
        }
        ChangeSnowSize();
    }


    void ChangeSnowSize()
    {
        for (int x = -crystalSize; x < crystalSize; x++)
        {
            for (int y = -crystalSize; y < crystalSize; y++)
            {
                for (int z = -crystalSize; z < crystalSize; z++)
                {
                    float clampedDistance = Mathf.Clamp(
                        (crystalLattice[x + crystalSize, y + crystalSize, z + crystalSize].transform.position - muton.transform.position).magnitude,0, sphereDiameter);
                    //Debug.Log($"snow:xyz {x},{y},{z}");
                    float normalizedDistance = clampedDistance / sphereDiameter;

                    //Debug.Log($"snow.materialindex: {(int)inverseDistance * 10 / (int)sphereDiameter}");

                    //crystalLattice[x + crystalSize, y + crystalSize, z + crystalSize].transform.localScale =
                    //    ((sphereDiameter - clampedDistance)  * nodeScaler / sphereDiameter + nodeSize) * Vector3.one;

                    crystalLattice[x + crystalSize, y + crystalSize, z + crystalSize].transform.localScale =
                        (normalizedDistance  * nodeScaler + nodeSize) * Vector3.forward + Vector3.one*nodeSize;

                    crystalLattice[x + crystalSize, y + crystalSize, z + crystalSize].transform.LookAt(muton.transform);
                    //crystalLattice[x + crystalSize, y + crystalSize, z + crystalSize].GetComponent<Renderer>().material =
                    //    materials[((int)clampedDistance * (shellCount-1)) / (int)sphereDiameter];
                }
            }
        }
    }
}
