using System;
using System.Collections;
using System.Collections.Generic;
using StarWriter.Core.Input;
using UnityEditor;
using UnityEngine;

namespace StarWriter.Core
{

    using System.Reflection;


    [CustomEditor(typeof(Ship))]
    public class ShipEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var shipScript = (Ship)target;

            SerializedProperty property = serializedObject.GetIterator();

            while (property.NextVisible(true))
            {
                FieldInfo fieldInfo = shipScript.GetType().GetField(property.name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

                if (fieldInfo != null)
                {
                    ShowIfAttribute showIfControlOverrideAttribute = (ShowIfAttribute)Attribute.GetCustomAttribute(
                        fieldInfo,
                        typeof(ShowIfAttribute)
                    );

                    if (showIfControlOverrideAttribute == null 
                        || shipScript.ControlOverrides.Contains(showIfControlOverrideAttribute.ControlOverride)
                        || shipScript.LevelEffects.Contains(showIfControlOverrideAttribute.LevelEffect)
                        || shipScript.crystalImpactEffects.Contains(showIfControlOverrideAttribute.CrystalImpactEffect)
                        || shipScript.fullSpeedStraightEffects.Contains(showIfControlOverrideAttribute.Action)
                        || shipScript.rightStickEffects.Contains(showIfControlOverrideAttribute.Action)
                        || shipScript.leftStickEffects.Contains(showIfControlOverrideAttribute.Action)
                        || shipScript.flipEffects.Contains(showIfControlOverrideAttribute.Action)
                        || shipScript.idleEffects.Contains(showIfControlOverrideAttribute.Action)
                        || shipScript.minimumSpeedStraightEffects.Contains(showIfControlOverrideAttribute.Action))
                    {
                        EditorGUILayout.PropertyField(property, true);
                    }
                }
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
    

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class ShowIfAttribute : PropertyAttribute
    {
        public ShipControlOverrides ControlOverride { get; private set; }
        public ShipActions Action { get; private set; }
        public ShipLevelEffects LevelEffect { get; private set; }
        public CrystalImpactEffects CrystalImpactEffect { get; private set; }

        public ShowIfAttribute(ShipControlOverrides controlOverride) { ControlOverride = controlOverride; }
        public ShowIfAttribute(ShipActions action) { Action = action; }
        public ShowIfAttribute(ShipLevelEffects levelEffect) { LevelEffect = levelEffect; }
        public ShowIfAttribute(CrystalImpactEffects crystalImpactEffect) { CrystalImpactEffect = crystalImpactEffect; }
    }

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
        [ShowIf(ShipActions.DropFakeCrystal)] [SerializeField] FakeCrystal fakeCrystal;

        [Header("Environment Interactions")]
        public List<CrystalImpactEffects> crystalImpactEffects;
        [ShowIf(CrystalImpactEffects.AreaOfEffectExplosion)] [SerializeField] float minExplosionScale = 50;
        [ShowIf(CrystalImpactEffects.AreaOfEffectExplosion)] [SerializeField] float maxExplosionScale = 400;

        List<TrailBlockImpactEffects> trailBlockImpactEffects;
        [SerializeField] float blockChargeChange;

        [Header("Configuration")]
        public float boostMultiplier = 4f; //TODO: Move to ShipController
        public float boostFuelAmount = -.01f; 
        
        [Header("Dynamically Assignable Controls")]
        public List<ShipActions> fullSpeedStraightEffects;
        public List<ShipActions> rightStickEffects;
        public List<ShipActions> leftStickEffects;
        public List<ShipActions> flipEffects;
        public List<ShipActions> idleEffects;
        public List<ShipActions> minimumSpeedStraightEffects;

        [ShowIf(ShipActions.ZoomOut)] [SerializeField] float cameraGrowthRate = 1;
        [ShowIf(ShipActions.ZoomOut)] [SerializeField] float cameraShrinkRate = 1;
        [ShowIf(ShipActions.GrowTrail)] [SerializeField] float minTrailYScale = 15;
        [ShowIf(ShipActions.GrowTrail)] [SerializeField] float maxTrailYScale = 100;

        [ShowIf(ShipActions.GrowTrail)] [SerializeField] float trailGrowthRate = 1;
        [ShowIf(ShipActions.GrowTrail)] [SerializeField] float trailShrinkRate = 1;
        [ShowIf(ShipActions.GrowSkimmer)] [SerializeField] float skimmerGrowthRate = 1;
        [ShowIf(ShipActions.GrowSkimmer)] [SerializeField] float skimmerShrinkRate = 1;

        [Header("Passive Effects")]
        public List<ShipLevelEffects> LevelEffects;
        
        [ShowIf(ShipLevelEffects.ScaleGap)] [SerializeField] float minGap = 0;
        [ShowIf(ShipLevelEffects.ScaleGap)] [SerializeField] float maxGap = 0;
        [ShowIf(ShipLevelEffects.ScaleSkimmers)] [SerializeField] float minFarFieldSkimmerScale = 100;
        [ShowIf(ShipLevelEffects.ScaleSkimmers)] [SerializeField] float maxFarFieldSkimmerScale = 200;
        [ShowIf(ShipLevelEffects.ScaleSkimmers)] [SerializeField] float minNearFieldSkimmerScale = 15;
        [ShowIf(ShipLevelEffects.ScaleSkimmers)] [SerializeField] float maxNearFieldSkimmerScale = 100;

        // TODO: move these into GunShipController
        [ShowIf(ShipLevelEffects.ScaleProjectiles)] [SerializeField] float minProjectileScale = 1;
        [ShowIf(ShipLevelEffects.ScaleProjectiles)] [SerializeField] float maxProjectileScale = 10;
        [ShowIf(ShipLevelEffects.ScaleProjectileBlocks)] [SerializeField] Vector3 minProjectileBlockScale = new Vector3(1.5f, 1.5f, 3f);
        [ShowIf(ShipLevelEffects.ScaleProjectileBlocks)] [SerializeField] Vector3 maxProjectileBlockScale = new Vector3(1.5f, 1.5f, 30f);

        public List<ShipControlOverrides> ControlOverrides;
        [ShowIf(ShipControlOverrides.CloseCam)] public float closeCamDistance;
        [ShowIf(ShipControlOverrides.FarCam)] [SerializeField] float farCamDistance;

        public Dictionary<InputEvents, List<ShipActions>> ShipControlActions;

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
        bool skimmerGrowing;
        bool trailGrowing;

        Coroutine returnSkimmerToNeutralCoroutine;
        Coroutine growSkimmerCoroutine;
        Coroutine returnTrailToNeutralCoroutine;
        Coroutine growTrailCoroutine;

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
            ApplyShipControlOverrides(ControlOverrides);

            foreach (var shipGeometry in shipGeometries)
                shipGeometry.AddComponent<ShipGeometry>().Ship = this;

            ShipControlActions = new Dictionary<InputEvents, List<ShipActions>> { 
                { InputEvents.FullSpeedStraightAction, fullSpeedStraightEffects },
                { InputEvents.MinimumSpeedStraightAction, minimumSpeedStraightEffects },
                { InputEvents.LeftStickAction, leftStickEffects },
                { InputEvents.RightStickAction, rightStickEffects },
                { InputEvents.FlipAction, flipEffects },
                { InputEvents.IdleAction, idleEffects },
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
                    case ShipControlOverrides.CloseCam:
                        cameraManager.CloseCamDistance = closeCamDistance;
                        cameraManager.SetCloseCameraDistance(closeCamDistance);
                        break;
                    case ShipControlOverrides.FarCam:
                        cameraManager.FarCamDistance = farCamDistance;
                        cameraManager.SetFarCameraDistance(farCamDistance);
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
                        ResourceSystem.ChangeBoostAmount(crystalProperties.fuelAmount);
                        break;
                    case CrystalImpactEffects.Boost:
                        SpeedModifiers.Add(new ShipSpeedModifier(crystalProperties.speedBuffAmount, 4 * speedModifierDuration, 0));
                        break;
                    case CrystalImpactEffects.DrainAmmo:
                        ResourceSystem.ChangeAmmoAmount(-ResourceSystem.CurrentAmmo);
                        break;
                    case CrystalImpactEffects.GainOneThirdMaxAmmo:
                        ResourceSystem.ChangeAmmoAmount(ResourceSystem.MaxAmmo/3f);
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
                        ResourceSystem.ChangeAmmoAmount(-ResourceSystem.CurrentAmmo / 2f);
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
                        ResourceSystem.ChangeBoostAmount(blockChargeChange);
                        break;
                    case TrailBlockImpactEffects.DecrementLevel:
                        DecrementLevel();
                        break;
                    case TrailBlockImpactEffects.Attach:
                        Attach(trailBlockProperties.trailBlock);
                        shipData.GunsActive = true;
                        break;
                    case TrailBlockImpactEffects.ChangeAmmo:
                        ResourceSystem.ChangeAmmoAmount(blockChargeChange);
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
                        shipData.GunsActive = false;
                        break;
                    case ShipActions.PauseGuns:
                        shipData.GunsActive = false;
                        break;
                    case ShipActions.FireBigGun:
                        if (shipController is GunShipController) ((GunShipController)shipController).BigFire();
                        break;
                    case ShipActions.LayBulletTrail:
                        shipData.LayingBulletTrail = true;
                        break;
                    case ShipActions.DropFakeCrystal:
                        if (ResourceSystem.CurrentAmmo > ResourceSystem.MaxAmmo / 3f)
                        {
                            var fake = Instantiate(fakeCrystal).GetComponent<FakeCrystal>();
                            fake.Team = team;
                            fake.SetPositionAndRotation(transform.position + (Quaternion.Euler(0, 0, UnityEngine.Random.Range(0, 360)) * transform.up * UnityEngine.Random.Range(40, 60)) + (transform.forward * 40), transform.rotation);
                            ResourceSystem.ChangeAmmoAmount(-ResourceSystem.MaxAmmo / 3f);
                        }
                        break;
                    case ShipActions.StartGuns:
                        shipData.GunsActive = true;
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
                        break;
                    case ShipActions.LayBulletTrail:
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

        public void Rotate(Quaternion rotation)
        {
            shipController.Rotate(rotation);
        }

        void ApplyShipMaterial()
        {
            if (ShipMaterial == null)
                return;

            foreach (var shipGeometry in shipGeometries)
                shipGeometry.GetComponent<MeshRenderer>().material = ShipMaterial;
        }

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
            foreach (ShipLevelEffects effect in LevelEffects)
            {
                switch (effect)
                {
                    case ShipLevelEffects.ScaleSkimmers:
                        ScaleSkimmersWithLevel();
                        break;
                    case ShipLevelEffects.ScaleGap:
                        ScaleGapWithLevel();
                        break;
                    case ShipLevelEffects.ScaleProjectiles:
                        ScaleProjectilesWithLevel();
                        break;
                    case ShipLevelEffects.ScaleProjectileBlocks:
                        ScaleProjectileBlocksWithLevel();
                        break;
                }
            }
        }

        void IncrementLevel()
        {
            ResourceSystem.ChangeLevel(ChargeDisplay.OneFuelUnit);
            UpdateLevel();
        }

        void DecrementLevel()
        {
            ResourceSystem.ChangeLevel(-ChargeDisplay.OneFuelUnit);
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
            // TODO: 
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