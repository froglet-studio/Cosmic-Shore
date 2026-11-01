using System;
using System.Threading.Tasks;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Services.Authentication;
using Unity.Services.Core;

namespace CosmicShore.Core
{
    /// <summary>
    /// Ties an Android player's UGS account to their Google Play Games account (fair play, Threat Board T1), the way
    /// <see cref="SteamSignIn"/> and <see cref="GameCenterSignIn"/> do: with no cached session, sign in WITH Play Games
    /// so a reinstall lands on the same account; with one, resume it and LINK Play Games to it. Uses only Play Games'
    /// own automatic sign-in, so nothing is shown. Off while <see cref="MobileSignInConfigSO.SignInWithPlayGames"/> is
    /// off and on every platform but Android. Every failure falls back to the anonymous behaviour.
    /// </summary>
    public static class PlayGamesSignIn
    {
        static MobileSignInConfigSO Config => MobileSignInConfigSO.LoadOrDefault();

        public static bool IsEnabled => PlayGamesPlatform.IsSupported && Config.SignInWithPlayGames;

        public static async Task<bool> TrySignInAsync(IAuthenticationService svc)
        {
            var code = await PlayGamesPlatform.GetServerAuthCodeAsync(Config.PlayGamesWebClientId, Config.PlayGamesTimeoutSeconds)
                .AsMainThread();
            if (string.IsNullOrEmpty(code))
                return false;

            try
            {
                await svc.SignInWithGooglePlayGamesAsync(code).AsMainThread();
                return svc.IsSignedIn;
            }
            catch (Exception e)
            {
                var reason = e is RequestFailedException failed ? failed.ErrorCode.ToString() : e.GetType().Name;
                CSDebug.LogWarning($"[PlayGames] UGS refused the Play Games sign-in ({reason}: {e.Message}); signing in anonymously.");
                return false;
            }
        }

        /// <summary>Links Play Games to the signed-in account when it has none yet. Never throws.</summary>
        public static async void LinkInBackground(IAuthenticationService svc)
        {
            try
            {
                if (!svc.IsSignedIn) return;

                var info = svc.PlayerInfo ?? await svc.GetPlayerInfoAsync().AsMainThread();
                if (!string.IsNullOrEmpty(info?.GetGooglePlayGamesId())) return;

                var code = await PlayGamesPlatform.GetServerAuthCodeAsync(Config.PlayGamesWebClientId, Config.PlayGamesTimeoutSeconds)
                    .AsMainThread();
                if (string.IsNullOrEmpty(code) || !svc.IsSignedIn) return;

                await svc.LinkWithGooglePlayGamesAsync(code).AsMainThread();
                CSDebug.LogVerbose(CSLogChannel.Boot, "[PlayGames] Linked Play Games to this account.");
            }
            catch (AuthenticationException e) when (e.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
            {
                // Same rule as SteamSignIn: never move the identity off its own account mid-session.
                CSDebug.LogWarning("[PlayGames] This Play Games account is already tied to another UGS account; staying on this one.");
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[PlayGames] Could not link Play Games to this account ({e.Message}); trying again next launch.");
            }
        }
    }
}
