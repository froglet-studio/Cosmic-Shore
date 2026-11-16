using System;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Voyages' chase camera (Rev 114 F1): while a voyage is out, the flight camera takes the framing
    /// <see cref="VoyageCameraSO"/> holds for the pilot's vessel class (close behind, low, a little to one side), and gives
    /// it back the moment the voyage ends or this run goes. It writes only <see cref="CustomCameraController.Mode"/>, the
    /// camera's one mode-owned framing, on the existing follow camera: there is no second rig, the vessel's own
    /// <see cref="CameraSettingsSO"/> is never edited, and nothing the vessel does changes.
    ///
    /// <para>Re-read each frame (a few comparisons), so a vessel swap mid-voyage takes its own row. The intro's hand-over
    /// swings into this same pose (<c>VoyageIntro</c> snaps the follow camera and blends to it).</para>
    /// </summary>
    public sealed class VoyageChaseCamera : MonoBehaviour
    {
        Func<bool> _out;
        Func<IVesselStatus> _local;
        CustomCameraController _framed;
        CustomCameraController.ModeFraming _written;
        bool _holding;

        /// <summary>Frame the pilot's camera while <paramref name="isOut"/> holds; <paramref name="local"/> is the pilot's vessel.</summary>
        public static VoyageChaseCamera Attach(GameObject host, Func<bool> isOut, Func<IVesselStatus> local)
        {
            if (!host) return null;
            var chase = host.GetComponent<VoyageChaseCamera>();
            if (!chase) chase = host.AddComponent<VoyageChaseCamera>();
            chase._out = isOut;
            chase._local = local;
            return chase;
        }

        /// <summary>The follow camera voyages frame (the pilot's close camera), or null.</summary>
        public static CustomCameraController FollowCamera()
        {
            var manager = CameraManager.Instance;
            var t = manager ? manager.GetCloseCamera() : null;
            return t && t.TryGetComponent(out CustomCameraController cam) ? cam : null;
        }

        /// <summary>Put the chase framing on now (the intro calls this before it swings in, so both agree).</summary>
        public void ApplyNow() => Refresh();

        void LateUpdate() => Refresh();

        void Refresh()
        {
            bool isOut = _out != null && _out();
            var local = isOut ? _local?.Invoke() : null;
            var settings = local != null ? VoyageCameraSO.Load() : null;
            if (!settings)
            {
                Release();
                return;
            }

            var cam = FollowCamera();
            if (cam != _framed) Release();
            if (!cam) return;
            var framing = settings.For(local.VesselType);
            if (_holding && cam.Mode.Equals(framing)) return;
            cam.Mode = framing;
            _framed = cam;
            _written = framing;
            _holding = true;
        }

        /// <summary>Give the camera back its own framing, unless something else has framed it since.</summary>
        void Release()
        {
            if (_holding && _framed && _framed.Mode.Equals(_written))
                _framed.Mode = CustomCameraController.ModeFraming.None;
            _framed = null;
            _holding = false;
        }

        void OnDisable() => Release();
    }
}
