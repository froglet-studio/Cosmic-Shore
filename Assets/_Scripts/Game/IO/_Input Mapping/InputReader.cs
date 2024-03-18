using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using TouchPhase = UnityEngine.TouchPhase;

namespace CosmicShore
{
    [CreateAssetMenu(fileName = "InputReaderGamepad", menuName = "CosmicShore/InputReader/Gamepad")]
    public class InputReaderGamepad : InputReader
    {
        protected override Vector2 ProcessLeftControl()
        {
            return Ease(Gamepad.current.leftStick.ReadValue());
        }

        protected override Vector2 ProcessRightControl()
        {
            return Ease(Gamepad.current.rightStick.ReadValue());
        }
    }

    [CreateAssetMenu(fileName = "InputReaderTouchpad", menuName = "CosmicShore/InputReader/Touchpad")]
    public class InputReaderTouchpad : InputReader
    {
        private bool _isFumble => Input.touchCount > 2;
        private bool _isSingleTouch => Input.touchCount == 1;
        private bool _isDualTouch => Input.touchCount == 2;

        private int _left;
        private int _right;
        
        
        private float TouchRaidus = Screen.dpi;

        public void OnDisable()
        {
            AssignControls();
        }

        private void AssignControls()
        {
            if (_isFumble)
            {
                _left = GetClosestTouch(PreviousLeftControl);
                _right = GetClosestTouch(PreviousRightControl);
            }
            
            if(_isDualTouch)
            {
                if (Input.touches.First().position.x <= Input.touches[1].position.x)
                {
                    _left = 0;
                    _right = 1;
                }
                else
                {
                    _left = 1;
                    _right = 0;
                }
            }
            
            if (_isSingleTouch)
            {
                _left = 0;
            }
            
        }
        
        void HandleTouch(ref Vector2 previousPosition, ref Vector2 currentPosition, ref Vector2 clampedPosition, int touchIndex)
        {
            Touch touch = Input.touches[touchIndex];

            // We check for Vector2.zero since this is the default (i.e uninitialized) value for Vec2
            // Otherwise, if we missed the TouchPhase.Began event (like before a minigame starts),
            // we always end up with the joystick as a JoystickRadius long vector
            // starting at the bottom left corner and pointing toward the touch position
            if (touch.phase == TouchPhase.Began || previousPosition == Vector2.zero)
                previousPosition = touch.position;

            var offset = touch.position - previousPosition;
            var clampedOffset = Vector2.ClampMagnitude(offset, TouchRaidus); 
            clampedPosition = previousPosition + clampedOffset;
            var normalizedOffset = clampedOffset / TouchRaidus;
            currentPosition = normalizedOffset;
        }
        
        
        private int GetClosestTouch(Vector2 target)
        {
            int touchIndex = 0;
            float minDistance = Screen.dpi;
            
            for (int i = 0; i < Input.touches.Length; i++)
            {
                if (Vector2.Distance(target, Input.touches[i].position) < minDistance)
                {
                    minDistance = Vector2.Distance(target, Input.touches[i].position);
                    touchIndex = i;
                }
            }

            return touchIndex;
        }
    }

    [CreateAssetMenu(fileName = "InputReaderKeyboardMouse", menuName = "CosmicShore/InputReader/KeyboardMouse")]
    public class InputReaderKeyboardMouse : InputReader
    {
        
    }

    public class InputReader : ScriptableObject, IFlight
    {
        public Vector2 LeftControl => ProcessLeftControl();
        public Vector2 RightControl => ProcessRightControl();
        
        public Vector2 LeftClampedPosition { get; protected set; }
        public Vector2 RightClampedPosition { get;  protected set;}


        protected Vector2 PreviousLeftControl;
        protected Vector2 PreviousRightControl;

        private const float QauterPi = Mathf.PI / 4.0f;

        public void Enable()
        {
            // 
        }
        
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
        Vector2 LeftClampedPosition { get; }
        Vector2 RightClampedPosition { get; }
        void Enable();
    }
}
