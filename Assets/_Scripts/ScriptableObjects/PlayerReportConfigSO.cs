using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Report and Block on a player's profile popup (fair play, Threat Board T8; Docs/AntiCheat/README.md §4.4).
    /// Every word the player reads is here, and so is the anti-spam rule. Loaded from
    /// <c>Resources/PlayerReportConfig</c>; an empty word shows nothing.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerReportConfig", menuName = "ScriptableObjects/Fair Play/Player Report Config")]
    public class PlayerReportConfigSO : ScriptableObject
    {
        public const string ResourcePath = "PlayerReportConfig";

        [Header("Anti-spam")]
        [Tooltip("One report per reported player in this many seconds, per session. Reports past it are not sent.")]
        [SerializeField, Min(0f)] float reportCooldownSeconds = 600f;

        [Header("Buttons")]
        [SerializeField] string reportWord = "Report";
        [Tooltip("Shown on Report once a report has been sent.")]
        [SerializeField] string reportedWord = "Reported";
        [Tooltip("Shown on Report when the report could not be sent.")]
        [SerializeField] string reportFailedWord = "Try again";
        [SerializeField] string blockWord = "Block";
        [SerializeField] string blockedWord = "Blocked";

        [Header("Reasons (shown after Report is pressed)")]
        [SerializeField] string cheatingWord = "Cheating";
        [SerializeField] string nameWord = "Offensive name";
        [SerializeField] string trollingWord = "Trolling";

        public float ReportCooldownSeconds => reportCooldownSeconds;
        public string ReportWord => reportWord ?? string.Empty;
        public string ReportedWord => reportedWord ?? string.Empty;
        public string ReportFailedWord => reportFailedWord ?? string.Empty;
        public string BlockWord => blockWord ?? string.Empty;
        public string BlockedWord => blockedWord ?? string.Empty;
        public string CheatingWord => cheatingWord ?? string.Empty;
        public string NameWord => nameWord ?? string.Empty;
        public string TrollingWord => trollingWord ?? string.Empty;

        static PlayerReportConfigSO s_loaded;

        public static PlayerReportConfigSO LoadOrDefault()
        {
            if (s_loaded) return s_loaded;
            s_loaded = Resources.Load<PlayerReportConfigSO>(ResourcePath);
            if (!s_loaded) s_loaded = CreateInstance<PlayerReportConfigSO>();
            return s_loaded;
        }
    }
}
