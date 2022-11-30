using System.Collections;
using UnityEngine;

namespace StarWriter.Core.Input
{
    public class InputFlowController : MonoBehaviour
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

        public delegate void Boost(string uuid, float amount);
        public static event Boost OnBoost;
        string uuid;

        public float speed;
        public float[,] flowField = new float[3, 3] 
        { 
            { 0, -1, 0 },
            { 1,  0, 0 },
            { 0,  0, 0 }
        };

        [SerializeField] FlowFieldData flowFieldData;
        ShipData shipData;


        private readonly float defaultThrottle = 10f;
        private readonly float rotationThrottleScaler = 3;
        private readonly float throttleScaler = 50;
        private readonly float rotationScaler = 130f;

        private readonly float lerpAmount = 2f;
        private readonly float smallLerpAmount = .7f;

        private readonly float animationScaler = 25f;
        private readonly float yawAnimationScaler = 80f;

        private Gyroscope gyro;
        private Quaternion derivedCorrection;
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
            shipData = GetComponent<ShipData>();
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
            //flowVector = new Vector3(flowField[0, 0] * shipTransform.position.x + flowField[0, 1] * shipTransform.position.y + flowField[0, 2] * shipTransform.position.z,
            //                         flowField[1, 0] * shipTransform.position.x + flowField[1, 1] * shipTransform.position.y + flowField[1, 2] * shipTransform.position.z,
            //                         flowField[2, 0] * shipTransform.position.x + flowField[2, 1] * shipTransform.position.y + flowField[2, 2] * shipTransform.position.z);
            //var fieldThickness = 200;


            //flowVector = new Vector3(
            //                         -transform.position.y / 30 * (1 - Mathf.Clamp(Mathf.Abs(transform.position.z / fieldThickness), 0, 1)),
            //                         transform.position.x / 120 * (1 - Mathf.Clamp(Mathf.Abs(transform.position.z / fieldThickness), 0, 1)),
            //                         0);

            Vector3 flowVector = flowFieldData.FlowVector(transform);

            // Move ship velocityDirection and update shipdata
            shipTransform.position += speed * Time.deltaTime * shipTransform.forward + flowVector;
            shipData.speed = speed;
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
                                        lerpAmount * Time.deltaTime);

            RightWing.localRotation = Quaternion.Lerp(
                                        RightWing.localRotation, 
                                        Quaternion.Euler(
                                            -(Ysum + Ydiff) * animationScaler, 
                                            0,
                                            (Xdiff - Xsum) * yawAnimationScaler), 
                                        lerpAmount * Time.deltaTime);

            Fusilage.localRotation = Quaternion.Lerp(
                                        Fusilage.localRotation, 
                                        Quaternion.Euler(
                                            -Ysum * animationScaler,
                                            Ydiff* animationScaler,
                                            -Xsum * animationScaler),
                                        lerpAmount * Time.deltaTime);
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

        private void ReceiveTouchInput()
        {
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

                // reparameterize
                float xSum = ((rightTouch.x + leftTouch.x) / (Screen.currentResolution.width) - 1);
                float ySum = ((rightTouch.y + leftTouch.y) / (Screen.currentResolution.height) - 1);
                float xDiff = (rightTouch.x - leftTouch.x) / (Screen.currentResolution.width);
                float yDiff = (rightTouch.y - leftTouch.y) / (Screen.currentResolution.width);

                if (invertYEnabled)
                    ySum *= -1;

                Pitch(ySum);
                Roll(yDiff);
                Yaw(xSum);
                //Throttle(xDiff);

                PerformShipAnimations(xSum, ySum, xDiff, yDiff);

                Special(xDiff, yDiff, xSum, ySum);
            }
            else if (UnityEngine.Input.touches.Length == 1)
            {
                if (leftTouch != Vector2.zero && rightTouch != Vector2.zero)
                {
                    var position = UnityEngine.Input.touches[0].position;
                    // reparameterize
                    float xSum = ((rightTouch.x + leftTouch.x) / (Screen.currentResolution.width) - 1);
                    float ySum = ((rightTouch.y + leftTouch.y) / (Screen.currentResolution.height) - 1);
                    float xDiff = (rightTouch.x - leftTouch.x) / (Screen.currentResolution.width);
                    float yDiff = (rightTouch.y - leftTouch.y) / (Screen.currentResolution.width);

                    if (invertYEnabled)
                        ySum *= -1;

                    Pitch(ySum);
                    Roll(yDiff);
                    Yaw(xSum);
                    Throttle(xDiff);

                    PerformShipAnimations(xSum, ySum, xDiff, yDiff);

                    if (Vector2.Distance(leftTouch, position) < Vector2.Distance(rightTouch, position))
                    {
                        //var diff = position - leftTouch;
                        //WingTipRotate(diff, true);
                        leftTouch = position;
                        //rightTouch = Vector2.Lerp(rightTouch, new Vector2(rightTouch.x, leftTouch.y), .1f);
                    }
                    else
                    {
                        //var diff = position - rightTouch;
                        //WingTipRotate(diff, false);
                        rightTouch = position;
                        //leftTouch = Vector2.Lerp(leftTouch, new Vector2(leftTouch.x, rightTouch.y), .1f);
                    }


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

        private void Special(float xDiff, float yDiff, float xSum, float ySum)
        {
            float fuelAmount = -.0001f;
            float threshold = .1f;
            float boost = 2.7f;
            float value = (1 - xDiff) + Mathf.Abs(yDiff) + Mathf.Abs(ySum) + Mathf.Abs(xSum);
            if (value < threshold)
            {
                speed = Mathf.Lerp(speed, xDiff * throttleScaler*boost + defaultThrottle, lerpAmount * Time.deltaTime);
                OnBoost?.Invoke(uuid, fuelAmount);
            }
            else
            {
                Throttle(xDiff);
            }
        }

        private void WingTipRotate(Vector2 diff, bool leftWing)
        {
            //var value = (leftWing ? -10 : 10);
            //Vector3 point = transform.position + value*transform.right;
            //Vector3 axis = diff.y*transform.right + diff.x * transform.velocityDirection;
            //Pitch(diff.y / 100);
            //Roll(diff.x / 100);
            //transform.RotateAround(point, axis, diff.magnitude/1f);
            //transform.position = Vector3.Lerp(transform.position, transform.position + diff.y * transform.up * Time.deltaTime*2, diff.y*diff.y/ Screen.currentResolution.width);
            //transform.position = Vector3.Lerp(transform.position, transform.position + diff.x * transform.right * Time.deltaTime*2, diff.y*diff.y / Screen.currentResolution.width);
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