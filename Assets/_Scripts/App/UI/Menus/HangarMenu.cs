using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Menus
{
    public class HangarMenu : MonoBehaviour
    {
        [SerializeField] SO_ShipList ShipList;
        [SerializeField] Transform ShipSelectionContainer;
        [SerializeField] InfiniteScroll ShipSelectionScrollView;
        [SerializeField] HangarShipSelectNavLink ShipSelectCardPrefab;
        [SerializeField] NavGroup TopNav;

        // TODO: the conversion over to NavLink/NavGroup paradigm isn't complete
        [Header("Overview View")]
        [SerializeField] HangarOverviewView OverviewView;

        [Header("Abilities View")]
        [SerializeField] HangarAbilitiesView AbilitiesView;

        [Header("Captains View")]
        [SerializeField] TMPro.TMP_Text SelectedCaptainName;
        [SerializeField] TMPro.TMP_Text SelectedCaptainElementLabel;
        [SerializeField] TMPro.TMP_Text SelectedCaptainQuote;
        [SerializeField] Image SelectedCaptainImage;
        [SerializeField] Transform CaptainSelectionContainer;
        [SerializeField] Sprite CaptainSelectButtonBorderSpriteSelected;        // TODO: Move to CaptainSelectCard
        [SerializeField] Sprite CaptainSelectButtonBorderSpriteDeselected;        // TODO: Move to CaptainSelectCard

        [Header("Captains - Upgrades UI")]
        [SerializeField] TMPro.TMP_Text SelectedUpgradeDescription;
        [SerializeField] TMPro.TMP_Text SelectedUpgradeXPAcquired;
        [SerializeField] TMPro.TMP_Text SelectedUpgradeXPRequirement;
        [SerializeField] TMPro.TMP_Text SelectedUpgradeCrystalRequirement;

        [Header("Training UI")]
        [SerializeField] GameObject TrainingView;
        [SerializeField] Transform GameSelectionContainer;
        [SerializeField] Image ShipModelImage;
        [SerializeField] TMPro.TMP_Text SelectedGameName;
        [SerializeField] TMPro.TMP_Text SelectedGameDescription;
        [SerializeField] GameObject SelectedGamePreviewWindow;

        List<SO_Ship> Ships;
        SO_Ship SelectedShip;
        SO_Captain SelectedCaptain;
        SO_ArcadeGame SelectedGame;
        SO_ShipAbility SelectedAbility;

        public void LoadView()
        {
            Ships = ShipList.ShipList;
            OverviewView.AssignModels(Ships.ConvertAll(x => (ScriptableObject)x));
            PopulateShipSelectionList();
        }

        /// <summary>
        /// Populates the list of ship buttons based using the SO_ShipList ShipList assigned to the menu
        /// </summary>
        void PopulateShipSelectionList()
        {
            if (ShipSelectionContainer == null)
            {
                Debug.LogError($"SerializedField 'ShipSelectionContainer' has not been assigned in HangarMenu");
                return;
            }

            // Deactivate all
            for (var i = 0; i < ShipSelectionContainer.childCount; i++)
            {
                var child = ShipSelectionContainer.GetChild(i);
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            // Reactivate based on the number of ships
            for (var i = 0; i < Ships.Count; i++)
            {
                var ship = Ships[i];
                Debug.Log($"Populating Ship Select List: {ship.Name}");                
                var shipSelectCard = Instantiate(ShipSelectCardPrefab, ShipSelectionContainer.transform);
                shipSelectCard.name = shipSelectCard.name.Replace("(Clone)", "");
                shipSelectCard.AssignShipClass(ship);
                shipSelectCard.AssignIndex(i);
                shipSelectCard.HangarMenu = this;
            }

            ShipSelectionScrollView.Initialize(true);

            StartCoroutine(SelectShipCoroutine(ShipSelectionContainer.GetChild(0).gameObject.GetComponent<HangarShipSelectNavLink>()));
        }

        void PopulateCaptainSelectionList()
        {
            if (CaptainSelectionContainer == null) return;

            // Deactivate all
            for (var i = 0; i < CaptainSelectionContainer.transform.childCount; i++)
                CaptainSelectionContainer.GetChild(i).gameObject.SetActive(false);

            // Reactivate based on the number of abilities for the selected ship
            for (var i = 0; i < SelectedShip.Captains.Count; i++)
            {
                var selectionIndex = i;
                var captain = SelectedShip.Captains[i];
                Debug.Log($"Populating Captain Select List: {captain?.Name}");
                var captainSelection = CaptainSelectionContainer.GetChild(i).gameObject;
                captainSelection.SetActive(true);
                captainSelection.GetComponent<Image>().sprite = captain?.Icon;
                captainSelection.GetComponent<Button>().onClick.RemoveAllListeners();
                captainSelection.GetComponent<Button>().onClick.AddListener(() => SelectCaptain(selectionIndex));
                captainSelection.GetComponent<Button>().onClick.AddListener(() => CaptainSelectionContainer.GetComponent<MenuAudio>().PlayAudio());
            }

            StartCoroutine(SelectCaptainCoroutine(0));
        }

        void PopulateCaptainDetails()
        {
            Debug.Log($"Populating Captain Details List: {SelectedCaptain.Name}");
            Debug.Log($"Populating Captain Details List: {SelectedCaptain.Description}");
            Debug.Log($"Populating Captain Details List: {SelectedCaptain.Icon}");
            Debug.Log($"Populating Captain Details List: {SelectedCaptain.Image}");

            if (SelectedCaptainName != null) SelectedCaptainName.text = SelectedCaptain.Name;
            if (SelectedCaptainElementLabel != null) SelectedCaptainElementLabel.text = "The " + SelectedCaptain.PrimaryElement.ToString() + " " + SelectedCaptain.Ship.Name;
            if (SelectedUpgradeDescription != null) SelectedUpgradeDescription.text = SelectedCaptain.Description;
            if (SelectedCaptainQuote != null) SelectedCaptainQuote.text = SelectedCaptain.Flavor;
            if (SelectedCaptainImage != null) SelectedCaptainImage.sprite = SelectedCaptain.Image;
        }

        void PopulateTrainingGameDetails()
        {
            Debug.Log($"Populating Training Details List: {SelectedGame.DisplayName}");
            Debug.Log($"Populating Training  Details List: {SelectedGame.Description}");
            Debug.Log($"Populating Training  Details List: {SelectedGame.Icon}");
            Debug.Log($"Populating Training  Details List: {SelectedGame.PreviewClip}");

            if (ShipModelImage != null) ShipModelImage.sprite = SelectedGame.Icon;
            SelectedGameName.text = SelectedGame.DisplayName;
            SelectedGameDescription.text = SelectedGame.Description;
            
            // Show intensity Selection

            if (SelectedGamePreviewWindow != null)
            {
                for (var i = 2; i < SelectedGamePreviewWindow.transform.childCount; i++)
                    Destroy(SelectedGamePreviewWindow.transform.GetChild(i).gameObject);

                var preview = Instantiate(SelectedGame.PreviewClip);
                preview.transform.SetParent(SelectedGamePreviewWindow.transform, false);
                SelectedGamePreviewWindow.SetActive(true);
                Canvas.ForceUpdateCanvases();
            }
        }

        public void SelectShip(int index)
        {
            var selectedShip = Ships[index];
            Debug.Log($"SelectShip: {selectedShip.Name}");
            Debug.Log($"ShipSelectionContainer.childCount: {ShipSelectionContainer.childCount}");
            Debug.Log($"Ships.Count: {Ships.Count}");

            // set all sprites to deselected - the selected card will activate it's own sprite
            for (var i = 0; i < ShipSelectionContainer.childCount; i++)
            {
                var selectCard = ShipSelectionContainer.GetChild(i).gameObject.GetComponent<HangarShipSelectNavLink>();
                selectCard.SetActive(selectCard.Ship == selectedShip);
            }

            SelectedShip = selectedShip;

            // notify the mini game engine that this is the ship to play
            //Hangar.Instance.SetPlayerShip((int)SelectedShip.Class);

            // Update the Overview view
            OverviewView.Select(index);

            // populate the abilities/overview views
            foreach (var ability in SelectedShip.Abilities) ability.Ship = selectedShip;
            AbilitiesView.AssignModels(SelectedShip.Abilities.ConvertAll(x => (ScriptableObject)x));

            // populate the captain view
            PopulateCaptainSelectionList();
            StartCoroutine(SelectCaptainCoroutine(0));  // Select the default view in a coroutine so that it happens next frame - otherwise there is a draw order error
        }

        /* Selects the Captain in the UI for display */
        /// <summary>
        /// Select a Captain in the UI to display its meta data
        /// TODO: Add UI Captain Assets for Urchin and Bufo when they are available
        /// TODO: WIP Convert this UI element to a card
        /// </summary>
        /// <param name="index">Index of the displayed Captain list</param>
        public void SelectCaptain(int index)
        {
            Debug.Log($"SelectCaptain: {index}");

            try
            {
                // Deselect them all
                for (var i = 0; i < 4; i++)
                {
                    // Deselect the border image
                    CaptainSelectionContainer.GetChild(i).gameObject.GetComponent<Image>().sprite = CaptainSelectButtonBorderSpriteDeselected;
                    // Deselect the captains element image
                    CaptainSelectionContainer.GetChild(i).GetChild(0).gameObject.GetComponent<Image>().sprite = SelectedShip.Captains[i].Icon;
                    // TODO: WIP - adjust the above to use the so_element's upgrade level corresponding to the captains current upgrade status
                }

                // Select the border image
                // Select the captains element image
                SelectedCaptain = SelectedShip.Captains[index];
                CaptainSelectionContainer.GetChild(index).gameObject.GetComponent<Image>().sprite = CaptainSelectButtonBorderSpriteSelected;                
                CaptainSelectionContainer.GetChild(index).GetChild(0).gameObject.GetComponent<Image>().sprite = SelectedCaptain.SelectedIcon;
            }
            catch (ArgumentOutOfRangeException argumentOutOfRangeException)
            {
                Debug.LogWarningFormat("{0} - {1} - The ship lacks captain assets. Please add them. {2}", nameof(HangarMenu),
                    nameof(SelectCaptain), argumentOutOfRangeException.Message);
            }
            catch (NullReferenceException nullReferenceException)
            {
                Debug.LogWarningFormat("{0} - {1} - The ship lacks captain assets. Please add them. {2}", nameof(HangarMenu),
                    nameof(SelectCaptain), nullReferenceException.Message);
            }

            PopulateCaptainDetails();
        }

        public void DisplayTrainingModal()
        {
            SelectTrainingGame(SelectedShip.Games[0]);
        }

        /// <summary>
        /// </summary>
        /// <param name="index">Index of the displayed Game list</param>
        //public void SelectTrainingGame(int index)
        public void SelectTrainingGame(SO_ArcadeGame game)  //TODO WIP: Use SO_TrainingGame once the data is populated so we can dynamically set the elements on the selection cards
        {
            Debug.Log($"SelectTainingGame: {game.DisplayName}");

            SelectedGame = game;

            try
            {
                // Deselect them all
                for (var i = 0; i < 2; i++)
                    GameSelectionContainer.GetChild(i).gameObject.GetComponent<HangarTrainingGameButton>().SetInactive();
            }
            catch (ArgumentOutOfRangeException argumentOutOfRangeException)
            {
                Debug.LogWarningFormat("{0} - {1} - The ship lacks training games. Please add them. {2}", nameof(HangarMenu),
                    nameof(SelectCaptain), argumentOutOfRangeException.Message);
            }
            catch (NullReferenceException nullReferenceException)
            {
                Debug.LogWarningFormat("{0} - {1} - The ship lacks training games. Please add them. {2}", nameof(HangarMenu),
                    nameof(SelectCaptain), nullReferenceException.Message);
            }

            PopulateTrainingGameDetails();
        }

        IEnumerator SelectCaptainCoroutine(int index)
        {
            yield return new WaitForEndOfFrame();
            SelectCaptain(index);
        }

        IEnumerator SelectShipCoroutine(HangarShipSelectNavLink shipSelectCard)
        {
            yield return new WaitForEndOfFrame();
            shipSelectCard.Select();
        }
    }
}