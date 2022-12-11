using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace StarWriter.Core.Input
{
    public class InputController : MonoBehaviour
    {
        #region Ship
        [SerializeField] public Ship ship;
        [SerializeField] public Transform shipTransform; // TODO: make this pull from ship

        public ShipAnimation shipAnimation;

        #endregion

        public delegate void Boost(string uuid, float amount);
        public static event Boost OnBoost;
        string uuid;

        float phoneFlipThreshold = .1f;

        [SerializeField] bool driftEnabled = false;
        bool drifting = false;
        float boostDecay = 0;

        float speed;
        [SerializeField] public ShipData shipData; // TODO: make this pull from ship

        public float initialDThrottle = 10f; 
        public float initialThrottleScaler = 50;

        public float defaultThrottle;
        public float throttleScaler;

        float xSum;
        float ySum;
        float xDiff;
        float yDiff;

        private readonly float rotationThrottleScaler = 3;
        private readonly float rotationScaler = 130f;

        private readonly float lerpAmount = 2f;
        private readonly float smallLerpAmount = .7f;

        private UnityEngine.Gyroscope gyro;
        private Quaternion derivedCorrection;
        private Quaternion displacementQ;
        private Quaternion inverseInitialRotation=new(0,0,0,0);

        private bool isGyroEnabled = true;
        private bool invertYEnabled = false;
        public bool IsGyroEnabled { get => isGyroEnabled;  } // GameManager controls the gyro status

        private void OnEnable()
        {
            GameSetting.OnChangeGyroEnabledStatus += OnToggleGyro;
            GameSetting.OnChangeInvertYEnabledStatus += OnToggleInvertY;
        }

        private void OnDisable()
        {
            GameSetting.OnChangeGyroEnabledStatus -= OnToggleGyro;
            GameSetting.OnChangeInvertYEnabledStatus -= OnToggleInvertY;
        }

        void Start()
        {
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

        float gyroInitializationAcceptableRange = .05f;

        IEnumerator GyroInitializationCoroutine()
        {
            derivedCorrection = GyroToUnity(Quaternion.Inverse(new Quaternion(0, .65f, .75f, 0)));
            isGyroEnabled = PlayerPrefs.GetInt(GameSetting.PlayerPrefKeys.isGyroEnabled.ToString()) == 1;
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
            shipTransform.position += shipData.speed * Time.deltaTime * shipTransform.forward;
            
            if (!drifting)
            {
                shipData.velocityDirection = shipTransform.forward;
                shipData.blockRotation = shipTransform.rotation;
            }
        }

        

        private void RotateShip()
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

        Vector2 leftTouch, rightTouch;

        private float Ease(float input)
        {
            return input < 0 ? (Mathf.Cos(input) - 1) / 2 : -(Mathf.Cos(input) - 1) / 2;
        }

        private void ReceiveTouchInput()
        {
            Debug.Log($"Gamepad.Current: {Gamepad.current}");
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

                if (UnityEngine.InputSystem.Gamepad.current.rightShoulder.wasPressedThisFrame)
                {
                    ship.PerformFlipEffects();
                }
                if (UnityEngine.InputSystem.Gamepad.current.leftTrigger.isPressed)
                {
                    ship.PerformLeftStickEffectsEffects();
                }
                if (UnityEngine.InputSystem.Gamepad.current.rightTrigger.isPressed)
                {
                    ship.PerformRightStickEffectsEffects();
                }

                Pitch(ySum);
                Yaw(xSum);
                Roll(yDiff);


                if (driftEnabled)
                {
                    if (UnityEngine.InputSystem.Gamepad.current.leftTrigger.isPressed) Drift(xDiff);
                    else
                    {
                        if (boostDecay > 0) ExitDrift(xDiff);
                        else Special(ySum, xSum, yDiff, xDiff);
                    }
                }
                else Special(ySum, xSum, yDiff, xDiff);

                shipAnimation.PerformShipAnimations(ySum, xSum, yDiff, xDiff);
            }
            else
            {
                if (Mathf.Abs(UnityEngine.Input.acceleration.y) >= phoneFlipThreshold)
                {
                    if (UnityEngine.Input.acceleration.y < 0 &&  GameManager.Instance.PhoneFlipState) ship.StartFlipEffects();
                    else if (UnityEngine.Input.acceleration.y > 0 && !GameManager.Instance.PhoneFlipState) ship.StopFlipEffects();
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

                    reparameterize();

                    Pitch(ySum);
                    Yaw(xSum);
                    Roll(yDiff);
                    shipAnimation.PerformShipAnimations(ySum, xSum, yDiff, xDiff);

                    if (driftEnabled && boostDecay > 0)  ExitDrift(xDiff);
                    else Special(ySum, xSum, yDiff, xDiff);
                }

                else if (UnityEngine.Input.touches.Length == 1)
                {
                    if (leftTouch != Vector2.zero && rightTouch != Vector2.zero)
                    {
                        var position = UnityEngine.Input.touches[0].position;

                        reparameterize();

                        Pitch(ySum);
                        Yaw(xSum);
                        Roll(yDiff);
                        shipAnimation.PerformShipAnimations(ySum, xSum, yDiff, xDiff);

                        if (driftEnabled) Drift(xDiff);
                        else Throttle(xDiff);

                        

                        if (Vector2.Distance(leftTouch, position) < Vector2.Distance(rightTouch, position))
                        {
                            leftTouch = position;
                            ship.PerformLeftStickEffectsEffects();
                        }
                        else
                        {
                            rightTouch = position;
                            ship.PerformRightStickEffectsEffects();
                        }
                    }
                }
                else
                {
                    speed = Mathf.Lerp(speed, defaultThrottle, smallLerpAmount * Time.deltaTime);
                    shipAnimation.Idle();
                }
            }
        }

        void reparameterize()
        {
            xSum = ((rightTouch.x + leftTouch.x) / (Screen.currentResolution.width) - 1);
            ySum = ((rightTouch.y + leftTouch.y) / (Screen.currentResolution.height) - 1);
            xDiff = (rightTouch.x - leftTouch.x) / (Screen.currentResolution.width);
            yDiff = (rightTouch.y - leftTouch.y) / (Screen.currentResolution.width);

            if (invertYEnabled)
                ySum *= -1;
        }

        private void Drift(float xDiff)
        {
            if (!drifting)
            {
                shipData.velocityDirection = shipTransform.forward;
                shipData.blockRotation = shipTransform.rotation;
            }
            speed = Mathf.Lerp(speed, xDiff * throttleScaler + defaultThrottle, lerpAmount * Time.deltaTime);
            shipTransform.position -= speed * Time.deltaTime * shipTransform.forward;
            shipTransform.position += speed * Time.deltaTime * shipData.velocityDirection;
            boostDecay = 6f;
            drifting = true;
        }

        private void ExitDrift(float xDiff)
        {
            drifting = false;
            boostDecay -= Time.deltaTime;
            speed = Mathf.Lerp(speed, xDiff * throttleScaler * boostDecay + defaultThrottle, lerpAmount * Time.deltaTime);
            shipData.velocityDirection = shipTransform.forward;
            shipData.blockRotation = shipTransform.rotation;
        }

        private void Yaw(float Xsum)  // These need to not use *= ... remember quaternions are not commutative
        {
            displacementQ = Quaternion.AngleAxis(
                                Xsum * (speed * rotationThrottleScaler + rotationScaler) *
                                    (Screen.currentResolution.width/Screen.currentResolution.height) * Time.deltaTime, 
                                shipTransform.up) * displacementQ;
        }

        private void Roll(float Ydiff)
        {
            displacementQ = Quaternion.AngleAxis(
                                Ydiff * (speed * rotationThrottleScaler + rotationScaler) * Time.deltaTime,
                                shipTransform.forward) * displacementQ;
        }

        private void Pitch(float Ysum)
        {
            displacementQ = Quaternion.AngleAxis(
                                Ysum * -(speed * rotationThrottleScaler + rotationScaler) * Time.deltaTime,
                                shipTransform.right) * displacementQ;
        }

        private void Special(float ySum, float xSum, float yDiff, float xDiff) // TODO decouple "special" and "throttle"
        {
            float fuelAmount = -.01f;
            float threshold = .3f;
            float boost = 4f;
            float value = (1 - xDiff) + Mathf.Abs(yDiff) + Mathf.Abs(ySum) + Mathf.Abs(xSum);

            

            if (value < threshold && FuelSystem.CurrentFuel>0)
            {
                ship.PerformFullSpeedStraightEffects();
                speed = Mathf.Lerp(speed, xDiff * throttleScaler * boost + defaultThrottle, lerpAmount * Time.deltaTime);
                shipData.boost = true;
                OnBoost?.Invoke(uuid, fuelAmount);
            }
            else
            {
                Throttle(xDiff);
                shipData.boost = false;
            }
        }

        private void Throttle(float xDiff)
        {
            speed = Mathf.Lerp(speed, xDiff * throttleScaler + defaultThrottle, lerpAmount * Time.deltaTime);
        }

        // Converts Android Quaterions into Unity Quaterions
        private Quaternion GyroToUnity(Quaternion q)
        {
            return new Quaternion(q.x, -q.z, q.y, q.w);
        }


        /// <summary>
        /// Gets gyros updated current status from GameManager.onToggleGyro Event
        /// </summary>
        /// <param name="status"></param>bool
        private void OnToggleGyro(bool status)
        {
            Debug.Log($"InputController.OnToggleGyro - status: {status}");
            if (SystemInfo.supportsGyroscope && status) { 
                inverseInitialRotation = Quaternion.Inverse(GyroToUnity(gyro.attitude) * derivedCorrection);
            }
            if (driftEnabled) isGyroEnabled = true;
            else isGyroEnabled = status;
        }

        /// <summary>
        /// Sets InvertY Status based off of game settings event
        /// </summary>
        /// <param name="status"></param>bool
        private void OnToggleInvertY(bool status)
        {
            Debug.Log($"InputController.OnToggleInvertY - status: {status}");

            invertYEnabled = status;
        }
    }
}