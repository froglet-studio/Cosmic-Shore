using CosmicShore.Game.AI;
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
    public class R_ShipController : R_ShipBase, IShipHUDController
    {
        [SerializeField] bool isMultiplayerMode = false;

        [SerializeField] List<ImpactProperties> impactProperties;

        readonly NetworkVariable<float> n_Speed = new(writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Vector3> n_Course = new(writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Quaternion> n_BlockRotation = new(writePerm: NetworkVariableWritePermission.Owner);

        [SerializeField] private ShipHUDController _shipHUDController;
        [SerializeField] private AIPilot _aiPilot;

        void OnEnable()
        {
            if (!isMultiplayerMode || IsOwner)
                actionHandler.SubscribeEvents();

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
                actionHandler.UnsubscribeEvents();

            if (isMultiplayerMode && !IsOwner)
            {
                n_Speed.OnValueChanged -= OnSpeedChanged;
                n_Course.OnValueChanged -= OnCourseChanged;
                n_BlockRotation.OnValueChanged -= OnBlockRotationChanged;
            }
        }

        public override void OnNetworkSpawn()
        {
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

        public override void Initialize(IPlayer player)
        {
            SetPlayerToShipStatusAndSkimmers(player);
            SetTeamToShipStatusAndSkimmers(player.Team);

            actionHandler.Initialize(ShipStatus);
            impactHandler.Initialize(ShipStatus);
            customization.Initialize(ShipStatus);
            _aiPilot.Initialize(true);

            ShipStatus.ShipAnimation.Initialize(ShipStatus);
            ShipStatus.TrailSpawner.Initialize(ShipStatus);

            if (nearFieldSkimmer != null) nearFieldSkimmer.Initialize(this);
            if (farFieldSkimmer != null) farFieldSkimmer.Initialize(this);

            if (isMultiplayerMode)
            {
                if (IsOwner)
                {
                    if (!ShipStatus.FollowTarget) ShipStatus.FollowTarget = transform;

                    _shipHUDController.InitializeShipHUD(_shipType);
                    onBottomEdgeButtonsEnabled.RaiseEvent(true);
                    ShipStatus.AIPilot.Initialize(false);
                    ShipStatus.ShipCameraCustomizer.Initialize(this);
                    ShipStatus.ShipTransformer.Initialize(this);
                }

                ShipStatus.ShipTransformer.enabled = IsOwner;
                ShipStatus.TrailSpawner.ForceStartSpawningTrail();
                ShipStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(2f);
            }
            else
            {
                if (ShipStatus.FollowTarget == null) ShipStatus.FollowTarget = transform;
                onBottomEdgeButtonsEnabled.RaiseEvent(true);
                _shipHUDController.InitializeShipHUD(_shipType);
                ShipStatus.Silhouette.Initialize(this);
                ShipStatus.ShipTransformer.Initialize(this);
                ShipStatus.AIPilot.Initialize(ShipStatus.AIPilot.AutoPilotEnabled);
                ShipStatus.ShipCameraCustomizer.Initialize(this);
                ShipStatus.TrailSpawner.Initialize(ShipStatus);

                if (!ShipStatus.AutoPilotEnabled && ShipStatus.ShipHUDContainer != null)
                {
                    ShipStatus.ShipHUDView = ShipStatus.ShipHUDContainer.Show(_shipType);
                    ShipStatus.ShipHUDView?.Initialize(this);
                }
            }

            InvokeShipInitializedEvent();
        }

        void OnSpeedChanged(float previousValue, float newValue) => ShipStatus.Speed = newValue;
        void OnCourseChanged(Vector3 previousValue, Vector3 newValue) => ShipStatus.Course = newValue;
        void OnBlockRotationChanged(Quaternion previousValue, Quaternion newValue) => ShipStatus.blockRotation = newValue;

        public override void PerformButtonActions(int buttonNumber)
        {
            switch (buttonNumber)
            {
                case 1:
                    if (actionHandler != null && actionHandler.HasAction(InputEvents.Button1Action))
                        actionHandler.PerformShipControllerActions(InputEvents.Button1Action);
                    break;
                case 2:
                    if (actionHandler != null && actionHandler.HasAction(InputEvents.Button2Action))
                        actionHandler.PerformShipControllerActions(InputEvents.Button2Action);
                    break;
                case 3:
                    if (actionHandler != null && actionHandler.HasAction(InputEvents.Button3Action))
                        actionHandler.PerformShipControllerActions(InputEvents.Button3Action);
                    break;
            }
        }

        public void StopButtonActions(int buttonNumber)
        {
            switch (buttonNumber)
            {
                case 1:
                    if (actionHandler != null && actionHandler.HasAction(InputEvents.Button1Action))
                        actionHandler.StopShipControllerActions(InputEvents.Button1Action);
                    break;
                case 2:
                    if (actionHandler != null && actionHandler.HasAction(InputEvents.Button2Action))
                        actionHandler.StopShipControllerActions(InputEvents.Button2Action);
                    break;
                case 3:
                    if (actionHandler != null && actionHandler.HasAction(InputEvents.Button3Action))
                        actionHandler.StopShipControllerActions(InputEvents.Button3Action);
                    break;
            }
        }

        public void ToggleCollision(bool enabled)
        {
            foreach (var collider in GetComponentsInChildren<Collider>(true))
                collider.enabled = enabled;
        }

        public void FlipShipUpsideDown() => orientationHandle.transform.localRotation = Quaternion.Euler(0, 0, 180);
        public void FlipShipRightsideUp() => orientationHandle.transform.localRotation = Quaternion.Euler(0, 0, 0);
        public void OnButtonPressed(int buttonNumber) => PerformButtonActions(buttonNumber);
    }
}
