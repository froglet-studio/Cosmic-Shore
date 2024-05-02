using System.Collections.Generic;
using PlayFab;
using PlayFab.CloudScriptModels;
using UnityEngine;

namespace CosmicShore.Integrations.Playfab.CloudScripts
{
    public class AzureFunctionExample : MonoBehaviour
    {
        public void Start()
        {
            CallCloudScript();
        }

        private void CallCloudScript()
        {
            PlayFabCloudScriptAPI.ExecuteFunction(new ExecuteFunctionRequest
            {
                Entity = new EntityKey
                {
                    Id = PlayFabSettings.staticPlayer.EntityId, //Get this from when you logged in,
                    Type = PlayFabSettings.staticPlayer.EntityType, //Get this from when you logged in
                },
                FunctionName = "HelloWorld", //This should be the name of your Azure Function that you created.
                FunctionParameter = new Dictionary<string, object> { { "inputValue", "Test" } }, //This is the data that you would want to pass into your function.
                GeneratePlayStreamEvent = false //Set this to true if you would like this call to show up in PlayStream
            }, result =>
            {
                if (result.FunctionResultTooLarge ?? false)
                {
                    Debug.Log("This can happen if you exceed the limit that can be returned from an Azure Function, See PlayFab Limits Page for details.");
                    return;
                }
                Debug.Log($"The {result.FunctionName} function took {result.ExecutionTimeMilliseconds} to complete");
                Debug.Log($"Result: {result.FunctionResult}");
            }, error =>
            {
                Debug.Log($"Something went wrong: {error.GenerateErrorReport()}");
            });
        }
    }
}
