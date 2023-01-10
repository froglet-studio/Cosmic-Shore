using StarWriter.Core.Input;
using StarWriter.Core;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(TrailSpawner))]
public class Ship : MonoBehaviour
{
    CameraManager cameraManager;

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
    [SerializeField] float rotationThrottleScaler;
    [SerializeField] float maxExplosionScale = 400;
    [SerializeField] float blockFuelChange;
    [SerializeField] float closeCamDistance;
    [SerializeField] float farCamDistance;
    [SerializeField] GameObject head;
    [SerializeField] GameObject ShipRotationOverride;

    bool invulnerable;
    [SerializeField] ShipTypes SecondMode = ShipTypes.Shark;
    Ship secondShip;
    Ship[] ships;

    Vector3 initialDirection = Vector3.zero;

    Teams team;
    ShipData shipData;
    InputController inputController;
    Material ShipMaterial;
    List<ShipGeometry> shipGeometries = new List<ShipGeometry>();

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
        cameraManager = CameraManager.Instance;
        shipData = GetComponent<ShipData>();
        inputController = player.GetComponent<InputController>();
        PerformShipPassiveEffects(passiveEffects);
    }
    void Update()
    {
        ApplySpeedModifiers();
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
                case PassiveAbilities.CloseCam:
                    cameraManager.SetCloseCameraDistance(closeCamDistance);
                    break;
                case PassiveAbilities.FarCam:
                    cameraManager.SetFarCameraDistance(farCamDistance);
                    break;
                case PassiveAbilities.SecondMode:
                    ships = Player.LoadSecondShip(SecondMode);
                    break;
                case PassiveAbilities.SpeedBasedTurning:
                    inputController.rotationThrottleScaler = rotationThrottleScaler;
                    break;
                case PassiveAbilities.DensityBasedBlockSize:
                    // TODO: WIP Density based block size

                    break;
            }
        }
    }

    public void PerformCrystalImpactEffects(CrystalProperties crystalProperties)
    {
        ScoringManager.Instance.CrystalCollected(this, crystalProperties);

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
                    var AOEExplosion = Instantiate(AOEPrefab).GetComponent<AOEExplosion>();
                    AOEExplosion.Team = team;
                    AOEExplosion.Ship = this;
                    AOEExplosion.transform.SetPositionAndRotation(transform.position,transform.rotation);
                    AOEExplosion.MaxScale = 50 + FuelSystem.CurrentFuel * maxExplosionScale;

                    if (typeof(AOEExplosion) == typeof(AOEBlockCreation))
                        ((AOEBlockCreation)AOEExplosion).SetBlockMaterial(TrailSpawner.GetBlockMaterial());
                    break;
                case CrystalImpactEffects.FillFuel:
                    FuelSystem.ChangeFuelAmount(player.PlayerUUID, crystalProperties.fuelAmount);
                    break;
                case CrystalImpactEffects.Boost:
                    SpeedModifiers.Add(new SpeedModifier(crystalProperties.speedBuffAmount, 4 * speedModifierDuration, 0));
                    break;
                case CrystalImpactEffects.DrainFuel:
                    FuelSystem.ChangeFuelAmount(player.PlayerUUID, -FuelSystem.CurrentFuel);
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
                case TrailBlockImpactEffects.DrainHalfFuel:
                    FuelSystem.ChangeFuelAmount(player.PlayerUUID, -FuelSystem.CurrentFuel/2f);
                    break;
                case TrailBlockImpactEffects.DebuffSpeed:
                    SpeedModifiers.Add(new SpeedModifier(trailBlockProperties.speedDebuffAmount, speedModifierDuration, 0));
                    break;
                case TrailBlockImpactEffects.DeactivateTrailBlock:
                    break;
                case TrailBlockImpactEffects.ActivateTrailBlock:
                    break;
                case TrailBlockImpactEffects.OnlyBuffSpeed:
                    if (trailBlockProperties.speedDebuffAmount > 1) SpeedModifiers.Add(new SpeedModifier(trailBlockProperties.speedDebuffAmount, speedModifierDuration, 0));
                    break;
                case TrailBlockImpactEffects.ChangeFuel:
                    FuelSystem.ChangeFuelAmount(player.PlayerUUID, blockFuelChange);
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
                    if (initialDirection == Vector3.zero) initialDirection = transform.forward;
                    cameraManager.DriftCam(initialDirection, transform.forward);
                    break;
                case ActiveAbilities.Boost:
                    if (FuelSystem.CurrentFuel > 0)
                    {
                        inputController.BoostShip(boostMultiplier, boostFuelAmount); // TODO move fuel change out of inputController
                        shipData.boost = true; // TODO make a block change ability instead
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
                    head.transform.localScale *= 1.02f; // TODO make this its own ability 
                    break;
                case ActiveAbilities.ToggleCamera:
                    CameraManager.Instance.ToggleCloseOrFarCamOnPhoneFlip(true);
                    TrailSpawner.ToggleBlockWaitTime(true);
                    break;
                case ActiveAbilities.ToggleMode:
                    // TODO
                    break;
                case ActiveAbilities.ToggleGyro:
                    inputController.OnToggleGyro(true);
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
                    inputController.StartBoostWithDecay();
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
                    CameraManager.Instance.ToggleCloseOrFarCamOnPhoneFlip(false);
                    TrailSpawner.ToggleBlockWaitTime(false);
                    break;
                case ActiveAbilities.ToggleMode:
                    // TODO
                    break;
                case ActiveAbilities.ToggleGyro:
                    inputController.OnToggleGyro(false);
                    break;
            }
        }
    }

    void ApplySpeedModifiers()
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

    public void SetBlockMaterial(Material material)
    {
        TrailSpawner.SetBlockMaterial(material);
    }

    public void FlipShipUpsideDown()
    {
        ShipRotationOverride.transform.localRotation = Quaternion.Euler(0, 0, 180);
    }
    public void FlipShipRightsideUp()
    {
        ShipRotationOverride.transform.localRotation = Quaternion.Euler(0, 0, 0);
    }
    void ApplyShipMaterial()
    {
        if (ShipMaterial == null)
            return;

        foreach (var shipGeometry in shipGeometries)
            shipGeometry.GetComponent<MeshRenderer>().material = ShipMaterial;
    }
}