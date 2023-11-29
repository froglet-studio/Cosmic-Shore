using CosmicShore.App.Systems.UserActions;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.App.Systems.CTA
{
    [Serializable]
    public class CallToAction
    {
        public CallToActionTargetType CallToActionTargetID;
        public UserActionType CompletionUserAction;
        public List<CallToActionTargetType> DependencyTargetIDs;

        public CallToAction(CallToActionTargetType callToActionTargetID, UserActionType completionUserAction, List<CallToActionTargetType> dependencyTargets = null)
        {
            CallToActionTargetID = callToActionTargetID;
            CompletionUserAction = completionUserAction;
            Debug.LogFormat("{0} - {1} {2}",nameof(CallToAction), nameof(CallToActionTargetID), CallToActionTargetID);
            Debug.LogFormat("{0} - {1} {2}",nameof(CallToAction), nameof(CompletionUserAction), CompletionUserAction);

            if (dependencyTargets != null)
            {
                DependencyTargetIDs = dependencyTargets;
                Debug.LogFormat("{0} - {1} {2}",nameof(CallToAction), nameof(DependencyTargetIDs), CompletionUserAction);
            }
                
            else
                DependencyTargetIDs = new();
        }
    }
}