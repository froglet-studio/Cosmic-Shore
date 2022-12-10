using StarWriter.Core.Input;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(TrailSpawner))]
public class Ship : MonoBehaviour
{
    [SerializeField] string Name;
    [SerializeField] public ShipTypes ShipType;
    [SerializeField] GameObject AOEPrefab;
    [SerializeField] Player player;
    [SerializeField] public TrailSpawner TrailSpawner;
    [SerializeField] List<CrystalImpactEffects> crystalImpactEffects;
    [SerializeField] List<TrailBlockImpactEffects> trailBlockImpactEffects;

    Teams team;
    ShipData shipData;
    private Material ShipMaterial;
    private List<ShipGeometry> shipGeometries = new List<ShipGeometry>();

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

    public Teams Team { get => team; set => team = value; }
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
        foreach (CrystalImpactEffects effect in crystalImpactEffects)
        {
            switch (effect)
            {
                case CrystalImpactEffects.PlayHaptics:
                    HapticController.PlayCrystalImpactHaptics();
                    break;
                case CrystalImpactEffects.AreaOfEffectExplosion:
                    // Spawn AOE explosion
                    // TODO: add position to crystal properties? use crystal properties to set position
                    var AOEExplosion = Instantiate(AOEPrefab);
                    AOEExplosion.GetComponent<AOEExplosion>().Team = team;
                    AOEExplosion.transform.position = transform.position;
                    break;
                case CrystalImpactEffects.FillFuel:
                    FuelSystem.ChangeFuelAmount(player.PlayerUUID, crystalProperties.fuelAmount);
                    break;
                case CrystalImpactEffects.Score:
                    ScoringManager.Instance.UpdateScore(player.PlayerUUID, crystalProperties.scoreAmount);
                    break;
                case CrystalImpactEffects.ResetAggression:
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
        foreach (TrailBlockImpactEffects effect in trailBlockImpactEffects)
        {
            switch (effect)
            {
                case TrailBlockImpactEffects.PlayHaptics:
                    HapticController.PlayBlockCollisionHaptics();
                    break;
                case TrailBlockImpactEffects.DrainFuel:
                    break;
                case TrailBlockImpactEffects.DebuffSpeed:
                    SpeedModifiers.Add(new SpeedModifier(trailBlockProperties.speedDebuffAmount, speedModifierDuration, 0));
                    break;
                case TrailBlockImpactEffects.DeactivateTrailBlock:
                    break;
                case TrailBlockImpactEffects.ActivateTrailBlock:
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

    public void RegisterShipGeometry(ShipGeometry shipGeometry)
    {
        shipGeometries.Add(shipGeometry);
        ApplyShipMaterial();
    }

    public void SetShipMaterial(Material material)
    {
        ShipMaterial = material;
        ApplyShipMaterial();
    }

    private void ApplyShipMaterial()
    {
        if (ShipMaterial == null)
            return;

        foreach (var shipGeometry in shipGeometries)
            shipGeometry.GetComponent<MeshRenderer>().material = ShipMaterial;
    }
}