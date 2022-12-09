using UnityEngine;

public class StarChanger : MonoBehaviour
{
    [SerializeField] GameObject Crystal;

    Material starMaterial;
    Vector3 crystalPosition;
    Color green = new Color(0, .4f, .6f);
    Color blue = new Color(.18f, .18f, .58f);
    Color red = new Color(.28f, 0, .52f);
    Color starColor;
    Color fuelColor;

    private void OnEnable()
    {
        FuelSystem.OnFuelChange += UpdateFuelLevel;
    }

    private void OnDisable()
    {
        FuelSystem.OnFuelChange -= UpdateFuelLevel;
    }

    void Start()
    {
        starMaterial = gameObject.GetComponent<Renderer>().material;
        starMaterial.SetColor("_color", green);
        fuelColor = green;
    }

    void Update()
    {
        crystalPosition = Vector3.Lerp(crystalPosition, Crystal.transform.position,.02f);
        starColor = Color.Lerp(starColor, fuelColor, .02f);
        starMaterial.SetColor("_color", starColor);
        starMaterial.SetVector("_mutonPosition", crystalPosition);
    }

    public void UpdateFuelLevel(float amount)
    {
        if (amount > .55f) fuelColor = green;
        else if (amount > .23) fuelColor = blue;
        else fuelColor = red;
    }
}