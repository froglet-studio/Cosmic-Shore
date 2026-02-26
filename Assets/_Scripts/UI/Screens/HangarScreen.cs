using CosmicShore.Models.ScriptableObjects;
using CosmicShore.UI.Views;
using CosmicShore.Utility.Recording;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI.Screens
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
        [SerializeField] HangarTrainingModal HangarTrainingModal;

        [Header("Training UI")]
        [SerializeField] Transform GameSelectionContainer;
        [SerializeField] Image ShipModelImage;
        [SerializeField] TMPro.TMP_Text SelectedGameName;
        [SerializeField] TMPro.TMP_Text SelectedGameDescription;
        [SerializeField] GameObject SelectedGamePreviewWindow;

        List<SO_Ship> Ships;
        SO_Ship SelectedShip;

        public void LoadView()
        {
            Ships = ShipList.ShipList;
            OverviewView.AssignModels(Ships.ConvertAll(x => (ScriptableObject)x));
            PopulateShipSelectionList();
        }

        /// <summary>
        /// Populates the list of vessel buttons based using the SO_ShipList ShipList assigned to the menu
        /// </summary>
        void PopulateShipSelectionList()
        {
            if (ShipSelectionContainer == null)
            {
                CSDebug.LogError($"SerializedField 'ShipSelectionContainer' has not been assigned in HangarMenu");
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
                CSDebug.Log($"Populating Vessel Select List: {ship.Name}");                
                var shipSelectCard = Instantiate(ShipSelectCardPrefab, ShipSelectionContainer.transform);
                shipSelectCard.name = shipSelectCard.name.Replace("(Clone)", "");
                shipSelectCard.AssignShipClass(ship);
                shipSelectCard.AssignIndex(i);
                shipSelectCard.HangarMenu = this;
            }

            ShipSelectionScrollView.Initialize(true);

            StartCoroutine(SelectFirstShipCoroutine());
        }

        public void SelectShip(int index)
        {
            //PlayerPrefs.SetInt("HangarLastSelectedShipIndex", index);
            //PlayerPrefs.Save();
            var selectedShip = Ships[index];
            CSDebug.Log($"SelectShip: {selectedShip.Name}");
            CSDebug.Log($"ShipSelectionContainer.childCount: {ShipSelectionContainer.childCount}");
            CSDebug.Log($"Ships.Count: {Ships.Count}");

            // set all sprites to deselected - the selected card will activate it's own sprite
            for (var i = 0; i < ShipSelectionContainer.childCount; i++)
            {
                var selectCard = ShipSelectionContainer.GetChild(i).gameObject.GetComponent<HangarShipSelectNavLink>();
                selectCard.SetActive(selectCard.Ship == selectedShip);
            }

            SelectedShip = selectedShip;

            // Update the Overview view
            OverviewView.Select(index);

            // populate the abilities/overview views
            foreach (var ability in SelectedShip.Abilities) ability.Ship = selectedShip;
            AbilitiesView.AssignModels(SelectedShip.Abilities.ConvertAll(x => (ScriptableObject)x));
            //CaptainsView.AssignModels(SelectedShip.Captains.ConvertAll(x => (ScriptableObject) x));
        }

        public void DisplayTrainingModal()
        {
            HangarTrainingModal.SetTrainingGames(SelectedShip.TrainingGames);
            HangarTrainingModal.ModalWindowIn();
        }

        IEnumerator SelectFirstShipCoroutine()
        {
            yield return null; // new WaitForSeconds(2);
            var shipSelectCard = ShipSelectionContainer.GetChild(0).gameObject.GetComponent<HangarShipSelectNavLink>();
            CSDebug.Log($"Starting SelectShipCoroutine: {shipSelectCard.name}, {shipSelectCard.Ship.Name}");
            shipSelectCard.Select();
        }
    }
}