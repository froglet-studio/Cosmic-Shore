using System;
using System.Collections.Generic;
using CosmicShore.Core;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// First-run privacy flow: a COPPA age gate followed by an opt-in analytics
    /// consent dialog. Drop this on a full-screen overlay panel in an early scene
    /// (Authentication or first Menu_Main entry) and wire the references below.
    ///
    /// Opt-in + safe by default: collection stays OFF until an age-eligible player
    /// explicitly taps Accept (see <see cref="AnalyticsServiceFacade"/>). If this
    /// panel is never built/wired, nothing is collected - the fail-safe is silence.
    ///
    /// Not a consent wall: "Decline" / "Under 13" both dismiss the panel and let
    /// the player continue - they just leave analytics off (legally required).
    /// </summary>
    public class PrivacyConsentController : MonoBehaviour
    {
        [Inject] AnalyticsServiceFacade _analytics;

        [Header("Root")]
        [Tooltip("Overlay root toggled on while the privacy flow is active. Defaults to this GameObject.")]
        [SerializeField] GameObject root;

        [Header("Steps")]
        [SerializeField] GameObject ageGatePanel;
        [SerializeField] GameObject consentPanel;

        [Header("Age gate (neutral - preferred)")]
        [Tooltip("Optional. If assigned, populated with birth years; the submit button reads the selection.")]
        [SerializeField] TMP_Dropdown birthYearDropdown;
        [SerializeField] Button birthYearSubmitButton;

        [Header("Age gate (simple fallback)")]
        [Tooltip("Optional two-button age gate. Prefer the neutral birth-year picker for COPPA.")]
        [SerializeField] Button ageEligibleButton;   // "I'm 13 or older"
        [SerializeField] Button ageUnder13Button;     // "Under 13"

        [Header("Consent")]
        [SerializeField] Button acceptButton;
        [SerializeField] Button declineButton;

        [Header("Privacy policy")]
        [SerializeField] Button privacyPolicyButton;
        [Tooltip("Public URL of the hosted privacy policy. Required for the link button.")]
        [SerializeField] string privacyPolicyUrl = "";

        /// <summary>Raised once the player has resolved the whole privacy flow (any outcome).</summary>
        public event Action OnPrivacyFlowCompleted;

        void Start()
        {
            if (root == null)
                root = gameObject;

            WireButtons();
            PopulateBirthYears();

            if (_analytics == null)
            {
                // No facade injected (e.g. scene missing a ContainerScope). Stay hidden;
                // the facade's opt-in gate keeps collection off regardless.
                Debug.LogWarning("[PrivacyConsent] AnalyticsServiceFacade not injected - hiding privacy flow.");
                root.SetActive(false);
                return;
            }

            if (!_analytics.NeedsPrivacyFlow)
            {
                root.SetActive(false);
                return;
            }

            root.SetActive(true);
            if (_analytics.AgeChecked)
                ShowConsent();
            else
                ShowAgeGate();
        }

        void WireButtons()
        {
            if (birthYearSubmitButton != null) birthYearSubmitButton.onClick.AddListener(OnBirthYearSubmitted);
            if (ageEligibleButton != null)     ageEligibleButton.onClick.AddListener(OnAgeEligible);
            if (ageUnder13Button != null)      ageUnder13Button.onClick.AddListener(OnAgeUnder13);
            if (acceptButton != null)          acceptButton.onClick.AddListener(OnAccept);
            if (declineButton != null)         declineButton.onClick.AddListener(OnDecline);
            if (privacyPolicyButton != null)   privacyPolicyButton.onClick.AddListener(OpenPrivacyPolicy);
        }

        void PopulateBirthYears()
        {
            if (birthYearDropdown == null) return;

            int currentYear = DateTime.UtcNow.Year;
            var years = new List<string>();
            // Most recent first so the picker doesn't default-bias toward "old enough".
            for (int y = currentYear; y >= currentYear - 100; y--)
                years.Add(y.ToString());

            birthYearDropdown.ClearOptions();
            birthYearDropdown.AddOptions(years);
        }

        void ShowAgeGate()
        {
            if (ageGatePanel != null) ageGatePanel.SetActive(true);
            if (consentPanel != null) consentPanel.SetActive(false);
        }

        void ShowConsent()
        {
            if (ageGatePanel != null) ageGatePanel.SetActive(false);
            if (consentPanel != null) consentPanel.SetActive(true);
        }

        // -- Age gate --

        public void OnBirthYearSubmitted()
        {
            if (birthYearDropdown == null) return;
            if (int.TryParse(birthYearDropdown.options[birthYearDropdown.value].text, out int year))
                _analytics.SubmitBirthYear(year);

            AfterAgeAnswered();
        }

        public void OnAgeEligible()
        {
            _analytics.SetAgeEligible(true);
            AfterAgeAnswered();
        }

        public void OnAgeUnder13()
        {
            _analytics.SetAgeEligible(false);
            AfterAgeAnswered();
        }

        void AfterAgeAnswered()
        {
            // Eligible players continue to the consent step; under-13 is done (no collection).
            if (_analytics.AgeEligible)
                ShowConsent();
            else
                Finish();
        }

        // -- Consent --

        public void OnAccept()
        {
            _analytics.SetConsent(true);
            Finish();
        }

        public void OnDecline()
        {
            _analytics.SetConsent(false);
            Finish();
        }

        // -- Shared --

        public void OpenPrivacyPolicy()
        {
            if (string.IsNullOrEmpty(privacyPolicyUrl))
            {
                Debug.LogWarning("[PrivacyConsent] No privacy policy URL set.");
                return;
            }
            Application.OpenURL(privacyPolicyUrl);
        }

        void Finish()
        {
            root.SetActive(false);
            OnPrivacyFlowCompleted?.Invoke();
        }
    }
}
