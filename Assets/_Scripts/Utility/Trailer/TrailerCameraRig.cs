using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility.Trailer
{
    /// <summary>
    /// Spawns and manages multiple trailer cameras that track a vessel from
    /// different cinematic angles. Cameras render to off-screen RenderTextures
    /// so they never interfere with the gameplay camera.
    /// </summary>
    public class TrailerCameraRig : MonoBehaviour
    {
        [SerializeField] private TrailerCameraConfigSO config;

        private Transform _vesselTransform;
        private readonly List<TrailerCameraInstance> _cameras = new();
        private bool _isInitialized;

        public IReadOnlyList<TrailerCameraInstance> Cameras => _cameras;
        public bool IsInitialized => _isInitialized;

        public void Initialize(Transform vessel, TrailerCameraConfigSO overrideConfig = null)
        {
            if (overrideConfig != null)
                config = overrideConfig;

            _vesselTransform = vessel;
            DestroyExistingCameras();

            foreach (var setup in config.cameraSetups)
            {
                if (!setup.enabled) continue;
                CreateCamera(setup);
            }

            _isInitialized = true;
        }

        public void Teardown()
        {
            DestroyExistingCameras();
            _isInitialized = false;
            _vesselTransform = null;
        }

        private void CreateCamera(TrailerCameraSetup setup)
        {
            var cameraGO = new GameObject($"TrailerCam_{setup.label}");
            cameraGO.transform.SetParent(transform);

            var cam = cameraGO.AddComponent<Camera>();
            cam.enabled = false; // We render manually via RenderTexture
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 5000f;
            cam.fieldOfView = 60f;

            // Hide UI layer from trailer cameras
            if (config.hideUILayer)
            {
                int uiLayer = LayerMask.NameToLayer("UI");
                if (uiLayer >= 0)
                    cam.cullingMask &= ~(1 << uiLayer);
            }

            var rt = new RenderTexture(config.captureWidth, config.captureHeight, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = Mathf.Clamp(config.antiAliasing, 1, 8);
            rt.filterMode = FilterMode.Bilinear;
            rt.Create();
            cam.targetTexture = rt;

            var instance = new TrailerCameraInstance
            {
                Camera = cam,
                RenderTexture = rt,
                Setup = setup,
                FollowVelocity = Vector3.zero,
                OrbitAngle = 0f
            };

            // Position the camera at its initial location
            PositionCamera(instance, snap: true);
            _cameras.Add(instance);
        }

        private void LateUpdate()
        {
            if (!_isInitialized || _vesselTransform == null) return;

            foreach (var instance in _cameras)
            {
                PositionCamera(instance, snap: false);
            }
        }

        private void PositionCamera(TrailerCameraInstance instance, bool snap)
        {
            if (_vesselTransform == null) return;

            var setup = instance.Setup;
            var camTransform = instance.Camera.transform;
            Vector3 targetPosition;

            switch (setup.cameraType)
            {
                case TrailerCameraType.ChaseBehind:
                    targetPosition = _vesselTransform.position
                                     - _vesselTransform.forward * setup.distance
                                     + Vector3.up * setup.heightOffset;
                    break;

                case TrailerCameraType.SideTracking:
                    targetPosition = _vesselTransform.position
                                     + _vesselTransform.right * setup.lateralOffset
                                     - _vesselTransform.forward * (setup.distance * 0.3f)
                                     + Vector3.up * setup.heightOffset;
                    break;

                case TrailerCameraType.FrontHeroShot:
                    targetPosition = _vesselTransform.position
                                     + _vesselTransform.forward * setup.distance
                                     + Vector3.up * setup.heightOffset;
                    break;

                case TrailerCameraType.HighOrbit:
                    instance.OrbitAngle += setup.orbitSpeed * Time.deltaTime;
                    float highRad = instance.OrbitAngle * Mathf.Deg2Rad;
                    targetPosition = _vesselTransform.position + new Vector3(
                        Mathf.Sin(highRad) * setup.distance,
                        setup.heightOffset,
                        Mathf.Cos(highRad) * setup.distance
                    );
                    break;

                case TrailerCameraType.LowAngleHero:
                    targetPosition = _vesselTransform.position
                                     - _vesselTransform.forward * setup.distance
                                     + Vector3.up * setup.heightOffset;
                    break;

                case TrailerCameraType.SlowOrbit:
                    instance.OrbitAngle += setup.orbitSpeed * Time.deltaTime;
                    float slowRad = instance.OrbitAngle * Mathf.Deg2Rad;
                    targetPosition = _vesselTransform.position + new Vector3(
                        Mathf.Sin(slowRad) * setup.distance,
                        setup.heightOffset,
                        Mathf.Cos(slowRad) * setup.distance
                    );
                    break;

                default:
                    targetPosition = _vesselTransform.position - Vector3.forward * setup.distance;
                    break;
            }

            if (snap)
            {
                camTransform.position = targetPosition;
            }
            else
            {
                camTransform.position = Vector3.SmoothDamp(
                    camTransform.position,
                    targetPosition,
                    ref instance.FollowVelocity,
                    setup.smoothTime
                );
            }

            // Always look at the vessel
            Vector3 lookDir = _vesselTransform.position - camTransform.position;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                camTransform.rotation = snap
                    ? targetRot
                    : Quaternion.Slerp(camTransform.rotation, targetRot, Time.deltaTime * 5f);
            }
        }

        /// <summary>
        /// Renders all cameras to their RenderTextures this frame.
        /// Called by TrailerClipRecorder during capture.
        /// </summary>
        public void RenderAll()
        {
            foreach (var instance in _cameras)
            {
                instance.Camera.Render();
            }
        }

        /// <summary>
        /// Renders a specific camera by index.
        /// </summary>
        public void RenderCamera(int index)
        {
            if (index >= 0 && index < _cameras.Count)
                _cameras[index].Camera.Render();
        }

        private void DestroyExistingCameras()
        {
            foreach (var instance in _cameras)
            {
                if (instance.RenderTexture != null)
                {
                    instance.RenderTexture.Release();
                    Destroy(instance.RenderTexture);
                }

                if (instance.Camera != null)
                    Destroy(instance.Camera.gameObject);
            }

            _cameras.Clear();
        }

        private void OnDestroy()
        {
            DestroyExistingCameras();
        }
    }

    /// <summary>
    /// Holds runtime state for a single trailer camera.
    /// </summary>
    public class TrailerCameraInstance
    {
        public Camera Camera;
        public RenderTexture RenderTexture;
        public TrailerCameraSetup Setup;
        public Vector3 FollowVelocity;
        public float OrbitAngle;
    }
}
