using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StarChanger : MonoBehaviour
{

    private void OnEnable()
    {
        FuelSystem.onFuelChange += UpdateFuelLevel;
    }

    private void OnDisable()
    {
        FuelSystem.onFuelChange -= UpdateFuelLevel;
    }

    Material starMaterial;
    Vector3 mutonPosition;

    [SerializeField]
    GameObject Muton;

    Color green = new Color(0, .4f, .6f);
    Color blue = new Color(.18f, .18f, .58f);
    Color red = new Color(.28f, 0, .52f);
    Color starColor;
    Color fuelColor;


    // Start is called before the first frame update
    void Start()
    {
        starMaterial = gameObject.GetComponent<Renderer>().material;
        starMaterial.SetColor("_color", green);
        fuelColor = green;

    }

    // Update is called once per frame
    void Update()
    {
        mutonPosition = Vector3.Lerp(mutonPosition, Muton.transform.position,.02f);
        starColor = Color.Lerp(starColor,fuelColor,.02f);
        starMaterial.SetColor("_color", starColor);
        starMaterial.SetVector("_mutonPosition", mutonPosition);
        //RenderSettings.skybox = starMaterial;
    }

    public void UpdateFuelLevel(string uuid, float amount)
    {
        
        if (amount > .55f) fuelColor = green;
        else if (amount > .23) fuelColor = blue;
        else fuelColor = red;
        //starMaterial.SetFloat("_fuel", amount);
    }

}
