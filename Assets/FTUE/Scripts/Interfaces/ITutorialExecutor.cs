using CosmicShore.FTUE.Data;
using System;
using System.Collections;

namespace CosmicShore.FTUE.Interfaces
{
    public interface ITutorialExecutor
    {
        void SetupPreIntroUI();
        void PrepareArcadeScreen();
        IEnumerator ExecutePayload(TutorialStepPayload payload, Action onComplete);
    }
}
