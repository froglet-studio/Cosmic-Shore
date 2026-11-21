using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The "Welcome back, pilot" card on the Bootstrap splash (Docs/MENU_REDESIGN_B.md §58; the owner, 2026-10-18:
    /// "During loading profile creating session in the bootstrap scene I should see the welcome pilot popup"). It reads
    /// the boot status channel the splash already listens to: once the player is signed in, every status line
    /// ("Loading profile...", "Joining lobby...", "Creating session...", "Host ready...") shows the card with the pilot's
    /// name and that line; Hide or Retry puts it away. Before sign-in ("Connecting...", "Signing in...") it stays hidden.
    /// The same look as the sign-in window's signed-in face (SignInChooser AUTO).
    /// </summary>
    public class BootWelcomeCard : MonoBehaviour
    {
        [Header("Inbound")]
        [SerializeField] ScriptableEventBootStatusRequest requestEvent;
        [SerializeField] AuthenticationDataVariable authData;

        [Header("UI")]
        [SerializeField] CanvasGroup group;
        [SerializeField] TMP_Text eyebrowText;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text lineText;

        [Header("Words (empty shows nothing)")]
        [Tooltip("The eyebrow when the pilot has a name.")]
        [SerializeField] string welcomeBackLine = "Welcome back, pilot";
        [Tooltip("The eyebrow before the pilot has picked a name.")]
        [SerializeField] string welcomeLine = "Welcome, pilot";

        bool _shown;

        void Awake() => SetShown(false);

        void OnEnable() => requestEvent.OnRaised += HandleRequest;

        void OnDisable() => requestEvent.OnRaised -= HandleRequest;

        void HandleRequest(BootStatusRequest req)
        {
            bool signedIn = authData && authData.Value != null && authData.Value.IsSignedIn;
            bool show = req.Mode == BootStatusMode.Status && signedIn;
            if (show)
            {
                string pilot = PilotName();
                Set(eyebrowText, string.IsNullOrEmpty(pilot) ? welcomeLine : welcomeBackLine);
                Set(nameText, pilot);
                Set(lineText, req.Text);
            }
            if (show && !_shown) KitEntrance.ReplayUnder(transform);
            SetShown(show);
        }

        string PilotName()
        {
            string name = PlayerDataService.Instance?.CurrentProfile?.Identity.DisplayName;
            if (!string.IsNullOrEmpty(name)) return name;
            var userName = authData && authData.Value != null ? authData.Value.UserName : null;
            return userName ? userName.Value : string.Empty;
        }

        void SetShown(bool on)
        {
            _shown = on;
            KitVisibility.SetShown(group, on);
        }

        static void Set(TMP_Text t, string s)
        {
            if (t) t.text = s ?? string.Empty;
        }
    }
}
