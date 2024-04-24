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
        [FormerlySerializedAs("VesselSelectionGrid")]
        [SerializeField] GameObject GuideSelectionGrid;
        [FormerlySerializedAs("VesselSelectionRowPrefab")]
        [SerializeField] HorizontalLayoutGroup GuideSelectionRowPrefab;
        [FormerlySerializedAs("VesselCardPrefab")]
        [SerializeField] GuideCard GuideCardPrefab;
        [FormerlySerializedAs("PlayerVesselButton")]
        [SerializeField] GuideSelectButton PlayerGuideButton;
        [FormerlySerializedAs("RogueOneVesselButton")]
        [SerializeField] GuideSelectButton RogueOneGuideButton;
        [FormerlySerializedAs("RogueTwoVesselButton")]
        [SerializeField] GuideSelectButton RogueTwoGuideButton;

        //[SerializeField] Inventory PlayerInventory;
        [SerializeField] SO_ShipList PlayerShips;
        List<SO_Guide> AllGuides = new();
        List<GuideCard> GuideCards = new();

        public int ActiveSquadMember = 0;

        void Start()
        {
            // Populate Guide Selection Grid
            int guideIndex = 0;
            foreach (var ship in PlayerShips.ShipList)
            {
                var row = Instantiate(GuideSelectionRowPrefab);
                var guides = new List<SO_Guide>();
                foreach (var guide in ship.Guides)
                {
                    guides.Add(guide);
                    AllGuides.Add(guide);
                }
                guides.Sort((x, y) => { return x.PrimaryElement < y.PrimaryElement ? 1 : -1; });

                foreach (var guide in guides)
                {
                    var guideCard = Instantiate(GuideCardPrefab);
                    guideCard.Guide = guide;
                    guideCard.transform.SetParent(row.transform, false);
                    guideCard.SquadMenu = this;
                    guideCard.Index = guideIndex;
                    GuideCards.Add(guideCard);
                    guideIndex++;
                }

                row.transform.SetParent(GuideSelectionGrid.transform, false);
            }

            // Populate Squad Buttons
            SquadSystem.GuideList = AllGuides;
            SquadSystem.DefaultLeader = AllGuides[0];
            SquadSystem.DefaultRogueOne = AllGuides[1];
            SquadSystem.DefaultRogueTwo = AllGuides[2];

            Debug.Log($"SquadSystem.DefaultLeader: {SquadSystem.DefaultLeader}");
            Debug.Log($"SquadSystem.DefaultRogueOne: {SquadSystem.DefaultRogueOne}");
            Debug.Log($"SquadSystem.DefaultRogueTwo: {SquadSystem.DefaultRogueTwo}");

            // Get player guide and set guide image for button. set player button active
            var squad = SquadSystem.LoadSquad();
            PlayerGuideButton.Guide = SquadSystem.SquadLeader;
            RogueOneGuideButton.Guide = SquadSystem.RogueOne;
            RogueTwoGuideButton.Guide = SquadSystem.RogueTwo;
            SetSquadLeaderAssignmentActive();
        }

        public void AssignGuide(GuideCard guideCard)
        {
            SO_Guide guide = guideCard.Guide;
            foreach (var card in GuideCards)
                card.Active(false);

            guideCard.Active(true);

            switch (ActiveSquadMember)
            {
                case 0:
                    SquadSystem.SetSquadLeader(guide);
                    PlayerGuideButton.Guide = guide;
                    break;
                case 1:
                    SquadSystem.SetRogueOne(guide);
                    RogueOneGuideButton.Guide = guide;
                    break;
                default:
                    SquadSystem.SetRogueTwo(guide);
                    RogueTwoGuideButton.Guide = guide;
                    break;
            }

            SquadSystem.SaveSquad();
        }

        public void SetSquadLeaderAssignmentActive()
        {
            Debug.Log("SetSquadLeaderAssignmentActive");
            ActiveSquadMember = 0;
            PlayerGuideButton.Active(true);
            RogueOneGuideButton.Active(false);
            RogueTwoGuideButton.Active(false);

            UpdateCardGrid(SquadSystem.SquadLeader);
        }
        public void SetRogueOneAssignmentActive()
        {
            Debug.Log("SetRogueOneAssignmentActive");
            ActiveSquadMember = 1;
            PlayerGuideButton.Active(false);
            RogueOneGuideButton.Active(true);
            RogueTwoGuideButton.Active(false);

            UpdateCardGrid(SquadSystem.RogueOne);
        }
        public void SetRogueTwoAssignmentActive()
        {
            Debug.Log("SetRogueTwoAssignmentActive");
            ActiveSquadMember = 2;
            PlayerGuideButton.Active(false);
            RogueOneGuideButton.Active(false);
            RogueTwoGuideButton.Active(true);

            UpdateCardGrid(SquadSystem.RogueTwo);
        }

        void UpdateCardGrid(SO_Guide activeGuide)
        {
            foreach (var card in GuideCards)
            {
                card.Active(card.Guide == activeGuide);

            }
        }
    }
}