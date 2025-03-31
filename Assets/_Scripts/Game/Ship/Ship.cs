using CosmicShore.Game;
using CosmicShore.Game.IO;
using CosmicShore.Game.Projectiles;
using CosmicShore.Models;
using CosmicShore.Models.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;


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

    [RequireComponent(typeof(ShipStatus))]
    public class Ship : MonoBehaviour, IShip
    {
        public event Action OnShipInitialized;

        [SerializeField] List<ImpactProperties> impactProperties;
        

        [Header("Ship Meta")]

        [SerializeField] string Name;

        [FormerlySerializedAs("ShipType")]
        [SerializeField] ShipTypes _shipType;


        [Header("Ship Components")]

       
        [SerializeField] Skimmer nearFieldSkimmer;
        [SerializeField] GameObject OrientationHandle;

        [SerializeField] List<GameObject> shipGeometries;
        public List<GameObject> ShipGeometries => shipGeometries;

        [SerializeField] GameObject shipHUD;

        IShipStatus _shipStatus;

        public IShipStatus ShipStatus
        {
            get
            {
                _shipStatus = _shipStatus != null ? _shipStatus : GetComponent<ShipStatus>();
                _shipStatus.Name = name;

                _shipStatus.ShipType = _shipType;
                _shipStatus.ShipTransform = transform;
                return _shipStatus;
            }
        }


        [Header("Optional Ship Components")]

        [SerializeField] GameObject AOEPrefab;
        [SerializeField] Skimmer farFieldSkimmer;

        [SerializeField] int resourceIndex = 0;
        [SerializeField] int ammoResourceIndex = 0; // TODO: move to an ability system with separate classes
        [SerializeField] int boostResourceIndex = 0; // TODO: move to an ability system with separate classes

        [Header("Environment Interactions")]
        [SerializeField] public List<CrystalImpactEffects> crystalImpactEffects;
        [ShowIf(CrystalImpactEffects.AreaOfEffectExplosion)] 
        [SerializeField] float minExplosionScale = 50; // TODO: depricate "ShowIf" once we adopt modularity
        [ShowIf(CrystalImpactEffects.AreaOfEffectExplosion)] 
        [SerializeField] float maxExplosionScale = 400;

        [SerializeField] List<TrailBlockImpactEffects> trailBlockImpactEffects;
        [SerializeField] float blockChargeChange;

        [Header("Configuration")]
        [SerializeField] float boostMultiplier = 4f; // TODO: Move to ShipController
        public float BoostMultiplier { get => boostMultiplier; set => boostMultiplier = value; }

        [SerializeField] bool bottomEdgeButtons = false;
        [SerializeField] float Inertia = 70f;

        [SerializeField] List<InputEventShipActionMapping> inputEventShipActions;
        Dictionary<InputEvents, List<ShipAction>> ShipControlActions = new();

        [SerializeField] List<ResourceEventShipActionMapping> resourceEventClassActions;
        Dictionary<ResourceEvents, List<ShipAction>> ClassResourceActions = new();
        
        Dictionary<InputEvents, float> inputAbilityStartTimes = new();
        Dictionary<ResourceEvents, float> resourceAbilityStartTimes = new();

        Material ShipMaterial;

        float speedModifierDuration = 2f;
        

        [Serializable]
        public struct ElementStat
        {
            public string StatName;
            public Element Element;

            public ElementStat(string statName, Element element)
            {
                StatName = statName;
                Element = element;
            }
        }

        [SerializeField] List<ElementStat> ElementStats = new();

        public Transform Transform => transform;
        public IInputStatus InputStatus => ShipStatus.InputController.InputStatus;

        private void Start()
        {
            _shipStatus.Name = name;
            _shipStatus.ShipType = _shipType;
            _shipStatus.ShipTransform = transform;
        }


        public void Initialize(IPlayer player)
        {
            SetPlayerToShipStatusAndSkimmers(player);
            SetTeamToShipStatusAndSkimmers(player.Team);

            if (ShipStatus.FollowTarget == null) ShipStatus.FollowTarget = transform;
            if (bottomEdgeButtons) ShipStatus.Player.GameCanvas.MiniGameHUD.PositionButtonPanel(true);

            InitializeShipGeometries();
            InitializeShipControlActions();
            InitializeClassResourceActions();

            ShipStatus.Silhouette.Initialize(this);
            ShipStatus.ShipTransformer.Initialize(this);
            ShipStatus.ShipAnimation.Initialize(ShipStatus);
            //ShipStatus.AIPilot.Initialize(false);
            ShipStatus.AIPilot.Initialize(player.Ship.ShipStatus.AIPilot.AutoPilotEnabled);
            nearFieldSkimmer?.Initialize(this);
            farFieldSkimmer?.Initialize(this);
            ShipStatus.ShipCameraCustomizer.Initialize(this);
            ShipStatus.TrailSpawner.Initialize(this);

            if (ShipStatus.AIPilot.AutoPilotEnabled) return;
            if (!shipHUD) return;
            shipHUD.SetActive(true);
            foreach (var child in shipHUD.GetComponentsInChildren<Transform>(false))
            {
                child.SetParent(ShipStatus.Player.GameCanvas.transform, false);
                child.SetSiblingIndex(0);   // Don't draw on top of modal screens
            }

            OnShipInitialized?.Invoke();
        }

        void SetTeamToShipStatusAndSkimmers(Teams team)
        {
            ShipStatus.Team = team;
            if (nearFieldSkimmer != null) nearFieldSkimmer.Team = team;
            if (farFieldSkimmer != null) farFieldSkimmer.Team = team;
        }

        void SetPlayerToShipStatusAndSkimmers(IPlayer player)
        {
            ShipStatus.Player = player;
            if (nearFieldSkimmer != null) nearFieldSkimmer.Player = player;
            if (farFieldSkimmer != null) farFieldSkimmer.Player = player;
        }

        void InitializeShipGeometries() => ShipHelper.InitializeShipGeometries(this, shipGeometries);
        void InitializeShipControlActions() => ShipHelper.InitializeShipControlActions(this, inputEventShipActions, ShipControlActions);
        void InitializeClassResourceActions() => ShipHelper.InitializeClassResourceActions(this, resourceEventClassActions, ClassResourceActions);

        public void BindElementalFloat(string statName, Element element)
        {
            Debug.Log($"Ship.NotifyShipStatBinding - statName:{statName}, element:{element}");
            if (ElementStats.All(x => x.StatName != statName))
                ElementStats.Add(new ElementStat(statName, element));
            
            Debug.Log($"Ship.NotifyShipStatBinding - ElementStats.Count:{ElementStats.Count}");
        }

        public void PerformCrystalImpactEffects(CrystalProperties crystalProperties) // TODO: move to an ability system with separate classes
        {
            foreach (CrystalImpactEffects effect in crystalImpactEffects)
            {
                switch (effect)
                {
                    case CrystalImpactEffects.PlayHaptics:
                        if (!ShipStatus.AutoPilotEnabled) HapticController.PlayHaptic(HapticType.CrystalCollision);//.PlayCrystalImpactHaptics();
                        break;
                    case CrystalImpactEffects.AreaOfEffectExplosion:
                        var aoeExplosion = Instantiate(AOEPrefab).GetComponent<AOEExplosion>();
                        aoeExplosion.Initialize(this);
                        aoeExplosion.SetPositionAndRotation(transform.position, transform.rotation);
                        aoeExplosion.MaxScale = ShipStatus.ResourceSystem.Resources.Count > ammoResourceIndex 
                            ? Mathf.Lerp(minExplosionScale, maxExplosionScale, ShipStatus.ResourceSystem.Resources[ammoResourceIndex].CurrentAmount) : maxExplosionScale;
                        break;
                    case CrystalImpactEffects.IncrementLevel:
                        ShipStatus.ResourceSystem.IncrementLevel(crystalProperties.Element); // TODO: consider removing here and leaving this up to the crystals
                        break;
                    case CrystalImpactEffects.FillCharge:
                        ShipStatus.ResourceSystem.ChangeResourceAmount(boostResourceIndex, crystalProperties.fuelAmount);
                        break;
                    case CrystalImpactEffects.Boost:
                        ShipStatus.ShipTransformer.ModifyThrottle(crystalProperties.speedBuffAmount, 4 * speedModifierDuration);
                        break;
                    case CrystalImpactEffects.DrainAmmo:
                        ShipStatus.ResourceSystem.ChangeResourceAmount(ammoResourceIndex, -ShipStatus.ResourceSystem.Resources[ammoResourceIndex].CurrentAmount);
                        break;
                    case CrystalImpactEffects.GainOneThirdMaxAmmo:
                        ShipStatus.ResourceSystem.ChangeResourceAmount(ammoResourceIndex, ShipStatus.ResourceSystem.Resources[ammoResourceIndex].CurrentAmount / 3f);
                        break;
                    case CrystalImpactEffects.GainFullAmmo:
                        ShipStatus.ResourceSystem.ChangeResourceAmount(ammoResourceIndex, ShipStatus.ResourceSystem.Resources[ammoResourceIndex].MaxAmount);
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
                        ShipStatus.ResourceSystem.ChangeResourceAmount(ammoResourceIndex, -ShipStatus.ResourceSystem.Resources[ammoResourceIndex].CurrentAmount / 2f);
                        break;
                    case TrailBlockImpactEffects.DebuffSpeed:
                        ShipStatus.ShipTransformer.ModifyThrottle(trailBlockProperties.speedDebuffAmount, speedModifierDuration);
                        break;
                    case TrailBlockImpactEffects.DeactivateTrailBlock:
                        break;
                    case TrailBlockImpactEffects.ActivateTrailBlock:
                        break;
                    case TrailBlockImpactEffects.OnlyBuffSpeed:
                        if (trailBlockProperties.speedDebuffAmount > 1) ShipStatus.ShipTransformer.ModifyThrottle(trailBlockProperties.speedDebuffAmount, speedModifierDuration);
                        break;
                    case TrailBlockImpactEffects.GainResourceByVolume:
                        ShipStatus.ResourceSystem.ChangeResourceAmount(boostResourceIndex, blockChargeChange);
                        break;
                    case TrailBlockImpactEffects.Attach:
                        Attach(trailBlockProperties.trailBlock);
                        ShipStatus.GunsActive = true;
                        break;
                    case TrailBlockImpactEffects.GainResource:
                        ShipStatus.ResourceSystem.ChangeResourceAmount(resourceIndex, blockChargeChange);
                        break;
                    case TrailBlockImpactEffects.Bounce:
                        var cross = Vector3.Cross(transform.forward, trailBlockProperties.trailBlock.transform.forward);
                        var normal = Quaternion.AngleAxis(90, cross) * trailBlockProperties.trailBlock.transform.forward;
                        var reflectForward = Vector3.Reflect(transform.forward, normal);
                        var reflectUp = Vector3.Reflect(transform.up, normal);
                        ShipStatus.ShipTransformer.GentleSpinShip(reflectForward, reflectUp, 1);
                        ShipStatus.ShipTransformer.ModifyVelocity((transform.position - trailBlockProperties.trailBlock.transform.position).normalized * 5 , Time.deltaTime * 15);
                        break;
                    case TrailBlockImpactEffects.Redirect:
                        ShipStatus.ShipTransformer.GentleSpinShip(.5f*transform.forward + .5f * (UnityEngine.Random.value < 0.5f ? -1f : 1f) * transform.right, transform.up, 1);
                        break;
                    case TrailBlockImpactEffects.Explode:
                        trailBlockProperties.trailBlock.Damage(ShipStatus.Course * ShipStatus.Speed * Inertia, ShipStatus.Team, ShipStatus.Player.PlayerName);
                        break;
                    case TrailBlockImpactEffects.FeelDanger:
                        if (trailBlockProperties.IsDangerous && trailBlockProperties.trailBlock.Team != ShipStatus.Team)
                        {
                            HapticController.PlayHaptic(HapticType.FakeCrystalCollision);
                            ShipStatus.ShipTransformer.ModifyThrottle(trailBlockProperties.speedDebuffAmount, 1.5f);
                        }
                        break;
                    case TrailBlockImpactEffects.Steal:
                        break;
                    case TrailBlockImpactEffects.DecrementLevel:
                        break;
                    case TrailBlockImpactEffects.Shield:
                        break;
                    case TrailBlockImpactEffects.Stop:
                        break;
                    case TrailBlockImpactEffects.Fire:
                        break;
                    case TrailBlockImpactEffects.FX:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public void PerformShipControllerActions(InputEvents controlType)
        {
            inputAbilityStartTimes[controlType] = Time.time;

            if (!ShipControlActions.TryGetValue(controlType, out var shipControlActions)) return;
            foreach (var action in shipControlActions)
                action.StartAction();
        }

        public void StopShipControllerActions(InputEvents controlType)
        {
            if (StatsManager.Instance != null)
                StatsManager.Instance.AbilityActivated(ShipStatus.Team, ShipStatus.Player.PlayerName, controlType, Time.time-inputAbilityStartTimes[controlType]);

            if (ShipControlActions.TryGetValue(controlType, out var shipControlActions))
            {
                foreach (var action in shipControlActions)
                    action.StopAction();
            }
        }


        // this is used with buttons so "Find all references" will not return editor usage
        public void PerformButtonActions(int buttonNumber)
        {
            Debug.Log($"Ship.PerformButtonActions - buttonNumber:{buttonNumber}");
            
            switch (buttonNumber)
            {
                case 1:
                    if(ShipControlActions.ContainsKey(InputEvents.Button1Action))
                        PerformShipControllerActions(InputEvents.Button1Action);
                    break;
                case 2:
                    if(ShipControlActions.ContainsKey(InputEvents.Button2Action))
                        PerformShipControllerActions(InputEvents.Button2Action);
                    break;
                case 3:
                    if(ShipControlActions.ContainsKey(InputEvents.Button3Action))
                        PerformShipControllerActions(InputEvents.Button3Action);
                    break;
                default:
                    Debug.LogWarning($"Ship.PerformButtonActions - buttonNumber:{buttonNumber} is not associated to any of the ship actions.");
                    break;
            }
        }

        // this is used with buttons so "Find all references" will not return editor usage
        public void StopButtonActions(int buttonNumber)
        {
            Debug.Log($"Ship.StopButtonActions - buttonNumber:{buttonNumber}");

            switch (buttonNumber)
            {
                case 1:
                    if(ShipControlActions.ContainsKey(InputEvents.Button1Action))
                        StopShipControllerActions(InputEvents.Button1Action);
                    break;
                case 2:
                    if(ShipControlActions.ContainsKey(InputEvents.Button2Action))
                        StopShipControllerActions(InputEvents.Button2Action);
                    break;
                case 3:
                    if(ShipControlActions.ContainsKey(InputEvents.Button3Action))
                        StopShipControllerActions(InputEvents.Button3Action);
                    break;
                default:
                    Debug.LogWarning($"Ship.StopButtonActions - buttonNumber:{buttonNumber} is not associated to any of the ship actions.");
                    break;
            }
        }

        public void PerformClassResourceActions(ResourceEvents resourceEvent)
        {
            resourceAbilityStartTimes[resourceEvent] = Time.time;

            if (!ClassResourceActions.TryGetValue(resourceEvent, out var classResourceActions)) return;

            foreach (var action in classResourceActions)
                action.StartAction();
        }
     
        public void StopClassResourceActions(ResourceEvents resourceEvent)
        {
            //if (StatsManager.Instance != null)
            //    StatsManager.Instance.AbilityActivated(Team, player.PlayerName, resourceEvent, Time.time-inputAbilityStartTimes[controlType]);

            if (!ClassResourceActions.TryGetValue(resourceEvent, out var classResourceActions)) return;
            foreach (var action in classResourceActions)
                action.StopAction();
        }

        public void ToggleCollision(bool enabled)
        {
            foreach (var collider in GetComponentsInChildren<Collider>(true))
                collider.enabled = enabled;
        }

        public void SetResourceLevels(ResourceCollection resourceGroup)
        {
            ShipStatus.ResourceSystem.InitializeElementLevels(resourceGroup);
        }

        public void AssignCaptain(Captain captain)
        {
            ShipStatus.Captain = captain.SO_Captain;
            SetResourceLevels(captain.ResourceLevels);
        }

        public void AssignCaptain(SO_Captain captain)
        {
            ShipStatus.Captain = captain;
            SetResourceLevels(captain.InitialResourceLevels);
        }

        public void SetShipMaterial(Material material)
        {
            ShipMaterial = material;
            ApplyShipMaterial();
        }

        public void SetBoostMultiplier(float multiplier) => boostMultiplier = multiplier;

        public void ToggleGameObject(bool toggle) => gameObject.SetActive(toggle);

        public void SetBlockSilhouettePrefab(GameObject prefab)
        {
            if (ShipStatus.Silhouette) ShipStatus.Silhouette.SetBlockPrefab(prefab);
        }

        public void SetAOEExplosionMaterial(Material material)
        {
            ShipStatus.AOEExplosionMaterial = material;
        }

        public void SetAOEConicExplosionMaterial(Material material)
        {
            ShipStatus.AOEConicExplosionMaterial = material;
        }

        public void SetSkimmerMaterial(Material material)
        {
            ShipStatus.SkimmerMaterial = material;
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