using CosmicShore.Core;
using CosmicShore.Game.AI;
using CosmicShore.Game.Animation;
using CosmicShore.Game.IO;
using CosmicShore.Utility.ClassExtensions;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;


namespace CosmicShore.Game
{
    /// <remarks>
    /// Keep this class as monobehaviour, 
    /// as the network vessel status needs to be a network behaviour
    /// </remarks>
    [RequireComponent(typeof(VesselPrismController))]
    [RequireComponent(typeof(ResourceSystem))]
    [RequireComponent(typeof(VesselTransformer))]
    [RequireComponent(typeof(AIPilot))]
    [RequireComponent(typeof(Silhouette))]
    [RequireComponent(typeof(VesselCameraCustomizer))]
    [RequireComponent(typeof(VesselAnimation))]
    [RequireComponent(typeof(R_VesselActionHandler))]
    [RequireComponent(typeof(VesselCustomization))]
    [RequireComponent(typeof(R_ShipElementStatsHandler))]

    public class VesselStatus : MonoBehaviour, IVesselStatus
    {
        [SerializeField, RequireInterface(typeof(IVessel))]
        UnityEngine.Object _shipInstance;
        public IVessel Vessel
        {
            get
            {
                if (_shipInstance is not null) 
                    return _shipInstance as IVessel;
                
                Debug.LogError("ShipInstance is not referenced in inspector of Vessel Prefab!");
                return null;
            }
        }
        
        [SerializeField]
        MonoBehaviour _shipHUDController;
        public IVesselHUDController VesselHUDController => _shipHUDController as IVesselHUDController;
        
        [SerializeField] VesselHUDView _vesselHUDView;
        public VesselHUDView VesselHUDView
        {
            get => _vesselHUDView;
            set => _vesselHUDView = value;
        }

        [SerializeField] protected float boostMultiplier = 4f;
        public float BoostMultiplier
        {
            get => boostMultiplier;
            set => boostMultiplier = value;
        }

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
        public SO_Captain Captain { get; set; }
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

        Silhouette _silhouette;
        public Silhouette Silhouette
        {
            get
            {
                _silhouette = _silhouette != null ? _silhouette : gameObject.GetOrAdd<Silhouette>();
                return _silhouette;
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
        public bool IsOverheating { get; set; }
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
            IsOverheating = false;

            ResourceSystem.Reset();
            VesselTransformer.ResetTransformer();
            VesselPrismController.StopSpawn();
            VesselPrismController.ClearTrails();
            VesselAnimation.StopFlareEngine();
            VesselAnimation.StopFlareBody();
        }
    }
}