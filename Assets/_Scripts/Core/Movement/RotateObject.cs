using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateObject : MonoBehaviour
{
    public float speed = 2;
    public Vector3 rotationDirection = Vector3.right;

    // Update is called once per frame
    void Update()
    {
        float speedT = speed * Time.deltaTime;
        transform.Rotate(rotationDirection.x * speedT, rotationDirection.y * speedT, rotationDirection.z * speedT);
    }
}
