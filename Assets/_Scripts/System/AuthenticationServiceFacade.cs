using System;
using System.Threading;
using System.Threading.Tasks;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Services.Core;
using Unity.Services.Authentication;

namespace CosmicShore.Core
{
    public class AuthenticationServiceFacade
    {
        public bool IsSignedIn =>
            AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn;

        public string PlayerId =>
            IsSignedIn ? AuthenticationService.Instance.PlayerId : string.Empty;

        public bool SessionTokenExists =>
            AuthenticationService.Instance != null && AuthenticationService.Instance.SessionTokenExists;

        readonly AuthenticationDataVariable _authenticationDataVariable;
        readonly bool _allowLog;

        AuthenticationData authenticationData => _authenticationDataVariable.Value;

        bool _startupAttempted;
        bool _eventsWired;
        bool _successNotified;
        Task _initTask;

        public AuthenticationServiceFacade(AuthenticationDataVariable authenticationDataVariable, bool allowLog)
        {
            _authenticationDataVariable = authenticationDataVariable;
            _allowLog = allowLog;
        }

        /// <summary>
        /// Kicks off initialization + anonymous sign-in.
        /// Safe to call from AppManager.Start() as fire-and-forget.
        /// </summary>
        public async void StartAuthentication()
        {
            if (_startupAttempted)
                return;

            _startupAttempted = true;

            try
            {
                await EnsureInitializedAsync();
                await EnsureSignedInAnonymouslyAsync();
            }
            catch (Exception e)
            {
                OnSignInFailed(e);
            }
        }

        /// <summary>
        /// Initializes Unity Services and wires auth events.
        /// Coalesces concurrent callers into a single initialization attempt.
        /// </summary>
        public Task EnsureInitializedAsync()
        {
            if (authenticationData.State == AuthenticationData.AuthState.Ready ||
                authenticationData.State == AuthenticationData.AuthState.SignedIn)
                return Task.CompletedTask;

            if (_initTask != null && !_initTask.IsCompleted)
                return _initTask;

            _initTask = InitializeCore();
            return _initTask;
        }

        async Task InitializeCore()
        {
            authenticationData.State = AuthenticationData.AuthState.Initializing;
            Log("Initializing Unity Services...");

            await UnityServices.InitializeAsync();
            WireAuthEventsOnce();

            authenticationData.State = AuthenticationData.AuthState.Ready;
            Log("Unity Services initialized.");
        }

        /// <summary>
        /// Signs in anonymously if not already signed in.
        /// Uses cached session token when available for silent re-authentication.
        /// </summary>
        public async Task EnsureSignedInAnonymouslyAsync()
        {
            await EnsureInitializedAsync();

            if (AuthenticationService.Instance == null)
            {
                OnSignInFailed("AuthenticationService.Instance is null after initialization.");
                return;
            }

            if (AuthenticationService.Instance.IsSignedIn)
            {
                Log($"Already signed in. PlayerId={AuthenticationService.Instance.PlayerId}");
                OnSignInSuccess();
                return;
            }

            authenticationData.State = AuthenticationData.AuthState.SigningIn;
            Log($"Signing in anonymously... (SessionTokenExists={SessionTokenExists})");

            try
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                OnSignInSuccess();
            }
            catch (Exception e)
            {
                OnSignInFailed(e);
            }
        }

        /// <summary>
        /// Attempts to restore a cached session without showing UI.
        /// Returns true if the user is now signed in.
        /// </summary>
        public async Task<bool> TrySignInCachedAsync()
        {
            await EnsureInitializedAsync();

            if (AuthenticationService.Instance == null)
                return false;

            if (AuthenticationService.Instance.IsSignedIn)
            {
                OnSignInSuccess();
                return true;
            }

            if (!AuthenticationService.Instance.SessionTokenExists)
                return false;

            try
            {
                authenticationData.State = AuthenticationData.AuthState.SigningIn;
                Log("Attempting cached session sign-in...");
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                OnSignInSuccess();
                return true;
            }
            catch (Exception ex)
            {
                authenticationData.State = AuthenticationData.AuthState.Failed;
                Log($"Cached sign-in failed: {ex.Message}");
                return false;
            }
        }

        public void SignOut(bool clearSessionToken = false)
        {
            if (AuthenticationService.Instance == null)
                return;

            try
            {
                AuthenticationService.Instance.SignOut();

                if (clearSessionToken)
                    AuthenticationService.Instance.ClearSessionToken();

                OnSignedOut("Manual SignOut invoked.");
            }
            catch (Exception e)
            {
                Log($"SignOut threw exception: {e}");
                OnSignedOut("Manual SignOut invoked (exception occurred).");
            }
        }

        /// <summary>
        /// Allows the caller to reset the startup guard so authentication
        /// can be re-attempted after a failure or sign-out.
        /// </summary>
        public void ResetStartupState()
        {
            _startupAttempted = false;
            _initTask = null;
        }

        // Provider stubs for future platform sign-in
        public Task SignInWithGoogleAsync(string idToken) => Task.CompletedTask;
        public Task SignInWithAppleAsync(string identityToken) => Task.CompletedTask;
        public Task SignInWithFacebookAsync(string accessToken) => Task.CompletedTask;
        public Task SignInWithSteamAsync(string steamSessionTicket) => Task.CompletedTask;
        public Task SignInWithUnityPlayerAccountAsync(string token) => Task.CompletedTask;
        public Task LinkWithGoogleAsync(string idToken) => Task.CompletedTask;
        public Task LinkWithAppleAsync(string identityToken) => Task.CompletedTask;
        public Task LinkWithFacebookAsync(string accessToken) => Task.CompletedTask;
        public Task LinkWithSteamAsync(string steamSessionTicket) => Task.CompletedTask;

        // ──────────────────────────────────────────────
        //  UGS Auth Event Wiring
        // ──────────────────────────────────────────────

        void WireAuthEventsOnce()
        {
            if (_eventsWired)
                return;

            if (AuthenticationService.Instance == null)
                return;

            _eventsWired = true;

            AuthenticationService.Instance.SignedIn += () => OnSignInSuccess();

            AuthenticationService.Instance.SignInFailed += (RequestFailedException ex) => OnSignInFailed(ex);

            AuthenticationService.Instance.SignedOut += () => OnSignedOut("Auth event: SignedOut");

            AuthenticationService.Instance.Expired += () => OnSignedOut("Auth event: Session Expired");
        }

        // ──────────────────────────────────────────────
        //  Centralized State + SOAP Event Helpers
        // ──────────────────────────────────────────────

        void OnSignInSuccess()
        {
            if (AuthenticationService.Instance == null)
            {
                OnSignInFailed("OnSignInSuccess called but AuthenticationService.Instance is null.");
                return;
            }

            // Prevent double-raising (await completion + SignedIn event)
            if (_successNotified && authenticationData.State == AuthenticationData.AuthState.SignedIn)
                return;

            _successNotified = true;

            authenticationData.State = AuthenticationData.AuthState.SignedIn;
            authenticationData.IsSignedIn = true;
            authenticationData.PlayerId = AuthenticationService.Instance.PlayerId;

            Log($"Sign-in complete. PlayerId={AuthenticationService.Instance.PlayerId}");
            authenticationData.OnSignedIn?.Raise();
        }

        void OnSignInFailed(Exception e)
        {
            authenticationData.State = AuthenticationData.AuthState.Failed;
            authenticationData.IsSignedIn = false;
            authenticationData.PlayerId = string.Empty;

            Log($"Sign-in failed: {e}");
            authenticationData.OnSignInFailed?.Raise();
        }

        void OnSignInFailed(string reason)
        {
            authenticationData.State = AuthenticationData.AuthState.Failed;
            authenticationData.IsSignedIn = false;
            authenticationData.PlayerId = string.Empty;

            Log($"Sign-in failed: {reason}");
            authenticationData.OnSignInFailed?.Raise();
        }

        void OnSignedOut(string reason)
        {
            _successNotified = false;

            authenticationData.State = AuthenticationData.AuthState.Ready;
            authenticationData.IsSignedIn = false;
            authenticationData.PlayerId = string.Empty;

            Log(reason);
            authenticationData.OnSignedOut?.Raise();
        }

        void Log(string msg)
        {
            if (_allowLog)
                CSDebug.Log($"[UGS Auth] {msg}");
        }
    }
}
