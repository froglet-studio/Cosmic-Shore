using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StarWriter.Core.Input
{
    public class InputController : MonoBehaviour
    {
        #region Ship
        [SerializeField] public Ship ship;
        [SerializeField] public bool AutoPilotEnabled = false;
        #endregion

        float phoneFlipThreshold = .1f;
        public bool PhoneFlipState;
        public static ScreenOrientation currentOrientation;
        public bool portrait = false;

        bool leftStickEffectsStarted = false;
        bool rightStickEffectsStarted = false;
        bool fullSpeedStraightEffectsStarted = false;
        bool minimumSpeedStraightEffectsStarted = false;

        public float XSum;
        public float YSum;
        public float XDiff;
        public float YDiff;

        public bool Idle;

        UnityEngine.Gyroscope gyro;
        Quaternion derivedCorrection;
        float gyroInitializationAcceptableRange = .05f;

        public bool Paused {  get => inputPaused; }
        public bool isGyroEnabled = false;
        bool invertYEnabled = false;
        bool inputPaused;

        Vector2 leftTouch, rightTouch;

        Quaternion inverseInitialRotation=new(0,0,0,0);

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
            gyro = UnityEngine.Input.gyro;
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
            if (PauseSystem.Paused || inputPaused) return;

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
            }
            else if (Gamepad.current != null)
            {
                leftTouch.x = Gamepad.current.leftStick.x.ReadValue();
                leftTouch.y = Gamepad.current.leftStick.y.ReadValue();
                rightTouch.x = Gamepad.current.rightStick.x.ReadValue();
                rightTouch.y = Gamepad.current.rightStick.y.ReadValue();

                //if (Gamepad.current.leftStick.IsActuated() || Gamepad.current.rightStick.IsActuated() && Idle)
                //{
                //    Idle = false;
                //    ship.StopShipControllerActions(InputEvents.IdleAction);
                //}
                //else if (!Idle)
                //{
                //    Idle = true;
                //    ship.PerformShipControllerActions(InputEvents.IdleAction);
                //}

                XSum = Ease(rightTouch.x + leftTouch.x); //negative is because joysitcks and unity axes don't agree
                YSum = -Ease(rightTouch.y + leftTouch.y);
                XDiff = (leftTouch.x - rightTouch.x + 2.1f) / 4.1f;
                YDiff = Ease(rightTouch.y - leftTouch.y);

                if (invertYEnabled)
                    YSum *= -1;

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

                CheckSpeedAndOrientation();
            }
            else
            {
                if (portrait)
                {
                    ship.SetShipUp(90);
                }
                else if (Mathf.Abs(UnityEngine.Input.acceleration.y) >= phoneFlipThreshold)
                {
                    if (UnityEngine.Input.acceleration.y < 0 && PhoneFlipState)
                    {
                        PhoneFlipState = false;
                        ship.StopShipControllerActions(InputEvents.FlipAction);
                        ship.FlipShipRightsideUp();

                        currentOrientation = ScreenOrientation.LandscapeLeft;

                        Debug.Log($"InputController Phone flip state change detected - new flip state: {PhoneFlipState}, acceleration.y: {UnityEngine.Input.acceleration.y}");
                    }
                    else if (UnityEngine.Input.acceleration.y > 0 && !PhoneFlipState)
                    {
                        PhoneFlipState = true;
                        ship.PerformShipControllerActions(InputEvents.FlipAction);
                        ship.FlipShipUpsideDown(); // TODO make shipAction

                        currentOrientation = ScreenOrientation.LandscapeRight;

                        Debug.Log($"InputController Phone flip state change detected - new flip state: {PhoneFlipState}, acceleration.y: {UnityEngine.Input.acceleration.y}");
                    }
                }

                var threeFingerFumble = false;
                if (UnityEngine.Input.touches.Length >= 3)
                {
                    // Sub select the two best touch inputs here
                    // If we have more than two touches, find the closest to each of the last touch positions we used
                    threeFingerFumble = true;
                    int leftTouchIndex = 0, rightTouchIndex = 0;
                    float minLeftTouchDistance = Vector2.Distance(leftTouch, UnityEngine.Input.touches[0].position);
                    float minRightTouchDistance = Vector2.Distance(rightTouch, UnityEngine.Input.touches[0].position);

                    for (int i = 1; i < UnityEngine.Input.touches.Length; i++)
                    {
                        if (Vector2.Distance(leftTouch, UnityEngine.Input.touches[i].position) < minLeftTouchDistance)
                        {
                            minLeftTouchDistance = Vector2.Distance(leftTouch, UnityEngine.Input.touches[i].position);
                            leftTouchIndex = i;
                        }
                        if (Vector2.Distance(rightTouch, UnityEngine.Input.touches[i].position) < minRightTouchDistance)
                        {
                            minRightTouchDistance = Vector2.Distance(rightTouch, UnityEngine.Input.touches[i].position);
                            rightTouchIndex = i;
                        }
                    }
                    leftTouch = UnityEngine.Input.touches[leftTouchIndex].position;
                    rightTouch = UnityEngine.Input.touches[rightTouchIndex].position;
                }
                if (UnityEngine.Input.touches.Length == 2 || threeFingerFumble)
                {
                    // If we didn't fat finger the phone, find the 
                    if (!threeFingerFumble)
                    {
                        if (UnityEngine.Input.touches[0].position.x <= UnityEngine.Input.touches[1].position.x)
                        {
                            leftTouch = UnityEngine.Input.touches[0].position;
                            rightTouch = UnityEngine.Input.touches[1].position;
                        }
                        else
                        {
                            leftTouch = UnityEngine.Input.touches[1].position;
                            rightTouch = UnityEngine.Input.touches[0].position;
                        }
                    }

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
                else if (UnityEngine.Input.touches.Length == 1)
                {
                    if (leftTouch != Vector2.zero && rightTouch != Vector2.zero)
                    {
                        var position = UnityEngine.Input.touches[0].position;

                        if (Vector2.Distance(leftTouch, position) < Vector2.Distance(rightTouch, position) && !leftStickEffectsStarted)
                        {
                            leftStickEffectsStarted = true;
                            ship.PerformShipControllerActions(InputEvents.LeftStickAction);
                        }
                        else if (Vector2.Distance(leftTouch, position) < Vector2.Distance(rightTouch, position))
                        {
                            leftTouch = position;
                        }
                        else if (!rightStickEffectsStarted)
                        {
                            rightStickEffectsStarted = true;
                            ship.PerformShipControllerActions(InputEvents.RightStickAction);
                        }
                        else
                        {
                            rightTouch = position;
                        }
                    }
                }

                if (UnityEngine.Input.touches.Length > 0)
                {
                    Reparameterize();

                    CheckSpeedAndOrientation();

                    if (Idle)
                    {
                        Idle = false;
                        ship.StopShipControllerActions(InputEvents.IdleAction);
                    }
                }
                else
                {
                    Idle = true;

                    XSum = 0;
                    YSum = 0;
                    XDiff = 0;
                    YDiff = 0;
                    
                    ship.PerformShipControllerActions(InputEvents.IdleAction); // consider placing some stop methods for other Input events here  
                }
            }
        }

        void Reparameterize()
        {
            XSum = ((rightTouch.x + leftTouch.x) / Screen.currentResolution.width) - 1; 
            YSum = -(((rightTouch.y + leftTouch.y) / Screen.currentResolution.height) - 1); //negative is because joysitcks and unity axes don't agree
            XDiff = (rightTouch.x - leftTouch.x) / Screen.currentResolution.width;
            YDiff = (rightTouch.y - leftTouch.y) / Screen.currentResolution.width;

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
            return input < 0 ? (Mathf.Cos(input) - 1) / 2 : -(Mathf.Cos(input) - 1) / 2;
        }

        void CheckSpeedAndOrientation()
        {
            float threshold = .3f;
            float sumOfRotations = Mathf.Abs(YDiff) + Mathf.Abs(YSum) + Mathf.Abs(XSum);
            float DeviationFromFullSpeedStraight = (1 - XDiff) + sumOfRotations;
            float DeviationFromMinimumSpeedStraight = XDiff + sumOfRotations;


            if (DeviationFromFullSpeedStraight < threshold)
            {
                if (!fullSpeedStraightEffectsStarted)
                {
                    fullSpeedStraightEffectsStarted = true;
                    ship.PerformShipControllerActions(InputEvents.FullSpeedStraightAction);
                }
            }
            else if (DeviationFromMinimumSpeedStraight < threshold)
            {
                if (!minimumSpeedStraightEffectsStarted)
                {
                    minimumSpeedStraightEffectsStarted = true;
                    ship.PerformShipControllerActions(InputEvents.MinimumSpeedStraightAction);
                }
            }
            else
            {
                if (fullSpeedStraightEffectsStarted)
                {
                    fullSpeedStraightEffectsStarted = false;
                    ship.StopShipControllerActions(InputEvents.FullSpeedStraightAction);
                }
                if (minimumSpeedStraightEffectsStarted)
                {
                    minimumSpeedStraightEffectsStarted = false;
                    ship.StopShipControllerActions(InputEvents.MinimumSpeedStraightAction);
                }

            }
        }

        public void PauseInput(bool paused=true)
        {
            inputPaused = paused;
        }
    }
}