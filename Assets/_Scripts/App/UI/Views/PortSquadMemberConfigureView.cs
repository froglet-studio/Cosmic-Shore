using CosmicShore.App.UI.Modals;
using UnityEngine;

namespace CosmicShore.App.UI.Views
{
    public class PortSquadMemberConfigureView : ModalWindowManager
    {
        [SerializeField] SquadMemberCard squadMemberCard;
        [SerializeField] PortSquadCaptainSelectionView captainSelectView;
        [SerializeField] ShipSelectionView shipSelectionView;
        [SerializeField] PortSquadView squadView;

        // TODO: Need to pull this from inventory
        [SerializeField] SO_ShipList PlayerShips;

        SO_Captain SelectedCaptain;

        protected override void Start()
        {
            base.Start();
            shipSelectionView.OnSelect += captainSelectView.AssignModel;
            shipSelectionView.OnSelect += squadMemberCard.SetShip;
            
            captainSelectView.OnSelect += squadMemberCard.SetCaptain;
            captainSelectView.OnSelect += SelectCaptain;

            shipSelectionView.AssignModels(PlayerShips.ShipList.ConvertAll(x => (ScriptableObject)x));
        }

        public void InitializeView(SO_Captain captain, bool isPlayer) 
        {
            shipSelectionView.Select(PlayerShips.ShipList.IndexOf(captain.Ship));
            captainSelectView.IsPlayer = isPlayer;
            captainSelectView.SetSelectedCaptain(captain);
        }

        public void SelectCaptain(SO_Captain captain)
        {
            SelectedCaptain = captain;
        }

        public void ConfirmCaptain()
        {
            squadView.AssignCaptain(SelectedCaptain);
        }
    }
}