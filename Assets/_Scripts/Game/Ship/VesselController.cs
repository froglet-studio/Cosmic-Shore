using System;
using CosmicShore.Models.Enums;
using Obvious.Soap;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Combines behaviour of R_LocalShip and R_NetworkShip. Behaviour is
    /// selected at runtime based on <see cref="isMultiplayerMode"/>.
    /// </summary>
    [RequireComponent(typeof(IVesselStatus))]
    public class VesselController : NetworkBehaviour, IVessel
    {
        public event Action<IVesselStatus> OnShipInitialized;
        
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
        
        readonly NetworkVariable<float> n_Speed = new(writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Vector3> n_Course = new(writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Quaternion> n_BlockRotation = new(writePerm: NetworkVariableWritePermission.Owner);
        
        bool isMultiplayerMode = false;

        void OnEnable()
        {
            if (isMultiplayerMode && !IsOwner)
            {
                n_Speed.OnValueChanged += OnSpeedChanged;
                n_Course.OnValueChanged += OnCourseChanged;
                n_BlockRotation.OnValueChanged += OnBlockRotationChanged;
            }
            RefreshOwnershipFlag();
        }

        void OnDisable()
        {
            if (isMultiplayerMode && !IsOwner)
            {
                n_Speed.OnValueChanged -= OnSpeedChanged;
                n_Course.OnValueChanged -= OnCourseChanged;
                n_BlockRotation.OnValueChanged -= OnBlockRotationChanged;
            }
            RefreshOwnershipFlag();
        }

        public override void OnNetworkSpawn()
        {
            isMultiplayerMode = true;
            
            RefreshOwnershipFlag();

            if (IsOwner) 
                return;
            
            n_Speed.OnValueChanged += OnSpeedChanged;
            n_Course.OnValueChanged += OnCourseChanged;
            n_BlockRotation.OnValueChanged += OnBlockRotationChanged;
        }

        public override void OnNetworkDespawn()
        {
            isMultiplayerMode = false;
            RefreshOwnershipFlag();

            if (IsOwner) 
                return;
            
            n_Speed.OnValueChanged -= OnSpeedChanged;
            n_Course.OnValueChanged -= OnCourseChanged;
            n_BlockRotation.OnValueChanged -= OnBlockRotationChanged;
        }

        void Update()
        {
            if (!isMultiplayerMode || !IsOwner) 
                return;
            
            n_Speed.Value = VesselStatus.Speed;
            n_Course.Value = VesselStatus.Course;
            n_BlockRotation.Value = VesselStatus.blockRotation;
        }

        public void Initialize(IPlayer player, bool enableAIPilot)
        {
            VesselStatus.Player = player;
            VesselStatus.ShipAnimation.Initialize(VesselStatus);
            VesselStatus.TrailSpawner.Initialize(VesselStatus);
            RefreshOwnershipFlag();
            
            if (isMultiplayerMode)
            {
                InitializeForMultiplayerMode();
            }
            else
            {
                InitializeForSinglePlayerMode(enableAIPilot);
            }
            
            OnShipInitialized?.Invoke(VesselStatus);
        }

        public void PerformButtonActions(int buttonNumber)
        {
            InputEvents controlType;
            switch (buttonNumber)
            {
                case 1:
                    controlType = InputEvents.Button1Action;
                    break;
                case 2:
                    controlType = InputEvents.Button2Action;
                    break;
                case 3:
                    controlType = InputEvents.Button3Action;
                    break;
                default:
                    controlType = InputEvents.Button1Action;
                    break;
            }
            PerformShipControllerActions(controlType);
        }

        public void StopButtonActions(int buttonNumber)
        {
            InputEvents controlType;
            switch (buttonNumber)
            {
                case 1:
                    controlType = InputEvents.Button1Action;
                    break;
                case 2:
                    controlType = InputEvents.Button2Action;
                    break;
                case 3:
                    controlType = InputEvents.Button3Action;
                    break;
                default:
                    controlType = InputEvents.Button1Action;
                    break;
            }
            StopShipControllerActions(controlType);
        }

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

        public void OnButtonPressed(int buttonNumber)
        {
            throw new NotImplementedException();
        }

        public void ToggleAutoPilot(bool toggle)
        {
            if (toggle)
                VesselStatus.AIPilot.StartAIPilot();
            else
                VesselStatus.AIPilot.StopAIPilot();
        }

        public void Destroy()
        {
            Destroy(gameObject);
        }
        
        void InitializeForMultiplayerMode()
        {
            if (IsOwner)
            {
                if (!VesselStatus.CameraFollowTarget) 
                    VesselStatus.CameraFollowTarget = transform;
                VesselStatus.ShipCameraCustomizer.Initialize(this);
                    
                // TODO - Temp disabled, for testing.
                /*VesselStatus.ActionHandler.Initialize(VesselStatus);
                VesselStatus.Customization.Initialize(VesselStatus);

                if (VesselStatus.NearFieldSkimmer)
                    VesselStatus.NearFieldSkimmer.Initialize(VesselStatus);

                if (VesselStatus.FarFieldSkimmer)
                    VesselStatus.FarFieldSkimmer.Initialize(VesselStatus);
                */
                    
                VesselStatus.VesselTransformer.Initialize(this);
                VesselStatus.ShipHUDController.Initialize(VesselStatus, VesselStatus.ShipHudView);
                VesselStatus.ResourceSystem.Reset();
                VesselStatus.VesselTransformer.ResetShipTransformer();
                    
                onBottomEdgeButtonsEnabled.Raise(true);
            }
        }
        
        void InitializeForSinglePlayerMode(bool enableAIPilot)
        {
            VesselStatus.ActionHandler.Initialize(VesselStatus);
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
                VesselStatus.ShipHUDController.Initialize(VesselStatus, VesselStatus.ShipHudView);
                VesselStatus.ShipCameraCustomizer.Initialize(this);
            }
                
            VesselStatus.TrailSpawner.Initialize(VesselStatus);  
            
            // TODO - Currently AIPilot's update should run only after SingleStickVesselTransformer
            // sets SingleStickControls to true/false. Try finding a solution to remove this
            // sequential dependency.
            VesselStatus.AIPilot.Initialize(enableAIPilot, this);
            
            // TODO -> Remove the below TrailSpawner method execution if not needed.
            // Possibly this can be done when Players are activated for the game in GameDataSO
            /*VesselStatus.TrailSpawner.ForceStartSpawningTrail();
            VesselStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(2f);*/
            
            onBottomEdgeButtonsEnabled.Raise(true);
        }

        void OnSpeedChanged(float previousValue, float newValue) => VesselStatus.Speed = newValue;
        void OnCourseChanged(Vector3 previousValue, Vector3 newValue) => VesselStatus.Course = newValue;
        void OnBlockRotationChanged(Quaternion previousValue, Quaternion newValue) => VesselStatus.blockRotation = newValue;
        
        // helper
        void RefreshOwnershipFlag()
        {
            var value = !isMultiplayerMode || IsOwner;
            if (VesselStatus is VesselStatus concrete) concrete.SetIsOwnerForControllerOnly(value);
        }

    }
}
