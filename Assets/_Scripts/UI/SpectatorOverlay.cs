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

        static readonly Color BarColor     = new(0.05f, 0.07f, 0.12f, 0.82f);
        static readonly Color ButtonColor  = new(0.12f, 0.16f, 0.26f, 0.95f);
        static readonly Color LeaveColor   = new(0.45f, 0.12f, 0.14f, 0.95f);
        static readonly Color LabelColor   = new(0.85f, 0.9f, 1f, 1f);
        static readonly Color CaptionColor = new(0.6f, 0.68f, 0.8f, 1f);

        CanvasGroup _group;
        TMP_Text _nameText;
        TMP_Text _rosterText;
        TMP_Text _cameraText;
        float _targetAlpha;

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

        public void SetSpectated(string pilotName, Color domainColor, int index, int count)
        {
            if (_nameText)
            {
                _nameText.text = string.IsNullOrEmpty(pilotName) ? "PILOT" : pilotName.ToUpperInvariant();
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
                _cameraText.text = mode == SpectatorController.CameraMode.Dolly ? "CAM: DOLLY" : "CAM: PLAYER";
        }

        void Update()
        {
            if (!_group) return;
            float step = Time.unscaledDeltaTime / FadeSeconds;
            _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, step);
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

            // ── Top bar ────────────────────────────────────────────────────
            var bar = MakeRect("Bar", transform);
            bar.anchorMin = new Vector2(0.5f, 1f);
            bar.anchorMax = new Vector2(0.5f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.anchoredPosition = new Vector2(0f, -24f);
            bar.sizeDelta = new Vector2(900f, 72f);
            var barImage = bar.gameObject.AddComponent<Image>();
            barImage.color = BarColor;
            barImage.raycastTarget = true;

            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 10, 10);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            MakeButton(bar, "◀", 56f, ButtonColor, () => _onPrevious?.Invoke());

            var caption = MakeText(bar, "SPECTATING", 18f, CaptionColor, 150f);
            caption.alignment = TextAlignmentOptions.MidlineRight;

            _nameText = MakeText(bar, "PILOT", 28f, LabelColor, 300f);
            _nameText.fontStyle = FontStyles.Bold;
            _nameText.alignment = TextAlignmentOptions.Midline;
            _nameText.overflowMode = TextOverflowModes.Ellipsis;

            _rosterText = MakeText(bar, "", 18f, CaptionColor, 70f);
            _rosterText.alignment = TextAlignmentOptions.MidlineLeft;

            MakeButton(bar, "▶", 56f, ButtonColor, () => _onNext?.Invoke());

            var camButton = MakeButton(bar, "CAM: PLAYER", 150f, ButtonColor, () => _onToggleCamera?.Invoke());
            _cameraText = camButton.GetComponentInChildren<TMP_Text>();

            // ── Leave, top-right ───────────────────────────────────────────
            var leave = MakeRect("Leave", transform);
            leave.anchorMin = new Vector2(1f, 1f);
            leave.anchorMax = new Vector2(1f, 1f);
            leave.pivot = new Vector2(1f, 1f);
            leave.anchoredPosition = new Vector2(-24f, -24f);
            leave.sizeDelta = new Vector2(150f, 52f);
            BuildButton(leave, "✕  LEAVE", LeaveColor, () => _onLeave?.Invoke());

            // ── Hint line ──────────────────────────────────────────────────
            var hint = MakeText(transform, "◀ ▶ / Q E  switch pilot     C  camera     Esc  leave", 16f, CaptionColor, 900f);
            var hintRect = hint.rectTransform;
            hintRect.anchorMin = new Vector2(0.5f, 1f);
            hintRect.anchorMax = new Vector2(0.5f, 1f);
            hintRect.pivot = new Vector2(0.5f, 1f);
            hintRect.anchoredPosition = new Vector2(0f, -104f);
            hintRect.sizeDelta = new Vector2(900f, 28f);
            hint.alignment = TextAlignmentOptions.Midline;
        }

        static RectTransform MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static TMP_Text MakeText(Transform parent, string text, float size, Color color, float width)
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
            if (TMP_Settings.defaultFontAsset) tmp.font = TMP_Settings.defaultFontAsset;
            return tmp;
        }

        static Button MakeButton(Transform parent, string label, float width, Color color, Action onClick)
        {
            var rect = MakeRect("Button", parent);
            rect.sizeDelta = new Vector2(width, 48f);
            var layoutElement = rect.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = width;
            layoutElement.minWidth = width;
            return BuildButton(rect, label, color, onClick);
        }

        static Button BuildButton(RectTransform rect, string label, Color color, Action onClick)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = true;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick?.Invoke());

            var text = MakeText(rect, label, 20f, LabelColor, rect.sizeDelta.x);
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            text.alignment = TextAlignmentOptions.Midline;
            text.fontStyle = FontStyles.Bold;
            return button;
        }
    }
}
