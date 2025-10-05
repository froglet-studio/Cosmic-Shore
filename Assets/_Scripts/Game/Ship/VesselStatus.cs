using CosmicShore.Core;
using CosmicShore.Game.AI;
using CosmicShore.Game.Animation;
using CosmicShore.Game.IO;
using CosmicShore.Utility.ClassExtensions;
using System;
using System.Collections.Generic;
using CosmicShore.Utilities;
using UnityEngine;
using UnityEngine.Serialization;


namespace CosmicShore.Game
{
    /// <remarks>
    /// Keep this class as monobehaviour, 
    /// as the network vessel status needs to be a network behaviour
    /// </remarks>
    [RequireComponent(typeof(PrismSpawner))]
    [RequireComponent(typeof(ResourceSystem))]
    [RequireComponent(typeof(VesselTransformer))]
    [RequireComponent(typeof(AIPilot))]
    [RequireComponent(typeof(Silhouette))]
    [RequireComponent(typeof(VesselCameraCustomizer))]
    [RequireComponent(typeof(ShipAnimation))]
    [RequireComponent(typeof(R_VesselActionHandler))]
    [RequireComponent(typeof(VesselCustomization))]
    [RequireComponent(typeof(R_ShipElementStatsHandler))]

    public class VesselStatus : MonoBehaviour, IVesselStatus
    {
        public event Action<IVesselStatus> OnShipInitialized;
        
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
        public IVesselHUDController ShipHUDController => _shipHUDController as IVesselHUDController;
        
        [SerializeField] VesselHUDView _vesselHUDView;
        public VesselHUDView VesselHUDView
        {
            get => _vesselHUDView;
            set => _vesselHUDView = value;
        }

        [SerializeField] 
        ShipHUDContainer shipHUDContainer;
        public ShipHUDContainer ShipHUDContainer => shipHUDContainer;
        public IVesselHUDView ShipHUDView { get; set; }

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
        public IInputStatus InputStatus => Player.InputStatus;
        public Material AOEExplosionMaterial { get; set; }
        public Material AOEConicExplosionMaterial { get; set; }
        public Material ShipMaterial { get; set; }
        public Material SkimmerMaterial { get; set; }
        public SO_Captain Captain { get; set; }
        public CameraManager CameraManager { get; set; }
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

        InputController _inputController;
        public InputController InputController
        {
            get
            {
                if (_inputController == null)
                {
                    if (Player == null)
                    {
                        Debug.LogError($"No player found to get input controller!");
                        return null;
                    }
                    _inputController = Player.InputController;
                }
                return _inputController;
            }
        }

        ShipAnimation _shipAnimation;
        public ShipAnimation ShipAnimation
        {
            get
            {
                _shipAnimation = _shipAnimation != null ? _shipAnimation : gameObject.GetOrAdd<ShipAnimation>();
                return _shipAnimation;
            }
        }

        PrismSpawner prismSpawner;
        public PrismSpawner PrismSpawner
        {
            get
            {
                prismSpawner = prismSpawner != null ? prismSpawner : gameObject.GetOrAdd<PrismSpawner>();
                return prismSpawner;
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
        public bool Boosting { get; set; }
        public bool ChargedBoostDischarging { get; set; }
        public bool Drifting { get; set; }
        public bool Turret { get; set; }
        public bool Portrait { get; set; }
        public bool SingleStickControls { get; set; }
        public bool LiveProjectiles { get; set; }
        public bool IsStationary { get; set; }
        public bool AlignmentEnabled { get; set; }
        public bool Slowed { get; set; }
        public bool Overheating { get; set; }
        public bool Attached { get; set; }
        public bool GunsActive { get; set; }
        public float Speed { get; set; }
        public float ChargedBoostCharge { get; set; }
        
        public Vector3 Course { get; set; }
        public Quaternion blockRotation { get; set; }
        public bool IsOwnerClient => Vessel.IsOwnerClient;

        public void ResetValues()
        {
            Boosting = false;
            ChargedBoostDischarging = false;
            Drifting = false;
            Attached = false;
            AttachedPrism = null;
            GunsActive = false;
            Course = transform.forward;
            ChargedBoostCharge = 1f;
            Slowed = false;
            Overheating = false;
            
            VesselTransformer.ResetShipTransformer();
        }
    }
}