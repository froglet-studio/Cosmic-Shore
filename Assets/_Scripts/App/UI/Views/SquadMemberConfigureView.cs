using CosmicShore.App.Systems.Squads;
using CosmicShore.App.UI;
using CosmicShore.App.UI.Menus;
using UnityEngine;

namespace CosmicShore
{
    public class SquadMemberConfigureView : MonoBehaviour
    {
        [SerializeField] SquadMemberCard squadMemberCard;
        [SerializeField] SquadCaptainSelectionView captainSelectView;
        [SerializeField] ShipSelectionView shipSelectionView;
        [SerializeField] SquadView squadView;

        // TODO: Need to pull this from inventory
        [SerializeField] SO_ShipList PlayerShips;

        SO_Captain SelectedCaptain;

        void Start()
        {
            shipSelectionView.OnSelect += captainSelectView.AssignModel;
            shipSelectionView.OnSelect += squadMemberCard.SetShip;
            
            captainSelectView.OnSelect += squadMemberCard.SetCaptain;
            captainSelectView.OnSelect += SelectCaptain;

            shipSelectionView.AssignModels(PlayerShips.ShipList.ConvertAll(x => (ScriptableObject)x));
        }

        public void InitializeView(SO_Captain captain, bool isPlayer) 
        {
            shipSelectionView.Select(PlayerShips.ShipList.IndexOf(captain.Ship));
            captainSelectView.SetSelectedCaptain(captain);
            captainSelectView.IsPlayer = isPlayer;
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