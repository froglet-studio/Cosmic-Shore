namespace CosmicShore.Core
{
    using UnityEngine;

    public interface IInputStatus
    {
        // Floats
        float XSum { get; set; }
        float YSum { get; set; }
        float XDiff { get; set; }
        float YDiff { get; set; }

        // Booleans
        bool Idle { get; set; }
        bool Paused { get; set; }
        bool IsGyroEnabled { get; set; }
        bool InvertYEnabled { get; set; }
        bool InvertThrottleEnabled { get; set; }
        bool OneTouchLeft { get; set; }

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
    }

}