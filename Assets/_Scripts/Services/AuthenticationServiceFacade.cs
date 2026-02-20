using System;
using System.Threading.Tasks;
using CosmicShore.Utilities;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;

namespace CosmicShore.App.Services
{
    public class AuthenticationServiceFacade
    {
        public bool IsSignedIn =>
            AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn;

        public string PlayerId =>
            IsSignedIn ? AuthenticationService.Instance.PlayerId : string.Empty;

        // Safe null-guard (and returns "No" if service isn't available yet)
        private string SessionTokenExists =>
            (AuthenticationService.Instance != null && AuthenticationService.Instance.SessionTokenExists) ? "Yes" : "No";

        bool _startupAttempted;
        bool _allowLog;

        AuthenticationDataVariable _authenticationDataVariable;
        private AuthenticationData authenticationData => _authenticationDataVariable.Value;

        bool _eventsWired;
        bool _successNotified; // prevents double-raising SignedIn if we get both await + event

        public AuthenticationServiceFacade(AuthenticationDataVariable authenticationDataVariable, bool allowLog)
        {
            _authenticationDataVariable = authenticationDataVariable;
            _allowLog = allowLog;
        }
        
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
                // Any exception from init/sign-in should be treated as sign-in failure
                OnSignInFailed(e);
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

                // We call our helper here because:
                // - SignedOut event might fire later, or not at all in some edge cases.
                // - We want deterministic state + event raising from this call.
                OnSignedOut("Manual SignOut invoked.");
            }
            catch (Exception e)
            {
                // SignOut shouldn't usually fail, but if it does, log it and still move to signed-out-ready state.
                Log($"SignOut threw exception: {e}");
                OnSignedOut("Manual SignOut invoked (exception occurred).");
            }
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

        async Task EnsureInitializedAsync()
        {
            if (authenticationData.State == AuthenticationData.AuthState.Ready ||
                authenticationData.State == AuthenticationData.AuthState.SignedIn)
                return;

            if (authenticationData.State == AuthenticationData.AuthState.Initializing)
                return;

            authenticationData.State = AuthenticationData.AuthState.Initializing;
            Log("Initializing Unity Services...");

            await UnityServices.InitializeAsync();
            WireAuthEventsOnce();

            authenticationData.State = AuthenticationData.AuthState.Ready;
            Log("Unity Services initialized.");
        }

        async Task EnsureSignedInAnonymouslyAsync()
        {
            await EnsureInitializedAsync();

            if (AuthenticationService.Instance == null)
            {
                OnSignInFailed("AuthenticationService.Instance is null after initialization.");
                return;
            }

            if (AuthenticationService.Instance.IsSignedIn)
            {
                // Already signed in; raise success deterministically (guarded against double-firing).
                Log($"Already signed in. PlayerId={AuthenticationService.Instance.PlayerId}");
                OnSignInSuccess();
                return;
            }

            authenticationData.State = AuthenticationData.AuthState.SigningIn;
            Log($"Signing in anonymously... (SessionTokenExists={SessionTokenExists})");

            try
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

                // Some platforms trigger SignedIn event; some flows rely on the await completion.
                // Call success here, but it's guarded so it won't double-raise if the event also fires.
                OnSignInSuccess();
            }
            catch (Exception e)
            {
                OnSignInFailed(e);
            }
        }

        void WireAuthEventsOnce()
        {
            if (_eventsWired)
                return;

            if (AuthenticationService.Instance == null)
                return;

            _eventsWired = true;

            AuthenticationService.Instance.SignedIn += () =>
            {
                // Centralize all success handling
                OnSignInSuccess();
            };

            AuthenticationService.Instance.SignInFailed += (RequestFailedException ex) =>
            {
                // Centralize all failure handling
                OnSignInFailed(ex);
            };

            AuthenticationService.Instance.SignedOut += () =>
            {
                // Centralize all sign-out handling
                OnSignedOut("Auth event: SignedOut");
            };

            AuthenticationService.Instance.Expired += () =>
            {
                // Session expired != explicit signed-out, but your state/event behavior can be consistent.
                // If you WANT to treat Expired as signed-out, route through OnSignedOut as well.
                OnSignedOut("Auth event: Session Expired");
            };
        }

        // ---------------- Centralized event/state helpers ----------------

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
            // Reset the "success notified" flag so future sign-in attempts can raise success again.
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
                Debug.Log($"[UGS Auth] {msg}");
        }
    }
}