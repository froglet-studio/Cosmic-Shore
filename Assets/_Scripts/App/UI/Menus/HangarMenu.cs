using CosmicShore.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Menus
{
    public class HangarMenu : MonoBehaviour
    {
        [SerializeField] SO_ShipList ShipList;

        [SerializeField] Transform ShipSelectionContainer;
        [SerializeField] TMPro.TMP_Text SelectedShipName;
        [SerializeField] TMPro.TMP_Text SelectedShipDescription;
        [SerializeField] Image SelectedShipImage;
        [SerializeField] Image SelectedShipTrailImage;

        [SerializeField] Transform AbilitySelectionContainer;
        [SerializeField] TMPro.TMP_Text SelectedAbilityName;
        [SerializeField] TMPro.TMP_Text SelectedAbilityDescription;
        [SerializeField] GameObject SelectedAbilityPreviewWindow;

        [FormerlySerializedAs("VesselSelectionContainer")]
        [SerializeField] Transform GuideSelectionContainer;
        [FormerlySerializedAs("SelectedVesselName")]
        [SerializeField] TMPro.TMP_Text SelectedGuideName;
        [FormerlySerializedAs("SelectedVesselDescription")]
        [SerializeField] TMPro.TMP_Text SelectedGuideDescription;
        [FormerlySerializedAs("SelectedVesselFlavor")]
        [SerializeField] TMPro.TMP_Text SelectedGuideFlavor;
        [FormerlySerializedAs("SelectedVesselImage")]
        [SerializeField] Image SelectedGuideImage;

        List<SO_Ship> Ships;
        SO_Ship SelectedShip;
        SO_Guide SelectedGuide;
        SO_ShipAbility SelectedAbility;
        private int _legitShipCount;

        void Start()
        {
            Ships = ShipList.ShipList;
            PopulateShipSelectionList();
        }

        void PopulateShipSelectionList()
        {
            if (ShipSelectionContainer == null) return;

            // Get legitimate ship counts
            _legitShipCount = math.min(ShipSelectionContainer.childCount, Ships.Count);
            
            // Deactivate all
            for (var i = 0; i < ShipSelectionContainer.childCount; i++)
                ShipSelectionContainer.GetChild(i).gameObject.SetActive(false);

            // Reactivate based on the number of ships
            for (var i = 0; i < _legitShipCount; i++)
            {
                var selectionIndex = i;
                var ship = Ships[i];
                Debug.Log($"Populating Ship Select List: {ship.Name}");
                var shipSelection = ShipSelectionContainer.GetChild(i).gameObject;
                shipSelection.SetActive(true);
                shipSelection.GetComponent<Image>().sprite = ship.Icon;
                shipSelection.GetComponent<Button>().onClick.RemoveAllListeners();
                shipSelection.GetComponent<Button>().onClick.AddListener(() => SelectShip(selectionIndex));
                shipSelection.GetComponent<Button>().onClick.AddListener(() => ShipSelectionContainer.GetComponent<MenuAudio>().PlayAudio());
            }

            StartCoroutine(SelectShipCoroutine(0));
        }

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

            if (SelectedShip.Abilities.Count > 0)
                StartCoroutine(SelectAbilityCoroutine(0));
        }

        void PopulateGuideSelectionList()
        {
            if (GuideSelectionContainer == null) return;

            // Deactivate all
            for (var i = 0; i < GuideSelectionContainer.transform.childCount; i++)
                GuideSelectionContainer.GetChild(i).gameObject.SetActive(false);

            // Reactivate based on the number of abilities for the selected ship
            for (var i = 0; i < SelectedShip.Guides.Count; i++)
            {
                var selectionIndex = i;
                var guide = SelectedShip.Guides[i];
                Debug.Log($"Populating Guide Select List: {guide?.Name}");
                var guideSelection = GuideSelectionContainer.GetChild(i).gameObject;
                guideSelection.SetActive(true);
                guideSelection.GetComponent<Image>().sprite = guide?.Icon;
                guideSelection.GetComponent<Button>().onClick.RemoveAllListeners();
                guideSelection.GetComponent<Button>().onClick.AddListener(() => SelectPilot(selectionIndex));
                guideSelection.GetComponent<Button>().onClick.AddListener(() => GuideSelectionContainer.GetComponent<MenuAudio>().PlayAudio());
            }

            StartCoroutine(SelectGuideCoroutine(0));
        }

        void PopulateShipDetails()
        {
            Debug.Log($"Populating Ship Details List: {SelectedShip.Name}");
            Debug.Log($"Populating Ship Details List: {SelectedShip.Description}");
            Debug.Log($"Populating Ship Details List: {SelectedShip.Icon}");
            Debug.Log($"Populating Ship Details List: {SelectedShip.PreviewImage}");

            if (SelectedShipName != null) SelectedShipName.text = SelectedShip.Name;
            if (SelectedShipDescription != null) SelectedShipDescription.text = SelectedShip.Description;
            if (SelectedShipImage != null) SelectedShipImage.sprite = SelectedShip.PreviewImage;
            if (SelectedShipTrailImage != null) SelectedShipTrailImage.sprite = SelectedShip.TrailPreviewImage;
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
                for (var i = 2; i < SelectedAbilityPreviewWindow.transform.childCount; i++)
                    Destroy(SelectedAbilityPreviewWindow.transform.GetChild(i).gameObject);

                var preview = Instantiate(SelectedAbility.PreviewClip);
                preview.transform.SetParent(SelectedAbilityPreviewWindow.transform, false);
            }
        }

        void PopulateGuideDetails()
        {
            Debug.Log($"Populating Guide Details List: {SelectedGuide.Name}");
            Debug.Log($"Populating Guide Details List: {SelectedGuide.Description}");
            Debug.Log($"Populating Guide Details List: {SelectedGuide.Icon}");
            Debug.Log($"Populating Guide Details List: {SelectedGuide.Image}");

            if (SelectedGuideName != null) SelectedGuideName.text = SelectedGuide.Name + " - The " + SelectedGuide.PrimaryElement.ToString() + " " + SelectedGuide.Ship.Name;
            if (SelectedGuideDescription != null) SelectedGuideDescription.text = SelectedGuide.Description;
            if (SelectedGuideFlavor != null) SelectedGuideFlavor.text = SelectedGuide.Flavor;
            if (SelectedGuideImage != null) SelectedGuideImage.sprite = SelectedGuide.Image;
        }

        public void SelectShip(int index)
        {
            Debug.Log($"SelectShip: {index}");
            Debug.Log($"ShipSelectionContainer.childCount: {ShipSelectionContainer.childCount}");
            Debug.Log($"Ships.Count: {Ships.Count}");

            // Deselect them all
            for (var i = 0; i < Ships.Count; i++)
                ShipSelectionContainer.GetChild(i).gameObject.GetComponent<Image>().sprite = Ships[i].Icon;

            // Select the one
            SelectedShip = Ships[index];
            ShipSelectionContainer.GetChild(index).gameObject.GetComponent<Image>().sprite = SelectedShip.SelectedIcon;

            // notify the mini game engine that this is the ship to play
            Hangar.Instance.SetPlayerShip((int)SelectedShip.Class);

            PopulateShipDetails();

            // populate the games list with the one's games
            PopulateAbilitySelectionList();
            PopulateGuideSelectionList();
        }

        public void SelectAbility(int index)
        {
            Debug.Log($"SelectAbility: {index}");

            // Deselect them all
            for (var i = 0; i < SelectedShip.Abilities.Count; i++)
                AbilitySelectionContainer.GetChild(i).gameObject.GetComponent<Image>().sprite = SelectedShip.Abilities[i].Icon;

            // Select the one
            SelectedAbility = SelectedShip.Abilities[index];
            AbilitySelectionContainer.GetChild(index).gameObject.GetComponent<Image>().sprite = SelectedAbility.SelectedIcon;

            PopulateAbilityDetails();
        }

        /* Selects the Guide in the UI for display */
        /// <summary>
        /// Select a Guide in the UI to display its meta data
        /// TODO: Add UI Guide Assets for Urchin and Bufo when they are available
        /// </summary>
        /// <param name="index">Index of the displayed Guide list</param>
        public void SelectPilot(int index)
        {
            Debug.Log($"SelectGuide: {index}");

            try
            {
                // Deselect them all
                for (var i = 0; i < 4; i++)
                    GuideSelectionContainer.GetChild(i).gameObject.GetComponent<Image>().sprite =
                        SelectedShip.Guides[i].Icon;

                // Select the one
                SelectedGuide = SelectedShip.Guides[index];
                GuideSelectionContainer.GetChild(index).gameObject.GetComponent<Image>().sprite =
                    SelectedGuide.SelectedIcon;
            }
            catch (ArgumentOutOfRangeException argumentOutOfRangeException)
            {
                Debug.LogWarningFormat("{0} - {1} - The ship lacks guide assets. Please add them. {2}", nameof(HangarMenu),
                    nameof(SelectPilot), argumentOutOfRangeException.Message);
            }
            catch (NullReferenceException nullReferenceException)
            {
                Debug.LogWarningFormat("{0} - {1} - The ship lacks guide assets. Please add them. {2}", nameof(HangarMenu),
                    nameof(SelectPilot), nullReferenceException.Message);
            }

            PopulateGuideDetails();
        }


        IEnumerator SelectGuideCoroutine(int index)
        {
            yield return new WaitForEndOfFrame();
            SelectPilot(index);
        }

        IEnumerator SelectShipCoroutine(int index)
        {
            yield return new WaitForEndOfFrame();
            SelectShip(index);
        }

        IEnumerator SelectAbilityCoroutine(int index)
        {
            yield return new WaitForEndOfFrame();
            SelectAbility(index);
        }
    }
}