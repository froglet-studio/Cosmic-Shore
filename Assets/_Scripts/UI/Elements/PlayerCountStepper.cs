using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Stepper control for selecting player count.
    /// Displays a count with +/- buttons, clamped between min and max.
    /// </summary>
    public class PlayerCountStepper : MonoBehaviour
    {
        [SerializeField] Button decrementButton;
        [SerializeField] Button incrementButton;
        [SerializeField] TMP_Text countText;

        int _min = 1;
        int _max = 12;
        int _current = 1;

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

        void Increment()
        {
            if (_current >= _max) return;
            _current++;
            Refresh();
            OnValueChanged?.Invoke(_current);
        }

        void Decrement()
        {
            if (_current <= _min) return;
            _current--;
            Refresh();
            OnValueChanged?.Invoke(_current);
        }

        void Refresh()
        {
            if (countText) countText.text = _current.ToString();
            if (decrementButton) decrementButton.interactable = _current > _min;
            if (incrementButton) incrementButton.interactable = _current < _max;
        }
    }
}
