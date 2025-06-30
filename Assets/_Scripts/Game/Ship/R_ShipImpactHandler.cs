using CosmicShore.Core;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Handles crystal and trail block effects for a ship.
    /// </summary>
    public class R_ShipImpactHandler : MonoBehaviour
    {
        // TODO - remove this after replacing all effects with SOs
        [Header("Impact Settings")]
        [SerializeField] List<CrystalImpactEffects> crystalImpactEffects;
        [ShowIf(CrystalImpactEffects.AreaOfEffectExplosion)]
        [SerializeField] float minExplosionScale = 50f;
        [SerializeField] float maxExplosionScale = 400f;

        [SerializeField, RequireInterface(typeof(IImpactEffect))]
        List<ScriptableObject> _crystalImpactEffects;

        // TODO - remove this after replacing all effects with SOs
        [SerializeField] List<TrailBlockImpactEffects> trailBlockImpactEffects;

        [SerializeField, RequireInterface(typeof(IImpactEffect))] 
        List<ScriptableObject> _trailBlockImpactEffects;

        [SerializeField] float blockChargeChange = 1f;

        [Header("References")]
        [SerializeField] GameObject AOEPrefab;
        [SerializeField] int resourceIndex = 0;
        [SerializeField] int ammoResourceIndex = 0;
        [SerializeField] int boostResourceIndex = 0;
        [SerializeField] float Inertia = 70f;

        const float speedModifierDuration = 2f;

        IShipStatus _shipStatus;

        public void Initialize(IShipStatus ship) => _shipStatus = ship;

        /*public void PerformCrystalImpactEffects(CrystalProperties crystalProperties)
        {
            foreach (CrystalImpactEffects effect in crystalImpactEffects)
            {
                switch (effect)
                {
                    case CrystalImpactEffects.PlayHaptics:
                        if (!_shipStatus.AutoPilotEnabled)
                            HapticController.PlayHaptic(HapticType.CrystalCollision);
                        break;
                    case CrystalImpactEffects.AreaOfEffectExplosion:
                        var aoeExplosion = Instantiate(AOEPrefab).GetComponent<AOEExplosion>();
                        aoeExplosion.SetPositionAndRotation(transform.position, transform.rotation);
                        aoeExplosion.MaxScale = _shipStatus.ResourceSystem.Resources.Count > ammoResourceIndex
                            ? Mathf.Lerp(minExplosionScale, maxExplosionScale, _shipStatus.ResourceSystem.Resources[ammoResourceIndex].CurrentAmount)
                            : maxExplosionScale;
                        aoeExplosion.Detonate(_shipStatus.Ship);
                        break;
                    case CrystalImpactEffects.IncrementLevel:
                        _shipStatus.ResourceSystem.IncrementLevel(crystalProperties.Element);
                        break;
                    case CrystalImpactEffects.FillCharge:
                        _shipStatus.ResourceSystem.ChangeResourceAmount(boostResourceIndex, crystalProperties.fuelAmount);
                        break;
                    case CrystalImpactEffects.Boost:
                        _shipStatus.ShipTransformer.ModifyThrottle(crystalProperties.speedBuffAmount, 4 * speedModifierDuration);
                        break;
                    case CrystalImpactEffects.DrainAmmo:
                        _shipStatus.ResourceSystem.ChangeResourceAmount(ammoResourceIndex,
                            -_shipStatus.ResourceSystem.Resources[ammoResourceIndex].CurrentAmount);
                        break;
                    case CrystalImpactEffects.GainOneThirdMaxAmmo:
                        _shipStatus.ResourceSystem.ChangeResourceAmount(ammoResourceIndex,
                            _shipStatus.ResourceSystem.Resources[ammoResourceIndex].CurrentAmount / 3f);
                        break;
                    case CrystalImpactEffects.GainFullAmmo:
                        _shipStatus.ResourceSystem.ChangeResourceAmount(ammoResourceIndex,
                            _shipStatus.ResourceSystem.Resources[ammoResourceIndex].MaxAmount);
                        break;
                }
            }
        }*/

        public void PerformCrystalImpactEffects(CrystalProperties crystalProperties)
        {
            foreach (var effect in _crystalImpactEffects)
            {
                if (effect is IImpactEffect impactEffect)
                {
                    impactEffect.Execute(new ImpactContext
                    {
                        CrystalProps = crystalProperties,
                        ShipStatus = _shipStatus
                    });
                }
                else
                {
                    Debug.LogWarning($"Impact effect {effect.name} does not implement IImpactEffect interface.");
                }
            }
        }


        /*public void PerformTrailBlockImpactEffects(TrailBlockProperties trailBlockProperties)
            {
                foreach (TrailBlockImpactEffects effect in trailBlockImpactEffects)
                {
                    switch (effect)
                    {
                        case TrailBlockImpactEffects.PlayHaptics:
                            if (!_shipStatus.AutoPilotEnabled) HapticController.PlayHaptic(HapticType.BlockCollision);
                            break;
                        case TrailBlockImpactEffects.DrainHalfAmmo:
                            _shipStatus.ResourceSystem.ChangeResourceAmount(ammoResourceIndex,
                                -_shipStatus.ResourceSystem.Resources[ammoResourceIndex].CurrentAmount / 2f);
                            break;
                        case TrailBlockImpactEffects.DebuffSpeed:
                            _shipStatus.ShipTransformer.ModifyThrottle(trailBlockProperties.speedDebuffAmount, speedModifierDuration);
                            break;
                        case TrailBlockImpactEffects.DeactivateTrailBlock:
                            break;
                        case TrailBlockImpactEffects.ActivateTrailBlock:
                            break;
                        case TrailBlockImpactEffects.OnlyBuffSpeed:
                            if (trailBlockProperties.speedDebuffAmount > 1)
                                _shipStatus.ShipTransformer.ModifyThrottle(trailBlockProperties.speedDebuffAmount, speedModifierDuration);
                            break;
                        case TrailBlockImpactEffects.GainResourceByVolume:
                            _shipStatus.ResourceSystem.ChangeResourceAmount(boostResourceIndex, blockChargeChange);
                            break;
                        case TrailBlockImpactEffects.Attach:
                            Attach(trailBlockProperties.trailBlock);
                            _shipStatus.GunsActive = true;
                            break;
                        case TrailBlockImpactEffects.GainResource:
                            _shipStatus.ResourceSystem.ChangeResourceAmount(resourceIndex, blockChargeChange);
                            break;
                        case TrailBlockImpactEffects.Bounce:
                            var cross = Vector3.Cross(transform.forward, trailBlockProperties.trailBlock.transform.forward);
                            var normal = Quaternion.AngleAxis(90, cross) * trailBlockProperties.trailBlock.transform.forward;
                            var reflectForward = Vector3.Reflect(transform.forward, normal);
                            var reflectUp = Vector3.Reflect(transform.up, normal);
                            _shipStatus.ShipTransformer.GentleSpinShip(reflectForward, reflectUp, 1);
                            _shipStatus.ShipTransformer.ModifyVelocity((transform.position - trailBlockProperties.trailBlock.transform.position).normalized * 5,
                                Time.deltaTime * 15);
                            break;
                        case TrailBlockImpactEffects.Redirect:
                                _shipStatus.ShipTransformer.GentleSpinShip(.5f * transform.forward + .5f * (UnityEngine.Random.value < 0.5f ? -1f : 1f) * transform.right,
                                transform.up, 1);
                            break;
                        case TrailBlockImpactEffects.Explode:
                            trailBlockProperties.trailBlock.Damage(_shipStatus.Course * _shipStatus.Speed * Inertia,
                                _shipStatus.Team, _shipStatus.Player.PlayerName);
                            break;
                        case TrailBlockImpactEffects.FeelDanger:
                            if (trailBlockProperties.IsDangerous && trailBlockProperties.trailBlock.Team != _shipStatus.Team)
                            {
                                HapticController.PlayHaptic(HapticType.FakeCrystalCollision);
                                _shipStatus.ShipTransformer.ModifyThrottle(trailBlockProperties.speedDebuffAmount, 1.5f);
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
            }*/

        public void PerformTrailBlockImpactEffects(TrailBlockProperties trailBlockProperties)
        {
            foreach (var effect in _trailBlockImpactEffects)
            {
                if (effect is IImpactEffect impactEffect)
                {
                    impactEffect.Execute(new ImpactContext
                    {
                        TrailBlockProps = trailBlockProperties,
                        ShipStatus = _shipStatus
                    });
                }
                else
                {
                    Debug.LogWarning($"Impact effect {effect.name} does not implement IImpactEffect interface.");
                }
            }
        }

        void Attach(TrailBlock trailBlock)
        {
            if (trailBlock.Trail != null)
            {
                _shipStatus.Attached = true;
                _shipStatus.AttachedTrailBlock = trailBlock;
            }
        }
    }
}
