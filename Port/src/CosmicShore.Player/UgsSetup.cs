using System;
using System.IO;
using System.Threading.Tasks;
using CosmicShore.Engine.Networking;

namespace CosmicShore.Player
{
    /// <summary>
    /// How the player goes online (START ONLINE): UGS sign-in, UGS Relay and UGS Lobby, as the Unity build uses
    /// them (docs/MULTIPLAYER.md §6.8-6.10). From the environment:
    ///
    ///   COSMIC_SHORE_UGS_PROJECT      the UGS project id; unset reads cloudProjectId from ProjectSettings.asset
    ///   COSMIC_SHORE_UGS_ENVIRONMENT  the UGS environment; unset is Network.UgsEnvironment in
    ///                                 Port/ProjectSettings/AmoebiusProject.json, else "amoebius" ("" = the project's default)
    ///   COSMIC_SHORE_RELAY_REGION     a relay region id; unset lets UGS choose
    ///   COSMIC_SHORE_UGS_AUTH_URL / COSMIC_SHORE_UGS_RELAY_URL / COSMIC_SHORE_UGS_LOBBY_URL
    ///                                 stand-ins for UGS's endpoints (tests)
    /// </summary>
    static class UgsSetup
    {
        static string Env(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v.Trim() : null;

        public static string ProjectId(string projectRoot) => Env("COSMIC_SHORE_UGS_PROJECT") ?? UgsAuthentication.ReadUnityProjectId(projectRoot);

        /// <summary>
        /// A UGS sign-in for this save profile; its session token lives in the profile's folder under
        /// <paramref name="tokenName"/>, so the profile stays one UGS player across runs.
        /// </summary>
        public static UgsAuthentication SignIn(string projectRoot, string tokenName)
        {
            var project = ProjectId(projectRoot)
                ?? throw new InvalidOperationException("no UGS project id: set COSMIC_SHORE_UGS_PROJECT, or link the Unity project (cloudProjectId)");
            return new UgsAuthentication(project, EnvironmentName(projectRoot),
                Path.Combine(CosmicShore.Engine.Application.persistentDataPath, tokenName),
                Env("COSMIC_SHORE_UGS_AUTH_URL") ?? UgsAuthentication.UgsBaseUrl);
        }

        /// <summary>The UGS environment: the variable, else the branch's setting, else "amoebius". Empty = the project's default.</summary>
        public static string EnvironmentName(string projectRoot)
        {
            var v = Environment.GetEnvironmentVariable("COSMIC_SHORE_UGS_ENVIRONMENT")
                ?? (projectRoot != null ? Amoebius.AmoebiusProjectSettings.Load(projectRoot).Network.UgsEnvironment : null)
                ?? "amoebius";
            return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        }

        public static RelayAllocationClient UgsRelay(UgsAuthentication auth)
            => new(Env("COSMIC_SHORE_UGS_RELAY_URL") ?? RelayAllocationClient.UgsBaseUrl, auth.GetAccessTokenAsync);

        public static UgsLobbyClient UgsLobby(UgsAuthentication auth)
            => new(auth.GetAccessTokenAsync, Env("COSMIC_SHORE_UGS_LOBBY_URL") ?? UgsLobbyClient.UgsBaseUrl);

        /// <summary>
        /// Puts this player on UGS, in NetStack's order: the sign-in the game's anonymous sign-in uses (so its
        /// player id is its UGS one), UGS Relay over the transport, then UGS Lobby as the session service.
        /// </summary>
        public static void InstallOnline(string projectRoot)
        {
            UgsAuthentication auth;
            try { auth = SignIn(projectRoot, "ugs-session-token"); }
            catch (InvalidOperationException e)
            {
                Console.WriteLine("[online] " + e.Message + ": playing offline");
                return;
            }
            CosmicShore.Engine.Services.AuthenticationService.RemoteSignIn = async () =>
            {
                try { await auth.GetAccessTokenAsync(); }
                catch (Exception e)
                {
                    Console.WriteLine($"[online] UGS sign-in failed: {e.Message}");
                    Console.WriteLine($"[online] the UGS environment \"{auth.Environment ?? "(the project's default)"}\" must exist in the UGS dashboard " +
                                      "(or set SETTINGS > ONLINE > UGS environment); START plays offline meanwhile");
                    throw;
                }
                Console.WriteLine($"[online] signed in to UGS ({auth.LastSignIn}) as {auth.PlayerId}");
                return auth.PlayerId;
            };
            RelaySessions.Region = Env("COSMIC_SHORE_RELAY_REGION");
            NetStack.Install(Environment.GetEnvironmentVariable("COSMIC_SHORE_NET_TRANSPORT"),
                () => RelaySessions.Install(UgsRelay(auth)),
                () => MultiplayerService.Instance = new UgsMultiplayerService(UgsLobby(auth)));
            Console.WriteLine($"[online] UGS project {auth.ProjectId}, environment {auth.Environment ?? "(the project's default)"}: Lobby and Relay");
        }
    }
}
