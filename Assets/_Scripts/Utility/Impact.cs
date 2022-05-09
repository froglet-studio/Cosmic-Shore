using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Impact : MonoBehaviour
{
    [SerializeField]
    Material MutonMaterial;

    
    //public Vector3 velocity = new Vector3(1,0,0);
    //public float magnitude = 5;


    // Start is called before the first frame update
    void Start()
    {
        //StartCoroutine(ImpactCoroutine(velocity,));
    }

    public IEnumerator ImpactCoroutine(Vector3 velocity, Material material)
    {
        var velocityScale = .01f;
        while (velocityScale <= 1)
        {
            yield return new WaitForSeconds(.001f);
            velocityScale *= 1.015f;
            material.SetVector("_velocity", velocityScale*velocity);
            material.SetFloat("_opacity", 1-velocityScale);
        }
        

    }
}