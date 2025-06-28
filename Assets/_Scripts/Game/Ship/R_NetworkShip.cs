using CosmicShore.Core;
using CosmicShore.Game.IO;
using CosmicShore.Game.Projectiles;
using CosmicShore.Models;
using CosmicShore.Models.Enums;
using CosmicShore.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;


namespace CosmicShore.Game
{
    [RequireComponent(typeof(ShipStatus))]
    public class R_NetworkShip : R_ShipBase
    {
        public float BoostMultiplier { get => boostMultiplier; set => boostMultiplier = value; }

        #region Public Properties

        #endregion

        NetworkVariable<float> n_Speed = new(writePerm: NetworkVariableWritePermission.Owner);
        NetworkVariable<Vector3> n_Course = new(writePerm: NetworkVariableWritePermission.Owner);
        NetworkVariable<Quaternion> n_BlockRotation = new(writePerm: NetworkVariableWritePermission.Owner);

        float speedModifierDuration = 2f;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                n_Speed.OnValueChanged += OnSpeedChanged;
                n_Course.OnValueChanged += OnCourseChanged;
                n_BlockRotation.OnValueChanged += OnBlockRotationChanged;
            }
            else
            {
                shipInput?.SubscribeEvents();
            }
        }

        private void Update()
        {
            if (IsOwner)
            {
                n_Speed.Value = ShipStatus.Speed;
                n_Course.Value = ShipStatus.Course;
                n_BlockRotation.Value = ShipStatus.blockRotation;
            }
        }
        
        public override void OnNetworkDespawn()
        {
            if (!IsOwner)
            {
                n_Speed.OnValueChanged -= OnSpeedChanged;
                n_Course.OnValueChanged -= OnCourseChanged;
                n_BlockRotation.OnValueChanged -= OnBlockRotationChanged;
            }
            else
            {
                shipInput?.UnsubscribeEvents();
            }
        }

        public void Initialize(IPlayer player)
        {
            ShipStatus.Player = player;

            SetPlayerToShipStatusAndSkimmers(player);
            SetTeamToShipStatusAndSkimmers(player.Team);

            actionHandler?.Initialize(this);
            impactHandler?.Initialize(this);
            customization?.Initialize(this);

            InitializeShipGeometries();

            ShipStatus.ShipAnimation.Initialize(ShipStatus);
            ShipStatus.TrailSpawner.Initialize(ShipStatus);

            if (nearFieldSkimmer != null)
                nearFieldSkimmer.Initialize(this);

            if (farFieldSkimmer != null)
                farFieldSkimmer.Initialize(this);
            

            if (IsOwner)
            {
                if (!ShipStatus.FollowTarget) ShipStatus.FollowTarget = transform;

                // TODO - Remove GameCanvas dependency
                onBottomEdgeButtonsEnabled.RaiseEvent(true);
                // if (_bottomEdgeButtons) ShipStatus.Player.GameCanvas.MiniGameHUD.PositionButtonPanel(true);

                shipInput?.Initialize(this);

                ShipStatus.AIPilot.Initialize(false);
                ShipStatus.ShipCameraCustomizer.Initialize(this);
                ShipStatus.ShipTransformer.Initialize(this);
            }

            ShipStatus.ShipTransformer.enabled = IsOwner;
            ShipStatus.TrailSpawner.ForceStartSpawningTrail();
            ShipStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(2f);

            OnShipInitialized?.Invoke(ShipStatus);
        }

        void SetTeamToShipStatusAndSkimmers(Teams team)
        {
            ShipStatus.Team = team;
            if (nearFieldSkimmer != null) nearFieldSkimmer.Team = team;
            if (farFieldSkimmer != null) farFieldSkimmer.Team = team;
        }

        void SetPlayerToShipStatusAndSkimmers(IPlayer player)
        {
            ShipStatus.Player = player;
            if (nearFieldSkimmer != null) nearFieldSkimmer.Player = player;
            if (farFieldSkimmer != null) farFieldSkimmer.Player = player;
        }

        void InitializeShipGeometries() => ShipHelper.InitializeShipGeometries(this, shipGeometries);

        public void PerformShipControllerActions(InputEvents @event) => shipInput?.PerformShipControllerActions(@event);

        public void StopShipControllerActions(InputEvents @event) => shipInput?.StopShipControllerActions(@event);

        public void Teleport(Transform targetTransform) => ShipHelper.Teleport(transform, targetTransform);

        public void SetResourceLevels(ResourceCollection resourceGroup)
        {
            ShipStatus.ResourceSystem.InitializeElementLevels(resourceGroup);
        }

        public void SetShipUp(float angle)
        {
            orientationHandle.transform.localRotation = Quaternion.Euler(0, 0, angle);
        }

        public void DisableSkimmer()
        {
            nearFieldSkimmer?.gameObject.SetActive(false);
            farFieldSkimmer?.gameObject.SetActive(false);
        }

        public void PerformCrystalImpactEffects(CrystalProperties crystalProperties)
        {
            if (impactHandler != null)
            {
                impactHandler.PerformCrystalImpactEffects(crystalProperties);
                return;
            }
            foreach (CrystalImpactEffects effect in crystalImpactEffects)
            {
                switch (effect)
                {
                    case CrystalImpactEffects.PlayHaptics:
                        if (!ShipStatus.AutoPilotEnabled) HapticController.PlayHaptic(HapticType.CrystalCollision);//.PlayCrystalImpactHaptics();
                        break;
                    case CrystalImpactEffects.AreaOfEffectExplosion:
                        var aoeExplosion = Instantiate(AOEPrefab).GetComponent<AOEExplosion>();
                        aoeExplosion.SetPositionAndRotation(transform.position, transform.rotation);
                        aoeExplosion.MaxScale = ShipStatus.ResourceSystem.Resources.Count > ammoResourceIndex
                            ? Mathf.Lerp(minExplosionScale, maxExplosionScale, ShipStatus.ResourceSystem.Resources[ammoResourceIndex].CurrentAmount) : maxExplosionScale;
                        aoeExplosion.Detonate(this);
                        break;
                    case CrystalImpactEffects.IncrementLevel:
                        ShipStatus.ResourceSystem.IncrementLevel(crystalProperties.Element); // TODO: consider removing here and leaving this up to the crystals
                        break;
                    case CrystalImpactEffects.FillCharge:
                        ShipStatus.ResourceSystem.ChangeResourceAmount(boostResourceIndex, crystalProperties.fuelAmount);
                        break;
                    case CrystalImpactEffects.Boost:
                        ShipStatus.ShipTransformer.ModifyThrottle(crystalProperties.speedBuffAmount, 4 * speedModifierDuration);
                        break;
                    case CrystalImpactEffects.DrainAmmo:
                        ShipStatus.ResourceSystem.ChangeResourceAmount(ammoResourceIndex, -ShipStatus.ResourceSystem.Resources[ammoResourceIndex].CurrentAmount);
                        break;
                    case CrystalImpactEffects.GainOneThirdMaxAmmo:
                        ShipStatus.ResourceSystem.ChangeResourceAmount(ammoResourceIndex, ShipStatus.ResourceSystem.Resources[ammoResourceIndex].CurrentAmount / 3f);
                        break;
                    case CrystalImpactEffects.GainFullAmmo:
                        ShipStatus.ResourceSystem.ChangeResourceAmount(ammoResourceIndex, ShipStatus.ResourceSystem.Resources[ammoResourceIndex].MaxAmount);
                        break;
                }
            }
        }

        public void SetBoostMultiplier(float multiplier)
        {
            boostMultiplier = multiplier;
        }

        public void ToggleGameObject(bool toggle)
        {
            gameObject.SetActive(toggle);
        }

        public void SetShipMaterial(Material material)
        {
            shipMaterial = material;
            if (customization != null)
                customization.SetShipMaterial(material);
            else
                ShipHelper.ApplyShipMaterial(shipMaterial, shipGeometries);
        }

        public void SetBlockSilhouettePrefab(GameObject prefab)
        {
            if (customization != null)
                customization.SetBlockSilhouettePrefab(prefab);
            else if (ShipStatus.Silhouette)
                ShipStatus.Silhouette.SetBlockPrefab(prefab);
        }

        public void SetAOEExplosionMaterial(Material material)
        {
            if (customization != null)
                customization.SetAOEExplosionMaterial(material);
            else
                ShipStatus.AOEExplosionMaterial = material;
        }

        public void SetAOEConicExplosionMaterial(Material material)
        {
            if (customization != null)
                customization.SetAOEConicExplosionMaterial(material);
            else
                ShipStatus.AOEConicExplosionMaterial = material;
        }

        public void SetSkimmerMaterial(Material material)
        {
            if (customization != null)
                customization.SetSkimmerMaterial(material);
            else
                ShipStatus.SkimmerMaterial = material;
        }

        public void AssignCaptain(Captain captain)
        {
            ShipStatus.Captain = captain.SO_Captain;
            SetResourceLevels(captain.ResourceLevels);
        }

        public void AssignCaptain(SO_Captain captain)
        {
            ShipStatus.Captain = captain;
            SetResourceLevels(captain.InitialResourceLevels);
        }

        public void BindElementalFloat(string statName, Element element)
        {
            Debug.Log($"Ship.NotifyShipStatBinding - statName:{statName}, element:{element}");
            if (ElementStats.All(x => x.StatName != statName))
                ElementStats.Add(new ElementStat(statName, element));

            Debug.Log($"Ship.NotifyShipStatBinding - ElementStats.Count:{ElementStats.Count}");
        }

        public void PerformTrailBlockImpactEffects(TrailBlockProperties trailBlockProperties)
        {
            if (impactHandler != null)
            {
                impactHandler.PerformTrailBlockImpactEffects(trailBlockProperties);
                return;
            }
            foreach (TrailBlockImpactEffects effect in trailBlockImpactEffects)
            {
                switch (effect)
                {
                    case TrailBlockImpactEffects.PlayHaptics:
                        if (!ShipStatus.AutoPilotEnabled) HapticController.PlayHaptic(HapticType.BlockCollision);
                        break;
                    case TrailBlockImpactEffects.DrainHalfAmmo:
                        ShipStatus.ResourceSystem.ChangeResourceAmount(ammoResourceIndex, -ShipStatus.ResourceSystem.Resources[ammoResourceIndex].CurrentAmount / 2f);
                        break;
                    case TrailBlockImpactEffects.DebuffSpeed:
                        ShipStatus.ShipTransformer.ModifyThrottle(trailBlockProperties.speedDebuffAmount, speedModifierDuration);
                        break;
                    case TrailBlockImpactEffects.DeactivateTrailBlock:
                        break;
                    case TrailBlockImpactEffects.ActivateTrailBlock:
                        break;
                    case TrailBlockImpactEffects.OnlyBuffSpeed:
                        if (trailBlockProperties.speedDebuffAmount > 1) ShipStatus.ShipTransformer.ModifyThrottle(trailBlockProperties.speedDebuffAmount, speedModifierDuration);
                        break;
                    case TrailBlockImpactEffects.GainResourceByVolume:
                        ShipStatus.ResourceSystem.ChangeResourceAmount(boostResourceIndex, blockChargeChange);
                        break;
                    case TrailBlockImpactEffects.Attach:
                        Attach(trailBlockProperties.trailBlock);
                        ShipStatus.GunsActive = true;
                        break;
                    case TrailBlockImpactEffects.GainResource:
                        ShipStatus.ResourceSystem.ChangeResourceAmount(resourceIndex, blockChargeChange);
                        break;
                    case TrailBlockImpactEffects.Bounce:
                        var cross = Vector3.Cross(transform.forward, trailBlockProperties.trailBlock.transform.forward);
                        var normal = Quaternion.AngleAxis(90, cross) * trailBlockProperties.trailBlock.transform.forward;
                        var reflectForward = Vector3.Reflect(transform.forward, normal);
                        var reflectUp = Vector3.Reflect(transform.up, normal);
                        ShipStatus.ShipTransformer.GentleSpinShip(reflectForward, reflectUp, 1);
                        ShipStatus.ShipTransformer.ModifyVelocity((transform.position - trailBlockProperties.trailBlock.transform.position).normalized * 5, Time.deltaTime * 15);
                        break;
                    case TrailBlockImpactEffects.Redirect:
                        ShipStatus.ShipTransformer.GentleSpinShip(.5f * transform.forward + .5f * (UnityEngine.Random.value < 0.5f ? -1f : 1f) * transform.right, transform.up, 1);
                        break;
                    case TrailBlockImpactEffects.Explode:
                        trailBlockProperties.trailBlock.Damage(ShipStatus.Course * ShipStatus.Speed * Inertia, ShipStatus.Team, ShipStatus.Player.PlayerName);
                        break;
                    case TrailBlockImpactEffects.FeelDanger:
                        if (trailBlockProperties.IsDangerous && trailBlockProperties.trailBlock.Team != ShipStatus.Team)
                        {
                            HapticController.PlayHaptic(HapticType.FakeCrystalCollision);
                            ShipStatus.ShipTransformer.ModifyThrottle(trailBlockProperties.speedDebuffAmount, 1.5f);
                        }
                        break;
                    case TrailBlockImpactEffects.Steal:
                        break;
                    case TrailBlockImpactEffects.DecrementLevel:
                        break;
                    case TrailBlockImpactEffects.Shield:
                        break;
                    case TrailBlockImpactEffects.Stop:
                        break;
                    case TrailBlockImpactEffects.Fire:
                        break;
                    case TrailBlockImpactEffects.FX:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        void OnSpeedChanged(float previousValue, float newValue)
        {
            ShipStatus.Speed = newValue;
        }

        void OnCourseChanged(Vector3  previousValue, Vector3 newValue)
        {
            ShipStatus.Course = newValue;
        }

        void OnBlockRotationChanged(Quaternion previousValue, Quaternion newValue)
        {
            ShipStatus.blockRotation = newValue;
        }

        //
        // Attach and Detach
        //
        void Attach(TrailBlock trailBlock)
        {
            if (trailBlock.Trail != null)
            {
                ShipStatus.Attached = true;
                ShipStatus.AttachedTrailBlock = trailBlock;
            }
        }
    }

}
