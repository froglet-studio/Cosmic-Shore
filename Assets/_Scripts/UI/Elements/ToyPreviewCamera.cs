using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A live window onto a toy standing out by the cell membrane — the picture the Toy Box's
    /// configure modal shows in place of a preview image.
    ///
    /// <para><b>It photographs the REAL toy, not a model of one.</b> The toybox has already built
    /// every toy in Menu_Main's own cell by the time this modal can open, so there is nothing to
    /// stand up and nothing to tear down: point a camera at the object the player will fly to.
    /// That is the whole reason this is ~150 lines rather than the arcade's satellite-arena
    /// machinery — a mode has to BUILD its world to be previewed, a toy is already standing in
    /// ours. It also means the preview cannot drift from the toy: re-skin the toy and the picture
    /// re-skins itself.</para>
    ///
    /// <para><b>Rendered on demand into a RenderTexture, never to the screen.</b> The camera is
    /// created disabled and stepped by hand, for the reason <c>Docs/CONNECTING_PANEL.md</c>
    /// records: an ENABLED camera takes a full extra culling+draw pass over the whole menu cell
    /// every frame, and this one is looking at a lava lamp with a live prism ecology in it. A
    /// disabled camera driven from <c>LateUpdate</c> at a stated rate costs what we ask it to.</para>
    ///
    /// <para>Three details are each a bug if you get them wrong, and all three are borrowed from
    /// the connecting panel's preview rather than re-derived:</para>
    /// <list type="number">
    /// <item>The UI layer is EXCLUDED from the culling mask, or the camera draws this very panel
    /// inside its own window, one frame stale.</item>
    /// <item>The clip planes are derived from the shot, never copied from a template camera —
    /// a menu camera's far plane is sized for the cell it orbits and would clip the toy.</item>
    /// <item>The RenderTexture is released on disable, because a modal that is merely faded out
    /// (this project's modals stay ACTIVE at alpha 0) would otherwise hold a full-size surface
    /// for the life of the session.</item>
    /// </list>
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class ToyPreviewCamera : MonoBehaviour
    {
        [Header("Framing")]
        [SerializeField, Min(1f), Tooltip("How far back from the toy the camera sits, as a " +
                 "multiple of the toy's own switch-ring radius. The ring IS the toy's size, so " +
                 "this frames every toy the same way whatever it is built out of.")]
        float distanceFactor = 3.2f;

        [SerializeField, Range(10f, 90f), Tooltip("Field of view for the shot. Wider than the " +
                 "gameplay camera on purpose: the toy should sit IN its cell, not fill the frame.")]
        float fieldOfView = 42f;

        [SerializeField, Tooltip("Lift above the toy's own plane, as a multiple of its radius - " +
                 "a slight three-quarter view reads as an object rather than a sprite.")]
        float liftFactor = 0.35f;

        [SerializeField, Min(0f), Tooltip("Degrees per second the camera orbits the toy. 0 holds " +
                 "still. A slow drift is what tells the player this is a live world, not a photo.")]
        float orbitDegreesPerSecond = 12f;

        [Header("Cost")]
        [SerializeField, Min(1), Tooltip("Renders per second. The toy barely moves, so this does " +
                 "not need the display's frame rate - and every render is a full pass over the " +
                 "menu cell.")]
        int renderRate = 20;

        [SerializeField, Min(64), Tooltip("Render texture edge, in pixels.")]
        int resolution = 512;

        RawImage _surface;
        Camera _camera;
        RenderTexture _target;
        Transform _subject;
        float _radius = 40f;
        float _orbit;
        float _nextRender;

        void Awake() => _surface = GetComponent<RawImage>();

        /// <summary>
        /// Point the window at a toy. Safe to call with null — the surface simply goes blank,
        /// which is the honest state for a toy the toybox has not built yet.
        /// </summary>
        public void Show(Toy toy)
        {
            _subject = toy ? toy.transform : null;
            _radius = toy ? Mathf.Max(1f, toy.SwitchRingRadius) : 40f;
            _orbit = 0f;

            if (_subject) EnsureRig();
            if (_surface) _surface.enabled = _subject;

            // Render one frame immediately so the panel never opens on a blank or stale surface.
            _nextRender = 0f;
            if (_subject) Step();
        }

        public void Hide()
        {
            _subject = null;
            if (_surface) _surface.enabled = false;
        }

        void OnDisable()
        {
            // Modals fade rather than deactivate, so this fires on scene teardown - but a preview
            // holding a 512x512 surface for a session it is not drawing is worth releasing anyway.
            ReleaseRig();
            _subject = null;
        }

        void LateUpdate()
        {
            if (!_subject || !_camera) return;
            if (Time.unscaledTime < _nextRender) return;
            Step();
        }

        void Step()
        {
            _nextRender = Time.unscaledTime + 1f / Mathf.Max(1, renderRate);
            _orbit += orbitDegreesPerSecond / Mathf.Max(1, renderRate);

            var centre = _subject.position;
            var offset = Quaternion.Euler(0f, _orbit, 0f) * Vector3.back * (_radius * distanceFactor);
            offset += Vector3.up * (_radius * liftFactor);

            _camera.transform.SetPositionAndRotation(centre + offset,
                                                     Quaternion.LookRotation(centre - (centre + offset), Vector3.up));

            // Derived from the shot, never inherited: near hugs the camera, far reaches past the
            // toy with room for the cell behind it, so nothing in frame is clipped.
            _camera.nearClipPlane = Mathf.Max(0.05f, _radius * 0.05f);
            _camera.farClipPlane = _radius * distanceFactor * 40f;
            _camera.fieldOfView = fieldOfView;
            _camera.Render();
        }

        void EnsureRig()
        {
            if (!_target)
            {
                _target = new RenderTexture(resolution, resolution, 24) { name = "ToyPreview" };
                _target.Create();
                if (_surface) _surface.texture = _target;
            }

            if (_camera) return;

            var go = new GameObject("ToyPreviewCamera") { hideFlags = HideFlags.DontSave };
            _camera = go.AddComponent<Camera>();
            _camera.enabled = false;                 // stepped by hand - see the class doc
            _camera.targetTexture = _target;
            _camera.clearFlags = CameraClearFlags.Skybox;
            // Drawing the UI layer here would put this panel inside its own window.
            _camera.cullingMask = ~LayerMask.GetMask("UI");
        }

        void ReleaseRig()
        {
            if (_camera) { Destroy(_camera.gameObject); _camera = null; }
            if (_target)
            {
                if (_surface) _surface.texture = null;
                _target.Release();
                Destroy(_target);
                _target = null;
            }
        }
    }
}
