using System;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The fair-play notice and appeal screen (phase B; Docs/AntiCheat/README.md §4.6-4.7). Shown when the server says
    /// a notice is owed (<see cref="FairPlayTrust.Current"/>): after three matches in the Undertow or the quitters'
    /// pool, or at once when a person restricted the account. It says what kind of behaviour, which rule of the Code
    /// of Conduct, whether an automated check or a person decided, that paid content is untouched, and offers an
    /// appeal with an optional few words. It never says how the detection works.
    ///
    /// <para>It builds its own UI at runtime, as <see cref="PrivacyConsentOverlay"/> does (nothing to place in a
    /// scene, so nothing to forget), in the same Style-B kit: the panel and button sprites come from
    /// <c>PrivacyConsentConfig</c>, every word from <see cref="FairPlayNoticeConfigSO"/>. Not a wall: OK closes it
    /// and the player plays on in their pool. Shown once per session per notice state.</para>
    /// </summary>
    public class FairPlayNoticeOverlay : MonoBehaviour
    {
        /// <summary>Under the privacy overlay (32766): the age gate comes first on a first launch.</summary>
        const int SortingOrder = 32765;
        const float PanelWidth = 860f;
        const int AppealMaxChars = 500;
        const string TitleFontPath = "Fonts & Materials/ALDRICH-REGULAR SDF";
        const string LabelFontPath = "Fonts & Materials/ChakraPetch-Regular SDF";

        static FairPlayNoticeOverlay s_instance;
        static string s_shownKey;

        FairPlayNoticeConfigSO _words;
        PrivacyConsentConfigSO _kit;
        TMP_FontAsset _titleFont;
        TMP_FontAsset _labelFont;
        FairPlayTrust.Notice _notice;
        RectTransform _appealSection;
        TMP_InputField _appealField;
        TMP_Text _appealStatus;
        Button _appealButton;
        Button _closeButton;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_instance = null;
            s_shownKey = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Subscribe()
        {
            if (!FairPlayTrustGate.Enabled) return;
            FairPlayTrust.OnNoticeChanged -= Show;
            FairPlayTrust.OnNoticeChanged += Show;
        }

        /// <summary>Shows <paramref name="notice"/> unless this session already showed it in this state.</summary>
        public static void Show(FairPlayTrust.Notice notice)
        {
            if (notice == null || string.IsNullOrEmpty(notice.kind)) return;
            string key = $"{notice.kind}|{notice.restricted}|{notice.appeal}";
            if (s_instance != null || key == s_shownKey) return;
            s_shownKey = key;

            var go = new GameObject("[FairPlayNotice_Overlay]");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<FairPlayNoticeOverlay>();
            s_instance._notice = notice;
            s_instance._words = FairPlayNoticeConfigSO.LoadOrDefault();
            s_instance._kit = Resources.Load<PrivacyConsentConfigSO>("PrivacyConsentConfig");
            s_instance.Build();
        }

        void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }

        void Update()
        {
            bool back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                        || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            if (back && (_appealField == null || !_appealField.isFocused)) Close();
        }

        void Build()
        {
            EnsureEventSystem();
            _titleFont = Resources.Load<TMP_FontAsset>(TitleFontPath);
            _labelFont = Resources.Load<TMP_FontAsset>(LabelFontPath);

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();

            var scrim = NewRect("Scrim", transform);
            Stretch(scrim);
            scrim.gameObject.AddComponent<Image>().color = Tint(UIColorToken.SurfaceBlack, 0.72f);

            var content = SafeAreaLayer.Create(transform);
            var panel = NewPanel(content);

            string kind = _notice.kind;
            string rule = _notice.rule > 0 ? _notice.rule.ToString() : string.Empty;
            AddText(panel, _words.Eyebrow.ToUpperInvariant(), 14f, _labelFont, UIColorToken.NeutralLightest).characterSpacing = 14f;
            AddText(panel, _words.TitleFor(kind), 30f, _titleFont, UIColorToken.TextLight);
            AddText(panel, TemplateText.Fill(_words.BodyFor(kind), "rule", rule), 18f, _labelFont, UIColorToken.NeutralLightest);
            AddText(panel, _notice.restricted
                    ? TemplateText.Fill(_words.RestrictedLine, "until", FormatDate(_notice.until))
                    : _words.AutomatedLine, 16f, _labelFont, UIColorToken.NeutralLightest);
            AddText(panel, _words.PaidContentLine, 16f, _labelFont, UIColorToken.NeutralLightest);

            if (!string.IsNullOrEmpty(_words.CodeOfConductUrl) && !string.IsNullOrEmpty(_words.CodeOfConductLabel))
                AddLink(panel, _words.CodeOfConductLabel, () => Application.OpenURL(_words.CodeOfConductUrl));

            _appealSection = NewColumn(panel);
            BuildAppealSection();

            var row = NewRow(panel);
            _closeButton = AddButton(row, _words.CloseButton, _words.SecondaryKeycap, false, Close);
            var events = EventSystem.current;
            if (events) events.SetSelectedGameObject(_closeButton.gameObject);
        }

        void BuildAppealSection()
        {
            for (int i = _appealSection.childCount - 1; i >= 0; i--) Destroy(_appealSection.GetChild(i).gameObject);
            _appealField = null;
            _appealButton = null;

            if (_notice.appeal == "open")
            {
                AddText(_appealSection, _words.AppealOpenLine, 16f, _labelFont, UIColorToken.ControlActive);
                return;
            }
            if (_notice.appeal == "refused")
                AddText(_appealSection, _words.AppealRefusedLine, 16f, _labelFont, UIColorToken.NeutralLightest);

            AddText(_appealSection, _words.AppealPrompt, 16f, _labelFont, UIColorToken.NeutralLightest);
            _appealField = AddInputField(_appealSection, _words.AppealPlaceholder);
            _appealStatus = AddText(_appealSection, string.Empty, 15f, _labelFont, UIColorToken.Danger);
            var row = NewRow(_appealSection);
            _appealButton = AddButton(row, _words.AppealButton, _words.PrimaryKeycap, true, SendAppeal);
        }

        async void SendAppeal()
        {
            if (_appealButton) _appealButton.interactable = false;
            bool open = await FairPlayTrust.AppealAsync(_appealField ? _appealField.text : string.Empty);
            if (this == null) return;
            if (open)
            {
                _notice.appeal = "open";
                s_shownKey = $"{_notice.kind}|{_notice.restricted}|{_notice.appeal}";
                BuildAppealSection();
                return;
            }
            if (_appealStatus) _appealStatus.text = _words.AppealFailedLine;
            if (_appealButton) _appealButton.interactable = true;
        }

        void Close() => Destroy(gameObject);

        static string FormatDate(string iso) =>
            DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var d)
                ? d.ToLocalTime().ToString("d") : string.Empty;

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("[FairPlayNotice_EventSystem]", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(es);
        }

        #region Tiny UI toolkit (the PrivacyConsentOverlay kit, for one panel)

        static Color Tint(UIColorToken token, float alpha = 1f)
        {
            var c = UITheme.Resolve(null, token);
            c.a *= alpha;
            return c;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        Image NewImage(RectTransform r, Sprite sprite, Color color)
        {
            var image = r.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            return image;
        }

        Image AddLayer(RectTransform parent, string name, Sprite sprite, Color color, float outset = 0f)
        {
            var r = NewRect(name, parent);
            Stretch(r);
            r.offsetMin = new Vector2(-outset, -outset);
            r.offsetMax = new Vector2(outset, outset);
            r.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var image = NewImage(r, sprite, color);
            image.raycastTarget = false;
            return image;
        }

        RectTransform NewPanel(Transform parent)
        {
            var panel = NewRect("Notice", parent);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(PanelWidth, 0f);
            NewImage(panel, _kit ? _kit.PanelFillSprite : null, Tint(UIColorToken.SurfaceVeryDark, 0.94f)).raycastTarget = true;
            AddLayer(panel, "Border", _kit ? _kit.PanelBorderSprite : null, Tint(UIColorToken.SurfaceDark));
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(34, 34, 30, 32);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return panel;
        }

        static RectTransform NewColumn(RectTransform parent)
        {
            var col = NewRect("Appeal", parent);
            var layout = col.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(0, 0, 12, 0);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return col;
        }

        static RectTransform NewRow(RectTransform parent)
        {
            var row = NewRect("Row", parent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.padding = new RectOffset(0, 0, 14, 0);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return row;
        }

        static TMP_Text AddText(Transform parent, string text, float size, TMP_FontAsset font, UIColorToken color)
        {
            var r = NewRect("Text", parent);
            var tmp = r.gameObject.AddComponent<TextMeshProUGUI>();
            if (font) tmp.font = font;
            tmp.text = text ?? string.Empty;
            tmp.fontSize = size;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.raycastTarget = false;
            tmp.color = Tint(color);
            r.gameObject.SetActive(!string.IsNullOrEmpty(text));   // an empty field shows nothing
            return tmp;
        }

        TMP_InputField AddInputField(Transform parent, string placeholder)
        {
            var r = NewRect("AppealText", parent);
            var element = r.gameObject.AddComponent<LayoutElement>();
            element.minHeight = 96f;
            element.preferredHeight = 96f;
            var bg = NewImage(r, _kit ? _kit.ButtonFillSprite : null, Tint(UIColorToken.SurfaceBlack));
            AddLayer(r, "Border", _kit ? _kit.ButtonBorderSprite : null, Tint(UIColorToken.ControlActive));

            var area = NewRect("TextArea", r);
            Stretch(area);
            area.offsetMin = new Vector2(16f, 10f);
            area.offsetMax = new Vector2(-16f, -10f);
            area.gameObject.AddComponent<RectMask2D>();

            var textR = NewRect("Text", area);
            Stretch(textR);
            var text = textR.gameObject.AddComponent<TextMeshProUGUI>();
            if (_labelFont) text.font = _labelFont;
            text.fontSize = 18f;
            text.color = Tint(UIColorToken.TextLight);
            text.alignment = TextAlignmentOptions.TopLeft;

            var phR = NewRect("Placeholder", area);
            Stretch(phR);
            var ph = phR.gameObject.AddComponent<TextMeshProUGUI>();
            if (_labelFont) ph.font = _labelFont;
            ph.text = placeholder ?? string.Empty;
            ph.fontSize = 18f;
            ph.color = Tint(UIColorToken.SurfaceLight);
            ph.alignment = TextAlignmentOptions.TopLeft;

            var field = r.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = bg;
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = ph;
            field.characterLimit = AppealMaxChars;
            field.lineType = TMP_InputField.LineType.MultiLineNewline;
            return field;
        }

        Button AddButton(Transform parent, string label, string keycap, bool primary, Action onClick)
        {
            var r = NewRect("Button", parent);
            var element = r.gameObject.AddComponent<LayoutElement>();
            element.minWidth = 200f;
            element.preferredHeight = 56f;

            var content = primary ? Tint(UIColorToken.SurfaceBlack) : Tint(UIColorToken.TextLight);
            var fill = NewImage(r, _kit ? _kit.ButtonFillSprite : null, primary ? Tint(UIColorToken.Cta) : Tint(UIColorToken.SurfaceVeryDark));
            AddLayer(r, "Border", _kit ? _kit.ButtonBorderSprite : null, primary ? Tint(UIColorToken.Cta) : Tint(UIColorToken.NeutralLightest));
            var ring = AddLayer(r, "FocusRing", _kit ? _kit.FocusRingSprite : null, Tint(UIColorToken.TextLight), 6f);

            var layout = r.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 0, 0);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            if (KitInputDevice.ShowsControlHints && !string.IsNullOrEmpty(keycap))
            {
                var cap = AddText(r, keycap, 13f, _labelFont, primary ? UIColorToken.SurfaceBlack : UIColorToken.TextLight);
                cap.alignment = TextAlignmentOptions.Center;
            }
            var tmp = AddText(r, (label ?? string.Empty).ToUpperInvariant(), 18f, _labelFont, primary ? UIColorToken.SurfaceBlack : UIColorToken.TextLight);
            tmp.characterSpacing = 8f;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = content;

            var button = r.gameObject.AddComponent<Button>();
            button.targetGraphic = ring;
            var colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.25f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            fill.raycastTarget = true;
            button.onClick.AddListener(() => onClick?.Invoke());
            r.gameObject.SetActive(!string.IsNullOrEmpty(label));
            return button;
        }

        void AddLink(Transform parent, string label, Action onClick)
        {
            var r = NewRect("Link", parent);
            var element = r.gameObject.AddComponent<LayoutElement>();
            element.minHeight = 32f;
            element.preferredHeight = 32f;
            var image = r.gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            var tmp = AddText(r, label, 15f, _labelFont, UIColorToken.ControlActive);
            Stretch((RectTransform)tmp.transform);
            tmp.fontStyle = FontStyles.Underline;
            var button = r.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick?.Invoke());
        }

        #endregion
    }
}
