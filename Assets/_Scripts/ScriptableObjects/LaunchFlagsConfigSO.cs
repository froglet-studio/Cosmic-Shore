using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Every word of the update screens, and how long a saved flags answer stays good, for the live flags
    /// (Docs/LAUNCH/LIVE_FLAGS.md). Loaded from <c>Resources/LaunchFlagsConfig</c>. Templates take tokens:
    /// <c>{current}</c> (this build's version), <c>{minimum}</c> and <c>{recommended}</c> (the flags' versions).
    /// An empty field shows nothing.
    /// </summary>
    [CreateAssetMenu(fileName = "LaunchFlagsConfig", menuName = "ScriptableObjects/Live Ops/Launch Flags Config")]
    public class LaunchFlagsConfigSO : ScriptableObject
    {
        public const string ResourcePath = "LaunchFlagsConfig";

        [Header("Saved answer")]
        [Tooltip("How long the last server answer saved on this device keeps its kill switches in force when the server cannot be reached. After that the safe default applies (nothing killed). A saved answer never shows the update-required wall; only this session's answer can.")]
        [SerializeField, Min(0f)] float cacheMaxAgeHours = 72f;

        [Header("Update required (blocking)")]
        [SerializeField] string requiredEyebrow = "Update";
        [SerializeField] string requiredTitle = "An update is needed";
        [Tooltip("Shown in the danger colour: why the game cannot go on. {current} = this build, {minimum} = the oldest build that plays.")]
        [SerializeField, TextArea] string requiredReason = "Version {current} can no longer play online.";
        [SerializeField, TextArea] string requiredBody = "Update to version {minimum} or newer to keep playing with everyone else.";

        [Header("Update available (dismissible)")]
        [SerializeField] string recommendedEyebrow = "Update";
        [SerializeField] string recommendedTitle = "An update is available";
        [Tooltip("{current} = this build, {recommended} = the build we recommend.")]
        [SerializeField, TextArea] string recommendedBody = "Version {recommended} is out. You have {current}.";

        [Header("Message from the team")]
        [Tooltip("The eyebrow above a message the flags carry (its title and body come from the flags).")]
        [SerializeField] string messageEyebrow = "News";

        [Header("Buttons")]
        [SerializeField] string updateButton = "Update";
        [SerializeField] string laterButton = "Later";
        [SerializeField] string closeButton = "OK";
        [Tooltip("Desktop only: closes the game from the update-required screen. Empty hides it.")]
        [SerializeField] string quitButton = "Quit";
        [SerializeField] string primaryKeycap = "A";
        [SerializeField] string secondaryKeycap = "B";

        public float CacheMaxAgeHours => cacheMaxAgeHours;
        public string RequiredEyebrow => requiredEyebrow ?? string.Empty;
        public string RequiredTitle => requiredTitle ?? string.Empty;
        public string RequiredReason => requiredReason ?? string.Empty;
        public string RequiredBody => requiredBody ?? string.Empty;
        public string RecommendedEyebrow => recommendedEyebrow ?? string.Empty;
        public string RecommendedTitle => recommendedTitle ?? string.Empty;
        public string RecommendedBody => recommendedBody ?? string.Empty;
        public string MessageEyebrow => messageEyebrow ?? string.Empty;
        public string UpdateButton => updateButton ?? string.Empty;
        public string LaterButton => laterButton ?? string.Empty;
        public string CloseButton => closeButton ?? string.Empty;
        public string QuitButton => quitButton ?? string.Empty;
        public string PrimaryKeycap => primaryKeycap ?? string.Empty;
        public string SecondaryKeycap => secondaryKeycap ?? string.Empty;

        static LaunchFlagsConfigSO s_loaded;

        public static LaunchFlagsConfigSO LoadOrDefault()
        {
            if (s_loaded) return s_loaded;
            s_loaded = Resources.Load<LaunchFlagsConfigSO>(ResourcePath);
            if (!s_loaded) s_loaded = CreateInstance<LaunchFlagsConfigSO>();
            return s_loaded;
        }
    }
}
