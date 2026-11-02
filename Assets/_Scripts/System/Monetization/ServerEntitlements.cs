using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Services.CloudCode;

namespace CosmicShore.Core
{
    /// <summary>
    /// Whether the server owns episode tokens and episode ownership (Threat Board T3). Off until the
    /// <c>entitlements</c> Cloud Code script (Tools/UGS/Commerce/entitlements.js) is deployed to the UGS environment the
    /// build signs into (Tools/UGS/README.md); with it off, <see cref="EpisodeTokenService"/> keeps its local wallet and
    /// nothing calls Cloud Code. Flip it in the same change that confirms the deploy.
    /// </summary>
    public static class ServerEntitlementGate
    {
        public static readonly bool Enabled = false;
    }

    /// <summary>The record as the server holds it after a request. Field names match entitlements.js.</summary>
    [Serializable]
    public class ServerEntitlementsResult
    {
        public bool ok;
        public string reason;
        public int tokens;
        public long lifetimeTokensPurchased;
        public long lifetimeTokensSpent;
        public List<string> episodes = new();
        /// <summary>Orders already credited, as "provider:orderId".</summary>
        public List<string> orders = new();
    }

    /// <summary>
    /// The client half of the server-owned entitlements: one Cloud Code endpoint, <c>entitlements</c>. The server
    /// credits tokens only for an order its storefront confirmed, counted from its own table, and sells episodes for
    /// tokens. Calls run one at a time; a failed call returns null and the caller keeps what it showed.
    /// </summary>
    public static class ServerEntitlements
    {
        public const string Endpoint = "entitlements";
        static readonly SemaphoreSlim s_oneAtATime = new(1, 1);

        public static UniTask<ServerEntitlementsResult> StateAsync() =>
            CallAsync(new Dictionary<string, object> { { "action", "state" } });

        /// <summary>Asks the server to verify an order with its storefront and credit it (once per order).</summary>
        public static UniTask<ServerEntitlementsResult> RedeemOrderAsync(string provider, string orderId, string productId) =>
            CallAsync(new Dictionary<string, object>
                { { "action", "redeemOrder" }, { "provider", provider ?? string.Empty },
                  { "orderId", orderId ?? string.Empty }, { "productId", productId ?? string.Empty } });

        public static UniTask<ServerEntitlementsResult> UnlockEpisodeAsync(string episodeId) =>
            CallAsync(new Dictionary<string, object> { { "action", "unlockEpisode" }, { "episode", episodeId ?? string.Empty } });

        static async UniTask<ServerEntitlementsResult> CallAsync(Dictionary<string, object> args)
        {
            await s_oneAtATime.WaitAsync();
            try
            {
                var result = await CloudCodeService.Instance
                    .CallEndpointAsync<ServerEntitlementsResult>(Endpoint, args)
                    .AsMainThread();
                if (result is { ok: false })
                    CSDebug.LogVerbose(CSLogChannel.CloudData, $"[ServerEntitlements] {args["action"]} refused: {result.reason}.");
                return result;
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[ServerEntitlements] {args["action"]} could not reach the server: {e.Message}");
                return null;
            }
            finally
            {
                s_oneAtATime.Release();
            }
        }
    }
}
