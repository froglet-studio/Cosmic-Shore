using UnityEngine;


namespace CosmicShore.Gameplay
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

        /// <summary>
        /// THE Invert Y rule, the same on every device and every hull (Garrett, 2026-10-21: no Y
        /// inversion that is not the setting's). The setting turns the sticks' vertical axis over
        /// everywhere flight reads it: the two-stick pitch (<c>YSum</c>) and roll (<c>YDiff</c>, the
        /// sticks' vertical difference) AND the one-stick pitch (<c>EasedLeft/RightJoystickPosition.y</c>,
        /// which the single-stick hulls steer by). It used to flip only the first two, so on a pad or a
        /// keyboard the setting did nothing on the nine one-thumb hulls, and touch never read it at all.
        /// The raw <c>Left/RightNormalizedJoystickPosition</c> (where the thumb or stick IS, for aim and
        /// UI) is left as it is. Call it last, after the strategy's mix.
        /// </summary>
        protected void ApplyInvertSettings()
        {
            if (inputStatus.InvertYEnabled)
            {
                inputStatus.YSum = -inputStatus.YSum;
                inputStatus.YDiff = -inputStatus.YDiff;
                var left = inputStatus.EasedLeftJoystickPosition;
                inputStatus.EasedLeftJoystickPosition = new Vector2(left.x, -left.y);
                var right = inputStatus.EasedRightJoystickPosition;
                inputStatus.EasedRightJoystickPosition = new Vector2(right.x, -right.y);
            }

            if (inputStatus.InvertThrottleEnabled)
                inputStatus.XDiff = 1f - inputStatus.XDiff;
        }

        protected virtual float Ease(float input)
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
            inputStatus.LeftTriggerAnalog = 0f;
            inputStatus.RightTriggerAnalog = 0f;
            inputStatus.Idle = false;
        }
    }
}
