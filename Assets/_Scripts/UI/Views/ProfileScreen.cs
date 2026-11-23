using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    public class ProfileScreen : MonoBehaviour, IScreen
    {
        [SerializeField] private TMP_Text displayNameText;
        [SerializeField] private Image avatarImage;

        [Header("Pilot record (optional)")]
        [Tooltip("Games completed (PlayerProfileData.Lifecycle.GamesCompleted).")]
        [SerializeField] private TMP_Text gamesCompletedText;
        [Tooltip("Total flight time, \"6h 12m\" (Lifecycle.TotalFlightTimeSeconds).")]
        [SerializeField] private TMP_Text flightTimeText;
        [Tooltip("Sessions played (Lifecycle.SessionCount).")]
        [SerializeField] private TMP_Text sessionsText;

        [Header("Actions (optional)")]
        [Tooltip("The profile window (Avatar / Display name tabs).")]
        [SerializeField] private ProfileIconSelectView profileWindow;
        [Tooltip("Opens the window on the Avatar tab: the avatar itself and Change avatar.")]
        [SerializeField] private Button[] avatarButtons = { };
        [Tooltip("Opens the window on the Display name tab.")]
        [SerializeField] private Button renameButton;
        [Tooltip("The pilot's name on the card: pressing it opens the name editor, as RENAME does (Rev 13).")]
        [SerializeField] private Button nameButton;
        [Tooltip("The Episodes card; its press asks EpisodeScreen, which refuses while episodes are closed.")]
        [SerializeField] private Button episodesButton;
        [Tooltip("The (inactive) Episodes panel.")]
        [SerializeField] private EpisodeScreen episodes;
        [Tooltip("Legacy (Rev 10): the pilot card's expand button. Empty since Rev 13, where the record IS the profile.")]
        [SerializeField] private Button recordButton;
        [Tooltip("The pilot record (favourites, fly time by vessel, Audio Studio). Since Rev 13 it shows whenever this " +
                 "screen is on (Docs/MENU_REDESIGN_B.md §69).")]
        [SerializeField] private ProfileRecordWindow recordWindow;

        [Header("Back (Rev 13)")]
        [Tooltip("The profile's Back (B on a pad): closes the pilot picker if it is open, else returns to Home.")]
        [SerializeField] private Button backButton;
        [Tooltip("The scene's switcher, which Back returns through. Found in the scene when left empty.")]
        [SerializeField] private ScreenSwitcher screenSwitcher;

        [Header("Words (empty shows nothing)")]
        [Tooltip("Flight time with hours. {h} hours, {m} minutes.")]
        [SerializeField] private string hoursTemplate = "{h}h {m}m";
        [Tooltip("Flight time under an hour. {m} minutes.")]
        [SerializeField] private string minutesTemplate = "{m}m";

        // Use the persistent singleton rather than [Inject]: it is reliably available from
        // bootstrap (Awake) and avoids both the DI-injection timing gap (OnEnable fires before
        // [Inject] fields are populated) and a NullReferenceException if injection ever fails.
        PlayerDataService Service => PlayerDataService.Instance;

        void Start()
        {
            if (displayNameText == null)
                displayNameText = GetComponentInChildren<TMP_Text>();
        }

        // Wired in code, not as persistent calls: the buttons live in the generated ProfileView
        // prefab, which cannot name the scene's window, and this component already knows both.
        void Awake()
        {
            foreach (var b in avatarButtons)
                if (b) b.onClick.AddListener(OpenAvatar);
            if (renameButton) renameButton.onClick.AddListener(OpenName);
            if (nameButton) nameButton.onClick.AddListener(OpenName);
            if (episodesButton) episodesButton.onClick.AddListener(OpenEpisodes);
            if (recordButton) recordButton.onClick.AddListener(OpenRecord);
            if (backButton) backButton.onClick.AddListener(Back);
        }

        void Back()
        {
            if (recordWindow && recordWindow.ClosePicker()) return;
            if (!screenSwitcher) screenSwitcher = FindAnyObjectByType<ScreenSwitcher>(FindObjectsInactive.Include);
            if (screenSwitcher) screenSwitcher.OnClickProfileBack();
        }

        void OpenRecord() { if (recordWindow) recordWindow.Open(); }

        void OpenAvatar() { if (profileWindow) profileWindow.OpenAvatar(); }
        void OpenName() { if (profileWindow) profileWindow.OpenDisplayName(); }
        void OpenEpisodes() { if (episodes) episodes.ShowPanel(); }

        void OnEnable()
        {
            var service = Service;
            if (service == null)
                return;
            service.OnProfileChanged += Refresh;
            if (service.CurrentProfile != null)
                Refresh(service.CurrentProfile);
        }

        void OnDisable()
        {
            var service = Service;
            if (service != null)
                service.OnProfileChanged -= Refresh;
        }

        // Games and flight time are recorded without an event (RecordGameCompleted /
        // RecordFlightTime), and menu freestyle adds flight time while this screen is merely
        // hidden - so the record is re-read every time the screen comes on.
        public void OnScreenEnter()
        {
            var service = Service;
            if (service != null && service.CurrentProfile != null)
                Refresh(service.CurrentProfile);
            if (recordWindow && !recordButton) recordWindow.Open();
        }

        public void OnScreenExit()
        {
            if (recordWindow && recordWindow.IsOpen) recordWindow.Close();
        }

        void Refresh(PlayerProfileData profile)
        {
            if (profile == null) return;
            if (displayNameText != null)
                displayNameText.text = profile.Identity.DisplayName;
            if (avatarImage != null && Service != null)
            {
                var sprite = Service.GetAvatarSprite(profile.Identity.AvatarId);
                if (sprite != null)
                    avatarImage.sprite = sprite;
            }

            var life = profile.Lifecycle;
            if (life == null) return;
            if (gamesCompletedText) gamesCompletedText.text = life.GamesCompleted.ToString("N0");
            if (sessionsText) sessionsText.text = life.SessionCount.ToString("N0");
            if (flightTimeText) flightTimeText.text = FormatFlightTime(life.TotalFlightTimeSeconds, hoursTemplate, minutesTemplate);
        }

        /// <summary>Whole minutes, hours once there are any, through the authored templates ("{h}h {m}m" / "{m}m").</summary>
        public static string FormatFlightTime(float seconds, string hoursTemplate, string minutesTemplate)
        {
            int minutes = Mathf.Max(0, Mathf.FloorToInt(seconds / 60f));
            int hours = minutes / 60;
            return hours > 0
                ? TemplateText.Fill(hoursTemplate, "h", hours.ToString(), "m", (minutes % 60).ToString())
                : TemplateText.Fill(minutesTemplate, "m", minutes.ToString());
        }
    }
}
