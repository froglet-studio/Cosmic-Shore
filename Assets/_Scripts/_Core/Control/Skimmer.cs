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

    Dictionary<string, float> skimStartTimes = new Dictionary<string, float>();

    [SerializeField] float MultiSkimMultiplier = 0f;
    int activelySkimmingBlockCount = 0;

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

                    if (NodeControlManager.Instance != null)
                    {
                        // Node control tracking
                        NodeControlManager.Instance.RemoveBlock(team, Player.PlayerName, trailBlockProperties);
                    }
                    break;
                case TrailBlockImpactEffects.Steal:
                    trailBlockProperties.trail.ConvertToTeam(Player.PlayerName, team);
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

        var trail = other.GetComponent<Trail>();
        if (trail != null)
        {
            activelySkimmingBlockCount++;
            trail.InstantiateParticle(transform);

            if (thief && trail.Team != Player.Team)
                trail.ConvertToTeam(Player.PlayerName, Player.Team);

            skimStartTimes.Add(trail.ID, Time.time);

            OnSkim?.Invoke(ship.Player.PlayerUUID, fuelAmount + (activelySkimmingBlockCount * MultiSkimMultiplier));
        }
    }

    void OnTriggerStay(Collider other)
    {
        float skimDecayDuration = 1;

        var trail = other.GetComponent<Trail>();
        if (trail != null)
        {
            // start with a baseline fuel amount the ranges from 0-1 depending on proximity of the skimmer to the trail block
            var fuel = fuelAmount * (1 - (Vector3.Magnitude(transform.position - other.transform.position) / transform.localScale.x));

            // apply decay
            fuel *= Mathf.Min(0, (skimDecayDuration - (Time.time - skimStartTimes[trail.ID])) / skimDecayDuration);

            // apply multiskim multiplier
            fuel += (activelySkimmingBlockCount * MultiSkimMultiplier);

            // grant the fuel
            OnSkim?.Invoke(ship.Player.PlayerUUID, fuel);
        }
    }

    void OnTriggerExit(Collider other)
    {
        var trail = other.GetComponent<Trail>();
        if (trail != null)
        {
            skimStartTimes.Remove(trail.ID);
            activelySkimmingBlockCount--;
        }
    }
}