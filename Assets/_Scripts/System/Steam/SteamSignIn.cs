using System;
using System.Threading.Tasks;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Services.Authentication;
using Unity.Services.Core;

namespace CosmicShore.Core
{
    /// <summary>
    /// Ties a PC player's UGS account to their Steam account (fair play, Threat Board T1;
    /// Docs/AntiCheat/README.md §4.1). An anonymous account costs nothing to replace, so a restriction on one is
    /// escaped by reinstalling; a Steam account costs a purchase of the game. Used by
    /// <see cref="AuthenticationServiceFacade"/>:
    /// <list type="bullet">
    /// <item>No cached session (a first launch, a reinstall): sign in WITH Steam, so the player lands on the account
    /// already tied to their Steam id.</item>
    /// <item>A cached anonymous session: resume it as before, then LINK Steam to it, so an existing player keeps
    /// their progress and gains the tie.</item>
    /// </list>
    /// Off while <see cref="SteamConfigSO.SignInWithSteam"/> is off, and on every platform without Steam. Every
    /// failure falls back to the anonymous behaviour; Steam never stops a player from signing in.
    /// </summary>
    public static class SteamSignIn
    {
        static SteamConfigSO Config => SteamConfigSO.LoadOrDefault();

        /// <summary>True when this launch should try Steam at all.</summary>
        public static bool IsEnabled => Config.SignInWithSteam && SteamPlatform.TryStart(Config);

        /// <summary>
        /// Signs in with Steam. False, with nothing changed, when Steam gives no ticket or UGS refuses it.
        /// </summary>
        public static async Task<bool> TrySignInAsync(IAuthenticationService svc)
        {
            var ticket = await SteamPlatform.GetWebApiTicketAsync(Config.WebApiIdentity, Config.TicketTimeoutSeconds).AsMainThread();
            if (string.IsNullOrEmpty(ticket))
                return false;

            try
            {
                await svc.SignInWithSteamAsync(ticket, Config.WebApiIdentity).AsMainThread();
                return svc.IsSignedIn;
            }
            catch (Exception e)
            {
                var code = e is RequestFailedException failed ? failed.ErrorCode.ToString() : e.GetType().Name;
                CSDebug.LogWarning($"[Steam] UGS refused the Steam sign-in ({code}: {e.Message}); signing in anonymously.");
                return false;
            }
        }

        /// <summary>
        /// Links Steam to the signed-in account when it has no Steam identity yet. Never throws: the player is
        /// already signed in, and a link that fails is tried again next launch.
        /// </summary>
        public static async void LinkInBackground(IAuthenticationService svc)
        {
            try
            {
                if (!svc.IsSignedIn) return;

                var info = svc.PlayerInfo ?? await svc.GetPlayerInfoAsync().AsMainThread();
                if (!string.IsNullOrEmpty(info?.GetSteamId())) return;

                var ticket = await SteamPlatform.GetWebApiTicketAsync(Config.WebApiIdentity, Config.TicketTimeoutSeconds).AsMainThread();
                if (string.IsNullOrEmpty(ticket) || !svc.IsSignedIn) return;

                await svc.LinkWithSteamAsync(ticket, Config.WebApiIdentity).AsMainThread();
                CSDebug.LogVerbose(CSLogChannel.Boot, "[Steam] Linked Steam to this account.");
            }
            catch (AuthenticationException e) when (e.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
            {
                // This Steam account already has its own UGS account (the player played on another install before
                // this one kept a session). Moving the Steam id here would orphan that account's progress, and
                // switching accounts mid-session would reload everything, so the session stays as it is. The
                // next launch without a cached session signs in to the Steam account.
                CSDebug.LogWarning("[Steam] This Steam account is already tied to another UGS account; staying on this one.");
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[Steam] Could not link Steam to this account ({e.Message}); trying again next launch.");
            }
        }
    }
}
