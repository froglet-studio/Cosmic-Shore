using System.Collections.Generic;
using CosmicShore.Integrations.PlayFab.Authentication;
using CosmicShore.Integrations.PlayFab.Utility;
using PlayFab;
using PlayFab.CloudScriptModels;
using UnityEngine;

namespace CosmicShore.Integrations.PlayFab.CloudScripts
{
    public class AzureFunctionExample : MonoBehaviour
    {
        private static string _entityId;
        private static string _entityType;
        public void Start()
        {
            AuthenticationManager.OnLoginSuccess += InitEntity;
            AuthenticationManager.OnLoginSuccess += CallSaveRewardClaimTime;
        }

        public void OnDisable()
        {
            AuthenticationManager.OnLoginSuccess -= CallSaveRewardClaimTime;
            AuthenticationManager.OnLoginSuccess -= InitEntity;
        }

        private void InitEntity()
        {
            _entityId = AuthenticationManager.PlayFabAccount.AuthContext.EntityId;
            _entityType = AuthenticationManager.PlayFabAccount.AuthContext.EntityType;
        }

        private void CallSaveRewardClaimTime()
        {
            var request =
                new ExecuteFunctionRequest //Set this to true if you would like this call to show up in PlayStream
                {
                    Entity = new EntityKey
                    {
                        Id = _entityId, //Get this from when you logged in,
                        Type = _entityType //Get this from when you logged in
                    },
                    FunctionName = "SaveRewardClaimTime", //This should be the name of your Azure Function that you created.
                    GeneratePlayStreamEvent = false //Set this to true if you would like this call to show up in PlayStream
                };
            
            PlayFabCloudScriptAPI.ExecuteFunction(request, OnSaveRewardClaimTimeSuccess, PlayFabUtility.HandleErrorReport);
        }

        private void OnSaveRewardClaimTimeSuccess(ExecuteFunctionResult result)
        {
            if (result.FunctionResultTooLarge ?? false)
            {
                Debug.Log("Cloud script - This can happen if you exceed the limit that can be returned from an Azure Function, See PlayFab Limits Page for details.");
                return;
            }
            Debug.Log($"Cloud script - The {result.FunctionName} function took {result.ExecutionTimeMilliseconds} to complete");
            Debug.Log($"Cloud script - Result: {result.FunctionResult}");
        }

        private void CallHelloWorldCloudScript()
        {
      
            var request =
                new ExecuteFunctionRequest //Set this to true if you would like this call to show up in PlayStream
            {
                Entity = new EntityKey
                {
                    Id = _entityId, //Get this from when you logged in,
                    Type = _entityType //Get this from when you logged in
                },
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
