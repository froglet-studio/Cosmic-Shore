using UnityEngine;


namespace CosmicShore.Game.IO
{
    public abstract class BaseInputStrategy : IInputStrategy
    {
        protected const float PI_OVER_FOUR = 0.785f;

        protected IInputStatus inputStatus;

        public virtual void Initialize(IInputStatus inputStatus)
        {
            this.inputStatus = inputStatus; 
        }

        public abstract void ProcessInput();
        public virtual void SetPortrait(bool portrait) { }
        public virtual void OnStrategyActivated() { }
        public virtual void OnStrategyDeactivated() { }
        public virtual void OnPaused() { }
        public virtual void OnResumed() { }

        public virtual void SetInvertY(bool status)
        {
            inputStatus.InvertYEnabled = status;
        }

        public virtual void SetInvertThrottle(bool status)
        {
            inputStatus.InvertThrottleEnabled = status;
        }

        protected float Ease(float input)
        {
            return input < 0 ?
                (Mathf.Cos(input * PI_OVER_FOUR) - 1) :
                -(Mathf.Cos(input * PI_OVER_FOUR) - 1);
        }

        protected virtual void ResetInput()
        {
            inputStatus.XSum = 0;
            inputStatus.YSum = 0;
            inputStatus.XDiff = 0;
            inputStatus.YDiff = 0;
            inputStatus.EasedLeftJoystickPosition = Vector2.zero;
            inputStatus.EasedRightJoystickPosition = Vector2.zero;
            inputStatus.RightJoystickStart = Vector2.zero;
            inputStatus.LeftJoystickStart = Vector2.zero;
            inputStatus.RightNormalizedJoystickPosition = Vector2.zero;
            inputStatus.LeftNormalizedJoystickPosition = Vector2.zero;
            inputStatus.Idle = false;
        }
    }
}
