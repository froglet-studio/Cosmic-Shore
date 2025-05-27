using CosmicShore.FTUE;
using System.Collections;

public interface ITutorialStepHandler
{
    TutorialStepType HandlesType { get; }
    IEnumerator ExecuteStep(TutorialStep step, IFlowController controller);
}
