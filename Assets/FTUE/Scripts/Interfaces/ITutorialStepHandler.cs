using CosmicShore.Core;
using System.Collections;

namespace CosmicShore.Core
{
    public interface ITutorialStepHandler
    {
        TutorialStepType HandlesType { get; }
        IEnumerator ExecuteStep(TutorialStep step, IFlowController controller);
    }
}
