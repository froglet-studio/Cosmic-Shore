using UnityEngine;
using UnityEngine.InputSystem.Controls;

public class FancyCamController : MonoBehaviour
{
    public bool canRotate = false;
    public float speed = 10;
    public KeyControl toggleKey = UnityEngine.InputSystem.Keyboard.current.leftBracketKey;

    void Update()
    {
        if (toggleKey.wasPressedThisFrame) canRotate = !canRotate;

        if (canRotate) transform.Rotate(speed * Vector3.up * Time.deltaTime);
    }
}