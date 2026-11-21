using System;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;
namespace CosmicShore.Gameplay
{
    public interface IInputStatus
    {
        public event Action<bool> OnToggleInputPaused;
        
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
        float LeftTriggerAnalog { get; set; }
        float RightTriggerAnalog { get; set; }

        // Booleans
        bool Idle { get; set; }
        bool Paused { get; set; }
        bool IsGyroEnabled { get; set; }
        bool InvertYEnabled { get; set; }
        bool InvertThrottleEnabled { get; set; }
        bool OneTouchLeft { get; set; }
        bool CommandStickControls { get; set; }
        InputDeviceType ActiveInputDevice { get; set; }

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

        /// <summary>
        /// Roll held as an ANGLE rather than a rate: while true, the hull's roll since this went true is
        /// <see cref="HeldRollDegrees"/> (clockwise from behind positive), one to one - the phone held like
        /// an augmented-reality window (the Dolphin's aim). Owner-local, never replicated: the hull's pose
        /// already replicates through its owner-authority NetworkTransform.
        /// </summary>
        bool HoldsRollAngle { get; set; }
        float HeldRollDegrees { get; set; }

        Quaternion GetGyroRotation();
        void ResetForReplay();
    }

}