using System;

namespace CosmicShore.FTUE.Interfaces
{
    public interface ITutorialUIView
    {
        void ShowStep(string text, Action onComplete);
        void ToggleCanvas(bool visible);
    }
}
