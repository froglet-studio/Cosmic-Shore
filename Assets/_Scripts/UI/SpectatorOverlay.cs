using System;
using CosmicShore.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The spectator's ONLY screen-space surface: a top bar with the watched pilot's name,
    /// previous / next pilot, the camera-mode toggle and a close (leave) button. Built in
    /// code, like <c>ArkwayVoyageHud</c> and the environment load veil, because a spectator
    /// lands in whichever of fifteen forked game canvases the host happens to be playing in,
    /// and none of them should have to carry a panel for a viewer who has no Player.
    /// Driven by <see cref="SpectatorController"/>; it renders and forwards clicks, nothing more.
    /// </summary>
    public sealed class SpectatorOverlay : MonoBehaviour
    {
        // Above the gameplay HUD, below the transition veil (32767) and the load veil (30000).
        const int SortingOrder = 20000;
        const float FadeSeconds = 0.25f;

        // Style B (Docs/MENU_REDESIGN_B.md section 22.5): theme tokens, theme faces. The overlay is
        // built in code (SpectatorController creates it on demand), so the faces come from TMP's
        // Resources folder rather than a serialized reference.
        const string BodyFontPath = "Fonts & Materials/ALDRICH-REGULAR SDF";
        const string LabelFontPath = "Fonts & Materials/ChakraPetch-Regular SDF";
        static Color Plate => WithAlpha(UITheme.Resolve(null, UIColorToken.SurfaceVeryDark), 0.82f);
        static Color Line => UITheme.Resolve(null, UIColorToken.SurfaceLight);
        static Color ButtonFill => UITheme.Resolve(null, UIColorToken.SurfaceDark);
        static Color LabelColor => UITheme.Resolve(null, UIColorToken.TextLight);
        static Color CaptionColor => UITheme.Resolve(null, UIColorToken.NeutralLightest);
        static Color LeaveColor => UITheme.Resolve(null, UIColorToken.Danger);

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        CanvasGroup _group;
        TMP_Text _nameText;
        TMP_Text _rosterText;
        TMP_Text _detailText;
        TMP_Text _cameraText;
        float _targetAlpha;
        RectTransform _bar;
        const float DropDistance = 14f;

        Action _onPrevious, _onNext, _onToggleCamera, _onLeave;

        public static SpectatorOverlay Create(Action onPrevious, Action onNext, Action onToggleCamera, Action onLeave)
        {
            var go = new GameObject("SpectatorOverlay", typeof(RectTransform));
            var overlay = go.AddComponent<SpectatorOverlay>();
            overlay._onPrevious = onPrevious;
            overlay._onNext = onNext;
            overlay._onToggleCamera = onToggleCamera;
            overlay._onLeave = onLeave;
            overlay.Build();
            return overlay;
        }

        public void Show() => _targetAlpha = 1f;
        public void Hide() => _targetAlpha = 0f;

        /// <summary>
        /// The spectated pilot: name in their domain colour, and under it the board's detail line -
        /// "Ruby - Sparrow" - when the caller knows the domain and hull.
        /// </summary>
        public void SetSpectated(string pilotName, Color domainColor, int index, int count, string detail)
        {
            SetSpectated(pilotName, domainColor, index, count);
            if (_detailText) _detailText.text = detail ?? string.Empty;
        }

        public void SetSpectated(string pilotName, Color domainColor, int index, int count)
        {
            if (_nameText)
            {
                _nameText.text = string.IsNullOrEmpty(pilotName) ? "Pilot" : pilotName;
                _nameText.color = domainColor.a > 0f ? domainColor : LabelColor;
            }
            SetRoster(index, count);
        }

        public void SetRoster(int index, int count)
        {
            if (_rosterText)
                _rosterText.text = count > 0 && index >= 0 ? $"{index + 1} / {count}" : string.Empty;
        }

        public void SetCameraMode(SpectatorController.CameraMode mode)
        {
            if (_cameraText)
                _cameraText.text = mode == SpectatorController.CameraMode.Dolly ? "CAM: DOLLY" : "CAM: PILOT";
        }

        void Update()
        {
            if (!_group) return;
            float step = Time.unscaledDeltaTime / FadeSeconds;
            _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, step);
            // The bar drops in as it fades in (and lifts away as it fades out), eased.
            if (_bar)
            {
                float a = _group.alpha;
                float eased = 1f - (1f - a) * (1f - a) * (1f - a);
                _bar.anchoredPosition = new Vector2(0f, -136f + DropDistance * (1f - eased));
            }
            bool live = _targetAlpha > 0f;
            _group.interactable = live;
            _group.blocksRaycasts = live;
        }

        // ── Construction ────────────────────────────────────────────────────

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;

            // ── The bar: one glass plate under the score columns (the HudSpectator board) ──
            var bar = MakeRect("Bar", transform);
            _bar = bar;
            bar.anchorMin = new Vector2(0.5f, 1f);
            bar.anchorMax = new Vector2(0.5f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.anchoredPosition = new Vector2(0f, -136f);
            bar.sizeDelta = new Vector2(900f, 72f);
            var barImage = bar.gameObject.AddComponent<Image>();
            barImage.color = Plate;
            barImage.raycastTarget = true;
            var barLine = bar.gameObject.AddComponent<Outline>();
            barLine.effectColor = Line;
            barLine.effectDistance = new Vector2(1f, -1f);

            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 12, 12);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            MakeButton(bar, "<", 48f, ButtonFill, LabelColor, () => _onPrevious?.Invoke());

            var caption = MakeText(bar, "SPECTATING", 14f, LabelColor, 140f, label: true);
            caption.alignment = TextAlignmentOptions.MidlineRight;
            caption.characterSpacing = 14f;

            MakeRule(bar);

            // Name over the detail line, one column.
            var pilot = MakeRect("Pilot", bar);
            pilot.sizeDelta = new Vector2(290f, 48f);
            var pilotLayout = pilot.gameObject.AddComponent<LayoutElement>();
            pilotLayout.preferredWidth = 290f;
            var column = pilot.gameObject.AddComponent<VerticalLayoutGroup>();
            column.childAlignment = TextAnchor.MiddleCenter;
            column.childControlWidth = true;
            column.childControlHeight = false;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            column.spacing = 2f;

            _nameText = MakeText(pilot, "Pilot", 20f, LabelColor, 290f);
            _nameText.rectTransform.sizeDelta = new Vector2(290f, 26f);
            _nameText.alignment = TextAlignmentOptions.Midline;
            _nameText.overflowMode = TextOverflowModes.Ellipsis;

            _detailText = MakeText(pilot, "", 12f, CaptionColor, 290f, label: true);
            _detailText.rectTransform.sizeDelta = new Vector2(290f, 16f);
            _detailText.alignment = TextAlignmentOptions.Midline;
            _detailText.characterSpacing = 14f;

            _rosterText = MakeText(bar, "", 22f, LabelColor, 70f);
            _rosterText.alignment = TextAlignmentOptions.Midline;

            MakeButton(bar, ">", 48f, ButtonFill, LabelColor, () => _onNext?.Invoke());

            MakeRule(bar);

            var camButton = MakeButton(bar, "CAM: PILOT", 150f, ButtonFill, LabelColor, () => _onToggleCamera?.Invoke(), label: true);
            _cameraText = camButton.GetComponentInChildren<TMP_Text>();

            // ── Leave, top-right: a Danger outline, never a filled red slab ──
            var leave = MakeRect("Leave", transform);
            leave.anchorMin = new Vector2(1f, 1f);
            leave.anchorMax = new Vector2(1f, 1f);
            leave.pivot = new Vector2(1f, 1f);
            leave.anchoredPosition = new Vector2(-24f, -136f);
            leave.sizeDelta = new Vector2(150f, 52f);
            var leaveButton = BuildButton(leave, "LEAVE", WithAlpha(LeaveColor, 0.12f), LeaveColor, () => _onLeave?.Invoke(), label: true);
            var leaveLine = leaveButton.gameObject.AddComponent<Outline>();
            leaveLine.effectColor = LeaveColor;
            leaveLine.effectDistance = new Vector2(1f, -1f);

            // ── Hints: the keys, then what they do (pad / keyboard) ──
            var hint = MakeText(transform, "LB  RB  /  Q  E   Switch pilot        Y  /  C   Camera        Esc   Leave",
                                15f, CaptionColor, 900f);
            var hintRect = hint.rectTransform;
            hintRect.anchorMin = new Vector2(0.5f, 1f);
            hintRect.anchorMax = new Vector2(0.5f, 1f);
            hintRect.pivot = new Vector2(0.5f, 1f);
            hintRect.anchoredPosition = new Vector2(0f, -222f);
            hintRect.sizeDelta = new Vector2(900f, 24f);
            hint.alignment = TextAlignmentOptions.Midline;
        }

        static void MakeRule(Transform parent)
        {
            var rect = MakeRect("Rule", parent);
            rect.sizeDelta = new Vector2(1f, 36f);
            var le = rect.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 1f;
            var img = rect.gameObject.AddComponent<Image>();
            img.color = Line;
            img.raycastTarget = false;
        }

        static RectTransform MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static TMP_FontAsset _bodyFont, _labelFont;

        static TMP_Text MakeText(Transform parent, string text, float size, Color color, float width, bool label = false)
        {
            var rect = MakeRect("Text", parent);
            rect.sizeDelta = new Vector2(width, 40f);
            var layoutElement = rect.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = width;

            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            var face = label
                ? (_labelFont ? _labelFont : _labelFont = Resources.Load<TMP_FontAsset>(LabelFontPath))
                : (_bodyFont ? _bodyFont : _bodyFont = Resources.Load<TMP_FontAsset>(BodyFontPath));
            if (face) tmp.font = face;
            else if (TMP_Settings.defaultFontAsset) tmp.font = TMP_Settings.defaultFontAsset;
            if (label) tmp.fontStyle = FontStyles.UpperCase;
            return tmp;
        }

        static Button MakeButton(Transform parent, string caption, float width, Color color, Color textColor, Action onClick, bool label = false)
        {
            var rect = MakeRect("Button", parent);
            rect.sizeDelta = new Vector2(width, 48f);
            var layoutElement = rect.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = width;
            layoutElement.minWidth = width;
            return BuildButton(rect, caption, color, textColor, onClick, label);
        }

        static Button BuildButton(RectTransform rect, string caption, Color color, Color textColor, Action onClick, bool label = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = true;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            // The Image carries the colour; the Button's own tint multiplies it, so hover lifts by
            // the theme's Light rather than an over-1 literal.
            var colors = button.colors;
            colors.highlightedColor = Color.Lerp(colors.normalColor, LabelColor, 0.25f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick?.Invoke());

            var text = MakeText(rect, caption, label ? 14f : 20f, textColor, rect.sizeDelta.x, label);
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            text.alignment = TextAlignmentOptions.Midline;
            return button;
        }
    }
}
