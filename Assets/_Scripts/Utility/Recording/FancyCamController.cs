using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class FancyCamController : MonoBehaviour
{
    [SerializeField] bool canRotate;
    [SerializeField] bool keyboardControl;
    [SerializeField] bool gamepadControl;
    [SerializeField] bool mouseControl;
    [SerializeField] bool gamepadRotationControls;
    bool isRotating = false;
    public float speed = 10;
    public KeyControl toggleKey = UnityEngine.InputSystem.Keyboard.current.leftBracketKey;
    public Transform target;

    void Update()
    {
        if (canRotate)
        {
            if (toggleKey.wasPressedThisFrame) isRotating = !isRotating;

            if (isRotating) transform.Rotate(speed * Vector3.up * Time.deltaTime);
        }

        if (keyboardControl)
        {
            if (Keyboard.current.wKey.isPressed) transform.Translate(speed * transform.forward * Time.deltaTime);
            if (Keyboard.current.sKey.isPressed) transform.Translate(speed * -transform.forward * Time.deltaTime);
            if (Keyboard.current.aKey.isPressed) transform.Translate(speed * -transform.right * Time.deltaTime);
            if (Keyboard.current.dKey.isPressed) transform.Translate(speed * transform.right * Time.deltaTime);
            if (Keyboard.current.qKey.isPressed) transform.Translate(speed * -transform.up * Time.deltaTime);
            if (Keyboard.current.eKey.isPressed) transform.Translate(speed * transform.up * Time.deltaTime);
        }

        if (gamepadControl)
        {
            if (Gamepad.current.leftStick.up.isPressed) transform.Translate(speed * transform.forward * Time.deltaTime);
            if (Gamepad.current.leftStick.down.isPressed) transform.Translate(speed * -transform.forward * Time.deltaTime);
            if (Gamepad.current.leftStick.left.isPressed) transform.Translate(speed * -transform.forward * Time.deltaTime);
            if (Gamepad.current.leftStick.right.isPressed) transform.Translate(speed * transform.right * Time.deltaTime);
            if (Gamepad.current.leftShoulder.isPressed) transform.Translate(speed * -transform.forward * Time.deltaTime);
            if (Gamepad.current.rightShoulder.isPressed) transform.Translate(speed * transform.up * Time.deltaTime);
        }

        if (mouseControl)
        {
            if (Mouse.current.leftButton.isPressed) transform.Translate(speed * transform.forward * Time.deltaTime);
            if (Mouse.current.rightButton.isPressed) transform.Translate(speed * -transform.forward * Time.deltaTime);
            if (Mouse.current.middleButton.isPressed) transform.Translate(speed * -transform.right * Time.deltaTime);
            if (Mouse.current.forwardButton.isPressed) transform.Translate(speed * transform.right * Time.deltaTime);
            if (Mouse.current.backButton.isPressed) transform.Translate(speed * -transform.up * Time.deltaTime);
            if (Mouse.current.scroll.y.ReadValue() > 0) transform.Translate(speed * transform.up * Time.deltaTime);
            if (Mouse.current.scroll.y.ReadValue() < 0) transform.Translate(speed * -transform.forward * Time.deltaTime);
        }

        if (mouseControl && Mouse.current.leftButton.isPressed)
        {
            transform.Translate(speed * transform.forward * Time.deltaTime * Mouse.current.delta.ReadValue().y);
            transform.Translate(speed * -transform.forward * Time.deltaTime * Mouse.current.delta.ReadValue().x);
        }

        if (target != null && !gamepadRotationControls)
        {
            transform.LookAt(target);
        }

        if (gamepadRotationControls)
        {
            transform.Rotate(speed * transform.up * Time.deltaTime * Gamepad.current.rightStick.ReadValue().x);
            transform.Rotate(speed * -transform.forward * Time.deltaTime * Gamepad.current.rightStick.ReadValue().y);
        }
        // this code uses the left stick to move the camera toward a target and roll the camera
        if (target != null && gamepadRotationControls)
        {
            transform.LookAt(target);
            transform.Translate(speed * transform.forward * Time.deltaTime * Gamepad.current.leftStick.ReadValue().y);
            transform.Rotate(speed * transform.up * Time.deltaTime * Gamepad.current.leftStick.ReadValue().x);
        }
    }
}
