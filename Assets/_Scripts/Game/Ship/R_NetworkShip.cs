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
    [RequireComponent(typeof(ShipStatus))]
    public class R_NetworkShip : R_ShipBase
    {
        #region Public Properties

        #endregion

        NetworkVariable<float> n_Speed = new(writePerm: NetworkVariableWritePermission.Owner);
        NetworkVariable<Vector3> n_Course = new(writePerm: NetworkVariableWritePermission.Owner);
        NetworkVariable<Quaternion> n_BlockRotation = new(writePerm: NetworkVariableWritePermission.Owner);

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
                shipInput?.SubscribeEvents();
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
                shipInput?.UnsubscribeEvents();
            }
        }

        public void Initialize(IPlayer player)
        {
            ShipStatus.Player = player;

            SetPlayerToShipStatusAndSkimmers(player);
            SetTeamToShipStatusAndSkimmers(player.Team);

            actionHandler?.Initialize(this);
            impactHandler?.Initialize(this);
            customization?.Initialize(this);

            InitializeShipGeometries();

            ShipStatus.ShipAnimation.Initialize(ShipStatus);
            ShipStatus.TrailSpawner.Initialize(ShipStatus);

            if (nearFieldSkimmer != null)
                nearFieldSkimmer.Initialize(this);

            if (farFieldSkimmer != null)
                farFieldSkimmer.Initialize(this);
            

            if (IsOwner)
            {
                if (!ShipStatus.FollowTarget) ShipStatus.FollowTarget = transform;

                // TODO - Remove GameCanvas dependency
                onBottomEdgeButtonsEnabled.RaiseEvent(true);
                // if (_bottomEdgeButtons) ShipStatus.Player.GameCanvas.MiniGameHUD.PositionButtonPanel(true);

                shipInput?.Initialize(this);

                ShipStatus.AIPilot.Initialize(false);
                ShipStatus.ShipCameraCustomizer.Initialize(this);
                ShipStatus.ShipTransformer.Initialize(this);
            }

            ShipStatus.ShipTransformer.enabled = IsOwner;
            ShipStatus.TrailSpawner.ForceStartSpawningTrail();
            ShipStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(2f);

            OnShipInitialized?.Invoke(ShipStatus);
        }

        public void PerformShipControllerActions(InputEvents @event) => shipInput?.PerformShipControllerActions(@event);

        public void StopShipControllerActions(InputEvents @event) => shipInput?.StopShipControllerActions(@event);



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

        //
        // Attach and Detach
        //
        void Attach(TrailBlock trailBlock)
        {
            if (trailBlock.Trail != null)
            {
                ShipStatus.Attached = true;
                ShipStatus.AttachedTrailBlock = trailBlock;
            }
        }
    }

}
