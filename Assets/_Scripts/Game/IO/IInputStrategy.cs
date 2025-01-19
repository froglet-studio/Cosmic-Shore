using UnityEngine;

namespace CosmicShore.Game.IO
{
    public interface IInputStrategy
    {
        void Initialize(IShip ship);
        void ProcessInput(IShip ship);
        void SetPortrait(bool portrait);
        void OnStrategyActivated();
        void OnStrategyDeactivated();
        void OnPaused();
        void OnResumed();
        void SetInvertY(bool status);
        void SetInvertThrottle(bool status);
        void SetAutoPilotValues(Vector2 position);
        void SetAutoPilotValues(float xSum, float ySum, float xDiff, float yDiff);

        /*// Properties that match original InputController
        bool IsIdle { get; }
        float XSum { get; }
        float YSum { get; }
        float XDiff { get; }
        float YDiff { get; }
        Vector2 EasedLeftJoystickPosition { get; }
        Vector2 EasedRightJoystickPosition { get; }
        Vector2 RightJoystickHome { get; }
        Vector2 LeftJoystickHome { get; }
        Vector2 RightClampedPosition { get; }
        Vector2 LeftClampedPosition { get; }
        Vector2 RightJoystickStart { get; }
        Vector2 LeftJoystickStart { get; }
        Vector2 RightNormalizedJoystickPosition { get; }
        Vector2 LeftNormalizedJoystickPosition { get; }
        bool OneTouchLeft { get; }
        Vector2 SingleTouchValue { get; }
        Vector3 ThreeDPosition { get; }*/
    }
}
