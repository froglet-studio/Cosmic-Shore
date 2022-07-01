using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StarChanger : MonoBehaviour
{
    Material starMaterial;
    Vector3 mutonPosition;
    [SerializeField]
    float scale;

    [SerializeField]
    GameObject Muton;

    // Start is called before the first frame update
    void Start()
    {
        starMaterial = gameObject.GetComponent<Renderer>().material;
    }

    // Update is called once per frame
    void Update()
    {
        mutonPosition = Vector3.Lerp(mutonPosition, Muton.transform.position*scale,.04f);
        starMaterial.SetVector("_mutonPosition", mutonPosition);
        //RenderSettings.skybox = starMaterial;
    }
}
