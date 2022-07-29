using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Bedazzle : MonoBehaviour
{
    private Image image;
    private float hue;
    private float speed;

    private float increment;

    // Start is called before the first frame update
    void Start()
    {
        image = GetComponent<Image>();
    }

    // Update is called once per frame
    void Update()
    {
        increment += .2f*Time.deltaTime;
        increment = increment % 1;
        hue = increment*Mathf.PI*2;
        hue = Mathf.Sin(hue)/17f+.53f;
        image.color = Color.HSVToRGB(hue, 1, 1);
    }
}
