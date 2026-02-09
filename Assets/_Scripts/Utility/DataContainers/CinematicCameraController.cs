using UnityEngine;

namespace CosmicShore.Game.Cinematics
{
    /// <summary>
    /// Handles cinematic camera behaviors during end-game sequences.
    /// Actually moves the camera for different cinematic shots.
    /// </summary>
    public class CinematicCameraController : MonoBehaviour
    {
        private Camera mainCamera;
        private Transform cameraTransform;
        private Transform vesselTransform;
        private CinematicCameraSetup currentSetup;
        private bool isActive;
        private float setupStartTime;
        
        // Static camera state
        private Vector3 fixedCameraPosition;
        private Quaternion fixedCameraRotation;
        private bool isStaticShot;
        
        // Circle camera state
        private float circleAngle;
        private Vector3 circleCenter;
        
        // Zoom state
        private float initialDistance;
        
        // Follow state
        private Vector3 followVelocity;

        public void Initialize(Camera camera, Transform vessel)
        {
            mainCamera = camera;
            cameraTransform = camera.transform;
            vesselTransform = vessel;
            
            Debug.Log($"CinematicCameraController initialized - Camera: {camera.name}, Vessel: {vessel.name}");
        }

        /// <summary>
        /// Start a specific camera setup
        /// </summary>
        public void StartCameraSetup(CinematicCameraSetup setup)
        {
            currentSetup = setup;
            isActive = true;
            setupStartTime = Time.time;
            isStaticShot = false;

            Debug.Log($"Starting camera setup: {setup.cameraType}");

            switch (setup.cameraType)
            {
                case CinematicCameraType.StaticFrontView:
                    InitializeStaticFrontView();
                    break;
                case CinematicCameraType.StaticSideView:
                    InitializeStaticSideView();
                    break;
                case CinematicCameraType.FollowBehind:
                    InitializeFollowBehind();
                    break;
                case CinematicCameraType.CircleAround:
                    InitializeCircleAround();
                    break;
                case CinematicCameraType.ZoomOut:
                    InitializeZoomOut();
                    break;
                case CinematicCameraType.Custom:
                    InitializeCustom();
                    break;
            }
        }

        /// <summary>
        /// Stop current camera setup
        /// </summary>
        public void StopCameraSetup()
        {
            isActive = false;
            isStaticShot = false;
            currentSetup = null;
            Debug.Log("Camera setup stopped");
        }

        private void LateUpdate()
        {
            if (!isActive || currentSetup == null || !mainCamera || !vesselTransform)
                return;

            switch (currentSetup.cameraType)
            {
                case CinematicCameraType.StaticFrontView:
                    UpdateStaticFrontView();
                    break;
                case CinematicCameraType.StaticSideView:
                    UpdateStaticSideView();
                    break;
                case CinematicCameraType.FollowBehind:
                    UpdateFollowBehind();
                    break;
                case CinematicCameraType.CircleAround:
                    UpdateCircleAround();
                    break;
                case CinematicCameraType.ZoomOut:
                    UpdateZoomOut();
                    break;
                case CinematicCameraType.Custom:
                    UpdateCustom();
                    break;
            }
        }

        #region Static Front View
        private void InitializeStaticFrontView()
        {
            // Position camera in front of vessel
            fixedCameraPosition = vesselTransform.position + vesselTransform.forward * currentSetup.distance;
            fixedCameraPosition.y += currentSetup.heightOffset;
            
            // Look at vessel
            Vector3 lookDirection = vesselTransform.position - fixedCameraPosition;
            fixedCameraRotation = Quaternion.LookRotation(lookDirection);
            
            // Immediately set camera position
            cameraTransform.position = fixedCameraPosition;
            cameraTransform.rotation = fixedCameraRotation;
            
            isStaticShot = true;
            
            Debug.Log($"Static Front View initialized at {fixedCameraPosition}, looking at {vesselTransform.position}");
        }

        private void UpdateStaticFrontView()
        {
            // Camera stays at fixed position
            cameraTransform.position = fixedCameraPosition;
            
            // Always look at vessel (vessel is moving)
            Vector3 lookDirection = vesselTransform.position - fixedCameraPosition;
            if (lookDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation, targetRotation, Time.deltaTime * 2f);
            }
        }
        #endregion

        #region Static Side View
        private void InitializeStaticSideView()
        {
            // Position camera to the side of vessel
            Vector3 rightDirection = vesselTransform.right;
            fixedCameraPosition = vesselTransform.position + rightDirection * currentSetup.distance;
            fixedCameraPosition.y += currentSetup.heightOffset;
            
            // Look at vessel
            Vector3 lookDirection = vesselTransform.position - fixedCameraPosition;
            fixedCameraRotation = Quaternion.LookRotation(lookDirection);
            
            // Immediately set camera
            cameraTransform.position = fixedCameraPosition;
            cameraTransform.rotation = fixedCameraRotation;
            
            isStaticShot = true;
            
            Debug.Log($"Static Side View initialized at {fixedCameraPosition}");
        }

        private void UpdateStaticSideView()
        {
            // Camera stays fixed
            cameraTransform.position = fixedCameraPosition;
            
            // Always look at vessel
            Vector3 lookDirection = vesselTransform.position - fixedCameraPosition;
            if (lookDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation, targetRotation, Time.deltaTime * 2f);
            }
        }
        #endregion

        #region Follow Behind
        private void InitializeFollowBehind()
        {
            followVelocity = Vector3.zero;
            Debug.Log("Follow Behind initialized");
        }

        private void UpdateFollowBehind()
        {
            // Camera follows behind vessel at configured distance
            Vector3 targetPosition = vesselTransform.position - vesselTransform.forward * currentSetup.distance;
            targetPosition.y += currentSetup.heightOffset;
            
            // Smooth follow
            cameraTransform.position = Vector3.SmoothDamp(
                cameraTransform.position,
                targetPosition,
                ref followVelocity,
                0.3f
            );
            
            // Look at vessel
            Vector3 lookDirection = vesselTransform.position - cameraTransform.position;
            if (lookDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation, targetRotation, Time.deltaTime * 3f);
            }
        }
        #endregion

        #region Circle Around
        private void InitializeCircleAround()
        {
            circleAngle = 0f;
            circleCenter = vesselTransform.position;
            Debug.Log($"Circle Around initialized - Center: {circleCenter}, Distance: {currentSetup.distance}");
        }

        private void UpdateCircleAround()
        {
            // Update circle center to follow vessel
            circleCenter = Vector3.Lerp(circleCenter, vesselTransform.position, Time.deltaTime * 0.5f);
            
            // Increment angle
            circleAngle += currentSetup.rotationSpeed * Time.deltaTime;
            
            // Calculate position on circle
            float radians = circleAngle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(
                Mathf.Sin(radians) * currentSetup.distance,
                currentSetup.heightOffset,
                Mathf.Cos(radians) * currentSetup.distance
            );
            
            Vector3 targetPosition = circleCenter + offset;
            cameraTransform.position = targetPosition;
            
            // Look at vessel
            Vector3 lookDirection = vesselTransform.position - cameraTransform.position;
            if (lookDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                cameraTransform.rotation = targetRotation;
            }
        }
        #endregion

        #region Zoom Out
        private void InitializeZoomOut()
        {
            // Start from current distance
            initialDistance = currentSetup.distance;
            Debug.Log($"Zoom Out initialized - From: {initialDistance}, To: {currentSetup.zoomTargetDistance}");
        }

        private void UpdateZoomOut()
        {
            // Calculate progress
            float elapsedTime = Time.time - setupStartTime;
            float progress = Mathf.Clamp01(elapsedTime / currentSetup.duration);
            
            // Interpolate distance
            float currentDistance = Mathf.Lerp(
                initialDistance,
                currentSetup.zoomTargetDistance,
                progress
            );
            
            // Position camera behind vessel at interpolated distance
            Vector3 targetPosition = vesselTransform.position - vesselTransform.forward * currentDistance;
            targetPosition.y += currentSetup.heightOffset;
            
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, targetPosition, Time.deltaTime * 2f);
            
            // Look at vessel
            Vector3 lookDirection = vesselTransform.position - cameraTransform.position;
            if (lookDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation, targetRotation, Time.deltaTime * 3f);
            }
        }
        #endregion

        #region Custom
        private void InitializeCustom()
        {
            Debug.Log("Custom camera behavior - using default follow");
            InitializeFollowBehind();
        }

        private void UpdateCustom()
        {
            // Default to follow behavior for custom
            UpdateFollowBehind();
        }
        #endregion

        #region Public Helper Methods
        
        /// <summary>
        /// Get camera ready for cinematic mode
        /// </summary>
        public void PrepareCinematicMode()
        {
            Debug.Log("Camera prepared for cinematic mode");
        }
        
        /// <summary>
        /// Restore camera to normal gameplay mode
        /// </summary>
        public void RestoreNormalMode()
        {
            isActive = false;
            isStaticShot = false;
            Debug.Log("Camera restored to normal mode");
        }
        
        #endregion
    }
}