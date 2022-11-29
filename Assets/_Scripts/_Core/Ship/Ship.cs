using StarWriter.Core.Input;
using UnityEngine;

public class Ship : MonoBehaviour
{
    [SerializeField] string shipUUID;
    [SerializeField] SO_Ship shipSO;
    [SerializeField] GameObject AOEPrefab;

    public enum CrystalImpactEffect
    {
        PlayHaptics,
        Refuel,
        Score,
        AreaOfEffectExplosion,
        ResetAggression,
    }

    public enum TrailBlockImpactEffect
    {
        DeactivateTrailBlock,
        ReactivateTrailBlock,
    }

    public string ShipName { get => shipSO.Name; }
    public string ShipUUID { get => shipUUID; }
    public SO_Ship ShipSO { get => shipSO; set => shipSO = value; }

    public void PerformCrystalImpactEffects(MutonPopUp.CrystalProperties crystalProperties)
    {
        foreach (CrystalImpactEffect effect in shipSO.CrystalImpactEffects)
        {
            switch (effect)
            {
                case CrystalImpactEffect.PlayHaptics:
                    HapticController.PlayCrystalImpactHaptics();
                    break;
                case CrystalImpactEffect.AreaOfEffectExplosion:
                    // Spawn AOE explosion
                    var AOEExplosion = Instantiate(AOEPrefab);
                    // TODO: make explosion not collide with ships
                    // TODO: add position to crystal properties? use crystal properties to set position
                    AOEExplosion.transform.position = transform.position; 
                    // Do the thing
                    break;
                case CrystalImpactEffect.Refuel:
                    // TODO: the below line assumes this script will live on an object containing the player script -> could be more robust
                    FuelSystem.ChangeFuelAmount(gameObject.GetComponent<Player>().PlayerUUID, crystalProperties.fuelAmount);
                    break;
                case CrystalImpactEffect.Score:
                    // 
                    break;
                case CrystalImpactEffect.ResetAggression:
                    AiShipController controllerScript = gameObject.GetComponent<AiShipController>();
                    controllerScript.lerp = controllerScript.defaultLerp;
                    controllerScript.throttle = controllerScript.defaultThrottle;
                    break;
            }
        }
    }

    // TODO: public void PerformTrailBlockImpactEffects(TrailBlockProperties trailBlockProperties)
}