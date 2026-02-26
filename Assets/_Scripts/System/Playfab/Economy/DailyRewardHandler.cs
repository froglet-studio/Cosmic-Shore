using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Utility;
using PlayFab.CloudScriptModels;
using UnityEngine;
using CosmicShore.Data;
namespace CosmicShore.Core
{
    /// <summary>
    /// TODO: Generalize function execution
    /// </summary>
    public class DailyRewardHandler : SingletonPersistent<DailyRewardHandler>
    {
        private static EntityKey _entity;
        public void Start()
        {
            // [PLAYFAB DISABLED] Daily rewards will be rebuilt on UGS. Pending removal.
            return;

            AuthenticationManager.OnLoginSuccess += InitEntity;
        }

        public void OnDisable()
        {
            AuthenticationManager.OnLoginSuccess -= InitEntity;
        }

        /// <summary>
        /// Initialize entity key for cloud script authentication upon login
        /// </summary>
        private static void InitEntity()
        {
            _entity = new()
            {
                Id = AuthenticationManager.PlayFabAccount.AuthContext.EntityId,
                Type = AuthenticationManager.PlayFabAccount.AuthContext.EntityType
            };
        }

        /// <summary>
        /// Execute SaveRewardClaimTime Azure Function
        /// Returns UpdateUserInternalDataResult and nextClaimTime if request is successful.
        /// </summary>
        public void Claim()
        {
            var functionProperties = new FunctionProperties
            {
                FunctionName = "Claim",
                EntityKey = _entity
            };
            
            CloudScriptRunner.Execute(functionProperties, OnClaimDailyRewardSuccess);
        }

        /// <summary>
        /// On Saving Daily Reward Claim Time Delegate
        /// </summary>
        /// <param name="result">ExecuteFunctionResult</param>
        private void OnClaimDailyRewardSuccess(ExecuteFunctionResult result)
        {
            if (result.FunctionResultTooLarge ?? false)
            {
                CSDebug.LogError("Cloud script - This can happen if you exceed the limit that can be returned from an Azure Function, See PlayFab Limits Page for details.");
                return;
            }

            CatalogManager.Instance.RewardClaimed(Element.Omni, CatalogManager.DailyRewardAmount);

#if !UNITY_WEBGL
            //FirebaseAnalytics.
#endif

            CSDebug.Log($"Cloud script - The {result.FunctionName} function took {result.ExecutionTimeMilliseconds} to complete");
            CSDebug.Log($"Cloud script - Result: {result.FunctionResult}");
        }

        /// <summary>
        /// Runs granting bundle items to player inventory
        /// </summary>
        /// <param name="itemIds"> A list of item ids from PlayFab</param>
        public void GrantBundle(string[] itemIds)
        {
            var functionProperties = new FunctionProperties
            {
                FunctionName = "AddItemsToInventory",
                EntityKey = _entity,
                FunctionParameter = new Dictionary<string, object> { { "itemIds", itemIds } }
            };
            
            // No action needed for on success callback, leave it null to use the default on success callback
            CloudScriptRunner.Execute(functionProperties);
        }

        public void ClaimDailyChallengeReward(int tier, int rewardValue)
        {
            var functionProperties = new FunctionProperties
            {
                FunctionName = "ClaimDailyChallengeReward",
                EntityKey = _entity,
                FunctionParameter = new Dictionary<string, object> { { "tier", tier }, { "rewardValue", rewardValue } },

            };

            // TODO: P1 need to do this in the on success callback - extend the backend to return the reward value granted
            CatalogManager.Instance.RewardClaimed(Element.Omni, rewardValue);

            CSDebug.Log($"ClaimDailyChallengeReward(int tier, int rewardValue) - tier:{tier}, value:{rewardValue}");
            CloudScriptRunner.Execute(functionProperties);
        }

        /// <summary>
        /// Claim Daily Challenge Reward result returns if the claim is successful and a time available for the next claim
        /// IsClaimed, nextClaimTime
        /// </summary>
        /// <param name="result">Function execution result</param>
        void OnClaimDailyChallengeRewardSuccess(ExecuteFunctionResult result)
        {
            CSDebug.Log("DailyRewardHandler - OnClaimDailyChallengeRewardSuccess");
            if (result.FunctionResultTooLarge ?? false)
            {
                CSDebug.LogError("Cloud script - This can happen if you exceed the limit that can be returned from an Azure Function, See PlayFab Limits Page for details.");
                return;
            }
            
            CSDebug.Log($"Cloud script - The {result.FunctionName} function took {result.ExecutionTimeMilliseconds} to complete");
            CSDebug.Log($"Cloud script - Result: {result.FunctionResult}");
        }

        /// <summary>
        /// Play Daily Challenge checks if the player has enough balance to play
        /// And subtract balance by 1 if the balance is sufficient
        /// </summary>
        public void PlayDailyChallenge(Action<ExecuteFunctionResult> playDailyChallengeSuccess)
        {
            var functionProperties = new FunctionProperties
            {
                FunctionName = "PlayDailyChallenge",
                EntityKey = _entity
            };
            
            CloudScriptRunner.Execute(functionProperties, playDailyChallengeSuccess);
        }

        
    }
}
