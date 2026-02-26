using CosmicShore.FTUE.Data;
using System.Collections;

namespace CosmicShore.FTUE.Interfaces
{
    public interface ITutorialStepHandler
    {
        TutorialStepType HandlesType { get; }
        IEnumerator ExecuteStep(TutorialStep step, IFlowController controller);
    }
}
