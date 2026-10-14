using System;
using System.IO;
using System.Threading.Tasks;
using CosmicShore.Engine.Networking;

namespace CosmicShore.Player
{
    /// <summary>
    /// How the player reaches a relay, from its environment (docs/MULTIPLAYER.md §6.7-6.8):
    ///
    ///   COSMIC_SHORE_RELAY            off (default) | a relay server's allocations URL | ugs
    ///   COSMIC_SHORE_RELAY_SECRET     the bearer token Froglet's relay server was started with
    ///   COSMIC_SHORE_RELAY_REGION     a relay region id; unset lets the service choose
    ///   COSMIC_SHORE_UGS_PROJECT      the UGS project id; unset reads cloudProjectId from ProjectSettings.asset
    ///   COSMIC_SHORE_UGS_ENVIRONMENT  a UGS environment name; unset is the project's default, as the game uses
    ///   COSMIC_SHORE_UGS_AUTH_URL / COSMIC_SHORE_UGS_RELAY_URL  stand-ins for UGS's endpoints (tests)
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
            return new UgsAuthentication(project, Env("COSMIC_SHORE_UGS_ENVIRONMENT"),
                Path.Combine(CosmicShore.Engine.Application.persistentDataPath, tokenName),
                Env("COSMIC_SHORE_UGS_AUTH_URL") ?? UgsAuthentication.UgsBaseUrl);
        }

        public static RelayAllocationClient UgsRelay(UgsAuthentication auth)
            => new(Env("COSMIC_SHORE_UGS_RELAY_URL") ?? RelayAllocationClient.UgsBaseUrl, auth.GetAccessTokenAsync);

        /// <summary>Installs the relay COSMIC_SHORE_RELAY names, if any. Returns a line for the console, or null.</summary>
        public static string InstallRelay(string projectRoot)
        {
            var relay = Env("COSMIC_SHORE_RELAY");
            if (relay == null || relay == "off") return null;
            RelaySessions.Region = Env("COSMIC_SHORE_RELAY_REGION");
            if (relay == "ugs")
            {
                UgsAuthentication auth;
                try { auth = SignIn(projectRoot, "ugs-session-token"); }
                catch (InvalidOperationException e) { return "[relay] COSMIC_SHORE_RELAY=ugs: " + e.Message + "; using direct connections"; }
                RelaySessions.Install(UgsRelay(auth));
                return $"[relay] sessions go through UGS Relay (project {auth.ProjectId}{(auth.Environment != null ? ", environment " + auth.Environment : "")}); the first host signs in";
            }
            var secret = Env("COSMIC_SHORE_RELAY_SECRET");
            RelaySessions.Install(new RelayAllocationClient(relay, secret == null ? null : () => Task.FromResult(secret)));
            return $"[relay] sessions go through the relay at {relay}";
        }
    }
}
