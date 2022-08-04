using UnityEngine;
using UnityEngine.UI;

public class Bedazzle : MonoBehaviour
{
    Image image;
    float hue;
    float increment;
    float hueMidPoint = .53f;
    float hueMagnitude = .06f;
    const float TWO_PI = Mathf.PI * 2;

    // Start is called before the first frame update
    void Start()
    {
        image = GetComponent<Image>();
    }

    // Update is called once per frame
    void Update()
    {
        increment += .2f*Time.deltaTime;
        increment %= 1;
        hue = increment * TWO_PI;
        hue = Mathf.Sin(hue) * hueMagnitude + hueMidPoint;
        image.color = Color.HSVToRGB(hue, 1, 1);
    }
}