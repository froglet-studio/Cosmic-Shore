using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class FancyCamController : MonoBehaviour
{
    [SerializeField] bool canRotate;
    [SerializeField] bool keyboardTranslation;
    [SerializeField] bool mouseRotation;
    [SerializeField] bool gamepadTargetControls;
    [SerializeField] bool mouseTargetControls;
    [SerializeField] Key RotationToggleKey = Key.LeftBracket;
    bool isRotating = false;
    public float speed = 10;
    public Transform target;
    float followDistance = 10;

    void Update()
    {
        if (canRotate)
        {
            if (Keyboard.current[RotationToggleKey].wasPressedThisFrame) isRotating = !isRotating;

            if (isRotating) transform.Rotate(Vector3.up * (speed * Time.deltaTime));
        }

        if (keyboardTranslation)
        {
            if (Keyboard.current.wKey.isPressed) transform.Translate(transform.forward * (speed * Time.deltaTime));
            if (Keyboard.current.sKey.isPressed) transform.Translate(-transform.forward * (speed * Time.deltaTime));
            if (Keyboard.current.aKey.isPressed) transform.Translate(-transform.right * (speed * Time.deltaTime));
            if (Keyboard.current.dKey.isPressed) transform.Translate(transform.right * (speed * Time.deltaTime));
            if (Keyboard.current.qKey.isPressed) transform.Translate(-transform.up * (speed * Time.deltaTime));
            if (Keyboard.current.eKey.isPressed) transform.Translate(transform.up * (speed * Time.deltaTime));
        }

        if (mouseRotation)
        {
            if (Mouse.current.leftButton.isPressed) transform.Rotate(Vector3.up * (speed * Time.deltaTime * Mouse.current.delta.ReadValue().x));
            if (Mouse.current.rightButton.isPressed) transform.Rotate(Vector3.right * (speed * Time.deltaTime * Mouse.current.delta.ReadValue().y));
        }

        if (target != null && !gamepadTargetControls)
        {
            transform.LookAt(target);
        }

        // this code uses the left stick to move the camera toward a target and roll the camera
        if (target != null && gamepadTargetControls)
        {
            transform.LookAt(target);

            followDistance += Gamepad.current.rightTrigger.ReadValue() * 10;
            followDistance -= Gamepad.current.leftTrigger.ReadValue() * 10;
            transform.position = Vector3.Lerp(transform.position, target.position - (transform.forward * followDistance), Time.deltaTime * 10);

            transform.RotateAround(target.position, transform.forward, Gamepad.current.rightStick.ReadValue().x * speed * Time.deltaTime);

            transform.RotateAround(target.position, transform.up, Gamepad.current.leftStick.ReadValue().x * speed * Time.deltaTime);
            transform.RotateAround(target.position, transform.right, Gamepad.current.leftStick.ReadValue().y * speed * Time.deltaTime);

        }

        // this code uses the mouse to move the camera toward a target and roll the camera
        if (target != null && mouseTargetControls)
        {
            transform.LookAt(target);

            followDistance += Mouse.current.scroll.ReadValue().y * 10;
            transform.position = Vector3.Lerp(transform.position, target.position - (transform.forward * followDistance), Time.deltaTime * 10);

            // this sets a condition that the mouse must be pressed switcch between x controlling roll and x controlling yaw
            if (Mouse.current.leftButton.isPressed)
            {
                transform.RotateAround(target.position, transform.forward, Mouse.current.delta.ReadValue().x * speed * Time.deltaTime);
            }
            else transform.RotateAround(target.position, transform.up, Mouse.current.delta.ReadValue().x * speed * Time.deltaTime);

            transform.RotateAround(target.position, transform.right, Mouse.current.delta.ReadValue().y * speed * Time.deltaTime);
        }
    }
}
