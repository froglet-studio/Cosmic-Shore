using System;

public interface ITutorialUIView
{
    void ShowStep(string text, bool showArrow, Action onComplete);
    void ToggleCanvas(bool visible);
}