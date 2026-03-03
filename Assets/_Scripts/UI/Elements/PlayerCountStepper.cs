using System;
using Obvious.Soap;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    public class PlayerCountStepper : MonoBehaviour
    {
        [SerializeField] TMP_Text countText;
        [SerializeField] Button incrementButton;
        [SerializeField] Button decrementButton;
        [SerializeField] IntVariable selectedPlayerCount;

        int _min = 1;
        int _max = 12;
        int _current = 1;

        public int Count => _current;
        public event Action<int> OnCountChanged;

        void OnEnable()
        {
            if (incrementButton) incrementButton.onClick.AddListener(Increment);
            if (decrementButton) decrementButton.onClick.AddListener(Decrement);
        }

        void OnDisable()
        {
            if (incrementButton) incrementButton.onClick.RemoveListener(Increment);
            if (decrementButton) decrementButton.onClick.RemoveListener(Decrement);
        }

        public void SetRange(int min, int max)
        {
            _min = Mathf.Max(1, min);
            _max = Mathf.Max(_min, max);
            SetCount(Mathf.Clamp(_current, _min, _max));
        }

        public void SetCount(int count)
        {
            _current = Mathf.Clamp(count, _min, _max);
            RefreshUI();
        }

        void Increment()
        {
            if (_current >= _max) return;
            _current++;
            RefreshUI();
            OnCountChanged?.Invoke(_current);
        }

        void Decrement()
        {
            if (_current <= _min) return;
            _current--;
            RefreshUI();
            OnCountChanged?.Invoke(_current);
        }

        void RefreshUI()
        {
            if (countText)
                countText.text = _current.ToString();

            if (selectedPlayerCount)
                selectedPlayerCount.Value = _current;

            if (decrementButton) decrementButton.interactable = _current > _min;
            if (incrementButton) incrementButton.interactable = _current < _max;
        }
    }
}
