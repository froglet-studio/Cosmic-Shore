using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore
{
    [CreateAssetMenu(fileName = "InputReaderGamepad", menuName = "CosmicShore/Input Reader")]
    public class InputReaderGamepad : InputReader
    {
        protected override Vector2 ProcessLeftControl()
        {
            return Gamepad.current.leftStick.ReadValue();
        }

        protected override Vector2 ProcessRightControl()
        {
            return Gamepad.current.rightStick.ReadValue();
        }
    }

    [CreateAssetMenu(fileName = "InputReaderTouchpad", menuName = "CosmicShore/Input Reader")]
    public class InputReaderTouchpad : InputReader
    {

    }

    [CreateAssetMenu(fileName = "InputReaderKeyboardMouse", menuName = "CosmicShore/Input Reader")]
    public class InputReaderKeyboardMouse : InputReader
    {
        
    }

    public class InputReader : ScriptableObject, IFlight
    {
        public Vector2 LeftControl => ProcessLeftControl();
        public Vector2 RightControl => ProcessRightControl();

        private const float QauterPi = Mathf.PI / 4.0f;
        protected virtual Vector2 ProcessLeftControl()
        {
            return Vector2.zero;
        }

        protected virtual Vector2 ProcessRightControl()
        {
            return Vector2.zero;
        }

        protected Vector2 Ease(Vector2 input)
        {
            return new Vector2(Ease(input.x), Ease(input.y));
        }

        protected float Ease(float input)
        {
            return input < 0 
                ? Mathf.Cos(input* QauterPi) - 1 
                : -(Mathf.Cos(input* QauterPi) - 1);
        }
}
    public interface IFlight
    {
        Vector2 LeftControl { get; }
        Vector2 RightControl { get; }
    }
}
