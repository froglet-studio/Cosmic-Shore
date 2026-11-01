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
    /// The first-run privacy flow: a neutral COPPA age gate, then an opt-in analytics consent
    /// dialog. Shown once per install; the answer persists and it never appears again.
    ///
    /// <para><b>It builds its own UI and is created by AppManager, deliberately.</b> The previous
    /// consent screen was a MonoBehaviour that had to be placed on a panel in some scene — and it
    /// never was, so the consent gate stayed shut forever and every analytics event in the game was
    /// silently dropped before reaching UGS or PostHog. A screen that has to be remembered is a
    /// screen that gets forgotten. This one cannot be unwired: it is constructed at runtime, made
    /// persistent, and appears in every scene and every build with nothing to author.
    /// <see cref="SceneTransitionManager"/> builds its fade overlay the same way.</para>
    ///
    /// <para>Copy and the policy URL come from <see cref="PrivacyConsentConfigSO"/> in Resources,
    /// so legal can change what the player is told without a code change.</para>
    ///
    /// <para><b>Not a wall.</b> Declining, or answering under-13, dismisses the dialog and lets the
    /// player carry on — they simply leave analytics off. Gating play on consent would not be
    /// freely-given consent.</para>
    /// </summary>
    public class PrivacyConsentOverlay : MonoBehaviour
    {
        /// <summary>Sits directly under the scene-transition fade (32767) so a transition covers it.</summary>
        const int SortingOrder = 32766;

        const int MinimumAge = 13;
        const int OldestPlausibleAge = 100;

        static PrivacyConsentOverlay _instance;

        /// <summary>
        /// The overlay currently holding the screen, or null once the flow is resolved. The
        /// Authentication scene reads this to PAUSE its boot while the player is still on the
        /// age gate: the overlay is deliberately non-blocking, so without that wait the scene's
        /// sign-in, profile-load and safety timeouts all expire behind the scrim and the player
        /// surfaces in the menu having never been offered a username (the shipped symptom on a
        /// first launch: "asked my birthday, then nothing, then the menu").
        /// </summary>
        public static PrivacyConsentOverlay Current => _instance;

        AnalyticsServiceFacade _analytics;
        PrivacyConsentConfigSO _config;

        GameObject _agePanel;
        GameObject _consentPanel;
        TMP_InputField _birthYearField;
        TMP_Text _ageError;
        Button _continueButton;

        /// <summary>Raised once the player has resolved the flow, whatever the outcome.</summary>
        public event Action OnPrivacyFlowCompleted;

        /// <summary>
        /// Creates the overlay if the player still owes an answer. Safe to call more than once and
        /// safe to call when the flow is already resolved — it returns null and costs nothing.
        /// </summary>
        public static PrivacyConsentOverlay CreateIfNeeded(AnalyticsServiceFacade analytics)
        {
            if (_instance != null)
                return _instance;

            if (analytics == null)
            {
                Debug.LogWarning("[PrivacyConsent] No AnalyticsServiceFacade - privacy flow skipped. " +
                                 "Collection stays off, which is the safe default.");
                return null;
            }

            if (!analytics.NeedsPrivacyFlow)
                return null;

            var go = new GameObject("[PrivacyConsent_Overlay]");
            DontDestroyOnLoad(go);

            _instance = go.AddComponent<PrivacyConsentOverlay>();
            _instance._analytics = analytics;
            _instance._config = Resources.Load<PrivacyConsentConfigSO>("PrivacyConsentConfig");
            _instance.Build();
            return _instance;
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        #region Construction

        // Style B (Docs/MENU_REDESIGN_B.md section 26; the StartAgeGate / StartConsent boards).
        const string TitleFontPath = "Fonts & Materials/ALDRICH-REGULAR SDF";
        const string LabelFontPath = "Fonts & Materials/ChakraPetch-Regular SDF";
        const float AgePanelWidth = 520f;
        const float ConsentPanelWidth = 860f;
        const float FadeInSeconds = 0.2f;

        TMP_FontAsset _titleFont;
        TMP_FontAsset _labelFont;
        CanvasGroup _group;
        Button _acceptButton;

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
            // Expand: the whole 1920 x 1080 design stays on screen at any aspect (a 4:3 tablet, a
            // 20:9 phone) - Docs/MENU_REDESIGN_B.md section 28.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            gameObject.AddComponent<GraphicRaycaster>();
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;

            // Scrim: dims the game behind and swallows clicks so nothing underneath reacts.
            // It BLEEDS - a dimmer that stopped at the safe area would leave lit strips of live
            // gameplay under the cutout - so the panels ride a fitted sibling above it rather than
            // being its children (Docs/UI_ARCHITECTURE_AUDIT.md §1.3).
            var scrim = NewRect("Scrim", transform);
            Stretch(scrim);
            var scrimImage = scrim.gameObject.AddComponent<Image>();
            scrimImage.color = Tint(UIColorToken.SurfaceBlack, 0.72f);

            // Both panels are centred and hug their content, so nothing is clipped today; fitted anyway
            // so that under a ONE-SIDED cutout they stay centred in the usable area rather than in a
            // screen a slice of which the player cannot see.
            var content = SafeAreaLayer.Create(transform);

            _agePanel = BuildAgeGate(content).gameObject;
            _consentPanel = BuildConsent(content).gameObject;

            // Resume mid-flow: an age-checked player only owes the consent answer.
            ShowStep(_analytics.AgeChecked);
        }

        RectTransform BuildAgeGate(RectTransform parent)
        {
            var panel = NewPanel("AgeGate", parent, AgePanelWidth);

            AddEyebrow(panel, _config != null ? _config.AgeEyebrow : "First flight");
            AddTitle(panel, _config != null ? _config.AgeTitle : "Before you fly");
            AddBody(panel, _config != null ? _config.AgeBody : "What year were you born?");

            // A birth-year question rather than "are you 13 or older?" - a yes/no age question
            // nudges the player toward the answer that unlocks collection, which is exactly what
            // COPPA guidance says a neutral age screen must not do.
            _birthYearField = AddInputField(panel, "YYYY");
            _birthYearField.onSubmit.AddListener(_ => SubmitBirthYear());
            _ageError = AddNote(panel, "");
            _ageError.color = Tint(UIColorToken.Danger);
            _ageError.gameObject.SetActive(false);

            var row = NewRow(panel);
            _continueButton = AddButton(row, _config != null ? _config.ContinueLabel : "Continue", _config != null ? _config.PrimaryKeycap : "A", true, SubmitBirthYear);

            return panel;
        }

        RectTransform BuildConsent(RectTransform parent)
        {
            var panel = NewPanel("Consent", parent, ConsentPanelWidth);

            AddEyebrow(panel, _config != null ? _config.ConsentEyebrow : "Privacy");
            AddTitle(panel, _config != null ? _config.ConsentTitle : "Help us improve Cosmic Shore");
            AddBody(panel, _config != null ? _config.ConsentBody : "");

            if (_config != null && _config.HasPrivacyPolicyUrl)
                AddLinkButton(panel, _config.PolicyLabel, () => Application.OpenURL(_config.PrivacyPolicyUrl));

            var row = NewRow(panel);
            _acceptButton = AddButton(row, _config != null ? _config.AcceptLabel : "I agree", _config != null ? _config.PrimaryKeycap : "A", true, Accept);
            AddButton(row, _config != null ? _config.DeclineLabel : "No thanks", _config != null ? _config.SecondaryKeycap : "B", false, Decline);

            return panel;
        }

        /// <summary>Shows one step and puts the pad and keyboard on it: the birth-year field (type at
        /// once; Enter continues), or the consent's primary action (B / Esc declines, see Update).</summary>
        void ShowStep(bool consent)
        {
            _agePanel.SetActive(!consent);
            _consentPanel.SetActive(consent);
            var events = EventSystem.current;
            if (!events) return;
            var target = consent ? (_acceptButton ? _acceptButton.gameObject : null)
                                 : (_birthYearField ? _birthYearField.gameObject : null);
            if (target) events.SetSelectedGameObject(target);
        }

        void Update()
        {
            if (_group && _group.alpha < 1f)
                _group.alpha = Mathf.Min(1f, _group.alpha + Time.unscaledDeltaTime / FadeInSeconds);

            // B / Esc is the consent step's back: it declines, the same as No thanks. The age gate has
            // no back - it asks a question with no default answer.
            if (_consentPanel == null || !_consentPanel.activeSelf) return;
            bool back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                        || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            if (back) Decline();
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            // InputSystemUIInputModule, not StandaloneInputModule: the project ships with
            // activeInputHandler = 1 (new Input System only), under which the legacy module throws
            // and no click would ever register. Matches DiagnosticsHUD.EnsureEventSystem.
            var es = new GameObject("[PrivacyConsent_EventSystem]",
                typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(es);
        }

        #endregion

        #region Flow

        void SubmitBirthYear()
        {
            string raw = _birthYearField != null ? _birthYearField.text.Trim() : "";
            int thisYear = DateTime.UtcNow.Year;

            if (!int.TryParse(raw, out int year) || year > thisYear || year < thisYear - OldestPlausibleAge)
            {
                ShowAgeError($"Please enter a year between {thisYear - OldestPlausibleAge} and {thisYear}.");
                return;
            }

            _analytics.SubmitBirthYear(year);

            if (thisYear - year >= MinimumAge)
            {
                ShowStep(true);
                return;
            }

            // Under 13: no consent step exists for them, and nothing is ever collected.
            Finish();
        }

        void ShowAgeError(string message)
        {
            if (_ageError == null) return;
            _ageError.text = message;
            _ageError.gameObject.SetActive(true);
        }

        void Accept()
        {
            _analytics.SetConsent(true);
            Finish();
        }

        void Decline()
        {
            _analytics.SetConsent(false);
            Finish();
        }

        /// <summary>
        /// Close the overlay without an answer (a tool with no person at the keyboard: the escort sim, the voyage smoke
        /// test). Nothing is granted or recorded, so analytics stays off and the overlay asks again on the next launch;
        /// the boot it was holding is released, as an answer would release it.
        /// </summary>
        public void CloseUnanswered() => Finish();

        void Finish()
        {
            OnPrivacyFlowCompleted?.Invoke();
            Destroy(gameObject);
        }

        #endregion

        #region Tiny UI toolkit

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

        /// <summary>A layer that fills its parent and stays out of the parent's layout.</summary>
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

        /// <summary>A slivered panel of a fixed width whose height hugs its column.</summary>
        RectTransform NewPanel(string name, Transform parent, float width)
        {
            var panel = NewRect(name, parent);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(width, 0f);

            var fill = NewImage(panel, _config != null ? _config.PanelFillSprite : null, Tint(UIColorToken.SurfaceVeryDark, 0.94f));
            fill.raycastTarget = true;
            AddLayer(panel, "Border", _config != null ? _config.PanelBorderSprite : null, Tint(UIColorToken.SurfaceDark));

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(34, 34, 30, 32);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return panel;
        }

        static RectTransform NewRow(RectTransform parent)
        {
            var row = NewRect("Row", parent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.padding = new RectOffset(0, 0, 18, 0);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return row;
        }

        void AddEyebrow(Transform parent, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var t = AddText(parent, text.ToUpperInvariant(), 14f, _labelFont);
            t.characterSpacing = 14f;
            t.color = Tint(UIColorToken.NeutralLightest);
        }

        void AddTitle(Transform parent, string text)
        {
            var t = AddText(parent, text, 30f, _titleFont);
            t.color = Tint(UIColorToken.TextLight);
        }

        void AddBody(Transform parent, string text)
        {
            var t = AddText(parent, text, 18f, _labelFont);
            t.lineSpacing = 12f;
            t.color = Tint(UIColorToken.NeutralLightest);
        }

        TMP_Text AddNote(Transform parent, string text)
        {
            var t = AddText(parent, text, 16f, _labelFont);
            t.color = Tint(UIColorToken.NeutralLightest);
            return t;
        }

        static TMP_Text AddText(Transform parent, string text, float size, TMP_FontAsset font)
        {
            var r = NewRect("Text", parent);
            var tmp = r.gameObject.AddComponent<TextMeshProUGUI>();
            if (font) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.raycastTarget = false;
            return tmp;
        }

        TMP_InputField AddInputField(Transform parent, string placeholder)
        {
            var r = NewRect("BirthYear", parent);
            var element = r.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 220f;
            element.minHeight = 58f;
            element.preferredHeight = 58f;

            var bg = NewImage(r, _config != null ? _config.ButtonFillSprite : null, Tint(UIColorToken.SurfaceBlack));
            AddLayer(r, "Border", _config != null ? _config.ButtonBorderSprite : null, Tint(UIColorToken.ControlActive));

            var textArea = NewRect("TextArea", r);
            Stretch(textArea);
            textArea.offsetMin = new Vector2(18f, 8f);
            textArea.offsetMax = new Vector2(-18f, -8f);
            textArea.gameObject.AddComponent<RectMask2D>();

            var textR = NewRect("Text", textArea);
            Stretch(textR);
            var text = textR.gameObject.AddComponent<TextMeshProUGUI>();
            if (_titleFont) text.font = _titleFont;
            text.fontSize = 24f;
            text.color = Tint(UIColorToken.TextLight);
            text.alignment = TextAlignmentOptions.Left;

            var phR = NewRect("Placeholder", textArea);
            Stretch(phR);
            var ph = phR.gameObject.AddComponent<TextMeshProUGUI>();
            if (_titleFont) ph.font = _titleFont;
            ph.text = placeholder;
            ph.fontSize = 24f;
            ph.color = Tint(UIColorToken.SurfaceLight);
            ph.alignment = TextAlignmentOptions.Left;

            var field = r.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = bg;
            field.textViewport = textArea;
            field.textComponent = text;
            field.placeholder = ph;
            field.characterLimit = 4;
            field.contentType = TMP_InputField.ContentType.IntegerNumber;
            return field;
        }

        /// <summary>
        /// A kit-styled button: slivered fill and border, a keycap naming its pad button, an upper-case
        /// label. The primary is the Play style (Cta fill, dark text); the secondary the Cancel style
        /// (Neutral border, light text). A focus ring shows while it is selected, so the pad can see
        /// where A will land.
        /// </summary>
        Button AddButton(Transform parent, string label, string keycap, bool primary, Action onClick)
        {
            var r = NewRect($"Button_{label}", parent);
            var element = r.gameObject.AddComponent<LayoutElement>();
            element.minWidth = 200f;
            element.preferredHeight = 56f;

            var content = primary ? Tint(UIColorToken.SurfaceBlack) : Tint(UIColorToken.TextLight);
            var fill = NewImage(r, _config != null ? _config.ButtonFillSprite : null,
                                primary ? Tint(UIColorToken.Cta) : Tint(UIColorToken.SurfaceVeryDark));
            AddLayer(r, "Border", _config != null ? _config.ButtonBorderSprite : null,
                     primary ? Tint(UIColorToken.Cta) : Tint(UIColorToken.NeutralLightest));
            var ring = AddLayer(r, "FocusRing", _config != null ? _config.FocusRingSprite : null, Tint(UIColorToken.TextLight), 6f);

            var layout = r.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 0, 0);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // No keycap where controls are not named (a phone or tablet with no pad).
            var cap = NewRect("Keycap", r);
            cap.gameObject.SetActive(KitInputDevice.ShowsControlHints && !string.IsNullOrEmpty(keycap));
            var capElement = cap.gameObject.AddComponent<LayoutElement>();
            capElement.preferredWidth = 26f;
            capElement.preferredHeight = 24f;
            var capBorder = NewImage(cap, _config != null ? _config.ButtonBorderSprite : null, new Color(content.r, content.g, content.b, 0.75f));
            capBorder.raycastTarget = false;
            var capText = AddText(cap, keycap, 13f, _labelFont);
            Stretch((RectTransform)capText.transform);
            capText.alignment = TextAlignmentOptions.Center;
            capText.color = content;

            var tmp = AddText(r, label.ToUpperInvariant(), 18f, _labelFont);
            tmp.characterSpacing = 8f;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = content;

            var button = r.gameObject.AddComponent<Button>();
            // The ring is the tint target: clear at rest, lit when selected or hovered, dimmer pressed.
            button.targetGraphic = ring;
            var colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            fill.raycastTarget = true;
            button.onClick.AddListener(() => onClick?.Invoke());
            return button;
        }

        void AddLinkButton(Transform parent, string label, Action onClick)
        {
            var r = NewRect($"Link_{label}", parent);
            var element = r.gameObject.AddComponent<LayoutElement>();
            element.minHeight = 32f;
            element.preferredHeight = 32f;

            var image = r.gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f); // click target only

            var tmp = AddText(r, label, 15f, _labelFont);
            Stretch((RectTransform)tmp.transform);
            tmp.fontStyle = FontStyles.Underline;
            tmp.characterSpacing = 4f;
            tmp.color = Tint(UIColorToken.ControlActive);
            tmp.alignment = TextAlignmentOptions.Left;

            var button = r.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick?.Invoke());
        }

        #endregion
    }
}
