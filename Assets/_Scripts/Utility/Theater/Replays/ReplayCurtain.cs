using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Clears the screen for a replay watched from a menu, and puts it back.
    ///
    /// <para><b>Why not the Theater's own UI hiding.</b> <see cref="TheaterStage"/> hides every root
    /// canvas AND stands the EventSystem down, which is right for the Theater's dev overlay (IMGUI,
    /// no EventSystem) and wrong for a player-facing replay, whose controls are a uGUI canvas that
    /// needs the EventSystem to be pressed. So <see cref="ReplayPlayer"/> turns the stage's UI hiding
    /// off and raises this instead: every root screen-space canvas but the replay's own is hidden,
    /// and the EventSystem stays up.</para>
    ///
    /// <para><b>Competing cameras.</b> The menu's view is a scene camera that is not one of
    /// <c>CameraManager</c>'s rigs. Every enabled base camera drawing to the screen other than the
    /// replay camera is switched off while the curtain is up, so the replay never depends on which
    /// camera happens to draw last. Overlay cameras and cameras that draw into a texture are left
    /// alone; only a camera's <see cref="Camera.enabled"/> flag is touched, so its AudioListener
    /// keeps the menu's sound where it was.</para>
    /// </summary>
    public sealed class ReplayCurtain
    {
        readonly List<Canvas> _canvases = new();
        readonly List<Camera> _cameras = new();

        public bool IsUp { get; private set; }

        public void Raise(Canvas keep, Camera replayCamera)
        {
            if (IsUp) return;
            IsUp = true;

            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                var canvas = canvases[i];
                if (!canvas || !canvas.enabled || !canvas.isRootCanvas) continue;
                if (canvas.renderMode == RenderMode.WorldSpace || canvas == keep) continue;
                canvas.enabled = false;
                _canvases.Add(canvas);
            }

            if (!replayCamera) return;
            var cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                var camera = cameras[i];
                if (!camera || camera == replayCamera || camera.targetTexture) continue;
                if (camera.TryGetComponent(out UniversalAdditionalCameraData data) && data.renderType == CameraRenderType.Overlay)
                    continue;
                camera.enabled = false;
                _cameras.Add(camera);
            }
        }

        public void Lower()
        {
            if (!IsUp) return;
            IsUp = false;

            for (int i = 0; i < _cameras.Count; i++)
                if (_cameras[i]) _cameras[i].enabled = true;
            _cameras.Clear();

            for (int i = 0; i < _canvases.Count; i++)
            {
                var canvas = _canvases[i];
                if (!canvas) continue;
                canvas.enabled = true;

                // A Graphic's rebuilds are dropped while its Canvas is disabled, so anything that
                // tried to redraw under the curtain is re-dirtied (the same step TheaterStage takes).
                var graphics = canvas.GetComponentsInChildren<Graphic>(true);
                for (int g = 0; g < graphics.Length; g++)
                    if (graphics[g]) graphics[g].SetAllDirty();
            }
            _canvases.Clear();
        }
    }
}
