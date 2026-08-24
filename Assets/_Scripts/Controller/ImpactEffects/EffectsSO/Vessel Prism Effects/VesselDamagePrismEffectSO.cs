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

        [Tooltip("Debris speed as a multiple of ram speed. 1 = the physical read (the prism leaves at the speed of the hull that hit it); shipped at 1/3 because full speed reads too hot. Matches the skimmer path so a hull hit and a sword hit at the same velocity stay identical.")]
        [SerializeField] private float restitution = 1f / 3f;

        [Tooltip("Ceiling on debris speed, in real speed units, replacing the explosion prefab's clamp. Matches the sword's so the two paths saturate together.")]
        [SerializeField] private float debrisSpeedLimit = 200f;

        public static event Action<string> OnVesselDamagedPrism;

        [Header("Attach")]
        [Tooltip("ON (default): a vessel that is RIDING a trail does not also ram it. Riding " +
                 "and ramming are the same collision, so without this the attacher destroys " +
                 "the very prism it just latched onto.")]
        [SerializeField] private bool skipWhileAttached = true;

        public override void Execute(VesselImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor.Vessel.VesselStatus;

            // A vessel riding a trail is not ramming it. Attaching and colliding are the SAME
            // contact, dispatched through one flat effect list with no ordering guarantee
            // between them - so a vessel that can attach will destroy the prism it attached to
            // unless something declines here. This shipped as a real bug in 2023 ("urchin
            // destroying first block when attaching to a trail") and the fix of the day lived
            // in a collision path that no longer exists; the Urchin only escaped it afterwards
            // by never listing a damage effect at all, which leaves the trap armed for the next
            // vessel that attaches. It is guarded here, once, for the whole fleet.
            if (skipWhileAttached && status.IsAttached) return;

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