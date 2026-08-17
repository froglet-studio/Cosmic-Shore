using System;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Utility;
using CosmicShore.ScriptableObjects;
namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(
        fileName = "VesselExplosionByOmniCrystal",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/VesselExplosionByCrystalEffectSO")]
    public class VesselExplosionByCrystalEffectSO : VesselCrystalEffectSO
    {
        [Header("Events")]
        [SerializeField] private ScriptableEventVesselImpactor rhinoCrystalExplosionEvent;
        [SerializeField] private ScriptableEventVesselImpactor squirrelCrystalExplosionEvent;

        public static event Action<VesselImpactor> OnMantaFlowerExplosion;
        
      


        [Header("Explosion Settings")]
        [SerializeField] private AOEExplosion[] _aoePrefabs;
        [Tooltip("Blast size at EMPTY resource. On a CONIC blast this is its capsule LENGTH when " +
                 "the resource is empty, not its width — see Core Explosion Scale.")]
        [SerializeField] private float _minExplosionScale;
        [Tooltip("Blast size at FULL resource. On a CONIC blast this is its capsule LENGTH at full " +
                 "charge, and also the rendered cone's base diameter, so the capsule's tips ride " +
                 "the visible base circle.")]
        [SerializeField] private float _maxExplosionScale;
        [Tooltip("CONIC blasts only. The capsule's DIAMETER — the width the blast keeps across the " +
                 "beam at EVERY charge, while charge buys length along the vessel's gape axis. " +
                 "Independent of Min Explosion Scale so an uncharged blast can already be a short " +
                 "capsule instead of a sphere; leave 0 to fall back to Min (a sphere at rest). " +
                 "Ignored by the spherical blast.")]
        [SerializeField] private float _coreExplosionScale;
        [SerializeField] private int _resourceIndex;
        [SerializeField] private Material _aoeExplosionMaterial;
        [SerializeField] private Vector3 _spawnOffset = new Vector3(0, 0, -5f);

        [Header("Elemental (Space) — blast reach")]
        [Tooltip("SPACE → blast SIZE: multiplier at Space level 10 (1 at the resting level, " +
                 "extrapolating into the deficit band so debuffed Space shrinks the blast). Leave " +
                 "at 1 to opt a vessel out entirely.\n\n" +
                 "It scales the blast SELF-SIMILARLY — reach and base diameter by the same factor — " +
                 "because a cone's half-angle IS baseRadius/height. Stretching the length alone " +
                 "would narrow the cone and steal the angle _resourceIndex set. So: the resource " +
                 "owns the cone's ANGLE, Space owns how far down-range it carries.")]
        [SerializeField] private float _heightMultiplierAtFullSpace = 1f;
        [Tooltip("Floor for the Space size multiplier so a deficit can never collapse the blast.")]
        [SerializeField] private float _minHeightMultiplier = 0.35f;

        [Header("Elemental (Space) — friendly fire")]
        [Tooltip("When ON, this blast damages the pilot's OWN domain until Space's level-5 upgrade " +
                 "lands, and spares allies once it does — the upgrade IS the no-friendly-fire " +
                 "reward. When OFF the blast keeps whatever the prefab authored, for vessels whose " +
                 "Space slot means something else.")]
        [SerializeField] private bool _spaceUpgradeSparesAllies;

        [Header("Anti-Spam")]
        [Tooltip("Minimum time between explosions from the same vessel hitting a crystal.")]
        [SerializeField] private float _explosionCooldown = 0.15f;

        // Keyed by instance ID (not the impactor reference) so destroyed vessel
        // impactors are never retained by this static dictionary across scene loads.
        private static readonly Dictionary<int, float> _lastExplosionTimeByImpactor
            = new ();

        /// <summary>
        /// The volume this blast would sweep if <paramref name="status"/>'s vessel struck a crystal
        /// right now — the shape the Dolphin's Echo Sight highlights.
        ///
        /// It reads THIS asset's authored scales and re-derives the Space multiplier the same way
        /// <see cref="Execute"/> does, so a preview cannot drift from the detonation: retune a
        /// scale, or move Space's reach, and both move together. Returns false for a vessel whose
        /// blast is not conic (nothing to preview) or before the vessel has a transform.
        /// </summary>
        public bool TryResolveBlastVolume(IVesselStatus status, out BlastVolume volume)
        {
            volume = default;
            if (status == null) return false;

            float sizeMultiplier = 1f;
            if (!Mathf.Approximately(_heightMultiplierAtFullSpace, 1f))
                sizeMultiplier = ElementalScaling.Multiplier(status, Element.Space,
                    _heightMultiplierAtFullSpace, _minHeightMultiplier);

            return ExplosionHelper.TryResolveConicVolume(
                _aoePrefabs, status,
                _minExplosionScale, _maxExplosionScale,
                _resourceIndex, _spawnOffset,
                sizeMultiplier, _coreExplosionScale,
                out volume);
        }

        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            if (vesselImpactor == null || vesselImpactor.Vessel == null)
                return;

            var now = Time.time;
            int impactorId = vesselImpactor.GetInstanceID();

            if (_lastExplosionTimeByImpactor.TryGetValue(impactorId, out var lastTime))
            {
                if (now - lastTime < _explosionCooldown)
                    return;
            }

            _lastExplosionTimeByImpactor[impactorId] = now;

            var status = vesselImpactor.Vessel.VesselStatus;

            // SPACE → how far down-range the cone carries. Read live, per blast, off this vessel's
            // own levels; exactly 1x at the resting level, so an unconfigured asset changes nothing.
            //
            // It grows the blast SELF-SIMILARLY (reach and base diameter together), which is the
            // whole point: a cone's half-angle is baseRadius/height, so stretching the length alone
            // would NARROW the cone and quietly steal the angle that skim energy just set. Space
            // moves the blast's SIZE; the resource keeps its ANGLE.
            float sizeMultiplier = 1f;
            if (!Mathf.Approximately(_heightMultiplierAtFullSpace, 1f))
                sizeMultiplier = ElementalScaling.Multiplier(status, Element.Space,
                    _heightMultiplierAtFullSpace, _minHeightMultiplier);

            // SPACE level 5 → the blast stops eating the pilot's own domain. Below the unlock the
            // cone is indiscriminate, which is what makes sparing allies worth earning. Routed
            // through IsUpgradeActive (not the raw level) because this changes what the blast
            // DESTROYS — an outcome, so every machine has to agree on it.
            bool? affectSelfOverride = null;
            if (_spaceUpgradeSparesAllies)
            {
                var handler = status?.ElementalAbilityHandler;
                bool sparesAllies = handler && handler.IsUpgradeActive(Element.Space);
                affectSelfOverride = !sparesAllies;
            }

            ExplosionHelper.CreateExplosion(
                _aoePrefabs,
                vesselImpactor,
                _minExplosionScale,
                _maxExplosionScale,
                _aoeExplosionMaterial,
                _resourceIndex,
                _spawnOffset,
                sizeMultiplier,
                affectSelfOverride,
                _coreExplosionScale);

            switch (vesselImpactor.Vessel.VesselStatus.VesselType)
            {
                case VesselClassType.Rhino:
                    rhinoCrystalExplosionEvent?.Raise(vesselImpactor);
                    break;
                case VesselClassType.Manta:
                    OnMantaFlowerExplosion?.Invoke(vesselImpactor);
                    break;
                case VesselClassType.Any:
                    break;
                case VesselClassType.Random:
                    break;
                case VesselClassType.Dolphin:
                    break;
                case VesselClassType.Urchin:
                    break;
                case VesselClassType.Grizzly:
                    break;
                case VesselClassType.Squirrel:
                    squirrelCrystalExplosionEvent?.Raise(vesselImpactor);
                    break;
                case VesselClassType.Serpent:
                    break;
                case VesselClassType.Termite:
                    break;
                case VesselClassType.Falcon:
                    break;
                case VesselClassType.Shrike:
                    break;
                case VesselClassType.Sparrow:
                    break;
            }
        }
    }
}