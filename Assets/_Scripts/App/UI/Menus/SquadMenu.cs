using CosmicShore.App.Systems.Squads;
using CosmicShore.App.UI.Elements;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Menus
{
    public class SquadMenu : MonoBehaviour
    {
        [SerializeField] GameObject VesselSelectionGrid;
        [SerializeField] HorizontalLayoutGroup VesselSelectionRowPrefab;
        [SerializeField] VesselCard VesselCardPrefab;
        [SerializeField] VesselSelectButton PlayerVesselButton;
        [SerializeField] VesselSelectButton RogueOneVesselButton;
        [SerializeField] VesselSelectButton RogueTwoVesselButton;

        //[SerializeField] Inventory PlayerInventory;
        [SerializeField] SO_ShipList PlayerShips;
        List<SO_Vessel> AllVessels = new();
        List<VesselCard> VesselCards = new();

        public int ActiveSquadMember = 0;

        void Start()
        {
            // Populate Vessel Selection Grid
            int vesselIndex = 0;
            foreach (var ship in PlayerShips.ShipList)
            {
                var row = Instantiate(VesselSelectionRowPrefab);
                var vessels = new List<SO_Vessel>();
                foreach (var vessel in ship.Vessels)
                {
                    vessels.Add(vessel);
                    AllVessels.Add(vessel);
                }
                vessels.Sort((x, y) => { return x.PrimaryElement < y.PrimaryElement ? 1 : -1; });

                foreach (var vessel in vessels)
                {
                    var vesselCard = Instantiate(VesselCardPrefab);
                    vesselCard.Vessel = vessel;
                    vesselCard.transform.SetParent(row.transform, false);
                    vesselCard.SquadMenu = this;
                    vesselCard.Index = vesselIndex;
                    VesselCards.Add(vesselCard);
                    vesselIndex++;
                }

                row.transform.SetParent(VesselSelectionGrid.transform, false);
            }

            // Populate Squad Buttons
            SquadSystem.VesselList = AllVessels;
            SquadSystem.DefaultLeader = AllVessels[0];
            SquadSystem.DefaultRogueOne = AllVessels[1];
            SquadSystem.DefaultRogueTwo = AllVessels[2];

            Debug.Log($"SquadSystem.DefaultLeader: {SquadSystem.DefaultLeader}");
            Debug.Log($"SquadSystem.DefaultRogueOne: {SquadSystem.DefaultRogueOne}");
            Debug.Log($"SquadSystem.DefaultRogueTwo: {SquadSystem.DefaultRogueTwo}");

            // Get player vessel and set vessel image for button. set player button active
            var squad = SquadSystem.LoadSquad();
            PlayerVesselButton.Vessel = SquadSystem.SquadLeader;
            RogueOneVesselButton.Vessel = SquadSystem.RogueOne;
            RogueTwoVesselButton.Vessel = SquadSystem.RogueTwo;
            SetSquadLeaderAssignmentActive();
        }

        public void AssignVessel(VesselCard vesselCard)
        {
            SO_Vessel Vessel = vesselCard.Vessel;
            foreach (var card in VesselCards)
                card.Active(false);

            vesselCard.Active(true);

            switch (ActiveSquadMember)
            {
                case 0:
                    SquadSystem.SetSquadLeader(Vessel);
                    PlayerVesselButton.Vessel = Vessel;
                    break;
                case 1:
                    SquadSystem.SetRogueOne(Vessel);
                    RogueOneVesselButton.Vessel = Vessel;
                    break;
                default:
                    SquadSystem.SetRogueTwo(Vessel);
                    RogueTwoVesselButton.Vessel = Vessel;
                    break;
            }

            SquadSystem.SaveSquad();
        }

        public void SetSquadLeaderAssignmentActive()
        {
            Debug.Log("SetSquadLeaderAssignmentActive");
            ActiveSquadMember = 0;
            PlayerVesselButton.Active(true);
            RogueOneVesselButton.Active(false);
            RogueTwoVesselButton.Active(false);

            UpdateCardGrid(SquadSystem.SquadLeader);
        }
        public void SetRogueOneAssignmentActive()
        {
            Debug.Log("SetRogueOneAssignmentActive");
            ActiveSquadMember = 1;
            PlayerVesselButton.Active(false);
            RogueOneVesselButton.Active(true);
            RogueTwoVesselButton.Active(false);

            UpdateCardGrid(SquadSystem.RogueOne);
        }
        public void SetRogueTwoAssignmentActive()
        {
            Debug.Log("SetRogueTwoAssignmentActive");
            ActiveSquadMember = 2;
            PlayerVesselButton.Active(false);
            RogueOneVesselButton.Active(false);
            RogueTwoVesselButton.Active(true);

            UpdateCardGrid(SquadSystem.RogueTwo);
        }

        void UpdateCardGrid(SO_Vessel activeVessel)
        {
            foreach (var card in VesselCards)
            {
                card.Active(card.Vessel == activeVessel);

            }
        }
    }
}