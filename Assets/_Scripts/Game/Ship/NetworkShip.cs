using CosmicShore.Core;
using CosmicShore.Game.AI;
using CosmicShore.Game.Animation;
using CosmicShore.Game.IO;
using CosmicShore.Models.Enums;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;


namespace CosmicShore.Game
{
    [RequireComponent(typeof(ShipStatus))]
    [RequireComponent(typeof(AIPilot))]
    [RequireComponent(typeof(ResourceSystem))]
    [RequireComponent(typeof(ShipCameraCustomizer))]
    public class NetworkShip : NetworkBehaviour, IShip
    {
        public event Action OnShipInitialized;

        [Header("Ship Meta")]
        [SerializeField] string _name;
        [SerializeField] ShipTypes _shipType;

        [Header("Ship Components")]
        [SerializeField] Skimmer _nearFieldSkimmer;
        [SerializeField] GameObject _orientationHandle;
        [SerializeField] List<GameObject> _shipGeometries;

        [Header("Optional Ship Components")]
        [SerializeField] Skimmer _farFieldSkimmer;

        [Header("Configuration")]
        [SerializeField] bool _bottomEdgeButtons = false;
        [SerializeField] List<InputEventShipActionMapping> _inputEventShipActions;
        [SerializeField] List<ResourceEventShipActionMapping> _resourceEventClassActions;

        #region Public Properties

        public string ShipName => _name;
        public ShipTypes GetShipType => _shipType;
        public Material AOEExplosionMaterial { get; private set; }
        public Material AOEConicExplosionMaterial { get; private set; }
        public Material SkimmerMaterial { get; private set; }
        public Transform FollowTarget { get; private set; }

        Teams _team;
        public Teams Team
        {
            get => _team;
            private set
            {
                _team = value;
                if (_nearFieldSkimmer != null) _nearFieldSkimmer.Team = value;
                if (_farFieldSkimmer != null) _farFieldSkimmer.Team = value;
            }
        }

        AIPilot _aiPilot;
        public AIPilot AIPilot
        {
            get
            {
                _aiPilot = _aiPilot != null ? _aiPilot : gameObject.GetComponent<AIPilot>();
                return _aiPilot;
            }
        }

        ResourceSystem _resourceSystem;
        public ResourceSystem ResourceSystem
        {
            get
            {
                _resourceSystem = _resourceSystem != null ? _resourceSystem : GetComponent<ResourceSystem>();
                return _resourceSystem;
            }
        }

        ShipStatus _shipStatus;
        public ShipStatus ShipStatus
        {
            get
            {
                _shipStatus = _shipStatus != null ? _shipStatus : GetComponent<ShipStatus>();
                return _shipStatus;
            }
        }

        IPlayer _player;
        public IPlayer Player
        {
            get => _player;
            private set
            {
                _player = value;
                if (_nearFieldSkimmer != null) _nearFieldSkimmer.Player = value;
                if (_farFieldSkimmer != null) _farFieldSkimmer.Player = value;
            }
        }

        InputController _inputController;
        public InputController InputController
        {
            get
            {
                if (_inputController == null)
                {
                    if (Player == null)
                    {
                        Debug.LogError($"No player found to get input controller!");
                        return null;
                    }
                    if (Player.InputController == null)
                    {
                        Debug.LogError($"No input controller inside player found!");
                        return null;
                    }
                    _inputController = Player.InputController;
                }
                return _inputController;
            }
        }

        ShipTransformer _shipTransformer;
        public ShipTransformer ShipTransformer
        {
            get
            {
                _shipTransformer = _shipTransformer != null ? _shipTransformer : GetComponent<ShipTransformer>();
                return _shipTransformer;
            }
        }

        ShipAnimation _shipAnimation;
        public ShipAnimation ShipAnimation
        {
            get
            {
                _shipAnimation = _shipAnimation != null ? _shipAnimation : GetComponent<ShipAnimation>();
                return _shipAnimation;
            }
        }

        TrailSpawner _trailSpawner;
        public TrailSpawner TrailSpawner
        {
            get
            {
                _trailSpawner = _trailSpawner != null ? _trailSpawner : GetComponent<TrailSpawner>();
                return _trailSpawner;
            }
        }

        ShipCameraCustomizer _shipCameraCustomizer;
        public ShipCameraCustomizer ShipCameraCustomizer
        {
            get
            {
                _shipCameraCustomizer = _shipCameraCustomizer != null ? _shipCameraCustomizer : GetComponent<ShipCameraCustomizer>();
                return _shipCameraCustomizer;
            }
        }

        public Silhouette Silhouette => throw new NotImplementedException();

        public float GetInertia => throw new NotImplementedException();

        public float BoostMultiplier { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public SO_Captain Captain => throw new NotImplementedException();

        public CameraManager CameraManager => CameraManager.Instance;

        public List<GameObject> ShipGeometries => throw new NotImplementedException();

        public Transform Transform => transform;

        public List<InputEventShipActionMapping> InputEventShipActions => _inputEventShipActions;

        public IInputStatus InputStatus => InputController.InputStatus;

        #endregion

        Dictionary<InputEvents, float> _inputAbilityStartTimes = new();
        Dictionary<InputEvents, List<ShipAction>> _shipControlActions = new();
        Dictionary<ResourceEvents, List<ShipAction>> _classResourceActions = new();
        NetworkVariable<float> n_Speed = new(writePerm: NetworkVariableWritePermission.Owner);
        NetworkVariable<Vector3> n_Course = new(writePerm: NetworkVariableWritePermission.Owner);
        NetworkVariable<Quaternion> n_BlockRotation = new(writePerm: NetworkVariableWritePermission.Owner);

        Material _shipMaterial;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                n_Speed.OnValueChanged += OnSpeedChanged;
                n_Course.OnValueChanged += OnCourseChanged;
                n_BlockRotation.OnValueChanged += OnBlockRotationChanged;
            }
                

            ShipTransformer.enabled = IsOwner;
            TrailSpawner.ForceStartSpawningTrail();
            TrailSpawner.RestartTrailSpawnerAfterDelay(2f);
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
        }

        public void Initialize(IPlayer player, Teams team)
        {
            _player = player;
            _team = team;

            AIPilot.AutoPilotEnabled = false;
            InitializeShipGeometries();
            ShipAnimation.Initialize(this);
            TrailSpawner.Initialize(this);
            _nearFieldSkimmer.Initialize(this);
            _farFieldSkimmer.Initialize(this);
            

            if (IsOwner)
            {
                if (!FollowTarget) FollowTarget = transform;
                if (_bottomEdgeButtons) Player.GameCanvas.MiniGameHUD.PositionButtonPanel(true);

                InitializeShipControlActions();
                InitializeClassResourceActions();

                AIPilot.Initialize(this);
                ShipCameraCustomizer.Initialize(this);
                ShipTransformer.Initialize(this);
            }

            OnShipInitialized?.Invoke();
        }

        void InitializeShipGeometries() => ShipHelper.InitializeShipGeometries(this, _shipGeometries);

        void InitializeShipControlActions() => ShipHelper.InitializeShipControlActions(this, _inputEventShipActions, _shipControlActions);

        void InitializeClassResourceActions() => ShipHelper.InitializeClassResourceActions(this, _resourceEventClassActions, _classResourceActions);

        public void PerformShipControllerActions(InputEvents @event)
        {
            ShipHelper.PerformShipControllerActions(@event, out float time, _shipControlActions);
            _inputAbilityStartTimes[@event] = time;
        }

        public void StopShipControllerActions(InputEvents @event)
        {
            if (StatsManager.Instance != null)
                StatsManager.Instance.AbilityActivated(Team, _player.PlayerName, @event, Time.time - _inputAbilityStartTimes[@event]);

            ShipHelper.StopShipControllerActions(@event, _shipControlActions);
        }

        public void Teleport(Transform targetTransform) => ShipHelper.Teleport(transform, targetTransform);

        public void SetResourceLevels(ResourceCollection resourceGroup)
        {
            ResourceSystem.InitializeElementLevels(resourceGroup);
        }

        public void SetShipUp(float angle)
        {
            throw new NotImplementedException();
        }

        public void DisableSkimmer()
        {
            throw new NotImplementedException();
        }

        public void PerformCrystalImpactEffects(CrystalProperties crystalProperties)
        {
            //  throw new NotImplementedException();
        }

        public void SetBoostMultiplier(float boostMultiplier)
        {
            throw new NotImplementedException();
        }

        public void ToggleGameObject(bool toggle)
        {
            throw new NotImplementedException();
        }

        public void SetShipMaterial(Material material)
        {
            _shipMaterial = material;
            ShipHelper.ApplyShipMaterial(_shipMaterial, _shipGeometries);
        }

        public void SetBlockSilhouettePrefab(GameObject prefab)
        {
            // if (Silhouette) Silhouette.SetBlockPrefab(prefab);
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

        public void AssignCaptain(SO_Captain captain)
        {
            throw new NotImplementedException();    // not needed yet
        }

        public void BindElementalFloat(string name, Element element)
        {
            throw new NotImplementedException();        // not needed yet maybe
        }

        public void PerformTrailBlockImpactEffects(TrailBlockProperties trailBlockProperties)
        {
            // throw new NotImplementedException();
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
    }

}
