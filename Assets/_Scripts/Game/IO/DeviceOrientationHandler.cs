using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using CosmicShore.Core;

namespace CosmicShore.Game.IO
{
    public class DeviceOrientationHandler
    {
        private const float PHONE_FLIP_THRESHOLD = 0.1f;
        private const float GYRO_INITIALIZATION_RANGE = 0.05f;
        private bool phoneFlipState;
        private IShip ship;
        private bool attitudeInitialized = false;

        private Quaternion derivedCorrection;
        private Quaternion inverseInitialRotation = Quaternion.identity;

        private MonoBehaviour coroutineRunner;

        public void Initialize(IShip ship, MonoBehaviour coroutineRunner)
        {
            this.ship = ship;
            this.coroutineRunner = coroutineRunner;
            // Don't enable the sensor by default - wait for explicit enable via OnToggleGyro

            Screen.orientation = ScreenOrientation.LandscapeLeft;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            
        }

        public void Update()
        {
            HandlePhoneOrientation();
        }

        private void HandlePhoneOrientation()
        {
            if (Accelerometer.current != null)
            {
                var acceleration = Accelerometer.current.acceleration.ReadValue().x;
                if (Mathf.Abs(acceleration) >= PHONE_FLIP_THRESHOLD)
                {
                    UpdatePhoneFlipState(acceleration);
                }
            }
        }

        private void UpdatePhoneFlipState(float accelerationX)
        {
            if (ship == null) return;
            bool newFlipState = accelerationX > 0;
            if (newFlipState != phoneFlipState)
            {
                phoneFlipState = newFlipState;
                if (phoneFlipState)
                {
                    ship.PerformShipControllerActions(InputEvents.FlipAction);
                    //InputController.currentOrientation = ScreenOrientation.LandscapeRight;
                }
                else
                {
                    ship.StopShipControllerActions(InputEvents.FlipAction);
                    //InputController.currentOrientation = ScreenOrientation.LandscapeLeft;
                }
                Debug.Log($"Phone flip state change detected - new flip state: {phoneFlipState}, acceleration.x: {accelerationX}");
            }
        }

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
            else Debug.Log("Attitude Sensor not available on this device");
        }

        private IEnumerator AttitudeInitializationCoroutine()
        {
            derivedCorrection = GyroToUnity(Quaternion.Inverse(new Quaternion(0, .65f, .75f, 0)));
            inverseInitialRotation = Quaternion.identity;

            while (AttitudeSensor.current == null || !AttitudeSensor.current.enabled)
            {
                yield return new WaitForSeconds(0.1f);
                Debug.Log("Waiting for attitude sensor to be ready...");
            }

            // Wait for the sensor to start providing real values
            yield return new WaitForSeconds(0.5f);

            var lastAttitude = GetAttitude();
            yield return new WaitForSeconds(0.1f);

            while (!(1 - Mathf.Abs(Quaternion.Dot(lastAttitude, GetAttitude())) < GYRO_INITIALIZATION_RANGE))
            {
                lastAttitude = GetAttitude();
                yield return new WaitForSeconds(0.1f);
                Debug.Log($"Waiting for attitude sensor to stabilize...{lastAttitude}");
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
