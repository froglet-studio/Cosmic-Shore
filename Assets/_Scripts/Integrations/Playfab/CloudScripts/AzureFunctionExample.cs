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
        public void Start()
        {
            AuthenticationManager.OnLoginSuccess += CallHelloWorldCloudScript;
        }

        public void OnDisable()
        {
            AuthenticationManager.OnLoginSuccess -= CallHelloWorldCloudScript;
        }
        
        private void 

        private void CallHelloWorldCloudScript()
        {
            var entityId = AuthenticationManager.PlayFabAccount.AuthContext.EntityId;
            var entityType = AuthenticationManager.PlayFabAccount.AuthContext.EntityType;

            var request =
                new ExecuteFunctionRequest //Set this to true if you would like this call to show up in PlayStream
            {
                Entity = new EntityKey
                {
                    Id = entityId, //Get this from when you logged in,
                    Type = entityType //Get this from when you logged in
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
