using CosmicShore.Gameplay;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The ELEMENTAL CONVERTER, on the Ark screen: the four elemental crystals idle with their own effects
    /// and, on CONVERT, fly in one after another and become one omni crystal, which wakes with all four of
    /// its effects (<see cref="OmniCombinePlayer"/>, the Omni Combine Lab's round-2 choreography). Every press
    /// draws a fresh choreography from <see cref="OmniCombineConfigSO"/>'s ranges, so no two plays are alike.
    ///
    /// <para><b>For now it only PLAYS the animation</b> (designer, 2026-10-13: "for now i just want to use
    /// it to test this animation rendering in the app shell"). Spending the player's elemental crystals and
    /// crediting omni crystals is the next step and is not wired: no such balances exist yet.</para>
    ///
    /// <para><b>Its picture is an off-screen stage</b>, the Toy Box preview's pattern
    /// (<see cref="ToyPreviewCamera"/>): the crystals stand far outside every gameplay camera's far clip, a
    /// hand-stepped camera adopts the game camera's image settings (post-processing is what makes the HDR
    /// crystals read as crystals - <see cref="OffscreenCameraSetup"/>) and renders into a RenderTexture this
    /// screen shows. It renders only while the Ark screen is the active one (<see cref="IScreen"/>), on
    /// UNSCALED time, because <c>ScreenSwitcher</c> pauses the game on every screen but HOME.</para>
    ///
    /// <para><b>Its UI is built in code</b> under this object's rect (the picture, a title and the CONVERT
    /// button), so the scene carries one GameObject and this component
    /// (<c>Tools/Build/author_elemental_converter.py</c>).</para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class ElementalConverterView : MonoBehaviour, IScreen
    {
        /// <summary>Far below the menu cell and the Toy Box's own stage (y −90000), past every far clip.</summary>
        static readonly Vector3 StageOrigin = new(0f, -150000f, 0f);

        [Header("Combine")]
        [Tooltip("What the converter plays: the combine data, the crystals' materials and the per-play ranges.")]
        [SerializeField] OmniCombineConfigSO config;

        [Header("Picture")]
        [Tooltip("Pixels along the picture's longer edge.")]
        [SerializeField, Range(256, 2048)] int resolution = 1280;
        [Tooltip("Vertical field of view of the stage camera, degrees.")]
        [SerializeField, Range(15f, 70f)] float fieldOfView = 34f;
        [Tooltip("Degrees per second the camera circles the combine.")]
        [SerializeField, Range(0f, 30f)] float orbitDegreesPerSecond = 5f;
        [Tooltip("Degrees the camera looks down on the combine.")]
        [SerializeField, Range(-60f, 60f)] float pitchDegrees = 18f;
        [Tooltip("Camera distance as a multiple of the distance that just fits what is on stage now (the four spread out, then fewer as each lands, then the omni).")]
        [SerializeField, Range(0.5f, 3f)] float framing = 1.15f;
        [Tooltip("Seconds the camera takes to ease onto a new framing: the dolly in as each crystal lands.")]
        [SerializeField, Range(0.1f, 5f)] float framingEaseSeconds = 1.6f;

        [Header("Layout")]
        [Tooltip("Title shown above the picture.")]
        [SerializeField] string title = "ELEMENTAL CONVERTER";
        [Tooltip("Caption on the button.")]
        [SerializeField] string buttonLabel = "CONVERT";
        [Tooltip("Font for the title and the button. Empty uses TextMeshPro's default.")]
        [SerializeField] TMP_FontAsset font;
        [Tooltip("The button's size in canvas units.")]
        [SerializeField] Vector2 buttonSize = new(420f, 110f);
        [Tooltip("How far the button sits above the bottom of this rect (clear of the nav bar).")]
        [SerializeField] float buttonBottomOffset = 230f;
        [Tooltip("How far the title sits below the top of this rect.")]
        [SerializeField] float titleTopOffset = 150f;
        [Tooltip("The button's fill.")]
        [SerializeField] Color buttonColor = new(0.6f, 1f, 0.5f, 0.9f);
        [Tooltip("The button's and the title's text colour.")]
        [SerializeField] Color textColor = new(0.04f, 0.08f, 0.04f, 1f);
        [Tooltip("The title's colour.")]
        [SerializeField] Color titleColor = new(0.6f, 1f, 0.5f, 1f);

        RawImage _surface;
        Button _button;
        Transform _stage;
        Camera _camera;
        RenderTexture _target;
        OmniCombinePlayer _player;
        bool _active;
        float _orbit, _frame;

        void Awake()
        {
            BuildUi();
        }

        public void OnScreenEnter()
        {
            _active = true;
            if (!EnsureStage()) return;
            // Arriving shows the four idling, waiting to be converted; a finished combine plays again.
            _player.Begin(convert: false);
            _frame = 0f;   // snap to the first framing rather than easing in from the last visit's
            if (_button) _button.Select();
        }

        public void OnScreenExit()
        {
            _active = false;
        }

        void OnDisable() => _active = false;

        void OnDestroy() => ReleaseStage();

        /// <summary>The CONVERT button: sends the four idling crystals in, or plays the whole combine again.</summary>
        public void Convert()
        {
            if (!EnsureStage()) return;
            _player.Convert();
        }

        void LateUpdate()
        {
            if (!_active || !_player) return;
            float dt = Time.unscaledDeltaTime;
            // Pose first (the Mass depth effect reads last frame's camera, which nothing can see), then frame it.
            _player.Tick(dt, _camera.transform.position);
            _orbit += orbitDegreesPerSecond * dt;
            var centre = _stage.position;
            float half = fieldOfView * 0.5f * Mathf.Deg2Rad;
            // Frame what is on stage NOW, eased: a slow dolly in as each crystal lands and the omni forms.
            float want = Mathf.Max(_player.StageRadius, 1e-3f);
            _frame = _frame <= 0f ? want : Mathf.Lerp(_frame, want, 1f - Mathf.Exp(-dt / framingEaseSeconds));
            float distance = framing * _frame / Mathf.Sin(half);
            var dir = Quaternion.Euler(pitchDegrees, _orbit, 0f) * Vector3.back;
            _camera.transform.SetPositionAndRotation(centre + dir * distance, Quaternion.LookRotation(-dir, Vector3.up));
            _camera.nearClipPlane = Mathf.Max(0.05f, distance - 2f * _player.FramingRadius);
            _camera.farClipPlane = distance + 2f * _player.FramingRadius;
            _camera.fieldOfView = fieldOfView;
            EnsureTarget();
            if (_target && _target.height > 0) _camera.aspect = (float)_target.width / _target.height;
            _camera.Render();
        }

        bool EnsureStage()
        {
            if (_player) return true;
            if (!config)
            {
                CSDebug.LogError("[ElementalConverterView] No OmniCombineConfig assigned: the converter has nothing to play.", this);
                return false;
            }
            var stage = new GameObject("ElementalConverterStage") { hideFlags = HideFlags.DontSave };
            stage.transform.position = StageOrigin;
            _stage = stage.transform;
            _player = OmniCombinePlayer.Create(_stage, config);
            if (!_player)
            {
                ReleaseStage();
                return false;
            }

            var go = new GameObject("ElementalConverterCamera") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(_stage, false);
            _camera = go.AddComponent<Camera>();
            _camera.enabled = false;                       // stepped by hand in LateUpdate
            _camera.clearFlags = CameraClearFlags.Skybox;
            _camera.cullingMask = ~LayerMask.GetMask("UI");
            _camera.useOcclusionCulling = false;
            OffscreenCameraSetup.AdoptGameCameraImage(_camera, postProcessing: true, antiAliasing: false, shadows: false);
            _camera.allowMSAA = false;
            var data = _camera.GetUniversalAdditionalCameraData();
            if (data)
            {
                data.requiresColorOption = CameraOverrideOption.Off;
                data.requiresDepthOption = CameraOverrideOption.Off;
            }
            EnsureTarget();
            return true;
        }

        /// <summary>A render target at the picture's own aspect (a square texture in a wide window stretches).</summary>
        void EnsureTarget()
        {
            var r = _surface ? _surface.rectTransform.rect : new Rect(0, 0, 1, 1);
            float w = Mathf.Max(1f, r.width), h = Mathf.Max(1f, r.height), k = resolution / Mathf.Max(w, h);
            int tw = Mathf.Max(64, Mathf.RoundToInt(w * k)), th = Mathf.Max(64, Mathf.RoundToInt(h * k));
            if (_target && _target.width == tw && _target.height == th) return;
            ReleaseTarget();
            _target = new RenderTexture(tw, th, 24) { name = "ElementalConverter", antiAliasing = 1 };
            _target.Create();
            if (_camera) _camera.targetTexture = _target;
            if (_surface) _surface.texture = _target;
        }

        void ReleaseTarget()
        {
            if (!_target) return;
            if (_camera) _camera.targetTexture = null;
            if (_surface) _surface.texture = null;
            _target.Release();
            Destroy(_target);
            _target = null;
        }

        void ReleaseStage()
        {
            ReleaseTarget();
            if (_stage) Destroy(_stage.gameObject);   // the player and the camera are its children
            _stage = null;
            _player = null;
            _camera = null;
        }

        void BuildUi()
        {
            var picture = NewChild("Picture");
            Stretch(picture);
            _surface = picture.gameObject.AddComponent<RawImage>();
            _surface.raycastTarget = false;
            _surface.color = Color.white;

            var heading = NewChild("Title");
            heading.anchorMin = new Vector2(0f, 1f);
            heading.anchorMax = new Vector2(1f, 1f);
            heading.pivot = new Vector2(0.5f, 1f);
            heading.sizeDelta = new Vector2(0f, 90f);
            heading.anchoredPosition = new Vector2(0f, -titleTopOffset);
            Label(heading, title, 54f, titleColor);

            var button = NewChild("Convert");
            button.anchorMin = button.anchorMax = new Vector2(0.5f, 0f);
            button.pivot = new Vector2(0.5f, 0f);
            button.sizeDelta = buttonSize;
            button.anchoredPosition = new Vector2(0f, buttonBottomOffset);
            var fill = button.gameObject.AddComponent<Image>();
            fill.color = buttonColor;
            _button = button.gameObject.AddComponent<Button>();
            _button.targetGraphic = fill;
            _button.onClick.AddListener(Convert);
            var caption = NewChild("Label", button);
            Stretch(caption);
            Label(caption, buttonLabel, 46f, textColor);
        }

        RectTransform NewChild(string name, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent ? parent : transform, false);
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        void Label(RectTransform rt, string text, float size, Color color)
        {
            var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font) label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
        }
    }
}
