using System;
using CosmicShore.Models.Enums;
using Obvious.Soap;
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

        public bool IsOwnerClient => IsSpawned && IsOwner;

        readonly NetworkVariable<float> n_Speed = new(writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Vector3> n_Course = new(writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Quaternion> n_BlockRotation = new(writePerm: NetworkVariableWritePermission.Owner);

        public override void OnDestroy()
        {
            if ((!IsSpawned && VesselStatus.Player is { IsInitializedAsAI: false }) || IsOwner)
            {
                OnBeforeDestroyed?.Invoke();
            }
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

        public void Initialize(IPlayer player, bool enableAIPilot)
        {
            VesselStatus.Player = player;
            VesselStatus.VesselAnimation.Initialize(VesselStatus);
            VesselStatus.VesselPrismController.Initialize(VesselStatus);
            VesselStatus.Customization.Initialize(VesselStatus);
            if (!VesselStatus.CameraFollowTarget) 
                VesselStatus.CameraFollowTarget = transform;
            VesselStatus.ActionHandler.Initialize(VesselStatus);
            VesselStatus.VesselTransformer.Initialize(this);
            VesselStatus.ShipHUDController.Initialize(VesselStatus, VesselStatus.VesselHUDView);
            if (VesselStatus.VesselHUDView)
                VesselStatus.VesselHUDView.Hide();
            
            if (IsSpawned)
            {
                InitializeForMultiplayerMode();
            }
            else
            {
                InitializeForSinglePlayerMode(enableAIPilot);
            }
            
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
            var trail = VesselStatus?.VesselHUDView ? VesselStatus.VesselHUDView.TrailUI : null;
            if (trail != null)
            {
                trail.SetBlockPrefab(prefab);
                return;
            }

            VesselStatus?.ShipHUDController?.SetBlockPrefab(prefab);
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

        void ToggleStationaryMode(bool enable) =>
            VesselStatus.IsStationary = enable;

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

        public void SetPose(Pose pose) => 
            VesselStatus.VesselTransformer.SetPose(pose);

        public void ChangePlayer(IPlayer player)
        {
            VesselStatus.ShipHUDController.TearDown();
            VesselStatus.Player = player;

            if (player.IsInitializedAsAI || player.IsNetworkClient)
            {
                if (player.IsInitializedAsAI)
                    VesselStatus.VesselTransformer.ToggleActive(true);
                if (player.IsNetworkClient)
                {
                    VesselStatus.VesselTransformer.ToggleActive(false);
                    SubscribeToNetworkVariables();
                }
                VesselStatus.ActionHandler.ToggleSubscription(false);
                if (VesselStatus.VesselHUDView)
                    VesselStatus.VesselHUDView.Hide();
                return;
            }
            
            UnsubscribeFromNetworkVariables();

            if (VesselStatus.VesselHUDView)
                VesselStatus.VesselHUDView.Show();
                
            VesselStatus.VesselTransformer.ToggleActive(true);
            VesselStatus.ActionHandler.ToggleSubscription(true);
            VesselStatus.VesselCameraCustomizer.RetargetAndApply(this);
        }

        void InitializeForMultiplayerMode()
        {
            if (!IsOwner) 
                return;
            
            VesselStatus.VesselCameraCustomizer.Initialize(this);
            VesselStatus.Silhouette.Initialize(this);

            if (VesselStatus.NearFieldSkimmer)
                VesselStatus.NearFieldSkimmer.Initialize(VesselStatus);

            if (VesselStatus.FarFieldSkimmer)
                VesselStatus.FarFieldSkimmer.Initialize(VesselStatus);

            VesselStatus.VesselTransformer.ToggleActive(true);
            VesselStatus.ActionHandler.ToggleSubscription(true);

            if (VesselStatus.VesselHUDView)
                VesselStatus.VesselHUDView.Show();
        }
        
        void InitializeForSinglePlayerMode(bool enableAIPilot)
        {
            if (VesselStatus.NearFieldSkimmer) 
                VesselStatus.NearFieldSkimmer.Initialize(VesselStatus);

            if (VesselStatus.FarFieldSkimmer) 
                VesselStatus.FarFieldSkimmer.Initialize(VesselStatus);

            VesselStatus.Silhouette.Initialize(this);
            VesselStatus.VesselTransformer.ToggleActive(true);
            
            if (!enableAIPilot)
            {
                VesselStatus.ActionHandler.ToggleSubscription(true);
                VesselStatus.VesselHUDView.Show();   
                VesselStatus.VesselCameraCustomizer.Initialize(this);
            }
            
            // TODO - Currently AIPilot's update should run only after SingleStickVesselTransformer
            // sets SingleStickControls to true/false. Try finding a solution to remove this
            // sequential dependency.
            // AIPilot will be initialized both in User controlled / AI Vessels
            // Multiplayer modes will also have auto-pilot initialized
            
            VesselStatus.AIPilot.Initialize(enableAIPilot, this);
        }

        void OnSpeedChanged(float previousValue, float newValue) => VesselStatus.Speed = newValue;
        void OnCourseChanged(Vector3 previousValue, Vector3 newValue) => VesselStatus.Course = newValue;
        void OnBlockRotationChanged(Quaternion previousValue, Quaternion newValue) => VesselStatus.blockRotation = newValue;
        
        void SubscribeToNetworkVariables()
        {
            n_Speed.OnValueChanged += OnSpeedChanged;
            n_Course.OnValueChanged += OnCourseChanged;
            n_BlockRotation.OnValueChanged += OnBlockRotationChanged;
        }
        
        void UnsubscribeFromNetworkVariables()
        {
            n_Speed.OnValueChanged -= OnSpeedChanged;
            n_Course.OnValueChanged -= OnCourseChanged;
            n_BlockRotation.OnValueChanged -= OnBlockRotationChanged;
        }
    }
}
