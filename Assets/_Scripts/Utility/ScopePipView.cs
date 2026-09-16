using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using CosmicShore.Gameplay;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The Serpent scope's <b>picture-in-picture</b>: the ordinary chase shot of your own vessel,
    /// drawn into a corner of the screen while the cockpit view has the middle of it.
    ///
    /// <para><b>Why it exists.</b> The scope takes the pilot's whole view into the cockpit and
    /// magnifies it, which is the point — and it also takes away every cue they fly by: where the
    /// hull is, how it is banked, what is beside it. A scoped Serpent could line up a shot or fly,
    /// not both. The PIP gives the flying half back without giving up the magnification.</para>
    ///
    /// <para><b>It is NOT a second gameplay camera</b>, which the platform forbids for four
    /// concrete reasons (Docs/REAR_VIEW.md): the speed tunnel resolves <c>CameraManager</c>'s
    /// ACTIVE controller, <c>ApplyCameraGraphicsSettings</c> pushes the player's FOV and AA onto
    /// three managed cameras and no others, background colour is applied per camera, and
    /// <c>Camera.main</c> returns the first ENABLED camera tagged MainCamera. This camera is
    /// created at runtime, is <b>never tagged MainCamera</b>, renders only into a
    /// <c>RenderTexture</c> and is left <b>disabled</b> — it is stepped by hand — so it is outside
    /// all four systems by construction and cannot draw to the display at all. It is the same
    /// shape the connecting panel's arena preview uses (<c>ConnectingArenaPreview</c>).</para>
    ///
    /// <para><b>The cost is real and is stated rather than hidden.</b> Unlike that preview — which
    /// stands the gameplay camera DOWN because the panel covers the screen — this one is a genuine
    /// SECOND render of the world, because the first one is what the player is looking through. So
    /// it is paid for the only way left: a low render height, no post-processing, no shadows, no
    /// anti-aliasing, a capped refresh rate, and a lifetime of exactly as long as the trigger is
    /// held. It is off the moment the scope drops.</para>
    ///
    /// <para><b>It shows the camera's OWN chase pose</b>, read from
    /// <c>CustomCameraController.FollowOffset</c> — the vessel's authored <c>CameraSettingsSO</c>
    /// value — rather than a constant, so a Serpent whose camera is retuned gets the retuned shot
    /// and the PIP cannot drift into being a second opinion about where this hull is watched
    /// from.</para>
    /// </summary>
    public sealed class ScopePipView
    {
        readonly RawImage _surface;

        Camera _camera;
        RenderTexture _texture;
        float _nextRenderTime;

        /// <summary>
        /// Render height in pixels. Small on purpose — see the class note on cost — but it has to
        /// track the WINDOW: the surface is half the screen's height, so a 216p texture upscaled
        /// into 540 screen pixels is visibly soft, and a picture of your own hull that cannot be
        /// read is the same as no picture.
        /// </summary>
        public int RenderHeight = 360;

        /// <summary>How often the window refreshes, in Hz. Capped for the same reason.</summary>
        public float RefreshHz = 20f;

        public ScopePipView(RawImage surface) => _surface = surface;

        public bool Visible => _surface != null && _surface.enabled;

        /// <summary>
        /// Show the window and advance it, at most <see cref="RefreshHz"/> times a second. Safe to
        /// call every frame; safe to call before a camera controller exists.
        /// </summary>
        public void Tick()
        {
            if (_surface == null) return;

            var controller = CameraManager.Instance != null
                ? CameraManager.Instance.GetActiveController() as CustomCameraController
                : null;
            var target = controller != null ? controller.FollowTarget : null;
            if (controller == null || target == null) { Hide(); return; }

            if (!EnsureCamera()) { Hide(); return; }

            _surface.texture = _texture;
            _surface.enabled = true;

            PoseCamera(controller, target);

            // Stepped by hand rather than left enabled: an enabled camera renders every frame, and
            // this one is drawing the whole arena a second time.
            if (Time.unscaledTime < _nextRenderTime) return;
            _nextRenderTime = Time.unscaledTime + 1f / Mathf.Max(1f, RefreshHz);
            _camera.Render();
        }

        public void Hide()
        {
            if (_surface != null) _surface.enabled = false;
        }

        /// <summary>
        /// Tear the window down completely. Called when the vessel goes away, not merely when the
        /// trigger is released — a RenderTexture that outlives its owner is a leak nothing reports.
        /// </summary>
        public void Dispose()
        {
            Hide();
            if (_camera != null)
            {
                _camera.targetTexture = null;
                Object.Destroy(_camera.gameObject);
                _camera = null;
            }
            if (_texture != null)
            {
                _texture.Release();
                Object.Destroy(_texture);
                _texture = null;
            }
        }

        void PoseCamera(CustomCameraController controller, Transform target)
        {
            // Exactly the controller's own non-first-person pose: offset in the vessel's frame,
            // then look back at it with the vessel's up. Reproducing it here rather than borrowing
            // the live camera is what keeps this window outside the four systems above.
            Vector3 offset = controller.FollowOffset;
            Vector3 position = target.position + target.rotation * offset;

            _camera.transform.position = position;
            if (SafeLookRotation.TryGet(target.position - position, target.up, out var rot,
                                        context: null, logError: false))
                _camera.transform.rotation = rot;

            float distance = Mathf.Max(1f, offset.magnitude);
            // Derived from the shot, never copied from the gameplay camera: that camera's planes
            // are sized for a view this one does not have, and the arena clips out of a borrowed
            // far plane (the same finding ConnectingArenaPreview records).
            _camera.nearClipPlane = Mathf.Max(0.3f, distance * 0.01f);
            _camera.farClipPlane = Mathf.Max(2000f, distance * 200f);
        }

        bool EnsureCamera()
        {
            if (_camera != null && _texture != null) return true;

            if (_texture == null)
            {
                int height = Mathf.Clamp(RenderHeight, 96, 480);
                _texture = new RenderTexture(Mathf.RoundToInt(height * 16f / 9f), height, 16)
                {
                    name = "SerpentScopePipRT",
                    antiAliasing = 1,
                    filterMode = FilterMode.Bilinear,
                    useMipMap = false,
                };
            }

            if (_camera == null)
            {
                var go = new GameObject("[SerpentScopePipCamera]");
                // Deliberately NOT tagged MainCamera, and deliberately left disabled.
                _camera = go.AddComponent<Camera>();
                _camera.enabled = false;
                _camera.fieldOfView = 60f;

                var source = Camera.main;
                if (source != null)
                {
                    _camera.clearFlags = source.clearFlags;
                    _camera.backgroundColor = source.backgroundColor;
                    _camera.cullingMask = source.cullingMask;
                    _camera.allowHDR = source.allowHDR;
                }
                else
                {
                    _camera.clearFlags = CameraClearFlags.Skybox;
                    _camera.cullingMask = ~0;
                }

                // Everything except UI: a window that rendered the canvas would draw itself inside
                // itself, one frame stale, forever.
                int ui = LayerMask.NameToLayer("UI");
                if (ui >= 0) _camera.cullingMask &= ~(1 << ui);

                _camera.allowMSAA = false;

                var data = _camera.GetUniversalAdditionalCameraData();
                if (data != null)
                {
                    // The three expensive passes are OFF here, unlike the connecting panel's
                    // preview, because that one replaces the gameplay render and this one is
                    // ADDED to it.
                    data.renderPostProcessing = false;
                    data.antialiasing = AntialiasingMode.None;
                    data.renderShadows = false;
                }
            }

            _camera.targetTexture = _texture;
            return true;
        }
    }
}
