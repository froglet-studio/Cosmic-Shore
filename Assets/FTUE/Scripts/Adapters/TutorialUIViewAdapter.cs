// TutorialUIViewAdapter.cs
using CosmicShore.FTUE;
using System;
using UnityEngine;

[AddComponentMenu("FTUE/Adapters/TutorialUIViewAdapter")]
public class TutorialUIViewAdapter : MonoBehaviour, ITutorialUIView
{
    [SerializeField] private TutorialUIView _inner;

    public void ShowStep(string text, Action onComplete)
        => _inner.ShowStep(text, onComplete);

    public void ToggleCanvas(bool visible)
        => _inner.ToggleFTUECanvas(visible);
}
