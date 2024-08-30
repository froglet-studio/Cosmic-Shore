using CosmicShore.App.UI.Views;
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

        public void SelectShip(int index)
        {
            //PlayerPrefs.SetInt("HangarLastSelectedShipIndex", index);
            //PlayerPrefs.Save();
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

            // Update the Overview view
            OverviewView.Select(index);

            // populate the abilities/overview views
            foreach (var ability in SelectedShip.Abilities) ability.Ship = selectedShip;
            AbilitiesView.AssignModels(SelectedShip.Abilities.ConvertAll(x => (ScriptableObject)x));
            CaptainsView.AssignModels(SelectedShip.Captains.ConvertAll(x => (ScriptableObject) x));
        }

        public void DisplayTrainingModal()
        {
            HangarTrainingModal.SetTrainingGames(SelectedShip.TrainingGames);
            HangarTrainingModal.ModalWindowIn();
        }

        IEnumerator SelectShipCoroutine(HangarShipSelectNavLink shipSelectCard)
        {
            yield return new WaitForEndOfFrame();
            shipSelectCard.Select();
        }
    }
}