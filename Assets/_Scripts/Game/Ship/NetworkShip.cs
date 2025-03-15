using CosmicShore.Core;
using CosmicShore.Models.Enums;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;


namespace CosmicShore.Game
{
    [RequireComponent(typeof(ShipStatus))]
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


        IShipStatus _shipStatus;
        public IShipStatus ShipStatus
        {
            get
            {
                _shipStatus = _shipStatus ?? GetComponent<ShipStatus>();
                _shipStatus.Name = _name;
                _shipStatus.ShipType = _shipType;
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

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                n_Speed.OnValueChanged += OnSpeedChanged;
                n_Course.OnValueChanged += OnCourseChanged;
                n_BlockRotation.OnValueChanged += OnBlockRotationChanged;
            }
                

            ShipStatus.ShipTransformer.enabled = IsOwner;
            ShipStatus.TrailSpawner.ForceStartSpawningTrail();
            ShipStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(2f);
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

        public void Initialize(IPlayer player)
        {
            ShipStatus.Player = player;

            SetPlayerToShipStatusAndSkimmers(player);
            SetTeamToShipStatusAndSkimmers(player.Team);

            InitializeShipGeometries();

            ShipStatus.ShipAnimation.Initialize(ShipStatus);
            ShipStatus.TrailSpawner.Initialize(this);
            _nearFieldSkimmer?.Initialize(this);
            _farFieldSkimmer?.Initialize(this);
            

            if (IsOwner)
            {
                if (!_shipStatus.FollowTarget) ShipStatus.FollowTarget = transform;
                if (_bottomEdgeButtons) ShipStatus.Player.GameCanvas.MiniGameHUD.PositionButtonPanel(true);

                InitializeShipControlActions();
                InitializeClassResourceActions();

                ShipStatus.AIPilot.Initialize(false);
                ShipStatus.ShipCameraCustomizer.Initialize(this);
                ShipStatus.ShipTransformer.Initialize(this);
            }

            OnShipInitialized?.Invoke();
        }

        void SetTeamToShipStatusAndSkimmers(Teams team)
        {
            ShipStatus.Team = team;
            if (_nearFieldSkimmer != null) _nearFieldSkimmer.Team = team;
            if (_farFieldSkimmer != null) _farFieldSkimmer.Team = team;
        }

        void SetPlayerToShipStatusAndSkimmers(IPlayer player)
        {
            ShipStatus.Player = player;
            if (_nearFieldSkimmer != null) _nearFieldSkimmer.Player = player;
            if (_farFieldSkimmer != null) _farFieldSkimmer.Player = player;
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

        public void AssignCaptain(SO_Captain captain)
        {
            throw new NotImplementedException();    // not needed yet
        }

        public void BindElementalFloat(string name, Element element)
        {
            // throw new NotImplementedException();        // not needed yet maybe
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
