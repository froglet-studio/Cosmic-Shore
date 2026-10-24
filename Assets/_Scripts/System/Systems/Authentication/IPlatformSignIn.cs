using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace CosmicShore.Core
{
    /// <summary>
    /// The seam between the sign-in UI and the platform sign-in providers (Steam, Google, Apple), Docs/MENU_REDESIGN_B.md
    /// §54. The UI (SignInChooser on the sign-in scene, AccountSettingsView in Settings) only ever talks to this; the
    /// login branch implements it over <see cref="AuthenticationServiceFacade"/> and the providers' SDKs and swaps the
    /// DI registration in AppManager. Until then <see cref="NoPlatformSignIn"/> reports every provider unavailable, so
    /// the UI shows only Play as guest in a release build (and the provider buttons, answering "not ready in this
    /// build", in the editor and development builds).
    /// Guest is not a provider here: it stays the scene controller's own path (anonymous sign-in).
    /// </summary>
    public interface IPlatformSignIn
    {
        /// <summary>This build can sign in or link with the provider.</summary>
        bool IsAvailable(SignInProvider provider);

        /// <summary>The provider's sign-in happens in the system browser (Google on PC), so the UI shows a
        /// "finish in your browser" window rather than a spinner.</summary>
        bool UsesBrowser(SignInProvider provider);

        /// <summary>The signed-in player has this provider linked.</summary>
        bool IsLinked(SignInProvider provider);

        /// <summary>The linked account's name ("Shomboss"), or empty when unknown or not linked.</summary>
        string LinkedName(SignInProvider provider);

        /// <summary>Signing out keeps the pilot reachable (some provider is linked). A guest who signs out loses the
        /// pilot, so the UI hides Sign out while this is false.</summary>
        bool CanSignOut { get; }

        bool CanDeleteAccount { get; }

        UniTask<SignInOutcome> SignInAsync(SignInProvider provider, CancellationToken ct);

        /// <summary>Adds the provider to the signed-in player, keeping their progress. Answers
        /// <see cref="SignInStatus.AlreadyHasPilot"/> with both pilots' summaries when the account belongs to another.</summary>
        UniTask<SignInOutcome> LinkAsync(SignInProvider provider, CancellationToken ct);

        /// <summary>After <see cref="SignInStatus.AlreadyHasPilot"/>: sign this device in as the provider's pilot instead.</summary>
        UniTask<SignInOutcome> SwitchToLinkedPilotAsync(SignInProvider provider, CancellationToken ct);

        UniTask SignOutAsync(CancellationToken ct);

        UniTask DeleteAccountAsync(CancellationToken ct);

        /// <summary>Raised when a link, unlink, sign-out or deletion changed what the Account tab shows.</summary>
        event Action AccountsChanged;
    }
}
