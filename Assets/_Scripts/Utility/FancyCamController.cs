using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Gamepad = UnityEngine.InputSystem.Gamepad;

public class FancyCamController : MonoBehaviour
{

    public bool canRotate = false;
    public float speed = 10;

    void Update()
    {

        //if (Gamepad.current.leftShoulder.wasPressedThisFrame) canRotate = !canRotate;
        if (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame) canRotate = !canRotate;

        if (canRotate) transform.Rotate(speed * Vector3.up * Time.deltaTime);
    }
}