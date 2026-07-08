namespace CosmicShore.Engine.Services
{
    /// <summary>
    /// Placeholder shim for the Unity Gaming Services authentication singleton
    /// (<c>Unity.Services.Authentication.AuthenticationService</c>) until the
    /// services phase ports the real auth layer (E13 — same precedent as the
    /// <c>ISession</c> placeholder). Harness-configurable: tests / the CLI set
    /// <see cref="PlayerName"/> / <see cref="PlayerId"/> / <see cref="IsSignedIn"/>
    /// directly (or swap a subclass into <see cref="Instance"/> — the sign-in/out
    /// methods are virtual); defaults are benign (signed out, empty identity) so
    /// verbatim call sites like <c>Player</c>'s tier-3 display-name fallback keep
    /// working headless.
    ///
    /// Grown 2026-07-08 (bootstrap arc) with the surface AuthenticationServiceFacade
    /// drives: <see cref="SessionTokenExists"/>, virtual
    /// <see cref="SignInAnonymouslyAsync"/> / <see cref="SignOut"/> /
    /// <see cref="ClearSessionToken"/>, and the four auth notifications
    /// (<see cref="SignedIn"/>, <see cref="SignInFailed"/>, <see cref="SignedOut"/>,
    /// <see cref="Expired"/>). The shim's local sign-in mirrors the real SDK's
    /// observable contract (identity set, token cached, SignedIn raised) without a
    /// wire; the real SDK binding replaces the bodies at the services phase.
    /// </summary>
    public class AuthenticationService
    {
        /// <summary>Settable so harnesses can swap in a configured instance; never null.</summary>
        public static AuthenticationService Instance { get; set; } = new();

        /// <summary>UGS player name (may carry a <c>#XXXX</c> suffix in the original service).</summary>
        public string PlayerName { get; set; } = string.Empty;

        public string PlayerId { get; set; } = string.Empty;

        public bool IsSignedIn { get; set; }

        /// <summary>True when a cached session token allows silent re-authentication.</summary>
        public bool SessionTokenExists { get; set; }

        /// <summary>Raised after a successful sign-in.</summary>
        public event System.Action SignedIn;

        /// <summary>Raised when a sign-in attempt fails.</summary>
        public event System.Action<RequestFailedException> SignInFailed;

        /// <summary>Raised after sign-out.</summary>
        public event System.Action SignedOut;

        /// <summary>Raised when the session expires server-side.</summary>
        public event System.Action Expired;

        /// <summary>
        /// Local anonymous sign-in: assigns a stable placeholder identity when none is
        /// configured, caches the session token, and raises <see cref="SignedIn"/> —
        /// the same observable sequence the real SDK produces.
        /// </summary>
        public virtual System.Threading.Tasks.Task SignInAnonymouslyAsync()
        {
            if (string.IsNullOrEmpty(PlayerId))
                PlayerId = "local-player";
            IsSignedIn = true;
            SessionTokenExists = true;
            SignedIn?.Invoke();
            return System.Threading.Tasks.Task.CompletedTask;
        }

        /// <summary>Sign out, keeping the cached session token (the real SDK's default).</summary>
        public virtual void SignOut()
        {
            IsSignedIn = false;
            PlayerId = string.Empty;
            SignedOut?.Invoke();
        }

        /// <summary>Drop the cached session token so the next sign-in is a fresh identity.</summary>
        public virtual void ClearSessionToken() => SessionTokenExists = false;

        /// <summary>
        /// Async name read (original contract: the SDK fetches the stored player name).
        /// The shim returns the local mirror.
        /// </summary>
        public virtual System.Threading.Tasks.Task<string> GetPlayerNameAsync()
            => System.Threading.Tasks.Task.FromResult(PlayerName);

        /// <summary>
        /// Update the UGS player name (original contract: the SDK persists it server-side
        /// and returns the stored value, possibly suffixed). The shim stores it locally.
        /// </summary>
        public virtual System.Threading.Tasks.Task<string> UpdatePlayerNameAsync(string name)
        {
            PlayerName = name;
            return System.Threading.Tasks.Task.FromResult(name);
        }

        /// <summary>Harness entry: raise <see cref="SignInFailed"/> as the SDK would.</summary>
        public void NotifySignInFailed(RequestFailedException e) => SignInFailed?.Invoke(e);

        /// <summary>Harness entry: raise <see cref="Expired"/> as the SDK would.</summary>
        public void NotifyExpired() => Expired?.Invoke();

        /// <summary>Restore the benign signed-out defaults (test isolation helper).</summary>
        public static void Reset() => Instance = new AuthenticationService();
    }
}
