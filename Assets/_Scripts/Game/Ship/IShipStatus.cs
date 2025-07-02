﻿using CosmicShore.Game;
using CosmicShore.Game.AI;
using CosmicShore.Game.Animation;
using CosmicShore.Game.IO;
using CosmicShore.Core;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Core          // Change it to CosmicShore.Game
{
    public interface IShipStatus
    {
        
        IShip Ship { get; } // FOR TEMP USE, TRY REMOVE SHIP REFERENCE FROM OTHER SYSTEMS

        AIPilot AIPilot { get; }
        bool AlignmentEnabled { get; set; }
        Material AOEConicExplosionMaterial { get; set; }
        Material AOEExplosionMaterial { get; set; }
        bool Attached { get; set; }
        TrailBlock AttachedTrailBlock { get; set; }
        bool AutoPilotEnabled { get; set; }
        Quaternion blockRotation { get; set; }
        bool Boosting { get; set; }
        float BoostMultiplier { get; set; }
        CustomCameraController CameraController { get; set; }
        SO_Captain Captain { get; set; }
        float ChargedBoostCharge { get; set; }
        bool ChargedBoostDischarging { get; set; }
        bool CommandStickControls { get; set; }
        Vector3 Course { get; set; }
        bool Drifting { get; set; }
        bool ElevatedResourceGain { get; set; }
        Transform FollowTarget { get; set; }
        float GetInertia { get; set; }
        bool GunsActive { get; set; }
        InputController InputController { get; }
        IInputStatus InputStatus { get; }
        bool LiveProjectiles { get; set; }
        string Name { get; set; }
        bool Overheating { get; set; }
        IPlayer Player { get; set; }
        string PlayerName
        {
            get
            {
                if (Player != null)
                    return Player.PlayerName;

                Debug.LogWarning("Player is null, returning empty string for PlayerName.");
                return "No-name";
            }
        }

        bool Portrait { get; set; }
        ResourceSystem ResourceSystem { get; }
        ShipAnimation ShipAnimation { get; }
        ShipCameraCustomizer ShipCameraCustomizer { get; }
        List<GameObject> ShipGeometries { get; set; }
        Transform ShipTransform { get; set; }
        ShipTransformer ShipTransformer { get; }
        ShipTypes ShipType { get; set; }
        Silhouette Silhouette { get; }
        bool SingleStickControls { get; set; }
        Material SkimmerMaterial { get; set; }
        bool Slowed { get; set; }
        float Speed { get; set; }
        bool Stationary { get; set; }
        Teams Team { get; set; }
        TrailSpawner TrailSpawner { get; }
        bool Turret { get; set; }

        void Reset();
    }
}