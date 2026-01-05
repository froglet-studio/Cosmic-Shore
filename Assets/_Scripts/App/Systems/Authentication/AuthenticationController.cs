using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;

namespace YourGame.Services.Auth
{
    public class AuthenticationController : MonoBehaviour
    {
        public enum AuthState
        {
            NotInitialized,
            Initializing,
            Ready,
            SigningIn,
            SignedIn,
            Failed
        }

        [Header("Startup")]
        [SerializeField] private bool dontDestroyOnLoad = true;
        [SerializeField] private bool autoSignInAnonymously = true;

        [Header("Debug")]
        [SerializeField] private bool verboseLogs = true;

        private AuthState State { get; set; } = AuthState.NotInitialized;

        private bool IsSignedIn => AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn;
        public string PlayerId => IsSignedIn ? AuthenticationService.Instance.PlayerId : string.Empty;
        private string SessionTokenExists => AuthenticationService.Instance != null && AuthenticationService.Instance.SessionTokenExists ? "Yes" : "No";

        public event Action<string> OnSignedIn;           
        public event Action<string> OnSignInFailed;     
        public event Action OnSignedOut;

        bool _startupAttempted;

        void Awake()
        {
            if (dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);
        }

        async void Start()
        {
            try
            {
                if (!autoSignInAnonymously) return;
                if (_startupAttempted) return;

                _startupAttempted = true;

                // Fire-and-forget startup sign-in, but safely.
                try
                {
                    await EnsureInitializedAsync();
                    await EnsureSignedInAnonymouslyAsync();
                }
                catch (Exception ex)
                {
                    State = AuthState.Failed;
                    Log($"Startup auth failed: {ex}");
                    OnSignInFailed?.Invoke(ex.Message);
                }
            }
            catch (Exception e)
            {
                 // TODO handle exception
            }
        }

        // ---------------------------
        // Core public API
        // ---------------------------

        internal async Task EnsureInitializedAsync()
        {
            if (State == AuthState.Ready || State == AuthState.SignedIn) return;
            if (State == AuthState.Initializing) return;

            State = AuthState.Initializing;
            Log("Initializing Unity Services...");

            await UnityServices.InitializeAsync();

            WireAuthEventsOnce();

            State = AuthState.Ready;
            Log("Unity Services initialized.");
        }

        internal async Task EnsureSignedInAnonymouslyAsync()
        {
            await EnsureInitializedAsync();

            if (AuthenticationService.Instance.IsSignedIn)
            {
                State = AuthState.SignedIn;
                Log($"Already signed in. PlayerId={AuthenticationService.Instance.PlayerId}");
                return;
            }

            State = AuthState.SigningIn;
            Log($"Signing in anonymously... (SessionTokenExists={SessionTokenExists})");

            await AuthenticationService.Instance.SignInAnonymouslyAsync();

            State = AuthState.SignedIn;
            Log($"Anonymous sign-in complete. PlayerId={AuthenticationService.Instance.PlayerId}");
            OnSignedIn?.Invoke(AuthenticationService.Instance.PlayerId);
        }

        public void SignOut(bool clearSessionToken = false)
        {
            if (AuthenticationService.Instance == null) return;

            AuthenticationService.Instance.SignOut();
            if (clearSessionToken)
                AuthenticationService.Instance.ClearSessionToken();

            State = AuthState.Ready;
            Log("Signed out.");
            OnSignedOut?.Invoke();
        }

        // ---------------------------
        // Placeholders: other sign-in providers (future)
        // ---------------------------

        // NOTE: These are intentionally "stubs" so your project compiles now.
        // Later, we’ll fill them with real provider flows + tokens.

        public Task SignInWithGoogleAsync(string idToken)
        {
            // TODO:
            // await EnsureInitializedAsync();
            // await AuthenticationService.Instance.SignInWithGoogleAsync(idToken);
            return NotImplemented("Google sign-in not implemented yet.");
        }

        public Task SignInWithAppleAsync(string identityToken)
        {
            // TODO:
            // await EnsureInitializedAsync();
            // await AuthenticationService.Instance.SignInWithAppleAsync(identityToken);
            return NotImplemented("Apple sign-in not implemented yet.");
        }

        public Task SignInWithFacebookAsync(string accessToken)
        {
            // TODO:
            // await EnsureInitializedAsync();
            // await AuthenticationService.Instance.SignInWithFacebookAsync(accessToken);
            return NotImplemented("Facebook sign-in not implemented yet.");
        }

        public Task SignInWithSteamAsync(string steamSessionTicket)
        {
            // TODO:
            // await EnsureInitializedAsync();
            // await AuthenticationService.Instance.SignInWithSteamAsync(steamSessionTicket);
            return NotImplemented("Steam sign-in not implemented yet.");
        }

        public Task SignInWithUnityPlayerAccountAsync(string unityPlayerAccountAccessToken)
        {
            // TODO:
            // await EnsureInitializedAsync();
            // await AuthenticationService.Instance.SignInWithUnityAsync(unityPlayerAccountAccessToken);
            return NotImplemented("Unity Player Accounts sign-in not implemented yet.");
        }

        // Linking stubs (common pattern: sign in anonymously first, then link a provider)
        public Task LinkWithGoogleAsync(string idToken)
        {
            // TODO:
            // await AuthenticationService.Instance.LinkWithGoogleAsync(idToken);
            return NotImplemented("Google link not implemented yet.");
        }

        public Task LinkWithAppleAsync(string identityToken)
        {
            // TODO:
            // await AuthenticationService.Instance.LinkWithAppleAsync(identityToken);
            return NotImplemented("Apple link not implemented yet.");
        }

        public Task LinkWithFacebookAsync(string accessToken)
        {
            // TODO:
            // await AuthenticationService.Instance.LinkWithFacebookAsync(accessToken);
            return NotImplemented("Facebook link not implemented yet.");
        }

        public Task LinkWithSteamAsync(string steamSessionTicket)
        {
            // TODO:
            // await AuthenticationService.Instance.LinkWithSteamAsync(steamSessionTicket);
            return NotImplemented("Steam link not implemented yet.");
        }

        // ---------------------------
        // Internals
        // ---------------------------

        bool _eventsWired;
        void WireAuthEventsOnce()
        {
            if (_eventsWired) return;
            _eventsWired = true;

            AuthenticationService.Instance.SignedIn += () =>
            {
                State = AuthState.SignedIn;
                Log($"Auth event: SignedIn. PlayerId={AuthenticationService.Instance.PlayerId}");
                OnSignedIn?.Invoke(AuthenticationService.Instance.PlayerId);
            };

            AuthenticationService.Instance.SignInFailed += (RequestFailedException ex) =>
            {
                State = AuthState.Failed;
                Log($"Auth event: SignInFailed: {ex.ErrorCode} - {ex.Message}");
                OnSignInFailed?.Invoke(ex.Message);
            };

            AuthenticationService.Instance.SignedOut += () =>
            {
                State = AuthState.Ready;
                Log("Auth event: SignedOut");
                OnSignedOut?.Invoke();
            };

            AuthenticationService.Instance.Expired += () =>
            {
                State = AuthState.Ready;
                Log("Auth event: Session Expired");
            };
        }

        static Task NotImplemented(string message)
        {
            Debug.LogWarning(message);
            return Task.CompletedTask;
        }

        void Log(string msg)
        {
            if (verboseLogs)
                Debug.Log($"[UGS Auth] {msg}");
        }
    }
}
