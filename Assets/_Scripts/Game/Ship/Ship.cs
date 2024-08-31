using CosmicShore.Game.AI;
using CosmicShore.Game.IO;
using CosmicShore.Game.Projectiles;
using CosmicShore.Models;
using CosmicShore.Models.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CosmicShore.Core
{
    [Serializable]
    public struct InputEventShipActionMapping
    {
        public InputEvents InputEvent;
        public List<ShipAction> ShipActions;
    }

    [Serializable]
    public struct ResourceEventShipActionMapping
    {
        public ResourceEvents ResourceEvent;
        public List<ShipAction> ClassActions;
    }

    [RequireComponent(typeof(ResourceSystem))]
    [RequireComponent(typeof(TrailSpawner))]
    [RequireComponent(typeof(ShipStatus))]
    public class Ship : MonoBehaviour
    {
        [SerializeField] List<ImpactProperties> impactProperties;
        [HideInInspector] public CameraManager cameraManager;
        [HideInInspector] public InputController InputController;
        [HideInInspector] public ResourceSystem ResourceSystem;

        [Header("Ship Meta")]
        [SerializeField] string Name;
        [SerializeField] public ShipTypes ShipType;

        [Header("Ship Components")]
        [HideInInspector] public TrailSpawner TrailSpawner;
        [HideInInspector] public ShipTransformer ShipTransformer;
        [HideInInspector] public AIPilot AutoPilot;
        [HideInInspector] public ShipStatus ShipStatus;
        [SerializeField] Skimmer nearFieldSkimmer;
        [SerializeField] GameObject OrientationHandle;
        [SerializeField] public List<GameObject> shipGeometries;
        [SerializeField] GameObject shipHUD;

        [Header("Optional Ship Components")]
        [SerializeField] Silhouette Silhouette;
        [SerializeField] GameObject AOEPrefab;
        [SerializeField] Skimmer farFieldSkimmer;
        [SerializeField] public ShipCameraCustomizer ShipCameraCustomizer;
        [SerializeField] public Transform FollowTarget;

        [Header("Environment Interactions")]
        [SerializeField] public List<CrystalImpactEffects> crystalImpactEffects;
        [ShowIf(CrystalImpactEffects.AreaOfEffectExplosion)] [SerializeField] float minExplosionScale = 50; // TODO: depricate "ShowIf" once we adopt modularity
        [ShowIf(CrystalImpactEffects.AreaOfEffectExplosion)] [SerializeField] float maxExplosionScale = 400;

        [SerializeField] List<TrailBlockImpactEffects> trailBlockImpactEffects;
        [SerializeField] float blockChargeChange;

        [Header("Configuration")]
        [SerializeField] public float boostMultiplier = 4f; // TODO: Move to ShipController
        [SerializeField] public float boostFuelAmount = -.01f;
        [SerializeField] bool bottomEdgeButtons = false;
        [SerializeField] public float Inertia = 70f;

        [SerializeField] List<InputEventShipActionMapping> inputEventShipActions;
        Dictionary<InputEvents, List<ShipAction>> ShipControlActions = new();

        [SerializeField] List<ResourceEventShipActionMapping> resourceEventClassActions;
        Dictionary<ResourceEvents, List<ShipAction>> ClassResourceActions = new();

        [Header("Passive Effects")]
        [SerializeField] float closeCamDistance;
        [SerializeField] float farCamDistance;
        
        Dictionary<InputEvents, float> inputAbilityStartTimes = new();
        Dictionary<ResourceEvents, float> resourceAbilityStartTimes = new();

        Material ShipMaterial;
        [HideInInspector] public Material AOEExplosionMaterial;
        [HideInInspector] public Material AOEConicExplosionMaterial;
        [HideInInspector] public Material SkimmerMaterial;
        float speedModifierDuration = 2f;

        Teams team;
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

        Player player;
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
            ShipTransformer = GetComponent<ShipTransformer>();
            TrailSpawner = GetComponent<TrailSpawner>();
            ShipStatus = GetComponent<ShipStatus>();
        }

        void Start()
        {
            cameraManager = CameraManager.Instance;
            InputController = player.GetComponent<InputController>();
            AutoPilot = GetComponent<AIPilot>();
            if (!FollowTarget) FollowTarget = transform;
            if (bottomEdgeButtons) Player.GameCanvas.MiniGameHUD.PositionButtonPanel(true);

            foreach (var shipGeometry in shipGeometries)
                shipGeometry.AddComponent<ShipGeometry>().Ship = this;

            foreach (var inputEventShipAction in inputEventShipActions)
                if (!ShipControlActions.ContainsKey(inputEventShipAction.InputEvent))
                    ShipControlActions.Add(inputEventShipAction.InputEvent, inputEventShipAction.ShipActions);
                else
                    ShipControlActions[inputEventShipAction.InputEvent].AddRange(inputEventShipAction.ShipActions);

            foreach (var key in ShipControlActions.Keys)
                foreach (var shipAction in ShipControlActions[key])
                    shipAction.Ship = this;
            
            foreach (var resourceEventClassAction in resourceEventClassActions)
                if (!ClassResourceActions.ContainsKey(resourceEventClassAction.ResourceEvent))
                    ClassResourceActions.Add(resourceEventClassAction.ResourceEvent, resourceEventClassAction.ClassActions);
                else
                    ClassResourceActions[resourceEventClassAction.ResourceEvent].AddRange(resourceEventClassAction.ClassActions);

            foreach (var key in ClassResourceActions.Keys)
                foreach (var classAction in ClassResourceActions[key])
                    classAction.Ship = this;

            if (!AutoPilot.AutoPilotEnabled)
            {
                if (shipHUD)
                {
                    shipHUD.SetActive(true);
                    foreach (var child in shipHUD.GetComponentsInChildren<Transform>(false))
                    {
                        child.SetParent(Player.GameCanvas.transform, false);
                        child.SetSiblingIndex(0);   // Don't draw on top of modal screens
                    }
                }
            }
        }

        [Serializable] public struct ElementStat
        {
            public string StatName;
            public Element Element;

            public ElementStat(string statname, Element element)
            {
                StatName = statname;
                Element = element;
            }
        }

        [SerializeField] List<ElementStat> ElementStats = new List<ElementStat>();
        public void BindElementalFloat(string statName, Element element)
        {
            Debug.Log($"Ship.NotifyShipStatBinding - statName:{statName}, element:{element}");
            if (!ElementStats.Where(x => x.StatName == statName).Any())
                ElementStats.Add(new ElementStat(statName, element));
            
            Debug.Log($"Ship.NotifyShipStatBinding - ElementStats.Count:{ElementStats.Count}");
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
                        if (!ShipStatus.AutoPilotEnabled) HapticController.PlayHaptic(HapticType.CrystalCollision);//.PlayCrystalImpactHaptics();
                        break;
                    case CrystalImpactEffects.AreaOfEffectExplosion:
                        var AOEExplosion = Instantiate(AOEPrefab).GetComponent<AOEExplosion>();
                        AOEExplosion.Ship = this;
                        AOEExplosion.SetPositionAndRotation(transform.position, transform.rotation);
                        AOEExplosion.MaxScale =  Mathf.Lerp(minExplosionScale, maxExplosionScale, ResourceSystem.CurrentAmmo);
                        break;
                    case CrystalImpactEffects.IncrementLevel:
                        ResourceSystem.IncrementLevel(crystalProperties.Element); // TODO: consider removing here and leaving this up to the crystals
                        break;
                    case CrystalImpactEffects.FillCharge:
                        ResourceSystem.ChangeBoostAmount(crystalProperties.fuelAmount);
                        break;
                    case CrystalImpactEffects.Boost:
                        ShipTransformer.ModifyThrottle(crystalProperties.speedBuffAmount, 4 * speedModifierDuration);
                        break;
                    case CrystalImpactEffects.DrainAmmo:
                        ResourceSystem.ChangeAmmoAmount(-ResourceSystem.CurrentAmmo);
                        break;
                    case CrystalImpactEffects.GainOneThirdMaxAmmo:
                        ResourceSystem.ChangeAmmoAmount(ResourceSystem.MaxAmmo/3f);
                        break;
                    case CrystalImpactEffects.GainFullAmmo:
                        ResourceSystem.ChangeAmmoAmount(ResourceSystem.MaxAmmo);
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
                        if (!ShipStatus.AutoPilotEnabled) HapticController.PlayHaptic(HapticType.BlockCollision);//.PlayBlockCollisionHaptics();
                        break;
                    case TrailBlockImpactEffects.DrainHalfAmmo:
                        ResourceSystem.ChangeAmmoAmount(-ResourceSystem.CurrentAmmo / 2f);
                        break;
                    case TrailBlockImpactEffects.DebuffSpeed:
                        ShipTransformer.ModifyThrottle(trailBlockProperties.speedDebuffAmount, speedModifierDuration);
                        break;
                    case TrailBlockImpactEffects.DeactivateTrailBlock:
                        break;
                    case TrailBlockImpactEffects.ActivateTrailBlock:
                        break;
                    case TrailBlockImpactEffects.OnlyBuffSpeed:
                        if (trailBlockProperties.speedDebuffAmount > 1) ShipTransformer.ModifyThrottle(trailBlockProperties.speedDebuffAmount, speedModifierDuration);
                        break;
                    case TrailBlockImpactEffects.ChangeBoost:
                        ResourceSystem.ChangeBoostAmount(blockChargeChange);
                        break;
                    case TrailBlockImpactEffects.Attach:
                        Attach(trailBlockProperties.trailBlock);
                        ShipStatus.GunsActive = true;
                        break;
                    case TrailBlockImpactEffects.ChangeAmmo:
                        ResourceSystem.ChangeAmmoAmount(blockChargeChange);
                        break;
                    case TrailBlockImpactEffects.Bounce:
                        var cross = Vector3.Cross(transform.forward, trailBlockProperties.trailBlock.transform.forward);
                        var normal = Quaternion.AngleAxis(90, cross) * trailBlockProperties.trailBlock.transform.forward;
                        var reflectForward = Vector3.Reflect(transform.forward, normal);
                        var reflectUp = Vector3.Reflect(transform.up, normal);
                        ShipTransformer.GentleSpinShip(reflectForward, reflectUp, 1);
                        ShipTransformer.ModifyVelocity((transform.position - trailBlockProperties.trailBlock.transform.position).normalized * 5 , Time.deltaTime * 15);
                        break;
                    case TrailBlockImpactEffects.Explode:
                        trailBlockProperties.trailBlock.Damage(ShipStatus.Course * ShipStatus.Speed * Inertia, Team, Player.PlayerName);
                        break;
                    case TrailBlockImpactEffects.FeelDanger:
                        if (trailBlockProperties.IsDangerous && trailBlockProperties.trailBlock.Team != team)
                        {
                            HapticController.PlayHaptic(HapticType.FakeCrystalCollision);
                            ShipTransformer.ModifyThrottle(trailBlockProperties.speedDebuffAmount, trailBlockProperties.volume / 10);                           
                        }
                        break;
                }
            }
        }

        public void PerformShipControllerActions(InputEvents controlType)
        {
            if (!inputAbilityStartTimes.ContainsKey(controlType))
                inputAbilityStartTimes.Add(controlType, Time.time);
            else
                inputAbilityStartTimes[controlType] = Time.time;

            if (ShipControlActions.ContainsKey(controlType))
            {
                var shipControlActions = ShipControlActions[controlType];
                foreach (var action in shipControlActions)
                    action.StartAction();
            }
        }

        public void StopShipControllerActions(InputEvents controlType)
        {
            if (StatsManager.Instance != null)
                StatsManager.Instance.AbilityActivated(Team, player.PlayerName, controlType, Time.time-inputAbilityStartTimes[controlType]);

            if (ShipControlActions.ContainsKey(controlType))
            {
                var shipControlActions = ShipControlActions[controlType];
                foreach (var action in shipControlActions)
                    action.StopAction();
            }
        }


        // this is used with buttons so "Find all references" will not return editor usage
        public void PerformButtonActions(int buttonNumber)
        {
            Debug.Log($"Ship.PerformButtonActions - buttonNumber:{buttonNumber}");
            if (buttonNumber == 1 && ShipControlActions.ContainsKey(InputEvents.Button1Action))
            {
                PerformShipControllerActions(InputEvents.Button1Action);
            }
            else if (buttonNumber == 2 && ShipControlActions.ContainsKey(InputEvents.Button2Action))
            {
                PerformShipControllerActions(InputEvents.Button2Action);
            }
            else if (buttonNumber == 3 && ShipControlActions.ContainsKey(InputEvents.Button3Action))
            {
                PerformShipControllerActions(InputEvents.Button3Action);
            }
        }

        // this is used with buttons so "Find all references" will not return editor usage
        public void StopButtonActions(int buttonNumber)
        {
            if (buttonNumber == 1 && ShipControlActions.ContainsKey(InputEvents.Button1Action))
            {
                StopShipControllerActions(InputEvents.Button1Action);
            }
            else if (buttonNumber == 2 && ShipControlActions.ContainsKey(InputEvents.Button2Action))
            {
                StopShipControllerActions(InputEvents.Button2Action);
            }
            else if (buttonNumber == 3 && ShipControlActions.ContainsKey(InputEvents.Button3Action))
            {
                StopShipControllerActions(InputEvents.Button3Action);
            }
        }

        public void PerformClassResourceActions(ResourceEvents resourceEvent)
        {
            if (!resourceAbilityStartTimes.ContainsKey(resourceEvent))
                resourceAbilityStartTimes.Add(resourceEvent, Time.time);
            else
                resourceAbilityStartTimes[resourceEvent] = Time.time;

            if (ClassResourceActions.ContainsKey(resourceEvent))
            {
                var classResourceActions = ClassResourceActions[resourceEvent];
                foreach (var action in classResourceActions)
                    action.StartAction();
            }
        }
     
        public void StopClassResourceActions(ResourceEvents resourceEvent)
        {
            //if (StatsManager.Instance != null)
            //    StatsManager.Instance.AbilityActivated(Team, player.PlayerName, resourceEvent, Time.time-inputAbilityStartTimes[controlType]);

            if (ClassResourceActions.ContainsKey(resourceEvent))
            {
                var classResourceActions = ClassResourceActions[resourceEvent];
                foreach (var action in classResourceActions)
                    action.StopAction();
            }
        }

        public void ToggleCollision(bool enabled)
        {
            foreach (var collider in GetComponentsInChildren<Collider>(true))
                collider.enabled = enabled;
        }

        public void SetResourceLevels(ResourceCollection resourceGroup)
        {
            ResourceSystem.InitializeElementLevels(resourceGroup);
        }

        public void AssignCaptain(Captain captain)
        {
            SetResourceLevels(captain.ResourceLevels);
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

        public void SetBlockSilhouettePrefab(GameObject prefab)
        {
            if (Silhouette) Silhouette.SetBlockPrefab(prefab);
        }

        public void SetShieldedBlockMaterial(Material material)
        {
            TrailSpawner.SetShieldedBlockMaterial(material);
        }

        public void SetAOEExplosionMaterial(Material material)
        {
            AOEExplosionMaterial = material;
        }

        public void SetAOEConicExplosionMaterial(Material material)
        {
            AOEConicExplosionMaterial = material;
        }

        public void SetSkimmerMaterial(Material material)
        {
            SkimmerMaterial = material;
        }

        public void FlipShipUpsideDown() // TODO: move to shipController
        {
            OrientationHandle.transform.localRotation = Quaternion.Euler(0, 0, 180);
        }

        public void FlipShipRightsideUp()
        {
            OrientationHandle.transform.localRotation = Quaternion.Euler(0, 0, 0);
        }

        public void SetShipUp(float angle)
        {
            OrientationHandle.transform.localRotation = Quaternion.Euler(0, 0, angle);
        }

        public void Teleport(Transform targetTransform)
        {
            transform.SetPositionAndRotation(targetTransform.position, targetTransform.rotation);
        }

        // TODO: need to be able to disable ship abilities as well for minigames
        public void DisableSkimmer()
        {
            nearFieldSkimmer?.gameObject.SetActive(false);
            farFieldSkimmer?.gameObject.SetActive(false);
        }

        void ApplyShipMaterial()
        {
            if (ShipMaterial == null)
                return;

            foreach (var shipGeometry in shipGeometries)
            {
                if (shipGeometry.GetComponent<SkinnedMeshRenderer>() != null) 
                {
                    var materials = shipGeometry.GetComponent<SkinnedMeshRenderer>().materials;
                    materials[2] = ShipMaterial;
                    shipGeometry.GetComponent<SkinnedMeshRenderer>().materials = materials;
                }
                else if (shipGeometry.GetComponent<MeshRenderer>() != null)
                {
                    var materials = shipGeometry.GetComponent<MeshRenderer>().materials;
                    materials[1] = ShipMaterial;
                    shipGeometry.GetComponent<MeshRenderer>().materials = materials;
                } 
            }
        }

        //
        // Attach and Detach
        //
        void Attach(TrailBlock trailBlock) 
        {
            if (trailBlock.Trail != null)
            {
                ShipStatus.Attached = true;
                ShipStatus.AttachedTrailBlock = trailBlock;
            }
        }

    }
}