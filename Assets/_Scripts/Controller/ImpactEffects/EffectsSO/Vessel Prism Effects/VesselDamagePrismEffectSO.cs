using System;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "VesselDamagePrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselDamagePrismEffectSO")]
    public class VesselDamagePrismEffectSO : VesselPrismEffectSO
    {
        [SerializeField] private Vector3 overrideCourse;
        [SerializeField] private bool useOverrideCourse;
        [SerializeField] private float overrideSpeed;
        [SerializeField] private bool useOverrideSpeed;

        [Header("Debris response")]
        [Tooltip("ON: debris leaves at the vessel's actual ram speed, identically for every prism size - so ramming faster visibly throws mass harder, and a hull hit matches a skimmer hit made at the same velocity. OFF (legacy): debris speed is ramSpeed * Inertia / prismVolume; with every vessel's Inertia at 1 that lands below the explosion clamp's floor for any realistic prism, so EVERY ram produced exactly the same 30 u/s and ram speed did nothing.")]
        [SerializeField] private bool proportionalDebris = true;

        [Tooltip("Debris speed as a multiple of ram speed. 1 = the prism leaves at the speed of the hull that hit it.")]
        [SerializeField] private float restitution = 1f;

        [Tooltip("Ceiling on debris speed, in real speed units, replacing the explosion prefab's clamp. Matches the sword's so the two paths saturate together.")]
        [SerializeField] private float debrisSpeedLimit = 600f;

        public static event Action<string> OnVesselDamagedPrism;

        public override void Execute(VesselImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor.Vessel.VesselStatus;
            var inertia = status.Inertia;
            var course  = useOverrideCourse ? overrideCourse : status.Course;
            var speed   = useOverrideSpeed  ? overrideSpeed  : status.Speed;

            if (proportionalDebris)
                PrismEffectHelper.DamageProportional(status, prismImpactee, course * speed, restitution, debrisSpeedLimit);
            else
                PrismEffectHelper.Damage(status, prismImpactee, inertia, course, speed);

            OnVesselDamagedPrism?.Invoke(status.PlayerName);
        }
    }
}