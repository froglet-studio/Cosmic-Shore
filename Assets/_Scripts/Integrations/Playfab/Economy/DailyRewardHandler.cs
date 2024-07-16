using System.Collections.Generic;
using CosmicShore.Integrations.PlayFab.Authentication;
using CosmicShore.Integrations.PlayFab.Utility;
using CosmicShore.Utility.Singleton;
using PlayFab;
using PlayFab.CloudScriptModels;
using UnityEngine;

namespace CosmicShore.Integrations.PlayFab.CloudScripts
{
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
                new ExecuteFunctionRequest //Set this to true if you would like this call to show up in PlayStream
                {
                    Entity = _entity,
                    FunctionName = "Claim", //This should be the name of your Azure Function that you created.
                    GeneratePlayStreamEvent = false //Set this to true if you would like this call to show up in PlayStream
                };
            
            PlayFabCloudScriptAPI.ExecuteFunction(request, OnSaveRewardClaimTimeSuccess, PlayFabUtility.HandleErrorReport);
        }
        
        

        /// <summary>
        /// On Saving Daily Reward Claim Time Delegate
        /// </summary>
        /// <param name="result">ExecuteFunctionResult</param>
        private void OnSaveRewardClaimTimeSuccess(ExecuteFunctionResult result)
        {
            if (result.FunctionResultTooLarge ?? false)
            {
                Debug.LogError("Cloud script - This can happen if you exceed the limit that can be returned from an Azure Function, See PlayFab Limits Page for details.");
                return;
            }
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
                Debug.LogError($"Cloud script - Exceed the Azure Function.");
                return;
            }
            
            Debug.Log($"Cloud script - The {result.FunctionName} function took {result.ExecutionTimeMilliseconds} to complete");
            Debug.Log($"Cloud script - Result: {result.FunctionResult}");
        }

        /// <summary>
        /// A test script, nothing to see here.
        /// </summary>
        private void CallHelloWorldCloudScript()
        {
      
            var request =
                new ExecuteFunctionRequest //Set this to true if you would like this call to show up in PlayStream
            {
                Entity = _entity,
                FunctionName = "HelloWorld", //This should be the name of your Azure Function that you created.
                FunctionParameter = new Dictionary<string, object>
                    { { "inputValue", "Test" } }, //This is the data that you would want to pass into your function.
                GeneratePlayStreamEvent = false //Set this to true if you would like this call to show up in PlayStream
            };

            PlayFabCloudScriptAPI.ExecuteFunction(request, OnHelloWorldSuccess, PlayFabUtility.HandleErrorReport);
        }

        private void OnHelloWorldSuccess(ExecuteFunctionResult result)
        {
            if (result.FunctionResultTooLarge ?? false)
            {
                Debug.Log("Cloud script - This can happen if you exceed the limit that can be returned from an Azure Function, See PlayFab Limits Page for details.");
                return;
            }
            Debug.Log($"Cloud script - The {result.FunctionName} function took {result.ExecutionTimeMilliseconds} to complete");
            Debug.Log($"Cloud script - Result: {result.FunctionResult}");
        }
    }
}
