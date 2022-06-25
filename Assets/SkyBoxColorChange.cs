using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkyBoxColorChange : MonoBehaviour
{
    Material skyBoxMaterial;
    float hue;

    // Start is called before the first frame update
    void Start()
    {
    skyBoxMaterial = gameObject.GetComponent<Renderer>().material;   
    }

    // Update is called once per frame
    void Update()
    {
        hue += Time.deltaTime*.1f;
        skyBoxMaterial.SetFloat("_hue", hue);
        RenderSettings.skybox = skyBoxMaterial;
    }
}
