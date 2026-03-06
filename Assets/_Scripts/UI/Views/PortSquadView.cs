using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.UI
{
    public class PortSquadView : View
    {
        [SerializeField] PortSquadMemberConfigureView squadMemberConfigureView;

        [SerializeField] SquadMemberCard PlayerCaptainButton;
        [SerializeField] SquadMemberCard RogueOneCaptainButton;
        [SerializeField] SquadMemberCard RogueTwoCaptainButton;

        // TODO: Need to pull this from inventory
        [SerializeField] SO_VesselList PlayerShips;
        List<SO_Captain> AllCaptains = new();

        public int ActiveSquadMember = 0;

        void Start()
        {
            // Captain system removed from vessels — squad system is inactive.
            // Keeping this stub to prevent runtime errors until squad is refactored.
            if (AllCaptains.Count > 0)
            {
                SquadSystem.DefaultLeader = AllCaptains[0];
                SquadSystem.DefaultRogueOne = AllCaptains[0];
                SquadSystem.DefaultRogueTwo = AllCaptains[0];
            }

            UpdateView();
        }

        public void AssignCaptain(SO_Captain captain)
        {
            switch (ActiveSquadMember)
            {
                case 0:
                    SquadSystem.SetSquadLeader(captain);
                    break;
                case 1:
                    SquadSystem.SetRogueOne(captain);
                    break;
                default:
                    SquadSystem.SetRogueTwo(captain);
                    break;
            }

            SquadSystem.SaveSquad();
            UpdateView();
        }

        /// <summary>
        /// Shows the captain select modal with the current squad member configuration set
        /// </summary>
        /// <param name="squadMember">0:leader, 1:Rogue One, 2: Rogue Two</param>
        public void ShowCaptainSelectModal(int squadMember)
        {
            ActiveSquadMember = squadMember;
            //squadMemberConfigureView.gameObject.SetActive(true);


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
            if (SquadSystem.CaptainList == null || SquadSystem.CaptainList.Count == 0)
                return;

            SquadSystem.LoadSquad();
            PlayerCaptainButton.Captain = SquadSystem.SquadLeader;
            RogueOneCaptainButton.Captain = SquadSystem.RogueOne;
            RogueTwoCaptainButton.Captain = SquadSystem.RogueTwo;
        }
    }
}