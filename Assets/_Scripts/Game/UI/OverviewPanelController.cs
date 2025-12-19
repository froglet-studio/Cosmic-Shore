using System;
using System.Collections.Generic;
using CosmicShore.App.UI.Views;
using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine;
using CosmicShore.Soap;
using Cysharp.Threading.Tasks;


namespace CosmicShore.App.UI.Controllers
{
    public sealed class OverviewPanelController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private OverviewPanelUI ui;
        [SerializeField] private VesselSpawner vesselSpawner;
        [SerializeField] private ThemeManagerDataContainerSO themeManagerData;
        [SerializeField] private GameDataSO gameData;

        private IPlayer Player => gameData?.LocalPlayer;
        private IVessel CurrentVessel => Player?.Vessel;

        private readonly List<ShipCardView> _cards = new();
        private ShipCardView _selectedCard;

        struct Snapshot
        {
            public Pose Pose;
            public Quaternion BlockRot;
            public Vector3 Course;
            public bool WasAIPilot;
            public bool WasActive;
        }

        private Snapshot snap;

        // ---------------------------------------------------------
        // Unity
        // ---------------------------------------------------------
        private void Awake()
        {
            CollectCards();
            ui.Hide();
        }

        // ---------------------------------------------------------
        // OPEN PANEL
        // ---------------------------------------------------------
        public void Open()
        {
            EnsureCardsCollected();
            DetermineCurrentSelection();
            EnsureFallbackSelection();
            ApplySnapshotAndShowUI();
        }
        
        // ---------------------------------------------------------
        // RESUME CLICK
        // ---------------------------------------------------------
        public void OnResumeButtonClicked()
        {
            if (!_selectedCard)
            {
                GoBackToGame();
                return;
            }

            PushSelectionToGameData(_selectedCard);

            var targetClass = _selectedCard.VesselClass;
            var currentClass = CurrentVessel.VesselStatus.VesselType;

            if (IsSameVesselClass(targetClass, currentClass) || 
                !TrySpawnReplacementVessel(targetClass, out var newVessel))
            {
                GoBackToGame();
                return;
            }

            ReplacePlayerVessel(newVessel);
            GoBackToGame();
        }

        public void OnPauseButtonClicked() => ui.Hide();

        // ----------------------
        // Helper Methods
        // ----------------------

        private bool IsSameVesselClass(VesselClassType target, VesselClassType current) =>
            target == current;
        
        private bool TrySpawnReplacementVessel(VesselClassType targetClass, out IVessel newVessel)
        {
            if (vesselSpawner.SpawnShip(targetClass, out newVessel)) 
                return true;
            
            Debug.LogError($"Failed to spawn {targetClass}");
            return false;
        }
        
        private void ReplacePlayerVessel(IVessel newVessel)
        {
            var oldVessel = CurrentVessel;
            InitializeNewVessel(newVessel);
            TransferSnapshotStateToNewVessel(newVessel);
            ActivateNewPlayerVessel(newVessel);
            oldVessel.DestroyVessel();
        }
        
        private void InitializeNewVessel(IVessel newVessel)
        {
            Player.ChangeVessel(newVessel);
            newVessel.Initialize(Player);
            VesselInitializeHelper.SetShipProperties(themeManagerData, newVessel);
        }
        
        private void TransferSnapshotStateToNewVessel(IVessel newVessel)
        {
            newVessel.SetPose(snap.Pose);
            newVessel.VesselStatus.blockRotation = snap.BlockRot;
            newVessel.VesselStatus.Course = snap.Course;
        }

        private void ActivateNewPlayerVessel(IVessel newVessel)
        {
            if (!snap.WasActive)
                return;
            
            Player.ResetForPlay();
            Player.StartPlayer();
        }
        
        private void EnsureCardsCollected()
        {
            if (_cards.Count == 0)
                CollectCards();
        }

        private void DetermineCurrentSelection()
        {
            var currentClass = CurrentVessel.VesselStatus.VesselType;
            _selectedCard = null;

            foreach (var card in _cards)
            {
                bool isSelected = (card.VesselClass == currentClass);
                card.SetSelected(isSelected);

                if (isSelected)
                    _selectedCard = card;
            }
        }

        private void EnsureFallbackSelection()
        {
            if (_selectedCard != null)
                return;

            if (_cards.Count == 0)
                return;

            _selectedCard = _cards[0];
            _selectedCard.SetSelected(true);

            PushSelectionToGameData(_selectedCard);
        }

        private void ApplySnapshotAndShowUI()
        {
            SaveSnapshotOfCurrentVessel();
            ui.Show();
            ActivateAIModeInLocalVessel();
        }
        
        // ---------------------------------------------------------
        // CARD CLICKED
        // ---------------------------------------------------------
        private void OnCardClicked(ShipCardView card)
        {
            _selectedCard = card;

            foreach (var c in _cards)
                c.SetSelected(c == card);

            PushSelectionToGameData(card);
        }

        private void PushSelectionToGameData(ShipCardView card)
        {
            if (gameData.selectedVesselClass)
                gameData.selectedVesselClass.Value = card.VesselClass;

            if (gameData.VesselClassSelectedIndex)
                gameData.VesselClassSelectedIndex.Value =
                    card.Number >= 0 ? card.Number : card.transform.GetSiblingIndex();
        }

        // ---------------------------------------------------------
        // SNAPSHOT
        // ---------------------------------------------------------
        private void SaveSnapshotOfCurrentVessel()
        {
            var vs = CurrentVessel.VesselStatus;

            snap.Pose = new Pose(vs.Transform.position, vs.Transform.rotation);
            snap.BlockRot = vs.blockRotation;
            snap.Course = vs.Course;
            snap.WasAIPilot = vs.AutoPilotEnabled;
            snap.WasActive = vs.Player.IsActive;
        }

        void ActivateAIModeInLocalVessel()
        {
            if (!snap.WasActive)
                return;
            
            if (!snap.WasAIPilot)
                CurrentVessel.ToggleAIPilot(true);

            Player.InputController.SetPause(true);
        }

        void GoBackToGame()
        {
            ui.Hide();
            ActivatePlayerModeInLocalVesselWithDelay().Forget();
        }
        
        async UniTaskVoid ActivatePlayerModeInLocalVesselWithDelay()
        {
            if (!snap.WasActive)
                return;
            
            await UniTask.Yield();
            CurrentVessel?.ToggleAIPilot(snap.WasAIPilot);
            Player?.InputController.SetPause(false);
        }
        
        // ---------------------------------------------------------
        // CARD COLLECTION
        // ---------------------------------------------------------
        private void CollectCards()
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
    }
}