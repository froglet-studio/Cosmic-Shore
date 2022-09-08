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
        var deltaTime = Time.deltaTime;
        if (Time.timeScale == 0)
            deltaTime = Time.fixedDeltaTime;//.16f;    // This is here so that bedazzled items still work if the game is paused - e.g during the rewarded ad screen

        increment += .2f*deltaTime;
        increment %= 1;
        hue = increment * TWO_PI;
        hue = Mathf.Sin(hue) * hueMagnitude + hueMidPoint;
        image.color = Color.HSVToRGB(hue, 1, 1);
    }
}