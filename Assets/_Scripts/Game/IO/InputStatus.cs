using CosmicShore.Game.IO;
using CosmicShore.SOAP;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// DEPRECATED - Use NetworkInputStatus instead
    /// </summary>
    public class InputStatus : MonoBehaviour, IInputStatus
    {
        public static ScreenOrientation CurrentOrientation;

        [SerializeField]
        ScriptableEventInputEvents _onButtonPressed;
        public ScriptableEventInputEvents OnButtonPressed => _onButtonPressed;
        
        [SerializeField]
        ScriptableEventInputEvents _onButtonReleased;
        public ScriptableEventInputEvents OnButtonReleased => _onButtonReleased;
        
        public InputController InputController { get; set; }

        public float XSum { get; set; }
        public float YSum {get; set;}
        public float XDiff {get; set;}
        public float YDiff {get; set;}
        public float Throttle { get; set; }
        public bool Idle {get; set;}
        public bool Paused {get; set;}
        public bool IsGyroEnabled {get; set;}
        public bool InvertYEnabled {get; set;}
        public bool InvertThrottleEnabled {get; set;}
        public bool OneTouchLeft {get; set;}
        public bool CommandStickControls { get; set; }
        public Vector2 SingleTouchValue {get; set;}
        public Vector3 ThreeDPosition {get; set;}
        public Vector2 RightJoystickHome {get; set;}
        public Vector2 LeftJoystickHome {get; set;}
        public Vector2 RightClampedPosition {get; set;}
        public Vector2 LeftClampedPosition {get; set;}
        public Vector2 RightJoystickStart {get; set;}
        public Vector2 LeftJoystickStart {get; set;}
        public Vector2 RightNormalizedJoystickPosition {get; set;}
        public Vector2 LeftNormalizedJoystickPosition {get; set;}
        public Vector2 EasedRightJoystickPosition {get; set;}
        public Vector2 EasedLeftJoystickPosition {get; set;}

        public Quaternion GetGyroRotation() => InputController.GetGyroRotation();
    }
}