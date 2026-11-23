using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One pip of <see cref="IntensityStepper"/>: an intensity level the modal drives exactly as it drives
    /// the legacy <see cref="IntensitySelectButton"/> (select, lock, offered range). It draws nothing of its
    /// own - the stepper colours all four pips together, since a pip lights for every level up to the picked
    /// one - so every state hook just asks the stepper to redraw.
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Intensity Pip")]
    public class KitIntensityPip : IntensitySelectButton
    {
        [Tooltip("The stepper this pip belongs to.")]
        [SerializeField] private IntensityStepper stepper;
        [Tooltip("The pip's bar.")]
        [SerializeField] private Image fill;

        public Image Fill => fill;

        // The pip's click is its Select - wired here, so the prefab carries no persistent call to drift.
        private void Awake()
        {
            if (TryGetComponent(out Button button)) button.onClick.AddListener(Select);
        }

        protected override void DrawLevel(int intensity) => Notify();
        protected override void DrawSelected(bool selected) => Notify();
        protected override void DrawActive(bool active) => Notify();
        protected override void DrawLocked(bool locked) => Notify();

        void Notify()
        {
            if (stepper) stepper.Redraw();
        }
    }
}
