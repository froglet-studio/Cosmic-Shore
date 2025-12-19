using CosmicShore.Game.CameraSystem;
using UnityEngine;

namespace CosmicShore.Game
{
    [DefaultExecutionOrder(-900)]
    public sealed class CameraZoomFollowScaleProvider : MonoBehaviour
    {
        public enum Mode { Fixed, FollowScale }

        [SerializeField] private Mode mode = Mode.FollowScale;

        [Header("Provider")]
        [SerializeField] private MonoBehaviour scaleProviderBehaviour;
        IScaleProvider _provider;

        [Header("Camera Limits")]
        [SerializeField] private float farClipPadding = 1.3f;
        [SerializeField] private float maxDistanceAbs = 10000f;

        ICameraController _controller;
        float _baseScale;
        float _baseDistance;
        bool  _hadAdaptiveZoom;

        void Awake()
        {
            _provider = scaleProviderBehaviour as IScaleProvider;
        }

        void OnEnable()
        {
            if (mode != Mode.FollowScale) return;

            _controller = CameraManager.Instance?.GetActiveController();
            if (_controller == null || _provider == null) return;

            _baseScale    = Mathf.Max(_provider.MinScale, 0.0001f);
            _baseDistance = _controller.GetCameraDistance();

            if (_controller is CustomCameraController cc)
            {
                _hadAdaptiveZoom = cc.adaptiveZoomEnabled;
                cc.adaptiveZoomEnabled = false;
            }
        }

        void OnDisable()
        {
            RestoreAdaptiveZoom();
        }

        void LateUpdate()
        {
            if (mode != Mode.FollowScale) return;
            if (_controller == null || _provider == null) return;

            float currentScale = Mathf.Max(_provider.CurrentScale, 0.0001f);
            float ratio = currentScale / Mathf.Max(_baseScale, 0.0001f);

            float target = _baseDistance * ratio;

            // keep distance negative if your camera uses negative Z distances
            if (!Mathf.Approximately(Mathf.Sign(target), Mathf.Sign(_baseDistance)))
                target = -Mathf.Abs(target);

            target = Mathf.Clamp(target, -maxDistanceAbs, -0.01f);

            _controller.SetCameraDistance(target);

            if (_controller is CustomCameraController concrete)
            {
                var cam = concrete.Camera;
                float need = Mathf.Abs(target) * 1.05f;
                if (need > cam.farClipPlane * 0.95f)
                    cam.farClipPlane = need * farClipPadding;
            }
        }

        void RestoreAdaptiveZoom()
        {
            if (_controller is CustomCameraController cc)
                cc.adaptiveZoomEnabled = _hadAdaptiveZoom;
            _hadAdaptiveZoom = false;
        }
    }
}