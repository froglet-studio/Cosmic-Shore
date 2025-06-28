using CosmicShore.Game;
using CosmicShore.Game.IO;
using CosmicShore.Game.Projectiles;
using CosmicShore.Models;
using CosmicShore.Models.Enums;
using CosmicShore.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;


namespace CosmicShore.Core
{
    [RequireComponent(typeof(ShipStatus))]
    public class R_LocalShip : R_ShipBase, IShipHUDController
    {
        [SerializeField] List<ImpactProperties> impactProperties;
        

        [SerializeField] internal GameObject shipHUD;

        public IShipHUDView ShipHUDView { get; private set; }

        public IInputStatus InputStatus => ShipStatus.InputController.InputStatus;

        private void OnEnable() => shipInput?.SubscribeEvents();

        private void OnDisable() => shipInput?.UnsubscribeEvents();

        public void Initialize(IPlayer player)
        {
            SetPlayerToShipStatusAndSkimmers(player);
            SetTeamToShipStatusAndSkimmers(player.Team);

            actionHandler?.Initialize(this);
            impactHandler?.Initialize(this);
            customization?.Initialize(this);

            if (ShipStatus.FollowTarget == null) ShipStatus.FollowTarget = transform;

            // TODO - Remove GameCanvas dependency
            onBottomEdgeButtonsEnabled.RaiseEvent(true);
            // if (bottomEdgeButtons) ShipStatus.Player.GameCanvas.MiniGameHUD.PositionButtonPanel(true);

            InitializeShipGeometries();
            shipInput?.Initialize(this);

            ShipStatus.Silhouette.Initialize(this);
            ShipStatus.ShipTransformer.Initialize(this);
            ShipStatus.ShipAnimation.Initialize(ShipStatus);
            //ShipStatus.AIPilot.Initialize(false);
            ShipStatus.AIPilot.Initialize(ShipStatus.AIPilot.AutoPilotEnabled);
            
            nearFieldSkimmer?.Initialize(this);
            farFieldSkimmer?.Initialize(this);
            ShipStatus.ShipCameraCustomizer.Initialize(this);
            ShipStatus.TrailSpawner.Initialize(ShipStatus);

            // if (ShipStatus.AIPilot.AutoPilotEnabled) return;
            Debug.Log($"<color=blue> Ai Pilot value {ShipStatus.AutoPilotEnabled}");
            if (!ShipStatus.AutoPilotEnabled /*&& !ShipStatus.AIPilot*/)
            {
                if (SceneManager.GetActiveScene().name == "Menu_Main")  // this is a temp feature we will be changing this later
                {
                    return;
                }

                Debug.Log("Showing UI for player");
                if (shipHUD != null)
                {
                    shipHUD.TryGetComponent(out ShipHUDContainer container);
                    var hudView = container.Show(_shipType);
                    hudView?.Initialize(this);
                    ShipHUDView = hudView;
                }
            }
            /*if (shipHUD)
            {
                shipHUD.SetActive(true);
                foreach (var child in shipHUD.GetComponentsInChildren<Transform>(false))
                {
                    child.SetParent(ShipStatus.Player.GameCanvasTransform, false);
                    child.SetSiblingIndex(0);   // Don't draw on top of modal screens
                }
            }*/

            OnShipInitialized?.Invoke(ShipStatus);
        }

        


        // this is used with buttons so "Find all references" will not return editor usage
        public void PerformButtonActions(int buttonNumber)
        {
            Debug.Log($"Ship.PerformButtonActions - buttonNumber:{buttonNumber}");
            
            switch (buttonNumber)
            {
                case 1:
                    if(shipInput != null && shipInput.HasAction(InputEvents.Button1Action))
                        shipInput.PerformShipControllerActions(InputEvents.Button1Action);
                    break;
                case 2:
                    if(shipInput != null && shipInput.HasAction(InputEvents.Button2Action))
                        shipInput.PerformShipControllerActions(InputEvents.Button2Action);
                    break;
                case 3:
                    if(shipInput != null && shipInput.HasAction(InputEvents.Button3Action))
                        shipInput.PerformShipControllerActions(InputEvents.Button3Action);
                    break;
                default:
                    Debug.LogWarning($"Ship.PerformButtonActions - buttonNumber:{buttonNumber} is not associated to any of the ship actions.");
                    break;
            }
        }

        // this is used with buttons so "Find all references" will not return editor usage
        public void StopButtonActions(int buttonNumber)
        {
            Debug.Log($"Ship.StopButtonActions - buttonNumber:{buttonNumber}");

            switch (buttonNumber)
            {
                case 1:
                    if(shipInput != null && shipInput.HasAction(InputEvents.Button1Action))
                        shipInput.StopShipControllerActions(InputEvents.Button1Action);
                    break;
                case 2:
                    if(shipInput != null && shipInput.HasAction(InputEvents.Button2Action))
                        shipInput.StopShipControllerActions(InputEvents.Button2Action);
                    break;
                case 3:
                    if(shipInput != null && shipInput.HasAction(InputEvents.Button3Action))
                        shipInput.StopShipControllerActions(InputEvents.Button3Action);
                    break;
                default:
                    Debug.LogWarning($"Ship.StopButtonActions - buttonNumber:{buttonNumber} is not associated to any of the ship actions.");
                    break;
            }
        }


        public void ToggleCollision(bool enabled)
        {
            foreach (var collider in GetComponentsInChildren<Collider>(true))
                collider.enabled = enabled;
        }

        public void FlipShipUpsideDown() // TODO: move to shipController
        {
            orientationHandle.transform.localRotation = Quaternion.Euler(0, 0, 180);
        }

        public void FlipShipRightsideUp()
        {
            orientationHandle.transform.localRotation = Quaternion.Euler(0, 0, 0);
        }

        public void OnButtonPressed(int buttonNumber)
        {
            PerformButtonActions(buttonNumber);
        }
    }

}
