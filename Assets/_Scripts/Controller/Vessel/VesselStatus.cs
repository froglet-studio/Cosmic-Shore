using CosmicShore.Gameplay;
using CosmicShore.Utility;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;

namespace CosmicShore.Gameplay
{
    /// <remarks>
    /// Keep this class as monobehaviour, 
    /// as the network vessel status needs to be a network behaviour
    /// </remarks>
    [RequireComponent(typeof(VesselPrismController))]
    [RequireComponent(typeof(ResourceSystem))]
    [RequireComponent(typeof(VesselTransformer))]
    [RequireComponent(typeof(AIPilot))]
    [RequireComponent(typeof(ElementalBarsController))]
    [RequireComponent(typeof(VesselCameraCustomizer))]
    [RequireComponent(typeof(VesselAnimation))]
    [RequireComponent(typeof(R_VesselActionHandler))]
    [RequireComponent(typeof(VesselCustomization))]
    [RequireComponent(typeof(R_ShipElementStatsHandler))]

    public class VesselStatus : MonoBehaviour, IVesselStatus
    {
        static readonly List<VesselStatus> s_live = new();

        /// <summary>
        /// Every enabled vessel, kept by OnEnable/OnDisable: the set
        /// <c>FindObjectsByType&lt;VesselStatus&gt;</c> answers, without walking the scene. That walk
        /// costs in proportion to EVERY object loaded (each trail prism is one), so it grows all
        /// session: in a Stoat match it measured 1.8 ms a call in the first minute and 20 ms
        /// (worst 61 ms) eight minutes in — the black-hole pull, the Stoat's ripple and the warp
        /// scaler each paid it on a timer (2026-10-17). Copy it before doing anything that can
        /// enable or disable a vessel while you iterate.
        /// </summary>
        public static IReadOnlyList<VesselStatus> Live => s_live;

        // Drops only dead entries, never live ones: this project enters Play without a domain or a
        // scene reload, so a vessel already in the open scene may have registered before this runs,
        // and clearing would hide it until it is toggled. OnDisable keeps the list clean otherwise.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLive() => s_live.RemoveAll(v => !v);

        void OnEnable()
        {
            s_live.Add(this);
            // Proximity collider-LOD: every vessel (human, AI, menu autopilot) is a
            // focus that keeps nearby prism colliders alive - hull, skimmer, and
            // trigger interactions all happen here. Registration lazily creates the
            // LOD manager, so it exists exactly in the scenes that have vessels.
            PrismColliderLodManager.EnsureInstance();
            PrismColliderLodManager.RegisterFocus(transform);
        }

        void OnDisable()
        {
            s_live.Remove(this);
            PrismColliderLodManager.UnregisterFocus(transform);
        }

        [SerializeField, RequireInterface(typeof(IVessel))]
        UnityEngine.Object _shipInstance;
        public IVessel Vessel
        {
            get
            {
                if (_shipInstance is not null) 
                    return _shipInstance as IVessel;
                
                CSDebug.LogError("ShipInstance is not referenced in inspector of Vessel Prefab!");
                return null;
            }
        }
        
        [SerializeField]
        MonoBehaviour vesselHUDController;
        public IVesselHUDController VesselHUDController
        {
            get
            {
                if (vesselHUDController is IVesselHUDController c) return c;
                // A hull may legitimately carry NO HUD (the Urchin has no HUD prefab yet), and
                // VesselController.Initialize already warns once for that. Only a reference that
                // is SET but of the wrong type is an authoring error worth shouting about -
                // logging on every read turned an optional component into console spam.
                if (vesselHUDController)
                    CSDebug.LogError($"{name}: vesselHUDController does not implement IVesselHUDController", this);
                return null;
            }
        }


        [SerializeField] protected float boostMultiplier = 4f;
        public float BoostMultiplier
        {
            get => boostMultiplier;
            set => boostMultiplier = value;
        }

        [SerializeField] protected float inertia = 1f;
        public float Inertia => inertia;


        [Header("Vessel Meta")]
        [SerializeField] protected string _name;
        public string Name => _name;

        [FormerlySerializedAs("_shipType")] [SerializeField] protected VesselClassType vesselType;
        public VesselClassType VesselType => vesselType;


        [Header("Vessel Components")]
        [SerializeField] protected Skimmer _nearFieldSkimmer;
        public Skimmer NearFieldSkimmer => _nearFieldSkimmer;

        [SerializeField] protected Skimmer _farFieldSkimmer;
        public Skimmer FarFieldSkimmer => _farFieldSkimmer;

        [SerializeField] protected GameObject orientationHandle;
        public GameObject OrientationHandle => orientationHandle;

        public Transform CameraFollowTarget { get; set; }
        public Transform ShipTransform => Vessel.Transform;
        public IPlayer Player { get; set; }
        public Material AOEExplosionMaterial { get; set; }
        public Material AOEConicExplosionMaterial { get; set; }
        public Material ShipMaterial { get; set; }
        public Material SkimmerMaterial { get; set; }
        public List<GameObject> ShipGeometries { get; set; }
        public Prism AttachedPrism { get; set; }

        R_VesselActionHandler actionHandler;
        public R_VesselActionHandler ActionHandler
        {
            get
            {
                actionHandler = actionHandler != null ? actionHandler : gameObject.GetOrAdd<R_VesselActionHandler>();
                return actionHandler;
            }
        }

        VesselCustomization customization;
        public VesselCustomization Customization
        {
            get
            {
                customization = customization != null ? customization : gameObject.GetOrAdd<VesselCustomization>();
                return customization;
            }
        }

        VesselAnimation vesselAnimation;
        public VesselAnimation VesselAnimation
        {
            get
            {
                vesselAnimation = vesselAnimation != null ? vesselAnimation : gameObject.GetOrAdd<VesselAnimation>();
                return vesselAnimation;
            }
        }

        VesselPrismController vesselPrismController;
        public VesselPrismController VesselPrismController
        {
            get
            {
                vesselPrismController = vesselPrismController != null ? vesselPrismController : gameObject.GetOrAdd<VesselPrismController>();
                return vesselPrismController;
            }
        }

        ResourceSystem _resourceSystem;
        public ResourceSystem ResourceSystem
        {
            get
            {
                _resourceSystem = _resourceSystem != null ? _resourceSystem : gameObject.GetOrAdd<ResourceSystem>();
                return _resourceSystem;
            }
        }

        R_VesselElementalAbilityHandler _elementalAbilityHandler;
        public R_VesselElementalAbilityHandler ElementalAbilityHandler
        {
            get
            {
                _elementalAbilityHandler = _elementalAbilityHandler != null
                    ? _elementalAbilityHandler
                    : gameObject.GetOrAdd<R_VesselElementalAbilityHandler>();
                if (!_elementalAbilityHandler.IsInitialized)
                    _elementalAbilityHandler.Initialize(this);
                return _elementalAbilityHandler;
            }
        }

        VesselTransformer vesselTransformer;
        public VesselTransformer VesselTransformer
        {
            get
            {
                vesselTransformer = vesselTransformer != null ? vesselTransformer : gameObject.GetOrAdd<VesselTransformer>();
                return vesselTransformer;
            }
        }

        AIPilot aiPilot;
        public AIPilot AIPilot
        {
            get
            {
                aiPilot = aiPilot != null ? aiPilot : gameObject.gameObject.GetOrAdd<AIPilot>();
                return aiPilot;
            }
        }

        AICinematicBehavior aiCinematicBehavior;
        public AICinematicBehavior AICinematicBehavior
        {
            get
            {
                aiCinematicBehavior = aiCinematicBehavior != null ? aiCinematicBehavior : gameObject.gameObject.GetOrAdd<AICinematicBehavior>();
                return aiCinematicBehavior;
            }
        }

        ElementalBarsController _elementalBarsController;
        public ElementalBarsController ElementalBarsController
        {
            get
            {
                _elementalBarsController = _elementalBarsController != null ? _elementalBarsController : gameObject.GetOrAdd<ElementalBarsController>();
                return _elementalBarsController;
            }
        }

        VesselCameraCustomizer _vesselCameraCustomizer;
        public VesselCameraCustomizer VesselCameraCustomizer
        {
            get
            {
                _vesselCameraCustomizer = _vesselCameraCustomizer != null ? _vesselCameraCustomizer : gameObject.GetOrAdd<VesselCameraCustomizer>();
                return _vesselCameraCustomizer;
            }
        }

        R_ShipElementStatsHandler _elementalStatsHandler;
        public R_ShipElementStatsHandler ElementalStatsHandler
        {
            get
            {
                _elementalStatsHandler = _elementalStatsHandler != null ? _elementalStatsHandler : gameObject.GetOrAdd<R_ShipElementStatsHandler>();
                return _elementalStatsHandler;
            }
        }
        
        // booleans
        public bool IsBoosting { get; set; }
        public bool IsChargedBoostDischarging { get; set; }
        public bool IsDrifting { get; set; }
        public bool IsPortrait { get; set; }
        public bool IsSingleStickControls { get; set; }
        public bool HasLiveProjectiles { get; set; }
        public bool IsStationary { get; set; }

        bool _isTranslationRestricted;
        public bool IsTranslationRestricted
        {
            get => _isTranslationRestricted;
            set
            {
                if (_isTranslationRestricted.Equals(value)) return;
                _isTranslationRestricted = value;
            }
        }

        public bool AlignmentEnabled { get; set; }
        public bool IsSlowed { get; set; }
        public bool IsAttached { get; set; }
        public bool GunsActive { get; set; }
        public float Speed { get; set; }
        public float ChargedBoostCharge { get; set; }
        
        public Vector3 Course { get; set; }
        public Quaternion blockRotation { get; set; }
        public bool IsNetworkOwner => Vessel.IsNetworkOwner;
        public bool IsNetworkClient => Vessel.IsNetworkClient;

        public void ResetForPlay()
        {
            IsStationary = true;
            IsBoosting = false;
            IsChargedBoostDischarging = false;
            IsDrifting = false;
            IsAttached = false;
            AttachedPrism = null;
            GunsActive = false;
            ChargedBoostCharge = 1f;
            IsSlowed = false;

            ResourceSystem.Reset();
            ResourceSystem.MarkSpawned();
            // A silence bought in the last turn does not carry into this one. Every peer resets
            // its copy of the vessel, so every peer lifts it.
            if (Player != null) VesselSilence.Lift(Player.Name);
            VesselTransformer.ResetTransformer();
            VesselPrismController.StopSpawn();
            VesselPrismController.ClearTrails();
            VesselAnimation.StopFlareEngine();
            VesselAnimation.StopFlareBody();
        }
    }
}