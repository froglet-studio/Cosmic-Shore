// TutorialUIViewAdapter.cs
using CosmicShore.FTUE.Interfaces;
using CosmicShore.FTUE.UI;
using System;
using UnityEngine;

namespace CosmicShore.FTUE.Adapters
{
    [AddComponentMenu("FTUE/Adapters/TutorialUIViewAdapter")]
    public class TutorialUIViewAdapter : MonoBehaviour, ITutorialUIView
    {
        [SerializeField] private TutorialUIView _inner;

        public void ShowStep(string text, Action onComplete)
            => _inner.ShowStep(text, onComplete);

        public void ToggleCanvas(bool visible)
            => _inner.ToggleFTUECanvas(visible);
    }
}
