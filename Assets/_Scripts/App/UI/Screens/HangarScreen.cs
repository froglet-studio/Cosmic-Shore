using CosmicShore.App.UI.Views;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Screens
{
    public class HangarScreen : MonoBehaviour
    {
        [SerializeField] SO_ShipList ShipList;
        [SerializeField] Transform ShipSelectionContainer;
        [SerializeField] InfiniteScroll ShipSelectionScrollView;
        [SerializeField] HangarShipSelectNavLink ShipSelectCardPrefab;
        [SerializeField] NavGroup TopNav;

        [Header("Views")]
        [SerializeField] HangarOverviewView OverviewView;   // TODO: the conversion over to NavLink/NavGroup paradigm isn't complete
        [SerializeField] HangarAbilitiesView AbilitiesView;
        [SerializeField] HangarCaptainsView CaptainsView;
        [SerializeField] Transform CaptainSelectionContainer; // TODO: move to Captains View

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

            // Assign captains
            for (var i = 0; i < CaptainSelectionContainer.transform.childCount; i++)
                CaptainSelectionContainer.GetChild(i).GetComponent<CaptainUpgradeSelectionCard>().AssignCaptain(SelectedShip.Captains[i]);

            StartCoroutine(SelectCaptainCoroutine(0));
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
        /// </summary>
        /// <param name="index">Index of the displayed Captain list</param>
        public void SelectCaptain(int index)
        {
            Debug.Log($"SelectCaptain: {index}");

            try
            {
                for (var i = 0; i < 4; i++)
                    CaptainSelectionContainer.GetChild(i).GetComponent<CaptainUpgradeSelectionCard>().ToggleSelected(i == index);

                SelectedCaptain = SelectedShip.Captains[index];
            }
            catch (ArgumentOutOfRangeException argumentOutOfRangeException)
            {
                Debug.LogWarningFormat("{0} - {1} - The ship lacks captain assets. Please add them. {2}", nameof(HangarScreen),
                    nameof(SelectCaptain), argumentOutOfRangeException.Message);
            }
            catch (NullReferenceException nullReferenceException)
            {
                Debug.LogWarningFormat("{0} - {1} - The ship lacks captain assets. Please add them. {2}", nameof(HangarScreen),
                    nameof(SelectCaptain), nullReferenceException.Message);
            }

            CaptainsView.AssignModel(SelectedCaptain);
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
                Debug.LogWarningFormat("{0} - {1} - The ship lacks training games. Please add them. {2}", nameof(HangarScreen),
                    nameof(SelectTrainingGame), argumentOutOfRangeException.Message);
            }
            catch (NullReferenceException nullReferenceException)
            {
                Debug.LogWarningFormat("{0} - {1} - The ship lacks training games. Please add them. {2}", nameof(HangarScreen),
                    nameof(SelectTrainingGame), nullReferenceException.Message);
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