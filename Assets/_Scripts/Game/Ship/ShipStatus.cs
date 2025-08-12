using CosmicShore.Core;
using CosmicShore.Game.AI;
using CosmicShore.Game.Animation;
using CosmicShore.Game.IO;
using CosmicShore.Utility.ClassExtensions;
using System;
using System.Collections.Generic;
using UnityEngine;


namespace CosmicShore.Game
{
    /// <remarks>
    /// Keep this class as monobehaviour, 
    /// as the network ship status needs to be a network behaviour
    /// </remarks>
    [RequireComponent(typeof(TrailSpawner))]
    [RequireComponent(typeof(ResourceSystem))]
    [RequireComponent(typeof(ShipTransformer))]
    [RequireComponent(typeof(AIPilot))]
    [RequireComponent(typeof(Silhouette))]
    [RequireComponent(typeof(ShipCameraCustomizer))]
    [RequireComponent(typeof(ShipAnimation))]
    [RequireComponent(typeof(R_ShipActionHandler))]
    
    // Deprecated - Use R_ShipImpactor instead
    // [RequireComponent(typeof(R_ShipImpactHandler))]
    
    [RequireComponent(typeof(R_ShipCustomization))]
    [RequireComponent(typeof(R_ShipElementStatsHandler))]

    public class ShipStatus : MonoBehaviour, IShipStatus
    {
        public event Action<IShipStatus> OnShipInitialized;


        [SerializeField, RequireInterface(typeof(IShip))]
        UnityEngine.Object _shipInstance;
        public IShip Ship
        {
            get
            {
                if (_shipInstance == null)
                {
                    Debug.LogError("ShipInstance is not referenced in inspector of Ship Prefab!");
                    return null;
                }
                return _shipInstance as IShip;
            }
        }
        
        [SerializeField]
        MonoBehaviour _shipHUDController;
        public IShipHUDController ShipHUDController => _shipHUDController as IShipHUDController;
        public IShipHUDView ShipHUDView { get; set; }

        [SerializeField] 
        ShipHUDContainer shipHUDContainer;
        public ShipHUDContainer ShipHUDContainer => shipHUDContainer;

        [SerializeField] protected float boostMultiplier = 4f;
        public float BoostMultiplier
        {
            get => boostMultiplier;
            set => boostMultiplier = value;
        }

        [Header("Ship Meta")]
        [SerializeField] protected string _name;
        public string Name => _name;

        [SerializeField] protected ShipClassType _shipType;
        public ShipClassType ShipType => _shipType;


        [Header("Ship Components")]
        [SerializeField] protected Skimmer _nearFieldSkimmer;
        public Skimmer NearFieldSkimmer => _nearFieldSkimmer;

        [SerializeField] protected Skimmer _farFieldSkimmer;
        public Skimmer FarFieldSkimmer => _farFieldSkimmer;

        [SerializeField] protected GameObject orientationHandle;
        public GameObject OrientationHandle => orientationHandle;

        public Transform FollowTarget { get; set; }
        public Transform ShipTransform => Ship.Transform;
        public IPlayer Player { get; set; }
        public IInputStatus InputStatus => Player.InputStatus;
        public Material AOEExplosionMaterial { get; set; }
        public Material AOEConicExplosionMaterial { get; set; }
        public Material ShipMaterial { get; set; }
        public Material SkimmerMaterial { get; set; }
        public SO_Captain Captain { get; set; }
        public CameraManager CameraManager { get; set; }
        public List<GameObject> ShipGeometries { get; set; }
        public TrailBlock AttachedTrailBlock { get; set; }

        R_ShipActionHandler actionHandler;
        public R_ShipActionHandler ActionHandler
        {
            get
            {
                actionHandler = actionHandler != null ? actionHandler : gameObject.GetOrAdd<R_ShipActionHandler>();
                return actionHandler;
            }
        }

        R_ShipCustomization customization;
        public R_ShipCustomization Customization
        {
            get
            {
                customization = customization != null ? customization : gameObject.GetOrAdd<R_ShipCustomization>();
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

        TrailSpawner _trailSpawner;
        public TrailSpawner TrailSpawner
        {
            get
            {
                _trailSpawner = _trailSpawner != null ? _trailSpawner : gameObject.GetOrAdd<TrailSpawner>();
                return _trailSpawner;
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

        ShipTransformer _shipTransformer;
        public ShipTransformer ShipTransformer
        {
            get
            {
                _shipTransformer = _shipTransformer != null ? _shipTransformer : gameObject.GetOrAdd<ShipTransformer>();
                return _shipTransformer;
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

        ShipCameraCustomizer _shipCameraCustomizer;
        public ShipCameraCustomizer ShipCameraCustomizer
        {
            get
            {
                _shipCameraCustomizer = _shipCameraCustomizer != null ? _shipCameraCustomizer : gameObject.GetOrAdd<ShipCameraCustomizer>();
                return _shipCameraCustomizer;
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
        public bool ElevatedResourceGain { get; set; }
        public bool AlignmentEnabled { get; set; }
        public bool Slowed { get; set; }
        public bool Overheating { get; set; }
        public bool Attached { get; set; }
        public bool GunsActive { get; set; }
        
        public float GetInertia { get; set; }
        public float Speed { get; set; }
        public float ChargedBoostCharge { get; set; }
        
        public Vector3 Course { get; set; }
        public Quaternion blockRotation { get; set; }


        public void ResetValues()
        {
            Boosting = false;
            ChargedBoostDischarging = false;
            Drifting = false;
            Attached = false;
            AttachedTrailBlock = null;
            GunsActive = false;
            Course = transform.forward;
            ChargedBoostCharge = 1f;
            Slowed = false;
            Overheating = false;
        }
    }
}