using System.Collections.Generic;
using StarWriter.Core.Input;
using UnityEngine;

namespace StarWriter.Core
{
    // TODO: pull into separate file
    public enum ShipActiveAbilityTypes
    {
        FullSpeedStraightAbility = 0,
        RightStickAbility = 1,
        LeftStickAbility = 2,
        FlipAbility = 3,
    }

    // TODO: pull into separate file
    public struct ShipSpeedModifier
    {
        public float initialValue;
        public float duration;
        public float elapsedTime;

        public ShipSpeedModifier(float initialValue, float duration, float elapsedTime)
        {
            this.initialValue = initialValue;
            this.duration = duration;
            this.elapsedTime = elapsedTime;
        }
    }

    [RequireComponent(typeof(ResourceSystem))]
    [RequireComponent(typeof(TrailSpawner))]
    public class Ship : MonoBehaviour
    {
        CameraManager cameraManager;

        [SerializeField] string Name;
        [SerializeField] public ShipTypes ShipType;
        [SerializeField] public TrailSpawner TrailSpawner;  // TODO: this should not be serialized -> pull from required component instead
        [SerializeField] public Skimmer skimmer;
        [SerializeField] GameObject AOEPrefab;
        
        [SerializeField] List<CrystalImpactEffects> crystalImpactEffects;
        [SerializeField] List<TrailBlockImpactEffects> trailBlockImpactEffects;

        public float boostMultiplier = 4f;
        public float boostFuelAmount = -.01f;
        [SerializeField] float rotationScaler = 130;
        [SerializeField] float rotationThrottleScaler;
        [SerializeField] float minExplosionScale = 50;
        [SerializeField] float maxExplosionScale = 400;
        [SerializeField] float blockFuelChange;
        [SerializeField] float closeCamDistance;
        [SerializeField] float farCamDistance;
        [SerializeField] GameObject head;
        [SerializeField] GameObject ShipRotationOverride;
        [SerializeField] ShipTypes SecondMode = ShipTypes.Shark;

        [Header("Dynamically Assignable Controls")]
        [SerializeField] List<ActiveAbilities> fullSpeedStraightEffects;
        [SerializeField] List<ActiveAbilities> rightStickEffects;
        [SerializeField] List<ActiveAbilities> leftStickEffects;
        [SerializeField] List<ActiveAbilities> flipEffects;

        [Header("Control Overrides")]
        [SerializeField] List<ShipControlOverrides> controlOverrides;

        bool invulnerable;
        Teams team;
        ShipData shipData; // TODO: this should be a required component or just a series of properties on the ship
        Player player;
        InputController inputController;
        Material ShipMaterial;
        Material AOEExplosionMaterial;
        ResourceSystem resourceSystem;
        readonly List<ShipGeometry> shipGeometries = new List<ShipGeometry>();
        readonly List<ShipSpeedModifier> SpeedModifiers = new List<ShipSpeedModifier>();
        float speedModifierDuration = 2f;
        float speedModifierMax = 6f;
        float abilityStartTime;

        public Teams Team { get => team; set => team = value; }
        public Player Player { get => player; set => player = value; }

        void Start()
        {
            cameraManager = CameraManager.Instance;
            shipData = GetComponent<ShipData>();
            resourceSystem = GetComponent<ResourceSystem>();
            inputController = player.GetComponent<InputController>();
            ApplyShipControlOverrides(controlOverrides);
        }

        void Update()
        {
            ApplySpeedModifiers();
        }

        void ApplyShipControlOverrides(List<ShipControlOverrides> controlOverrides)
        {
            foreach (ShipControlOverrides effect in controlOverrides)
            {
                switch (effect)
                {
                    case ShipControlOverrides.TurnSpeed:
                        inputController.rotationScaler = rotationScaler;
                        break;
                    case ShipControlOverrides.BlockScout:
                        break;
                    case ShipControlOverrides.CloseCam:
                        cameraManager.SetCloseCameraDistance(closeCamDistance);
                        break;
                    case ShipControlOverrides.FarCam:
                        cameraManager.SetFarCameraDistance(farCamDistance);
                        break;
                    case ShipControlOverrides.SecondMode:
                        // TODO: ship mode toggling

                        break;
                    case ShipControlOverrides.SpeedBasedTurning:
                        inputController.rotationThrottleScaler = rotationThrottleScaler;
                        break;
                }
            }
        }

        public void PerformCrystalImpactEffects(CrystalProperties crystalProperties)
        {
            if (StatsManager.Instance != null)
                StatsManager.Instance.CrystalCollected(this, crystalProperties);

            foreach (CrystalImpactEffects effect in crystalImpactEffects)
            {
                switch (effect)
                {
                    case CrystalImpactEffects.PlayHaptics:
                        HapticController.PlayCrystalImpactHaptics();
                        break;
                    case CrystalImpactEffects.AreaOfEffectExplosion:
                        var AOEExplosion = Instantiate(AOEPrefab).GetComponent<AOEExplosion>();
                        AOEExplosion.Material = AOEExplosionMaterial;
                        AOEExplosion.Team = team;
                        AOEExplosion.Ship = this;
                        AOEExplosion.transform.SetPositionAndRotation(transform.position, transform.rotation);
                        AOEExplosion.MaxScale =  Mathf.Max(minExplosionScale, resourceSystem.CurrentCharge * maxExplosionScale);

                        if (AOEExplosion is AOEBlockCreation aoeBlockcreation)
                            aoeBlockcreation.SetBlockMaterial(TrailSpawner.GetBlockMaterial());

                        break;
                    case CrystalImpactEffects.IncrementCharge:
                        resourceSystem.ChangeChargeAmount(player.PlayerUUID, ChargeDisplay.OneFuelUnit);
                        break;
                    case CrystalImpactEffects.FillCharge:
                        resourceSystem.ChangeChargeAmount(player.PlayerUUID, crystalProperties.fuelAmount);
                        break;
                    case CrystalImpactEffects.Boost:
                        SpeedModifiers.Add(new ShipSpeedModifier(crystalProperties.speedBuffAmount, 4 * speedModifierDuration, 0));
                        break;
                    case CrystalImpactEffects.DrainCharge:
                        resourceSystem.ChangeChargeAmount(player.PlayerUUID, -resourceSystem.CurrentCharge);
                        break;
                    case CrystalImpactEffects.Score:
                        //if (StatsManager.Instance != null)
                        //    StatsManager.Instance.UpdateScore(player.PlayerUUID, crystalProperties.scoreAmount);
                        // TODO: Remove this impact effect, or re-introduce scoring in a separate game mode
                        break;
                    case CrystalImpactEffects.ResetAggression:
                        if (gameObject.TryGetComponent<AIPilot>(out var aiPilot))
                        {
                            aiPilot.lerp = aiPilot.defaultLerp;
                            aiPilot.throttle = aiPilot.defaultThrottle;
                        }
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
                        resourceSystem.ChangeChargeAmount(player.PlayerUUID, -resourceSystem.CurrentCharge / 2f);
                        break;
                    case TrailBlockImpactEffects.DebuffSpeed:
                        SpeedModifiers.Add(new ShipSpeedModifier(trailBlockProperties.speedDebuffAmount, speedModifierDuration, 0));
                        break;
                    case TrailBlockImpactEffects.DeactivateTrailBlock:
                        break;
                    case TrailBlockImpactEffects.ActivateTrailBlock:
                        break;
                    case TrailBlockImpactEffects.OnlyBuffSpeed:
                        if (trailBlockProperties.speedDebuffAmount > 1) SpeedModifiers.Add(new ShipSpeedModifier(trailBlockProperties.speedDebuffAmount, speedModifierDuration, 0));
                        break;
                    case TrailBlockImpactEffects.ChangeCharge:
                        resourceSystem.ChangeChargeAmount(player.PlayerUUID, blockFuelChange);
                        break;
                    case TrailBlockImpactEffects.DecrementCharge:
                        resourceSystem.ChangeChargeAmount(player.PlayerUUID, ChargeDisplay.OneFuelUnit);
                        break;
                }
            }
        }


        public void PerformShipAbility(ShipActiveAbilityTypes abilityType)
        {
            abilityStartTime = Time.time;
            switch(abilityType)
            {
                case ShipActiveAbilityTypes.FullSpeedStraightAbility:
                    PerformShipAbilitiesEffects(fullSpeedStraightEffects);
                    break;
                case ShipActiveAbilityTypes.RightStickAbility:
                    PerformShipAbilitiesEffects(rightStickEffects);
                    break;
                case ShipActiveAbilityTypes.LeftStickAbility:
                    PerformShipAbilitiesEffects(leftStickEffects);
                    break;
                case ShipActiveAbilityTypes.FlipAbility:
                    PerformShipAbilitiesEffects(flipEffects);
                    break;
            }
        }

        public void StopShipAbility(ShipActiveAbilityTypes abilityType)
        {
            if (StatsManager.Instance != null)
                StatsManager.Instance.AbilityActivated(Team, player.PlayerName, abilityType, Time.time-abilityStartTime);

            switch (abilityType)
            {
                case ShipActiveAbilityTypes.FullSpeedStraightAbility:
                    StopShipAbilitiesEffects(fullSpeedStraightEffects);
                    break;
                case ShipActiveAbilityTypes.RightStickAbility:
                    StopShipAbilitiesEffects(rightStickEffects);
                    break;
                case ShipActiveAbilityTypes.LeftStickAbility:
                    StopShipAbilitiesEffects(leftStickEffects);
                    break;
                case ShipActiveAbilityTypes.FlipAbility:
                    StopShipAbilitiesEffects(flipEffects);
                    break;
            }
        }

        void PerformShipAbilitiesEffects(List<ActiveAbilities> shipAbilities)
        {
            foreach (ActiveAbilities effect in shipAbilities)
            {
                switch (effect)
                {
                    case ActiveAbilities.Drift:
                        // TODO: this should call inputController.StartDrift
                        shipData.Drifting = true;
                        break;
                    case ActiveAbilities.Boost:
                        shipData.Boosting = true;
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
                        inputController.EndDrift();
                        break;
                    case ActiveAbilities.Boost:
                        shipData.Boosting = false;
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

        public void SetAOEExplosionMaterial(Material material)
        {
            AOEExplosionMaterial = material;
        }

        public void FlipShipUpsideDown()
        {
            ShipRotationOverride.transform.localRotation = Quaternion.Euler(0, 0, 180);
        }
        public void FlipShipRightsideUp()
        {
            ShipRotationOverride.transform.localRotation = Quaternion.Euler(0, 0, 0);
        }

        void ApplySpeedModifiers()
        {
            float accumulatedSpeedModification = 1;
            for (int i = SpeedModifiers.Count - 1; i >= 0; i--)
            {
                var modifier = SpeedModifiers[i];

                modifier.elapsedTime += Time.deltaTime;

                if (modifier.elapsedTime >= modifier.duration)
                    SpeedModifiers.RemoveAt(i);
                else
                    accumulatedSpeedModification *= Mathf.Lerp(modifier.initialValue, 1f, modifier.elapsedTime / modifier.duration);
            }

            accumulatedSpeedModification = Mathf.Min(accumulatedSpeedModification, speedModifierMax);
            shipData.SpeedMultiplier = accumulatedSpeedModification;
        }

        void ApplyShipMaterial()
        {
            if (ShipMaterial == null)
                return;

            foreach (var shipGeometry in shipGeometries)
                shipGeometry.GetComponent<MeshRenderer>().material = ShipMaterial;
        }
    }
}