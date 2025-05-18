using System;

public interface ITutorialUIView
{
    void ShowStep(string text, Action onComplete);
    void ToggleCanvas(bool visible);
}