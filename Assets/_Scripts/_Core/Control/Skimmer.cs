using StarWriter.Core.Input;
using StarWriter.Core;
using System.Collections.Generic;
using UnityEngine;

public class Skimmer : MonoBehaviour
{
    [SerializeField] Ship ship;
    [SerializeField] public Player Player;
    [SerializeField] float fuelAmount;
    [SerializeField] List<SkimmerImpactEffects> skimmerImpactEffects;
    public bool thief;


    // TODO: move this away from using an event
    public delegate void Skim(string uuid, float amount);
    public static event Skim OnSkim;

    //Maja added this to try and enable shark skimmer smashing
    public void PerformSkimmerImpactEffects(TrailBlockProperties trailBlockProperties)
    {
        foreach (SkimmerImpactEffects effect in skimmerImpactEffects)
        {
            switch (effect)
            {
                case SkimmerImpactEffects.PlayHaptics:
                    HapticController.PlayBlockCollisionHaptics();
                    break;
                case SkimmerImpactEffects.DeactivateTrailBlock:
                    break;
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