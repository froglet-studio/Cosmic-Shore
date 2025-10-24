using CosmicShore.Core;
using CosmicShore.Game.AI;
using CosmicShore.Game.Animation;
using CosmicShore.Game.IO;
using System.Collections.Generic;
using CosmicShore.Utilities;
using UnityEngine;

namespace CosmicShore.Game
{
    public interface IVesselStatus
    {
        IVessel Vessel { get; } // FOR TEMP USE, TRY TO REMOVE SHIP REFERENCE FROM OTHER SYSTEMS
        Transform Transform => Vessel.Transform;
        AIPilot AIPilot { get; }
        bool IsInitializedAsAI => Player.IsInitializedAsAI;
        bool AutoPilotEnabled => AIPilot.AutoPilotEnabled;
        bool AlignmentEnabled { get; set; }
        Material AOEConicExplosionMaterial { get; set; }
        Material AOEExplosionMaterial { get; set; }
        bool Attached { get; set; }
        Prism AttachedPrism { get; set; }
        Quaternion blockRotation { get; set; }
        bool Boosting { get; set; }
        float BoostMultiplier { get; set; }
        CameraManager CameraManager { get; set; }
        SO_Captain Captain { get; set; }
        float ChargedBoostCharge { get; set; }
        bool ChargedBoostDischarging { get; set; }
        Vector3 Course { get; set; }
        bool Drifting { get; set; }
        Transform CameraFollowTarget { get; set; }
        bool GunsActive { get; set; }
        InputController InputController => Player.InputController;
        IInputStatus InputStatus => Player.InputStatus;
        bool LiveProjectiles { get; set; }
        bool Overheating { get; set; }
        IPlayer Player { get; set; }
        string PlayerName
        {
            get
            {
                if (Player != null)
                    return Player.Name;

                Debug.LogWarning("Player is null, returning empty string for PlayerName.");
                return "No-name";
            }
        }

        Domains Domain
        {
            get
            {
                if (Player == null)
                {
                    Debug.LogError("No Player found to get domain!");
                    return Domains.Jade;
                }
                return Player.Domain;
            }
        }

        bool Portrait { get; set; }
        ResourceSystem ResourceSystem { get; }
        VesselAnimation VesselAnimation { get; }
        VesselCameraCustomizer VesselCameraCustomizer { get; }
        List<GameObject> ShipGeometries { get; set; }
        Transform ShipTransform { get;}
        VesselTransformer VesselTransformer { get; }
        string Name { get; }
        VesselClassType VesselType { get; }
        Skimmer NearFieldSkimmer { get; }
        Skimmer FarFieldSkimmer { get; }
        GameObject OrientationHandle { get; }
        Silhouette Silhouette { get; }
        Material ShipMaterial { get; set; }
        Material SkimmerMaterial { get; set; }
        float Speed { get; set; }
        bool SingleStickControls { get; set; }
        bool Slowed { get; set; }
        bool IsStationary { get; set; }
        bool IsTranslationRestricted { get; set; }
        bool Turret { get; set; }
        VesselPrismController VesselPrismController { get; }
        ShipHUDContainer ShipHUDContainer { get; }
        IVesselHUDView ShipHUDView { get; set; }
        IVesselHUDController ShipHUDController { get; }
        VesselCustomization Customization { get; }
        R_VesselActionHandler ActionHandler { get; }
        VesselHUDView VesselHUDView { get; set; }
        R_ShipElementStatsHandler ElementalStatsHandler { get; }
        bool IsOwnerClient { get; }  
        void ResetForPlay();
    }
}