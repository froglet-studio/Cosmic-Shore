using StarWriter.Core.HangerBuilder;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

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

    [FormerlySerializedAs("PilotSelectionContainer")]
    [SerializeField] Transform VesselSelectionContainer;
    [FormerlySerializedAs("SelectedPilotName")]
    [SerializeField] TMPro.TMP_Text SelectedVesselName;
    [FormerlySerializedAs("SelectedPilotDescription")]
    [SerializeField] TMPro.TMP_Text SelectedVesselDescription;
    [FormerlySerializedAs("SelectedPilotImage")]
    [SerializeField] Image SelectedVesselImage;

    List<SO_Ship> Ships;
    SO_Ship SelectedShip;
    SO_Vessel SelectedVessel;
    SO_ShipAbility SelectedAbility;

    void Start()
    {
        Ships = ShipList.ShipList;
        PopulateShipSelectionList();
    }

    void PopulateShipSelectionList()
    {
        if (ShipSelectionContainer == null) return;

        // Deactivate all
        for (var i = 0; i < ShipSelectionContainer.childCount; i++)
            ShipSelectionContainer.GetChild(i).gameObject.SetActive(false);

        // Reactivate based on the number of ships
        for (var i = 0; i < Ships.Count; i++)
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

    void PopulateVesselSelectionList()
    {
        if (VesselSelectionContainer == null) return;

        // Deactivate all
        for (var i = 0; i < VesselSelectionContainer.transform.childCount; i++)
            VesselSelectionContainer.GetChild(i).gameObject.SetActive(false);

        // Reactivate based on the number of abilities for the selected ship
        for (var i = 0; i < SelectedShip.Vessels.Count; i++)
        {
            var selectionIndex = i;
            var vessel = SelectedShip.Vessels[i];
            Debug.Log($"Populating Vessel Select List: {vessel.Name}");
            var vesselSelection = VesselSelectionContainer.GetChild(i).gameObject;
            vesselSelection.SetActive(true);
            vesselSelection.GetComponent<Image>().sprite = vessel.Icon;
            vesselSelection.GetComponent<Button>().onClick.RemoveAllListeners();
            vesselSelection.GetComponent<Button>().onClick.AddListener(() => SelectPilot(selectionIndex));
            vesselSelection.GetComponent<Button>().onClick.AddListener(() => VesselSelectionContainer.GetComponent<MenuAudio>().PlayAudio());
        }

        StartCoroutine(SelectVesselCoroutine(0));
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
        if (SelectedShipTrailImage !=null) SelectedShipTrailImage.sprite = SelectedShip.TrailPreviewImage;
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

    void PopulateVesselDetails()
    {
        Debug.Log($"Populating Vessel Details List: {SelectedVessel.Name}");
        Debug.Log($"Populating Vessel Details List: {SelectedVessel.Description}");
        Debug.Log($"Populating Vessel Details List: {SelectedVessel.Icon}");
        Debug.Log($"Populating Vessel Details List: {SelectedVessel.Image}");

        if (SelectedVesselName != null) SelectedVesselName.text = SelectedVessel.Name;
        if (SelectedVesselDescription != null) SelectedVesselDescription.text = SelectedVessel.Description;
        if (SelectedVesselImage != null) SelectedVesselImage.sprite = SelectedVessel.Image;
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
        PopulateVesselSelectionList();
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

    /* Selects the vessel in the UI for display */
    /// <summary>
    /// Select a vessel in the UI to display its meta data
    /// </summary>
    /// <param name="index">Index of the displayed vessel list</param>
    public void SelectPilot(int index)
    {
        Debug.Log($"SelectVessel: {index}");

        // Deselect them all
        for (var i = 0; i < 4; i++)
            VesselSelectionContainer.GetChild(i).gameObject.GetComponent<Image>().sprite = SelectedShip.Vessels[i].Icon;

        // Select the one
        SelectedVessel = SelectedShip.Vessels[index];
        VesselSelectionContainer.GetChild(index).gameObject.GetComponent<Image>().sprite = SelectedVessel.SelectedIcon;

        PopulateVesselDetails();
    }

    IEnumerator SelectVesselCoroutine(int index)
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