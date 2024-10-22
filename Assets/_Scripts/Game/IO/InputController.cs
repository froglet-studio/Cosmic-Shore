using System.Collections;
using UnityEngine;
using CosmicShore.Core;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
using CosmicShore.Game.UI;
using CosmicShore.App.Systems;
using UnityEngine.InputSystem.Controls;
using Gyroscope = UnityEngine.InputSystem.Gyroscope;


namespace CosmicShore.Game.IO
{
    public class InputController : MonoBehaviour
    {
        [SerializeField] private GameCanvas gameCanvas;
        [SerializeField] public bool Portrait;

        [HideInInspector] public Ship ship;
        [HideInInspector] public bool AutoPilotEnabled;
        [HideInInspector] public static ScreenOrientation currentOrientation;

        const float PHONE_FLIP_THRESHOLD = 0.1f;
        const float PI_OVER_FOUR = 0.785f;
        const float MAP_SCALE_X = 2f;
        const float MAP_SCALE_Y = 2f;
        const float GYRO_INITIALIZATION_RANGE = 0.05f;

        bool phoneFlipState;
        bool leftStickEffectsStarted;
        bool rightStickEffectsStarted;
        bool fullSpeedStraightEffectsStarted;
        bool minimumSpeedStraightEffectsStarted;
        int leftTouchIndex, rightTouchIndex;

        [HideInInspector] public float XSum, YSum, XDiff, YDiff;
        [HideInInspector] public bool Idle, Paused;
        [HideInInspector] public bool isGyroEnabled, invertYEnabled, invertThrottleEnabled;
        [HideInInspector] public bool OneTouchLeft;

        [HideInInspector] public Vector2 RightJoystickHome, LeftJoystickHome;
        [HideInInspector] public Vector2 RightClampedPosition, LeftClampedPosition;
        [HideInInspector] public Vector2 RightJoystickStart, LeftJoystickStart;
        [HideInInspector] public Vector2 RightNormalizedJoystickPosition, LeftNormalizedJoystickPosition;
        [HideInInspector] public Vector2 EasedRightJoystickPosition, EasedLeftJoystickPosition;

        private Vector2 RightJoystickValue, LeftJoystickValue;
        public Vector2 SingleTouchValue;
        public Vector3 ThreeDPosition { get; private set; }

        float JoystickRadius;
        Gyroscope gyro;
        Quaternion derivedCorrection;
        Quaternion inverseInitialRotation = new(0, 0, 0, 0);

        #region Unity Lifecycle Methods

        private void OnEnable()
        {
            GameSetting.OnChangeInvertYEnabledStatus += OnToggleInvertY;
            GameSetting.OnChangeInvertThrottleEnabledStatus += OnToggleInvertThrottle;
            EnhancedTouchSupport.Enable();
        }

        private void OnDisable()
        {
            GameSetting.OnChangeInvertYEnabledStatus -= OnToggleInvertY;
            GameSetting.OnChangeInvertThrottleEnabledStatus -= OnToggleInvertThrottle;
            EnhancedTouchSupport.Disable();
        }

        private void Start()
        {
            InitializeJoysticks();
            InitializeGyroscope();
            LoadSettings();
        }

        private void Update()
        {
            if (PauseSystem.Paused || Paused) return;
            ReceiveInput();
        }

        #endregion

        #region Initialization Methods

        private void InitializeJoysticks()
        {
            JoystickRadius = Screen.dpi;
            LeftJoystickValue = LeftClampedPosition = LeftJoystickHome = new Vector2(JoystickRadius, JoystickRadius);
            RightJoystickValue = RightClampedPosition = RightJoystickHome = new Vector2(Screen.currentResolution.width - JoystickRadius, JoystickRadius);
        }

        private void InitializeGyroscope()
        {
            InputSystem.EnableDevice(Gyroscope.current);
            gyro = Gyroscope.current;
            StartCoroutine(GyroInitializationCoroutine());
        }

        private void LoadSettings()
        {
            invertYEnabled = GameSetting.Instance.InvertYEnabled;
            invertThrottleEnabled = GameSetting.Instance.InvertThrottleEnabled;
        }

        #endregion

        #region Input Processing Methods

        private void ReceiveInput()
        {
            if (AutoPilotEnabled)
            {
                ProcessAutoPilotInput();
            }
            else if (Gamepad.current != null)
            {
                ProcessGamepadInput();
            }
            else
            {
                ProcessTouchInput();
            }
        }

        private void ProcessAutoPilotInput()
        {
            if (ship.ShipStatus.SingleStickControls)
            {
                EasedLeftJoystickPosition = new Vector2(ship.AutoPilot.X, ship.AutoPilot.Y);
            }
            else
            {
                XSum = ship.AutoPilot.XSum;
                YSum = ship.AutoPilot.YSum;
                XDiff = ship.AutoPilot.XDiff;
                YDiff = ship.AutoPilot.YDiff;
            }
            PerformSpeedAndDirectionalEffects();
        }

        private void ProcessGamepadInput()
        {
            LeftNormalizedJoystickPosition = Gamepad.current.leftStick.ReadValue();
            RightNormalizedJoystickPosition = Gamepad.current.rightStick.ReadValue();

            Reparameterize();
            ProcessGamePadButtons();
            PerformSpeedAndDirectionalEffects();
        }

        private void ProcessTouchInput()
        {
            HandlePhoneOrientation();
            HandleMultiTouch();
            HandleSingleTouch();

            if (Touch.activeTouches.Count > 0)
            {
                Reparameterize();
                PerformSpeedAndDirectionalEffects();
                HandleIdleState(false);
            }
            else
            {
                ResetInputValues();
                HandleIdleState(true);
            }
        }

        private void HandlePhoneOrientation()
        {
            if (Portrait)
            {
                ship.SetShipUp(90);
            }
            else if (Mathf.Abs(Input.acceleration.y) >= PHONE_FLIP_THRESHOLD)
            {
                UpdatePhoneFlipState();
            }
        }

        private void UpdatePhoneFlipState()
        {
            bool newFlipState = Input.acceleration.y > 0;
            if (newFlipState != phoneFlipState)
            {
                phoneFlipState = newFlipState;
                if (phoneFlipState)
                {
                    ship.PerformShipControllerActions(InputEvents.FlipAction);
                    currentOrientation = ScreenOrientation.LandscapeRight;
                }
                else
                {
                    ship.StopShipControllerActions(InputEvents.FlipAction);
                    currentOrientation = ScreenOrientation.LandscapeLeft;
                }
                Debug.Log($"Phone flip state change detected - new flip state: {phoneFlipState}, acceleration.y: {Input.acceleration.y}");
            }
        }

        private void HandleMultiTouch()
        {
            if (Touch.activeTouches.Count >= 2)
            {
                AssignTouchIndices();
                UpdateJoystickValues();
                HandleJoystick(ref LeftJoystickStart, leftTouchIndex, ref LeftNormalizedJoystickPosition, ref LeftClampedPosition);
                HandleJoystick(ref RightJoystickStart, rightTouchIndex, ref RightNormalizedJoystickPosition, ref RightClampedPosition);
                StopStickEffects();
            }
        }

        private void AssignTouchIndices()
        {
            if (Touch.activeTouches.Count == 2)
            {
                if (Touch.activeTouches[0].screenPosition.x <= Touch.activeTouches[1].screenPosition.x)
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
            else
            {
                leftTouchIndex = GetClosestTouch(LeftJoystickValue);
                rightTouchIndex = GetClosestTouch(RightJoystickValue);
            }
        }

        private void UpdateJoystickValues()
        {
            LeftJoystickValue = Touch.activeTouches[leftTouchIndex].screenPosition;
            RightJoystickValue = Touch.activeTouches[rightTouchIndex].screenPosition;
        }

        private void StopStickEffects()
        {
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

        private void HandleSingleTouch()
        {
            if (Touch.activeTouches.Count == 1)
            {
                var position = Touch.activeTouches[0].screenPosition;
                if (ship && ship.ShipStatus.CommandStickControls)
                {
                    ProcessCommandStickControls(position);
                }
                ProcessSingleTouchJoystick(position);
            }
        }

        private void ProcessCommandStickControls(Vector2 position)
        {
            SingleTouchValue = position;
            var tempThreeDPosition = new Vector3((SingleTouchValue.x - Screen.width / 2) * MAP_SCALE_X, (SingleTouchValue.y - Screen.height / 2) * MAP_SCALE_Y, 0);

            if (tempThreeDPosition.sqrMagnitude < 10000 && Touch.activeTouches[0].phase == TouchPhase.Began)
            {
                ship.PerformShipControllerActions(InputEvents.NodeTapAction);
            }
            else if ((tempThreeDPosition - ship.transform.position).sqrMagnitude < 10000 && Touch.activeTouches[0].phase == TouchPhase.Began)
            {
                ship.PerformShipControllerActions(InputEvents.SelfTapAction);
            }
            else
            {
                ThreeDPosition = tempThreeDPosition;
            }
        }

        private void ProcessSingleTouchJoystick(Vector2 position)
        {
            if (Vector2.Distance(LeftJoystickValue, position) < Vector2.Distance(RightJoystickValue, position))
            {
                HandleLeftJoystick(position);
            }
            else
            {
                HandleRightJoystick(position);
            }
        }

        private void HandleLeftJoystick(Vector2 position)
        {
            if (!leftStickEffectsStarted)
            {
                leftStickEffectsStarted = true;
                ship.PerformShipControllerActions(InputEvents.LeftStickAction);
            }
            LeftJoystickValue = position;
            leftTouchIndex = 0;
            OneTouchLeft = true;
            HandleJoystick(ref LeftJoystickStart, leftTouchIndex, ref LeftNormalizedJoystickPosition, ref LeftClampedPosition);
            RightNormalizedJoystickPosition = Vector3.Lerp(RightNormalizedJoystickPosition, Vector3.zero, 7 * Time.deltaTime);
        }

        private void HandleRightJoystick(Vector2 position)
        {
            if (!rightStickEffectsStarted)
            {
                rightStickEffectsStarted = true;
                if (ship != null)
                    ship.PerformShipControllerActions(InputEvents.RightStickAction);
            }
            RightJoystickValue = position;
            rightTouchIndex = 0;
            OneTouchLeft = false;
            HandleJoystick(ref RightJoystickStart, rightTouchIndex, ref RightNormalizedJoystickPosition, ref RightClampedPosition);
            LeftNormalizedJoystickPosition = Vector3.Lerp(LeftNormalizedJoystickPosition, Vector3.zero, 7 * Time.deltaTime);
        }

        private void ResetInputValues()
        {
            XSum = 0;
            YSum = 0;
            XDiff = 0;
            YDiff = 0;
        }

        private void HandleIdleState(bool isIdle)
        {
            if (isIdle != Idle)
            {
                Idle = isIdle;
                if (Idle)
                {
                    if (ship) ship.PerformShipControllerActions(InputEvents.IdleAction);
                }
                else
                {
                    ship.StopShipControllerActions(InputEvents.IdleAction);
                }
            }
        }

        #endregion

        #region Helper Methods

        private void HandleJoystick(ref Vector2 joystickStart, int touchIndex, ref Vector2 joystick, ref Vector2 clampedPosition)
        {
            Touch touch = Touch.activeTouches[touchIndex];

            if (touch.phase == TouchPhase.Began || joystickStart == Vector2.zero)
                joystickStart = touch.screenPosition;

            Vector2 offset = touch.screenPosition - joystickStart;
            Vector2 clampedOffset = Vector2.ClampMagnitude(offset, JoystickRadius);
            clampedPosition = joystickStart + clampedOffset;
            Vector2 normalizedOffset = clampedOffset / JoystickRadius;
            joystick = normalizedOffset;
        }

        private void Reparameterize()
        {
            EasedRightJoystickPosition = new Vector2(Ease(2 * RightNormalizedJoystickPosition.x), Ease(2 * RightNormalizedJoystickPosition.y));
            EasedLeftJoystickPosition = new Vector2(Ease(2 * LeftNormalizedJoystickPosition.x), Ease(2 * LeftNormalizedJoystickPosition.y));

            XSum = Ease(RightNormalizedJoystickPosition.x + LeftNormalizedJoystickPosition.x);
            YSum = -Ease(RightNormalizedJoystickPosition.y + LeftNormalizedJoystickPosition.y);
            XDiff = (RightNormalizedJoystickPosition.x - LeftNormalizedJoystickPosition.x + 2) / 4;
            YDiff = Ease(RightNormalizedJoystickPosition.y - LeftNormalizedJoystickPosition.y);

            if (invertYEnabled)
                YSum *= -1;
            if (invertThrottleEnabled)
                YDiff = 1 - YDiff;
        }

        private float Ease(float input)
        {
            return input < 0 ? (Mathf.Cos(input * PI_OVER_FOUR) - 1) : -(Mathf.Cos(input * PI_OVER_FOUR) - 1);
        }

        private void PerformSpeedAndDirectionalEffects()
        {
            float threshold = .3f;
            float sumOfRotations = Mathf.Abs(YDiff) + Mathf.Abs(YSum) + Mathf.Abs(XSum);
            float DeviationFromFullSpeedStraight = (1 - XDiff) + sumOfRotations;
            float DeviationFromMinimumSpeedStraight = XDiff + sumOfRotations;

            HandleFullSpeedStraight(DeviationFromFullSpeedStraight, threshold);
            HandleMinimumSpeedStraight(DeviationFromMinimumSpeedStraight, threshold);
        }

        private void HandleFullSpeedStraight(float deviation, float threshold)
        {
            if (deviation < threshold && !fullSpeedStraightEffectsStarted)
            {
                fullSpeedStraightEffectsStarted = true;
                ship.PerformShipControllerActions(InputEvents.FullSpeedStraightAction);
            }
            else if (fullSpeedStraightEffectsStarted && deviation > threshold)
            {
                fullSpeedStraightEffectsStarted = false;
                ship.StopShipControllerActions(InputEvents.FullSpeedStraightAction);
            }
        }

        private void HandleMinimumSpeedStraight(float deviation, float threshold)
        {
            if (deviation < threshold && !minimumSpeedStraightEffectsStarted)
            {
                minimumSpeedStraightEffectsStarted = true;
                ship.PerformShipControllerActions(InputEvents.MinimumSpeedStraightAction);
            }
            else if (minimumSpeedStraightEffectsStarted && deviation > threshold)
            {
                minimumSpeedStraightEffectsStarted = false;
                ship.StopShipControllerActions(InputEvents.MinimumSpeedStraightAction);
            }
        }

        private int GetClosestTouch(Vector2 target)
        {
            int touchIndex = 0;
            float minDistance = Mathf.Infinity;

            for (int i = 0; i < Touch.activeTouches.Count; i++)
            {
                float distance = Vector2.Distance(target, Touch.activeTouches[i].screenPosition);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    touchIndex = i;
                }
            }
            return touchIndex;
        }

        #endregion

        #region Gyroscope Methods

        private IEnumerator GyroInitializationCoroutine()
        {
            derivedCorrection = GyroQuaternionToUnityQuaternion(Quaternion.Inverse(new Quaternion(0, .65f, .75f, 0)));
            inverseInitialRotation = Quaternion.identity;

            while (!SystemInfo.supportsGyroscope || UnityEngine.InputSystem.Gyroscope.current == null)
            {
                yield return new WaitForSeconds(0.1f);
            }

            var gyro = Input.gyro;
            var lastAttitude = gyro.attitude;
            yield return new WaitForSeconds(0.1f);

            while (!(1 - Mathf.Abs(Quaternion.Dot(lastAttitude, gyro.attitude)) < GYRO_INITIALIZATION_RANGE))
            {
                lastAttitude = gyro.attitude;
                yield return new WaitForSeconds(0.1f);
            }

            inverseInitialRotation = Quaternion.Inverse(GyroQuaternionToUnityQuaternion(gyro.attitude) * derivedCorrection);
        }

        public Quaternion GetGyroRotation()
        {
            return inverseInitialRotation * GyroQuaternionToUnityQuaternion(Input.gyro.attitude) * derivedCorrection;
        }

        private Quaternion GyroQuaternionToUnityQuaternion(Quaternion q)
        {
            return new Quaternion(q.x, -q.z, q.y, q.w);
        }

        public void OnToggleGyro(bool status)
        {
            Debug.Log($"InputController.OnToggleGyro - status: {status}");
            if (SystemInfo.supportsGyroscope && status)
            {
                inverseInitialRotation = Quaternion.Inverse(GyroQuaternionToUnityQuaternion(Input.gyro.attitude) * derivedCorrection);
            }

            isGyroEnabled = status;
        }

        #endregion

        #region Gamepad Methods

        private void ProcessGamePadButtons()
        {
            HandleGamepadButton(Gamepad.current.leftShoulder, InputEvents.IdleAction, ref Idle);
            HandleGamepadButton(Gamepad.current.rightShoulder, InputEvents.FlipAction, ref phoneFlipState);
            HandleGamepadTrigger(Gamepad.current.leftTrigger, InputEvents.LeftStickAction);
            HandleGamepadTrigger(Gamepad.current.rightTrigger, InputEvents.RightStickAction);
            HandleGamepadButton(Gamepad.current.bButton, InputEvents.Button1Action);
            HandleGamepadButton(Gamepad.current.aButton, InputEvents.Button2Action);
            HandleGamepadButton(Gamepad.current.rightStickButton, InputEvents.Button2Action);
            HandleGamepadButton(Gamepad.current.xButton, InputEvents.Button3Action);
        }

        private void HandleGamepadButton(ButtonControl button, InputEvents action, ref bool stateFlag)
        {
            if (button.wasPressedThisFrame)
            {
                stateFlag = !stateFlag;
                if (stateFlag)
                    ship.PerformShipControllerActions(action);
                else
                    ship.StopShipControllerActions(action);
            }
        }

        private void HandleGamepadButton(ButtonControl button, InputEvents action)
        {
            if (button.wasPressedThisFrame)
                ship.PerformShipControllerActions(action);
            if (button.wasReleasedThisFrame)
                ship.StopShipControllerActions(action);
        }

        private void HandleGamepadTrigger(ButtonControl trigger, InputEvents action)
        {
            if (trigger.wasPressedThisFrame)
                ship.PerformShipControllerActions(action);
            if (trigger.wasReleasedThisFrame)
                ship.StopShipControllerActions(action);
        }

        #endregion

        #region Public Methods

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

        public static bool UsingGamepad()
        {
            return Gamepad.current != null;
        }

        private void OnToggleInvertY(bool status)
        {
            Debug.Log($"InputController.OnToggleInvertY - status: {status}");
            invertYEnabled = status;
        }

        private void OnToggleInvertThrottle(bool status)
        {
            Debug.Log($"InputController.OnToggleInvertThrottle - status: {status}");
            invertThrottleEnabled = status;
        }

        #endregion
    }
}