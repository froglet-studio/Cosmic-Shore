using UnityEngine;
using TMPro;
using Cinemachine;

namespace StarWriter.Core.Input
{
    public class ShipGyroInputs : MonoBehaviour
    {
        /*
        public delegate void OnPitch();
        public static event OnPitch OnPitch;

        public delegate void OnRoll();
        public static event OnRoll OnRollEvent;

        public delegate void OnYaw();
        public static event OnYaw OnYawEvent;

        public delegate void OnGyro();
        public static event OnGyro OnGyroEvent;
        */

        public delegate void OnThrottle();
        public static event OnThrottle OnThrottleEvent;

        #region Camera 
        [SerializeField]
        CinemachineVirtualCameraBase cam1;

        [SerializeField]
        CinemachineVirtualCameraBase cam2;

        readonly int activePriority = 10;
        readonly int inactivePriority = 1;
        #endregion

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

        [SerializeField]
        float Speed = 0;

        [SerializeField]
        float OnThrottleEventThreshold = 1;

        private float throttle;
        private readonly float defaultThrottle = .3f;
        private readonly float lerpAmount = .2f;
        private readonly float touchScaler = .005f;
        private Gyroscope gyro;
        private Quaternion empiricalCorrection;
        private Quaternion displacementQ;

        private void Awake()
        {
            if (SystemInfo.supportsGyroscope)
            {
                gyro = UnityEngine.Input.gyro;
                empiricalCorrection = Quaternion.Inverse(new Quaternion(0, .65f, .75f, 0)); // TODO: move to derivedCoorection
            }
        }

        void Start()
        {
            if (SystemInfo.supportsGyroscope)
            {
                empiricalCorrection = GyroToUnity(empiricalCorrection);
                gyro.enabled = true;
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
                displacementQ = new Quaternion(0, 0, 0, -1);
            }
        }

        void Update()
        {
            // TODO: remove this check once movement is based on time.deltaTime
            if (PauseSystem.GetIsPaused())
            {
                return;
            }

            if (SystemInfo.supportsGyroscope)
            {
                // Updates GameObjects rotation from input device's gyroscope
                shipTransform.rotation = Quaternion.Lerp(
                                            shipTransform.rotation, 
                                            displacementQ * GyroToUnity(gyro.attitude) * empiricalCorrection, 
                                            lerpAmount);
            }

            cam1.Priority = UnityEngine.Input.acceleration.y > 0 ? activePriority : inactivePriority;
            cam2.Priority = UnityEngine.Input.acceleration.y > 0 ? inactivePriority: activePriority;

            if (UnityEngine.Input.touches.Length == 2)
            {
                Vector2 leftTouch, rightTouch;

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

                Pitch(leftTouch.y, rightTouch.y);
                Roll(leftTouch.y, rightTouch.y);
                Yaw(leftTouch.x, rightTouch.x);
                Throttle(leftTouch.x, rightTouch.x);
                PerformShipAnimations(leftTouch.y, rightTouch.y, leftTouch.x, rightTouch.x);
            }
            else
            {
                throttle = Mathf.Lerp(throttle, defaultThrottle, .1f);
                LeftWing.localRotation = Quaternion.Lerp(LeftWing.localRotation, Quaternion.Euler(0, 0, 0), .1f); // TODO: should these all use Quaternion.Identity?
                RightWing.localRotation = Quaternion.Lerp(RightWing.localRotation, Quaternion.Euler(0, 0, 0), .1f);
                Fusilage.localRotation = Quaternion.Lerp(Fusilage.localRotation, Quaternion.Euler(0, 0, 0), .1f);
            }

            // Move ship forward
            shipTransform.position += Speed * throttle * Time.deltaTime * shipTransform.forward;
        }

        private void PerformShipAnimations(float yl, float yr, float xl, float xr)
        {
            // Ship animations
            LeftWing.localRotation = Quaternion.Lerp(
                                        LeftWing.localRotation, 
                                        Quaternion.Euler(
                                            (-(yl + yr) + (Screen.currentResolution.height) + (yr - yl)) * .02f,
                                            0,
                                            -(throttle - defaultThrottle) * 50 - ((xl + xr) - (Screen.currentResolution.width)) * .025f), 
                                        lerpAmount);

            RightWing.localRotation = Quaternion.Lerp(
                                        RightWing.localRotation, 
                                        Quaternion.Euler(
                                            (-(yl + yr) + Screen.currentResolution.height - (yr - yl)) * .02f,
                                            0,
                                            (throttle - defaultThrottle) * 50 - ((xl + xr) - Screen.currentResolution.width) * .025f), 
                                        lerpAmount);

            Fusilage.localRotation = Quaternion.Lerp(
                                        Fusilage.localRotation, 
                                        Quaternion.Euler(
                                            (-(yl + yr) + Screen.currentResolution.height) * .02f,
                                            (yr - yl)*.02f,
                                            (-(xl + xr) + Screen.currentResolution.width) * .01f),
                                        lerpAmount);
        }

        private void Throttle(float xl, float xr)
        {
            throttle = Mathf.Lerp(throttle, (xr - xl) * touchScaler * .18f - .15f, .2f);

            if (throttle > OnThrottleEventThreshold)
                OnThrottleEvent?.Invoke();
        }

        private void Yaw(float xl, float xr)
        {
            displacementQ *= Quaternion.AngleAxis(
                                (((xl + xr) / 2) - (Screen.currentResolution.width / 2)) * touchScaler * (throttle+.5f), 
                                shipTransform.up);
        }

        private void Roll(float yl, float yr)
        {
            displacementQ *= Quaternion.AngleAxis(
                                (yr - yl) * touchScaler * throttle, 
                                shipTransform.forward);
        }

        private void Pitch(float yl, float yr)
        {
            displacementQ *= Quaternion.AngleAxis(
                                (((yl + yr) / 2) - (Screen.currentResolution.height / 2)) * -touchScaler * throttle, 
                                shipTransform.right);
        }

        //Coverts Android and Mobile Device Quaterion into Unity Quaterion  TODO: Test
        private Quaternion GyroToUnity(Quaternion q)
        {
            return new Quaternion(q.x, -q.z, q.y, q.w);
        }
    }
}
