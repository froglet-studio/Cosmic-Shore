using CosmicShore.Core;

namespace CosmicShore.Core
{
    public interface ITutorialStepExecutor
    {
        void ExecutePayload(TutorialStepPayload payload, System.Action onComplete);
    }
}
