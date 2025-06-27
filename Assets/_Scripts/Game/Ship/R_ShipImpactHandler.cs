using CosmicShore.Game;
using CosmicShore.Game.IO;
using CosmicShore.Game.Projectiles;
using CosmicShore.Models;
using CosmicShore.Models.Enums;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Handles crystal and trail block effects for a ship.
    /// </summary>
    public class R_ShipImpactHandler : MonoBehaviour
    {
        [Header("Impact Settings")]
        [SerializeField] List<CrystalImpactEffects> crystalImpactEffects;
        [SerializeField] float minExplosionScale = 50f;
        [SerializeField] float maxExplosionScale = 400f;
        [SerializeField] List<TrailBlockImpactEffects> trailBlockImpactEffects;
        [SerializeField] float blockChargeChange = 1f;

        [Header("References")]
        [SerializeField] GameObject AOEPrefab;
        [SerializeField] int resourceIndex = 0;
        [SerializeField] int ammoResourceIndex = 0;
        [SerializeField] int boostResourceIndex = 0;
        [SerializeField] float Inertia = 70f;

        const float speedModifierDuration = 2f;

        IShip _ship;

        public void Initialize(IShip ship) => _ship = ship;

        public void PerformCrystalImpactEffects(CrystalProperties crystalProperties)
        {
            foreach (CrystalImpactEffects effect in crystalImpactEffects)
            {
                switch (effect)
                {
                    case CrystalImpactEffects.PlayHaptics:
                        if (!_ship.ShipStatus.AutoPilotEnabled)
                            HapticController.PlayHaptic(HapticType.CrystalCollision);
                        break;
                    case CrystalImpactEffects.AreaOfEffectExplosion:
                        var aoeExplosion = Instantiate(AOEPrefab).GetComponent<AOEExplosion>();
                        aoeExplosion.SetPositionAndRotation(transform.position, transform.rotation);
                        aoeExplosion.MaxScale = _ship.ShipStatus.ResourceSystem.Resources.Count > ammoResourceIndex
                            ? Mathf.Lerp(minExplosionScale, maxExplosionScale, _ship.ShipStatus.ResourceSystem.Resources[ammoResourceIndex].CurrentAmount)
                            : maxExplosionScale;
                        aoeExplosion.Detonate(_ship);
                        break;
                    case CrystalImpactEffects.IncrementLevel:
                        _ship.ShipStatus.ResourceSystem.IncrementLevel(crystalProperties.Element);
                        break;
                    case CrystalImpactEffects.FillCharge:
                        _ship.ShipStatus.ResourceSystem.ChangeResourceAmount(boostResourceIndex, crystalProperties.fuelAmount);
                        break;
                    case CrystalImpactEffects.Boost:
                        _ship.ShipStatus.ShipTransformer.ModifyThrottle(crystalProperties.speedBuffAmount, 4 * speedModifierDuration);
                        break;
                    case CrystalImpactEffects.DrainAmmo:
                        _ship.ShipStatus.ResourceSystem.ChangeResourceAmount(ammoResourceIndex,
                            -_ship.ShipStatus.ResourceSystem.Resources[ammoResourceIndex].CurrentAmount);
                        break;
                    case CrystalImpactEffects.GainOneThirdMaxAmmo:
                        _ship.ShipStatus.ResourceSystem.ChangeResourceAmount(ammoResourceIndex,
                            _ship.ShipStatus.ResourceSystem.Resources[ammoResourceIndex].CurrentAmount / 3f);
                        break;
                    case CrystalImpactEffects.GainFullAmmo:
                        _ship.ShipStatus.ResourceSystem.ChangeResourceAmount(ammoResourceIndex,
                            _ship.ShipStatus.ResourceSystem.Resources[ammoResourceIndex].MaxAmount);
                        break;
                }
            }
        }

        public void PerformTrailBlockImpactEffects(TrailBlockProperties trailBlockProperties)
        {
            foreach (TrailBlockImpactEffects effect in trailBlockImpactEffects)
            {
                switch (effect)
                {
                    case TrailBlockImpactEffects.PlayHaptics:
                        if (!_ship.ShipStatus.AutoPilotEnabled) HapticController.PlayHaptic(HapticType.BlockCollision);
                        break;
                    case TrailBlockImpactEffects.DrainHalfAmmo:
                        _ship.ShipStatus.ResourceSystem.ChangeResourceAmount(ammoResourceIndex,
                            -_ship.ShipStatus.ResourceSystem.Resources[ammoResourceIndex].CurrentAmount / 2f);
                        break;
                    case TrailBlockImpactEffects.DebuffSpeed:
                        _ship.ShipStatus.ShipTransformer.ModifyThrottle(trailBlockProperties.speedDebuffAmount, speedModifierDuration);
                        break;
                    case TrailBlockImpactEffects.DeactivateTrailBlock:
                        break;
                    case TrailBlockImpactEffects.ActivateTrailBlock:
                        break;
                    case TrailBlockImpactEffects.OnlyBuffSpeed:
                        if (trailBlockProperties.speedDebuffAmount > 1)
                            _ship.ShipStatus.ShipTransformer.ModifyThrottle(trailBlockProperties.speedDebuffAmount, speedModifierDuration);
                        break;
                    case TrailBlockImpactEffects.GainResourceByVolume:
                        _ship.ShipStatus.ResourceSystem.ChangeResourceAmount(boostResourceIndex, blockChargeChange);
                        break;
                    case TrailBlockImpactEffects.Attach:
                        Attach(trailBlockProperties.trailBlock);
                        _ship.ShipStatus.GunsActive = true;
                        break;
                    case TrailBlockImpactEffects.GainResource:
                        _ship.ShipStatus.ResourceSystem.ChangeResourceAmount(resourceIndex, blockChargeChange);
                        break;
                    case TrailBlockImpactEffects.Bounce:
                        var cross = Vector3.Cross(transform.forward, trailBlockProperties.trailBlock.transform.forward);
                        var normal = Quaternion.AngleAxis(90, cross) * trailBlockProperties.trailBlock.transform.forward;
                        var reflectForward = Vector3.Reflect(transform.forward, normal);
                        var reflectUp = Vector3.Reflect(transform.up, normal);
                        _ship.ShipStatus.ShipTransformer.GentleSpinShip(reflectForward, reflectUp, 1);
                        _ship.ShipStatus.ShipTransformer.ModifyVelocity((transform.position - trailBlockProperties.trailBlock.transform.position).normalized * 5,
                            Time.deltaTime * 15);
                        break;
                    case TrailBlockImpactEffects.Redirect:
                        _ship.ShipStatus.ShipTransformer.GentleSpinShip(.5f * transform.forward + .5f * (UnityEngine.Random.value < 0.5f ? -1f : 1f) * transform.right,
                            transform.up, 1);
                        break;
                    case TrailBlockImpactEffects.Explode:
                        trailBlockProperties.trailBlock.Damage(_ship.ShipStatus.Course * _ship.ShipStatus.Speed * Inertia,
                            _ship.ShipStatus.Team, _ship.ShipStatus.Player.PlayerName);
                        break;
                    case TrailBlockImpactEffects.FeelDanger:
                        if (trailBlockProperties.IsDangerous && trailBlockProperties.trailBlock.Team != _ship.ShipStatus.Team)
                        {
                            HapticController.PlayHaptic(HapticType.FakeCrystalCollision);
                            _ship.ShipStatus.ShipTransformer.ModifyThrottle(trailBlockProperties.speedDebuffAmount, 1.5f);
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

        void Attach(TrailBlock trailBlock)
        {
            if (trailBlock.Trail != null)
            {
                _ship.ShipStatus.Attached = true;
                _ship.ShipStatus.AttachedTrailBlock = trailBlock;
            }
        }
    }
}
