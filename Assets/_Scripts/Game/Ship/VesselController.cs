using System;
using CosmicShore.Models.Enums;
using CosmicShore.Soap;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game
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

        public override void OnDestroy()
        {
            OnBeforeDestroyed?.Invoke();
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner) 
                return;
            
            SubscribeToNetworkVariables();
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner) 
                return;
            
            UnsubscribeFromNetworkVariables();
        }

        void Update()
        {
            if (!IsSpawned || !IsOwner) 
                return;
            
            n_Speed.Value = VesselStatus.Speed;
            n_Course.Value = VesselStatus.Course;
            n_BlockRotation.Value = VesselStatus.blockRotation;
        }

        public void Initialize(IPlayer player)
        {
            if (VesselStatus.Player != null)
            {
                Debug.LogError("Double initialization not allowed!");
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
            VesselStatus.VesselHUDController.Initialize(VesselStatus);
            VesselStatus.VesselHUDController.HideHUD();
            
            if (VesselStatus.NearFieldSkimmer) 
                VesselStatus.NearFieldSkimmer.Initialize(VesselStatus);

            if (VesselStatus.FarFieldSkimmer) 
                VesselStatus.FarFieldSkimmer.Initialize(VesselStatus);
            
            VesselStatus.Silhouette.Initialize(VesselStatus);
            VesselStatus.VesselTransformer.ToggleActive(true);
            
            if (player.IsLocalUser)
            {
                VesselStatus.ActionHandler.ToggleSubscription(true);
                VesselStatus.VesselCameraCustomizer.Initialize(this);
                VesselStatus.VesselHUDController.SubscribeToEvents();
                VesselStatus.VesselHUDController.ShowHUD();
            }
            
            ShipHelper.SetShipProperties(gameData.ThemeManagerData, this);
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

        public void SetBlockSilhouettePrefab(GameObject prefab)
        {
            // var trail =VesselStatus.VesselHUDView.TrailUI;
            // if (trail != null)
            // {
            //     trail.SetBlockPrefab(prefab, VesselStatus);
            //     return;
            // }

            VesselStatus?.VesselHUDController?.SetBlockPrefab(prefab);
        }

        public void SetAOEExplosionMaterial(Material material) =>
            VesselStatus.AOEExplosionMaterial = material;

        public virtual void SetAOEConicExplosionMaterial(Material material) =>
                VesselStatus.AOEConicExplosionMaterial = material;

        public virtual void SetSkimmerMaterial(Material material) =>
                VesselStatus.SkimmerMaterial = material;

        public virtual void AssignCaptain(SO_Captain captain)
        {
            VesselStatus.Captain = captain;
            SetResourceLevels(captain.InitialResourceLevels);
        }

        public virtual void BindElementalFloat(string name, Element element) =>
            VesselStatus.ElementalStatsHandler.BindElementalFloat(name, element);

        public void PerformShipControllerActions(InputEvents controlType) =>
                VesselStatus.ActionHandler.PerformShipControllerActions(controlType);

        public void StopShipControllerActions(InputEvents controlType) =>
                VesselStatus.ActionHandler.StopShipControllerActions(controlType);

        public void ToggleAIPilot(bool toggle)
        {
            if (toggle)
                VesselStatus.AIPilot.StartAIPilot();
            else
                VesselStatus.AIPilot.StopAIPilot();
        }

        public bool AllowClearPrismInitialization() => (IsSpawned && IsOwner) || VesselStatus.IsInitializedAsAI;

        public void DestroyVessel()
        {
            if (IsSpawned)
                return;
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

        public void ChangePlayer(IPlayer player)
        {
            VesselStatus.Player = player;

            // If the player is AI in general, or if it is a network client
            if (player.IsInitializedAsAI || player.IsNetworkClient)
            {
                VesselStatus.VesselHUDController.UnsubscribeFromEvents();
                if (player.IsInitializedAsAI)
                {
                    VesselStatus.VesselTransformer.ToggleActive(true);
                }
                if (player.IsNetworkClient)
                {
                    VesselStatus.VesselTransformer.ToggleActive(false);
                    SubscribeToNetworkVariables();
                }
                VesselStatus.ActionHandler.ToggleSubscription(false);
                VesselStatus.VesselHUDController.HideHUD();

                return;
            }
            
            UnsubscribeFromNetworkVariables();

            VesselStatus.VesselHUDController.SubscribeToEvents();
            VesselStatus.VesselHUDController.ShowHUD();

                
            VesselStatus.VesselTransformer.ToggleActive(true);
            VesselStatus.ActionHandler.ToggleSubscription(true);
            VesselStatus.VesselCameraCustomizer.RetargetAndApply(this);
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
