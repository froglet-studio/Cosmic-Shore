using CosmicShore.App.Systems.Squads;
using CosmicShore.App.UI.Modals;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.App.UI.Views
{
    public class PortSquadView : View
    {
        [SerializeField] PortSquadMemberConfigureView squadMemberConfigureView;

        [SerializeField] SquadMemberCard PlayerCaptainButton;
        [SerializeField] SquadMemberCard RogueOneCaptainButton;
        [SerializeField] SquadMemberCard RogueTwoCaptainButton;

        // TODO: Need to pull this from inventory
        [SerializeField] SO_ShipList PlayerShips;
        List<SO_Captain> AllCaptains = new();

        public int ActiveSquadMember = 0;

        void Start()
        {
            foreach (var ship in PlayerShips.ShipList)
                foreach (var captain in ship.Captains)
                    AllCaptains.Add(captain);

            // Populate Squad Buttons
            SquadSystem.CaptainList = AllCaptains;
            SquadSystem.DefaultLeader = AllCaptains[0];
            SquadSystem.DefaultRogueOne = AllCaptains[0];
            SquadSystem.DefaultRogueTwo = AllCaptains[0];

            // Get player captain and set captain image for button. set player button active
            var squad = SquadSystem.LoadSquad();
            PlayerCaptainButton.Captain = SquadSystem.SquadLeader;
            RogueOneCaptainButton.Captain = SquadSystem.RogueOne;
            RogueTwoCaptainButton.Captain = SquadSystem.RogueTwo;
        }

        public void AssignCaptain(SO_Captain captain)
        {
            switch (ActiveSquadMember)
            {
                case 0:
                    SquadSystem.SetSquadLeader(captain);
                    PlayerCaptainButton.Captain = captain;
                    break;
                case 1:
                    SquadSystem.SetRogueOne(captain);
                    RogueOneCaptainButton.Captain = captain;
                    break;
                default:
                    SquadSystem.SetRogueTwo(captain);
                    RogueTwoCaptainButton.Captain = captain;
                    break;
            }

            SquadSystem.SaveSquad();
        }

        /// <summary>
        /// Shows the captain select modal with the current squad member configuration set
        /// </summary>
        /// <param name="squadMember">0:leader, 1:Rogue One, 2: Rogue Two</param>
        public void ShowCaptainSelectModal(int squadMember)
        {
            ActiveSquadMember = squadMember;
            
            switch (ActiveSquadMember)
            {
                case 0:
                    squadMemberConfigureView.InitializeView(SquadSystem.SquadLeader, true);
                    break;
                case 1:
                    squadMemberConfigureView.InitializeView(SquadSystem.RogueOne, false);
                    break;
                default:
                    squadMemberConfigureView.InitializeView(SquadSystem.RogueTwo, false);
                    break;
            }

            squadMemberConfigureView.ModalWindowIn();
        }

        public override void UpdateView()
        {
            throw new System.NotImplementedException();
        }
    }
}