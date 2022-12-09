using StarWriter.Core.Input;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(TrailSpawner))]
public class Ship : MonoBehaviour
{
    [SerializeField] SO_Ship shipSO;
    [SerializeField] string Name;
    [SerializeField] GameObject AOEPrefab;
    [SerializeField] Player player;
    [SerializeField] public TrailSpawner TrailSpawner;
    [SerializeField] List<CrystalImpactEffect> crystalImpactEffects;
    [SerializeField] List<TrailBlockImpactEffect> trailBlockImpactEffects;

    Team team;
    ShipData shipData;

    class SpeedModifier
    {
        public float initialValue;
        public float duration;
        public float elapsedTime;

        public SpeedModifier(float initialValue, float duration, float elapsedTime)
        {
            this.initialValue = initialValue;
            this.duration = duration;
            this.elapsedTime = elapsedTime;
        }
    }

    List<SpeedModifier> SpeedModifiers = new List<SpeedModifier>();
    float speedModifierDuration = 2f;
    float speedModifierMax = 6f;

    public Team Team { get => team; set => team = value; }
    public Player Player { get => player; set => player = value; }

    public void Start()
    {
        shipData = GetComponent<ShipData>();
    }
    void Update()
    {
        ApplySpeedModifiers();
    }

    public void PerformCrystalImpactEffects(CrystalProperties crystalProperties)
    {
        foreach (CrystalImpactEffect effect in crystalImpactEffects)
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
        foreach (TrailBlockImpactEffect effect in trailBlockImpactEffects)
        {
            switch (effect)
            {
                case TrailBlockImpactEffect.PlayHaptics:
                    HapticController.PlayBlockCollisionHaptics();
                    break;
                case TrailBlockImpactEffect.DrainFuel:
                    break;
                case TrailBlockImpactEffect.DebuffSpeed:
                    SpeedModifiers.Add(new SpeedModifier(trailBlockProperties.speedDebuffAmount, speedModifierDuration, 0));
                    break;
                case TrailBlockImpactEffect.DeactivateTrailBlock:
                    break;
                case TrailBlockImpactEffect.ActivateTrailBlock:
                    break;
            }
        }
    }

    private void ApplySpeedModifiers()
    {
        float accumulatedSpeedModification = 1; 
        for (int i = SpeedModifiers.Count-1; i >= 0; i--)
        {
            var modifier = SpeedModifiers[i];

            modifier.elapsedTime += Time.deltaTime;

            if (modifier.elapsedTime >= modifier.duration)
                SpeedModifiers.RemoveAt(i);
            else
                accumulatedSpeedModification *= Mathf.Lerp(modifier.initialValue, 1f, modifier.elapsedTime / modifier.duration);
        }

        accumulatedSpeedModification = Mathf.Min(accumulatedSpeedModification, speedModifierMax);
        shipData.speedMultiplier = accumulatedSpeedModification;
    }

 

    public void ToggleCollision(bool enabled)
    {
        foreach (var collider in GetComponentsInChildren<Collider>(true))
            collider.enabled = enabled;
    }


    IEnumerator DebuffSpeedCoroutine(TrailBlockProperties trailBlockProperties)
    {
        var speedMultiplierDelta = -trailBlockProperties.speedDebuffAmount; //this is so multiple debuffs can run in parallel
        shipData.speedMultiplier -= trailBlockProperties.speedDebuffAmount;

        var speedReturnRate = .01f; // this might cause errors if this is changed

        while (speedMultiplierDelta < 0)
        {
            speedMultiplierDelta += speedReturnRate;
            shipData.speedMultiplier += speedReturnRate;
            yield return null;
        }
    }
}