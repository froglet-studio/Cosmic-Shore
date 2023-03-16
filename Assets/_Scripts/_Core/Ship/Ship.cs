using System.Collections;
using System.Collections.Generic;
using StarWriter.Core.Input;
using UnityEngine;
using UnityEngine.Serialization;

namespace StarWriter.Core
{
    [RequireComponent(typeof(ResourceSystem))]
    [RequireComponent(typeof(TrailSpawner))]
    public class Ship : MonoBehaviour
    {
        [Header("ship Meta")]
        [SerializeField] string Name;
        [SerializeField] public ShipTypes ShipType;

        [Header("ship Components")]
        [SerializeField] Skimmer nearFieldSkimmer;
        [SerializeField] GameObject OrientationHandle;
        [SerializeField] List<GameObject> shipGeometries;
        [HideInInspector] public TrailSpawner TrailSpawner;
        [SerializeField] GameObject head;
        ShipController shipController;

        [Header("optional ship Components")]
        [SerializeField] GameObject AOEPrefab;
        [SerializeField] Skimmer farFieldSkimmer;

        [Header("Environment Interactions")]
        [SerializeField] List<CrystalImpactEffects> crystalImpactEffects;
        [SerializeField] List<TrailBlockImpactEffects> trailBlockImpactEffects;

        [SerializeField] float minExplosionScale = 50;
        [SerializeField] float maxExplosionScale = 400;
        [SerializeField] float blockChargeChange;

        [Header("Configuration")]
        public float boostMultiplier = 4f;
        public float boostFuelAmount = -.01f;
        
        [Header("Dynamically Assignable Controls")]
        [SerializeField] List<ShipActions> fullSpeedStraightEffects;
        [SerializeField] List<ShipActions> rightStickEffects;
        [SerializeField] List<ShipActions> leftStickEffects;
        [SerializeField] List<ShipActions> flipEffects;
        [SerializeField] List<ShipActions> idleEffects;
        [SerializeField] List<ShipActions> minimumSpeedStraightEffects;

        [SerializeField] float cameraGrowthRate = 1;
        [SerializeField] float cameraShrinkRate = 1;
        [SerializeField] float minTrailYScale = 15;
        [SerializeField] float maxTrailYScale = 100;
        [SerializeField] float skimmerGrowthRate = 1;
        [SerializeField] float skimmerShrinkRate = 1;
        [SerializeField] float trailGrowthRate = 1;
        [SerializeField] float trailShrinkRate = 1;

        [Header("Passive Effects")]
        [SerializeField] List<LevelEffects> levelEffects;
   
        [SerializeField] float minFarFieldSkimmerScale = 100;
        [SerializeField] float maxFarFieldSkimmerScale = 200;
        [SerializeField] float minNearFieldSkimmerScale = 15;
        [SerializeField] float maxNearFieldSkimmerScale = 100;
        [SerializeField] float minGap = 0;
        [SerializeField] float maxGap = 0;
        [SerializeField] float minProjectileScale = 1;
        [SerializeField] float maxProjectileScale = 10;
        [SerializeField] Vector3 minProjectileBlockScale = new Vector3(1.5f, 1.5f, 3f);
        [SerializeField] Vector3 maxProjectileBlockScale = new Vector3(1.5f, 1.5f, 30f);

        [SerializeField] List<ShipControlOverrides> controlOverrides;
        [SerializeField] float closeCamDistance;
        [SerializeField] float farCamDistance;
        [SerializeField] float throttle = 50;
        [SerializeField] float BoostDecayGrowthRate = .03f;
        [SerializeField] float MaxBoostDecay = 10;
        [SerializeField] float rotationScaler = 130;
        [SerializeField] float rotationThrottleScaler;


        Dictionary<InputEvents, List<ShipActions>> ShipControlActions;

        bool invulnerable;
        Teams team;
        CameraManager cameraManager;
        Player player;
        ShipData shipData; // TODO: this should be a required component or just a series of properties on the ship
        [HideInInspector] public InputController inputController;
        Material ShipMaterial;
        Material AOEExplosionMaterial;
        [HideInInspector] public ResourceSystem ResourceSystem;
        readonly List<ShipSpeedModifier> SpeedModifiers = new List<ShipSpeedModifier>();
        float speedModifierDuration = 2f;
        float speedModifierMax = 6f;
        float abilityStartTime;
        float elapsedTime;
        bool skimmerGrowing;
        bool trailGrowing;

        public Teams Team 
        { 
            get => team; 
            set 
            { 
                team = value;
                if (nearFieldSkimmer != null) nearFieldSkimmer.team = value;
                if (farFieldSkimmer != null) farFieldSkimmer.team = value; 
            }
        }
        public Player Player 
        { 
            get => player;
            set
            {
                player = value;
                if (nearFieldSkimmer != null) nearFieldSkimmer.Player = value;
                if (farFieldSkimmer != null) farFieldSkimmer.Player = value;
            }
        }

        void Awake()
        {
            ResourceSystem = GetComponent<ResourceSystem>();
            shipController = GetComponent<ShipController>();
            TrailSpawner = GetComponent<TrailSpawner>();
            shipData = GetComponent<ShipData>();
        }

        void Start()
        {
            cameraManager = CameraManager.Instance;
            inputController = player.GetComponent<InputController>();
            ApplyShipControlOverrides(controlOverrides);

            foreach (var shipGeometry in shipGeometries)
                shipGeometry.AddComponent<ShipGeometry>().Ship = this;

            ShipControlActions = new Dictionary<InputEvents, List<ShipActions>> { 
                { InputEvents.FullSpeedStraightAction, fullSpeedStraightEffects },
                { InputEvents.FlipAction, flipEffects },
                { InputEvents.LeftStickAction, leftStickEffects },
                { InputEvents.RightStickAction, rightStickEffects },
                { InputEvents.IdleAction, idleEffects },
                { InputEvents.MinimumSpeedStraightAction, minimumSpeedStraightEffects }
            };
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
                        shipController.rotationScaler = rotationScaler;
                        break;
                    case ShipControlOverrides.BlockScout:
                        break;
                    case ShipControlOverrides.CloseCam:
                        cameraManager.CloseCamDistance = closeCamDistance;
                        cameraManager.SetCloseCameraDistance(closeCamDistance);
                        break;
                    case ShipControlOverrides.FarCam:
                        cameraManager.FarCamDistance = farCamDistance;
                        cameraManager.SetFarCameraDistance(farCamDistance);
                        break;
                    case ShipControlOverrides.SecondMode:
                        // TODO: ship mode toggling
                        break;
                    case ShipControlOverrides.SpeedBasedTurning:
                        shipController.rotationThrottleScaler = rotationThrottleScaler;
                        break;
                    case ShipControlOverrides.Throttle:
                        shipController.ThrottleScaler = throttle;
                        break;
                    case ShipControlOverrides.BoostDecayGrowthRate:
                        shipController.BoostDecayGrowthRate = BoostDecayGrowthRate;
                        break;
                    case ShipControlOverrides.MaxBoostDecay:
                        shipController.MaxBoostDecay = MaxBoostDecay;
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
                        AOEExplosion.SetPositionAndRotation(transform.position, transform.rotation);
                        AOEExplosion.MaxScale =  Mathf.Max(minExplosionScale, ResourceSystem.CurrentAmmo * maxExplosionScale);

                        if (AOEExplosion is AOEBlockCreation aoeBlockcreation)
                            aoeBlockcreation.SetBlockMaterial(TrailSpawner.GetBlockMaterial());
                        if (AOEExplosion is AOEFlowerCreation aoeFlowerCreation)
                        {
                            StartCoroutine(CreateTunnelCoroutine(aoeFlowerCreation, 3));
                        }
                        break;
                    case CrystalImpactEffects.IncrementLevel:
                        IncrementLevel();
                        break;
                    case CrystalImpactEffects.FillCharge:
                        ResourceSystem.ChangeBoostAmount(player.PlayerUUID, crystalProperties.fuelAmount);
                        break;
                    case CrystalImpactEffects.Boost:
                        SpeedModifiers.Add(new ShipSpeedModifier(crystalProperties.speedBuffAmount, 4 * speedModifierDuration, 0));
                        break;
                    case CrystalImpactEffects.DrainAmmo:
                        ResourceSystem.ChangeAmmoAmount(player.PlayerUUID, -ResourceSystem.CurrentAmmo);
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
                    case TrailBlockImpactEffects.DrainHalfAmmo:
                        ResourceSystem.ChangeAmmoAmount(player.PlayerUUID, -ResourceSystem.CurrentAmmo / 2f);
                        break;
                    case TrailBlockImpactEffects.DebuffSpeed:
                        ModifySpeed(trailBlockProperties.speedDebuffAmount, speedModifierDuration);
                        break;
                    case TrailBlockImpactEffects.DeactivateTrailBlock:
                        break;
                    case TrailBlockImpactEffects.ActivateTrailBlock:
                        break;
                    case TrailBlockImpactEffects.OnlyBuffSpeed:
                        if (trailBlockProperties.speedDebuffAmount > 1) SpeedModifiers.Add(new ShipSpeedModifier(trailBlockProperties.speedDebuffAmount, speedModifierDuration, 0));
                        break;
                    case TrailBlockImpactEffects.ChangeBoost:
                        ResourceSystem.ChangeBoostAmount(player.PlayerUUID, blockChargeChange);
                        break;
                    case TrailBlockImpactEffects.DecrementLevel:
                        DecrementLevel();
                        break;
                    case TrailBlockImpactEffects.Attach:
                        Attach(trailBlockProperties.trailBlock);
                        break;
                    case TrailBlockImpactEffects.ChangeAmmo:
                        ResourceSystem.ChangeAmmoAmount(player.PlayerUUID, blockChargeChange);
                        break;
                }
            }
        }

        public void PerformShipControllerActions(InputEvents controlType)
        {
            abilityStartTime = Time.time;
            var shipActions = ShipControlActions[controlType];

            foreach (ShipActions action in shipActions)
            {
                switch (action)
                {
                    case ShipActions.Drift:
                        // TODO: this should call inputController.StartDrift
                        shipData.Drifting = true;
                        break;
                    case ShipActions.Boost:
                        shipData.Boosting = true;
                        break;
                    case ShipActions.Invulnerability:
                        if (!invulnerable)
                        {
                            invulnerable = true;
                            trailBlockImpactEffects.Remove(TrailBlockImpactEffects.DebuffSpeed);
                            trailBlockImpactEffects.Add(TrailBlockImpactEffects.OnlyBuffSpeed);
                        } 
                        break;
                    case ShipActions.ToggleCamera:
                        CameraManager.Instance.ToggleCloseOrFarCamOnPhoneFlip(true);
                        TrailSpawner.ToggleBlockWaitTime(true);
                        break;
                    case ShipActions.ToggleMode:
                        // TODO
                        break;
                    case ShipActions.ToggleGyro:
                        inputController.OnToggleGyro(true);
                        break;
                    case ShipActions.ZoomOut:
                        cameraManager.ZoomOut(cameraGrowthRate);
                        break;
                    case ShipActions.GrowSkimmer:
                        GrowSkimmer(skimmerGrowthRate);
                        break;
                    case ShipActions.ChargeBoost: 
                        shipData.BoostCharging = true;
                        break;
                    case ShipActions.GrowTrail:
                        GrowTrail(trailGrowthRate);
                        break;
                    case ShipActions.Detach:
                        Detach();
                        break;
                    case ShipActions.PauseGuns:
                        shipData.GunsActive = false;
                        break;
                    case ShipActions.FireBigGun:
                        if (shipController is GunShipController) ((GunShipController)shipController).BigFire();
                        break;
                    case ShipActions.layBulletTrail:
                        shipData.LayingBulletTrail = true;
                        break;

                }
            }
        }

        public void StopShipControllerActions(InputEvents controlType)
        {
            if (StatsManager.Instance != null)
                StatsManager.Instance.AbilityActivated(Team, player.PlayerName, controlType, Time.time-abilityStartTime);

            var shipActions = ShipControlActions[controlType];
            foreach (ShipActions action in shipActions)
            {
                switch (action)
                {
                    case ShipActions.Drift:
                        shipData.Drifting = false;
                        GetComponent<TrailSpawner>().SetDotProduct(1);
                        break;
                    case ShipActions.Boost:
                        shipData.Boosting = false;
                        break;
                    case ShipActions.Invulnerability:
                        invulnerable = false;
                        trailBlockImpactEffects.Add(TrailBlockImpactEffects.DebuffSpeed);
                        trailBlockImpactEffects.Remove(TrailBlockImpactEffects.OnlyBuffSpeed);
                        break;
                    case ShipActions.ToggleCamera:
                        CameraManager.Instance.ToggleCloseOrFarCamOnPhoneFlip(false);
                        TrailSpawner.ToggleBlockWaitTime(false);
                        break;
                    case ShipActions.ToggleMode:
                        // TODO
                        break;
                    case ShipActions.ToggleGyro:
                        inputController.OnToggleGyro(false);
                        break;
                    case ShipActions.ZoomOut:
                        cameraManager.ResetToNeutral(cameraShrinkRate);
                        break;
                    case ShipActions.GrowSkimmer:
                        ResetSkimmerToNeutral(skimmerShrinkRate);
                        break;
                    case ShipActions.ChargeBoost:
                        shipData.BoostCharging = false;
                        shipController.StartChargedBoost();
                        break;
                    case ShipActions.GrowTrail:
                        ResetTrailToNeutral(trailShrinkRate);
                        break;
                    case ShipActions.PauseGuns:
                        shipData.GunsActive = true;
                        break;
                    case ShipActions.layBulletTrail:
                        shipData.LayingBulletTrail = false;
                        break;
                }
            }
        }

        public void ToggleCollision(bool enabled)
        {
            foreach (var collider in GetComponentsInChildren<Collider>(true))
                collider.enabled = enabled;
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
            OrientationHandle.transform.localRotation = Quaternion.Euler(0, 0, 180);
        }
        public void FlipShipRightsideUp()
        {
            OrientationHandle.transform.localRotation = Quaternion.Euler(0, 0, 0);
        }

        public void Teleport(Transform _transform)
        {
            transform.SetPositionAndRotation(_transform.position, _transform.rotation);
        }

        // TODO: need to be able to disable ship abilities as well for minigames
        public void DisableSkimmer()
        {
            nearFieldSkimmer?.gameObject.SetActive(false);
            farFieldSkimmer?.gameObject.SetActive(false);
        }

        //
        // Speed Modification
        //

        public void ModifySpeed(float amount, float duration)
        {
            SpeedModifiers.Add(new ShipSpeedModifier(amount, duration, 0));
        }

        void ApplySpeedModifiers()
        {
            float accumulatedSpeedModification = 1;
            for (int i = SpeedModifiers.Count - 1; i >= 0; i--)
            {
                var modifier = SpeedModifiers[i];
                modifier.elapsedTime += Time.deltaTime;
                SpeedModifiers[i] = modifier;

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

        


        Coroutine returnSkimmerToNeutralCoroutine;
        Coroutine growSkimmerCoroutine;
        Coroutine returnTrailToNeutralCoroutine;
        Coroutine growTrailCoroutine;
       

        //
        // grow skimmer
        //


        void GrowSkimmer(float growthRate)
        {
            if (returnSkimmerToNeutralCoroutine != null)
            {
                StopCoroutine(returnSkimmerToNeutralCoroutine);
                returnSkimmerToNeutralCoroutine = null;
            }
            skimmerGrowing = true;
            growSkimmerCoroutine = StartCoroutine(GrowSkimmerCoroutine(growthRate));
        }

        IEnumerator GrowSkimmerCoroutine(float growthRate)
        {
            while (skimmerGrowing && nearFieldSkimmer.transform.localScale.z < maxNearFieldSkimmerScale)
            {
                nearFieldSkimmer.transform.localScale += Time.deltaTime * growthRate * Vector3.one;
                yield return null;
            }
        }

        public void ResetSkimmerToNeutral(float shrinkRate)
        {
            if (growSkimmerCoroutine != null)
            {
                StopCoroutine(growSkimmerCoroutine);
                growSkimmerCoroutine = null;
            }
            skimmerGrowing = false;
            returnSkimmerToNeutralCoroutine = StartCoroutine(ReturnSkimmerToNeutralCoroutine(shrinkRate));
        }

        IEnumerator ReturnSkimmerToNeutralCoroutine(float shrinkRate)
        {
            while (nearFieldSkimmer.transform.localScale.z > minNearFieldSkimmerScale)
            {
                nearFieldSkimmer.transform.localScale -= Time.deltaTime * shrinkRate * Vector3.one;
                yield return null;
            }
            nearFieldSkimmer.transform.localScale = minNearFieldSkimmerScale * Vector3.one;
        }

        //
        // Grow trail
        //

        void GrowTrail(float growthRate)
        {
            if (returnTrailToNeutralCoroutine != null)
            {
                StopCoroutine(returnTrailToNeutralCoroutine);
                returnTrailToNeutralCoroutine = null;
            }
            trailGrowing = true;
            growTrailCoroutine = StartCoroutine(GrowTrailCoroutine(growthRate));
        }

        IEnumerator GrowTrailCoroutine(float growthRate)
        {
            while (trailGrowing && TrailSpawner.YScaler < maxTrailYScale)
            {
                TrailSpawner.YScaler += Time.deltaTime * growthRate;
                TrailSpawner.XScaler += Time.deltaTime * growthRate;
                yield return null;
            }
        }

        public void ResetTrailToNeutral(float shrinkRate)
        {
            if (growTrailCoroutine != null)
            {
                StopCoroutine(growTrailCoroutine);
                growTrailCoroutine = null;
            }
            trailGrowing = false;
            returnTrailToNeutralCoroutine = StartCoroutine(ReturnTrailToNeutralCoroutine(shrinkRate));
        }

        IEnumerator ReturnTrailToNeutralCoroutine(float shrinkRate)
        {
            while (TrailSpawner.YScaler  > minTrailYScale)
            {
                TrailSpawner.YScaler -= Time.deltaTime * shrinkRate;
                TrailSpawner.XScaler -= Time.deltaTime * shrinkRate;
                yield return null;
            }
            nearFieldSkimmer.transform.localScale = minNearFieldSkimmerScale * Vector3.one;
        }

        //
        // Attach and Detach
        //

        void Attach(TrailBlock trailBlock) 
        {
            if (trailBlock.Trail != null)
            {
                shipData.Attached = true;
                shipData.AttachedTrailBlock = trailBlock;
                IncrementLevel();
            }
        }

        void Detach()
        {
            if (shipData.Attached)
            {
                shipData.Attached = false;
                shipData.AttachedTrailBlock = null;
                StartCoroutine(TemporaryIntangibilityCoroutine(3));
                DecrementLevel();
            }
        }

        //
        // level up and down
        //


        void UpdateLevel()
        {
            foreach (LevelEffects effect in levelEffects)
            {
                switch (effect)
                {
                    case LevelEffects.ScaleSkimmers:
                        ScaleSkimmersWithLevel();
                        break;
                    case LevelEffects.ScaleGap:
                        ScaleGapWithLevel();
                        break;
                    case LevelEffects.ScaleProjectiles:
                        ScaleProjectilesWithLevel();
                        break;
                    case LevelEffects.ScaleProjectileBlocks:
                        ScaleProjectileBlocksWithLevel();
                        break;
                }
            }
        }

        void IncrementLevel()
        {
            ResourceSystem.ChangeLevel(player.PlayerUUID, ChargeDisplay.OneFuelUnit);
            UpdateLevel();
        }

        void DecrementLevel()
        {
            ResourceSystem.ChangeLevel(player.PlayerUUID, -ChargeDisplay.OneFuelUnit);
            UpdateLevel();
        }

        void ScaleSkimmersWithLevel()
        {
            if (nearFieldSkimmer != null)
                nearFieldSkimmer.transform.localScale = Vector3.one * (minNearFieldSkimmerScale + ((ResourceSystem.CurrentLevel / ResourceSystem.MaxLevel) * (maxNearFieldSkimmerScale - minNearFieldSkimmerScale)));
            if (farFieldSkimmer != null)
                farFieldSkimmer.transform.localScale = Vector3.one * (maxFarFieldSkimmerScale - ((ResourceSystem.CurrentLevel / ResourceSystem.MaxLevel) * (maxFarFieldSkimmerScale - minFarFieldSkimmerScale)));
        }
        void ScaleGapWithLevel()
        {
            TrailSpawner.gap = maxGap - ((ResourceSystem.CurrentLevel / ResourceSystem.MaxLevel) * (maxGap - minGap));
        }

        void ScaleProjectilesWithLevel()
        {
            if (shipController is GunShipController controller)
                controller.ProjectileScale = minProjectileScale + ((ResourceSystem.CurrentLevel / ResourceSystem.MaxLevel) * (maxProjectileScale - minProjectileScale));
            else
                Debug.LogWarning("Trying to scale projectile of ShipController that is not a GunShipController");
        }

        void ScaleProjectileBlocksWithLevel()
        {
            if (shipController is GunShipController controller)
                controller.BlockScale = minProjectileBlockScale + ((ResourceSystem.CurrentLevel / ResourceSystem.MaxLevel) * (maxProjectileBlockScale - minProjectileBlockScale));
            else
                Debug.LogWarning("Trying to scale projectile block of ShipController that is not a GunShipController");
        }

        IEnumerator CreateTunnelCoroutine(AOEFlowerCreation aoeFlowerCreation, float amount)
        {
            var count = 0f;
            int currentPosition = TrailSpawner.TrailLength - 1;
            while (count < amount)
            { 
                if (currentPosition < TrailSpawner.TrailLength)
                {
                    count++;
                    currentPosition++;
                    aoeFlowerCreation.SetBlockDimensions(TrailSpawner.InnerDimensions);
                    aoeFlowerCreation.SeedBlocks(TrailSpawner.GetLastTwoBlocks());
                }
                yield return null;
            }
        }

        IEnumerator TemporaryIntangibilityCoroutine(float duration)
        {
            foreach (var geometry in shipGeometries)
                geometry.GetComponent<Collider>().enabled = false;

            yield return new WaitForSeconds(duration);

            foreach (var geometry in shipGeometries)
                geometry.GetComponent<Collider>().enabled = true;
        }
    }
}