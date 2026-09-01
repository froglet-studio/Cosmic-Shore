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

        IPlayer Player { get; set; }

        /// <summary>
        /// Local User in singleplayer is the player providing input, not AI.
        /// In Multiplayer, it is the Owner Client providing input.
        /// </summary>
        bool IsLocalUser => Player.IsLocalUser;

        string PlayerName
        {
            get
            {
                if (Player != null)
                    return Player.Name;

                CSDebug.LogWarning("Player is null, returning empty string for PlayerName.");
                return "No-name";
            }
        }

        Domains Domain
        {
            get
            {
                if (Player == null)
                {
                    CSDebug.LogError("No Player found to get domain!");
                    return Domains.Jade;
                }
                return Player.Domain;
            }
        }

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

        ElementalBarsController ElementalBarsController { get; }

        Material ShipMaterial { get; set; }
        Material SkimmerMaterial { get; set; }

        float Speed { get; set; }

        bool IsSingleStickControls { get; set; }
        bool IsSlowed { get; set; }
        bool IsStationary { get; set; }
        bool IsTranslationRestricted { get; set; }

        /// <summary>
        /// True while some system wards this vessel against elemental debuffs of the given SOURCE
        /// class — the general "invulnerable to elemental debuffs" state, asked about one kind of
        /// debuff. Negative <see cref="ResourceSystem.ApplyElementalEffect"/> calls carrying that
        /// class are dropped while it holds; buffs still land. Grant/revoke through
        /// <see cref="ResourceSystem.SetElementalDebuffImmunity"/>, or declare a window on the
        /// shared <c>VesselElementalImmunity</c> driver — it is not owned by any one vessel class
        /// (the Sparrow holds it against everything while boosting at Time 5, the Serpent while
        /// stopped; the Dolphin holds it against DANGER PRISMS ONLY while drifting at Time 5).
        /// <para>Always name the class you are asking about. There is deliberately no bare
        /// "is immune" bool: the Dolphin's ward proves a caller that assumes total immunity from a
        /// true answer would be wrong, and that mistake is silent.</para>
        /// </summary>
        // `this.` is deliberate: the property and its type share a name, and while the C# "Color
        // Color" rule resolves that correctly, this file is the only place in the codebase that
        // would rely on it in a value context — not worth the ambiguity.
        bool IsImmuneToElementalDebuff(ElementalDebuffSources source) =>
            this.ResourceSystem && this.ResourceSystem.IsImmuneTo(source);

        /// <summary>
        /// The union of the source classes this vessel is currently warded against
        /// (<see cref="ElementalDebuffSources.None"/> when nothing is held). For HUD / VFX /
        /// diagnostics — gameplay asks <see cref="IsImmuneToElementalDebuff"/>.
        /// </summary>
        ElementalDebuffSources ImmuneDebuffSources =>
            this.ResourceSystem ? this.ResourceSystem.ImmuneDebuffSources : ElementalDebuffSources.None;

        VesselPrismController VesselPrismController { get; }

        // Renamed: IShipHUDController -> IVesselHUDController
        IVesselHUDController VesselHUDController { get; }

        VesselCustomization Customization { get; }
        R_VesselActionHandler ActionHandler { get; }

        R_ShipElementStatsHandler ElementalStatsHandler { get; }

        /// <summary>
        /// Per-vessel elemental ability state: quantitative multipliers + level-threshold
        /// qualitative unlocks, configured by the class's ElementalAbilityMapSO.
        /// Lazily created and self-initializing (the ResourceSystem pattern).
        /// </summary>
        R_VesselElementalAbilityHandler ElementalAbilityHandler { get; }

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
