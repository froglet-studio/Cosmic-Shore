using CosmicShore.Gameplay;
using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
namespace CosmicShore.Gameplay
{
    public interface IVesselStatus
    {
        IVessel Vessel { get; } // FOR TEMP USE, TRY TO REMOVE SHIP REFERENCE FROM OTHER SYSTEMS
        Transform Transform => Vessel.Transform;

        AIPilot AIPilot { get; }
        AICinematicBehavior AICinematicBehavior { get; }
        bool IsInitializedAsAI => Player.IsInitializedAsAI;
        bool AutoPilotEnabled => AIPilot.AutoPilotEnabled;

        bool AlignmentEnabled { get; set; }

        Material AOEConicExplosionMaterial { get; set; }
        Material AOEExplosionMaterial { get; set; }

        bool IsAttached { get; set; }
        Prism AttachedPrism { get; set; }

        Quaternion blockRotation { get; set; }

        bool IsBoosting { get; set; }
        float BoostMultiplier { get; set; }

        float Inertia { get; }

        float ChargedBoostCharge { get; set; }
        bool IsChargedBoostDischarging { get; set; }

        Vector3 Course { get; set; }
        bool IsDrifting { get; set; }

        Transform CameraFollowTarget { get; set; }

        bool GunsActive { get; set; }

        InputController InputController => Player.InputController;
        IInputStatus InputStatus => Player.InputStatus;

        bool HasLiveProjectiles { get; set; }
        bool IsOverheating { get; set; }

        IPlayer Player { get; set; }

        /// <summary>
        /// Local User in singleplayer is the player providing input, not AI.
        /// In Multiplayer, it is the Owner Client providing input.
        /// </summary>
        bool IsLocalUser => Player.IsLocalUser;

        string PlayerName { get; }

        Domains Domain { get; }

        bool IsPortrait { get; set; }

        ResourceSystem ResourceSystem { get; }
        VesselAnimation VesselAnimation { get; }
        VesselCameraCustomizer VesselCameraCustomizer { get; }

        List<GameObject> ShipGeometries { get; set; }
        Transform ShipTransform { get; }

        VesselTransformer VesselTransformer { get; }

        string Name { get; }
        VesselClassType VesselType { get; }

        Skimmer NearFieldSkimmer { get; }
        Skimmer FarFieldSkimmer { get; }

        GameObject OrientationHandle { get; }

        SilhouetteController Silhouette { get; }

        Material ShipMaterial { get; set; }
        Material SkimmerMaterial { get; set; }

        float Speed { get; set; }

        bool IsSingleStickControls { get; set; }
        bool IsSlowed { get; set; }
        bool IsStationary { get; set; }
        bool IsTranslationRestricted { get; set; }

        VesselPrismController VesselPrismController { get; }

        // Renamed: IShipHUDController -> IVesselHUDController
        IVesselHUDController VesselHUDController { get; }

        VesselCustomization Customization { get; }
        R_VesselActionHandler ActionHandler { get; }

        R_ShipElementStatsHandler ElementalStatsHandler { get; }

        /// <summary>
        /// In multiplayer mode, true -> owner client, false -> other clients
        /// In singleplayer mode, always false.
        /// </summary>
        bool IsNetworkOwner { get; }

        /// <summary>
        /// In multiplayer mode, true -> non-owner client, false -> owner client
        /// In singleplayer mode, always false
        /// </summary>
        bool IsNetworkClient { get; }

        void ResetForPlay();
    }
}
