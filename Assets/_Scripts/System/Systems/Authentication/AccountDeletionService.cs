using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using CosmicShore.Utility;

namespace CosmicShore.Core
{
    /// <summary>How an in-app account deletion ended (<see cref="AccountDeletionService.DeleteAsync"/>).</summary>
    public enum AccountDeletionStatus
    {
        /// <summary>The server data and the account are gone; the device was wiped and the game restarts as a new guest.</summary>
        Deleted = 0,

        /// <summary>Nothing was attempted: not signed in to UGS, the browser Toybox, or a deletion already running.</summary>
        Unavailable = 1,

        /// <summary>A server step failed (usually the network). The account still exists and nothing local was wiped,
        /// so the player can try again.</summary>
        Failed = 2,
    }

    /// <summary>
    /// The steps of an account deletion, behind one seam so the order and the failure rules in
    /// <see cref="AccountDeletionService"/> can be tested without UGS. <see cref="UgsAccountDeletionSteps"/> is the
    /// shipped implementation. A server step THROWS when it did not complete; a local step never throws.
    /// </summary>
    public interface IAccountDeletionSteps
    {
        /// <summary>A deletion can be attempted now: signed in to UGS, and not the browser Toybox.</summary>
        bool CanDelete { get; }

        /// <summary>Deletes every Cloud Save key the player owns, private and public.</summary>
        UniTask DeleteCloudSaveAsync(CancellationToken ct);

        /// <summary>Asks every analytics sink to delete the player's data and stops collection.</summary>
        void RequestAnalyticsDeletion();

        /// <summary>Deletes the UGS Authentication account (and with it the session token).</summary>
        UniTask DeleteAuthenticationAccountAsync(CancellationToken ct);

        /// <summary>Removes what this device still holds about the player: the local Cloud Save snapshot,
        /// PlayerPrefs and any session token left.</summary>
        void WipeLocalData();

        /// <summary>Re-runs the boot chain so the game comes back as a new guest.</summary>
        void Restart();
    }

    /// <summary>
    /// In-app account deletion (Docs/LAUNCH/READINESS.md §3 and §10 #15; App Store Review Guideline 5.1.1(v), Google
    /// Play's account deletion policy). Deletion is provider-independent - a guest deletes the same way as a linked
    /// pilot - so every <see cref="IPlatformSignIn"/> implementation delegates its
    /// <see cref="IPlatformSignIn.DeleteAccountAsync"/> here.
    ///
    /// <para><b>Order.</b> Cloud Save first, while the player is still signed in and while a failure (the likely one:
    /// no network) still leaves everything in place. Then the analytics deletion request, which needs the player's id.
    /// Then the account itself, which signs the player out. Only when all three completed is the device wiped and the
    /// game restarted; a failure anywhere before that returns <see cref="AccountDeletionStatus.Failed"/> with the
    /// account still there, so trying again finishes the job instead of stranding a half-deleted player.</para>
    ///
    /// <para><b>Not done here, by design.</b> PostHog cannot delete a person from the client (its key is write-only):
    /// the sink flags the person and the deletion is finished server side (<c>PostHogAnalyticsSink.RequestDataDeletion</c>).
    /// Data that other UGS services keep against the player id (Leaderboards, Friends, Lobby) cannot be deleted by the
    /// client either; it goes with the account through UGS's own player deletion.</para>
    /// </summary>
    public sealed class AccountDeletionService
    {
        readonly IAccountDeletionSteps _steps;
        bool _running;

        public AccountDeletionService(IAccountDeletionSteps steps)
        {
            _steps = steps ?? throw new ArgumentNullException(nameof(steps));
        }

        /// <summary>A deletion is in progress.</summary>
        public bool IsDeleting => _running;

        /// <summary>The Delete account action can be offered now.</summary>
        public bool CanDelete => !_running && _steps.CanDelete;

        public async UniTask<AccountDeletionStatus> DeleteAsync(CancellationToken ct)
        {
            if (!CanDelete) return AccountDeletionStatus.Unavailable;

            _running = true;
            try
            {
                await _steps.DeleteCloudSaveAsync(ct);
                ct.ThrowIfCancellationRequested();
                _steps.RequestAnalyticsDeletion();
                await _steps.DeleteAuthenticationAccountAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return AccountDeletionStatus.Failed;
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[AccountDeletion] Deletion did not complete, nothing local was wiped: {e.Message}");
                return AccountDeletionStatus.Failed;
            }
            finally
            {
                _running = false;
            }

            _steps.WipeLocalData();
            _steps.Restart();
            return AccountDeletionStatus.Deleted;
        }
    }
}
