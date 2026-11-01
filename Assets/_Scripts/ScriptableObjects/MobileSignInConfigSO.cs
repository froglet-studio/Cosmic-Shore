using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Platform sign-in on phones (fair play, Threat Board T1; Docs/AntiCheat/README.md §4.1): Game Center on iOS.
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

        public bool SignInWithGameCenter => signInWithGameCenter;
        public float GameCenterTimeoutSeconds => gameCenterTimeoutSeconds;

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
