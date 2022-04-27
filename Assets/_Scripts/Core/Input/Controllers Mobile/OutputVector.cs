using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OutputVector : MonoBehaviour
{
    public float length = 5;
    public float radius = 1;

    public Transform needle;

    // Start is called before the first frame update
    void Start()
    {
      
        needle.localScale = new Vector3(length, radius, radius);
    }

    // Update is called once per frame
    void Update()
    {
        var t = Time.time;
        needle.Rotate(UnityEngine.Input.gyro.gravity, t*.05f);


    }
}
