using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class shaderEffect : MonoBehaviour
{

    [SerializeField]
    Material MutonMaterial;

    Shader mutonShader;


    // Start is called before the first frame update
    void Start()
    {
        Shader mutonShader = MutonMaterial.shader;
    }

    // Update is called once per frame
    void Update()
    {
    Debug.Log("first shader property: " + mutonShader.GetPropertyName(1));
    
    }
}
