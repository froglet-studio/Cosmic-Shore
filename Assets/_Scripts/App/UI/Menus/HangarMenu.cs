using CosmicShore.Core;
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
        [SerializeField] HangarShipSelectCard ShipSelectCardPrefab;
        [SerializeField] NavGroup TopNav;

        [Header("Overview - Ship UI")]
        [SerializeField] GameObject OverviewView;
        [SerializeField] GameObject ShipDetailsPanel;
        [SerializeField] TMPro.TMP_Text SelectedShipName;
        [SerializeField] TMPro.TMP_Text SelectedShipSummary;
        [SerializeField] TMPro.TMP_Text SelectedShipDescription;
        [SerializeField] GameObject SelectedShipPreviewWindow;
        [SerializeField] GameObject OverviewButton;
        [SerializeField] Sprite OverviewButtonActiveSprite;
        [SerializeField] Sprite OverviewButtonInactiveSprite;

        [Header("Overview - Abilities UI")]
        [SerializeField] GameObject AbilityDetailsPanel;
        [SerializeField] Transform AbilitySelectionContainer;
        [SerializeField] TMPro.TMP_Text SelectedAbilityName;
        [SerializeField] TMPro.TMP_Text SelectedAbilityDescription;
        [SerializeField] GameObject SelectedAbilityPreviewWindow;

        [Header("Captains UI")]
        [SerializeField] GameObject CaptainsView;
        [SerializeField] TMPro.TMP_Text SelectedCaptainName;
        [SerializeField] TMPro.TMP_Text SelectedCaptainElementLabel;
        [SerializeField] TMPro.TMP_Text SelectedCaptainQuote;
        [SerializeField] Image SelectedCaptainImage;
        [SerializeField] Transform CaptainSelectionContainer;
        [SerializeField] Sprite CaptainSelectButtonBorderSpriteSelected;
        [SerializeField] Sprite CaptainSelectButtonBorderSpriteDeselected;

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
        //[SerializeField] TMPro.TMP_Text SelectedGameDescription;
        [SerializeField] GameObject SelectedGamePreviewWindow;

        List<SO_Ship> Ships;
        SO_Ship SelectedShip;
        SO_Captain SelectedCaptain;
        SO_ArcadeGame SelectedGame;
        SO_ShipAbility SelectedAbility;
        int _legitShipCount;

        public void LoadView()
        {
            Ships = ShipList.ShipList;
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
                shipSelectCard.HangarMenu = this;
            }

            ShipSelectionScrollView.Initialize(true);

            StartCoroutine(SelectShipCoroutine(ShipSelectionContainer.GetChild(0).gameObject.GetComponent<HangarShipSelectCard>()));
        }

        /// <summary>
        /// Populates the list of Ability Selection Buttons using the currently selected ship
        /// </summary>
        void PopulateAbilitySelectionList()
        {
            if (AbilitySelectionContainer == null) return;

            // Deactivate all
            for (var i = 0; i < AbilitySelectionContainer.transform.childCount; i++)
                AbilitySelectionContainer.GetChild(i).gameObject.SetActive(false);

            // Reactivate based on the number of abilities for the selected ship
            for (var i = 0; i < SelectedShip.Abilities.Count; i++)
            {
                var selectionIndex = i;
                var ability = SelectedShip.Abilities[i];
                Debug.Log($"Populating Abilities Select List: {ability.Name}");
                var abilitySelection = AbilitySelectionContainer.GetChild(i).gameObject;
                abilitySelection.SetActive(true);
                abilitySelection.GetComponent<Image>().sprite = ability.Icon;
                abilitySelection.GetComponent<Button>().onClick.RemoveAllListeners();
                abilitySelection.GetComponent<Button>().onClick.AddListener(() => SelectAbility(selectionIndex));
                abilitySelection.GetComponent<Button>().onClick.AddListener(() => AbilitySelectionContainer.GetComponent<MenuAudio>().PlayAudio());
            }
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
                //captain.
                captainSelection.GetComponent<Image>().sprite = captain?.Icon;
                captainSelection.GetComponent<Button>().onClick.RemoveAllListeners();
                captainSelection.GetComponent<Button>().onClick.AddListener(() => SelectCaptain(selectionIndex));
                captainSelection.GetComponent<Button>().onClick.AddListener(() => CaptainSelectionContainer.GetComponent<MenuAudio>().PlayAudio());
            }

            StartCoroutine(SelectCaptainCoroutine(0));
        }

        void PopulateShipDetails()
        {
            Debug.Log($"Populating Ship Details List: {SelectedShip.Name}");
            Debug.Log($"Populating Ship Details List: {SelectedShip.Description}");
            Debug.Log($"Populating Ship Details List: {SelectedShip.Icon}");
            Debug.Log($"Populating Ship Details List: {SelectedShip.PreviewImage}");

            if (SelectedShipName != null) SelectedShipName.text = SelectedShip.Name;
            if (SelectedShipDescription != null) SelectedShipDescription.text = SelectedShip.Description;
            if (SelectedShipSummary != null) SelectedShipSummary.text = SelectedShip.Summary;

            var preview = Instantiate(SelectedShip.PreviewImage);
            //preview.transform.SetParent(SelectedAbilityPreviewWindow.transform, false);
            //TODO P0: Refactor Ship SO to have a preview clip
        }

        void PopulateAbilityDetails()
        {
            Debug.Log($"Populating Ability Details List: {SelectedAbility.Name}");
            Debug.Log($"Populating Ability Details List: {SelectedAbility.Description}");
            Debug.Log($"Populating Ability Details List: {SelectedAbility.Icon}");
            Debug.Log($"Populating Ability Details List: {SelectedAbility.PreviewClip}");

            if (SelectedAbilityName != null) SelectedAbilityName.text = SelectedAbility.Name;
            if (SelectedAbilityDescription != null) SelectedAbilityDescription.text = SelectedAbility.Description;

            if (SelectedAbilityPreviewWindow != null)
            {
                for (var i = 0; i < SelectedAbilityPreviewWindow.transform.childCount; i++)
                    Destroy(SelectedAbilityPreviewWindow.transform.GetChild(i).gameObject);

                var preview = Instantiate(SelectedAbility.PreviewClip, SelectedAbilityPreviewWindow.transform);
                preview.GetComponent<RawImage>().rectTransform.sizeDelta = new Vector2(256, 144);
                SelectedAbilityPreviewWindow.SetActive(true);
                Canvas.ForceUpdateCanvases();
            }
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

            // Show intensity Selection

            //[SerializeField] TMPro.TMP_Text SelectedGameDescription;
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

        public void SelectShip(SO_Ship selectedShip)
        {
            Debug.Log($"SelectShip: {selectedShip.Name}");
            Debug.Log($"ShipSelectionContainer.childCount: {ShipSelectionContainer.childCount}");
            Debug.Log($"Ships.Count: {Ships.Count}");

            // set all sprites to deselected - the selected card will activate it's own sprite
            for (var i = 0; i < ShipSelectionContainer.childCount; i++)
            {
                //ShipSelectionContainer.GetChild(i).gameObject.GetComponent<Image>().sprite = Ships[i].Icon;
                var selectCard = ShipSelectionContainer.GetChild(i).gameObject.GetComponent<HangarShipSelectCard>();
                if (selectCard != null && selectCard.Ship == selectedShip)
                    selectCard.SetActive();
                else
                    selectCard.SetInactive();
            }

            SelectedShip = selectedShip;

            // notify the mini game engine that this is the ship to play
            Hangar.Instance.SetPlayerShip((int)SelectedShip.Class);

            // 
            PopulateShipDetails();
            
            // populate the abilities/overview views
            PopulateAbilitySelectionList(); // TODO: P0 - We no longer need to dynamically populate these event listeners since there is always a fixed number of abilities and captains now
            StartCoroutine(SelectOverviewCoroutine());  // Select the default view in a coroutine so that it happens next frame - otherwise there is a draw order error

            // populate the captain view
            PopulateCaptainSelectionList();
            StartCoroutine(SelectCaptainCoroutine(0));  // Select the default view in a coroutine so that it happens next frame - otherwise there is a draw order error
        }

        public void SelectOverview()
        {
            // Deselect all Ability Icons
            for (var i = 0; i < SelectedShip.Abilities.Count; i++)
                AbilitySelectionContainer.GetChild(i).gameObject.GetComponent<Image>().sprite = SelectedShip.Abilities[i].Icon;

            OverviewButton.GetComponent<Image>().sprite = OverviewButtonActiveSprite;

            ShipDetailsPanel.SetActive(true);
            AbilityDetailsPanel.SetActive(false);
        }

        public void SelectAbility(int index)
        {
            Debug.Log($"SelectAbility: {index}");

            OverviewButton.GetComponent<Image>().sprite = OverviewButtonInactiveSprite;
            ShipDetailsPanel.SetActive(false);
            AbilityDetailsPanel.SetActive(true);

            // Deselect them all
            for (var i = 0; i < SelectedShip.Abilities.Count; i++)
                AbilitySelectionContainer.GetChild(i).gameObject.GetComponent<Image>().sprite = SelectedShip.Abilities[i].Icon;

            // Select the one
            SelectedAbility = SelectedShip.Abilities[index];
            AbilitySelectionContainer.GetChild(index).gameObject.GetComponent<Image>().sprite = SelectedAbility.SelectedIcon;

            PopulateAbilityDetails();
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

        /// <summary>
        /// </summary>
        /// <param name="index">Index of the displayed Game list</param>
        //public void SelectTrainingGame(int index)
        public void SelectTrainingGame(SO_TrainingGame game)
        {
            Debug.Log($"SelectTainingGame: {game.DisplayName}");

            try
            {
                // Deselect them all
                for (var i = 0; i < 2; i++)
                    GameSelectionContainer.GetChild(i).gameObject.GetComponent<HangarTrainingGameButton>().SetInactive();

                // Select the one
                SelectedGame = game;
                //GameSelectionContainer.GetChild(i).gameObject.GetComponent<HangarTrainingGameButton>().SetInactive();
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


        IEnumerator SelectOverviewCoroutine()
        {
            yield return new WaitForEndOfFrame();
            SelectOverview();
        }

        IEnumerator SelectCaptainCoroutine(int index)
        {
            yield return new WaitForEndOfFrame();
            SelectCaptain(index);
        }

        IEnumerator SelectShipCoroutine(HangarShipSelectCard shipSelectCard)
        {
            yield return new WaitForEndOfFrame();
            shipSelectCard.Select();
        }

        IEnumerator SelectAbilityCoroutine(int index)
        {
            yield return new WaitForEndOfFrame();
            SelectAbility(index);
        }

        IEnumerator SelectTrainingGameCoroutine(SO_TrainingGame game)
        {
            yield return new WaitForEndOfFrame();
            SelectTrainingGame(game);
        }
    }
}