using CosmicShore.Gameplay;
using UnityEngine;
using System.Linq;

namespace CosmicShore.Gameplay
{
    public class ZoomOutAction : ShipAction
    {
        private IScaleProvider _scaleSource;
        private CustomCameraController _controller;

        [Header("Zoom multipliers")]
        [Min(0)] [SerializeField]
        private float zoomOutMultiplier = 6f;
        [Min(0)] [SerializeField] private float zoomInMultiplier  = 1f;

        private enum ZoomDir
        {
            None = 0, Out = 1, In = 2
        }

        private const float EPS = 1e-4f;
        private ZoomDir _zoomDir   = ZoomDir.None;
        private float _prevRatio = 1f;
        private float _vel;
        private const float MaxZoomSpeed = 150f;

        public override void StartAction()
        {
        }

        public override void StopAction()
        {
        }

        public void AddProvider(IScaleProvider provider)
        {
            _scaleSource = provider;
        }

        private void LateUpdate()
        {
            if (!IsInitialized)
                return;

            // The camera belongs to the hull it follows, whoever holds the sticks (CameraManager.ActiveRigFollowing):
            // the zoom runs for the hull on screen and for no other. It used to stop on AutoPilotEnabled, so a hull
            // the AI flew for the player sat nearer than the player's own view, and a remote human's hull (not on
            // autopilot either) moved this machine's camera.
            _controller = CameraManager.ActiveRigFollowing(Vessel.Transform);
            if (_scaleSource == null || !_controller) return;

            var ratio = _scaleSource.CurrentScale / Mathf.Max(_scaleSource.MinScale, 0.0001f);

            if (ratio > _prevRatio + EPS) _zoomDir = ZoomDir.Out;
            else if (ratio < _prevRatio - EPS) _zoomDir = ZoomDir.In;

            var newDir = ratio > _prevRatio + EPS ? ZoomDir.Out
                : ratio < _prevRatio - EPS ? ZoomDir.In
                : _zoomDir;

            if (newDir != _zoomDir) _vel = 0f;
            _zoomDir = newDir;

            var eff = _zoomDir switch
            {
                ZoomDir.Out => ratio * zoomOutMultiplier,
                ZoomDir.In => ratio / Mathf.Max(zoomInMultiplier, 0.0001f),
                _ => ratio
            };

            var neutralZ = _controller.NeutralOffsetZ; // e.g. –45
            var targetZ = Mathf.Min(neutralZ * eff, neutralZ); // never inside neutral

            var newZ = Mathf.SmoothDamp(_controller.GetCameraDistance(), targetZ, ref _vel, _controller.ZoomSmoothTime, MaxZoomSpeed);

            _controller.SetCameraDistance(newZ);

            _prevRatio = ratio;
        }
    }
}
