using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using UnityEngine;
using System.Linq;

namespace CosmicShore.UI
{
    public class PortSquadMemberConfigureView : ModalWindowManager
    {
        [SerializeField] SquadMemberCard squadMemberCard;
        [SerializeField] PortSquadCaptainSelectionView captainSelectView;
        [SerializeField] ShipSelectionView shipSelectionView;
        [SerializeField] PortSquadView squadView;
        [SerializeField] PortSquadView homeSquadView;

        // TODO: Need to pull this from inventory
        [SerializeField] SO_VesselList PlayerShips;

        SO_Captain SelectedCaptain;

        void OnEnable()
        {
            if (shipSelectionView == null || captainSelectView == null || squadMemberCard == null) return;

            shipSelectionView.OnSelect += captainSelectView.AssignModel;
            shipSelectionView.OnSelect += squadMemberCard.SetShip;

            captainSelectView.OnSelect += squadMemberCard.SetCaptain;
            captainSelectView.OnSelect += SelectCaptain;
        }

        void OnDisable()
        {
            if (shipSelectionView == null || captainSelectView == null || squadMemberCard == null) return;

            shipSelectionView.OnSelect -= captainSelectView.AssignModel;
            shipSelectionView.OnSelect -= squadMemberCard.SetShip;

            captainSelectView.OnSelect -= squadMemberCard.SetCaptain;
            captainSelectView.OnSelect -= SelectCaptain;
        }

        protected override void Start()
        {
            if (shipSelectionView != null && PlayerShips != null)
                shipSelectionView.AssignModels(PlayerShips.VesselList.ConvertAll(x => (ScriptableObject)x));

            base.Start();
        }

        public void InitializeView(SO_Captain captain, bool isPlayer) 
        {
            shipSelectionView.Select(PlayerShips.VesselList.IndexOf(captain.Vessel));
            captainSelectView.IsPlayer = isPlayer;
            squadMemberCard.SetShip(captain.Vessel);

            squadMemberCard.SetCaptain(captain);
            captainSelectView.AssignModel(captain.Vessel);
            captainSelectView.SetSelectedCaptain(captain);
        }

        public void SelectCaptain(SO_Captain captain)
        {
            SelectedCaptain = captain;
        }

        public void ConfirmCaptain()
        {
            squadView.AssignCaptain(SelectedCaptain);
            homeSquadView.UpdateView();
        }
    }
}