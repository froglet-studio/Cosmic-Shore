using System.Collections.Generic;
using StarWriter.Core.Input;
using UnityEngine;


namespace StarWriter.Core
{
    // TODO: pull into separate file
    public enum ShipActiveAbilityTypes
    {
        FullSpeedStraightAbility,
        RightStickAbility,
        LeftStickAbility,
        FlipAbility,
    }

    [RequireComponent(typeof(ResourceSystem))]
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

        bool invulnerable;
        [SerializeField] ShipTypes SecondMode = ShipTypes.Shark;

        Teams team;
        ShipData shipData;
        InputController inputController;
        Material ShipMaterial;
        Material AOEExplosionMaterial;
        ResourceSystem resourceSystem;
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
            resourceSystem = GetComponent<ResourceSystem>();
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
                    case PassiveAbilities.BlockThief: //TODO remove
                        //skimmer.thief = true;
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
                        // TODO: ship mode toggling

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
                    case CrystalImpactEffects.FillFuel:
                        resourceSystem.ChangeChargeAmount(player.PlayerUUID, crystalProperties.fuelAmount);
                        break;
                    case CrystalImpactEffects.Boost:
                        SpeedModifiers.Add(new SpeedModifier(crystalProperties.speedBuffAmount, 4 * speedModifierDuration, 0));
                        break;
                    case CrystalImpactEffects.DrainFuel:
                        resourceSystem.ChangeChargeAmount(player.PlayerUUID, -resourceSystem.CurrentCharge);
                        break;
                    case CrystalImpactEffects.Score:
                        //if (StatsManager.Instance != null)
                        //    StatsManager.Instance.UpdateScore(player.PlayerUUID, crystalProperties.scoreAmount);
                        // TODO: Remove this impact effect, or re-introduce scoring in a separate game mode
                        break;
                    case CrystalImpactEffects.ResetAggression:
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
                        resourceSystem.ChangeChargeAmount(player.PlayerUUID, -resourceSystem.CurrentCharge / 2f);
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
                        resourceSystem.ChangeChargeAmount(player.PlayerUUID, blockFuelChange);
                        break;
                }
            }
        }

        float abilityStartTime;

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
                        shipData.Drifting = true;
                        //cameraManager.SetFarCameraDistance(closeCamDistance); //use the far cam as the drift cam by setting it to the close cam distance first
                        //cameraManager.SetFarCameraActive();
                        //shipData.velocityDirection
                        //cameraManager.DriftCam(shipData.velocityDirection, transform.forward);
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
                        //inputController.drifting = false;
                        //cameraManager.SetCloseCameraActive();
                        //cameraManager.driftDistance = 1;
                        //cameraManager.tempOffset = Vector3.zero;
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
        void ApplyShipMaterial()
        {
            if (ShipMaterial == null)
                return;

            foreach (var shipGeometry in shipGeometries)
                shipGeometry.GetComponent<MeshRenderer>().material = ShipMaterial;
        }
    }
}