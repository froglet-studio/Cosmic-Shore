using System.Collections;
using System.Threading;
using UnityEngine;
using Gamepad = UnityEngine.InputSystem.Gamepad;
using UnityEngine.UIElements;

namespace StarWriter.Core.IO
{
    public class InputController : MonoBehaviour
    {
        [SerializeField] ThreeButtonPanel threeButtonPanel;
        [SerializeField] GameObject rearView;
        

        #region Ship
        [SerializeField] public Ship ship;
        [SerializeField] public bool AutoPilotEnabled = false;
        #endregion

        float phoneFlipThreshold = .1f;
        public bool PhoneFlipState;
        public static ScreenOrientation currentOrientation;
        public bool Portrait = false;

        bool leftStickEffectsStarted = false;
        bool rightStickEffectsStarted = false;
        bool fullSpeedStraightEffectsStarted = false;
        bool minimumSpeedStraightEffectsStarted = false;

        int leftTouchIndex = 0, rightTouchIndex = 0;
        bool oneFinger = false;
        bool leftActive = true;

        public float XSum;
        public float YSum;
        public float XDiff;
        public float YDiff;

        float JoystickRadius = 350f;
        public Vector2 RightJoystick = Vector2.zero;
        public Vector2 LeftJoystick = Vector2.zero;

        Vector2 RightJoystickStart;
        Vector2 LeftJoystickStart;

        public bool Idle;

        UnityEngine.Gyroscope gyro;
        Quaternion derivedCorrection;
        float gyroInitializationAcceptableRange = .05f;

        public bool Paused {  get => inputPaused; }
        public bool isGyroEnabled = false;
        bool invertYEnabled = false;
        bool inputPaused;

        Vector2 leftInput = new Vector2(200, 200);
        Vector2 rightInput = new Vector2(Screen.currentResolution.width - 200, 200);

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
            Debug.Log($"joystick readius {400 / Screen.dpi}");
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
                leftInput.x = Gamepad.current.leftStick.x.ReadValue();
                leftInput.y = Gamepad.current.leftStick.y.ReadValue();
                rightInput.x = Gamepad.current.rightStick.x.ReadValue();
                rightInput.y = Gamepad.current.rightStick.y.ReadValue();

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

                //Debug.Log($"rightInput {rightInput}, leftInput {leftInput}");
                XSum = Ease(rightInput.x + leftInput.x); 
                YSum = -Ease(rightInput.y + leftInput.y); //negative is because joysitcks and unity axes don't agree
                XDiff = (leftInput.x - rightInput.x + 2.1f) / 4.1f;
                YDiff = Ease(rightInput.y - leftInput.y);

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

                
                var threeFingerFumble = false;
                if (Input.touchCount >= 3)
                {
                    // Sub select the two best touch inputs here
                    // If we have more than two touches, find the closest to each of the last touch positions we used
                    threeFingerFumble = true;

                    float minLeftTouchDistance = Vector2.Distance(leftInput, Input.touches[0].position);
                    float minRightTouchDistance = Vector2.Distance(rightInput, Input.touches[0].position);

                    for (int i = 1; i < Input.touches.Length; i++)
                    {
                        if (Vector2.Distance(leftInput, Input.touches[i].position) < minLeftTouchDistance)
                        {
                            minLeftTouchDistance = Vector2.Distance(leftInput, Input.touches[i].position);
                            leftTouchIndex = i;
                        }
                        if (Vector2.Distance(rightInput, Input.touches[i].position) < minRightTouchDistance)
                        {
                            minRightTouchDistance = Vector2.Distance(rightInput, Input.touches[i].position);
                            rightTouchIndex = i;
                        }
                    }
                }
                
                if (Input.touchCount == 2 || threeFingerFumble)
                {
                    // If we didn't fat finger the phone, find the 
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

                    leftInput = Input.touches[leftTouchIndex].position;
                    rightInput = Input.touches[rightTouchIndex].position;

                    if (Portrait)
                    {
                        rightInput = leftInput; // if your palm hits it is better to take the one closer to the top.
                        leftActive = false;
                    }

                    HandleJoystick(ref LeftJoystickStart, leftTouchIndex, ref LeftJoystick);
                    HandleJoystick(ref RightJoystickStart, rightTouchIndex, ref RightJoystick);

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
                    oneFinger = true;
                    //if (Portrait)
                    //{
                    //    rightInput = Input.touches[0].position;
                    //    leftInput = new Vector2(Screen.currentResolution.width / 4f, Screen.currentResolution.height / 2f);
                    //}
                    //else
                    //if (leftInput != Vector2.zero && rightInput != Vector2.zero)
                    //{
                        var position = Input.touches[0].position;

                        
                        if (Vector2.Distance(leftInput, position) < Vector2.Distance(rightInput, position))
                        {
                            if (!leftStickEffectsStarted)
                            {
                                leftStickEffectsStarted = true;
                                ship.PerformShipControllerActions(InputEvents.LeftStickAction);
                            }
                        leftInput = position;
                            leftTouchIndex = 0;
                            HandleJoystick(ref LeftJoystickStart, leftTouchIndex, ref LeftJoystick);
                            leftActive = true;
                        }
                        else
                        {
                            if (!rightStickEffectsStarted)
                            {
                                rightStickEffectsStarted = true;
                                ship.PerformShipControllerActions(InputEvents.RightStickAction);
                            }
                            rightInput = position;
                            rightTouchIndex = 0;
                            HandleJoystick(ref RightJoystickStart, rightTouchIndex, ref RightJoystick);
                            leftActive = false;
                        }

                    //}
                }
                else oneFinger = false;

                if (Input.touchCount > 0)
                {
                    Reparameterize();
                    CheckSpeedAndOrientation();

                    if (Portrait)
                    {
                        threeButtonPanel.FadeOutButtons();
                    }

                    if (Idle)
                    {
                        Idle = false;
                        ship.StopShipControllerActions(InputEvents.IdleAction);
                    }
                }
                else
                {
                    if (Portrait)
                    {
                        threeButtonPanel.FadeInButtons();
                        CheckSpeedAndOrientation();
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

        void HandleJoystick(ref Vector2 joystickStart, int touchIndex, ref Vector2 joystick)
        {
            Touch touch = Input.touches[touchIndex];

            if (touch.phase == TouchPhase.Began)
            {
                //joystickStart = (touchIndex == leftTouchIndex) ? leftInput : rightInput;
                joystickStart = touch.position;
            }

            Vector2 offset = touch.position - joystickStart;
            Vector2 clampedOffset = Vector2.ClampMagnitude(offset, JoystickRadius) / JoystickRadius;
            joystick = clampedOffset;
        }

        void Reparameterize()
        {
            //Debug.Log($"RightJoystick {RightJoystick}, LeftJoystick {LeftJoystick}");
            if (oneFinger || Portrait)
            {
                if (leftActive)
                {
                    XSum = Ease(LeftJoystick.x);
                    YSum = -Ease(LeftJoystick.y); //negative is because joysitcks and unity axes don't agree
                    XDiff = .5f;
                    YDiff = 0;
                }
                else
                {
                    XSum = Ease(RightJoystick.x);
                    YSum = -Ease(RightJoystick.y); //negative is because joysitcks and unity axes don't agree
                    XDiff = .5f;
                    YDiff = 0;
                }
            }
            else
            {
                XSum = Ease(RightJoystick.x + LeftJoystick.x);
                YSum = -Ease(RightJoystick.y + LeftJoystick.y); //negative is because joysitcks and unity axes don't agree
                XDiff = (LeftJoystick.x - RightJoystick.x + 2.1f) / 4.1f;
                YDiff = Ease(RightJoystick.y - LeftJoystick.y);
            }

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

        public void SetPortrait(bool value)
        {
            if (value)
            {
                threeButtonPanel.FadeInButtons(); // TODO: make these event driven instead?
                rearView.SetActive(true);
            }
            else
            {
                threeButtonPanel.FadeOutButtons();
                rearView.SetActive(false);
            }
            Portrait = value; 
        }

    }
}