using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Generic integer stepper. Displays a count with +/- buttons, clamped
    /// between configurable min and max. Reused by the arcade configure modal
    /// for both player-count and domain-count selection.
    /// </summary>
    public class IntStepper : MonoBehaviour
    {
        [SerializeField] Button decrementButton;
        [SerializeField] Button incrementButton;
        [SerializeField] TMP_Text countText;

        int _min = 1;
        int _max = 12;
        int _current = 1;
        bool _interactable = true;

        public int Value => _current;

        public event Action<int> OnValueChanged;

        void OnEnable()
        {
            decrementButton.onClick.AddListener(Decrement);
            incrementButton.onClick.AddListener(Increment);
        }

        void OnDisable()
        {
            decrementButton.onClick.RemoveListener(Decrement);
            incrementButton.onClick.RemoveListener(Increment);
        }

        public void Initialize(int min, int max, int startValue)
        {
            _min = min;
            _max = max;
            _current = Mathf.Clamp(startValue, _min, _max);
            Refresh();
        }

        public void SetValue(int value)
        {
            _current = Mathf.Clamp(value, _min, _max);
            Refresh();
        }

        public void Increment()
        {
            if (_current >= _max) return;
            _current++;
            Refresh();
            OnValueChanged?.Invoke(_current);
        }

        public void Decrement()
        {
            if (_current <= _min) return;
            _current--;
            Refresh();
            OnValueChanged?.Invoke(_current);
        }

        /// <summary>
        /// Enable or disable the +/- buttons without hiding the stepper.
        /// The count text remains visible so clients can see the host's value.
        /// </summary>
        public void SetInteractable(bool interactable)
        {
            _interactable = interactable;
            Refresh();
        }

        void Refresh()
        {
            if (countText) countText.text = _current.ToString();
            if (decrementButton) decrementButton.interactable = _interactable && _current > _min;
            if (incrementButton) incrementButton.interactable = _interactable && _current < _max;
        }
    }
}
