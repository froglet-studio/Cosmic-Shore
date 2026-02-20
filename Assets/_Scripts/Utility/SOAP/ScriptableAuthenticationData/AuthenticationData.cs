using Obvious.Soap;

namespace CosmicShore.Utilities
{
    [System.Serializable]
    public class AuthenticationData
    {
        public enum AuthState
        {
            NotInitialized, Initializing, Ready, SigningIn, SignedIn, Failed
        }
        
        public string PlayerId { get; set; } = string.Empty;
        public bool IsSignedIn { get; set; } = false;
        public AuthState State { get; set; } = AuthState.NotInitialized;

        public ScriptableEventNoParam OnSignedIn;
        public ScriptableEventNoParam OnSignedOut;
        public ScriptableEventNoParam OnSignInFailed;
    }
}
