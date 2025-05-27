using CosmicShore.FTUE;

public interface ITutorialStepExecutor
{
    void ExecutePayload(TutorialStepPayload payload, System.Action onComplete);
}
