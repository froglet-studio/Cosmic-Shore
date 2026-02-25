using CosmicShore.FTUE.Data;

namespace CosmicShore.FTUE.Interfaces
{
    public interface ITutorialStepExecutor
    {
        void ExecutePayload(TutorialStepPayload payload, System.Action onComplete);
    }
}
