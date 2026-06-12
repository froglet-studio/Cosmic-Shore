namespace CosmicShore.Engine.Services
{
    /// <summary>
    /// Placeholder shim for the Unity Gaming Services authentication singleton
    /// (<c>Unity.Services.Authentication.AuthenticationService</c>) until the
    /// services phase ports the real auth layer (E13 — same precedent as the
    /// <c>ISession</c> placeholder). Harness-configurable: tests / the CLI set
    /// <see cref="PlayerName"/> / <see cref="PlayerId"/> / <see cref="IsSignedIn"/>
    /// directly; defaults are benign (signed out, empty identity) so verbatim call
    /// sites like <c>Player</c>'s tier-3 display-name fallback keep working headless.
    /// </summary>
    public class AuthenticationService
    {
        /// <summary>Settable so harnesses can swap in a configured instance; never null.</summary>
        public static AuthenticationService Instance { get; set; } = new();

        /// <summary>UGS player name (may carry a <c>#XXXX</c> suffix in the original service).</summary>
        public string PlayerName { get; set; } = string.Empty;

        public string PlayerId { get; set; } = string.Empty;

        public bool IsSignedIn { get; set; }

        /// <summary>Restore the benign signed-out defaults (test isolation helper).</summary>
        public static void Reset() => Instance = new AuthenticationService();
    }
}
