using CosmicShore.Game;
using CosmicShore.Game.IO;
using CosmicShore.Models;
using CosmicShore.Models.Enums;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Base behaviour for ship implementations.  Logic shared between
    /// <see cref="Ship"/> and <see cref="NetworkShip"/> should live here.
    /// </summary>
    [RequireComponent(typeof(ShipStatus))]
    public abstract class R_ShipBase : NetworkBehaviour, IShip
    {
        public event Action<IShipStatus> OnShipInitialized;

        [Header("Ship Meta")]
        [SerializeField] protected string _name;
        [SerializeField] protected ShipTypes _shipType;

        [Header("Ship Components")]
        [SerializeField] protected Skimmer nearFieldSkimmer;
        [SerializeField] protected GameObject orientationHandle;
        [SerializeField] protected List<GameObject> shipGeometries;

        [Header("Optional Ship Components")]
        [SerializeField] protected GameObject AOEPrefab;
        [SerializeField] protected Skimmer farFieldSkimmer;

        [Header("Environment Interactions")]
        [SerializeField] public List<CrystalImpactEffects> crystalImpactEffects;
        [SerializeField] protected List<TrailBlockImpactEffects> trailBlockImpactEffects;

        [Header("Configuration")]
        [SerializeField] protected int resourceIndex = 0;
        [SerializeField] protected int ammoResourceIndex = 0;
        [SerializeField] protected int boostResourceIndex = 0;
        [SerializeField] protected float boostMultiplier = 4f;
        [SerializeField] protected bool bottomEdgeButtons = false;
        [SerializeField] protected float Inertia = 70f;

        [SerializeField] protected R_ShipInput shipInput;

        [Header("Elemental Stats")]
        [Serializable]
        public struct ElementStat
        {
            public string StatName;
            public Element Element;

            public ElementStat(string statName, Element element)
            {
                StatName = statName;
                Element = element;
            }
        }

        [SerializeField] protected List<ElementStat> ElementStats = new();

        [Header("Event Channels")]
        [SerializeField] protected BoolEventChannelSO onBottomEdgeButtonsEnabled;

        [Header("Refactored Components")]
        [SerializeField] protected R_ShipActionHandler actionHandler;
        [SerializeField] protected R_ShipImpactHandler impactHandler;
        [SerializeField] protected R_ShipCustomization customization;

        protected Material shipMaterial;
        protected const float speedModifierDuration = 2f;

        protected IShipStatus _shipStatus;
        public IShipStatus ShipStatus
        {
            get
            {
                _shipStatus ??= GetComponent<ShipStatus>();
                _shipStatus.Name = _name;
                _shipStatus.ShipType = _shipType;
                _shipStatus.BoostMultiplier = boostMultiplier;
                return _shipStatus;
            }
        }

        public Transform Transform => transform;

        protected void SetTeamToShipStatusAndSkimmers(Teams team)
        {
            ShipStatus.Team = team;
            if (nearFieldSkimmer != null) nearFieldSkimmer.Team = team;
            if (farFieldSkimmer != null) farFieldSkimmer.Team = team;
        }

        protected void SetPlayerToShipStatusAndSkimmers(IPlayer player)
        {
            ShipStatus.Player = player;
            if (nearFieldSkimmer != null) nearFieldSkimmer.Player = player;
            if (farFieldSkimmer != null) farFieldSkimmer.Player = player;
        }

        protected void InitializeShipGeometries() => ShipHelper.InitializeShipGeometries(this, shipGeometries);

        protected void InitializeShipGeometries() => ShipHelper.InitializeShipGeometries(this, shipGeometries);

        public abstract void Initialize(IPlayer player);

        public virtual void Teleport(Transform targetTransform) =>
            ShipHelper.Teleport(transform, targetTransform);

        public virtual void SetResourceLevels(ResourceCollection resources) =>
            ShipStatus.ResourceSystem.InitializeElementLevels(resources);

        public virtual void SetShipUp(float angle) =>
            orientationHandle.transform.localRotation = Quaternion.Euler(0, 0, angle);

        public virtual void DisableSkimmer()
        {
            nearFieldSkimmer?.gameObject.SetActive(false);
            farFieldSkimmer?.gameObject.SetActive(false);
        }

        public virtual void PerformCrystalImpactEffects(CrystalProperties properties)
        {
            if (impactHandler != null)
            {
                impactHandler.PerformCrystalImpactEffects(properties);
                return;
            }

            foreach (var effect in crystalImpactEffects)
            {
                switch (effect)
                {
                    case CrystalImpactEffects.PlayHaptics:
                        if (!ShipStatus.AutoPilotEnabled)
                            HapticController.PlayHaptic(HapticType.CrystalCollision);
                        break;
                    case CrystalImpactEffects.AreaOfEffectExplosion:
                        var aoe = Instantiate(AOEPrefab).GetComponent<AOEExplosion>();
                        aoe.SetPositionAndRotation(transform.position, transform.rotation);
                        aoe.MaxScale = ShipStatus.ResourceSystem.Resources.Count > ammoResourceIndex
                            ? Mathf.Lerp(minExplosionScale, maxExplosionScale,
                                ShipStatus.ResourceSystem.Resources[ammoResourceIndex].CurrentAmount)
                            : maxExplosionScale;
                        aoe.Detonate(this);
                        break;
                    case CrystalImpactEffects.IncrementLevel:
                        ShipStatus.ResourceSystem.IncrementLevel(properties.Element);
                        break;
                    case CrystalImpactEffects.FillCharge:
                        ShipStatus.ResourceSystem.ChangeResourceAmount(boostResourceIndex, properties.fuelAmount);
                        break;
                    case CrystalImpactEffects.Boost:
                        ShipStatus.ShipTransformer.ModifyThrottle(properties.speedBuffAmount, 4 * speedModifierDuration);
                        break;
                    case CrystalImpactEffects.DrainAmmo:
                        ShipStatus.ResourceSystem.ChangeResourceAmount(ammoResourceIndex,
                            -ShipStatus.ResourceSystem.Resources[ammoResourceIndex].CurrentAmount);
                        break;
                    case CrystalImpactEffects.GainOneThirdMaxAmmo:
                        ShipStatus.ResourceSystem.ChangeResourceAmount(ammoResourceIndex,
                            ShipStatus.ResourceSystem.Resources[ammoResourceIndex].CurrentAmount / 3f);
                        break;
                    case CrystalImpactEffects.GainFullAmmo:
                        ShipStatus.ResourceSystem.ChangeResourceAmount(ammoResourceIndex,
                            ShipStatus.ResourceSystem.Resources[ammoResourceIndex].MaxAmount);
                        break;
                }
            }
        }

        public virtual void SetBoostMultiplier(float multiplier) => boostMultiplier = multiplier;

        public virtual void ToggleGameObject(bool toggle) => gameObject.SetActive(toggle);

        public virtual void SetShipMaterial(Material material)
        {
            shipMaterial = material;
            if (customization != null)
                customization.SetShipMaterial(material);
            else
                ShipHelper.ApplyShipMaterial(shipMaterial, shipGeometries);
        }

        public virtual void SetBlockSilhouettePrefab(GameObject prefab)
        {
            if (customization != null)
                customization.SetBlockSilhouettePrefab(prefab);
            else
                ShipStatus.Silhouette?.SetBlockPrefab(prefab);
        }

        public virtual void SetAOEExplosionMaterial(Material material)
        {
            if (customization != null)
                customization.SetAOEExplosionMaterial(material);
            else
                ShipStatus.AOEExplosionMaterial = material;
        }

        public virtual void SetAOEConicExplosionMaterial(Material material)
        {
            if (customization != null)
                customization.SetAOEConicExplosionMaterial(material);
            else
                ShipStatus.AOEConicExplosionMaterial = material;
        }

        public virtual void SetSkimmerMaterial(Material material)
        {
            if (customization != null)
                customization.SetSkimmerMaterial(material);
            else
                ShipStatus.SkimmerMaterial = material;
        }

        public virtual void AssignCaptain(SO_Captain captain)
        {
            ShipStatus.Captain = captain;
            SetResourceLevels(captain.InitialResourceLevels);
        }

        public virtual void BindElementalFloat(string name, Element element)
        {
            if (ElementStats.TrueForAll(es => es.StatName != name))
                ElementStats.Add(new ElementStat(name, element));
        }

        public virtual void PerformTrailBlockImpactEffects(TrailBlockProperties properties)
        {
            if (impactHandler != null)
            {
                impactHandler.PerformTrailBlockImpactEffects(properties);
                return;
            }

            foreach (var effect in trailBlockImpactEffects)
            {
                switch (effect)
                {
                    case TrailBlockImpactEffects.PlayHaptics:
                        if (!ShipStatus.AutoPilotEnabled)
                            HapticController.PlayHaptic(HapticType.BlockCollision);
                        break;
                    case TrailBlockImpactEffects.DrainHalfAmmo:
                        ShipStatus.ResourceSystem.ChangeResourceAmount(ammoResourceIndex,
                            -ShipStatus.ResourceSystem.Resources[ammoResourceIndex].CurrentAmount / 2f);
                        break;
                    case TrailBlockImpactEffects.DebuffSpeed:
                        ShipStatus.ShipTransformer.ModifyThrottle(properties.speedDebuffAmount, speedModifierDuration);
                        break;
                    case TrailBlockImpactEffects.OnlyBuffSpeed:
                        if (properties.speedDebuffAmount > 1)
                            ShipStatus.ShipTransformer.ModifyThrottle(properties.speedDebuffAmount, speedModifierDuration);
                        break;
                    case TrailBlockImpactEffects.GainResourceByVolume:
                        ShipStatus.ResourceSystem.ChangeResourceAmount(boostResourceIndex, blockChargeChange);
                        break;
                    case TrailBlockImpactEffects.Attach:
                        Attach(properties.trailBlock);
                        ShipStatus.GunsActive = true;
                        break;
                    case TrailBlockImpactEffects.GainResource:
                        ShipStatus.ResourceSystem.ChangeResourceAmount(resourceIndex, blockChargeChange);
                        break;
                    case TrailBlockImpactEffects.Bounce:
                        var cross = Vector3.Cross(transform.forward, properties.trailBlock.transform.forward);
                        var normal = Quaternion.AngleAxis(90, cross) * properties.trailBlock.transform.forward;
                        var reflectForward = Vector3.Reflect(transform.forward, normal);
                        var reflectUp = Vector3.Reflect(transform.up, normal);
                        ShipStatus.ShipTransformer.GentleSpinShip(reflectForward, reflectUp, 1);
                        ShipStatus.ShipTransformer.ModifyVelocity(
                            (transform.position - properties.trailBlock.transform.position).normalized * 5,
                            Time.deltaTime * 15);
                        break;
                    case TrailBlockImpactEffects.Redirect:
                        ShipStatus.ShipTransformer.GentleSpinShip(
                            .5f * transform.forward + .5f * (UnityEngine.Random.value < 0.5f ? -1f : 1f) * transform.right,
                            transform.up, 1);
                        break;
                    case TrailBlockImpactEffects.Explode:
                        properties.trailBlock.Damage(ShipStatus.Course * ShipStatus.Speed * Inertia,
                            ShipStatus.Team, ShipStatus.Player.PlayerName);
                        break;
                    case TrailBlockImpactEffects.FeelDanger:
                        if (properties.IsDangerous && properties.trailBlock.Team != ShipStatus.Team)
                        {
                            HapticController.PlayHaptic(HapticType.FakeCrystalCollision);
                            ShipStatus.ShipTransformer.ModifyThrottle(properties.speedDebuffAmount, 1.5f);
                        }
                        break;
                }
            }
        }

        protected void Attach(TrailBlock trailBlock)
        {
            if (trailBlock && trailBlock.Trail != null)
            {
                ShipStatus.Attached = true;
                ShipStatus.AttachedTrailBlock = trailBlock;
            }
        }

    protected void RaiseInitialized() => OnShipInitialized?.Invoke(ShipStatus);
    }

    [Serializable]
    public struct InputEventShipActionMapping
    {
        public InputEvents InputEvent;
        public List<ShipAction> ShipActions;
    }

    [Serializable]
    public struct ResourceEventShipActionMapping
    {
        public ResourceEvents ResourceEvent;
        public List<ShipAction> ClassActions;
    }
}
