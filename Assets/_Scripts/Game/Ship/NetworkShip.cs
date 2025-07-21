using CosmicShore.Core;
using CosmicShore.Game.IO;
using CosmicShore.Game.Projectiles;
using CosmicShore.Models;
using CosmicShore.Models.Enums;
using CosmicShore.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;


namespace CosmicShore.Game
{
    /// <summary>
    /// /// DEPRECATED - Don't use this script anymore. Use IShip, R_ShipController instead.
    /// </summary>
    [RequireComponent(typeof(ShipStatus))]
    public class NetworkShip : NetworkBehaviour, IShip
    {
        public event Action<IShipStatus> OnShipInitialized;

        [Header("Ship Meta")]
        [SerializeField] string _name;
        [SerializeField] ShipClassType _shipType;

        [Header("Ship Components")]
        [SerializeField] Skimmer _nearFieldSkimmer;
        [SerializeField] GameObject _orientationHandle;
        [SerializeField] List<GameObject> _shipGeometries;

        [Header("Optional Ship Components")]
        [SerializeField] GameObject AOEPrefab;
        [SerializeField] Skimmer _farFieldSkimmer;

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

        [SerializeField] bool _bottomEdgeButtons = false;
        [SerializeField] float Inertia = 70f;

        [SerializeField] List<InputEventShipActionMapping> _inputEventShipActions;
        [SerializeField] List<ResourceEventShipActionMapping> _resourceEventClassActions;
        
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

        [SerializeField]
        BoolEventChannelSO onBottomEdgeButtonsEnabled;

        [SerializeField]
        InputEventsEventChannelSO OnButton1Pressed;

        [SerializeField]
        InputEventsEventChannelSO OnButton1Released;

        [SerializeField]
        InputEventsEventChannelSO OnButton2Pressed;

        [SerializeField]
        InputEventsEventChannelSO OnButton2Released;

        [SerializeField]
        InputEventsEventChannelSO OnButton3Pressed;

        [SerializeField]
        InputEventsEventChannelSO OnButton3Released;

        #region Public Properties

        private IShipStatus _shipStatus;
        public IShipStatus ShipStatus
        {
            get
            {
                _shipStatus ??= GetComponent<ShipStatus>();
                return _shipStatus;
            }
        }

        public Transform Transform => transform;

        #endregion

        Dictionary<InputEvents, float> _inputAbilityStartTimes = new();
        Dictionary<InputEvents, List<ShipAction>> _shipControlActions = new();
        Dictionary<ResourceEvents, List<ShipAction>> _classResourceActions = new();
        NetworkVariable<float> n_Speed = new(writePerm: NetworkVariableWritePermission.Owner);
        NetworkVariable<Vector3> n_Course = new(writePerm: NetworkVariableWritePermission.Owner);
        NetworkVariable<Quaternion> n_BlockRotation = new(writePerm: NetworkVariableWritePermission.Owner);

        Material _shipMaterial;

        float speedModifierDuration = 2f;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                n_Speed.OnValueChanged += OnSpeedChanged;
                n_Course.OnValueChanged += OnCourseChanged;
                n_BlockRotation.OnValueChanged += OnBlockRotationChanged;
            }
            else
            {
                OnButton1Pressed.OnEventRaised += PerformShipControllerActions;
                OnButton1Released.OnEventRaised += StopShipControllerActions;
                OnButton2Pressed.OnEventRaised += PerformShipControllerActions;
                OnButton2Released.OnEventRaised += StopShipControllerActions;
                OnButton3Pressed.OnEventRaised += PerformShipControllerActions;
                OnButton3Released.OnEventRaised += StopShipControllerActions;
            }
        }

        private void Update()
        {
            if (IsOwner)
            {
                n_Speed.Value = ShipStatus.Speed;
                n_Course.Value = ShipStatus.Course;
                n_BlockRotation.Value = ShipStatus.blockRotation;
            }
        }
        
        public override void OnNetworkDespawn()
        {
            if (!IsOwner)
            {
                n_Speed.OnValueChanged -= OnSpeedChanged;
                n_Course.OnValueChanged -= OnCourseChanged;
                n_BlockRotation.OnValueChanged -= OnBlockRotationChanged;
            }
            else
            {
                OnButton1Pressed.OnEventRaised -= PerformShipControllerActions;
                OnButton1Released.OnEventRaised -= StopShipControllerActions;
                OnButton2Pressed.OnEventRaised -= PerformShipControllerActions;
                OnButton2Released.OnEventRaised -= StopShipControllerActions;
                OnButton3Pressed.OnEventRaised -= PerformShipControllerActions;
                OnButton3Released.OnEventRaised -= StopShipControllerActions;
            }
        }

        public void Initialize(IPlayer player, bool enableAIPilot)
        {
            ShipStatus.Player = player;

            ShipStatus.ShipAnimation.Initialize(ShipStatus);
            ShipStatus.TrailSpawner.Initialize(ShipStatus);

            if (_nearFieldSkimmer != null)
                _nearFieldSkimmer.Initialize(ShipStatus);

            if (_farFieldSkimmer != null)
                _farFieldSkimmer.Initialize(ShipStatus);
            

            if (IsOwner)
            {
                if (!ShipStatus.FollowTarget) ShipStatus.FollowTarget = transform;

                // TODO - Remove GameCanvas dependency
                onBottomEdgeButtonsEnabled.RaiseEvent(true);
                // if (_bottomEdgeButtons) ShipStatus.Player.GameCanvas.MiniGameHUD.PositionButtonPanel(true);

                InitializeShipControlActions();
                InitializeClassResourceActions();

                /*ShipStatus.AIPilot.AssignShip(this);
                ShipStatus.AIPilot.Initialize(false);*/
                ShipStatus.ShipCameraCustomizer.Initialize(this);
                ShipStatus.ShipTransformer.Initialize(this);
            }

            ShipStatus.ShipTransformer.enabled = IsOwner;
            ShipStatus.TrailSpawner.ForceStartSpawningTrail();
            ShipStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(2f);

            OnShipInitialized?.Invoke(ShipStatus);
        }

        void InitializeShipControlActions() => ShipHelper.InitializeShipControlActions(ShipStatus, _inputEventShipActions, _shipControlActions);

        void InitializeClassResourceActions() => ShipHelper.InitializeClassResourceActions(ShipStatus, _resourceEventClassActions, _classResourceActions);

        public void PerformShipControllerActions(InputEvents @event)
        {
            ShipHelper.PerformShipControllerActions(@event, _inputAbilityStartTimes, _shipControlActions);
        }

        public void StopShipControllerActions(InputEvents @event)
        {
            if (StatsManager.Instance != null)
                StatsManager.Instance.AbilityActivated(ShipStatus.Team, ShipStatus.Player.PlayerName, @event, Time.time - _inputAbilityStartTimes[@event]);

            ShipHelper.StopShipControllerActions(@event, _shipControlActions);
        }

        public void Teleport(Transform targetTransform) => ShipHelper.Teleport(transform, targetTransform);

        public void SetResourceLevels(ResourceCollection resourceGroup)
        {
            ShipStatus.ResourceSystem.InitializeElementLevels(resourceGroup);
        }

        public void SetShipUp(float angle)
        {
            _orientationHandle.transform.localRotation = Quaternion.Euler(0, 0, angle);
        }

        public void DisableSkimmer()
        {
            _nearFieldSkimmer?.gameObject.SetActive(false);
            _farFieldSkimmer?.gameObject.SetActive(false);
        }

        public void PerformCrystalImpactEffects(CrystalProperties crystalProperties)
        {
            /*foreach (CrystalImpactEffects effect in crystalImpactEffects)
            {
                switch (effect)
                {
                    case CrystalImpactEffects.PlayHaptics:
                        if (!ShipStatus.AutoPilotEnabled) HapticController.PlayHaptic(HapticType.CrystalCollision);//.PlayCrystalImpactHaptics();
                        break;
                    case CrystalImpactEffects.AreaOfEffectExplosion:
                        var aoeExplosion = Instantiate(AOEPrefab).GetComponent<AOEExplosion>();
                        aoeExplosion.SetPositionAndRotation(transform.position, transform.rotation);
                        aoeExplosion.MaxScale = ShipStatus.ResourceSystem.Resources.Count > ammoResourceIndex
                            ? Mathf.Lerp(minExplosionScale, maxExplosionScale, ShipStatus.ResourceSystem.Resources[ammoResourceIndex].CurrentAmount) : maxExplosionScale;
                        aoeExplosion.InitializeAndDetonate(this);
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
            }*/
        }

        public void SetBoostMultiplier(float multiplier)
        {
            boostMultiplier = multiplier;
        }

        public void ToggleGameObject(bool toggle)
        {
            gameObject.SetActive(toggle);
        }

        public void SetShipMaterial(Material material)
        {
            _shipMaterial = material;
            ShipHelper.ApplyShipMaterial(_shipMaterial, _shipGeometries);
        }

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

        public void BindElementalFloat(string statName, Element element)
        {
            Debug.Log($"Ship.NotifyShipStatBinding - statName:{statName}, element:{element}");
            if (ElementStats.All(x => x.StatName != statName))
                ElementStats.Add(new ElementStat(statName, element));

            Debug.Log($"Ship.NotifyShipStatBinding - ElementStats.Count:{ElementStats.Count}");
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
                        ShipStatus.ShipTransformer.ModifyVelocity((transform.position - trailBlockProperties.trailBlock.transform.position).normalized * 5, Time.deltaTime * 15);
                        break;
                    case TrailBlockImpactEffects.Redirect:
                        ShipStatus.ShipTransformer.GentleSpinShip(.5f * transform.forward + .5f * (UnityEngine.Random.value < 0.5f ? -1f : 1f) * transform.right, transform.up, 1);
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

        void OnSpeedChanged(float previousValue, float newValue)
        {
            ShipStatus.Speed = newValue;
        }

        void OnCourseChanged(Vector3  previousValue, Vector3 newValue)
        {
            ShipStatus.Course = newValue;
        }

        void OnBlockRotationChanged(Quaternion previousValue, Quaternion newValue)
        {
            ShipStatus.blockRotation = newValue;
        }

        void Attach(TrailBlock trailBlock)
        {
            if (trailBlock.Trail != null)
            {
                ShipStatus.Attached = true;
                ShipStatus.AttachedTrailBlock = trailBlock;
            }
        }

        public void PerformButtonActions(int buttonNumber)
        {
            throw new NotImplementedException();
        }

        public void SetAISkillLevel(int value)
        {
            throw new NotImplementedException();
        }

        public void OnButtonPressed(int buttonNumber)
        {
            throw new NotImplementedException();
        }
    }

}
