using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Vessel selection panel for Menu_Main freestyle mode.
    ///
    /// Reuses <see cref="OverviewPanelUI"/> for presentation and <see cref="ShipCardView"/>
    /// for individual vessel cards. Delegates the actual vessel swap to
    /// <see cref="MenuVesselSwapController"/> which handles the Netcode despawn/spawn/RPC
    /// pipeline so the change replicates to all clients.
    ///
    /// Opened via a UI button in the freestyle HUD (wired in the scene inspector).
    /// The panel shows while the vessel flies on autopilot. Selecting a card and
    /// pressing "Resume" triggers the network swap, then restores freestyle control
    /// on the new vessel.
    /// </summary>
    public sealed class MenuOverviewPanelController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] OverviewPanelUI ui;
        [SerializeField] MenuVesselSwapController vesselSwapController;
        [SerializeField] MenuCrystalClickHandler crystalClickHandler;

        [Header("SOAP Events")]
        [SerializeField] MenuFreestyleEventsContainerSO freestyleEvents;

        [Header("Timing")]
        [Tooltip("Delay in ms after requesting a swap before restoring freestyle control. " +
                 "Must be long enough for the server to despawn/spawn and the client to initialize.")]
        [SerializeField] int restoreFreestyleDelayMs = 600;

        [Inject] GameDataSO gameData;

        IPlayer Player => gameData?.LocalPlayer;
        IVessel CurrentVessel => Player?.Vessel;

        readonly List<ShipCardView> _cards = new();
        ShipCardView _selectedCard;
        CancellationTokenSource _cts;

        // ---------------------------------------------------------
        // Unity
        // ---------------------------------------------------------
        void Awake()
        {
            CollectCards();
            ui.Hide();
        }

        void OnEnable()
        {
            _cts = new CancellationTokenSource();
            if (freestyleEvents?.OnExitFreestyle)
                freestyleEvents.OnExitFreestyle.OnRaised += OnExitFreestyle;
        }

        void OnDisable()
        {
            if (freestyleEvents?.OnExitFreestyle)
                freestyleEvents.OnExitFreestyle.OnRaised -= OnExitFreestyle;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        // ---------------------------------------------------------
        // OPEN PANEL (called from freestyle HUD button)
        // ---------------------------------------------------------

        /// <summary>
        /// Opens the vessel selection panel. Puts the local vessel into autopilot
        /// while the player browses vessel options.
        /// </summary>
        public void Open()
        {
            if (Player?.Vessel == null) return;
            if (!Player.IsLocalUser) return;

            // Put vessel on autopilot while browsing
            Player.Vessel.ToggleAIPilot(true);
            Player.InputController.SetPause(true);

            EnsureCardsCollected();
            DetermineCurrentSelection();
            EnsureFallbackSelection();
            ui.Show();
        }

        // ---------------------------------------------------------
        // RESUME CLICK (apply selection and close)
        // ---------------------------------------------------------
        public void OnResumeButtonClicked()
        {
            if (!_selectedCard)
            {
                CloseAndRestoreFreestyle();
                return;
            }

            var targetClass = _selectedCard.VesselClass;
            var currentClass = CurrentVessel?.VesselStatus.VesselType ?? VesselClassType.Any;

            if (targetClass != currentClass && !vesselSwapController.IsSwapping)
            {
                vesselSwapController.RequestSwap(targetClass);
                ui.Hide();

                // The swap puts the new vessel in autopilot.
                // Wait for the swap to complete, then restore freestyle control.
                RestoreFreestyleAfterSwapAsync(_cts.Token).Forget();
                return;
            }

            CloseAndRestoreFreestyle();
        }

        // ---------------------------------------------------------
        // CLOSE PANEL (no changes)
        // ---------------------------------------------------------
        public void OnCloseButtonClicked() => CloseAndRestoreFreestyle();

        // ---------------------------------------------------------
        // Auto-close when exiting freestyle
        // ---------------------------------------------------------
        void OnExitFreestyle() => ui.Hide();

        // ---------------------------------------------------------
        // FREESTYLE RESTORE
        // ---------------------------------------------------------

        void CloseAndRestoreFreestyle()
        {
            ui.Hide();

            // Restore player control since we paused input when opening
            if (Player?.Vessel != null && crystalClickHandler && crystalClickHandler.IsInFreestyle)
            {
                Player.Vessel.ToggleAIPilot(false);
                Player.InputController.SetPause(false);
            }
        }

        async UniTaskVoid RestoreFreestyleAfterSwapAsync(CancellationToken ct)
        {
            // Wait for the swap to complete (server despawn + spawn + client init)
            await UniTask.Delay(restoreFreestyleDelayMs, ignoreTimeScale: true, cancellationToken: ct);

            // Poll until the swap controller reports done
            const int maxAttempts = 20;
            const int intervalMs = 100;
            for (int i = 0; i < maxAttempts && vesselSwapController.IsSwapping; i++)
                await UniTask.Delay(intervalMs, ignoreTimeScale: true, cancellationToken: ct);

            // If still in freestyle mode, give player control of the new vessel
            if (crystalClickHandler && crystalClickHandler.IsInFreestyle
                && Player?.Vessel != null)
            {
                Player.Vessel.ToggleAIPilot(false);
                Player.InputController.SetPause(false);
            }
        }

        // ---------------------------------------------------------
        // CARD CLICKED
        // ---------------------------------------------------------
        void OnCardClicked(ShipCardView card)
        {
            _selectedCard = card;

            foreach (var c in _cards)
                c.SetSelected(c == card);
        }

        // ---------------------------------------------------------
        // CARD COLLECTION
        // ---------------------------------------------------------
        void CollectCards()
        {
            _cards.Clear();

            for (int i = 0; i < ui.ShipCardContainer.childCount; i++)
            {
                var card = ui.ShipCardContainer.GetChild(i).GetComponent<ShipCardView>();
                if (!card) continue;

                card.Clicked -= OnCardClicked;
                card.Clicked += OnCardClicked;
                _cards.Add(card);
            }
        }

        void EnsureCardsCollected()
        {
            if (_cards.Count == 0)
                CollectCards();
        }

        void DetermineCurrentSelection()
        {
            var currentClass = CurrentVessel.VesselStatus.VesselType;
            _selectedCard = null;

            foreach (var card in _cards)
            {
                bool isSelected = card.VesselClass == currentClass;
                card.SetSelected(isSelected);

                if (isSelected)
                    _selectedCard = card;
            }
        }

        void EnsureFallbackSelection()
        {
            if (_selectedCard != null) return;
            if (_cards.Count == 0) return;

            _selectedCard = _cards[0];
            _selectedCard.SetSelected(true);
        }
    }
}
