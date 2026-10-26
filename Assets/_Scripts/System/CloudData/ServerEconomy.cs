using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Services.CloudCode;

namespace CosmicShore.Core
{
    /// <summary>
    /// Whether the server owns the crystal wallet (fair play, Threat Board T3; Docs/AntiCheat/README.md §4.2).
    /// Off until the <c>economy</c> Cloud Code script is deployed to the UGS environment the build signs into
    /// (Tools/UGS/README.md); with it off, <see cref="CosmicShore.UI.PlayerDataService"/> keeps the old
    /// client-written wallet and nothing calls Cloud Code. Flip it in the same change that confirms the deploy.
    /// </summary>
    public static class ServerEconomyGate
    {
        public static readonly bool Enabled = false;
    }

    /// <summary>The wallet as the server holds it after a request. Field names match Tools/UGS/CloudCode/economy.js.</summary>
    [Serializable]
    public class ServerEconomyResult
    {
        public bool ok;
        public string reason;
        public int crystalBalance;
        public long lifetimeCrystalsEarned;
        public long lifetimeCrystalsSpent;
        public List<string> unlockedRewardIds = new();
    }

    /// <summary>
    /// The client half of the server-owned wallet: one Cloud Code endpoint, <c>economy</c>, that checks every
    /// earn, spend and once-only claim against its own rules and answers with the wallet as it now stands.
    /// Calls run one at a time, so answers arrive in the order the requests were made. A failed call (offline,
    /// service down) returns null and the caller keeps its local value until the next answer.
    /// </summary>
    public static class ServerEconomy
    {
        public const string Endpoint = "economy";
        static readonly SemaphoreSlim s_oneAtATime = new(1, 1);

        public static UniTask<ServerEconomyResult> StateAsync() =>
            CallAsync(new Dictionary<string, object> { { "action", "state" } });

        public static UniTask<ServerEconomyResult> EarnAsync(string source, int amount) =>
            CallAsync(new Dictionary<string, object> { { "action", "earn" }, { "source", source }, { "amount", amount } });

        public static UniTask<ServerEconomyResult> SpendAsync(string source, int amount, string item) =>
            CallAsync(new Dictionary<string, object>
                { { "action", "spend" }, { "source", source }, { "amount", amount }, { "item", item ?? string.Empty } });

        public static UniTask<ServerEconomyResult> ClaimAsync(string rewardId, int amount) =>
            CallAsync(new Dictionary<string, object> { { "action", "claim" }, { "rewardId", rewardId }, { "amount", amount } });

        static async UniTask<ServerEconomyResult> CallAsync(Dictionary<string, object> args)
        {
            await s_oneAtATime.WaitAsync();
            try
            {
                var result = await CloudCodeService.Instance
                    .CallEndpointAsync<ServerEconomyResult>(Endpoint, args)
                    .AsMainThread();
                if (result is { ok: false })
                    CSDebug.LogVerbose(CSLogChannel.CloudData,
                        $"[ServerEconomy] {args["action"]} refused: {result.reason}. Balance is {result.crystalBalance}.");
                return result;
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[ServerEconomy] {args["action"]} could not reach the server: {e.Message}");
                return null;
            }
            finally
            {
                s_oneAtATime.Release();
            }
        }
    }
}
