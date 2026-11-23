using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Steam sign-in on PC (fair play, Threat Board T1; Docs/AntiCheat/README.md §4.1). Loaded from
    /// <c>Resources/SteamConfig</c>. An app id of 0 switches Steam off entirely: the game never starts the
    /// Steam API and signs in anonymously, exactly as before.
    /// </summary>
    [CreateAssetMenu(fileName = "SteamConfig", menuName = "ScriptableObjects/Fair Play/Steam Config")]
    public class SteamConfigSO : ScriptableObject
    {
        public const string ResourcePath = "SteamConfig";

        [Header("Steam")]
        [Tooltip("The game's Steam app id (Steamworks partner site). 0 = Steam off. " +
                 "Docs/STEAM_BUSINESS_SETUP.md item A2 owns the real value.")]
        [SerializeField] uint appId;

        [Tooltip("Sign in to UGS with the player's Steam account. Turn on only after the Steam provider is " +
                 "enabled in the UGS dashboard (Tools/UGS/README.md); until then every sign-in falls back to " +
                 "anonymous after a refused Steam attempt.")]
        [SerializeField] bool signInWithSteam;

        [Tooltip("The identity the Web API ticket is issued for. Must match the UGS Steam provider. " +
                 "5-30 letters or digits.")]
        [SerializeField] string webApiIdentity = "unityauthenticationservice";

        [Tooltip("How long to wait for Steam to issue a ticket before signing in without it.")]
        [SerializeField, Min(1f)] float ticketTimeoutSeconds = 10f;

        public uint AppId => appId;
        public bool SignInWithSteam => signInWithSteam && appId != 0;
        public string WebApiIdentity => webApiIdentity ?? string.Empty;
        public float TicketTimeoutSeconds => ticketTimeoutSeconds;

        static SteamConfigSO s_loaded;

        public static SteamConfigSO LoadOrDefault()
        {
            if (s_loaded) return s_loaded;
            s_loaded = Resources.Load<SteamConfigSO>(ResourcePath);
            if (!s_loaded) s_loaded = CreateInstance<SteamConfigSO>();
            return s_loaded;
        }
    }
}
