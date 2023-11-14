using CosmicShore.App.Systems.UserActions;
using System;
using System.Collections.Generic;

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

            if (dependencyTargets != null)
                DependencyTargetIDs = dependencyTargets;
            else
                DependencyTargetIDs = new();
        }
    }
}