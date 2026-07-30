using System;
using CosmicShore.Data;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Combines behaviour of R_LocalVessel and R_NetworkVessel. Behaviour is
    /// selected at runtime based on <see cref="IsSpawned"/> in multiplayer mode.
    /// </summary>
    [RequireComponent(typeof(IVesselStatus))]
    public class VesselController : NetworkBehaviour, IVessel
    {
        [SerializeField]
        GameDataSO gameData;
        
        public event Action OnInitialized;
        public event Action OnBeforeDestroyed;
        
        IVesselStatus vesselStatus;
        public IVesselStatus VesselStatus
        {
            get
            {
                vesselStatus ??= GetComponent<IVesselStatus>();
                return vesselStatus;
            }
        }

        public bool IsNetworkOwner => IsSpawned && IsOwner;
        public bool IsNetworkClient => IsSpawned && !IsOwner;
        
        readonly NetworkVariable<float> n_Speed = new(writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Vector3> n_Course = new(writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Quaternion> n_BlockRotation = new(writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<bool> n_IsTranslationRestricted =
            new(writePerm: NetworkVariableWritePermission.Owner);
        
        public ulong PlayerNetId { get; private set; }
        public ulong VesselNetId => NetworkObjectId;
        public ulong OwnerClientNetId => OwnerClientId;
        
        public override void OnDestroy()
        {
            Debug.Log($"<color=#FFFF00>[VESSEL] OnDestroy '{gameObject.name}' - IsSpawned={IsSpawned}, IsServer={IsServer}, IsOwner={IsOwner}, NetObjId={NetworkObjectId}</color>");
            OnBeforeDestroyed?.Invoke();
        }

        public override void OnNetworkSpawn()
        {
            Debug.Log($"<color=#FFFF00>[VESSEL] OnNetworkSpawn '{gameObject.name}' - IsServer={IsServer}, IsOwner={IsOwner}, NetObjId={NetworkObjectId}</color>");
            // Cache it to game data early, so that later,
            // ClientInitializer can find the player and vessels with their Ids
            gameData.Vessels.Add(this);
            gameData.InvokeVesselNetworkSpawned();

            if (IsOwner)
                return;

            SubscribeToNetworkVariables();
        }

        public override void OnNetworkDespawn()
        {
            Debug.Log($"<color=#FFFF00>[VESSEL] OnNetworkDespawn '{gameObject.name}' - IsServer={IsServer}, IsOwner={IsOwner}, NetObjId={NetworkObjectId}</color>");
            if (IsOwner)
                return;

            UnsubscribeFromNetworkVariables();
        }

        void Update()
        {
            if (!IsSpawned || !IsOwner)
                return;

            // Per-frame owner→server kinematic replication - the hottest netcode write path.
            using (CosmicShore.Utility.PerformanceBenchmark.NetMarkers.Serialize.Auto())
            {
                n_Speed.Value = VesselStatus.Speed;
                n_Course.Value = VesselStatus.Course;
                n_BlockRotation.Value = VesselStatus.blockRotation;
                CosmicShore.Utility.PerformanceBenchmark.NetMarkers.CountNetVarDirty(3);
            }
        }

        public void Initialize(IPlayer player)
        {
            if (VesselStatus.Player != null)
            {
                CSDebug.LogError("Double initialization not allowed!");
                return;
            }

            VesselStatus.Player = player;
            VesselStatus.VesselAnimation.Initialize(VesselStatus);
            VesselStatus.VesselPrismController.Initialize(VesselStatus);

            if (!VesselStatus.CameraFollowTarget)
                VesselStatus.CameraFollowTarget = transform;

            VesselStatus.ActionHandler.Initialize(VesselStatus);
            VesselStatus.VesselTransformer.Initialize(this);
            VesselStatus.AIPilot.Initialize(this);

            var hudController = VesselStatus.VesselHUDController;
            if (hudController != null)
            {
                hudController.Initialize(VesselStatus);
                hudController.HideHUD();
            }
            else
            {
                CSDebug.LogWarning($"[VesselController] VesselHUDController is null on {name}. HUD will not function.");
            }

            if (VesselStatus.NearFieldSkimmer)
                VesselStatus.NearFieldSkimmer.Initialize(VesselStatus);

            if (VesselStatus.FarFieldSkimmer)
                VesselStatus.FarFieldSkimmer.Initialize(VesselStatus);

            VesselStatus.ElementalBarsController.Initialize(VesselStatus);
            VesselStatus.VesselTransformer.ToggleActive(true);

            if (player.IsLocalUser)
            {
                VesselStatus.ActionHandler.ToggleSubscription(true);
                VesselStatus.VesselCameraCustomizer.Initialize(this);
                hudController?.SubscribeToEvents();
            }

            if (gameData != null)
                ShipHelper.SetShipProperties(gameData.ThemeManagerData, this);
            else
                CSDebug.LogError($"[VesselController] GameDataSO is not assigned on {name}. Ship properties will not be set.");

            VesselStatus.Customization.Initialize(VesselStatus);
            VesselStatus.ResetForPlay();
            OnInitialized?.Invoke();
        }
        
        public Transform Transform => transform;

        public void Teleport(Transform targetTransform) =>
            ShipHelper.Teleport(transform, targetTransform);

        public void SetResourceLevels(ResourceCollection resources) =>
            VesselStatus.ResourceSystem.InitializeElementLevels(resources);

        public void SetShipUp(float angle) =>
            VesselStatus.OrientationHandle.transform.localRotation = Quaternion.Euler(0, 0, angle);

        public void DisableSkimmer()
        {
            VesselStatus.NearFieldSkimmer?.gameObject.SetActive(false);
            VesselStatus.FarFieldSkimmer?.gameObject.SetActive(false);
        }

        public void SetBoostMultiplier(float multiplier) => VesselStatus.BoostMultiplier = multiplier;
        
        public void SetShipMaterial(Material material) =>
            VesselStatus.ShipMaterial = material;

        public void SetAOEExplosionMaterial(Material material) =>
            VesselStatus.AOEExplosionMaterial = material;

        public virtual void SetAOEConicExplosionMaterial(Material material) =>
                VesselStatus.AOEConicExplosionMaterial = material;

        public virtual void SetSkimmerMaterial(Material material) =>
                VesselStatus.SkimmerMaterial = material;

        VesselTrailCustomization _trailCustomization;
        public virtual void SetTrailColors(Color highlightColor, Color coreColor)
        {
            if (_trailCustomization == null)
                _trailCustomization = GetComponentInChildren<VesselTrailCustomization>(includeInactive: true);
            _trailCustomization?.SetTrailColors(highlightColor, coreColor);
        }

        public virtual void BindElementalFloat(string name, Element element) =>
            VesselStatus.ElementalStatsHandler.BindElementalFloat(name, element);

        public void PerformShipControllerActions(InputEvents controlType) =>
                VesselStatus.ActionHandler.PerformShipControllerActions(controlType);

        public void StopShipControllerActions(InputEvents controlType) =>
                VesselStatus.ActionHandler.StopShipControllerActions(controlType);

        public void ToggleAIPilot(bool toggle)
        {
            // Any explicit pilot-mode change ends a cinematic flourish. EndGameSequencer
            // starts its behavior AFTER enabling the pilot, so the flourish still works;
            // nothing else may leave the cinematic writing input into a live vessel.
            // TryGetComponent, not the VesselStatus GetOrAdd accessor: a vessel that
            // never flourished shouldn't have the component instantiated just to
            // no-op stop it.
            if (TryGetComponent<AICinematicBehavior>(out var cinematic))
                cinematic.StopCinematicBehavior();

            if (toggle)
                VesselStatus.AIPilot.StartAIPilot();
            else
                VesselStatus.AIPilot.StopAIPilot();
        }

        public bool AllowClearPrismInitialization() => (IsSpawned && IsOwner) || VesselStatus.IsInitializedAsAI;

        public void DestroyVessel()
        {
            if (IsSpawned)
            {
                if (IsServer)
                    NetworkObject.Despawn(true);
                return;
            }
            Destroy(gameObject);
        }

        public void StartVessel()
        {
            ToggleStationaryMode(false);
            VesselStatus.VesselPrismController.StartSpawn();
        }

        public void ResetForPlay()
        {
            if (IsSpawned && IsOwner)
            {
                VesselStatus.Speed = 0f;
                VesselStatus.Course = transform.forward;
                VesselStatus.blockRotation = Quaternion.identity;
            }
            VesselStatus.ResetForPlay();
        }

        public void SetPose(Pose pose)
        {
            if (IsSpawned)
                SetPose_ClientRpc(pose);
            else
                SetPose_Local(pose);
        }

        public void SetTranslationRestricted(bool value)
        {
            if (IsNetworkOwner)
                n_IsTranslationRestricted.Value = value;

            VesselStatus.IsTranslationRestricted = value; 
        }

        public void ModifyThrottle(float amount, float duration) =>
            VesselStatus.VesselTransformer.ModifyThrottle(amount, duration);
        
        public void AddSlowedShipTransformToGameData()
        {
            if (IsSpawned)
                AddSlowedShipTransformToGameData_ServerRpc();
            else
                AddSlowedShipTransformToGameData_Local();
        }
        
        public void RemoveSlowedShipTransformFromGameData()
        {
            if (IsSpawned)
                RemoveSlowedShipTransformFromGameData_ServerRpc();
            else
                RemoveSlowedShipTransformFromGameData_Local();
        }

        [ServerRpc(RequireOwnership = false)]
        void RemoveSlowedShipTransformFromGameData_ServerRpc() =>
            RemoveSlowedShipTransformFromGameData_ClientRpc();

        [ClientRpc]
        void RemoveSlowedShipTransformFromGameData_ClientRpc() =>
            RemoveSlowedShipTransformFromGameData_Local();
        void RemoveSlowedShipTransformFromGameData_Local() =>
            gameData?.SlowedShipTransforms.Remove(transform);
        
        [ServerRpc(RequireOwnership = false)]
        void AddSlowedShipTransformToGameData_ServerRpc() =>
            AddSlowedShipTransformToGameData_ClientRpc();

        [ClientRpc]
        void AddSlowedShipTransformToGameData_ClientRpc() =>
            AddSlowedShipTransformToGameData_Local();
        void AddSlowedShipTransformToGameData_Local() =>
            gameData?.SlowedShipTransforms.Add(transform);

        [ClientRpc]
        void SetPose_ClientRpc(Pose pose) => SetPose_Local(pose);

        void SetPose_Local(Pose pose) => VesselStatus.VesselTransformer.SetPose(pose);

        // Route to the owner (like SetPose) so a party client's own swapped vessel also inherits
        // the previous ship's speed, not just the host's.
        public void SetInitialSpeed(float initialSpeed)
        {
            if (IsSpawned)
                SetInitialSpeed_ClientRpc(initialSpeed);
            else
                SetInitialSpeed_Local(initialSpeed);
        }

        [ClientRpc]
        void SetInitialSpeed_ClientRpc(float initialSpeed) => SetInitialSpeed_Local(initialSpeed);

        void SetInitialSpeed_Local(float initialSpeed) => VesselStatus.VesselTransformer.SetInitialSpeed(initialSpeed);
        
        void OnSpeedChanged(float previousValue, float newValue) => VesselStatus.Speed = newValue;
        void OnCourseChanged(Vector3 previousValue, Vector3 newValue) => VesselStatus.Course = newValue;
        void OnBlockRotationChanged(Quaternion previousValue, Quaternion newValue) => VesselStatus.blockRotation = newValue;
        void OnIsTranslationRestrictedValueChanged(bool previousValue, bool newValue) => VesselStatus.IsTranslationRestricted = newValue;
        
        void SubscribeToNetworkVariables()
        {
            n_Speed.OnValueChanged += OnSpeedChanged;
            n_Course.OnValueChanged += OnCourseChanged;
            n_BlockRotation.OnValueChanged += OnBlockRotationChanged;
            n_IsTranslationRestricted.OnValueChanged += OnIsTranslationRestrictedValueChanged;
        }
        
        void UnsubscribeFromNetworkVariables()
        {
            n_Speed.OnValueChanged -= OnSpeedChanged;
            n_Course.OnValueChanged -= OnCourseChanged;
            n_BlockRotation.OnValueChanged -= OnBlockRotationChanged;
            n_IsTranslationRestricted.OnValueChanged -= OnIsTranslationRestrictedValueChanged;
        }
        
        void ToggleStationaryMode(bool enable) =>
            VesselStatus.IsStationary = enable;
    }
}
