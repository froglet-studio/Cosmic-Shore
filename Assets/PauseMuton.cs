using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PauseMuton : MonoBehaviour 
{
    Material material;
    float timer = 0;

    // Start is called before the first frame update
    void Start()
    {
        material = GetComponent<MeshRenderer>().material;
        transform.localScale = new Vector3(.75f, .75f, .75f);
    }

    // Update is called once per frame
    void Update()
    {
        timer += .02f;
        var timer2 = timer/360f;
        transform.Rotate(timer2, timer2, timer2);
        material.SetFloat("_Timer", timer);
    }
}
