using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Combines behaviour of R_LocalShip and R_NetworkShip. Behaviour is
    /// selected at runtime based on <see cref="isMultiplayerMode"/>.
    /// </summary>
    [RequireComponent(typeof(IShipStatus))]
    public class R_ShipController : R_ShipBase
    {

        [SerializeField] List<ImpactProperties> impactProperties;

        readonly NetworkVariable<float> n_Speed = new(writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Vector3> n_Course = new(writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Quaternion> n_BlockRotation = new(writePerm: NetworkVariableWritePermission.Owner);
        
        bool isMultiplayerMode = false;

        void OnEnable()
        {
            if (!isMultiplayerMode || IsOwner)
                ShipStatus.ActionHandler.SubscribeEvents();

            if (isMultiplayerMode && !IsOwner)
            {
                n_Speed.OnValueChanged += OnSpeedChanged;
                n_Course.OnValueChanged += OnCourseChanged;
                n_BlockRotation.OnValueChanged += OnBlockRotationChanged;
            }
        }

        void OnDisable()
        {
            if (!isMultiplayerMode || IsOwner)
                ShipStatus.ActionHandler.UnsubscribeEvents();

            if (isMultiplayerMode && !IsOwner)
            {
                n_Speed.OnValueChanged -= OnSpeedChanged;
                n_Course.OnValueChanged -= OnCourseChanged;
                n_BlockRotation.OnValueChanged -= OnBlockRotationChanged;
            }
        }

        public override void OnNetworkSpawn()
        {
            isMultiplayerMode = true;
            
            if (!isMultiplayerMode) return;
            if (!IsOwner)
            {
                n_Speed.OnValueChanged += OnSpeedChanged;
                n_Course.OnValueChanged += OnCourseChanged;
                n_BlockRotation.OnValueChanged += OnBlockRotationChanged;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (!isMultiplayerMode) return;
            if (!IsOwner)
            {
                n_Speed.OnValueChanged -= OnSpeedChanged;
                n_Course.OnValueChanged -= OnCourseChanged;
                n_BlockRotation.OnValueChanged -= OnBlockRotationChanged;
            }
            
            isMultiplayerMode = false;
        }

        void Update()
        {
            if (!isMultiplayerMode) return;
            if (IsOwner)
            {
                n_Speed.Value = ShipStatus.Speed;
                n_Course.Value = ShipStatus.Course;
                n_BlockRotation.Value = ShipStatus.blockRotation;
            }
        }

        public override void Initialize(IPlayer player, bool enableAIPilot)
        {
            ShipStatus.Player = player;

            ShipStatus.ActionHandler.Initialize(ShipStatus);
            ShipStatus.ImpactHandler.Initialize(ShipStatus);
            ShipStatus.Customization.Initialize(ShipStatus);
            ShipStatus.ShipAnimation.Initialize(ShipStatus);
            ShipStatus.TrailSpawner.Initialize(ShipStatus);

            if (ShipStatus.NearFieldSkimmer != null) 
                ShipStatus.NearFieldSkimmer.Initialize(ShipStatus);

            if (ShipStatus.FarFieldSkimmer != null) 
                ShipStatus.FarFieldSkimmer.Initialize(ShipStatus);

            if (isMultiplayerMode)
            {
                if (IsOwner)
                {
                    if (!ShipStatus.FollowTarget) 
                        ShipStatus.FollowTarget = transform;

                    ShipStatus.ShipHUDController.InitializeShipHUD(ShipStatus.ShipType);
                    ShipStatus.ShipCameraCustomizer.Initialize(this);
                    ShipStatus.ShipTransformer.Initialize(this);
                    onBottomEdgeButtonsEnabled.RaiseEvent(true);
                }

                ShipStatus.ShipTransformer.enabled = IsOwner;
                ShipStatus.TrailSpawner.ForceStartSpawningTrail();
                ShipStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(2f);
            }
            else
            {
                if (ShipStatus.FollowTarget == null) 
                    ShipStatus.FollowTarget = transform;

                ShipStatus.Silhouette.Initialize(this);
                ShipStatus.ShipTransformer.Initialize(this);
                ShipStatus.ShipHUDController.InitializeShipHUD(ShipStatus.ShipType);
                onBottomEdgeButtonsEnabled.RaiseEvent(true);
                ShipStatus.ShipCameraCustomizer.Initialize(this);
                ShipStatus.TrailSpawner.Initialize(ShipStatus);                
            }

            // TODO - Currently AIPilot's update should run only after SingleStickShipTransformer
            // sets SingleStickControls to true/false. Try finding a solution to remove this
            // sequential dependency.
            ShipStatus.AIPilot.Initialize(enableAIPilot, this);

            InvokeShipInitializedEvent();
        }

        void OnSpeedChanged(float previousValue, float newValue) => ShipStatus.Speed = newValue;
        void OnCourseChanged(Vector3 previousValue, Vector3 newValue) => ShipStatus.Course = newValue;
        void OnBlockRotationChanged(Quaternion previousValue, Quaternion newValue) => ShipStatus.blockRotation = newValue;

        public override void PerformButtonActions(int buttonNumber)
        {
            Debug.Log($"[R_ShipController] PerformButtonActions({buttonNumber}) called!");
            switch (buttonNumber)
            {
                case 1:
                    if (ShipStatus.ActionHandler != null && ShipStatus.ActionHandler.HasAction(InputEvents.Button1Action))
                        ShipStatus.ActionHandler.PerformShipControllerActions(InputEvents.Button1Action);
                    break;
                case 2:
                    if (ShipStatus.ActionHandler != null && ShipStatus.ActionHandler.HasAction(InputEvents.Button2Action))
                        ShipStatus.ActionHandler.PerformShipControllerActions(InputEvents.Button2Action);
                    break;
                case 3:
                    if (ShipStatus.ActionHandler != null && ShipStatus.ActionHandler.HasAction(InputEvents.Button3Action))
                        ShipStatus.ActionHandler.PerformShipControllerActions(InputEvents.Button3Action);
                    break;
            }
        }

        public void StopButtonActions(int buttonNumber)
        {
            switch (buttonNumber)
            {
                case 1:
                    if (ShipStatus.ActionHandler != null && ShipStatus.ActionHandler.HasAction(InputEvents.Button1Action))
                        ShipStatus.ActionHandler.StopShipControllerActions(InputEvents.Button1Action);
                    break;
                case 2:
                    if (ShipStatus.ActionHandler != null && ShipStatus.ActionHandler.HasAction(InputEvents.Button2Action))
                        ShipStatus.ActionHandler.StopShipControllerActions(InputEvents.Button2Action);
                    break;
                case 3:
                    if (ShipStatus.ActionHandler != null && ShipStatus.ActionHandler.HasAction(InputEvents.Button3Action))
                        ShipStatus.ActionHandler.StopShipControllerActions(InputEvents.Button3Action);
                    break;
            }
        }

        public void ToggleCollision(bool enabled)
        {
            foreach (var collider in GetComponentsInChildren<Collider>(true))
                collider.enabled = enabled;
        }

        public void FlipShipUpsideDown() => ShipStatus.OrientationHandle.transform.localRotation = Quaternion.Euler(0, 0, 180);
        public void FlipShipRightsideUp() => ShipStatus.OrientationHandle.transform.localRotation = Quaternion.Euler(0, 0, 0);
    }
}
