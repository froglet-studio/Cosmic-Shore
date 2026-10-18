using Obvious.Soap;

namespace CosmicShore.ScriptableObjects
{
    [System.Serializable]
    public class AuthenticationData
    {
        public enum AuthState
        {
            NotInitialized = 0, Initializing = 1, Ready = 2, SigningIn = 3, SignedIn = 4, Failed = 5
        }
        
        public string PlayerId { get; set; } = string.Empty;
        public bool IsSignedIn { get; set; } = false;
        public AuthState State { get; set; } = AuthState.NotInitialized;

        public StringVariable UserName;

        public ScriptableEventNoParam OnSignedIn;
        public ScriptableEventNoParam OnSignedOut;
        public ScriptableEventNoParam OnSignInFailed;
    }
}
