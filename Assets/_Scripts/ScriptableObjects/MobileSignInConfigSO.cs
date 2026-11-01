using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Platform sign-in on phones (fair play, Threat Board T1; Docs/AntiCheat/README.md §4.1): Game Center on iOS,
    /// Google Play Games on Android.
    /// The PC twin is <see cref="SteamConfigSO"/>. Loaded from <c>Resources/MobileSignInConfig</c>. Off by default:
    /// with it off, a phone signs in anonymously exactly as before.
    /// </summary>
    [CreateAssetMenu(fileName = "MobileSignInConfig", menuName = "ScriptableObjects/Fair Play/Mobile Sign-In Config")]
    public class MobileSignInConfigSO : ScriptableObject
    {
        public const string ResourcePath = "MobileSignInConfig";

        [Header("Game Center (iOS)")]
        [Tooltip("Sign in to UGS with the player's Game Center account. Turn on only after the Apple Game Center " +
                 "provider is enabled in the UGS dashboard (Tools/UGS/README.md); until then every sign-in falls " +
                 "back to anonymous after a refused attempt.")]
        [SerializeField] bool signInWithGameCenter;

        [Tooltip("How long to wait for Game Center to answer before signing in without it.")]
        [SerializeField, Min(1f)] float gameCenterTimeoutSeconds = 10f;

        [Header("Google Play Games (Android)")]
        [Tooltip("Sign in to UGS with the player's Play Games account. Turn on only after Play Games Services is set " +
                 "up in the Play Console and the Google Play Games provider is enabled in the UGS dashboard " +
                 "(Tools/UGS/README.md). Needs both ids below.")]
        [SerializeField] bool signInWithPlayGames;

        [Tooltip("Play Console > Play Games Services > Configuration: the project ID (digits). The Android build writes " +
                 "it into the app's manifest while the switch is on (PlayGamesAppId).")]
        [SerializeField] string playGamesProjectId = "";

        [Tooltip("The OAuth web client ID (…apps.googleusercontent.com) of the Play Games Services game server " +
                 "credential; the same one the UGS Google Play Games provider is set up with.")]
        [SerializeField] string playGamesWebClientId = "";

        [Tooltip("How long to wait for Play Games to answer before signing in without it.")]
        [SerializeField, Min(1f)] float playGamesTimeoutSeconds = 10f;

        public bool SignInWithGameCenter => signInWithGameCenter;
        public float GameCenterTimeoutSeconds => gameCenterTimeoutSeconds;

        public bool SignInWithPlayGames => signInWithPlayGames && IsProjectId(playGamesProjectId) &&
                                           !string.IsNullOrWhiteSpace(playGamesWebClientId);
        public string PlayGamesProjectId => (playGamesProjectId ?? string.Empty).Trim();
        public string PlayGamesWebClientId => (playGamesWebClientId ?? string.Empty).Trim();
        public float PlayGamesTimeoutSeconds => playGamesTimeoutSeconds;

        public static bool IsProjectId(string id)
        {
            id = id?.Trim();
            if (string.IsNullOrEmpty(id)) return false;
            foreach (var c in id)
                if (c < '0' || c > '9') return false;
            return true;
        }

        static MobileSignInConfigSO s_loaded;

        public static MobileSignInConfigSO LoadOrDefault()
        {
            if (s_loaded) return s_loaded;
            s_loaded = Resources.Load<MobileSignInConfigSO>(ResourcePath);
            if (!s_loaded) s_loaded = CreateInstance<MobileSignInConfigSO>();
            return s_loaded;
        }
    }
}
