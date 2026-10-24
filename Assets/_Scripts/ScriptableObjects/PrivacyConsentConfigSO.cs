using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Copy and links for the first-run privacy flow. Asset lives at
    /// <c>Resources/PrivacyConsentConfig</c> so the overlay can load it with no scene wiring.
    ///
    /// The wording is authored data, not code, so legal or product can change what the player is
    /// told without a programmer — which matters because the disclosure has to name the processor
    /// (PostHog, EU) and the categories sent, and that text is a release gate.
    /// </summary>
    [CreateAssetMenu(fileName = "PrivacyConsentConfig", menuName = "ScriptableObjects/Analytics/Privacy Consent Config")]
    public class PrivacyConsentConfigSO : ScriptableObject
    {
        [Header("Links")]
        [Tooltip("Public URL of the hosted privacy policy. Leave EMPTY and the link button is hidden " +
                 "rather than shown broken — but it must be filled before any public release.")]
        [SerializeField] string privacyPolicyUrl = "";

        [Header("Age gate")]
        [TextArea(2, 4)]
        [Tooltip("Neutral age question. Do NOT phrase this so it nudges the player toward being " +
                 "old enough — a neutral birth-year question is the COPPA-safe form.")]
        [SerializeField] string ageTitle = "Before you fly";

        [TextArea(2, 5)]
        [SerializeField] string ageBody = "What year were you born?";

        [Header("Consent")]
        [TextArea(2, 4)]
        [SerializeField] string consentTitle = "Help us improve Cosmic Shore";

        [TextArea(4, 12)]
        [Tooltip("Must name the processor and the categories sent. Consent that does not name the " +
                 "recipient is not informed consent.")]
        [SerializeField] string consentBody =
            "We'd like to collect anonymous gameplay data — which modes you play, how long you fly, " +
            "and how the game performs — to work out what to fix and what to build next.\n\n" +
            "This is processed by Unity Gaming Services and PostHog (EU). It includes your player ID " +
            "and display name. It is never sold, and never used for advertising.\n\n" +
            "You can change this any time in Settings.";

        [Header("Buttons")]
        [SerializeField] string acceptLabel = "I agree";
        [SerializeField] string declineLabel = "No thanks";
        [SerializeField] string continueLabel = "Continue";
        [SerializeField] string policyLabel = "Privacy policy";

        [Header("Style B words (empty = not shown)")]
        [SerializeField] string ageEyebrow = "First flight";
        [SerializeField] string consentEyebrow = "Privacy";
        [Tooltip("The pad button named on the primary button's keycap (Continue, I agree).")]
        [SerializeField] string primaryKeycap = "A";
        [Tooltip("The pad button named on No thanks' keycap (B also declines).")]
        [SerializeField] string secondaryKeycap = "B";

        [Header("Style B surfaces (optional)")]
        [Tooltip("The panel's slivered fill (kit_sliver14_fill). Empty draws a plain rectangle.")]
        [SerializeField] Sprite panelFillSprite;
        [Tooltip("The panel's slivered border (kit_sliver14_line1).")]
        [SerializeField] Sprite panelBorderSprite;
        [Tooltip("A button's slivered fill (kit_sliver10_fill).")]
        [SerializeField] Sprite buttonFillSprite;
        [Tooltip("A button's border and its keycap (kit_sliver10_line1).")]
        [SerializeField] Sprite buttonBorderSprite;
        [Tooltip("The focus ring a selected button wears (kit_sliver10_line2).")]
        [SerializeField] Sprite focusRingSprite;

        public string PrivacyPolicyUrl => privacyPolicyUrl == null ? "" : privacyPolicyUrl.Trim();
        public bool HasPrivacyPolicyUrl => !string.IsNullOrWhiteSpace(PrivacyPolicyUrl);

        public string AgeTitle => ageTitle;
        public string AgeBody => ageBody;
        public string ConsentTitle => consentTitle;
        public string ConsentBody => consentBody;
        public string AcceptLabel => acceptLabel;
        public string DeclineLabel => declineLabel;
        public string ContinueLabel => continueLabel;
        public string PolicyLabel => policyLabel;

        public string AgeEyebrow => ageEyebrow;
        public string ConsentEyebrow => consentEyebrow;
        public string PrimaryKeycap => primaryKeycap;
        public string SecondaryKeycap => secondaryKeycap;
        public Sprite PanelFillSprite => panelFillSprite;
        public Sprite PanelBorderSprite => panelBorderSprite;
        public Sprite ButtonFillSprite => buttonFillSprite;
        public Sprite ButtonBorderSprite => buttonBorderSprite;
        public Sprite FocusRingSprite => focusRingSprite;
    }
}
