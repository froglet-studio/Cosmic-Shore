using CosmicShore.Core;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Settings/Privacy controls: an analytics opt-out toggle, a "Delete my data"
    /// button (right to erasure), and a privacy-policy link. Lets the player change
    /// the choice they made in the first-run consent dialog at any time.
    ///
    /// Drop on the Settings/Privacy panel and wire the references. The toggle
    /// reflects and writes <see cref="AnalyticsServiceFacade"/> consent; deletion
    /// routes through UGS's RequestDataDeletion.
    /// </summary>
    public class AnalyticsPrivacySettingsController : MonoBehaviour
    {
        [Inject] AnalyticsServiceFacade _analytics;

        [Header("Controls")]
        [Tooltip("Reflects current consent; toggling it grants/revokes collection.")]
        [SerializeField] Toggle consentToggle;
        [Tooltip("Requests deletion of the player's analytics data and revokes consent.")]
        [SerializeField] Button deleteDataButton;
        [Tooltip("Opens the hosted privacy policy.")]
        [SerializeField] Button privacyPolicyButton;

        [SerializeField] string privacyPolicyUrl = "";

        void OnEnable()
        {
            if (_analytics == null)
            {
                Debug.LogWarning("[AnalyticsPrivacySettings] AnalyticsServiceFacade not injected.");
                return;
            }

            if (consentToggle != null)
            {
                // Reflect current state without firing the change handler.
                consentToggle.SetIsOnWithoutNotify(_analytics.ConsentGranted);
                // Under-13 players can't enable collection - lock the toggle off.
                consentToggle.interactable = _analytics.AgeEligible;
                consentToggle.onValueChanged.AddListener(OnConsentToggled);
            }

            if (deleteDataButton != null)
                deleteDataButton.onClick.AddListener(OnDeleteDataClicked);

            if (privacyPolicyButton != null)
                privacyPolicyButton.onClick.AddListener(OpenPrivacyPolicy);
        }

        void OnDisable()
        {
            if (consentToggle != null)
                consentToggle.onValueChanged.RemoveListener(OnConsentToggled);
            if (deleteDataButton != null)
                deleteDataButton.onClick.RemoveListener(OnDeleteDataClicked);
            if (privacyPolicyButton != null)
                privacyPolicyButton.onClick.RemoveListener(OpenPrivacyPolicy);
        }

        void OnConsentToggled(bool granted) => _analytics?.SetConsent(granted);

        void OnDeleteDataClicked()
        {
            _analytics?.RequestDataDeletion();
            // Deletion revokes consent - reflect that in the toggle.
            if (consentToggle != null)
                consentToggle.SetIsOnWithoutNotify(false);
        }

        public void OpenPrivacyPolicy()
        {
            if (string.IsNullOrEmpty(privacyPolicyUrl))
            {
                Debug.LogWarning("[AnalyticsPrivacySettings] No privacy policy URL set.");
                return;
            }
            Application.OpenURL(privacyPolicyUrl);
        }
    }
}
