using StarWriter.Core.Input;
using UnityEngine;

public class Ship : MonoBehaviour
{
    [SerializeField] SO_Ship shipSO;
    [SerializeField] GameObject AOEPrefab;
    [SerializeField] Player player;
    Team team;

    public string ShipName { get => shipSO.Name; }
    public SO_Ship ShipSO { get => shipSO; set => shipSO = value; }
    public Team Team { get => team; set => team = value; }
    public Player Player { get => player; set => player = value; }

    public void PerformCrystalImpactEffects(CrystalProperties crystalProperties)
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
                    // TODO: add position to crystal properties? use crystal properties to set position
                    var AOEExplosion = Instantiate(AOEPrefab);
                    AOEExplosion.GetComponent<AOEExplosion>().Team = team;
                    AOEExplosion.transform.position = transform.position; 
                    break;
                case CrystalImpactEffect.FillFuel:
                    FuelSystem.ChangeFuelAmount(player.PlayerUUID, crystalProperties.fuelAmount);
                    break;
                case CrystalImpactEffect.Score:
                    ScoringManager.Instance.UpdateScore(player.PlayerUUID, crystalProperties.scoreAmount);
                    break;
                case CrystalImpactEffect.ResetAggression:
                    // TODO: PLAYERSHIP null pointer here
                    AIPilot controllerScript = gameObject.GetComponent<AIPilot>();
                    controllerScript.lerp = controllerScript.defaultLerp;
                    controllerScript.throttle = controllerScript.defaultThrottle;
                    break;
            }
        }
    }

    public void PerformTrailBlockImpactEffects(TrailBlockProperties trailBlockProperties)
    {
        foreach (TrailBlockImpactEffect effect in shipSO.TrailBlockImpactEffects)
        {
            switch (effect)
            {
                case TrailBlockImpactEffect.PlayHaptics:
                    HapticController.PlayBlockCollisionHaptics();
                    break;
                case TrailBlockImpactEffect.DrainFuel:
                    break;
                case TrailBlockImpactEffect.DebuffSpeed:
                    // TODO: replace this with a coroutine
                    GetComponent<ShipData>().speed *= trailBlockProperties.speedDebuffAmount;
                    break;
                case TrailBlockImpactEffect.DeactivateTrailBlock:
                    break;
                case TrailBlockImpactEffect.ActivateTrailBlock:
                    break;
            }
        }
    }

    public void ToggleCollision(bool enabled)
    {
        foreach (var collider in GetComponentsInChildren<Collider>(true))
            collider.enabled = enabled;
    }
}