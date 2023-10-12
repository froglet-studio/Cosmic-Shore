using UnityEngine;
using UnityEngine.InputSystem;

namespace Scenes.TestInput
{
    public class ControlsManager : MonoBehaviour
    {
        private ControlsManagerAsset controlsManagerAsset;

        private Vector2 leftJoyStickVector2;
        private Vector2 rightJoyStickVector2 ;

        private void OnEnable()
        {
            controlsManagerAsset ??= new ControlsManagerAsset();
            controlsManagerAsset.Enable();
            controlsManagerAsset.PlayerControls.LeftControls.performed += context => OnLeftControlsPerformed(context.ReadValue<Vector2>());
            controlsManagerAsset.PlayerControls.RightControls.performed += context => OnRightControlsPerformed(context.ReadValue<Vector2>());
        }

        private void OnDisable()
        {
            controlsManagerAsset?.Disable();
        }

        private void OnLeftControlsPerformed(Vector2 leftVector2)
        {
            leftJoyStickVector2 = leftVector2;
            Debug.Log($"Left joy stick x: {leftJoyStickVector2.x}, y: {leftJoyStickVector2.y}");
            
        }

        private void OnRightControlsPerformed(Vector2 rightVector2)
        {
            rightJoyStickVector2 = rightVector2;
            Debug.Log($"Right joystick x: {rightJoyStickVector2.x} y: {rightJoyStickVector2.y}");
        }
    }
}
