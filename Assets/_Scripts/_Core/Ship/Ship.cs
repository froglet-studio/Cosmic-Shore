using StarWriter.Core.Input;
using StarWriter.Core;
using System.Collections.Generic;
using UnityEngine;
using Mono.Cecil.Cil;

[RequireComponent(typeof(TrailSpawner))]
public class Ship : MonoBehaviour
{
    public delegate void TrailCollision(string uuid, float amount);
    public static event TrailCollision OnTrailCollision;


    [SerializeField] string Name;
    [SerializeField] public ShipTypes ShipType;
    [SerializeField] public TrailSpawner TrailSpawner;
    [SerializeField] public Skimmer skimmer;
    [SerializeField] GameObject AOEPrefab;
    [SerializeField] Player player;
    [SerializeField] List<CrystalImpactEffects> crystalImpactEffects;
    [SerializeField] List<TrailBlockImpactEffects> trailBlockImpactEffects;

    [SerializeField] List<ActiveAbilities> fullSpeedStraightEffects;
    [SerializeField] List<ActiveAbilities> rightStickEffects;
    [SerializeField] List<ActiveAbilities> leftStickEffects;
    [SerializeField] List<ActiveAbilities> flipEffects;

    [SerializeField] List<PassiveAbilities> passiveEffects;

    [SerializeField] float boostMultiplier = 4f;
    [SerializeField] float boostFuelAmount = -.01f;
    [SerializeField] float rotationScaler = 130;
    [SerializeField] GameObject head;
    bool invulnerable;

    Teams team;
    ShipData shipData;
    InputController inputController;
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
        inputController = player.GetComponent<InputController>();
        PerformShipPassiveEffects(passiveEffects);

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
                    //OnTrailCollision?.Invoke(ownerId, fuelChange);
                    break;
                case TrailBlockImpactEffects.DebuffSpeed:
                    SpeedModifiers.Add(new SpeedModifier(trailBlockProperties.speedDebuffAmount, speedModifierDuration, 0));
                    break;
                case TrailBlockImpactEffects.DeactivateTrailBlock:
                    break;
                case TrailBlockImpactEffects.ActivateTrailBlock:
                    break;
                case TrailBlockImpactEffects.OnlyBuffSpeed:
                    if (trailBlockProperties.speedDebuffAmount < 1) SpeedModifiers.Add(new SpeedModifier(1/trailBlockProperties.speedDebuffAmount, speedModifierDuration, 0));
                    else SpeedModifiers.Add(new SpeedModifier(trailBlockProperties.speedDebuffAmount, speedModifierDuration, 0));
                    break;
            }
        }
    }

    public void PerformFullSpeedStraightEffects()
    {
        PerformShipAbilitiesEffects(fullSpeedStraightEffects);
    }
    public void PerformRightStickEffectsEffects()
    {
        PerformShipAbilitiesEffects(rightStickEffects);
    }
    public void PerformLeftStickEffectsEffects()
    {
        PerformShipAbilitiesEffects(leftStickEffects);
    }
    public void StartFlipEffects()
    {
        PerformShipAbilitiesEffects(flipEffects);
    }

    public void StopFullSpeedStraightEffects()
    {
        StopShipAbilitiesEffects(fullSpeedStraightEffects);
    }
    public void StopRightStickEffects()
    {
        StopShipAbilitiesEffects(rightStickEffects);
    }
    public void StopLeftStickEffects()
    {
        StopShipAbilitiesEffects(leftStickEffects);
    }
    public void StopFlipEffects()
    {
        StopShipAbilitiesEffects(flipEffects);
    }

    void PerformShipAbilitiesEffects(List<ActiveAbilities> shipAbilities)
    {
        foreach (ActiveAbilities effect in shipAbilities)
        {
            switch (effect)
            {
                case ActiveAbilities.Drift:
                    inputController.Drift();
                    break;
                case ActiveAbilities.Boost:
                    if (FuelSystem.CurrentFuel > 0)
                    {
                        inputController.BoostShip(boostMultiplier, boostFuelAmount); // TODO move fuel change out of inputController
                        shipData.boost = true;
                    }
                    else StopFullSpeedStraightEffects(); // TODO this will stop other effects
                    break;
                case ActiveAbilities.Invulnerability:
                    if (!invulnerable)
                    {
                        invulnerable = true;
                        trailBlockImpactEffects.Remove(TrailBlockImpactEffects.DebuffSpeed);
                        trailBlockImpactEffects.Add(TrailBlockImpactEffects.OnlyBuffSpeed);
                    }
                    head.transform.localScale *= 1.02f;
                    break;
                case ActiveAbilities.ToggleCamera:
                    GameManager.Instance.PhoneFlipState = true; // TODO: remove Game manager dependency
                    break;
            }
        }
    }

    void StopShipAbilitiesEffects(List<ActiveAbilities> shipAbilities)
    {
        foreach (ActiveAbilities effect in shipAbilities)
        {
            switch (effect)
            {
                case ActiveAbilities.Drift:
                    inputController.drifting = false;
                    StartCoroutine(inputController.DecayingBoost());
                    break;
                case ActiveAbilities.Boost:
                    shipData.boost = false;
                    break;
                case ActiveAbilities.Invulnerability:
                    invulnerable = false;
                    trailBlockImpactEffects.Add(TrailBlockImpactEffects.DebuffSpeed);
                    trailBlockImpactEffects.Remove(TrailBlockImpactEffects.OnlyBuffSpeed);
                    head.transform.localScale = Vector3.one;
                    break;
                case ActiveAbilities.ToggleCamera:
                    GameManager.Instance.PhoneFlipState = false;
                    break;
            }
        }
    }

    void PerformShipPassiveEffects(List<PassiveAbilities> passiveEffects)
    {
        foreach (PassiveAbilities effect in passiveEffects)
        {
            switch (effect)
            {
                case PassiveAbilities.TurnSpeed:
                    inputController.rotationScaler = rotationScaler;
                    break;
                case PassiveAbilities.BlockThief:
                    skimmer.thief = true;
                    break;
                case PassiveAbilities.BlockScout:
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