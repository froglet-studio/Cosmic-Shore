using System;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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

        void Build()
        {
            EnsureEventSystem();

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            // Scrim: dims the game behind and swallows clicks so nothing underneath reacts.
            var scrim = NewRect("Scrim", transform);
            Stretch(scrim);
            var scrimImage = scrim.gameObject.AddComponent<Image>();
            scrimImage.color = new Color(0.02f, 0.02f, 0.04f, 0.88f);

            _agePanel = BuildAgeGate(scrim).gameObject;
            _consentPanel = BuildConsent(scrim).gameObject;

            // Resume mid-flow: an age-checked player only owes the consent answer.
            bool ageDone = _analytics.AgeChecked;
            _agePanel.SetActive(!ageDone);
            _consentPanel.SetActive(ageDone);
        }

        RectTransform BuildAgeGate(RectTransform parent)
        {
            var panel = NewPanel("AgeGate", parent, 720f, 460f);
            var col = NewColumn(panel);

            AddTitle(col, _config != null ? _config.AgeTitle : "Before you fly");
            AddBody(col, _config != null ? _config.AgeBody : "What year were you born?");

            // A birth-year question rather than "are you 13 or older?" - a yes/no age question
            // nudges the player toward the answer that unlocks collection, which is exactly what
            // COPPA guidance says a neutral age screen must not do.
            _birthYearField = AddInputField(col, "YYYY");
            _ageError = AddNote(col, "");
            _ageError.color = new Color(0.90f, 0.36f, 0.42f);
            _ageError.gameObject.SetActive(false);

            var row = NewRow(col);
            _continueButton = AddButton(row, _config != null ? _config.ContinueLabel : "Continue",
                new Color(0.16f, 0.62f, 0.52f), SubmitBirthYear);

            return panel;
        }

        RectTransform BuildConsent(RectTransform parent)
        {
            var panel = NewPanel("Consent", parent, 860f, 660f);
            var col = NewColumn(panel);

            AddTitle(col, _config != null ? _config.ConsentTitle : "Help us improve Cosmic Shore");
            AddBody(col, _config != null ? _config.ConsentBody : "");

            if (_config != null && _config.HasPrivacyPolicyUrl)
                AddLinkButton(col, _config.PolicyLabel, () => Application.OpenURL(_config.PrivacyPolicyUrl));

            var row = NewRow(col);
            AddButton(row, _config != null ? _config.DeclineLabel : "No thanks",
                new Color(0.28f, 0.30f, 0.35f), Decline);
            AddButton(row, _config != null ? _config.AcceptLabel : "I agree",
                new Color(0.16f, 0.62f, 0.52f), Accept);

            return panel;
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
                _agePanel.SetActive(false);
                _consentPanel.SetActive(true);
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

        void Finish()
        {
            OnPrivacyFlowCompleted?.Invoke();
            Destroy(gameObject);
        }

        #endregion

        #region Tiny UI toolkit

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

        static RectTransform NewPanel(string name, Transform parent, float width, float height)
        {
            var panel = NewRect(name, parent);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(width, height);

            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = new Color(0.09f, 0.10f, 0.13f, 0.99f);
            return panel;
        }

        static RectTransform NewColumn(RectTransform parent)
        {
            var col = NewRect("Column", parent);
            Stretch(col);
            col.offsetMin = new Vector2(48f, 40f);
            col.offsetMax = new Vector2(-48f, -40f);

            var layout = col.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.UpperLeft;
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
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;

            var fitter = row.gameObject.AddComponent<LayoutElement>();
            fitter.minHeight = 64f;
            return row;
        }

        static TMP_Text AddTitle(Transform parent, string text)
        {
            var t = AddText(parent, text, 42f, FontStyles.Bold);
            t.color = new Color(0.94f, 0.95f, 0.97f);
            return t;
        }

        static TMP_Text AddBody(Transform parent, string text)
        {
            var t = AddText(parent, text, 24f, FontStyles.Normal);
            t.color = new Color(0.76f, 0.78f, 0.83f);
            return t;
        }

        static TMP_Text AddNote(Transform parent, string text)
        {
            var t = AddText(parent, text, 20f, FontStyles.Normal);
            t.color = new Color(0.60f, 0.63f, 0.70f);
            return t;
        }

        static TMP_Text AddText(Transform parent, string text, float size, FontStyles style)
        {
            var r = NewRect("Text", parent);
            var tmp = r.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.alignment = TextAlignmentOptions.TopLeft;

            var fitter = r.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return tmp;
        }

        static TMP_InputField AddInputField(Transform parent, string placeholder)
        {
            var r = NewRect("BirthYear", parent);
            var element = r.gameObject.AddComponent<LayoutElement>();
            element.minHeight = 64f;
            element.preferredHeight = 64f;

            var bg = r.gameObject.AddComponent<Image>();
            bg.color = new Color(0.16f, 0.17f, 0.21f);

            var textArea = NewRect("TextArea", r);
            Stretch(textArea);
            textArea.offsetMin = new Vector2(16f, 8f);
            textArea.offsetMax = new Vector2(-16f, -8f);
            textArea.gameObject.AddComponent<RectMask2D>();

            var textR = NewRect("Text", textArea);
            Stretch(textR);
            var text = textR.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 28f;
            text.color = new Color(0.94f, 0.95f, 0.97f);
            text.alignment = TextAlignmentOptions.Left;

            var phR = NewRect("Placeholder", textArea);
            Stretch(phR);
            var ph = phR.gameObject.AddComponent<TextMeshProUGUI>();
            ph.text = placeholder;
            ph.fontSize = 28f;
            ph.color = new Color(0.45f, 0.47f, 0.53f);
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

        static Button AddButton(Transform parent, string label, Color tint, Action onClick)
        {
            var r = NewRect($"Button_{label}", parent);
            r.sizeDelta = new Vector2(240f, 64f);

            var image = r.gameObject.AddComponent<Image>();
            image.color = tint;

            var labelR = NewRect("Label", r);
            Stretch(labelR);
            var tmp = labelR.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 26f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;

            var button = r.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick?.Invoke());
            return button;
        }

        static Button AddLinkButton(Transform parent, string label, Action onClick)
        {
            var r = NewRect($"Link_{label}", parent);
            var element = r.gameObject.AddComponent<LayoutElement>();
            element.minHeight = 40f;
            element.preferredHeight = 40f;

            var image = r.gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f); // click target only

            var labelR = NewRect("Label", r);
            Stretch(labelR);
            var tmp = labelR.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = $"<u>{label}</u>";
            tmp.fontSize = 22f;
            tmp.color = new Color(0.35f, 0.72f, 0.95f);
            tmp.alignment = TextAlignmentOptions.Left;

            var button = r.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick?.Invoke());
            return button;
        }

        #endregion
    }
}
