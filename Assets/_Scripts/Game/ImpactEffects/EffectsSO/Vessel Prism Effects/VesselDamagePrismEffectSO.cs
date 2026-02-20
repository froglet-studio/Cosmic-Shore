using System;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "VesselDamagePrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselDamagePrismEffectSO")]
    public class VesselDamagePrismEffectSO : VesselPrismEffectSO
    {
        [SerializeField] private Vector3 overrideCourse;
        [SerializeField] private bool useOverrideCourse;
        [SerializeField] private float overrideSpeed;
        [SerializeField] private bool useOverrideSpeed;
        public static event Action<string> OnVesselDamagedPrism;

        public override void Execute(VesselImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor.Vessel.VesselStatus;
            var inertia = status.Inertia;
            var course  = useOverrideCourse ? overrideCourse : status.Course;
            var speed   = useOverrideSpeed  ? overrideSpeed  : status.Speed;

            PrismEffectHelper.Damage(status, prismImpactee, inertia, course, speed);

            OnVesselDamagedPrism?.Invoke(status.PlayerName);
        }
    }
}