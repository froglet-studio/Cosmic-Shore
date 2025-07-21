using CosmicShore.Core;
using CosmicShore.Game.AI;
using CosmicShore.Game.Animation;
using CosmicShore.Game.IO;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game
{
    public interface IShipStatus
    {
        IShip Ship { get; } // FOR TEMP USE, TRY TO REMOVE SHIP REFERENCE FROM OTHER SYSTEMS
        Transform Transform => Ship.Transform;
        AIPilot AIPilot { get; }
        bool AlignmentEnabled { get; set; }
        Material AOEConicExplosionMaterial { get; set; }
        Material AOEExplosionMaterial { get; set; }
        bool Attached { get; set; }
        TrailBlock AttachedTrailBlock { get; set; }
        bool AutoPilotEnabled { get; }
        Quaternion blockRotation { get; set; }
        bool Boosting { get; set; }
        float BoostMultiplier { get; set; }
        CameraManager CameraManager { get; set; }
        SO_Captain Captain { get; set; }
        float ChargedBoostCharge { get; set; }
        bool ChargedBoostDischarging { get; set; }
        Vector3 Course { get; set; }
        bool Drifting { get; set; }
        bool ElevatedResourceGain { get; set; }
        Transform FollowTarget { get; set; }
        float GetInertia { get; set; }
        bool GunsActive { get; set; }
        InputController InputController { get; }
        IInputStatus InputStatus { get; }
        bool LiveProjectiles { get; set; }
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
        Teams Team => Player.Team;

        bool Portrait { get; set; }
        ResourceSystem ResourceSystem { get; }
        ShipAnimation ShipAnimation { get; }
        ShipCameraCustomizer ShipCameraCustomizer { get; }
        List<GameObject> ShipGeometries { get; set; }
        Transform ShipTransform { get;}
        ShipTransformer ShipTransformer { get; }
        string Name { get; }
        ShipClassType ShipType { get; }
        Skimmer NearFieldSkimmer { get; }
        Skimmer FarFieldSkimmer { get; }
        GameObject OrientationHandle { get; }
        Silhouette Silhouette { get; }
        Material ShipMaterial { get; set; }
        Material SkimmerMaterial { get; set; }
        float Speed { get; set; }
        bool SingleStickControls { get; set; }
        bool Slowed { get; set; }
        bool Stationary { get; set; }
        bool Turret { get; set; }
        TrailSpawner TrailSpawner { get; }
        ShipHUDContainer ShipHUDContainer { get; }
        IShipHUDView ShipHUDView { get; set; }
        IShipHUDController ShipHUDController { get; }
        R_ShipCustomization Customization { get; }
        R_ShipActionHandler ActionHandler { get; }
        R_ShipImpactHandler ImpactHandler { get; }
        R_ShipElementStatsHandler ElementalStatsHandler { get; }

        void ResetValues();
    }
}