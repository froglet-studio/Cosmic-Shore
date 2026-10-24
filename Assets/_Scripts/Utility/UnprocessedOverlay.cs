using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.Utility
{
    /// <summary>
    /// A post-processing-free layer over a view, for its 2D marks: icons, dots, rings, cords, petals (Garrett,
    /// 2026-10-22: "treat them like UI and not post process them ... make sure anyone else using orthographic view never
    /// applies a panini or bloom to 2d assets by default").
    ///
    /// <para>An overlay camera on the base camera's URP stack, a child of it with the same lens, post-processing off, that
    /// sees ONLY the <see cref="LayerName"/> layer (nothing in a scene or prefab is on it). Draw a mark with
    /// <c>RenderParams.camera = overlay.Camera</c> and <c>layer = overlay.Layer</c> and it lands after the base camera's
    /// bloom and tonemapper, over the world (an overlay clears depth), as the HUD does, so its colours read true. The world
    /// under it keeps the game's post-processing.</para>
    ///
    /// <para>Every orthographic view draws its 2D marks through one of these
    /// (<c>Tools/Build/check_orthographic_views.py</c> holds it). The Panini half of the rule needs nothing from a view:
    /// <see cref="OrthographicLens"/> applies it to every orthographic camera.</para>
    /// </summary>
    public sealed class UnprocessedOverlay
    {
        /// <summary>The layer the marks draw on. Nothing in a scene or prefab uses it.</summary>
        public const string LayerName = "3D UI";

        readonly Camera _base;

        /// <summary>The camera to draw the marks for: the overlay, or the base camera itself when the layer is missing.</summary>
        public Camera Camera { get; }

        /// <summary>The layer to draw the marks on (Default when the layer is missing).</summary>
        public int Layer { get; }

        public UnprocessedOverlay(Camera baseCamera, string name)
        {
            _base = baseCamera;
            int layer = LayerMask.NameToLayer(LayerName);
            if (layer < 0 || !baseCamera)
            {
                CSDebug.LogError($"[UnprocessedOverlay] The \"{LayerName}\" layer is missing from the Tag Manager, so {name}'s " +
                                 "marks are drawn through its post-processing.");
                Camera = baseCamera;
                Layer = 0;
                return;
            }
            Layer = layer;
            var go = new GameObject(name);
            go.transform.SetParent(baseCamera.transform, false);
            var cam = go.AddComponent<Camera>();
            cam.cullingMask = 1 << layer;
            cam.clearFlags = CameraClearFlags.Nothing;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderType = CameraRenderType.Overlay;
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.antialiasing = AntialiasingMode.None;
            baseCamera.GetUniversalAdditionalCameraData().cameraStack.Add(cam);
            Camera = cam;
            Sync();
        }

        /// <summary>
        /// Match the base camera's lens. Call after the base camera is framed each frame: an orthographic overlay takes
        /// its aspect from its own rect, so the rect is copied too.
        /// </summary>
        public void Sync()
        {
            var cam = Camera;
            if (!cam || cam == _base || !_base) return;
            cam.rect = _base.rect;
            cam.orthographic = _base.orthographic;
            cam.orthographicSize = _base.orthographicSize;
            cam.fieldOfView = _base.fieldOfView;
            cam.nearClipPlane = _base.nearClipPlane;
            cam.farClipPlane = _base.farClipPlane;
        }

        /// <summary>Take the overlay off the base camera's stack and destroy it.</summary>
        public void Dispose()
        {
            var cam = Camera;
            if (!cam || cam == _base) return;
            if (_base && _base.TryGetComponent(out UniversalAdditionalCameraData data)) data.cameraStack.Remove(cam);
            Object.Destroy(cam.gameObject);
        }
    }
}
