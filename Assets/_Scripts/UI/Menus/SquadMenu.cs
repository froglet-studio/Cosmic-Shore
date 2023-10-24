using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SquadMenu : MonoBehaviour
{
    [SerializeField] GameObject VesselSelectionGrid;
    [SerializeField] HorizontalLayoutGroup VesselSelectionRowPrefab;
    [SerializeField] VesselCard VesselCardPrefab;

    //[SerializeField] Inventory PlayerInventory;
    [SerializeField] SO_ShipList PlayerShips;

    public int ActiveSquadMember = 0;

    void Start()
    {
        foreach (var ship in PlayerShips.ShipList)
        {
            var row = Instantiate(VesselSelectionRowPrefab);
            var vessels = new List<SO_Vessel>();
            foreach (var vessel in ship.Vessels)
            {
                vessels.Add(vessel);
            }
            vessels.Sort((x, y) => { return x.PrimaryElement < y.PrimaryElement ? 1 : -1; });

            foreach (var vessel in vessels)
            {
                var vesselCard = Instantiate(VesselCardPrefab);
                vesselCard.Vessel = vessel;
                vesselCard.transform.parent = row.transform;
            }

            row.transform.parent = VesselSelectionGrid.transform;
        }
    }

    public void AssignVessel(SO_Vessel Vessel)
    {
        switch (ActiveSquadMember)
        {
            case 0:
                SquadSystem.SetSquadLeader(Vessel);
                break;
            case 1:
                SquadSystem.SetRogueOne(Vessel);
                break;
            default:
                SquadSystem.SetRogueTwo(Vessel);
                break;
        }

        SquadSystem.SaveSquad();
    }

    public void SetSquadLeaderAssignmentActive()
    {
        ActiveSquadMember = 0;
    }
    public void SetRogueOneAssignmentActive()
    {
        ActiveSquadMember = 1;
    }
    public void SetRogueTwoAssignmentActive()
    {
        ActiveSquadMember = 2;
    }
}