using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleMoveDontFWitMeG : MonoBehaviour
{
    public float speed = 5f;
    public Vector3 direction;


    // Update is called once per frame
    void Update()
    {
        transform.position += direction * speed * Time.deltaTime;
    }
}
