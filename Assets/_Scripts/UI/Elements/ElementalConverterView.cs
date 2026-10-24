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
    /// <para><b>Its picture is built in code</b> under this object's rect, behind everything else. The style-B
    /// overlay (Docs/MENU_REDESIGN_B.md section 16: the title band, the recipe panel, the converter bar and
    /// CONVERT) is an authored prefab (<c>Tools/Build/author_menu_ark.py</c>) this view drives through the
    /// fields under "Style-B overlay"; with no CONVERT button wired it falls back to the first cut's
    /// code-built title and button (<c>Tools/Build/author_elemental_converter.py</c>).</para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class ElementalConverterView : MonoBehaviour, IScreen
    {
        /// <summary>
        /// Below the menu cell, past the menu camera's far clip (8000), and NOT far: a GPU computes positions in
        /// floats, and at y −150000 (the first cut) a float resolves only 1/64 of a unit, so every vertex snapped
        /// as the camera circled and the whole picture shimmered. At −30000 it resolves 1/512 - on an omni 40
        /// units across (<see cref="OmniCombineConfigSO"/>), well under a pixel.
        /// </summary>
        static readonly Vector3 StageOrigin = new(0f, -30000f, 0f);

        [Header("Combine")]
        [Tooltip("What the converter plays: the combine data, the crystals' materials and the per-play ranges.")]
        [SerializeField] OmniCombineConfigSO config;

        [Header("Picture")]
        [Tooltip("Rendered pixels per SCREEN pixel along each edge. The picture is rendered at the size it is shown on " +
                 "screen times this, then filtered down: 2 = four samples per pixel before MSAA.")]
        [SerializeField, Range(1, 3)] int supersampling = 2;
        [Tooltip("Hardware multisampling on top of the supersampling: 1 (off), 2, 4 or 8. Memory grows with it. Off at 3x supersampling (a mipped target cannot be multisampled).")]
        [SerializeField] int msaaSamples = 4;
        [Tooltip("The render target's longest edge never exceeds this many pixels (a 4K screen at 2x would be 7680).")]
        [SerializeField, Range(1024, 8192)] int maxTextureSize = 6144;
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
        [Tooltip("Caption on the button (and CONVERT's resting label in the style-B overlay).")]
        [SerializeField] string buttonLabel = "CONVERT";
        [Tooltip("Font for the title and the button. Empty uses TextMeshPro's default.")]
        [SerializeField] TMP_FontAsset font;
        [Tooltip("The button's size in canvas units.")]
        [SerializeField] Vector2 buttonSize = new(420f, 110f);
        [Tooltip("How far the button sits above the bottom of this rect (the nav bar is at the TOP of the screen, above this rect).")]
        [SerializeField] float buttonBottomOffset = 56f;
        [Tooltip("How far the title sits below the top of this rect.")]
        [SerializeField] float titleTopOffset = 36f;
        [Tooltip("The button's fill.")]
        [SerializeField] Color buttonColor = new(0.6f, 1f, 0.5f, 0.9f);
        [Tooltip("The button's and the title's text colour.")]
        [SerializeField] Color textColor = new(0.04f, 0.08f, 0.04f, 1f);
        [Tooltip("The title's colour.")]
        [SerializeField] Color titleColor = new(0.6f, 1f, 0.5f, 1f);

        [Header("Style-B overlay (optional)")]
        [Tooltip("CONVERT. Wiring it switches the code-built title and button off.")]
        [SerializeField] KitButton convertButton;
        [Tooltip("CONVERT's label: \"Convert\", \"Converting...\", \"Convert again\".")]
        [SerializeField] TMP_Text convertLabel;
        [Tooltip("The converter bar's eyebrow: Ready / Converting / Converted.")]
        [SerializeField] KitText statusText;
        [Tooltip("The run's clock, \"00:11 / 00:22\".")]
        [SerializeField] TMP_Text clockText;
        [Tooltip("The bar's four segments (Image, Filled Horizontal), in fly-in order: Space, Charge, Mass, Time.")]
        [SerializeField] Image[] segments = { };
        [Tooltip("Each segment's travelling spark, shown once its element has merged.")]
        [SerializeField] CanvasGroup[] segmentSparks = { };
        [Tooltip("The recipe panel's four rows, in fly-in order.")]
        [SerializeField] ConverterRecipeRow[] recipeRows = { };
        [Tooltip("\"2 of 4 merged\".")]
        [SerializeField] KitText mergedText;
        [Tooltip("The omni block: dim until the omni forms.")]
        [SerializeField] CanvasGroup omniGroup;
        [Tooltip("Pops the omni in as it forms (ceremony). Optional.")]
        [SerializeField] KitEntrance omniPop;

        const float OmniDimAlpha = 0.35f;
        const int Elements = 4;

        enum Phase { None = -1, Ready = 0, Converting = 1, Converted = 2 }

        Phase _phase = Phase.None;
        int _merged = -1, _clockShown = -1, _clockTotal = -1;
        bool _omniShown;

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
            // No Select() here: the switcher's Refocus lands on CONVERT, the screen's first live control,
            // and a second owner of first focus is how the pad lost track of it on other screens.
            _phase = Phase.None;
            DrawOverlay();
        }

        // Leaving lets the picture go: at screen pixels x2 with MSAA it is hundreds of MB on a phone, and nothing
        // shows it until the Ark is entered again (LateUpdate rebuilds it). The stage itself is small and stays.
        public void OnScreenExit()
        {
            _active = false;
            ReleaseTarget();
        }

        void OnDisable()
        {
            _active = false;
            ReleaseTarget();
        }

        void OnDestroy() => ReleaseStage();

        /// <summary>The CONVERT button: sends the four idling crystals in, or plays the whole combine again.</summary>
        public void Convert()
        {
            if (!EnsureStage()) return;
            // A press mid-run used to restart the whole sequence; the run finishes first now (CONVERT reads
            // "Converting..." and is inactive meanwhile).
            if (_player.IsRunning) return;
            _player.Convert();
            DrawOverlay();
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
            // Near hugs the stage (nothing on it comes closer than distance - FramingRadius): a far-out near plane is
            // what keeps the depth buffer's precision for the crystals themselves.
            _camera.nearClipPlane = Mathf.Max(0.5f, 0.5f * (distance - _player.FramingRadius));
            _camera.farClipPlane = distance + 2f * _player.FramingRadius;
            _camera.fieldOfView = fieldOfView;
            EnsureTarget();
            if (_target && _target.height > 0) _camera.aspect = (float)_target.width / _target.height;
            _camera.Render();
            DrawOverlay();
        }

        /// <summary>
        /// The overlay's live state: each element's row and segment from its own flight, the merged count,
        /// the omni (dim until it forms, then popped in), the clock and CONVERT. Text and colours are written
        /// only when they change; the bars every frame.
        /// </summary>
        void DrawOverlay()
        {
            if (!_player) return;
            int merged = 0;
            for (int k = 0; k < Elements; k++)
            {
                float p = _player.ElementProgress(k);
                if (p >= 1f) merged++;
                if (k < recipeRows.Length && recipeRows[k]) recipeRows[k].Draw(p);
                if (k < segments.Length && segments[k]) segments[k].fillAmount = p;
                if (k < segmentSparks.Length) KitVisibility.SetShown(segmentSparks[k], p >= 1f);
            }
            if (merged != _merged)
            {
                _merged = merged;
                if (mergedText) mergedText.SetText($"{merged} of {Elements} merged");
            }

            bool formed = _player.OmniFormed;
            if (formed != _omniShown || _phase == Phase.None)
            {
                if (formed && !_omniShown && omniPop && isActiveAndEnabled) omniPop.Play();
                _omniShown = formed;
                if (omniGroup) omniGroup.alpha = formed ? 1f : OmniDimAlpha;
            }

            var phase = _player.IsRunning ? Phase.Converting : _player.IsDone ? Phase.Converted : Phase.Ready;
            if (phase != _phase)
            {
                _phase = phase;
                if (statusText)
                    statusText.SetText(phase switch { Phase.Converting => "Converting", Phase.Converted => "Converted", _ => "Ready" });
                if (convertLabel)
                    convertLabel.text = phase switch { Phase.Converting => "Converting...", Phase.Converted => "Convert again", _ => buttonLabel };
                if (convertButton)
                {
                    convertButton.Variant = phase == Phase.Converting ? KitButtonVariant.Inactive : KitButtonVariant.Purchase;   // converting spends the merged elements: gold
                    convertButton.interactable = phase != Phase.Converting;
                }
            }

            // Play time runs on after the omni wakes (it idles), so the clock stops at the run's length.
            int total = Mathf.CeilToInt((float)_player.RunDuration);
            int shown = Mathf.Min(Mathf.FloorToInt((float)_player.RunTime), total);
            if (clockText && (shown != _clockShown || total != _clockTotal))
            {
                _clockShown = shown;
                _clockTotal = total;
                clockText.text = $"{Clock(shown)} / {(total > 0 ? Clock(total) : "--:--")}";
            }
        }

        /// <summary>"01:05" - minutes and seconds.</summary>
        public static string Clock(int seconds)
        {
            seconds = Mathf.Max(0, seconds);
            return $"{seconds / 60:00}:{seconds % 60:00}";
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
            // The picture is the whole screen and the point of it, so it takes every quality the game camera has and
            // more: its post AA (SMAA High when the game has none), HDR, MSAA on the target and supersampling.
            OffscreenCameraSetup.AdoptGameCameraImage(_camera, postProcessing: true, antiAliasing: true, shadows: false);
            _camera.allowHDR = true;
            _camera.allowMSAA = msaaSamples > 1;
            var data = _camera.GetUniversalAdditionalCameraData();
            if (data)
            {
                data.requiresColorOption = CameraOverrideOption.Off;
                data.requiresDepthOption = CameraOverrideOption.Off;
                if (data.antialiasing == AntialiasingMode.None) data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.High;
            }
            EnsureTarget();
            return true;
        }

        /// <summary>
        /// A render target at the picture's on-screen PIXEL size times <see cref="supersampling"/> (never a fixed
        /// resolution stretched over the screen), at the picture's own aspect. At 2x the RawImage's bilinear
        /// sampling lands between four texels for every screen pixel, which is an exact box filter; above 2x the
        /// target carries mips so the downsample stays filtered.
        /// </summary>
        void EnsureTarget()
        {
            var r = _surface ? _surface.rectTransform.rect : new Rect(0, 0, 1, 1);
            var canvas = _surface ? _surface.canvas : null;
            float toPixels = canvas ? canvas.scaleFactor : 1f;
            float w = Mathf.Max(1f, r.width * toPixels) * supersampling, h = Mathf.Max(1f, r.height * toPixels) * supersampling;
            float k = Mathf.Min(1f, maxTextureSize / Mathf.Max(w, h));
            int tw = Mathf.Max(64, Mathf.RoundToInt(w * k)), th = Mathf.Max(64, Mathf.RoundToInt(h * k));
            // A multisampled target cannot carry mips, and above 2x the supersampling is the anti-aliasing anyway.
            bool mips = supersampling > 2;
            int msaa = mips ? 1 : msaaSamples >= 8 ? 8 : msaaSamples >= 4 ? 4 : msaaSamples >= 2 ? 2 : 1;
            if (_target && _target.width == tw && _target.height == th && _target.antiAliasing == msaa) return;
            ReleaseTarget();
            _target = new RenderTexture(tw, th, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "ElementalConverter",
                antiAliasing = msaa,
                useMipMap = mips,
                autoGenerateMips = mips,
                filterMode = mips ? FilterMode.Trilinear : FilterMode.Bilinear,
            };
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
            // Behind the authored overlay: a child added in Awake would otherwise draw over it.
            picture.SetAsFirstSibling();
            _surface = picture.gameObject.AddComponent<RawImage>();
            _surface.raycastTarget = false;
            _surface.color = Color.white;

            if (convertButton)
            {
                convertButton.onClick.AddListener(Convert);
                return;
            }

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
