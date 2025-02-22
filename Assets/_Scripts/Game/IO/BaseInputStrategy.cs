using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Core;

namespace CosmicShore.Game.IO
{
    public abstract class BaseInputStrategy : IInputStrategy
    {
        protected const float PI_OVER_FOUR = 0.785f;
        protected IShip _ship;

        /*
        protected bool idle;
        protected bool invertYEnabled;
        protected bool invertThrottleEnabled;

        public float XSum { get; protected set; }
        public float YSum { get; protected set; }
        public float XDiff { get; protected set; }
        public float YDiff { get; protected set; }
        public Vector2 EasedLeftJoystickPosition { get; protected set; }
        public Vector2 EasedRightJoystickPosition { get; protected set; }
        public Vector2 RightJoystickHome { get; protected set; }
        public Vector2 LeftJoystickHome { get; protected set; }
        public Vector2 RightClampedPosition { get; protected set; }
        public Vector2 LeftClampedPosition { get; protected set; }
        public Vector2 RightJoystickStart { get; protected set; }
        public Vector2 LeftJoystickStart { get; protected set; }
        public Vector2 RightNormalizedJoystickPosition { get; protected set; }
        public Vector2 LeftNormalizedJoystickPosition { get; protected set; }
        public bool OneTouchLeft { get; protected set; }
        public Vector2 SingleTouchValue { get; protected set; }
        public Vector3 ThreeDPosition { get; protected set; }
        public bool IsIdle => idle;*/

        protected IInputStatus InputStatus;

        public virtual void Initialize(IShip ship)
        {
            _ship = ship;
            InputStatus = _ship.ShipStatus.InputStatus;
        }

        public abstract void ProcessInput();
        public virtual void SetPortrait(bool portrait) { }
        public virtual void OnStrategyActivated() { }
        public virtual void OnStrategyDeactivated() { }
        public virtual void OnPaused() { }
        public virtual void OnResumed() { }

        public virtual void SetInvertY(bool status)
        {
            InputStatus.InvertYEnabled = status;
        }

        public virtual void SetInvertThrottle(bool status)
        {
            InputStatus.InvertThrottleEnabled = status;
        }

        public virtual void SetAutoPilotValues(Vector2 position)
        {
            InputStatus.EasedLeftJoystickPosition = position;
        }

        public virtual void SetAutoPilotValues(float xSum, float ySum, float xDiff, float yDiff)
        {
            InputStatus.XSum = xSum;
            InputStatus.YSum = ySum;
            InputStatus.XDiff = xDiff;
            InputStatus.YDiff = yDiff;
        }

        protected float Ease(float input)
        {
            return input < 0 ?
                (Mathf.Cos(input * PI_OVER_FOUR) - 1) :
                -(Mathf.Cos(input * PI_OVER_FOUR) - 1);
        }

        protected virtual void ResetInput()
        {
            InputStatus.XSum = 0;
            InputStatus.YSum = 0;
            InputStatus.XDiff = 0;
            InputStatus.YDiff = 0;
            InputStatus.EasedLeftJoystickPosition = Vector2.zero;
            InputStatus.EasedRightJoystickPosition = Vector2.zero;
            InputStatus.RightJoystickStart = Vector2.zero;
            InputStatus.LeftJoystickStart = Vector2.zero;
            InputStatus.RightNormalizedJoystickPosition = Vector2.zero;
            InputStatus.LeftNormalizedJoystickPosition = Vector2.zero;
            InputStatus.Idle = false;
        }
    }
}
