using CosmicShore.App.Systems.Squads;
using CosmicShore.App.UI.Elements;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Menus
{
    public class SquadMenu : MonoBehaviour
    {
        [SerializeField] GameObject CaptainSelectionGrid;
        [SerializeField] HorizontalLayoutGroup CaptainSelectionRowPrefab;
        [SerializeField] CaptainCard CaptainCardPrefab;
        [SerializeField] CaptainSelectButton PlayerCaptainButton;
        [SerializeField] CaptainSelectButton RogueOneCaptainButton;
        [SerializeField] CaptainSelectButton RogueTwoCaptainButton;

        //[SerializeField] Inventory PlayerInventory;
        [SerializeField] SO_ShipList PlayerShips;
        List<SO_Captain> AllCaptains = new();
        List<CaptainCard> CaptainCards = new();

        public int ActiveSquadMember = 0;

        void Start()
        {
            // Populate Captain Selection Grid
            int captainIndex = 0;
            foreach (var ship in PlayerShips.ShipList)
            {
                var row = Instantiate(CaptainSelectionRowPrefab);
                var captains = new List<SO_Captain>();
                foreach (var captain in ship.Captains)
                {
                    captains.Add(captain);
                    AllCaptains.Add(captain);
                }
                captains.Sort((x, y) => { return x.PrimaryElement < y.PrimaryElement ? 1 : -1; });

                foreach (var captain in captains)
                {
                    var captainCard = Instantiate(CaptainCardPrefab);
                    captainCard.Captain = captain;
                    captainCard.transform.SetParent(row.transform, false);
                    captainCard.SquadMenu = this;
                    captainCard.Index = captainIndex;
                    CaptainCards.Add(captainCard);
                    captainIndex++;
                }

                row.transform.SetParent(CaptainSelectionGrid.transform, false);
            }

            // Populate Squad Buttons
            SquadSystem.CaptainList = AllCaptains;
            SquadSystem.DefaultLeader = AllCaptains[0];
            SquadSystem.DefaultRogueOne = AllCaptains[1];
            SquadSystem.DefaultRogueTwo = AllCaptains[2];

            Debug.Log($"SquadSystem.DefaultLeader: {SquadSystem.DefaultLeader}");
            Debug.Log($"SquadSystem.DefaultRogueOne: {SquadSystem.DefaultRogueOne}");
            Debug.Log($"SquadSystem.DefaultRogueTwo: {SquadSystem.DefaultRogueTwo}");

            // Get player captain and set captain image for button. set player button active
            var squad = SquadSystem.LoadSquad();
            PlayerCaptainButton.Captain = SquadSystem.SquadLeader;
            RogueOneCaptainButton.Captain = SquadSystem.RogueOne;
            RogueTwoCaptainButton.Captain = SquadSystem.RogueTwo;
            SetSquadLeaderAssignmentActive();
        }

        public void AssignCaptain(CaptainCard captainCard)
        {
            SO_Captain captain = captainCard.Captain;
            foreach (var card in CaptainCards)
                card.Active(false);

            captainCard.Active(true);

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

        public void SetSquadLeaderAssignmentActive()
        {
            Debug.Log("SetSquadLeaderAssignmentActive");
            ActiveSquadMember = 0;
            PlayerCaptainButton.Active(true);
            RogueOneCaptainButton.Active(false);
            RogueTwoCaptainButton.Active(false);

            UpdateCardGrid(SquadSystem.SquadLeader);
        }
        public void SetRogueOneAssignmentActive()
        {
            Debug.Log("SetRogueOneAssignmentActive");
            ActiveSquadMember = 1;
            PlayerCaptainButton.Active(false);
            RogueOneCaptainButton.Active(true);
            RogueTwoCaptainButton.Active(false);

            UpdateCardGrid(SquadSystem.RogueOne);
        }
        public void SetRogueTwoAssignmentActive()
        {
            Debug.Log("SetRogueTwoAssignmentActive");
            ActiveSquadMember = 2;
            PlayerCaptainButton.Active(false);
            RogueOneCaptainButton.Active(false);
            RogueTwoCaptainButton.Active(true);

            UpdateCardGrid(SquadSystem.RogueTwo);
        }

        void UpdateCardGrid(SO_Captain activeCaptain)
        {
            foreach (var card in CaptainCards)
            {
                card.Active(card.Captain == activeCaptain);

            }
        }
    }
}