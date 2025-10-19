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
        
        [Header("Event Channels")]
        [SerializeField] protected ScriptableEventBool onBottomEdgeButtonsEnabled;
        
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

        void OnEnable()
        {
            if (IsSpawned && !IsOwner)
            {
                n_Speed.OnValueChanged += OnSpeedChanged;
                n_Course.OnValueChanged += OnCourseChanged;
                n_BlockRotation.OnValueChanged += OnBlockRotationChanged;
            }
        }

        void OnDisable()
        {
            if (IsSpawned && !IsOwner)
            {
                n_Speed.OnValueChanged -= OnSpeedChanged;
                n_Course.OnValueChanged -= OnCourseChanged;
                n_BlockRotation.OnValueChanged -= OnBlockRotationChanged;
            }
        }

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
            
            n_Speed.OnValueChanged += OnSpeedChanged;
            n_Course.OnValueChanged += OnCourseChanged;
            n_BlockRotation.OnValueChanged += OnBlockRotationChanged;
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner) 
                return;
            
            n_Speed.OnValueChanged -= OnSpeedChanged;
            n_Course.OnValueChanged -= OnCourseChanged;
            n_BlockRotation.OnValueChanged -= OnBlockRotationChanged;
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
            VesselStatus.ShipAnimation.Initialize(VesselStatus);
            VesselStatus.VesselPrismController.Initialize(VesselStatus);
            
            if (IsSpawned)
            {
                InitializeForMultiplayerMode();
            }
            else
            {
                InitializeForSinglePlayerMode(enableAIPilot);
            }
            
            OnInitialized?.Invoke();
        }
        
        /*public void PerformButtonActions(int buttonNumber)
        {
            if (VesselStatus.AutoPilotEnabled) return;

            var controlType = buttonNumber switch
            {
                1 => InputEvents.Button1Action,
                2 => InputEvents.Button2Action,
                3 => InputEvents.Button3Action,
                _ => InputEvents.Button1Action
            };
            PerformShipControllerActions(controlType);
        }

        public void StopButtonActions(int buttonNumber)
        {
            if (VesselStatus.AutoPilotEnabled) return;

            var controlType = buttonNumber switch
            {
                1 => InputEvents.Button1Action,
                2 => InputEvents.Button2Action,
                3 => InputEvents.Button3Action,
                _ => InputEvents.Button1Action
            };
            StopShipControllerActions(controlType);
        }*/

        public void FlipShipUpsideDown() => VesselStatus.OrientationHandle.transform.localRotation = Quaternion.Euler(0, 0, 180);
        public void FlipShipRightsideUp() => VesselStatus.OrientationHandle.transform.localRotation = Quaternion.Euler(0, 0, 0);
        
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

        public void SetBlockSilhouettePrefab(GameObject prefab) =>
            VesselStatus.ShipHUDController.SetBlockPrefab(prefab);

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

        public void ResetForPlay()
        {
            VesselStatus.ResetForPlay();
        }

        public void SetPose(Pose pose) => 
            VesselStatus.VesselTransformer.SetPose(pose);

        public void ChangePlayer(IPlayer player)
        {
            VesselStatus.ShipHUDController.TearDown();
            VesselStatus.Player = player;

            if (player.IsInitializedAsAI) return;
            VesselStatus.ShipHUDController.ReInitialize(VesselStatus, VesselStatus.VesselHUDView);
            VesselStatus.VesselCameraCustomizer.RetargetAndApply(this);
        }

        void InitializeForMultiplayerMode()
        {
            if (IsOwner)
            {
                if (!VesselStatus.CameraFollowTarget) 
                    VesselStatus.CameraFollowTarget = transform;
                VesselStatus.VesselCameraCustomizer.Initialize(this);

                if (VesselStatus.NearFieldSkimmer)
                    VesselStatus.NearFieldSkimmer.Initialize(VesselStatus);

                if (VesselStatus.FarFieldSkimmer)
                    VesselStatus.FarFieldSkimmer.Initialize(VesselStatus);
                    
                VesselStatus.VesselTransformer.Initialize(this);
                VesselStatus.ShipHUDController.Initialize(VesselStatus, VesselStatus.VesselHUDView);
                VesselStatus.ResetForPlay();
                    
                onBottomEdgeButtonsEnabled.Raise(true);
            }
            
            VesselStatus.Customization.Initialize(VesselStatus);
            VesselStatus.ActionHandler.Initialize(VesselStatus, IsOwner);
        }
        
        void InitializeForSinglePlayerMode(bool enableAIPilot)
        {
            VesselStatus.ActionHandler.Initialize(VesselStatus, !VesselStatus.IsInitializedAsAI);
            VesselStatus.Customization.Initialize(VesselStatus);

            if (VesselStatus.NearFieldSkimmer) 
                VesselStatus.NearFieldSkimmer.Initialize(VesselStatus);

            if (VesselStatus.FarFieldSkimmer) 
                VesselStatus.FarFieldSkimmer.Initialize(VesselStatus);
                
            if (VesselStatus.CameraFollowTarget == null) 
                VesselStatus.CameraFollowTarget = transform;

            VesselStatus.Silhouette.Initialize(this);
            VesselStatus.VesselTransformer.Initialize(this);
                
            if (!enableAIPilot)
            {
                VesselStatus.ShipHUDController.Initialize(VesselStatus, VesselStatus.VesselHUDView);
                VesselStatus.VesselCameraCustomizer.Initialize(this);
            }
            
            // TODO - Currently AIPilot's update should run only after SingleStickVesselTransformer
            // sets SingleStickControls to true/false. Try finding a solution to remove this
            // sequential dependency.
            /// AIPilot will be initialized both in User controlled / AI Vessels
            /// Multiplayer modes will also have auto-pilot initialized
            
            VesselStatus.AIPilot.Initialize(enableAIPilot, this);
            VesselStatus.ResetForPlay();
            
            onBottomEdgeButtonsEnabled.Raise(true);
        }

        void OnSpeedChanged(float previousValue, float newValue) => VesselStatus.Speed = newValue;
        void OnCourseChanged(Vector3 previousValue, Vector3 newValue) => VesselStatus.Course = newValue;
        void OnBlockRotationChanged(Quaternion previousValue, Quaternion newValue) => VesselStatus.blockRotation = newValue;
    }
}
