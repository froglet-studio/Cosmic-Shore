using System.Collections.Generic;
using CosmicShore.Integrations.PlayFab.Authentication;
using CosmicShore.Integrations.PlayFab.Economy;
using CosmicShore.Integrations.PlayFab.Utility;
using CosmicShore.Utility.Singleton;
using PlayFab;
using PlayFab.CloudScriptModels;
using UnityEngine;

namespace CosmicShore.Integrations.PlayFab.CloudScripts
{
    /// <summary>
    /// TODO: Generalize function execution
    /// </summary>
    public class DailyRewardHandler : SingletonPersistent<DailyRewardHandler>
    {
        private static EntityKey _entity;
        public void Start()
        {
            AuthenticationManager.OnLoginSuccess += InitEntity;
            // AuthenticationManager.OnLoginSuccess += CallSaveRewardClaimTime;
        }

        public void OnDisable()
        {
            // AuthenticationManager.OnLoginSuccess -= CallSaveRewardClaimTime;
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
            var request =
                new ExecuteFunctionRequest
                {
                    Entity = _entity,
                    FunctionName = "Claim", //This should be the name of your Azure Function that you created.
                    GeneratePlayStreamEvent = false //Set this to true if you would like this call to show up in PlayStream
                };
            
            PlayFabCloudScriptAPI.ExecuteFunction(request, OnClaimDailyRewardSuccess, PlayFabUtility.HandleErrorReport);
        }

        /// <summary>
        /// On Saving Daily Reward Claim Time Delegate
        /// </summary>
        /// <param name="result">ExecuteFunctionResult</param>
        private void OnClaimDailyRewardSuccess(ExecuteFunctionResult result)
        {
            if (result.FunctionResultTooLarge ?? false)
            {
                Debug.LogError("Cloud script - This can happen if you exceed the limit that can be returned from an Azure Function, See PlayFab Limits Page for details.");
                return;
            }

            CatalogManager.Instance.RewardClaimed(Element.Omni, CatalogManager.Instance.GetDailyChallengeTicket().Amount);

            Debug.Log($"Cloud script - The {result.FunctionName} function took {result.ExecutionTimeMilliseconds} to complete");
            Debug.Log($"Cloud script - Result: {result.FunctionResult}");
        }

        public void GrantBundle(string[] itemIds)
        {
            var request = new ExecuteFunctionRequest
            {
                Entity = _entity,
                FunctionName = "AddItemsToInventory",
                FunctionParameter = new Dictionary<string, object> { { "itemIds", itemIds } },
                GeneratePlayStreamEvent = false
            };
            
            PlayFabCloudScriptAPI.ExecuteFunction(request, OnGrantBundleSuccess, PlayFabUtility.HandleErrorReport);
        }

        private void OnGrantBundleSuccess(ExecuteFunctionResult result)
        {
            if (result.FunctionResultTooLarge ?? false)
            {
                Debug.LogError("Cloud script - Exceed the Azure Function.");
                return;
            }
            
            Debug.Log($"Cloud script - The {result.FunctionName} function took {result.ExecutionTimeMilliseconds} to complete");
            Debug.Log($"Cloud script - Result: {result.FunctionResult}");
        }

        public void ClaimDailyChallengeReward(int tier, int rewardValue)
        {
            var request = new ExecuteFunctionRequest
            {
                Entity = _entity,
                FunctionName = "ClaimDailyChallengeReward",
                FunctionParameter = new Dictionary<string, object> { { "tier", tier }, { "rewardValue", rewardValue } },
                GeneratePlayStreamEvent = false
            };

            // TODO: P1 need to do this in the on success callback - extend the backend to return the reward value granted
            CatalogManager.Instance.RewardClaimed(Element.Omni, rewardValue);

            Debug.Log($"ClaimDailyChallengeReward(int tier, int rewardValue) - tier:{tier}, value:{rewardValue}");
            PlayFabCloudScriptAPI.ExecuteFunction(request, OnClaimDailyChallengeRewardSuccess, PlayFabUtility.HandleErrorReport);
        }

        /// <summary>
        /// Claim Daily Challenge Reward result returns if the claim is successful and a time available for the next claim
        /// IsClaimed, nextClaimTime
        /// </summary>
        /// <param name="result">Function execution result</param>
        void OnClaimDailyChallengeRewardSuccess(ExecuteFunctionResult result)
        {
            Debug.Log("DailyRewardHandler - OnClaimDailyChallengeRewardSuccess");
            if (result.FunctionResultTooLarge ?? false)
            {
                Debug.LogError("Cloud script - Exceed the Azure Function.");
                return;
            }
            
            Debug.Log($"Cloud script - The {result.FunctionName} function took {result.ExecutionTimeMilliseconds} to complete");
            Debug.Log($"Cloud script - Result: {result.FunctionResult}");
        }

        /// <summary>
        /// Play Daily Challenge checks if the player has enough balance to play
        /// And subtract balance by 1 if the balance is sufficient
        /// </summary>
        public void PlayDailyChallenge()
        {
            var request = new ExecuteFunctionRequest
            {
                Entity = _entity,
                FunctionName = "PlayDailyChallenge",
                GeneratePlayStreamEvent = false
            };
            
            PlayFabCloudScriptAPI.ExecuteFunction(request, OnPlayDailyChallengeSuccess, PlayFabUtility.HandleErrorReport);
        }

        /// <summary>
        /// Play Daily Challenge function execution successful result
        /// Returns bool CanPlay, int remainingBalance
        /// </summary>
        /// <param name="result">Function execution result</param>
        private void OnPlayDailyChallengeSuccess(ExecuteFunctionResult result)
        {
            Debug.Log("DailyRewardHandler - OnPlayDailyChallengeSuccess");
            if (result.FunctionResultTooLarge ?? false)
            {
                Debug.LogError("Cloud script - Exceed the Azure Function.");
                return;
            }
            
            // TODO: Invoke the result if needed from the UI and Daily Reward System
            
            Debug.Log($"Cloud script - The {result.FunctionName} function took {result.ExecutionTimeMilliseconds} to complete");
            Debug.Log($"Cloud script - Result: {result.FunctionResult}");
        }
    }
}
