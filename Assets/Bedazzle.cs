using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Bedazzle : MonoBehaviour
{
    private Image image;
    private float value;
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
        value = increment*Mathf.PI*2;
        value = Mathf.Sin(value)/6+.63f;
        image.color = Color.HSVToRGB(value, 1, 1);
    }
}
