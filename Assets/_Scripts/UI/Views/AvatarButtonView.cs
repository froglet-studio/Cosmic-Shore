using System;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.Profile
{
    public class AvatarButtonView : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private GameObject selectedHighlight;

        Button _button;

        void Awake()
        {
            _button = GetComponent<Button>();
        }

        public void Initialize(Sprite icon, bool isSelected, Action onClick)
        {
            if (iconImage)
                iconImage.sprite = icon;

            SetSelected(isSelected);

            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(() => onClick?.Invoke());
            }
        }

        public void SetSelected(bool selected)
        {
            if (selectedHighlight)
                selectedHighlight.SetActive(selected);
        }
    }
}