using System.Threading.Tasks;
using Unity.Services.Authentication;

namespace CosmicShore.Core
{
    /// <summary>
    /// The one platform sign-in a launch uses (fair play, Threat Board T1): Steam on PC (<see cref="SteamSignIn"/>),
    /// Game Center on iOS (<see cref="GameCenterSignIn"/>). <see cref="AuthenticationServiceFacade"/> asks only this,
    /// so a new platform joins here and nowhere else. Off, every launch signs in anonymously as before.
    /// </summary>
    public static class PlatformSignIn
    {
        public static bool IsEnabled => SteamSignIn.IsEnabled || GameCenterSignIn.IsEnabled;

        public static Task<bool> TrySignInAsync(IAuthenticationService svc)
        {
            if (SteamSignIn.IsEnabled) return SteamSignIn.TrySignInAsync(svc);
            if (GameCenterSignIn.IsEnabled) return GameCenterSignIn.TrySignInAsync(svc);
            return Task.FromResult(false);
        }

        public static void LinkInBackground(IAuthenticationService svc)
        {
            if (SteamSignIn.IsEnabled) SteamSignIn.LinkInBackground(svc);
            else if (GameCenterSignIn.IsEnabled) GameCenterSignIn.LinkInBackground(svc);
        }
    }
}
