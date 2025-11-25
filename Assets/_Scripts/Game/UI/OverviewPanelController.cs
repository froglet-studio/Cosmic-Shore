using System;
using System.Collections.Generic;
using CosmicShore.App.UI.Views;
using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine;
using CosmicShore.SOAP;
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
            public float Speed;
            public bool WasAIPilot;
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

        // ---------------------------------------------------------
        // OPEN PANEL
        // ---------------------------------------------------------
        public void Open()
        {
            if (_cards.Count == 0) CollectCards();

            var currentClass = gameData?.selectedVesselClass ? 
                               gameData.selectedVesselClass.Value :
                               CurrentVessel.VesselStatus.VesselType;

            _selectedCard = null;

            foreach (var c in _cards)
            {
                bool isSel = (c.VesselClass == currentClass);
                c.SetSelected(isSel);

                if (isSel) _selectedCard = c;
            }

            if (_selectedCard == null && _cards.Count > 0)
            {
                _selectedCard = _cards[0];
                _selectedCard.SetSelected(true);
                PushSelectionToGameData(_selectedCard);
            }

            SaveSnapshotAndActivateAIMode();
            ui.Show();
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
        private void SaveSnapshotAndActivateAIMode()
        {
            var vs = CurrentVessel.VesselStatus;

            snap.Pose = new Pose(vs.Transform.position, vs.Transform.rotation);
            snap.BlockRot = vs.blockRotation;
            snap.Course = vs.Course;
            snap.Speed = vs.Speed;
            snap.WasAIPilot = vs.AutoPilotEnabled;

            if (!snap.WasAIPilot)
                CurrentVessel.ToggleAIPilot(true);

            Player.InputController.SetPause(true);
        }
        
        // ---------------------------------------------------------
        // RESUME CLICK
        // ---------------------------------------------------------
        public void OnResumeButtonClicked()
        {
            if (_selectedCard == null)
            {
                GoBackToGame();
                return;
            }

            var targetClass = _selectedCard.VesselClass;
            var currentClass = CurrentVessel.VesselStatus.VesselType;

            PushSelectionToGameData(_selectedCard);

            if (targetClass == currentClass)
            {
                GoBackToGame();
                return;
            }

            // Spawn new ship
            if (!vesselSpawner.SpawnShip(targetClass, out var newVessel))
            {
                Debug.LogError($"Failed to spawn {targetClass}");
                GoBackToGame();
                return;
            }

            var old = CurrentVessel;

            newVessel.Initialize(Player, snap.WasAIPilot);
            PlayerVesselInitializeHelper.SetShipProperties(themeManagerData, newVessel);

            Player.ChangeVessel(newVessel);
            Player.ResetForPlay();

            newVessel.SetPose(snap.Pose);
            newVessel.VesselStatus.blockRotation = snap.BlockRot;
            newVessel.VesselStatus.Course = snap.Course;

            Player.StartPlayer();
            old.DestroyVessel();
            
            GoBackToGame();
        }

        public void OnCloseButtonClicked() => GoBackToGame();

        public void HideUI()
        {
            ui.Hide();
            CurrentVessel?.ToggleAIPilot(snap.WasAIPilot);
        }

        void GoBackToGame()
        {
            ui.Hide();
            ActivatePlayerWithDelay().Forget();
        }
        
        async UniTaskVoid ActivatePlayerWithDelay()
        {
            await UniTask.Yield();
            CurrentVessel?.ToggleAIPilot(snap.WasAIPilot);
            Player?.InputController.SetPause(false);
        }
    }
}