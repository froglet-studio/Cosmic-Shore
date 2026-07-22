using UnityEngine;
using UnityEngine.InputSystem;
using CosmicShore.Utility;
using CosmicShore.Data;


namespace CosmicShore.Gameplay
{
    public class GamepadInputStrategy : BaseInputStrategy
    {
        private const float TriggerDeadzone = 0.05f;

        // Worn/miscalibrated triggers can REST well above zero (field repro: an Xbox
        // pad resting at L=0.38). A rest value above TriggerDeadzone makes the edge
        // detector read the trigger as permanently held - the press edge never fires
        // and trigger-bound actions (e.g. the Squirrel's drift) go dead, while the
        // analog intensity idles non-zero. Track the minimum observed raw value per
        // trigger as its resting baseline and remap [baseline..1] onto [0..1] so
        // edges and analog behave as on a healthy pad. Min-tracking self-corrects if
        // the trigger happens to be held when the strategy activates.
        private float _leftTriggerRestBaseline = 1f;
        private float _rightTriggerRestBaseline = 1f;

        static float RemapFromRest(float raw, float rest) =>
            rest >= 0.99f ? 0f : Mathf.Clamp01((raw - rest) / (1f - rest));

        private bool fullSpeedStraightEffectsStarted;
        private bool minimumSpeedStraightEffectsStarted;

        private Vector2 leftStickRaw;
        private Vector2 rightStickRaw;

        private bool prevLeftTriggerActive;
        private bool prevRightTriggerActive;

        public override void Initialize(IInputStatus inputStatus)
        {
            base.Initialize(inputStatus);
            ResetInput();
        }

        // TEMPORARY [DRIFT-DIAG]: remove after the Scurry drift investigation.
        private float _diagNextRawLogTime;

        public override void OnStrategyActivated()
        {
            base.OnStrategyActivated();
            inputStatus.ActiveInputDevice = InputDeviceType.Gamepad;

            // Re-calibrate on (re)activation - the active pad may have changed.
            _leftTriggerRestBaseline = 1f;
            _rightTriggerRestBaseline = 1f;

            // TEMPORARY [DRIFT-DIAG]: remove after the Scurry drift investigation.
            CSDebug.Log($"[DRIFT-DIAG] GamepadStrategy ACTIVATED pad='{Gamepad.current?.displayName}' " +
                        $"type={Gamepad.current?.GetType().Name} allGamepads={Gamepad.all.Count}");
        }

        public override void ProcessInput()
        {
            if (Gamepad.current == null) return;

            ProcessStickInput();
            ProcessButtonInput();
            Reparameterize();
            PerformSpeedAndDirectionalEffects();
        }

        private void ProcessStickInput()
        {
            // Read throttle (this is just the boost button - don't invert it)
            inputStatus.Throttle = Gamepad.current.rightShoulder.ReadValue();
            
            // Read raw stick values without any inversion yet
            leftStickRaw = Gamepad.current.leftStick.ReadValue();
            rightStickRaw = Gamepad.current.rightStick.ReadValue();
        }

        private void ProcessButtonInput()
        {
            // Primary action buttons
            if (Gamepad.current.buttonSouth.wasPressedThisFrame)
                inputStatus.OnButtonPressed.Raise(InputEvents.Button1Action);
                //vessel.PerformShipControllerActions(InputEvents.Button1Action);
            if (Gamepad.current.buttonSouth.wasReleasedThisFrame)
                inputStatus.OnButtonReleased.Raise(InputEvents.Button1Action);
                // vessel.StopShipControllerActions(InputEvents.Button1Action);

            if (Gamepad.current.buttonEast.wasPressedThisFrame)
                inputStatus.OnButtonPressed.Raise(InputEvents.Button2Action);
                // vessel.PerformShipControllerActions(InputEvents.Button2Action);
            if (Gamepad.current.buttonEast.wasReleasedThisFrame)
                inputStatus.OnButtonReleased.Raise(InputEvents.Button2Action);
                // vessel.StopShipControllerActions(InputEvents.Button2Action);

            if (Gamepad.current.buttonWest.wasPressedThisFrame)
                inputStatus.OnButtonPressed.Raise(InputEvents.Button3Action);
                // vessel.PerformShipControllerActions(InputEvents.Button3Action);
            if (Gamepad.current.buttonWest.wasReleasedThisFrame)
                inputStatus.OnButtonReleased.Raise(InputEvents.Button3Action);
                // vessel.StopShipControllerActions(InputEvents.Button3Action);

            // Shoulder buttons and triggers
            if (Gamepad.current.leftShoulder.wasPressedThisFrame)
            {
                //inputStatus.Idle = true;
                //inputStatus.OnButtonPressed.Raise(InputEvents.IdleAction);;
                // vessel.PerformShipControllerActions(InputEvents.IdleAction);
            }
            if (Gamepad.current.leftShoulder.wasReleasedThisFrame)
            {
                //inputStatus.Idle = false;
                //inputStatus.OnButtonReleased.Raise(InputEvents.IdleAction);;
                // vessel.StopShipControllerActions(InputEvents.IdleAction);
            }

            // Right shoulder for flip action
            if (Gamepad.current.rightShoulder.wasPressedThisFrame)
                inputStatus.OnButtonPressed.Raise(InputEvents.FlipAction);
                // vessel.PerformShipControllerActions(InputEvents.FlipAction);
            if (Gamepad.current.rightShoulder.wasReleasedThisFrame)
                inputStatus.OnButtonReleased.Raise(InputEvents.FlipAction);
            // vessel.StopShipControllerActions(InputEvents.FlipAction);

            // Triggers - read analog values and use custom deadzone for edge detection.
            // This gives full analog range (0-1) for drift scaling while keeping
            // binary event compatibility for button-style triggers (which snap 0/1).
            // Values are measured from the trigger's calibrated resting baseline (see
            // RemapFromRest) so a drifting trigger can't read as permanently held.
            float leftTriggerRaw = Gamepad.current.leftTrigger.ReadValue();
            float rightTriggerRaw = Gamepad.current.rightTrigger.ReadValue();

            _leftTriggerRestBaseline = Mathf.Min(_leftTriggerRestBaseline, leftTriggerRaw);
            _rightTriggerRestBaseline = Mathf.Min(_rightTriggerRestBaseline, rightTriggerRaw);

            float leftTriggerValue = RemapFromRest(leftTriggerRaw, _leftTriggerRestBaseline);
            float rightTriggerValue = RemapFromRest(rightTriggerRaw, _rightTriggerRestBaseline);

            inputStatus.LeftTriggerAnalog = leftTriggerValue;
            inputStatus.RightTriggerAnalog = rightTriggerValue;

            // TEMPORARY [DRIFT-DIAG]: remove after the Scurry drift investigation.
            // Logs at most once per second, only while a trigger physically reads non-zero.
            if ((leftTriggerRaw > 0.01f || rightTriggerRaw > 0.01f)
                && Time.unscaledTime >= _diagNextRawLogTime)
            {
                _diagNextRawLogTime = Time.unscaledTime + 1f;
                CSDebug.Log($"[DRIFT-DIAG] RawTrigger L={leftTriggerValue:F2} (raw={leftTriggerRaw:F2} rest={_leftTriggerRestBaseline:F2}) " +
                            $"R={rightTriggerValue:F2} (raw={rightTriggerRaw:F2} rest={_rightTriggerRestBaseline:F2}) " +
                            $"pad='{Gamepad.current.displayName}' type={Gamepad.current.GetType().Name}");
            }

            bool leftActive = leftTriggerValue > TriggerDeadzone;
            bool rightActive = rightTriggerValue > TriggerDeadzone;

            bool leftJustPressed = leftActive && !prevLeftTriggerActive;
            bool leftJustReleased = !leftActive && prevLeftTriggerActive;
            bool leftHeld = leftActive;

            bool rightJustPressed = rightActive && !prevRightTriggerActive;
            bool rightJustReleased = !rightActive && prevRightTriggerActive;
            bool rightHeld = rightActive;

            prevLeftTriggerActive = leftActive;
            prevRightTriggerActive = rightActive;

            // Individual trigger events
            if (leftJustPressed)
                inputStatus.OnButtonPressed.Raise(InputEvents.LeftStickAction);
            if (leftJustReleased)
                inputStatus.OnButtonReleased.Raise(InputEvents.LeftStickAction);
            if (rightJustPressed)
                inputStatus.OnButtonPressed.Raise(InputEvents.RightStickAction);
            if (rightJustReleased)
                inputStatus.OnButtonReleased.Raise(InputEvents.RightStickAction);

            // BothSticksAction Released
            if ((leftJustReleased && rightJustReleased)
                || (leftJustReleased && rightHeld)
                || (rightJustReleased && leftHeld))
                inputStatus.OnButtonReleased.Raise(InputEvents.BothSticksAction);

            // OnlyLeftStickAction Released
            if ((leftJustReleased && !rightHeld)
                || (rightJustPressed && leftHeld))
                inputStatus.OnButtonReleased.Raise(InputEvents.OnlyLeftStickAction);

            // OnlyRightStickAction Released
            if ((rightJustReleased && !leftHeld)
                || (leftJustPressed && rightHeld))
                inputStatus.OnButtonReleased.Raise(InputEvents.OnlyRightStickAction);

            // OnlyLeftStickAction Pressed
            if ((leftJustPressed && !rightHeld)
                || (rightJustReleased && leftHeld))
                inputStatus.OnButtonPressed.Raise(InputEvents.OnlyLeftStickAction);

            // OnlyRightStickAction Pressed
            if ((rightJustPressed && !leftHeld)
                || (leftJustReleased && rightHeld))
                inputStatus.OnButtonPressed.Raise(InputEvents.OnlyRightStickAction);

            // BothSticksAction Pressed
            if ((leftJustPressed && rightJustPressed)
                || (leftJustPressed && rightHeld)
                || (rightJustPressed && leftHeld))
                inputStatus.OnButtonPressed.Raise(InputEvents.BothSticksAction);
        }

        private void Reparameterize()
        {
            // Calculate eased joystick positions
            inputStatus.EasedLeftJoystickPosition = new Vector2(
                Ease(2 * leftStickRaw.x),
                Ease(2 * leftStickRaw.y)
            );
            inputStatus.EasedRightJoystickPosition = new Vector2(
                Ease(2 * rightStickRaw.x),
                Ease(2 * rightStickRaw.y)
            );

            inputStatus.RightNormalizedJoystickPosition = rightStickRaw;
            inputStatus.LeftNormalizedJoystickPosition = leftStickRaw;

            // Calculate sums and differences exactly as touch input does
            inputStatus.XSum = Ease(rightStickRaw.x + leftStickRaw.x);
            inputStatus.YSum = -Ease(rightStickRaw.y + leftStickRaw.y);
            inputStatus.XDiff = (rightStickRaw.x - leftStickRaw.x + 2) / 4;
            inputStatus.YDiff = Ease(rightStickRaw.y - leftStickRaw.y);
            
            // Store values before inversion for debugging
            float ySumBefore = inputStatus.YSum;
            float yDiffBefore = inputStatus.YDiff;
            float xDiffBefore = inputStatus.XDiff;
            
            // Apply inversions AFTER calculations
            if (inputStatus.InvertYEnabled)
            {
                inputStatus.YSum *= -1f;   // Invert pitch
                inputStatus.YDiff *= -1f;  // Invert roll
            }
            
            if (inputStatus.InvertThrottleEnabled)
            {
                inputStatus.XDiff = 1f - inputStatus.XDiff;  // Invert throttle/speed
            }
            
            // DEBUG: Uncomment to see inversion working (press Tab key to log)
            #if UNITY_EDITOR
            if (UnityEngine.InputSystem.Keyboard.current != null && 
                UnityEngine.InputSystem.Keyboard.current.tabKey.wasPressedThisFrame)
            {
                CSDebug.Log($"[GamepadInput] Reparameterize Debug:\n" +
                          $"  Raw Sticks - L: {leftStickRaw}, R: {rightStickRaw}\n" +
                          $"  YSum: {ySumBefore:F2} → {inputStatus.YSum:F2} (InvertY: {inputStatus.InvertYEnabled})\n" +
                          $"  YDiff: {yDiffBefore:F2} → {inputStatus.YDiff:F2} (InvertY: {inputStatus.InvertYEnabled})\n" +
                          $"  XDiff: {xDiffBefore:F2} → {inputStatus.XDiff:F2} (InvertThrottle: {inputStatus.InvertThrottleEnabled})");
            }
            #endif
        }

        private void PerformSpeedAndDirectionalEffects()
        {
            float threshold = .3f;
            float sumOfRotations = Mathf.Abs(inputStatus.YDiff) + Mathf.Abs(inputStatus.YSum) + Mathf.Abs(inputStatus.XSum);
            float DeviationFromFullSpeedStraight = (1 - inputStatus.XDiff) + sumOfRotations;
            float DeviationFromMinimumSpeedStraight = inputStatus.XDiff + sumOfRotations;

            if (DeviationFromFullSpeedStraight < threshold && !fullSpeedStraightEffectsStarted)
            {
                fullSpeedStraightEffectsStarted = true;
                inputStatus.OnButtonPressed.Raise(InputEvents.FullSpeedStraightAction);
                // vessel.PerformShipControllerActions(InputEvents.FullSpeedStraightAction);
            }
            else if (DeviationFromMinimumSpeedStraight < threshold && !minimumSpeedStraightEffectsStarted)
            {
                minimumSpeedStraightEffectsStarted = true;
                inputStatus.OnButtonPressed.Raise(InputEvents.MinimumSpeedStraightAction);
                // vessel.PerformShipControllerActions(InputEvents.MinimumSpeedStraightAction);
            }
            else
            {
                if (fullSpeedStraightEffectsStarted && DeviationFromFullSpeedStraight > threshold)
                {
                    fullSpeedStraightEffectsStarted = false;
                    inputStatus.OnButtonReleased.Raise(InputEvents.FullSpeedStraightAction);
                    // vessel.StopShipControllerActions(InputEvents.FullSpeedStraightAction);
                }
                if (minimumSpeedStraightEffectsStarted && DeviationFromMinimumSpeedStraight > threshold)
                {
                    minimumSpeedStraightEffectsStarted = false;
                    inputStatus.OnButtonReleased.Raise(InputEvents.MinimumSpeedStraightAction);
                    // vessel.StopShipControllerActions(InputEvents.MinimumSpeedStraightAction);
                }
            }
        }

        public override void SetPortrait(bool portrait)
        {
            // Gamepad doesn't need to handle portrait mode changes
        }
    }
}