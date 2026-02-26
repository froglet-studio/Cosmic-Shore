using System;

namespace CosmicShore.Core
{
    public interface ITutorialUIView
    {
        void ShowStep(string text, Action onComplete);
        void ToggleCanvas(bool visible);
    }
}
