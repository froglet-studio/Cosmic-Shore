using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace CosmicShore.Core
{
    /// <summary>
    /// The <see cref="IPlatformSignIn"/> registered until the login branch lands: no provider is available, nothing is
    /// linked, and every call answers <see cref="SignInStatus.Unavailable"/> (Docs/MENU_REDESIGN_B.md §54). Deleting the
    /// account is the exception: it does not need a provider, so it works for a guest through
    /// <see cref="AccountDeletionService"/>.
    /// </summary>
    public sealed class NoPlatformSignIn : IPlatformSignIn
    {
        readonly AccountDeletionService _deletion;

        public NoPlatformSignIn(AccountDeletionService deletion = null)
        {
            _deletion = deletion;
        }

        public bool IsAvailable(SignInProvider provider) => false;
        public bool UsesBrowser(SignInProvider provider) => provider == SignInProvider.Google;
        public bool IsLinked(SignInProvider provider) => false;
        public string LinkedName(SignInProvider provider) => string.Empty;
        public bool CanSignOut => false;
        public bool CanDeleteAccount => _deletion != null && _deletion.CanDelete;

        public UniTask<SignInOutcome> SignInAsync(SignInProvider provider, CancellationToken ct) =>
            UniTask.FromResult(SignInOutcome.Unavailable(provider));

        public UniTask<SignInOutcome> LinkAsync(SignInProvider provider, CancellationToken ct) =>
            UniTask.FromResult(SignInOutcome.Unavailable(provider));

        public UniTask<SignInOutcome> SwitchToLinkedPilotAsync(SignInProvider provider, CancellationToken ct) =>
            UniTask.FromResult(SignInOutcome.Unavailable(provider));

        public UniTask SignOutAsync(CancellationToken ct) => UniTask.CompletedTask;
        public UniTask<AccountDeletionStatus> DeleteAccountAsync(CancellationToken ct) =>
            _deletion != null ? _deletion.DeleteAsync(ct) : UniTask.FromResult(AccountDeletionStatus.Unavailable);

        public event Action AccountsChanged { add { } remove { } }
    }
}
