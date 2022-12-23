using System.Collections.Generic;
using UnityEngine;

public class Skimmer : MonoBehaviour
{
    [SerializeField] public Ship ship;
    [SerializeField] public Player Player;
    [SerializeField] float fuelAmount;
    [SerializeField] List<TrailBlockImpactEffects> trailBlockImpactEffects;
    public Teams team;
    public bool thief;


    // TODO: move this away from using an event
    public delegate void Skim(string uuid, float amount);
    public static event Skim OnSkim;

    //Maja added this to try and enable shark skimmer smashing
    public void PerformSkimmerImpactEffects(TrailBlockProperties trailBlockProperties)
    {
        foreach (TrailBlockImpactEffects effect in trailBlockImpactEffects)
        {
            switch (effect)
            {
                case TrailBlockImpactEffects.PlayHaptics:
                    HapticController.PlayBlockCollisionHaptics();
                    break;
                case TrailBlockImpactEffects.DeactivateTrailBlock:
                    trailBlockProperties.trail.Explode(ship.transform.forward * ship.GetComponent<ShipData>().speed, team);
                    ScoringManager.Instance.BlockDestroyed(team, Player.PlayerName, trailBlockProperties);
                    break;
                // This is actually redundant with Skimmer's built in "Fuel Amount" variable
                //case TrailBlockImpactEffects.ChangeFuel:
                    //FuelSystem.ChangeFuelAmount(Player.PlayerUUID, ship.blockFuelChange);
                    //break;
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log("skim collider: " + other.name);
        OnSkim?.Invoke(ship.Player.PlayerUUID, fuelAmount);

        var trail = other.GetComponent<Trail>();
        if (trail != null)
        {
            trail.InstantiateParticle(transform);
            
            if (thief)
            {
                trail.Team = Player.Team;
                trail.PlayerName = ship.Player.PlayerName;

                // TODO: update control/scoring stats
            }
        }
    }
}