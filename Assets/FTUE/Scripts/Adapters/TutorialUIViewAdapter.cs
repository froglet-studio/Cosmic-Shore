// TutorialUIViewAdapter.cs
using CosmicShore.FTUE;
using System;
using UnityEngine;

[AddComponentMenu("FTUE/Adapters/TutorialUIViewAdapter")]
public class TutorialUIViewAdapter : MonoBehaviour, ITutorialUIView
{
    [SerializeField] private TutorialUIView _inner;

    public void ShowStep(string text, bool showArrow, Action onComplete)
        => _inner.ShowStep(text, showArrow, onComplete);

    public void ToggleCanvas(bool visible)
        => _inner.ToggleFTUECanvas(visible);
}
