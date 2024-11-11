using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Core;

namespace CosmicShore.Game.IO
{
    public abstract class BaseInputStrategy : IInputStrategy
    {
        protected const float PI_OVER_FOUR = 0.785f;
        protected Ship ship;
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
        public bool IsIdle => idle;

        public virtual void Initialize(Ship ship)
        {
            this.ship = ship;
        }

        public abstract void ProcessInput(Ship ship);
        public virtual void SetPortrait(bool portrait) { }
        public virtual void OnStrategyActivated() { }
        public virtual void OnStrategyDeactivated() { }
        public virtual void OnPaused() { }
        public virtual void OnResumed() { }

        public virtual void SetInvertY(bool status)
        {
            invertYEnabled = status;
        }

        public virtual void SetInvertThrottle(bool status)
        {
            invertThrottleEnabled = status;
        }

        public virtual void SetAutoPilotValues(Vector2 position)
        {
            EasedLeftJoystickPosition = position;
        }

        public virtual void SetAutoPilotValues(float xSum, float ySum, float xDiff, float yDiff)
        {
            XSum = xSum;
            YSum = ySum;
            XDiff = xDiff;
            YDiff = yDiff;
        }

        protected float Ease(float input)
        {
            return input < 0 ?
                (Mathf.Cos(input * PI_OVER_FOUR) - 1) :
                -(Mathf.Cos(input * PI_OVER_FOUR) - 1);
        }

        protected virtual void ResetInput()
        {
            XSum = 0;
            YSum = 0;
            XDiff = 0;
            YDiff = 0;
            EasedLeftJoystickPosition = Vector2.zero;
            EasedRightJoystickPosition = Vector2.zero;
            RightJoystickStart = Vector2.zero;
            LeftJoystickStart = Vector2.zero;
            RightNormalizedJoystickPosition = Vector2.zero;
            LeftNormalizedJoystickPosition = Vector2.zero;
            idle = false;
        }
    }
}
