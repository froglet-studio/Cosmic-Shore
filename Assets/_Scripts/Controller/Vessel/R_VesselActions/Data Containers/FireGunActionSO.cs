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
        [SerializeField] float projectileTime = 3f;

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

        [Header("Payload")]
        [Tooltip("This shot carries its PROXIMITY FUZE and the WARHEAD shockwave the fuze " +
                 "exists to deliver - the big vessel-and-fauna detection sphere that sets the " +
                 "rocket off early, and the blast that debuffs pilots and jousts creatures " +
                 "without touching mass. The projectile prefab authors both radii; this says " +
                 "whether the shot is carrying them at all.\n\n" +
                 "OFF on the Sparrow's BASE rocket: a cheap missile has to be AIMED.")]
        [SerializeField] bool armWarhead = true;

        [Tooltip("This shot's detonation may spawn the mass-CREATING blasts in its effect " +
                 "assets - the skyburst's 72-prism shielded cairn. The DESTRUCTIVE blast in the " +
                 "same detonation is unaffected either way, so turning this off still blows a " +
                 "hole; it just leaves nothing behind.\n\n" +
                 "OFF on the Sparrow's BASE rocket.")]
        [SerializeField] bool createMassOnDetonation = true;

        [Tooltip("This shot lays a line of small prisms behind it as it flies, at the spacing, " +
                 "size and HARD CAP the projectile prefab authors (Projectile > Flight Prism " +
                 "Trail). Ordinary conserved mass in the shooter's domain - grazeable, " +
                 "stealable, destroyable, and never removed on a clock.")]
        [SerializeField] bool layPrismTrail = false;

        [Header("Stationary Variant")]
        [Tooltip("This weapon fires a DIFFERENT shot while the vessel is in its stationary " +
                 "stance (IVesselStatus.IsTranslationRestricted - the Sparrow's turret stance, " +
                 "the Serpent's stop). Off = one shot, and every field below is ignored.\n\n" +
                 "The stance is the discriminator rather than measured speed because it is " +
                 "SERVER-REPLICATED (VesselController.n_IsTranslationRestricted) while speed is " +
                 "simulated locally: a fire press is replayed on every peer and each peer runs " +
                 "this resolve, so the peers must agree on which shot left the bay.")]
        [SerializeField] bool hasStationaryVariant = false;

        [Tooltip("What the stationary shot costs. The shipped skyburst is DOUBLE the base " +
                 "cost - the bay holds four cheap rockets or two heavy ones.")]
        [SerializeField] float stationaryAmmoCost = 0.5f;

        [Tooltip("How fast the stationary shot flies. The shipped skyburst is DOUBLE the base " +
                 "speed.\n\n" +
                 "Note a round's RANGE is not speed x projectileTime: the mover decelerates on " +
                 "a cosine, so it covers speed x 2T/pi (~64%). Doubling the speed doubles the " +
                 "range as well as the time to target.")]
        [SerializeField] float stationarySpeed = 240f;

        [SerializeField] bool stationaryArmWarhead = true;
        [SerializeField] bool stationaryCreateMassOnDetonation = true;
        [SerializeField] bool stationaryLayPrismTrail = true;

        [Header("Bay Launch")]
        [Tooltip("Seconds between the fire input (which starts the missile-bay animation) and the " +
                 "projectile actually spawning. 0 = spawn immediately at the gun muzzle (legacy " +
                 "behavior). The Sparrow's skyburst uses this to let the bay open and the animated " +
                 "missile clear the hull before the live projectile takes over at the bay's pose.")]
        [SerializeField] float launchDelaySeconds = 0f;

        /// <summary>
        /// ONE shot's cost, speed and payload, resolved from the weapon's two variants at fire
        /// time. A struct rather than four out-parameters because the four must be spent
        /// together: a shot that paid the base price and flew the stationary payload is the
        /// failure this type exists to make unrepresentable.
        /// </summary>
        public readonly struct Shot
        {
            public readonly float AmmoCost;
            public readonly float Speed;
            public readonly ProjectilePayload Payload;
            /// <summary>True when the STATIONARY variant was selected - what a HUD, a toast or
            /// an animation asks when it wants to say which rocket just left.</summary>
            public readonly bool IsStationaryVariant;

            public Shot(float ammoCost, float speed, ProjectilePayload payload, bool stationary)
            {
                AmmoCost = ammoCost;
                Speed = speed;
                Payload = payload;
                IsStationaryVariant = stationary;
            }
        }

        /// <summary>
        /// Which shot this press fires. Read ONCE at press time by the executor and carried
        /// through the launch delay - the ammo is spent at the press, so the variant that was
        /// paid for must be the variant that launches even if the pilot leaves the stance in
        /// the 0.2 s the bay takes to open.
        /// </summary>
        public Shot ResolveShot(IVesselStatus status)
        {
            bool stationary = hasStationaryVariant &&
                              status != null && status.IsTranslationRestricted;

            return stationary
                ? new Shot(stationaryAmmoCost, stationarySpeed,
                    new ProjectilePayload(stationaryArmWarhead,
                                          stationaryCreateMassOnDetonation,
                                          stationaryLayPrismTrail), true)
                : new Shot(ammoCost, speed,
                    new ProjectilePayload(armWarhead, createMassOnDetonation, layPrismTrail), false);
        }

        public int AmmoIndex => ammoIndex;

        /// <summary>
        /// The BASE shot's cost - what the HUD prices the tank in, and what it must keep
        /// pricing it in: the icon ladder counts how many rockets the bay holds, and a bay that
        /// holds four cheap ones does not hold four expensive ones. A pilot who wants heavy
        /// rockets is spending two slots each.
        /// </summary>
        public float AmmoCost => ammoCost;
        public float ProjectileScale => projectileScale;
        public int Energy => energy;

        /// <summary>
        /// The BASE shot's speed. NOT the speed a given shot flies — that comes from
        /// <see cref="ResolveShot"/>, because the stationary variant has its own. Kept for
        /// parity with <see cref="AmmoCost"/> and for anything that wants to describe the
        /// weapon rather than a shot.
        /// </summary>
        public float Speed => speed;
        public float ProjectileTime => projectileTime;
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
