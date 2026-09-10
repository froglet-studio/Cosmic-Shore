using System;
using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.Rendering.Universal;
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
    /// That is the whole reason this is ~200 lines rather than the arcade's satellite-arena
    /// machinery — a mode has to BUILD its world to be previewed, a toy is already standing in
    /// ours. It also means the preview cannot drift from the toy: re-skin the toy and the picture
    /// re-skins itself.</para>
    ///
    /// <para><b>Rendered on demand into a RenderTexture, never to the screen.</b> The camera is
    /// created disabled and stepped by hand, for the reason <c>Docs/CONNECTING_PANEL.md</c>
    /// records: an ENABLED camera takes a full extra culling+draw pass over the whole menu cell
    /// every frame, and this one is looking at a lava lamp with a live prism ecology in it.</para>
    ///
    /// <para><b>What it costs is decided by the FAR PLANE, and the first cut got that wrong.</b>
    /// The shot used to reach forty toy-distances out, so every render culled and drew the whole
    /// cell — the lattice forest, the trail, every creature — to show a ring 130 units away, and at
    /// 20 renders a second on a menu already spending its frame on the lava lamp that read as a
    /// window that stutters. The far plane now reaches a few toy-distances past the subject: the
    /// toy and its neighbourhood, with the skybox behind. Post-processing, shadows, MSAA and HDR
    /// are off on this camera — none of them is legible on a picture this size — and the target is
    /// smaller. Each render is then a small pass, which is what lets it run at a rate the eye
    /// reads as motion rather than as a slideshow.</para>
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
    ///
    /// <para><b>It also shows a VARIANT — and that one is not the live world, so it gets a stage of
    /// its own.</b> Selecting a row asks the toy to build a model of what the option would give you
    /// (<see cref="ToyShellOption.BuildPreview"/>), and there is nowhere in the menu cell to put
    /// it: dropped in place it is a mystery object hanging in the lava lamp. So the model is built
    /// on a private stage parked far outside every gameplay camera's far clip — the same answer
    /// the arcade's satellite arena reaches for the same reason, with this camera's Skybox clear
    /// giving it a clean backdrop for free. The stage sits on −Y where the satellite sits on +X, so
    /// the two previews can never photograph each other.</para>
    ///
    /// <para><b>And it can WATCH — a live thing in the world the toy just made.</b> A Spawn press
    /// on the Lifeform Matrix releases a creature into the cell, and telling the player it
    /// happened is weaker than showing it: <see cref="Watch"/> turns the camera onto that object
    /// where it landed, at a radius the option states (the creature blooms in from zero, so its
    /// own bounds say nothing on the frame it appears), and goes back to the toy when the target
    /// dies or the window moves on.</para>
    ///
    /// <para>A variant is framed by MEASURING it rather than by being told its size. Each toy
    /// builds its model at whatever radius its own stations use, so a camera that assumed a number
    /// would frame the next toy's model wrong; a bounds walk per selection is right for a model
    /// this class has never seen.</para>
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class ToyPreviewCamera : MonoBehaviour
    {
        [Header("Framing")]
        [SerializeField, Min(1f), Tooltip("How far back from the subject the camera sits, as a " +
                 "multiple of its radius. The ring IS the toy's size, so this frames every toy " +
                 "the same way whatever it is built out of.")]
        float distanceFactor = 3.2f;

        [SerializeField, Range(10f, 90f), Tooltip("Field of view for the shot. Wider than the " +
                 "gameplay camera on purpose: the toy should sit IN its cell, not fill the frame.")]
        float fieldOfView = 42f;

        [SerializeField, Tooltip("Lift above the subject's own plane, as a multiple of its radius " +
                 "- a slight three-quarter view reads as an object rather than a sprite.")]
        float liftFactor = 0.35f;

        [SerializeField, Min(0f), Tooltip("Degrees per second the camera orbits the subject. 0 " +
                 "holds still. A slow drift is what tells the player this is a live world, not a photo.")]
        float orbitDegreesPerSecond = 10f;

        [Header("Cost")]
        [SerializeField, Min(1), Tooltip("Renders per second, at most - a render is skipped on any " +
                 "frame that arrives sooner. Every render is a pass over the toy's neighbourhood, " +
                 "bounded by farReachFactor.")]
        int renderRate = 30;

        [SerializeField, Min(64), Tooltip("Longest render-texture edge, in pixels. The other edge " +
                 "follows the surface's own aspect - see ResolveTargetSize.")]
        int resolution = 512;

        [SerializeField, Min(2f), Tooltip("How far the shot reaches PAST the subject, as a multiple " +
                 "of the camera distance. This is the cost dial: the cell behind the toy is drawn " +
                 "only out to here, and the skybox fills the rest. Forty drew the whole lattice " +
                 "forest to show one ring; a few is the toy and its neighbours.")]
        float farReachFactor = 5f;

        RawImage _surface;
        Camera _camera;
        RenderTexture _target;
        Transform _subject;
        float _radius = 40f;
        float _orbit;
        float _nextRender;
        float _lastRenderTime;

        // The toy this window is bound to, kept so ClearVariant can go back to it. Deliberately
        // separate from _subject, which is whatever is in frame right now - the toy, a model, or
        // something the toy released into the world.
        Toy _toy;
        Transform _stage;
        GameObject _variant;
        Transform _watched;

        /// <summary>
        /// Where a variant's model is built. Far outside Menu_Main's 8000 far clip so no gameplay
        /// camera can see it, and on a different axis from the arcade's satellite arena (+X at
        /// 120000) so the two previews cannot end up in each other's shots.
        /// </summary>
        static readonly Vector3 StageOrigin = new(0f, -90000f, 0f);

        /// <summary>True while the window is showing a variant's model rather than the toy.</summary>
        public bool IsShowingVariant => _variant;

        /// <summary>True while the window is turned onto something the toy released into the world.</summary>
        public bool IsWatching => _watched;

        void Awake() => _surface = GetComponent<RawImage>();

        /// <summary>
        /// Point the window at a toy. Safe to call with null — the surface simply goes blank,
        /// which is the honest state for a toy the toybox has not built yet.
        /// </summary>
        public void Show(Toy toy)
        {
            _toy = toy;
            _watched = null;
            DestroyVariant();
            FrameToy();
        }

        /// <summary>
        /// Show what an option would GIVE you, built by the toy that offers it. Returns false when
        /// the option declines to build one (most do) - the window keeps showing the toy, which is
        /// the honest picture rather than a blank surface.
        /// </summary>
        public bool ShowVariant(Func<Transform, GameObject> build)
        {
            // Whether one was showing decides what a FAILURE means. Dropping the old model and
            // then returning early would leave the camera framing a destroyed transform, which is
            // the one state this class must never be left in - so a failed build after a live one
            // goes back to the toy, while a failed build with nothing showing leaves the shot
            // exactly as it was rather than resetting the orbit on every unpreviewable row.
            bool had = _variant || _watched;
            _watched = null;
            DestroyVariant();

            GameObject model = null;
            if (build != null)
            {
                EnsureStage();
                model = build(_stage);
            }

            if (!model)
            {
                if (had) FrameToy();
                return false;
            }

            _variant = model;
            Frame(model.transform, MeasureRadius(model));
            return true;
        }

        /// <summary>
        /// Turn the window onto a live object in the WORLD - what a press just released - at a
        /// radius the caller states. Null (or a dead target) is a no-op that leaves the picture
        /// where it is, so a toy that made nothing changes nothing.
        /// </summary>
        public bool Watch(Transform target, float radius)
        {
            if (!target) return false;
            DestroyVariant();
            _watched = target;
            Frame(target, radius > 0f ? radius : (_toy ? _toy.SwitchRingRadius : 40f));
            return true;
        }

        /// <summary>Drop the variant's model (or the watched object) and go back to photographing the toy itself.</summary>
        public void ClearVariant()
        {
            if (!_variant && !_watched) return;
            _watched = null;
            DestroyVariant();
            FrameToy();
        }

        void FrameToy() => Frame(_toy ? _toy.transform : null, _toy ? _toy.SwitchRingRadius : 40f);

        void Frame(Transform subject, float radius)
        {
            _subject = subject;
            _radius = Mathf.Max(1f, radius);
            _orbit = 0f;
            _lastRenderTime = Time.unscaledTime;

            if (_subject) EnsureRig();
            if (_surface) _surface.enabled = _subject;

            // Render one frame immediately so the panel never opens on a blank or stale surface.
            _nextRender = 0f;
            if (_subject) Step();
        }

        public void Hide()
        {
            _toy = null;
            _watched = null;
            DestroyVariant();
            _subject = null;
            if (_surface) _surface.enabled = false;
        }

        void EnsureStage()
        {
            if (_stage) return;
            var go = new GameObject("ToyPreviewStage") { hideFlags = HideFlags.DontSave };
            go.transform.position = StageOrigin;
            _stage = go.transform;
        }

        void DestroyVariant()
        {
            if (!_variant) return;
            Destroy(_variant);
            _variant = null;
        }

        /// <summary>
        /// The radius that frames <paramref name="model"/>: the extent of its renderers about
        /// their own centre, never about the stage origin - a model built off-centre would
        /// otherwise read as an oversized one and be framed from far too far away. Falls back to
        /// the toy's ring for a model with no renderers, which frames nothing but frames it sanely.
        /// </summary>
        float MeasureRadius(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return _toy ? Mathf.Max(1f, _toy.SwitchRingRadius) : 40f;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return Mathf.Max(1f, bounds.extents.magnitude);
        }

        void OnDisable()
        {
            // Modals fade rather than deactivate, so this fires on scene teardown - but a preview
            // holding a surface for a session it is not drawing is worth releasing anyway.
            ReleaseRig();
            _subject = null;
            _toy = null;
            _watched = null;
        }

        void LateUpdate()
        {
            if (!_subject || !_camera)
            {
                // A watched creature that has since died (eaten, starved) takes the subject with
                // it: go back to the toy rather than drawing a frozen last frame of nothing.
                if (_watched == null && _toy && _surface && _surface.enabled && !_subject) FrameToy();
                return;
            }
            if (Time.unscaledTime < _nextRender) return;
            Step();
        }

        void Step()
        {
            float now = Time.unscaledTime;
            _nextRender = now + 1f / Mathf.Max(1, renderRate);

            // Orbit by the time that actually passed, not by a fixed step per render: a skipped
            // render (a slow frame) then costs a bigger step rather than a stall, and the drift
            // reads at the same speed whatever the frame rate.
            _orbit += orbitDegreesPerSecond * Mathf.Clamp(now - _lastRenderTime, 0f, 0.25f);
            _lastRenderTime = now;

            var centre = _subject.position;
            var offset = Quaternion.Euler(0f, _orbit, 0f) * Vector3.back * (_radius * distanceFactor);
            offset += Vector3.up * (_radius * liftFactor);

            _camera.transform.SetPositionAndRotation(centre + offset,
                                                     Quaternion.LookRotation(centre - (centre + offset), Vector3.up));

            // Derived from the shot, never inherited: near hugs the camera, far reaches a few
            // camera-distances past the subject - the toy and its neighbourhood, never the whole
            // cell. The skybox clear fills what the far plane cuts, so nothing reads as missing.
            float distance = _radius * distanceFactor;
            _camera.nearClipPlane = Mathf.Max(0.05f, _radius * 0.05f);
            _camera.farClipPlane = distance * Mathf.Max(2f, farReachFactor);
            _camera.fieldOfView = fieldOfView;
            // Told explicitly rather than left to the render target: the two agree by construction
            // here, and an explicit aspect is what makes a later uvRect or letterbox change safe.
            if (_target && _target.height > 0)
                _camera.aspect = (float)_target.width / _target.height;
            _camera.Render();
        }

        void EnsureRig()
        {
            // A square target drawn into a wide window is the classic preview STRETCH, and it is a
            // defect in the texture rather than in the layout: the surface is authored the shape the
            // designer wanted, so the render target takes ITS aspect and the camera is told about it.
            var size = ResolveTargetSize();
            if (_target && (_target.width != size.x || _target.height != size.y)) ReleaseTarget();

            if (!_target)
            {
                _target = new RenderTexture(size.x, size.y, 16) { name = "ToyPreview", antiAliasing = 1 };
                _target.Create();
                if (_surface) _surface.texture = _target;
            }

            if (_camera)
            {
                _camera.targetTexture = _target;
                return;
            }

            var go = new GameObject("ToyPreviewCamera") { hideFlags = HideFlags.DontSave };
            _camera = go.AddComponent<Camera>();
            _camera.enabled = false;                 // stepped by hand - see the class doc
            _camera.targetTexture = _target;
            _camera.clearFlags = CameraClearFlags.Skybox;
            // Drawing the UI layer here would put this panel inside its own window.
            _camera.cullingMask = ~LayerMask.GetMask("UI");
            _camera.allowMSAA = false;
            _camera.useOcclusionCulling = false;

            // Shadows and anti-aliasing are not legible on a picture this size and each is a
            // whole extra pass over what the camera draws, so they stay off. POST-PROCESSING IS
            // NOT OPTIONAL: every lifeform and prism material in the game is authored HDR-emissive
            // against the gameplay volume's tonemapper, and drawn without it a creature comes out
            // as a blown-out white silhouette with no colour in it - which is exactly how the
            // first cut of this window rendered a shark. The volume mask and HDR flag are ADOPTED
            // from the gameplay camera rather than written down, so the picture is tonemapped by
            // the same profile the world is.
            var data = _camera.GetUniversalAdditionalCameraData();
            var main = Camera.main;
            var mainData = main ? main.GetUniversalAdditionalCameraData() : null;
            _camera.allowHDR = main ? main.allowHDR : true;
            if (data)
            {
                data.renderPostProcessing = !mainData || mainData.renderPostProcessing;
                data.volumeLayerMask = mainData ? mainData.volumeLayerMask : (LayerMask)~0;
                data.renderShadows = false;
                data.antialiasing = AntialiasingMode.None;
                data.requiresColorOption = CameraOverrideOption.Off;
                data.requiresDepthOption = CameraOverrideOption.Off;
            }
        }

        /// <summary>
        /// The render target's pixel size, at the SURFACE's aspect with the longest edge held at
        /// <see cref="resolution"/>. Falls back to square only when the rect has not been laid out
        /// yet, which on this panel cannot happen: a card binds the window after the layout pass.
        /// </summary>
        Vector2Int ResolveTargetSize()
        {
            float w = 1f, h = 1f;
            if (_surface)
            {
                var r = _surface.rectTransform.rect;
                if (r.width > 1f && r.height > 1f) { w = r.width; h = r.height; }
            }

            float k = resolution / Mathf.Max(w, h);
            return new Vector2Int(Mathf.Max(64, Mathf.RoundToInt(w * k)),
                                  Mathf.Max(64, Mathf.RoundToInt(h * k)));
        }

        void ReleaseRig()
        {
            if (_camera) { Destroy(_camera.gameObject); _camera = null; }
            DestroyVariant();
            if (_stage) { Destroy(_stage.gameObject); _stage = null; }
            ReleaseTarget();
        }

        void ReleaseTarget()
        {
            if (!_target) return;
            if (_surface) _surface.texture = null;
            if (_camera) _camera.targetTexture = null;
            _target.Release();
            Destroy(_target);
            _target = null;
        }
    }
}
