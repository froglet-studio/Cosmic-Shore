using CosmicShore.FTUE;
using System;
using System.Collections;

public interface ITutorialExecutor
{
    void SetupPreIntroUI();
    void PrepareArcadeScreen();
    IEnumerator ExecutePayload(TutorialStepPayload payload, Action onComplete);
}