using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using CosmicShore.Utility;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using UnityEngine;
using PlayerDeleteAllOptions = Unity.Services.CloudSave.Models.Data.Player.DeleteAllOptions;
using PublicWriteAccessClassOptions = Unity.Services.CloudSave.Models.Data.Player.PublicWriteAccessClassOptions;

namespace CosmicShore.Core
{
    /// <summary>
    /// The shipped <see cref="IAccountDeletionSteps"/>: UGS Cloud Save, the analytics facade, UGS Authentication, the
    /// device's local copies and the boot chain. The same layers <c>FrogletTools &gt; PlayerDataWipeWindow</c> wipes in
    /// the editor, in the same order (Cloud Save while signed in, the account last).
    /// </summary>
    public sealed class UgsAccountDeletionSteps : IAccountDeletionSteps
    {
        readonly AuthenticationServiceFacade _auth;
        readonly AnalyticsServiceFacade _analytics;
        readonly Action _restart;

        /// <param name="restart">Re-runs the boot chain (<see cref="ReconnectService.ReconnectAsync"/>).</param>
        public UgsAccountDeletionSteps(AuthenticationServiceFacade auth, AnalyticsServiceFacade analytics, Action restart)
        {
            _auth = auth;
            _analytics = analytics;
            _restart = restart;
        }

        public bool CanDelete => !WebToybox.IsBuild && _auth != null && _auth.IsSignedIn;

        public async UniTask DeleteCloudSaveAsync(CancellationToken ct)
        {
            var player = CloudSaveService.Instance.Data.Player;

            // Default access class: progress, profile, settings - every UGSKeys key and anything older builds wrote.
            await DeleteAllAsync(() => player.DeleteAllAsync(new PlayerDeleteAllOptions()), ct);

            // Public access class: what other players can read about this one (DisplayNameRegistry's name index,
            // SquadDirectory's squad record). The protected class is server-written and not the client's to delete.
            await DeleteAllAsync(() => player.DeleteAllAsync(new PlayerDeleteAllOptions(new PublicWriteAccessClassOptions())), ct);
        }

        static async UniTask DeleteAllAsync(Func<System.Threading.Tasks.Task> deleteAll, CancellationToken ct)
        {
            try
            {
                await deleteAll().AsMainThread();
            }
            catch (CloudSaveException e) when (e.Reason == CloudSaveExceptionReason.NotFound)
            {
                // Nothing stored in that access class: already the state the player asked for.
            }
            ct.ThrowIfCancellationRequested();
        }

        public void RequestAnalyticsDeletion() => _analytics?.RequestDataDeletion();

        public async UniTask DeleteAuthenticationAccountAsync(CancellationToken ct)
        {
            await AuthenticationService.Instance.DeleteAccountAsync().AsMainThread();
            ct.ThrowIfCancellationRequested();
        }

        public void WipeLocalData()
        {
            LocalCloudDataCache.DeleteAll();
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            // DeleteAccountAsync drops the session token with the account; clear it anyway so the next sign-in can
            // only mint a new player. The SDK refuses to clear a token while signed in, so only when signed out.
            try
            {
                if (UnityServices.State == ServicesInitializationState.Initialized &&
                    !AuthenticationService.Instance.IsSignedIn &&
                    AuthenticationService.Instance.SessionTokenExists)
                    AuthenticationService.Instance.ClearSessionToken();
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[AccountDeletion] Clearing the session token failed: {e.Message}");
            }
        }

        public void Restart() => _restart?.Invoke();
    }
}
