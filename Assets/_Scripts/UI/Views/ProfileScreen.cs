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
        [Tooltip("The Episodes card; its press asks EpisodeScreen, which refuses while episodes are closed.")]
        [SerializeField] private Button episodesButton;
        [Tooltip("The (inactive) Episodes panel.")]
        [SerializeField] private EpisodeScreen episodes;

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
            if (episodesButton) episodesButton.onClick.AddListener(OpenEpisodes);
        }

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
        }

        public void OnScreenExit() { }

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
            if (flightTimeText) flightTimeText.text = FormatFlightTime(life.TotalFlightTimeSeconds);
        }

        /// <summary>"6h 12m", "12m", "0m" - whole minutes, hours once there are any.</summary>
        public static string FormatFlightTime(float seconds)
        {
            int minutes = Mathf.Max(0, Mathf.FloorToInt(seconds / 60f));
            int hours = minutes / 60;
            return hours > 0 ? $"{hours}h {minutes % 60}m" : $"{minutes}m";
        }
    }
}
