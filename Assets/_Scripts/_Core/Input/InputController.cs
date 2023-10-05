using System.Collections;
using UnityEngine;
using Gamepad = UnityEngine.InputSystem.Gamepad;

namespace StarWriter.Core.IO
{
    public class InputController : MonoBehaviour
    {
        [SerializeField] GameCanvas gameCanvas;
        [SerializeField] public bool AutoPilotEnabled;
        [SerializeField] public bool Portrait;
        [SerializeField] public bool SingleStickControls;
        [HideInInspector] public Ship ship;
        [HideInInspector] public static ScreenOrientation currentOrientation;
        ShipButtonPanel shipButtonPanel;

        float phoneFlipThreshold = .1f;
        bool PhoneFlipState;
        bool leftStickEffectsStarted;
        bool rightStickEffectsStarted;
        bool fullSpeedStraightEffectsStarted;
        bool minimumSpeedStraightEffectsStarted;
        int leftTouchIndex, rightTouchIndex;
        bool oneFingerMode;
        bool leftActive = true;

        const float piOverFour = 0.785f; 

        [HideInInspector] public float XSum;
        [HideInInspector] public float YSum;
        [HideInInspector] public float XDiff;
        [HideInInspector] public float YDiff;

        [HideInInspector] public bool Idle;
        [HideInInspector] public bool Paused;
        [HideInInspector] public Vector2 RightJoystickHome;
        [HideInInspector] public Vector2 LeftJoystickHome;
        [HideInInspector] public Vector2 RightClampedPosition;
        [HideInInspector] public Vector2 LeftClampedPosition;
        [HideInInspector] public bool isGyroEnabled;
        [HideInInspector] public bool invertYEnabled;
        Vector2 RightJoystickStart, LeftJoystickStart;
        [HideInInspector] public Vector2 RightJoystickPosition, LeftJoystickPosition;
        Vector2 RightJoystickValue, LeftJoystickValue;
        float JoystickRadius;

        Gyroscope gyro;
        Quaternion derivedCorrection;
        Quaternion inverseInitialRotation=new(0,0,0,0);
        float gyroInitializationAcceptableRange = .05f;

        void OnEnable()
        {
            GameSetting.OnChangeInvertYEnabledStatus += OnToggleInvertY;
        }

        void OnDisable()
        {
            GameSetting.OnChangeInvertYEnabledStatus -= OnToggleInvertY;
        }

        void Start()
        {
            if (gameCanvas != null)
            {
                shipButtonPanel = gameCanvas.ShipButtonPanel;
            }

            JoystickRadius = Screen.dpi;
            LeftJoystickValue = LeftClampedPosition = LeftJoystickHome = new Vector2(JoystickRadius, JoystickRadius);
            RightJoystickValue = RightClampedPosition = RightJoystickHome = new Vector2(Screen.currentResolution.width - JoystickRadius, JoystickRadius);

            gyro = Input.gyro;
            gyro.enabled = true;
            StartCoroutine(GyroInitializationCoroutine());
            invertYEnabled = GameSetting.Instance.InvertYEnabled;       
        }

        IEnumerator GyroInitializationCoroutine()
        {
            derivedCorrection = GyroQuaternionToUnityQuaternion(Quaternion.Inverse(new Quaternion(0, .65f, .75f, 0)));
            inverseInitialRotation = Quaternion.identity;

            // Turns out the gryo attitude is not avaiable immediately, so wait until we start getting values to initialize
            while (Equals(new Quaternion(0,0,0,0), gyro.attitude))
                yield return new WaitForSeconds(gyro.updateInterval);

            var lastAttitude = gyro.attitude;
            yield return new WaitForSeconds(gyro.updateInterval);

            // Also turns out that the first value returned is garbage, so wait for it to stabilize
            // We check for rough equality using the absolute value of the two quaternions dot product
            while (!(1 - Mathf.Abs(Quaternion.Dot(lastAttitude, gyro.attitude)) < gyroInitializationAcceptableRange))
            {
                lastAttitude = gyro.attitude;
                yield return new WaitForSeconds(gyro.updateInterval);
            }

            inverseInitialRotation = Quaternion.Inverse(GyroQuaternionToUnityQuaternion(gyro.attitude) * derivedCorrection);
        }

        void Update()
        {
            if (PauseSystem.Paused || Paused) return;

            // Convert two finger touch into values for displacement, Speed, and ship animations
            ReceiveInput();
        }

        void ReceiveInput()
        {
            if (AutoPilotEnabled)
            {
                XSum = ship.AutoPilot.XSum;
                YSum = ship.AutoPilot.YSum;
                XDiff = ship.AutoPilot.XDiff;
                YDiff = ship.AutoPilot.YDiff;
                PerformSpeedAndDirectionalEffects();
            }
            else if (Gamepad.current != null)
            {
                if (ship != null && ship.ShipStatus.ShowThreeButtonPanel)
                {
                    shipButtonPanel.FadeInButtons();
                }

                LeftJoystickPosition.x = Gamepad.current.leftStick.x.ReadValue();
                LeftJoystickPosition.y = Gamepad.current.leftStick.y.ReadValue();
                RightJoystickPosition.x = Gamepad.current.rightStick.x.ReadValue();
                RightJoystickPosition.y = Gamepad.current.rightStick.y.ReadValue();

                Reparameterize();

                ProcessGamePadButtons();

                PerformSpeedAndDirectionalEffects();
            }
            else
            {
                if (Portrait)
                {
                    ship.SetShipUp(90);
                }
                else if (Mathf.Abs(Input.acceleration.y) >= phoneFlipThreshold)
                {
                    if (Input.acceleration.y < 0 && PhoneFlipState)
                    {
                        PhoneFlipState = false;
                        ship.StopShipControllerActions(InputEvents.FlipAction);
                        ship.FlipShipRightsideUp();

                        currentOrientation = ScreenOrientation.LandscapeLeft;

                        Debug.Log($"InputController Phone flip state change detected - new flip state: {PhoneFlipState}, acceleration.y: {Input.acceleration.y}");
                    }
                    else if (Input.acceleration.y > 0 && !PhoneFlipState)
                    {
                        PhoneFlipState = true;
                        ship.PerformShipControllerActions(InputEvents.FlipAction);
                        ship.FlipShipUpsideDown(); // TODO make shipAction

                        currentOrientation = ScreenOrientation.LandscapeRight;

                        Debug.Log($"InputController Phone flip state change detected - new flip state: {PhoneFlipState}, acceleration.y: {Input.acceleration.y}");
                    }
                }
                
                if (SingleStickControls)
                {
                    if (Input.touchCount > 0)
                    {
                        leftTouchIndex = Input.touchCount >= 2 ? GetClosestTouch(LeftJoystickValue) : 0;
                        HandleJoystick(ref LeftJoystickStart, leftTouchIndex, ref LeftJoystickPosition, ref LeftClampedPosition);
                    }
                }
                else
                {
                    var threeFingerFumble = false;
                    if (Input.touchCount >= 3)
                    {
                        // Sub select the two best touch inputs here
                        // If we have more than two touches, find the closest to each of the last touch positions we used
                        threeFingerFumble = true;

                        leftTouchIndex = GetClosestTouch(LeftJoystickValue);
                        rightTouchIndex = GetClosestTouch(RightJoystickValue);
                    }

                    if (Input.touchCount == 2 || threeFingerFumble)
                    {
                        // If we didn't fat finger the phone, fix a finger index
                        if (!threeFingerFumble)
                        {
                            if (Input.touches[0].position.x <= Input.touches[1].position.x)
                            {
                                leftTouchIndex = 0;
                                rightTouchIndex = 1;
                            }
                            else
                            {
                                leftTouchIndex = 1;
                                rightTouchIndex = 0;
                            }
                        }

                        LeftJoystickValue = Input.touches[leftTouchIndex].position;
                        RightJoystickValue = Input.touches[rightTouchIndex].position;

                        HandleJoystick(ref LeftJoystickStart, leftTouchIndex, ref LeftJoystickPosition, ref LeftClampedPosition);
                        HandleJoystick(ref RightJoystickStart, rightTouchIndex, ref RightJoystickPosition, ref RightClampedPosition);

                        if (leftStickEffectsStarted)
                        {
                            leftStickEffectsStarted = false;
                            ship.StopShipControllerActions(InputEvents.LeftStickAction);
                        }
                        if (rightStickEffectsStarted)
                        {
                            rightStickEffectsStarted = false;
                            ship.StopShipControllerActions(InputEvents.RightStickAction);
                        }
                    }

                    if (Input.touchCount == 1)
                    {
                        oneFingerMode = true;
                        var position = Input.touches[0].position;

                        if (Vector2.Distance(LeftJoystickValue, position) < Vector2.Distance(RightJoystickValue, position))
                        {
                            if (!leftStickEffectsStarted)
                            {
                                leftStickEffectsStarted = true;
                                ship.PerformShipControllerActions(InputEvents.LeftStickAction);
                            }
                            LeftJoystickValue = position;
                            leftTouchIndex = 0;
                            HandleJoystick(ref LeftJoystickStart, leftTouchIndex, ref LeftJoystickPosition, ref LeftClampedPosition);
                            leftActive = true;
                        }
                        else
                        {
                            if (!rightStickEffectsStarted)
                            {
                                rightStickEffectsStarted = true;
                                ship.PerformShipControllerActions(InputEvents.RightStickAction);
                            }
                            RightJoystickValue = position;
                            rightTouchIndex = 0;
                            HandleJoystick(ref RightJoystickStart, rightTouchIndex, ref RightJoystickPosition, ref RightClampedPosition);
                            leftActive = false;
                        }
                    }
                    else oneFingerMode = false;
                }
                

                if (Input.touchCount > 0)
                {
                    Reparameterize();
                    PerformSpeedAndDirectionalEffects();

                    if (Portrait)
                    {
                        shipButtonPanel.FadeOutButtons();
                    }

                    if (Idle)
                    {
                        Idle = false;
                        ship.StopShipControllerActions(InputEvents.IdleAction);
                    }
                }
                else
                {
                    if (Portrait || ship.ShipStatus.ShowThreeButtonPanel)
                    {
                        shipButtonPanel.FadeInButtons();
                        PerformSpeedAndDirectionalEffects();
                    }
                    else 
                    {
                        XSum = 0;
                        YSum = 0;
                        XDiff = 0;
                        YDiff = 0;
                    }

                    Idle = true;
                    ship.PerformShipControllerActions(InputEvents.IdleAction); // consider placing some stop methods for other Input events here  
                }
            }
        }

        void HandleJoystick(ref Vector2 joystickStart, int touchIndex, ref Vector2 joystick, ref Vector2 clampedPosition)
        {
            Touch touch = Input.touches[touchIndex];

            // We check for Vector2.zero since this is the default (i.e uninitialized) value for Vec2
            // Otherwise, if we missed the TouchPhase.Began event (like before a minigame starts),
            // we always end up with the joystick as a JoystickRadius long vector
            // starting at the bottom left corner and pointing toward the touchposition
            if (touch.phase == TouchPhase.Began || joystickStart == Vector2.zero)
                joystickStart = touch.position;

            Vector2 offset = touch.position - joystickStart;
            Vector2 clampedOffset = Vector2.ClampMagnitude(offset, JoystickRadius); 
            clampedPosition = joystickStart + clampedOffset;
            Vector2 normalizedOffset = clampedOffset / JoystickRadius;
            joystick = normalizedOffset;
        }

        void Reparameterize()
        {
            //if (oneFingerMode || SingleStickControls)
            //{
            //    if (leftActive)
            //    {
            //        XSum = Ease(LeftJoystickPosition.x);
            //        YSum = -Ease(LeftJoystickPosition.y); //negative is because joysitcks and unity axes don't agree
            //        XDiff = .5f;
            //        YDiff = 0;
            //    }
            //    else
            //    {
            //        XSum = Ease(RightJoystickPosition.x);
            //        YSum = -Ease(RightJoystickPosition.y); //negative is because joysitcks and unity axes don't agree
            //        XDiff = .5f;
            //        YDiff = 0;
            //    }
            //}
            //else
            //{
                XSum = Ease(RightJoystickPosition.x + LeftJoystickPosition.x);
                YSum = -Ease(RightJoystickPosition.y + LeftJoystickPosition.y); //negative is because joysitcks and unity axes don't agree
                XDiff = (LeftJoystickPosition.x - RightJoystickPosition.x + 2.1f) / 4.1f;
                YDiff = Ease(RightJoystickPosition.y - LeftJoystickPosition.y);
            //}

            if (invertYEnabled)
                YSum *= -1;
        }

        // TODO: move to centralized helper class
        // Converts Android Quaternions into Unity Quaternions
        Quaternion GyroQuaternionToUnityQuaternion(Quaternion q)
        {
            return new Quaternion(q.x, -q.z, q.y, q.w);
        }

        /// <summary>
        /// Gets gyros updated current status from GameManager.onToggleGyro Event
        /// </summary>
        /// <param name="status"></param>bool
        public void OnToggleGyro(bool status)
        {
            Debug.Log($"InputController.OnToggleGyro - status: {status}");
            if (SystemInfo.supportsGyroscope && status)
            {
                inverseInitialRotation = Quaternion.Inverse(GyroQuaternionToUnityQuaternion(gyro.attitude) * derivedCorrection);
            }

            isGyroEnabled = status;
        }

        public Quaternion GetGyroRotation()
        {
            return inverseInitialRotation * GyroQuaternionToUnityQuaternion(gyro.attitude) * derivedCorrection;
        }

        /// <summary>
        /// Sets InvertY Status based off of game settings event
        /// </summary>
        /// <param name="status"></param>bool
        void OnToggleInvertY(bool status)
        {
            Debug.Log($"InputController.OnToggleInvertY - status: {status}");

            invertYEnabled = status;
        }

        float Ease(float input)
        {
            return input < 0 ? (Mathf.Cos(input* piOverFour) - 1) : -(Mathf.Cos(input* piOverFour) - 1); // the inflection point when fed a value of two which is the maximum input.
        }

        void PerformSpeedAndDirectionalEffects()
        {
            float threshold = .3f;
            float sumOfRotations = Mathf.Abs(YDiff) + Mathf.Abs(YSum) + Mathf.Abs(XSum);
            float DeviationFromFullSpeedStraight = (1 - XDiff) + sumOfRotations;
            float DeviationFromMinimumSpeedStraight = XDiff + sumOfRotations;

            if (DeviationFromFullSpeedStraight < threshold && !fullSpeedStraightEffectsStarted)
            {
                fullSpeedStraightEffectsStarted = true;
                ship.PerformShipControllerActions(InputEvents.FullSpeedStraightAction);
            }
            else if (DeviationFromMinimumSpeedStraight < threshold && !minimumSpeedStraightEffectsStarted)
            {
                minimumSpeedStraightEffectsStarted = true;
                ship.PerformShipControllerActions(InputEvents.MinimumSpeedStraightAction);
            }
            else
            {
                if (fullSpeedStraightEffectsStarted && DeviationFromFullSpeedStraight > threshold)
                {
                    fullSpeedStraightEffectsStarted = false;
                    ship.StopShipControllerActions(InputEvents.FullSpeedStraightAction);
                }
                if (minimumSpeedStraightEffectsStarted && DeviationFromMinimumSpeedStraight < threshold)
                {
                    minimumSpeedStraightEffectsStarted = false;
                    ship.StopShipControllerActions(InputEvents.MinimumSpeedStraightAction);
                }
            }
        }

        void ProcessGamePadButtons()
        {
            if (Gamepad.current.leftShoulder.wasPressedThisFrame)
            {
                Idle = true;
                ship.PerformShipControllerActions(InputEvents.IdleAction);
            }
            if (Gamepad.current.leftShoulder.wasReleasedThisFrame)
            {
                Idle = false;
                ship.StopShipControllerActions(InputEvents.IdleAction);
            }

            if (Gamepad.current.rightShoulder.wasPressedThisFrame && !PhoneFlipState)
            {
                PhoneFlipState = true;
                ship.PerformShipControllerActions(InputEvents.FlipAction);
            }
            else if (Gamepad.current.rightShoulder.wasPressedThisFrame && PhoneFlipState)
            {
                PhoneFlipState = false;
                ship.StopShipControllerActions(InputEvents.FlipAction);
            }

            if (Gamepad.current.leftTrigger.wasPressedThisFrame)
            {
                ship.PerformShipControllerActions(InputEvents.LeftStickAction);
            }
            if (Gamepad.current.leftTrigger.wasReleasedThisFrame)
            {
                ship.StopShipControllerActions(InputEvents.LeftStickAction);
            }

            if (Gamepad.current.rightTrigger.wasPressedThisFrame)
            {
                ship.PerformShipControllerActions(InputEvents.RightStickAction);
            }
            if (Gamepad.current.rightTrigger.wasReleasedThisFrame)
            {
                ship.StopShipControllerActions(InputEvents.RightStickAction);
            }

            if (Gamepad.current.bButton.wasPressedThisFrame)
            {
                ship.PerformShipControllerActions(InputEvents.Button1Action);
            }
            if (Gamepad.current.bButton.wasReleasedThisFrame)
            {
                ship.StopShipControllerActions(InputEvents.Button1Action);
            }

            if (Gamepad.current.aButton.wasPressedThisFrame)
            {
                ship.PerformShipControllerActions(InputEvents.Button2Action);
            }
            if (Gamepad.current.aButton.wasReleasedThisFrame)
            {
                ship.StopShipControllerActions(InputEvents.Button2Action);
            }

            if (Gamepad.current.xButton.wasPressedThisFrame)
            {
                ship.PerformShipControllerActions(InputEvents.Button3Action);
            }
            if (Gamepad.current.xButton.wasReleasedThisFrame)
            {
                ship.StopShipControllerActions(InputEvents.Button3Action);
            }
        }

        public void Button1Press() 
        {
            ship.PerformShipControllerActions(InputEvents.Button1Action);
        }

        public void Button1Release()
        {
            ship.StopShipControllerActions(InputEvents.Button1Action);
        }

        public void Button2Press() 
        {
            ship.PerformShipControllerActions(InputEvents.Button2Action);
        }

        public void Button2Release()
        {
            ship.StopShipControllerActions(InputEvents.Button2Action);
        }

        public void Button3Press() 
        {
            ship.PerformShipControllerActions(InputEvents.Button3Action);
        }

        public void Button3Release()
        {
            ship.StopShipControllerActions(InputEvents.Button3Action);
        }

        public void SetPortrait(bool portrait)
        {
            Portrait = portrait; 
        }

        int GetClosestTouch(Vector2 target)
        {
            int touchIndex = 0;
            float minDistance = Mathf.Infinity;

            for (int i = 0; i < Input.touches.Length; i++)
            {
                if (Vector2.Distance(target, Input.touches[i].position) < minDistance)
                {
                    minDistance = Vector2.Distance(target, Input.touches[i].position);
                    touchIndex = i;
                }
            }
            return touchIndex;
        }
    }
}