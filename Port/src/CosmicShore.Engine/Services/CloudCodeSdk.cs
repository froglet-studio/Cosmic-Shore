// ─────────────────────────────────────────────────────────────────────────────
// CloudCodeSdk.cs — engine placeholder surface for the UGS Cloud Code SDK
// (original contract: Unity.Services.CloudCode — CloudCodeService.Instance.
// CallEndpointAsync<TResult>). Added so the game's server-owned wallet client
// (Assets/_Scripts/System/CloudData/ServerEconomy.cs, fair play T3) compiles in
// Amoebius. There is no Cloud Code server in a single process, so a call fails
// the way an unreachable service does; the game treats that as "keep the local
// wallet" and the feature is gated off (ServerEconomyGate) until deployed.
// Tests swap a fake into the settable Instance.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CosmicShore.Engine.Services.CloudCode
{
    public interface ICloudCodeService
    {
        Task<TResult> CallEndpointAsync<TResult>(string function, Dictionary<string, object> args = null);
    }

    public sealed class CloudCodeException : Exception
    {
        public CloudCodeException(string message) : base(message) { }
    }

    /// <summary>The default: no Cloud Code server is reachable from a single process.</summary>
    public sealed class UnreachableCloudCodeService : ICloudCodeService
    {
        public Task<TResult> CallEndpointAsync<TResult>(string function, Dictionary<string, object> args = null) =>
            Task.FromException<TResult>(new CloudCodeException($"Cloud Code endpoint '{function}' is not available in Amoebius."));
    }

    public static class CloudCodeService
    {
        public static ICloudCodeService Instance { get; set; } = new UnreachableCloudCodeService();
    }
}
