using System;
using System.Collections.Generic;
using CosmicShore.Integrations.PlayFab.Utility;
using PlayFab;
using PlayFab.CloudScriptModels;
using UnityEngine;

namespace CosmicShore.Integrations.PlayFab.CloudScripts
{
    public class FunctionProperties
    {
        public string FunctionName { get; set; }
        public EntityKey EntityKey { get; set; }
        public Dictionary<string, object> FunctionParameter { get; set; } = null;
    }
    public class CloudScriptRunner
    {
        /// <summary>
        /// Executes cloud script
        /// // TODO: Replace daily reward handler's api calls to this generalized one.
        /// </summary>
        /// <param name="properties">Function essential properties</param>
        /// <param name="success">Action to be taken when the function runs successfully</param>
        public static void Execute(FunctionProperties properties, Action<ExecuteFunctionResult> success = null)
        {
            ExecuteFunctionRequest request;
            if (properties.FunctionParameter == null)
            {
                request = new ExecuteFunctionRequest
                {
                    Entity = properties.EntityKey,
                    FunctionName = properties.FunctionName,
                    GeneratePlayStreamEvent = false
                };
            }
            else
            {
                request = new ExecuteFunctionRequest
                {
                    Entity = properties.EntityKey,
                    FunctionName = properties.FunctionName,
                    FunctionParameter = properties.FunctionParameter,
                    GeneratePlayStreamEvent = false
                };
            }

            PlayFabCloudScriptAPI.ExecuteFunction(request, success ?? OnSuccess, PlayFabUtility.HandleErrorReport);
        }
        
        /// <summary>
        /// Default OnSuccess action for executing cloud script
        /// </summary>
        /// <param name="result"></param>
        private static void OnSuccess(ExecuteFunctionResult result)
        {
            if (result.FunctionResultTooLarge ?? false)
            {
                Debug.LogError("CloudScriptRunner - This can happen if you exceed the limit that can be returned from an Azure Function, See PlayFab Limits Page for details.");
                return;
            }

            Debug.Log($"CloudScriptRunner - The {result.FunctionName} function took {result.ExecutionTimeMilliseconds} to complete");
            Debug.Log($"CloudScriptRunner - Result: {result.FunctionResult}");
        }
    }
}
