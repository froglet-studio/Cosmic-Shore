using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "FireGunAction", menuName = "ScriptableObjects/Vessel Actions/Fire Gun")]
    public class FireGunActionSO : ShipActionSO
    {
        [Header("Config")]
        [SerializeField] int ammoIndex = 0;
        [SerializeField] float ammoCost = 0.03f;
        [SerializeField] float projectileScale = 1f;
        [SerializeField] int energy = 0;
        [SerializeField] float speed = 90f;
        [SerializeField] ElementalFloat projectileTime;

        [Header("Round Growth (MASS)")]
        [Tooltip("How many times its launch size the round swells to, at RESTING Mass " +
                 "(level 0). The skyburst leaves the bay at the size of the missile the bay " +
                 "animation just ejected and swells into the warhead that will detonate — the " +
                 "same in-flight growth the Sparrow's bullets have. 1 = no growth.\n\n" +
                 "WHEN it reaches this size is the projectile's business, not the action's: " +
                 "the missile prefab's Flight Growth Complete At is 0.2, so it swells over the " +
                 "first fifth of its flight and holds.")]
        [SerializeField, Min(0.01f)] float growthFactorAtRestingMass = 20f;

        [Tooltip("The same factor at Mass level 10. Linear in level and extrapolated across the " +
                 "whole [-5, 15] band, so at the shipped 20/32 a starved Mass level (-5) grows " +
                 "14x and full overcharge (15) grows 38x. Author both endpoints equal to take " +
                 "Mass out of it and fly one fixed size.")]
        [SerializeField, Min(0.01f)] float growthFactorAtFullMass = 32f;

        [Header("Bay Launch")]
        [Tooltip("Seconds between the fire input (which starts the missile-bay animation) and the " +
                 "projectile actually spawning. 0 = spawn immediately at the gun muzzle (legacy " +
                 "behavior). The Sparrow's skyburst uses this to let the bay open and the animated " +
                 "missile clear the hull before the live projectile takes over at the bay's pose.")]
        [SerializeField] float launchDelaySeconds = 0f;

        public int AmmoIndex => ammoIndex;
        public float AmmoCost => ammoCost;
        public float ProjectileScale => projectileScale;
        public int Energy => energy;
        public float Speed => speed;
        public ElementalFloat ProjectileTime => projectileTime;
        public float LaunchDelaySeconds => launchDelaySeconds;

        /// <summary>
        /// How much this round swells over its flight, from the vessel's LIVE Mass level —
        /// resolved per shot at fire time, never cached (element levels move mid-match, and a
        /// shared action asset must stay stateless).
        ///
        /// The same ONE parameter the Sparrow's bullets use
        /// (<see cref="ElementalScaling.RoundGrowthFactorForLevel"/>, authored per weapon),
        /// not a second Mass knob: MASS owns the SUBSTANCE of what you fire, so every round
        /// the vessel launches grows with it. Charge still owns the skyburst's BLAST radius —
        /// different quantity, different element.
        ///
        /// This is HOW MUCH. WHAT grows and WHEN are the projectile's business
        /// (<c>Projectile.flightGrowthTarget</c> / <c>flightGrowthCompleteAt01</c>): the
        /// skyburst grows its missile MODEL over the first fifth of its flight and then holds,
        /// so this factor is a look, not a reach — its hit sphere is untouched.
        /// </summary>
        public float ResolveGrowthFactor(IVesselStatus status)
            => ElementalScaling.RoundGrowthFactor(status, growthFactorAtRestingMass, growthFactorAtFullMass);

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<FireGunActionExecutor>()?.Fire(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
        }
    }
}
