using System.Collections.Generic;
using CosmicShore.App.UI.Views;
using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine;

// NEW
using CosmicShore.SOAP;

namespace CosmicShore.App.UI.Controllers
{
    public sealed class OverviewPanel : MonoBehaviour
    {
        [Header("Refs")] [SerializeField] OverviewPanelComponentReferences refs;
        [SerializeField] VesselSpawner vesselSpawner;
        [SerializeField] ThemeManagerDataContainerSO themeManagerData;

        [SerializeField] GameDataSO gameData;

        IPlayer Player => gameData?.LocalPlayer;
        IVessel CurrentVessel => Player?.Vessel;

        readonly List<ShipCardView> _cards = new();
        ShipCardView _selectedCard;

        struct Snapshot
        {
            public Pose Pose;
            public Quaternion BlockRot;
            public Vector3 Course;
            public float Speed;
            public bool WasAIPilot;
        }

        Snapshot snap;

        void Awake()
        {
            refs.ResumeButton.onClick.RemoveAllListeners();
            refs.CloseButton.onClick.RemoveAllListeners();
            refs.ResumeButton.onClick.AddListener(OnResumeClicked);
            refs.CloseButton.onClick.AddListener(CloseOnly);

            CollectCards();
            Hide();
        }

        void CollectCards()
        {
            _cards.Clear();
            if (!refs.ShipCardContainer) return;

            for (int i = 0; i < refs.ShipCardContainer.childCount; i++)
            {
                var card = refs.ShipCardContainer.GetChild(i).GetComponent<ShipCardView>();
                if (!card) continue;
                card.Clicked -= OnCardClicked;
                card.Clicked += OnCardClicked;
                _cards.Add(card);
            }
        }

        [ContextMenu("_Open")]
        public void Open()
        {
            if (_cards.Count == 0) CollectCards();

            var currentClass =
                CurrentVessel.VesselStatus.VesselType;

            if (gameData && gameData.selectedVesselClass)
                currentClass = gameData.selectedVesselClass.Value;

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

            SaveSnapshotAndFreeze();
            Show();
        }

        void OnCardClicked(ShipCardView card)
        {
            _selectedCard = card;
            foreach (var c in _cards)
                c.SetSelected(c == card);

            PushSelectionToGameData(card);
        }

        void PushSelectionToGameData(ShipCardView card)
        {
            if (!gameData) return;

            if (gameData.selectedVesselClass)
                gameData.selectedVesselClass.Value = card.VesselClass;

            if (!gameData.VesselClassSelectedIndex) return;
            int index = card.Number >= 0 ? card.Number : card.transform.GetSiblingIndex();
            gameData.VesselClassSelectedIndex.Value = index;
        }

        void SaveSnapshotAndFreeze()
        {
            var vs = CurrentVessel.VesselStatus;
            snap.Pose = new Pose(vs.Transform.position, vs.Transform.rotation);
            snap.BlockRot = vs.blockRotation;
            snap.Course = vs.Course;
            snap.Speed = vs.Speed;

            snap.WasAIPilot = Player?.Vessel.VesselStatus.AutoPilotEnabled ?? false;

            if (!snap.WasAIPilot) CurrentVessel.ToggleAIPilot(true);
            Player?.InputController.SetPause(true);
        }

        void RestoreAndUnfreeze()
        {
            CurrentVessel?.ToggleAIPilot(snap.WasAIPilot);
            Player?.InputController.SetPause(false);
        }

        void OnResumeClicked()
        {
            if (_selectedCard == null)
            {
                CloseOnly();
                return;
            }

            var targetClass = _selectedCard.VesselClass;
            var currentClass = CurrentVessel.VesselStatus.VesselType; 

            PushSelectionToGameData(_selectedCard);

            if (targetClass == currentClass)
            {
                CloseOnly();
                return;
            }

            if (!vesselSpawner.SpawnShip(targetClass, out var newVessel))
            {
                Debug.LogError($"[OverviewPanel] Failed to spawn {targetClass}");
                CloseOnly();
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
            newVessel.ToggleAIPilot(snap.WasAIPilot);
            Player.InputController.SetPause(snap.WasAIPilot);

            old?.DestroyVessel();

            Hide(); 
        }


        void CloseOnly()
        {
            RestoreAndUnfreeze();
            Hide();
        }

        void Show()
        {
            if (refs.PanelRoot) refs.PanelRoot.SetActive(true);
            if (!refs.PanelCanvasGroup) return;
            refs.PanelCanvasGroup.alpha = 1f;
            refs.PanelCanvasGroup.blocksRaycasts = true;
            refs.PanelCanvasGroup.interactable = true;
        }

        void Hide()
        {
            if (refs.PanelCanvasGroup)
            {
                refs.PanelCanvasGroup.alpha = 0f;
                refs.PanelCanvasGroup.blocksRaycasts = false;
                refs.PanelCanvasGroup.interactable = false;
            }

            if (refs.PanelRoot) refs.PanelRoot.SetActive(false);
        }
    }
}