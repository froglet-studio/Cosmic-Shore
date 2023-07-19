using StarWriter.Core.HangerBuilder;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// TODO: P1, move to enum folder
public enum PilotElement
{
    Charge = 1,
    Mass = 2,
    Space = 3,
    Time = 4,
}

public class HangarMenu : MonoBehaviour
{
    [SerializeField] SO_ShipList ShipList;
    [SerializeField] TMPro.TMP_Text SelectedShipName;
    [SerializeField] TMPro.TMP_Text SelectedShipDescription;
    [SerializeField] Image SelectedShipImage;
    [SerializeField] Image SelectedShipTrailImage;
    [SerializeField] TMPro.TMP_Text SelectedAbilityName;
    [SerializeField] TMPro.TMP_Text SelectedAbilityDescription;
    [SerializeField] GameObject SelectedAbilityPreviewWindow;

    [SerializeField] Transform ShipSelectionContainer;
    [SerializeField] Transform AbilitySelectionContainer;

    List<SO_Ship> Ships;
    SO_Ship SelectedShip;
    SO_Pilot SelectedPilot;
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
        }

        if (SelectedShip.Abilities.Count > 0)
            StartCoroutine(SelectAbilityCoroutine(0));
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
    }

    public void SelectPilot(int elementInt)
    {
        var element = (PilotElement)elementInt;
        switch (element)
        {
            case PilotElement.Charge:
                SelectedPilot = SelectedShip.ChargePilot;
                break;
            case PilotElement.Mass:
                SelectedPilot = SelectedShip.MassPilot;
                break;
            case PilotElement.Space:
                SelectedPilot = SelectedShip.SpacePilot;
                break;
            case PilotElement.Time:
                SelectedPilot = SelectedShip.TimePilot;
                break;
        }

        Hangar.Instance.SetPlayerPilot(SelectedPilot);
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