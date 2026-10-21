using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    public class DeviceOrientationHandler
    {
        private const float GYRO_INITIALIZATION_RANGE = 0.05f;
        // private IVessel vessel;
        IInputStatus inputStatus;
        private bool attitudeInitialized = false;

        private Quaternion derivedCorrection;
        private Quaternion inverseInitialRotation = Quaternion.identity;

        private MonoBehaviour coroutineRunner;

        // public void Initialize(IVessel vessel, MonoBehaviour coroutineRunner)
        public void Initialize(IInputStatus status, MonoBehaviour runner)
        {
            // this.vessel = vessel;
            inputStatus = status;
            coroutineRunner = runner;
            // Don't enable the sensor by default - wait for explicit enable via OnToggleGyro

            Screen.orientation = ScreenOrientation.LandscapeLeft;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            
        }

        /// <summary>
        /// Turning the phone over no longer presses <see cref="InputEvents.FlipAction"/> (2026-10-21,
        /// Docs/Reports/MOBILE_VESSELS.md D12). It never could: the accelerometer it reads starts
        /// disabled and nothing enabled it, and the screen is locked to LandscapeLeft above, so the
        /// "flip" meant playing upside down. Tilt flight (<see cref="TouchTilt"/>) now enables the
        /// accelerometer, which would have woken this: a 0.1 g threshold with no hold time toggles a
        /// mode from an ordinary wobble. On a phone, FlipAction (RB) is its card.
        /// </summary>
        public void Update() { }

        private void StartAttitudeSensor()
        {
            if (AttitudeSensor.current != null)
            {
                // Make sure the sensor is enabled before starting initialization
                if (!AttitudeSensor.current.enabled)
                {
                    InputSystem.EnableDevice(AttitudeSensor.current);
                }
                coroutineRunner.StartCoroutine(AttitudeInitializationCoroutine());
            }
            else CSDebug.LogVerbose(CSLogChannel.Input, "[DeviceOrientationHandler] Attitude sensor not available on this device");
        }

        private IEnumerator AttitudeInitializationCoroutine()
        {
            derivedCorrection = GyroToUnity(Quaternion.Inverse(new Quaternion(0, .65f, .75f, 0)));
            inverseInitialRotation = Quaternion.identity;

            while (AttitudeSensor.current == null || !AttitudeSensor.current.enabled)
            {
                yield return new WaitForSeconds(0.1f);
            }

            // Wait for the sensor to start providing real values
            yield return new WaitForSeconds(0.5f);

            var lastAttitude = GetAttitude();
            yield return new WaitForSeconds(0.1f);

            while (!(1 - Mathf.Abs(Quaternion.Dot(lastAttitude, GetAttitude())) < GYRO_INITIALIZATION_RANGE))
            {
                lastAttitude = GetAttitude();
                yield return new WaitForSeconds(0.1f);
            }

            inverseInitialRotation = Quaternion.Inverse(GyroToUnity(GetAttitude()) * derivedCorrection);
            attitudeInitialized = true;
        }

        public Quaternion GetAttitudeRotation()
        {
            InputSystem.EnableDevice(AttitudeSensor.current); //TODO: understand why this is needed.
            if (!attitudeInitialized || AttitudeSensor.current == null || !AttitudeSensor.current.enabled)
                return Quaternion.identity;

            var attitude = GetAttitude();
            return inverseInitialRotation * GyroToUnity(attitude) * derivedCorrection;
        }

        private Quaternion GetAttitude()
        {
            InputSystem.EnableDevice(AttitudeSensor.current);
            if (AttitudeSensor.current == null || !AttitudeSensor.current.enabled)
                return Quaternion.identity;

            return AttitudeSensor.current.attitude.ReadValue();
        }

        public void OnToggleGyro(bool status)
        {
            if (AttitudeSensor.current != null)
            {
                if (status)
                {
                    // Enable the sensor and start initialization if needed
                    InputSystem.EnableDevice(AttitudeSensor.current);
                    if (!attitudeInitialized)
                    {
                        StartAttitudeSensor();
                    }
                }
                else
                {
                    // Disable the sensor and reset initialization
                    InputSystem.DisableDevice(AttitudeSensor.current);
                    attitudeInitialized = false;
                }
            }
        }

        private static Quaternion GyroToUnity(Quaternion q)
        {
            return new Quaternion(q.x, -q.y, q.z, q.w);//Quaternion(q.x, -q.z, q.y, q.w);
        }
    }
}
