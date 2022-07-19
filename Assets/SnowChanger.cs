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

    int crystalSize = 11;
    float nodeScaler = 4;
    int nodedistance = 50;
    //int shellCount = 20;
    float nodeSize = .15f;
    float sphereScaler = 2;


    Color green = new Color(0, .4f, .6f);
    Color blue = new Color(.18f, .18f, .58f);
    Color red = new Color(.28f, 0, .52f);
    Color starColor;
    Color fuelColor;


    float sphereDiameter;

    private void OnEnable()
    {
        FuelSystem.onFuelChange += UpdateFuelLevel;
        MutonPopUp.OnMutonMove += ChangeSnowSize;
    }

    private void OnDisable()
    {
        FuelSystem.onFuelChange -= UpdateFuelLevel;
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

        fuelColor = green;

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
                    tempSnow.transform.position = new Vector3(x * nodedistance + Random.Range(-nodedistance/2, nodedistance / 2),
                                                              y * nodedistance + Random.Range(-nodedistance / 2, nodedistance / 2),
                                                              z * nodedistance + Random.Range(-nodedistance / 2, nodedistance / 2));
                    material.SetColor("_color", green);
                    tempSnow.GetComponent<Renderer>().material = material;

                    
                    

                    crystalLattice[x + crystalSize, y + crystalSize, z + crystalSize] = tempSnow;
                }
            }
        }
        ChangeSnowSize();
    }


    public void UpdateFuelLevel(string uuid, float amount)
    {

        if (amount > .55f) fuelColor = green;
        else if (amount > .23) fuelColor = blue;
        else fuelColor = red;
        //starMaterial.SetFloat("_fuel", amount);
    }

    void ChangeSnowSize()
    {
        for (int x = -crystalSize; x < crystalSize; x++)
        {
            for (int y = -crystalSize; y < crystalSize; y++)
            {
                for (int z = -crystalSize; z < crystalSize; z++)
                {
                    var node = crystalLattice[x + crystalSize, y + crystalSize, z + crystalSize];
                    float clampedDistance = Mathf.Clamp(
                        (node.transform.position - muton.transform.position).magnitude,0, sphereDiameter);
                    //Debug.Log($"snow:xyz {x},{y},{z}");
                    float normalizedDistance = clampedDistance / sphereDiameter;

                    //Debug.Log($"snow.materialindex: {(int)inverseDistance * 10 / (int)sphereDiameter}");

                    //crystalLattice[x + crystalSize, y + crystalSize, z + crystalSize].transform.localScale =
                    //    ((sphereDiameter - clampedDistance)  * nodeScaler / sphereDiameter + nodeSize) * Vector3.one;

                    node.transform.localScale =
                        (normalizedDistance  * nodeScaler + nodeSize) * Vector3.forward + Vector3.one* (normalizedDistance * nodeScaler/3 + nodeSize);

                    node.transform.LookAt(muton.transform);

                    starColor = Color.Lerp(starColor, fuelColor, .02f);

                    node.GetComponent<Renderer>().material.SetColor("_color", starColor);

                    //crystalLattice[x + crystalSize, y + crystalSize, z + crystalSize].GetComponent<Renderer>().material =
                    //    materials[((int)clampedDistance * (shellCount-1)) / (int)sphereDiameter];
                }
            }
        }
    }
}
