using CosmicShore.Game.IO;
using CosmicShore.SOAP;
using CosmicShore.Utilities;
using UnityEngine;


namespace CosmicShore.Game
{
    public interface IInputStatus
    {
        static ScreenOrientation CurrentOrientation;

        ScriptableEventInputEvents OnButtonPressed {get;}
        ScriptableEventInputEvents OnButtonReleased {get;}
        
        InputController InputController { get; set; }

        // Floats
        float XSum { get; set; }
        float YSum { get; set; }
        float XDiff { get; set; }
        float YDiff { get; set; }
        float Throttle { get; set; }

        // Booleans
        bool Idle { get; set; }
        bool Paused { get; set; }
        bool IsGyroEnabled { get; set; }
        bool InvertYEnabled { get; set; }
        bool InvertThrottleEnabled { get; set; }
        bool OneTouchLeft { get; set; }
        bool CommandStickControls { get; set; }

        // Vectors
        Vector2 RightJoystickHome { get; set; }
        Vector2 LeftJoystickHome { get; set; }
        Vector2 RightClampedPosition { get; set; }
        Vector2 LeftClampedPosition { get; set; }
        Vector2 RightJoystickStart { get; set; }
        Vector2 LeftJoystickStart { get; set; }
        Vector2 RightNormalizedJoystickPosition { get; set; }
        Vector2 LeftNormalizedJoystickPosition { get; set; }
        Vector2 EasedRightJoystickPosition { get; set; }
        Vector2 EasedLeftJoystickPosition { get; set; }
        Vector2 SingleTouchValue { get; set; }
        Vector3 ThreeDPosition { get; set; }

        Quaternion GetGyroRotation();
        void ResetForReplay();
    }

}