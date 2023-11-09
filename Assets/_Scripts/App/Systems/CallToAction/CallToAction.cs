using System.Collections.Generic;

public class CallToAction
{
    public CallToActionTargetID CallToActionTargetID { get; set; }
    public UserAction CompletionUserAction { get; set; }
    public List<CallToActionTargetID> DependencyTargetIDs { get; set; }

    public CallToAction(CallToActionTargetID callToActionTargetID, UserAction completionUserAction, List<CallToActionTargetID> dependencyTargets=null)
    {
        CallToActionTargetID = callToActionTargetID;
        CompletionUserAction = completionUserAction;

        if (dependencyTargets != null)
            DependencyTargetIDs = dependencyTargets;
        else
            DependencyTargetIDs = new ();
    }
}