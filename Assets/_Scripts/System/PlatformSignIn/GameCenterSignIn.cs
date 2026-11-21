using System;
using System.Threading.Tasks;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Services.Authentication;
using Unity.Services.Core;

namespace CosmicShore.Core
{
    /// <summary>
    /// Ties an iOS player's UGS account to their Game Center account (fair play, Threat Board T1), the way
    /// <see cref="SteamSignIn"/> does on PC: with no cached session, sign in WITH Game Center so a reinstall lands on
    /// the same account; with one, resume it and LINK Game Center to it. Game Center signs in silently (Apple asks
    /// the player once, in Settings), so nothing is shown. Off while
    /// <see cref="MobileSignInConfigSO.SignInWithGameCenter"/> is off and on every platform but iOS. Every failure
    /// falls back to the anonymous behaviour.
    /// </summary>
    public static class GameCenterSignIn
    {
        const string IdentityType = "apple-game-center";

        static MobileSignInConfigSO Config => MobileSignInConfigSO.LoadOrDefault();

        public static bool IsEnabled => GameCenterPlatform.IsSupported && Config.SignInWithGameCenter;

        public static async Task<bool> TrySignInAsync(IAuthenticationService svc)
        {
            var id = await GameCenterPlatform.GetIdentityAsync(Config.GameCenterTimeoutSeconds).AsMainThread();
            if (id is not { } identity)
                return false;

            try
            {
                await svc.SignInWithAppleGameCenterAsync(identity.Signature, identity.TeamPlayerId, identity.PublicKeyUrl,
                    identity.Salt, identity.Timestamp).AsMainThread();
                return svc.IsSignedIn;
            }
            catch (Exception e)
            {
                var code = e is RequestFailedException failed ? failed.ErrorCode.ToString() : e.GetType().Name;
                CSDebug.LogWarning($"[GameCenter] UGS refused the Game Center sign-in ({code}: {e.Message}); signing in anonymously.");
                return false;
            }
        }

        /// <summary>Links Game Center to the signed-in account when it has none yet. Never throws.</summary>
        public static async void LinkInBackground(IAuthenticationService svc)
        {
            try
            {
                if (!svc.IsSignedIn) return;

                var info = svc.PlayerInfo ?? await svc.GetPlayerInfoAsync().AsMainThread();
                if (HasGameCenter(info)) return;

                var id = await GameCenterPlatform.GetIdentityAsync(Config.GameCenterTimeoutSeconds).AsMainThread();
                if (id is not { } identity || !svc.IsSignedIn) return;

                await svc.LinkWithAppleGameCenterAsync(identity.Signature, identity.TeamPlayerId, identity.PublicKeyUrl,
                    identity.Salt, identity.Timestamp).AsMainThread();
                CSDebug.LogVerbose(CSLogChannel.Boot, "[GameCenter] Linked Game Center to this account.");
            }
            catch (AuthenticationException e) when (e.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
            {
                // Same rule as SteamSignIn: never move the identity off its own account mid-session.
                CSDebug.LogWarning("[GameCenter] This Game Center account is already tied to another UGS account; staying on this one.");
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[GameCenter] Could not link Game Center to this account ({e.Message}); trying again next launch.");
            }
        }

        static bool HasGameCenter(PlayerInfo info)
        {
            if (info?.Identities == null) return false;
            foreach (var identity in info.Identities)
                if (identity != null && identity.TypeId == IdentityType && !string.IsNullOrEmpty(identity.UserId))
                    return true;
            return false;
        }
    }
}
