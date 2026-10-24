using System;
using CosmicShore.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The sign-in window's three faces (Docs/MENU_REDESIGN_B.md §54, the approved Rev 9 boards; §57 R9b), on the auth panel of
    /// Authentication.unity:
    /// <list type="bullet">
    /// <item>CHOOSE: "Sign in to fly" - the platform's provider buttons in its order (SignInProvidersSO), the "or" rule,
    /// Play as guest (the scene controller's own button), a hint and the controller's status line. Rows the platform does
    /// not show collapse and the window shortens to fit.</item>
    /// <item>PROGRESS: waiting on a provider ("Finish in your browser" for Google on PC, a spinner otherwise), or a
    /// provider that did not finish, or one this build cannot use yet.</item>
    /// <item>AUTO: signed in for you - the silent provider's card (Steam on PC).</item>
    /// </list>
    /// It only draws; <see cref="AuthenticationSceneController"/> runs the sign-in and listens to the events. Every line
    /// is an authored field ({provider} is the provider's authored name).
    /// </summary>
    public class SignInChooser : MonoBehaviour
    {
        [Serializable]
        public struct ProviderButton
        {
            public SignInProvider provider;
            public Button button;
            [Tooltip("The button's face: SignInProvidersSO's button artwork for the provider (the provider's own image, " +
                     "untouched) replaces it when that is set.")]
            public Image mark;
        }

        [Header("Config")]
        [SerializeField] SignInProvidersSO providers;

        [Header("Window")]
        [SerializeField] RectTransform window;
        [Tooltip("Top of the first row under the title, from the window's top edge.")]
        [SerializeField] float stackTop = 152f;
        [SerializeField] float rowGap = 14f;
        [SerializeField] float bottomPadding = 34f;
        [SerializeField] float progressHeight = 330f;
        [SerializeField] float autoHeight = 230f;

        [Header("Choose")]
        [SerializeField] CanvasGroup chooseGroup;
        [SerializeField] ProviderButton[] providerButtons;
        [Tooltip("Above the buttons: shown when this platform's silent provider (Steam on PC) has no button and could not " +
                 "sign the player in, e.g. Steam is not running.")]
        [SerializeField] RectTransform silentNoteRow;
        [SerializeField] TMP_Text silentNoteText;
        [SerializeField] RectTransform orRule;
        [SerializeField] RectTransform guestRow;
        [Tooltip("Rows under Play as guest, in order (the hint, the controller's status line).")]
        [SerializeField] RectTransform[] trailingRows;

        [Header("Progress")]
        [SerializeField] CanvasGroup progressGroup;
        [SerializeField] TMP_Text progressEyebrow;
        [SerializeField] TMP_Text progressTitle;
        [SerializeField] TMP_Text progressBody;
        [SerializeField] TMP_Text progressLine;
        [SerializeField] GameObject spinner;
        [SerializeField] Button primaryButton;
        [SerializeField] TMP_Text primaryLabel;
        [SerializeField] Button secondaryButton;
        [SerializeField] TMP_Text secondaryLabel;
        [SerializeField] Button guestLinkButton;

        [Header("Signed in for you")]
        [SerializeField] CanvasGroup autoGroup;
        [SerializeField] Image autoMark;
        [SerializeField] TMP_Text autoName;
        [SerializeField] TMP_Text autoCaption;
        [SerializeField] TMP_Text autoLine;
        [SerializeField] Button otherWaysButton;

        [Header("Words (empty shows nothing; {provider} is the provider's name)")]
        [SerializeField] string steamName = "Steam";
        [SerializeField] string googleName = "Google";
        [SerializeField] string appleName = "Apple";
        [SerializeField] string waitingEyebrow = "{provider}";
        [SerializeField] string browserTitle = "Finish in your browser";
        [SerializeField] string browserBody = "A {provider} sign-in page opened in your browser. Choose your account there, then come back here.";
        [SerializeField] string browserLine = "Waiting for {provider}...";
        [SerializeField] string signingInTitle = "Signing in";
        [SerializeField] string signingInBody = "Finish signing in with {provider}.";
        [SerializeField] string signingInLine = "Signing in with {provider}...";
        [SerializeField] string failedEyebrow = "Sign-in";
        [SerializeField] string failedTitle = "{provider} sign-in did not finish";
        [SerializeField] string failedBody = "It was cancelled or timed out. Try again, pick another way, or play as a guest.";
        [SerializeField] string unavailableTitle = "{provider} is not ready yet";
        [SerializeField] string unavailableBody = "{provider} sign-in is not in this build yet. Pick another way, or play as a guest.";
        [SerializeField] string tryAgainLabel = "Try again";
        [SerializeField] string openAgainLabel = "Open again";
        [SerializeField] string otherWaysLabel = "Other ways";
        [SerializeField] string cancelLabel = "Cancel";
        [SerializeField] string autoCaptionLine = "{provider} account";
        [SerializeField] string autoSigningInLine = "Signing in with {provider}...";
        [Tooltip("The CHOOSE face's note when the silent provider could not sign you in.")]
        [SerializeField] string silentUnavailableLine = "{provider} is not running. Start {provider} to sign in with it, or pick a way below.";

        /// <summary>A provider button, Try again or Open again was pressed.</summary>
        public event Action<SignInProvider> ProviderChosen;
        /// <summary>Cancel while waiting, or Other ways from the AUTO card: stop the provider's sign-in.</summary>
        public event Action CancelRequested;
        /// <summary>Play as guest from the PROGRESS face.</summary>
        public event Action GuestRequested;

        IPlatformSignIn _service;
        SignInProvider _shown;
        bool _primaryRetries;

        public SignInProvidersSO Providers => providers;

        void Awake()
        {
            foreach (var p in providerButtons)
            {
                if (!p.button) continue;
                var provider = p.provider;
                p.button.onClick.AddListener(() => ProviderChosen?.Invoke(provider));
                var face = providers ? providers.ButtonFor(provider) : null;
                if (p.mark && face) p.mark.sprite = face;
            }
            if (primaryButton) primaryButton.onClick.AddListener(OnPrimary);
            if (secondaryButton) secondaryButton.onClick.AddListener(OnSecondary);
            if (guestLinkButton) guestLinkButton.onClick.AddListener(() => GuestRequested?.Invoke());
            if (otherWaysButton) otherWaysButton.onClick.AddListener(OnOtherWays);
        }

        public void Bind(IPlatformSignIn service) => _service = service;

        /// <summary>The platform's silent provider when this build can use it, else None.</summary>
        public SignInProvider SilentProvider
        {
            get
            {
                var p = providers ? providers.SilentProvider : SignInProvider.None;
                return p != SignInProvider.None && _service != null && _service.IsAvailable(p) ? p : SignInProvider.None;
            }
        }

        public string NameOf(SignInProvider provider) => provider switch
        {
            SignInProvider.Steam => steamName,
            SignInProvider.Google => googleName,
            SignInProvider.Apple => appleName,
            _ => string.Empty,
        };

        // ── CHOOSE ──────────────────────────────────────────────

        /// <summary>Shows the chooser and returns the control the pad should land on (the first provider, else
        /// <paramref name="fallback"/>, the guest button).</summary>
        public GameObject ShowChoose(GameObject fallback)
        {
            SetFace(chooseGroup);
            float y = stackTop;
            GameObject first = null;
            var order = providers ? providers.OrderFor(providers.CurrentGroup) : null;
            int sibling = 0;
            if (silentNoteRow)
            {
                // A silent provider has no button (Steam: R9b), so when it could not sign the player in, say so.
                var silent = providers ? providers.SilentProvider : SignInProvider.None;
                bool note = silent != SignInProvider.None && SilentProvider == SignInProvider.None && providers.Shows(silent, _service);
                silentNoteRow.gameObject.SetActive(note);
                if (note)
                {
                    Set(silentNoteText, TemplateText.Fill(silentUnavailableLine, "provider", NameOf(silent)));
                    y = Place(silentNoteRow, y);
                }
            }
            if (order != null)
            {
                foreach (var provider in order)
                {
                    var b = Find(provider);
                    if (!b.button || !providers.Shows(provider, _service)) continue;
                    var rt = (RectTransform)b.button.transform;
                    b.button.gameObject.SetActive(true);
                    rt.SetSiblingIndex(sibling++);
                    y = Place(rt, y);
                    if (!first) first = b.button.gameObject;
                }
            }
            foreach (var b in providerButtons)
                if (b.button && (order == null || !providers.Shows(b.provider, _service)))
                    b.button.gameObject.SetActive(false);

            if (orRule)
            {
                orRule.gameObject.SetActive(first);
                if (first) y = Place(orRule, y);
            }
            // Play as guest shows on every platform, Steam builds included (the owner, 2026-10-18).
            if (guestRow) y = Place(guestRow, y);
            if (trailingRows != null)
                foreach (var row in trailingRows)
                    if (row) y = Place(row, y);
            SetHeight(y - rowGap + bottomPadding);
            var target = first ? first : fallback;
            Select(target);
            return target;
        }

        // ── PROGRESS ────────────────────────────────────────────

        public void ShowWaiting(SignInProvider provider, bool inBrowser)
        {
            _shown = provider;
            _primaryRetries = true;
            string n = NameOf(provider);
            SetFace(progressGroup);
            SetHeight(progressHeight);
            Set(progressEyebrow, TemplateText.Fill(waitingEyebrow, "provider", n));
            Set(progressTitle, TemplateText.Fill(inBrowser ? browserTitle : signingInTitle, "provider", n));
            Set(progressBody, TemplateText.Fill(inBrowser ? browserBody : signingInBody, "provider", n));
            Set(progressLine, TemplateText.Fill(inBrowser ? browserLine : signingInLine, "provider", n));
            if (spinner) spinner.SetActive(true);
            SetButton(primaryButton, primaryLabel, inBrowser, openAgainLabel);
            SetButton(secondaryButton, secondaryLabel, true, cancelLabel);
            if (guestLinkButton) guestLinkButton.gameObject.SetActive(false);
            Select(secondaryButton ? secondaryButton.gameObject : null);
        }

        /// <summary>A provider's sign-in ended without a session. <see cref="SignInStatus.Unavailable"/> says the build
        /// cannot use it (no Try again); anything else offers Try again.</summary>
        public void ShowFailed(SignInProvider provider, SignInStatus status)
        {
            _shown = provider;
            bool unavailable = status == SignInStatus.Unavailable;
            _primaryRetries = !unavailable;
            string n = NameOf(provider);
            SetFace(progressGroup);
            SetHeight(progressHeight);
            Set(progressEyebrow, failedEyebrow);
            Set(progressTitle, TemplateText.Fill(unavailable ? unavailableTitle : failedTitle, "provider", n));
            Set(progressBody, TemplateText.Fill(unavailable ? unavailableBody : failedBody, "provider", n));
            Set(progressLine, string.Empty);
            if (spinner) spinner.SetActive(false);
            SetButton(primaryButton, primaryLabel, !unavailable, tryAgainLabel);
            SetButton(secondaryButton, secondaryLabel, true, otherWaysLabel);
            if (guestLinkButton) guestLinkButton.gameObject.SetActive(true);
            Select(!unavailable && primaryButton ? primaryButton.gameObject : secondaryButton ? secondaryButton.gameObject : null);
        }

        // ── AUTO ────────────────────────────────────────────────

        /// <summary>The silent provider's card. <paramref name="accountName"/> may be empty until the provider knows it.</summary>
        public void ShowAuto(SignInProvider provider, string accountName)
        {
            _shown = provider;
            string n = NameOf(provider);
            SetFace(autoGroup);
            SetHeight(autoHeight);
            var mark = providers ? providers.MarkFor(provider) : null;
            if (autoMark)
            {
                autoMark.sprite = mark;
                autoMark.enabled = mark;
            }
            Set(autoName, accountName);
            Set(autoCaption, TemplateText.Fill(autoCaptionLine, "provider", n));
            Set(autoLine, TemplateText.Fill(autoSigningInLine, "provider", n));
            Select(null);
        }

        // ── Buttons ─────────────────────────────────────────────

        void OnPrimary()
        {
            if (_primaryRetries) ProviderChosen?.Invoke(_shown);
        }

        void OnSecondary()
        {
            CancelRequested?.Invoke();
            ShowChoose(null);
        }

        void OnOtherWays()
        {
            CancelRequested?.Invoke();
            ShowChoose(null);
        }

        // ── Helpers ─────────────────────────────────────────────

        ProviderButton Find(SignInProvider provider)
        {
            foreach (var b in providerButtons)
                if (b.provider == provider) return b;
            return default;
        }

        float Place(RectTransform rt, float y)
        {
            // Rows are top-left anchored by the generator; only their y moves.
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -y);
            return y + rt.rect.height + rowGap;
        }

        void SetHeight(float h)
        {
            if (window) window.sizeDelta = new Vector2(window.sizeDelta.x, h);
        }

        void SetFace(CanvasGroup shown)
        {
            foreach (var g in new[] { chooseGroup, progressGroup, autoGroup })
            {
                if (!g) continue;
                bool on = g == shown;
                g.alpha = on ? 1f : 0f;
                g.interactable = on;
                g.blocksRaycasts = on;
            }
        }

        static void Set(TMP_Text t, string s)
        {
            if (t) t.text = s;
        }

        static void SetButton(Button b, TMP_Text label, bool shown, string caption)
        {
            if (!b) return;
            b.gameObject.SetActive(shown);
            if (label) label.text = caption;
        }

        static void Select(GameObject target)
        {
            var events = EventSystem.current;
            if (events) events.SetSelectedGameObject(target);
        }

        [ContextMenu("Preview: choose")]
        void PreviewChoose() => ShowChoose(null);

        [ContextMenu("Preview: Google in the browser")]
        void PreviewWaiting() => ShowWaiting(SignInProvider.Google, true);

        [ContextMenu("Preview: Google did not finish")]
        void PreviewFailed() => ShowFailed(SignInProvider.Google, SignInStatus.Cancelled);

        [ContextMenu("Preview: Steam signs you in")]
        void PreviewAuto() => ShowAuto(SignInProvider.Steam, "Shomboss");
    }
}
