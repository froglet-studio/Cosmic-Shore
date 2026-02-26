using CosmicShore.Core;
using System;
using System.Collections;

namespace CosmicShore.Core
{
    public interface ITutorialExecutor
    {
        void SetupPreIntroUI();
        void PrepareArcadeScreen();
        IEnumerator ExecutePayload(TutorialStepPayload payload, Action onComplete);
    }
}
