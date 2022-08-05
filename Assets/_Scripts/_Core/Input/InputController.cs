using System.Collections;
using UnityEngine;

namespace StarWriter.Core.Input
{
    public class InputController : MonoBehaviour
    {
        #region Ship
        [SerializeField] 
        Transform shipTransform;

        [SerializeField]
        Transform Fusilage;

        [SerializeField]
        Transform LeftWing;

        [SerializeField]
        Transform RightWing;
        #endregion

        public float speed;

        private readonly float defaultThrottle = 10f;
        private readonly float rotationThrottleScaler = 3;
        private readonly float throttleScaler = 50;
        private readonly float rotationScaler = 130f;

        private readonly float lerpAmount = 1f;
        private readonly float smallLerpAmount = .1f;

        private readonly float animationScaler = 25f;
        private readonly float yawAnimationScaler = 80f;

        private Gyroscope gyro;
        private Quaternion empiricalCorrection;
        private Quaternion displacementQ;
        private Quaternion inverseInitialRotation=new(0,0,0,0);

        private bool isPitchEnabled = true;
        private bool isYawEnabled = true;
        private bool isRollEnabled = true;
        private bool isThrottleEnabled = true;
        private bool isGyroEnabled = true;
        private bool invertYEnabled = false;
        public bool IsPitchEnabled { get => isPitchEnabled; set => isPitchEnabled = value; }
        public bool IsYawEnabled { get => isYawEnabled; set => isYawEnabled = value; }
        public bool IsRollEnabled { get => isRollEnabled; set => isRollEnabled = value; }
        public bool IsThrottleEnabled { get => isThrottleEnabled; set => isThrottleEnabled = value; }
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
            if (true) //TODO replace this with SystemInfo.supportsGyroscope and test on tablet
            {
                gyro = UnityEngine.Input.gyro;
                gyro.enabled = true;

                StartCoroutine(GyroInitializationCoroutine());

                displacementQ = shipTransform.rotation;

                Screen.sleepTimeout = SleepTimeout.NeverSleep;
            }
        }

        float gyroInitializationAcceptableRange = .05f;

        IEnumerator GyroInitializationCoroutine()
        {
            empiricalCorrection = GyroToUnity(Quaternion.Inverse(new Quaternion(0, .65f, .75f, 0)));  // TODO: move to derivedCoorection
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

            inverseInitialRotation = Quaternion.Inverse(GyroToUnity(gyro.attitude) * empiricalCorrection);
        }

        void Update()
        {
            if (PauseSystem.GetIsPaused()) return;

            // Convert two finger touch into values for displacement, speed, and ship animations
            ReceiveTouchInput();

            RotateShip();

            // Move ship forward
            shipTransform.position += speed * Time.deltaTime * shipTransform.forward; 
        }

        private void PerformShipAnimations(float Xsum, float Ysum, float Xdiff, float Ydiff)
        {
            // Ship animations TODO: figure out how to leverage a single definition for pitch, etc. that captures the gyro in the animations.
            LeftWing.localRotation = Quaternion.Lerp(
                                        LeftWing.localRotation, 
                                        Quaternion.Euler(
                                            (Ydiff - Ysum) * animationScaler, //tilt based on pitch and roll
                                            0,
                                            -(Xdiff + Xsum) * yawAnimationScaler), //sweep back based on throttle and yaw
                                        lerpAmount);

            RightWing.localRotation = Quaternion.Lerp(
                                        RightWing.localRotation, 
                                        Quaternion.Euler(
                                            -(Ysum + Ydiff) * animationScaler, 
                                            0,
                                            (Xdiff - Xsum) * yawAnimationScaler), 
                                        lerpAmount);

            Fusilage.localRotation = Quaternion.Lerp(
                                        Fusilage.localRotation, 
                                        Quaternion.Euler(
                                            -Ysum * animationScaler,
                                            Ydiff* animationScaler,
                                            -Xsum * animationScaler),
                                        lerpAmount);
        }

        private void RotateShip()
        {
            if (true //TODO replace this with SystemInfo.supportsGyroscope and test on tablet
                && isGyroEnabled 
                && !Equals(inverseInitialRotation, new Quaternion(0, 0, 0, 0)))
            {
                // Updates GameObjects rotation from input device's gyroscope
                shipTransform.rotation = Quaternion.Lerp(
                                            shipTransform.rotation,
                                            displacementQ * inverseInitialRotation * GyroToUnity(gyro.attitude) * empiricalCorrection,
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

        private void ReceiveTouchInput()
        {
            if (UnityEngine.Input.touches.Length == 2)
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

                // reparameterize
                float xSum = ((rightTouch.x + leftTouch.x) / (Screen.currentResolution.width) - 1);
                float ySum = ((rightTouch.y + leftTouch.y) / (Screen.currentResolution.height) - 1);
                float xDiff = (rightTouch.x - leftTouch.x) / (Screen.currentResolution.width);
                float yDiff = (rightTouch.y - leftTouch.y) / (Screen.currentResolution.width);

                if (invertYEnabled)
                    ySum *= -1;

                //if (isPitchEnabled) { Pitch(ySum); }    // this block was causing a bug where the ship movement is disabled untill the gyro is toggled
                //if (isRollEnabled) { Roll(yDiff); }
                //if (isYawEnabled) { Yaw(xSum); }
                //if (isThrottleEnabled) { Throttle(xDiff); }

                Pitch(ySum);  //replaces the commented out block above
                Roll(yDiff);
                Yaw(xSum);
                Throttle(xDiff);

                PerformShipAnimations(xSum, ySum, xDiff, yDiff);

            }
            else if (UnityEngine.Input.touches.Length == 1)
            {
                if (leftTouch != Vector2.zero && rightTouch != Vector2.zero)
                {
                    // reparameterize
                    float xSum = ((rightTouch.x + leftTouch.x) / (Screen.currentResolution.width) - 1);
                    float ySum = ((rightTouch.y + leftTouch.y) / (Screen.currentResolution.height) - 1);
                    float xDiff = (rightTouch.x - leftTouch.x) / (Screen.currentResolution.width);
                    float yDiff = (rightTouch.y - leftTouch.y) / (Screen.currentResolution.width);

                    if (invertYEnabled)
                        ySum *= -1;

                    //if (isPitchEnabled) { Pitch(ySum); }    // this block was causing a bug where the ship movement is disabled untill the gyro is toggled
                    //if (isRollEnabled) { Roll(yDiff); }
                    //if (isYawEnabled) { Yaw(xSum); }
                    //if (isThrottleEnabled) { Throttle(xDiff); }

                    Pitch(ySum);  //replaces the commented out block above
                    Roll(yDiff);
                    Yaw(xSum);
                    Throttle(xDiff);

                    PerformShipAnimations(xSum, ySum, xDiff, yDiff);
                }
            }
            else
            {
                speed = Mathf.Lerp(speed, defaultThrottle, smallLerpAmount * Time.deltaTime);
                LeftWing.localRotation = Quaternion.Lerp(LeftWing.localRotation, Quaternion.identity, smallLerpAmount * Time.deltaTime);
                RightWing.localRotation = Quaternion.Lerp(RightWing.localRotation, Quaternion.identity, smallLerpAmount * Time.deltaTime);
                Fusilage.localRotation = Quaternion.Lerp(Fusilage.localRotation, Quaternion.identity, smallLerpAmount * Time.deltaTime);
            }
        }

        private void Throttle(float Xdiff)
        {
            speed = Mathf.Lerp(speed, Xdiff * throttleScaler + defaultThrottle, lerpAmount * Time.deltaTime);
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
                inverseInitialRotation = Quaternion.Inverse(GyroToUnity(gyro.attitude) * empiricalCorrection);
            }

            isGyroEnabled = status;
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