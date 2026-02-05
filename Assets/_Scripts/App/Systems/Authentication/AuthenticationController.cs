using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;

namespace CosmicShore.Services.Auth
{
    public class AuthenticationController : MonoBehaviour
    {
        // [Visual Note] Added Singleton Instance here
        public static AuthenticationController Instance { get; private set; }

        public enum AuthState
        {
            NotInitialized, Initializing, Ready, SigningIn, SignedIn, Failed
        }

        [Header("Startup")]
        [SerializeField] private bool dontDestroyOnLoad = true;
        [SerializeField] private bool autoSignInAnonymously = true;

        [Header("Debug")]
        [SerializeField] private bool verboseLogs = true;

        private AuthState State { get; set; } = AuthState.NotInitialized;

        public bool IsSignedIn => AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn;
        public string PlayerId => IsSignedIn ? AuthenticationService.Instance.PlayerId : string.Empty;
        private string SessionTokenExists => AuthenticationService.Instance != null && AuthenticationService.Instance.SessionTokenExists ? "Yes" : "No";

        public event Action<string> OnSignedIn;           
        public event Action<string> OnSignInFailed;     
        public event Action OnSignedOut;

        bool _startupAttempted;

        void Awake()
        {
            // [Visual Note] Singleton Pattern Setup
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

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
            catch (Exception)
            {
                 // Ignored
            }
        }

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

        // ... [Keeping Stubs for Google/Apple/Facebook unchanged] ...
        public Task SignInWithGoogleAsync(string idToken) => Task.CompletedTask;
        public Task SignInWithAppleAsync(string identityToken) => Task.CompletedTask;
        public Task SignInWithFacebookAsync(string accessToken) => Task.CompletedTask;
        public Task SignInWithSteamAsync(string steamSessionTicket) => Task.CompletedTask;
        public Task SignInWithUnityPlayerAccountAsync(string token) => Task.CompletedTask;
        public Task LinkWithGoogleAsync(string idToken) => Task.CompletedTask;
        public Task LinkWithAppleAsync(string identityToken) => Task.CompletedTask;
        public Task LinkWithFacebookAsync(string accessToken) => Task.CompletedTask;
        public Task LinkWithSteamAsync(string steamSessionTicket) => Task.CompletedTask;

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

        void Log(string msg)
        {
            if (verboseLogs)
                Debug.Log($"[UGS Auth] {msg}");
        }
    }
}