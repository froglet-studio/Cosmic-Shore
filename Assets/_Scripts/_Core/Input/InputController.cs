using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StarWriter.Core.Input
{
    public class InputController : MonoBehaviour
    {
        #region Ship
        [SerializeField] public Ship ship;
        Transform shipTransform;
        ShipAnimation shipAnimation;
        ShipData shipData;
        #endregion

        float phoneFlipThreshold = .1f;
        public bool PhoneFlipState;
        public static ScreenOrientation currentOrientation;

        public delegate void Boost(string uuid, float amount);
        public static event Boost OnBoost;
        string uuid;

        public bool drifting = false;
        public float boostDecay = 1;

        bool LeftStickEffectsStarted = false;
        bool RightStickEffectsStarted = false;
        bool FullSpeedStraightEffectsStopped = true;

        float speed;

        public float initialDThrottle = 10f; 
        public float initialThrottleScaler = 50;

        public float defaultThrottle;
        public float throttleScaler;

        float xSum;
        float ySum;
        float xDiff;
        float yDiff;

        public float rotationThrottleScaler = 0;
        public float rotationScaler = 130f;

        readonly float lerpAmount = 2f;
        readonly float smallLerpAmount = .7f;

        UnityEngine.Gyroscope gyro;
        Quaternion derivedCorrection;
        Quaternion displacementQ;
        Quaternion inverseInitialRotation=new(0,0,0,0);

        bool isGyroEnabled = false;
        bool invertYEnabled = false;
        float gyroInitializationAcceptableRange = .05f;

        Vector2 leftTouch, rightTouch;

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
            shipTransform = ship.transform;
            shipAnimation = ship.GetComponent<ShipAnimation>();
            shipData = ship.GetComponent<ShipData>();

            defaultThrottle = initialDThrottle;
            throttleScaler = initialThrottleScaler;

            uuid = GameObject.FindWithTag("Player").GetComponent<Player>().PlayerUUID;
            
            // TODO: why is this here?
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            gyro = UnityEngine.Input.gyro;
            gyro.enabled = true;

            StartCoroutine(GyroInitializationCoroutine());

            displacementQ = shipTransform.rotation;

            invertYEnabled = GameSetting.Instance.InvertYEnabled;
        }

        IEnumerator GyroInitializationCoroutine()
        {
            derivedCorrection = GyroToUnity(Quaternion.Inverse(new Quaternion(0, .65f, .75f, 0)));
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

            inverseInitialRotation = Quaternion.Inverse(GyroToUnity(gyro.attitude) * derivedCorrection);
        }

        void Update()
        {
            if (PauseSystem.Paused) return;

            // Convert two finger touch into values for displacement, speed, and ship animations
            ReceiveTouchInput();

            RotateShip();

            // Move ship velocityDirection
            shipData.inputSpeed = speed;
            
            if (!drifting)
            {
                shipTransform.position += shipData.speed * Time.deltaTime * shipTransform.forward;
                shipData.velocityDirection = shipTransform.forward;
                shipData.blockRotation = shipTransform.rotation;
            }
        }

        void RotateShip()
        {
            if (isGyroEnabled && !Equals(inverseInitialRotation, new Quaternion(0, 0, 0, 0)))
            {
                // Updates GameObjects blockRotation from input device's gyroscope
                shipTransform.rotation = Quaternion.Lerp(
                                            shipTransform.rotation,
                                            displacementQ * inverseInitialRotation * GyroToUnity(gyro.attitude) * derivedCorrection,
                                            lerpAmount);
                
            }
            else
            {
                shipTransform.rotation = Quaternion.Lerp(
                                            shipTransform.rotation,
                                            displacementQ,
                                            lerpAmount);
            }
        }

        void ReceiveTouchInput()
        {
            //Debug.Log($"Gamepad.Current: {Gamepad.current}");
            if (Gamepad.current != null)
            {
                leftTouch.x = Gamepad.current.leftStick.x.ReadValue();
                leftTouch.y = Gamepad.current.leftStick.y.ReadValue();
                rightTouch.x = Gamepad.current.rightStick.x.ReadValue();
                rightTouch.y = Gamepad.current.rightStick.y.ReadValue();

                xSum = Ease(rightTouch.x + leftTouch.x);
                ySum = Ease(rightTouch.y + leftTouch.y);
                xDiff = (leftTouch.x - rightTouch.x + 2.1f) / 4.1f;
                yDiff = Ease(rightTouch.y - leftTouch.y);

                if (invertYEnabled)
                    ySum *= -1;

                if (Gamepad.current.rightShoulder.wasPressedThisFrame && !PhoneFlipState)
                {
                    PhoneFlipState = true;
                    ship.StartFlipEffects();
                }
                else if (Gamepad.current.rightShoulder.wasPressedThisFrame && PhoneFlipState)
                {
                    PhoneFlipState = false;
                    ship.StopFlipEffects();
                }

                if (Gamepad.current.leftTrigger.wasPressedThisFrame)
                {
                    //LeftStickEffectsStarted = false;
                    ship.PerformLeftStickEffects();
                }
                if (Gamepad.current.rightTrigger.wasPressedThisFrame)
                {
                    //RightStickEffectsStarted = false;
                    ship.PerformRightStickEffects();
                }
                if (Gamepad.current.leftTrigger.wasReleasedThisFrame) 
                {
                    //LeftStickEffectsStarted = true;
                    ship.StopLeftStickEffects(); 
                }
                if (Gamepad.current.rightTrigger.wasReleasedThisFrame)
                {
                    //RightStickEffectsStarted = true;
                    ship.StopRightStickEffects();
                }

                Pitch();
                Yaw();
                Roll();

                if (drifting) Drift();
                else if (boostDecay <= 1) CheckThrottle();

                shipAnimation.PerformShipAnimations(ySum, xSum, yDiff, xDiff);
            }
            else
            {
                if (Mathf.Abs(UnityEngine.Input.acceleration.y) >= phoneFlipThreshold)
                {
                    if (UnityEngine.Input.acceleration.y < 0 && PhoneFlipState)
                    {
                        PhoneFlipState = false;
                        ship.StopFlipEffects();
                        ship.FlipShipRightsideUp();

                        currentOrientation = ScreenOrientation.LandscapeLeft;

                        Debug.Log($"InputController Phone flip state change detected - new flip state: {PhoneFlipState}, acceleration.y: {UnityEngine.Input.acceleration.y}");
                    }
                    else if (UnityEngine.Input.acceleration.y > 0 && !PhoneFlipState)
                    {
                        PhoneFlipState = true;
                        ship.StartFlipEffects();
                        ship.FlipShipUpsideDown();

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

                    Reparameterize();

                    Pitch();
                    Yaw();
                    Roll();

                    if (LeftStickEffectsStarted)
                    {
                        LeftStickEffectsStarted = false;
                        ship.StopLeftStickEffects();
                    }
                    if (RightStickEffectsStarted)
                    {
                        RightStickEffectsStarted = false;
                        ship.StopRightStickEffects();
                    }

                    if (drifting)
                        Drift();
                    else if (boostDecay <= 1) CheckThrottle();

                    shipAnimation.PerformShipAnimations(ySum, xSum, yDiff, xDiff);
                }

                else if (UnityEngine.Input.touches.Length == 1)
                {
                    if (leftTouch != Vector2.zero && rightTouch != Vector2.zero)
                    {
                        var position = UnityEngine.Input.touches[0].position;

                        Reparameterize();

                        Pitch();
                        Yaw();
                        Roll();

                        if (Vector2.Distance(leftTouch, position) < Vector2.Distance(rightTouch, position) && !LeftStickEffectsStarted)
                        {
                            LeftStickEffectsStarted = true;
                            ship.PerformLeftStickEffects();
                        }
                        else if (Vector2.Distance(leftTouch, position) < Vector2.Distance(rightTouch, position))
                        {
                            leftTouch = position;
                        }
                        else if (!RightStickEffectsStarted)
                        {
                            RightStickEffectsStarted = true;
                            ship.PerformRightStickEffects();
                        }
                        else
                        {
                            rightTouch = position;
                        }

                        if (drifting)
                            Drift();
                        else Throttle();

                        shipAnimation.PerformShipAnimations(ySum, xSum, yDiff, xDiff);
                    }
                }
                else
                {
                    speed = Mathf.Lerp(speed, defaultThrottle, smallLerpAmount * Time.deltaTime);
                    shipAnimation.Idle();
                }
            }
        }

        void Reparameterize()
        {
            xSum = ((rightTouch.x + leftTouch.x) / (Screen.currentResolution.width) - 1);
            ySum = ((rightTouch.y + leftTouch.y) / (Screen.currentResolution.height) - 1);
            xDiff = (rightTouch.x - leftTouch.x) / (Screen.currentResolution.width);
            yDiff = (rightTouch.y - leftTouch.y) / (Screen.currentResolution.width);

            if (invertYEnabled)
                ySum *= -1;
        }

        void Drift()
        {
            speed = Mathf.Lerp(speed, xDiff * throttleScaler + defaultThrottle, lerpAmount * Time.deltaTime);
            shipTransform.position += speed * Time.deltaTime * shipData.velocityDirection;
            boostDecay += .03f;
        }

        public void EndDrift()
        {
            drifting = false;
            StartCoroutine(DecayingBoostCoroutine());
        }

        IEnumerator DecayingBoostCoroutine()
        {
            while (boostDecay > 1)
            {
                boostDecay = Mathf.Clamp(boostDecay - Time.deltaTime, 1, 10);
                speed = Mathf.Lerp(speed, xDiff * throttleScaler * boostDecay + defaultThrottle, lerpAmount * Time.deltaTime);
                yield return null;
            }
        }

        public void BoostShip(float boost, float fuelAmount)
        {
            speed = Mathf.Lerp(speed, xDiff * throttleScaler * boost + defaultThrottle, lerpAmount * Time.deltaTime);
            OnBoost?.Invoke(uuid, fuelAmount);
        }

        void Yaw()  // These need to not use *= ... remember quaternions are not commutative
        {
            displacementQ = Quaternion.AngleAxis(
                                xSum * (speed * rotationThrottleScaler + rotationScaler) *
                                    (Screen.currentResolution.width/Screen.currentResolution.height) * Time.deltaTime, 
                                shipTransform.up) * displacementQ;
        }

        void Roll()
        {
            displacementQ = Quaternion.AngleAxis(
                                yDiff * (speed * rotationThrottleScaler + rotationScaler) * Time.deltaTime,
                                shipTransform.forward) * displacementQ;
        }

        void Pitch()
        {
            displacementQ = Quaternion.AngleAxis(
                                ySum * -(speed * rotationThrottleScaler + rotationScaler) * Time.deltaTime,
                                shipTransform.right) * displacementQ;
        }

        void CheckThrottle()
        {
            float threshold = .3f;
            float value = (1 - xDiff) + Mathf.Abs(yDiff) + Mathf.Abs(ySum) + Mathf.Abs(xSum);

            if (value < threshold)
            {
                ship.PerformFullSpeedStraightEffects();
                FullSpeedStraightEffectsStopped = false;
            }
            else
            {
                if (!FullSpeedStraightEffectsStopped)
                {
                    FullSpeedStraightEffectsStopped = true;
                    ship.StopFullSpeedStraightEffects();
                }
                Throttle();
            }
        }

        void Throttle()
        {
            speed = Mathf.Lerp(speed, xDiff * throttleScaler + defaultThrottle, lerpAmount * Time.deltaTime);
        }

        // Converts Android Quaterions into Unity Quaterions
        Quaternion GyroToUnity(Quaternion q)
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
            if (SystemInfo.supportsGyroscope && status) { 
                inverseInitialRotation = Quaternion.Inverse(GyroToUnity(gyro.attitude) * derivedCorrection);
            }
            
            isGyroEnabled = status;
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
    }
}