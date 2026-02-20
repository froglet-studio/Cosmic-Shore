using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Views
{
    public class ShipSelectionItemView : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameLabel;
        //[SerializeField] private GameObject selectedHighlight;
        [SerializeField] private Button button;

        public void Configure(SO_Ship ship, bool isSelected, Action onClick)
        {
            if (!ship)
            {
                Clear();
                return;
            }

            gameObject.SetActive(true);

            if (iconImage)
                iconImage.sprite = isSelected ? ship.IconActive : ship.IconInactive;

            if (nameLabel)
                nameLabel.text = ship.Name.ToUpperInvariant();

            if (!button) return;
            button.onClick.RemoveAllListeners();
            if (onClick != null)
                button.onClick.AddListener(() => onClick());
        }

        public void Clear()
        {
            if (button)
                button.onClick.RemoveAllListeners();

            gameObject.SetActive(false);
        }
    }
}