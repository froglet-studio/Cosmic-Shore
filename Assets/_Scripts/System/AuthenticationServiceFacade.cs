using System;
using System.Threading;
using System.Threading.Tasks;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Services.Core;
using Unity.Services.Authentication;
#if UNITY_EDITOR
using Unity.Multiplayer.Playmode;
#endif

namespace CosmicShore.Core
{
    public class AuthenticationServiceFacade
    {
        /// <summary>
        /// True when Unity Services has finished initializing. UnityServices.State THROWS when
        /// read off the Unity thread, so the read is guarded rather than assumed - "not on the
        /// main thread" is answered as "not initialized", which routes the caller into the
        /// initialize path instead of past it.
        /// </summary>
        static bool ServicesInitialized
        {
            get
            {
                try
                {
                    return UnityServices.State == ServicesInitializationState.Initialized;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// The SDK singleton, or null when Unity Services has not finished initializing.
        ///
        /// AuthenticationService.Instance THROWS ServicesInitializationException before
        /// initialization - it never returns null - so every `Instance != null` test in this
        /// file was dead code that threw instead of taking its own guarded branch. That matters
        /// on more than a cold boot: [RuntimeInitializeOnLoadMethod] ResetStaticsOnLoad() nulls
        /// the SDK singleton on EVERY entry into play mode, including under this project's
        /// disabled domain reload, so the second Play of an editor session starts here.
        /// Verified against com.unity.services.authentication 3.6.1.
        /// </summary>
        static bool TryGetAuthService(out IAuthenticationService service)
        {
            try
            {
                service = AuthenticationService.Instance;
            }
            catch (Exception)
            {
                service = null;
            }

            return service != null;
        }

        public bool IsSignedIn =>
            TryGetAuthService(out var svc) && svc.IsSignedIn;

        public string PlayerId =>
            TryGetAuthService(out var svc) && svc.IsSignedIn ? svc.PlayerId : string.Empty;

        public bool SessionTokenExists =>
            TryGetAuthService(out var svc) && svc.SessionTokenExists;

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

            // State / IsSignedIn / PlayerId are plain auto-properties on a class held by a
            // ScriptableObject, so Unity does not serialize them and SOAP never resets them.
            // With Enter Play Mode Options disabling domain reload (this project does), that
            // object survives play-mode exit and the NEXT session starts holding the LAST
            // session's SignedIn - which made EnsureInitializedAsync short-circuit and
            // UnityServices.InitializeAsync never run. This facade is the single writer, so
            // it resets at construction. CLAUDE.md, SOAP anti-patterns.
            ResetRuntimeState();
        }

        /// <summary>Clears the runtime mirror without raising anything.</summary>
        void ResetRuntimeState()
        {
            var data = _authenticationDataVariable != null ? _authenticationDataVariable.Value : null;
            if (data == null)
                return;

            data.State = AuthenticationData.AuthState.NotInitialized;
            data.IsSignedIn = false;
            data.PlayerId = string.Empty;
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
            // Asked of the SDK, never of our own mirror. The mirror is a REPORT of this state
            // and can disagree with it - it survives a play-mode exit under fast enter-play-mode,
            // and a raise that threw can leave it stale mid-session - and when it does, this
            // guard skips the one call that makes AuthenticationService.Instance exist. Reconcile
            // with independently readable state rather than trusting the mirror (the same rule
            // AnalyticsServiceFacade records for its _signedIn latch).
            if (ServicesInitialized)
            {
                // Short-circuiting must not skip the event wiring: SignedIn / SignInFailed /
                // Expired are how a sign-in that completes OUTSIDE our own await is heard at all.
                WireAuthEventsOnce();

                if (authenticationData.State == AuthenticationData.AuthState.NotInitialized)
                    authenticationData.State = AuthenticationData.AuthState.Ready;

                return Task.CompletedTask;
            }

            if (_initTask != null && !_initTask.IsCompleted)
                return _initTask;

            _initTask = InitializeCore();
            return _initTask;
        }

        async Task InitializeCore()
        {
            authenticationData.State = AuthenticationData.AuthState.Initializing;
            Log("Initializing Unity Services...");

            // Marshalled back to the MAIN THREAD: everything after this line writes the SOAP
            // mirror and (downstream of sign-in) raises OnSignedIn, whose listeners instantiate
            // NetworkManager, load cloud data and touch ScriptableObjects. SOAP raises inline,
            // so an off-thread continuation surfaces as EnsureRunningOnMainThread inside a
            // listener - swallowed by the async-void catch, leaving sign-in silently unfinished.
            // Docs/THREADING.md.
            await UnityServices.InitializeAsync().AsMainThread();
            SwitchMppmProfileIfNeeded();
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

            if (!TryGetAuthService(out var svc))
            {
                OnSignInFailed("AuthenticationService is unavailable after initialization.");
                return;
            }

            if (svc.IsSignedIn)
            {
                Log($"Already signed in. PlayerId={svc.PlayerId}");
                OnSignInSuccess();
                return;
            }

            authenticationData.State = AuthenticationData.AuthState.SigningIn;
            Log($"Signing in anonymously... (SessionTokenExists={svc.SessionTokenExists})");

            try
            {
                await svc.SignInAnonymouslyAsync().AsMainThread();
            }
            catch (Exception e)
            {
                OnSignInFailed(e);
                return;
            }

            // Deliberately OUTSIDE the try. OnSignInSuccess raises OnSignedIn inline, so a
            // throwing LISTENER used to be caught here and reported as a sign-in failure -
            // flipping the state to Failed on a session that had in fact signed in, and taking
            // the rest of the listener chain down with it.
            OnSignInSuccess();
        }

        /// <summary>
        /// Attempts to restore a cached session without showing UI.
        /// Returns true if the user is now signed in.
        /// </summary>
        public async Task<bool> TrySignInCachedAsync()
        {
            await EnsureInitializedAsync();

            if (!TryGetAuthService(out var svc))
                return false;

            if (svc.IsSignedIn)
            {
                OnSignInSuccess();
                return true;
            }

            if (!svc.SessionTokenExists)
                return false;

            try
            {
                authenticationData.State = AuthenticationData.AuthState.SigningIn;
                Log("Attempting cached session sign-in...");
                await svc.SignInAnonymouslyAsync().AsMainThread();
            }
            catch (Exception ex)
            {
                authenticationData.State = AuthenticationData.AuthState.Failed;
                Log($"Cached sign-in failed: {ex.Message}");
                return false;
            }

            // Outside the try, for the same reason as EnsureSignedInAnonymouslyAsync above.
            OnSignInSuccess();
            return true;
        }

        public void SignOut(bool clearSessionToken = false)
        {
            if (!TryGetAuthService(out var svc))
                return;

            try
            {
                svc.SignOut();

                if (clearSessionToken)
                    svc.ClearSessionToken();

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

        /// <summary>
        /// Full reset for the RECONNECT flow (<see cref="ReconnectService"/>): re-arms the
        /// startup guard AND clears the success latch, so a subsequent successful sign-in
        /// raises <c>OnSignedIn</c> again.
        ///
        /// <para>
        /// That raise is the trunk the whole online stack hangs off - HostConnectionService's
        /// lobby + Relay session, UGSDataService's cloud load, MultiplayerSetup's host wiring.
        /// <see cref="ResetStartupState"/> alone leaves <c>_successNotified</c> latched from a
        /// prior session, so a reconnect that signs in successfully would go silent and
        /// nothing downstream would ever start. Auth events stay wired
        /// (<c>_eventsWired</c>) - they are idempotent SDK subscriptions, not session state.
        /// </para>
        /// </summary>
        public void ResetForReconnect()
        {
            ResetStartupState();
            _successNotified = false;

            // State is deliberately NOT reset - it would defeat the IsSignedIn fast path the
            // auth scene relies on to re-announce a still-live session. (Re-initialization is
            // no longer a risk either way: EnsureInitializedAsync asks UnityServices.State
            // rather than this mirror.) Clearing the success latch is the whole job: it is
            // what lets the next sign-in - or the fast path over an existing one - raise
            // OnSignedIn again.
            Log("Reset for reconnect - OnSignedIn will re-raise on the next sign-in.");
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
        //  MPPM Profile Isolation
        // ──────────────────────────────────────────────

        /// <summary>
        /// When running as an MPPM virtual player, switches to a tag-based
        /// auth profile so each editor instance gets its own UGS identity.
        /// Must be called after InitializeAsync() but before SignInAnonymouslyAsync().
        /// </summary>
        void SwitchMppmProfileIfNeeded()
        {
#if UNITY_EDITOR
            if (CurrentPlayer.IsMainEditor)
                return;

            var tags = CurrentPlayer.ReadOnlyTags();
            var profileName = tags != null && tags.Length > 0
                ? $"mppm-{string.Join("-", tags)}"
                : "mppm-clone";

            if (!TryGetAuthService(out var svc))
                return;

            svc.SwitchProfile(profileName);
            Log($"MPPM: Switched to auth profile '{profileName}'.");
#endif
        }

        // ──────────────────────────────────────────────
        //  UGS Auth Event Wiring
        // ──────────────────────────────────────────────

        void WireAuthEventsOnce()
        {
            if (_eventsWired)
                return;

            if (!TryGetAuthService(out var svc))
                return;

            _eventsWired = true;

            // Every one of these handlers writes the SOAP mirror and raises a SOAP event, which
            // runs its listeners INLINE. The SDK raises them on whatever thread finished the
            // request, so they are marshalled first. Docs/THREADING.md.
            svc.SignedIn += () => OnMainThread(OnSignInSuccess);

            svc.SignInFailed += (RequestFailedException ex) => OnMainThread(() => OnSignInFailed(ex));

            svc.SignedOut += () => OnMainThread(() => OnSignedOut("Auth event: SignedOut"));

            svc.Expired += () => OnMainThread(() => OnSignedOut("Auth event: Session Expired"));
        }

        /// <summary>
        /// Runs <paramref name="action"/> on the main thread - immediately when already there,
        /// so the common case keeps its synchronous ordering.
        /// </summary>
        static async void OnMainThread(Action action)
        {
            try
            {
                if (!MainThreadDispatcher.IsOnMainThread)
                    await MainThreadDispatcher.SwitchToMainThreadAsync();

                action();
            }
            catch (Exception e)
            {
                CSDebug.LogError($"[UGS Auth] Auth event handler threw: {e}");
            }
        }

        // ──────────────────────────────────────────────
        //  Centralized State + SOAP Event Helpers
        // ──────────────────────────────────────────────

        void OnSignInSuccess()
        {
            if (!TryGetAuthService(out var svc))
            {
                OnSignInFailed("OnSignInSuccess called but AuthenticationService is unavailable.");
                return;
            }

            // Prevent double-raising (await completion + SignedIn event)
            if (_successNotified && authenticationData.State == AuthenticationData.AuthState.SignedIn)
                return;

            _successNotified = true;

            authenticationData.State = AuthenticationData.AuthState.SignedIn;
            authenticationData.IsSignedIn = true;
            authenticationData.PlayerId = svc.PlayerId;

            Log($"Sign-in complete. PlayerId={svc.PlayerId}");
            authenticationData.OnSignedIn?.Raise();
        }

        void OnSignInFailed(Exception e)
        {
            authenticationData.State = AuthenticationData.AuthState.Failed;
            authenticationData.IsSignedIn = false;
            authenticationData.PlayerId = string.Empty;

            LogFailure($"Sign-in failed: {e}");
            authenticationData.OnSignInFailed?.Raise();
        }

        void OnSignInFailed(string reason)
        {
            authenticationData.State = AuthenticationData.AuthState.Failed;
            authenticationData.IsSignedIn = false;
            authenticationData.PlayerId = string.Empty;

            LogFailure($"Sign-in failed: {reason}");
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

        /// <summary>
        /// A sign-in failure is NEVER gated on the verbose flag. Everything downstream of
        /// OnSignedIn - cloud data, the presence lobby, the Relay session, analytics - simply
        /// waits when sign-in does not complete, so the only symptom is a boot that sits there.
        /// The one component that knows why has to say so unprompted.
        /// </summary>
        static void LogFailure(string msg) => CSDebug.LogWarning($"[UGS Auth] {msg}");
    }
}
