using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.SOAP;
using UnityEngine;

namespace CosmicShore.App.UI.Views
{
    [Serializable]
    public struct ShipSelectionSlot
    {
        public VesselClassType vesselType;
        public ShipSelectionItemView itemView;
    }

    /// <summary>
    /// Type-driven ship selection view.
    /// - Each slot is bound to a VesselClassType.
    /// - GameDataSO holds the selected vessel type and index.
    /// - shipsCatalog provides the SO_Ship data per class.
    /// </summary>
    public class ShipSelectionView : View
    {
        [Header("Layout")]
        [SerializeField] private Transform shipSelectionGrid;           // parent, for MenuAudio
        [SerializeField] private List<ShipSelectionSlot> slots = new(); // wired in inspector

        [Header("Data")]
        [SerializeField] private GameDataSO gameData;                   // the shared GameDataSO
        [SerializeField] private List<SO_Ship> shipsCatalog = new();    // all ships, one per class

        [SerializeField] private bool verboseLogging;

        public delegate void SelectionCallback(SO_Ship ship);
        public SelectionCallback OnSelect;

        MenuAudio _menuAudio;
        Dictionary<VesselClassType, SO_Ship> _shipsByClass;

        void Awake()
        {
            if (shipSelectionGrid != null)
                _menuAudio = shipSelectionGrid.GetComponent<MenuAudio>();
        }

        void OnEnable()
        {
            EnsureLookup();

            // Normalise selection from GameDataSO on open
            if (gameData != null && gameData.selectedVesselClass != null)
            {
                var current = gameData.selectedVesselClass.Value;

                if (!IsValidSelectedType(current))
                {
                    var def = GetDefaultType();
                    if (verboseLogging)
                        Debug.Log($"[ShipSelectionView] Normalizing selected vessel from {current} to {def}.");

                    gameData.selectedVesselClass.Value = def;
                    if (gameData.VesselClassSelectedIndex != null)
                        gameData.VesselClassSelectedIndex.Value = (int)def;
                }
            }

            // Initial paint
            UpdateView();
        }

        void EnsureLookup()
        {
            if (_shipsByClass != null) return;

            _shipsByClass = new Dictionary<VesselClassType, SO_Ship>();

            foreach (var ship in shipsCatalog)
            {
                if (!ship) continue;

                if (_shipsByClass.ContainsKey(ship.Class))
                {
                    if (verboseLogging)
                        Debug.LogWarning($"[ShipSelectionView] Duplicate ship class in catalog: {ship.Class}");
                    continue;
                }

                _shipsByClass[ship.Class] = ship;
            }

            if (verboseLogging)
                Debug.Log($"[ShipSelectionView] Built ships lookup with {_shipsByClass.Count} entries.");
        }

        bool IsValidSelectedType(VesselClassType type)
        {
            if (_shipsByClass == null || _shipsByClass.Count == 0)
                return false;

            if (type == VesselClassType.Any || type == VesselClassType.Random)
                return false;

            return _shipsByClass.ContainsKey(type);
        }

        VesselClassType GetDefaultType()
        {
            EnsureLookup();
            if (_shipsByClass == null || _shipsByClass.Count == 0)
                return VesselClassType.Dolphin; // fallback if catalog misconfigured

            // Prefer Dolphin if available
            if (_shipsByClass.ContainsKey(VesselClassType.Dolphin))
                return VesselClassType.Dolphin;

            // Otherwise, just take the first available
            return _shipsByClass.Keys.First();
        }

        public override void Select(int index)
        {
            // Base view is still index-based, we keep this for compatibility,
            // but real source of truth is GameDataSO.selectedVesselClass.
            base.Select(index);
            OnSelect?.Invoke(SelectedModel as SO_Ship);
        }

        public override void UpdateView()
        {
            EnsureLookup();

            if (slots == null || slots.Count == 0 || gameData == null || _shipsByClass == null)
                return;

            var selectedType = gameData.selectedVesselClass != null
                ? gameData.selectedVesselClass.Value
                : GetDefaultType();

            if (!IsValidSelectedType(selectedType))
                selectedType = GetDefaultType();

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];

                if (!slot.itemView)
                    continue;

                if (!_shipsByClass.TryGetValue(slot.vesselType, out var ship) || !ship)
                {
                    if (verboseLogging)
                        Debug.Log($"[ShipSelectionView] No ship in catalog for {slot.vesselType}, hiding slot {i}.");
                    slot.itemView.Clear();
                    continue;
                }

                bool isSelected = (selectedType == slot.vesselType);

                if (verboseLogging)
                    Debug.Log($"[ShipSelectionView] Slot {i} ({slot.vesselType}) → {ship.Name} (selected={isSelected})");

                var capturedType = slot.vesselType;
                var capturedShip = ship;

                slot.itemView.Configure(
                    capturedShip,
                    isSelected,
                    () => HandleSlotClicked(capturedType, capturedShip));
            }
        }

        void HandleSlotClicked(VesselClassType vesselType, SO_Ship ship)
        {
            if (gameData != null && gameData.selectedVesselClass != null)
            {
                gameData.selectedVesselClass.Value = vesselType;

                if (gameData.VesselClassSelectedIndex != null)
                    gameData.VesselClassSelectedIndex.Value = (int)vesselType;
            }

            if (verboseLogging)
                Debug.Log($"[ShipSelectionView] Clicked {vesselType}, index {(int)vesselType}");

            OnSelect?.Invoke(ship);
            _menuAudio?.PlayAudio();

            // Repaint selection state
            UpdateView();
        }
    }
}
