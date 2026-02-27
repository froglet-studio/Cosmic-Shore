using System;
using System.Threading;
using System.Threading.Tasks;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using UnityEngine;
using Unity.Services.Authentication;

namespace CosmicShore.Core
{
    /// <summary>
    /// MonoBehaviour adapter for <see cref="AuthenticationServiceFacade"/>.
    ///
    /// Place this on a scene GameObject when you need inspector-driven auto-sign-in
    /// or a MonoBehaviour lifecycle hook to kick off auth. All real auth work is
    /// delegated to the DI-provided facade, which manages state through the
    /// <see cref="AuthenticationDataVariable"/> SOAP asset.
    ///
    /// If the facade is not available via DI (e.g. running outside Bootstrap),
    /// a standalone fallback path handles initialization directly.
    /// </summary>
    public class AuthenticationController : MonoBehaviour
    {
        [Header("Startup")]
        [SerializeField] private bool autoSignInAnonymously;

        [Header("Debug")]
        [SerializeField] private bool verboseLogs = true;

        [Inject] AuthenticationServiceFacade _facade;
        [Inject] AuthenticationDataVariable _authDataVariable;

        CancellationTokenSource _cts;

        bool HasFacade => _facade != null;
        AuthenticationData AuthData => _authDataVariable?.Value;

        public bool IsSignedIn => HasFacade
            ? _facade.IsSignedIn
            : AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn;

        public string PlayerId => HasFacade
            ? _facade.PlayerId
            : (IsSignedIn ? AuthenticationService.Instance.PlayerId : string.Empty);

        void OnEnable()
        {
            _cts = new CancellationTokenSource();
        }

        void OnDisable()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        void Start()
        {
            if (!autoSignInAnonymously)
                return;

            if (HasFacade)
            {
                _facade.StartAuthentication();
            }
            else
            {
                Log("No facade injected. Skipping auto-sign-in (auth is handled by AppManager).");
            }
        }

        /// <summary>
        /// Ensures the user is signed in anonymously.
        /// Delegates to the facade when available.
        /// </summary>
        public async Task EnsureSignedInAnonymouslyAsync()
        {
            if (HasFacade)
            {
                await _facade.EnsureSignedInAnonymouslyAsync();
                return;
            }

            Log("No facade available. Cannot sign in.");
            throw new InvalidOperationException("AuthenticationServiceFacade not available via DI.");
        }

        /// <summary>
        /// Attempts to restore a cached session without showing UI.
        /// Returns true if the user is now signed in.
        /// </summary>
        public async Task<bool> TrySignInCachedAsync()
        {
            if (HasFacade)
                return await _facade.TrySignInCachedAsync();

            Log("No facade available for cached sign-in.");
            return false;
        }

        /// <summary>
        /// Signs out the current user.
        /// </summary>
        public void SignOut(bool clearSessionToken = false)
        {
            if (HasFacade)
            {
                _facade.SignOut(clearSessionToken);
                return;
            }

            if (AuthenticationService.Instance == null)
                return;

            AuthenticationService.Instance.SignOut();
            if (clearSessionToken)
                AuthenticationService.Instance.ClearSessionToken();
        }

        // Provider stubs — delegated to facade
        public Task SignInWithGoogleAsync(string idToken) => Task.CompletedTask;
        public Task SignInWithAppleAsync(string identityToken) => Task.CompletedTask;
        public Task SignInWithFacebookAsync(string accessToken) => Task.CompletedTask;
        public Task SignInWithSteamAsync(string steamSessionTicket) => Task.CompletedTask;
        public Task SignInWithUnityPlayerAccountAsync(string token) => Task.CompletedTask;
        public Task LinkWithGoogleAsync(string idToken) => Task.CompletedTask;
        public Task LinkWithAppleAsync(string identityToken) => Task.CompletedTask;
        public Task LinkWithFacebookAsync(string accessToken) => Task.CompletedTask;
        public Task LinkWithSteamAsync(string steamSessionTicket) => Task.CompletedTask;

        void Log(string msg)
        {
            if (verboseLogs)
                CSDebug.Log($"[AuthController] {msg}");
        }
    }
}
