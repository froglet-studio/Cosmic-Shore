using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Views
{
    /// One card already placed in the scene. Set its fields in the Inspector.
    public sealed class ShipCardView : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] int number;                   // your “slot/number”
        [SerializeField] ScriptableObject soShip;     
        [SerializeField] VesselClassType vesselClass;  // set this explicitly for reliability

        [Header("UI")]
        [SerializeField] Button button;
        [SerializeField] TMP_Text label;
        [SerializeField] Image icon;                   // optional
        [SerializeField] Sprite activeIcon;            // optional
        [SerializeField] Sprite inactiveIcon;          // optional
        [SerializeField] GameObject selectedMarker;    // optional tick/highlight

        public int Number => number;
        public ScriptableObject SoShip => soShip;
        public VesselClassType VesselClass => vesselClass;
        public Button Button => button;

        public event Action<ShipCardView> Clicked;

        void Awake()
        {
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => Clicked?.Invoke(this));
            }

            if (label != null)
                label.text = (vesselClass != VesselClassType.Random ? vesselClass.ToString() : $"SHIP {number}")
                    .ToUpperInvariant();

            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (selectedMarker) selectedMarker.SetActive(selected);
            if (icon)
                icon.sprite = selected ? (activeIcon ? activeIcon : icon.sprite)
                    : (inactiveIcon ? inactiveIcon : icon.sprite);
        }
    }
}