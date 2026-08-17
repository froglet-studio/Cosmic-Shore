using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Sparrow's full-auto cannons. This asset is also the single authored home of the
    /// vessel's gun cadence, muzzle speed, flight time and ACCURACY: the Turret Stance
    /// (<see cref="FullAutoBlockShootActionSO"/>) fires prisms at the SAME rate, the SAME
    /// speed, along the SAME flight path and through the SAME spread cone, and it gets those
    /// numbers by pointing at this asset rather than copying them. Retune here and both fire
    /// modes move together.
    /// </summary>
    [CreateAssetMenu(fileName="FullAutoAction", menuName="ScriptableObjects/Vessel Actions/Full Auto")]
    public class FullAutoActionSO : ShipActionSO
    {
        [Header("Config")]
        [SerializeField] int   ammoIndex = 0;
        [SerializeField] float ammoCost  = 0.03f;
        [SerializeField] bool  inherit   = false;
        [SerializeField] float projectileScale = 1f;
        [SerializeField] float firingRate = 1f;
        [SerializeField] float projectileTime = 3f;
        [SerializeField] FiringPatterns firingPattern = FiringPatterns.Default;
        [SerializeField] int   energy = 0;
        [SerializeField] ElementalFloat speedValue;

        [Header("Round Growth (MASS)")]
        [Tooltip("How many times its launch cross-section a round swells to by the END of its " +
                 "flight, at RESTING Mass (level 0). Rounds leave the muzzle small and arrive " +
                 "fat — the hit volume grows with the visual, so what you see is what you hit.")]
        [SerializeField, Min(0.01f)] float growthFactorAtRestingMass = 3f;

        [Tooltip("The same factor at Mass level 10. The curve is LINEAR IN LEVEL and " +
                 "extrapolated across the whole [-5, 15] band, so at the shipped 3/6 a starved " +
                 "Mass level (-5) grows 1.5x and full overcharge (15) grows 7.5x. This is the " +
                 "'huge projectiles' feel, earned: Mass is the size of what you fire.")]
        [SerializeField, Min(0.01f)] float growthFactorAtFullMass = 6f;

        [Header("Accuracy")]
        [Tooltip("How the cone opens while the trigger is held, and the haptic ramp that " +
                 "reports it. Shared with the Turret Stance through bulletAction, exactly like " +
                 "cadence and speed — a turret shot IS a bullet, spread included.")]
        [SerializeField] GunSpreadProfile spread = new();

        public int AmmoIndex => ammoIndex;
        public float AmmoCost => ammoCost;
        public bool Inherit => inherit;
        public float ProjectileScale => projectileScale;
        public float FiringRate => firingRate;
        public float ProjectileTime => projectileTime;
        public FiringPatterns FiringPattern => firingPattern;
        public int Energy => energy;
        public ElementalFloat SpeedValue => speedValue;

        /// <summary>The accuracy-decay cone, shared by both fire modes. Never null — an
        /// all-zero profile is the sanctioned "no spread" opt-out.</summary>
        public GunSpreadProfile Spread => spread ??= new GunSpreadProfile();

        /// <summary>
        /// The live muzzle speed of one shot: the authored base scaled by the vessel's SPACE
        /// multiplier from its <c>ElementalAbilityMapSO</c>. Read per volley at fire time —
        /// never cached across a hold, and never bound as an ElementalFloat on this shared
        /// asset (per-vessel state on a shared SO is last-initializer-wins in multiplayer).
        /// </summary>
        public float ResolveSpeed(IVesselStatus status)
        {
            var abilities = status?.ElementalAbilityHandler;
            return speedValue.Value * (abilities ? abilities.Multiplier(Element.Space) : 1f);
        }

        /// <summary>
        /// How much a round swells over its flight, from the vessel's LIVE Mass level —
        /// resolved per volley at fire time, exactly like <see cref="ResolveSpeed"/>, and
        /// shared with the Turret Stance through the same asset so the two fire modes cannot
        /// drift apart.
        ///
        /// Linear in LEVEL (not in the map's multiplier curve) so the authored endpoints ARE
        /// the shipped feel: 3× at resting Mass, 6× at Mass 10, extrapolated over the element
        /// system's full [-5, 15] band → 1.5× starved, 7.5× at full overcharge.
        ///
        /// Note this is quantitative MASS scaling on top of the map's own Mass multiplier
        /// (which stretches a turret prism's long axis). They are the same idea — Mass is the
        /// size of what you fire — expressed on the two things the Sparrow fires, which is why
        /// it does not read as double-dipping. Keep it that way: do not add a THIRD Mass
        /// parameter without revisiting the one-parameter-per-element convention.
        /// </summary>
        public float ResolveGrowthFactor(IVesselStatus status)
        {
            var resources = status?.ResourceSystem;
            int level = resources ? resources.GetLevel(Element.Mass) : 0;
            return GrowthFactorForLevel(level, growthFactorAtRestingMass, growthFactorAtFullMass);
        }

        /// <summary>
        /// The growth curve itself, pulled out as a pure function so it is edit-mode testable
        /// (<c>SparrowRoundGrowthTests</c>). Linear in <paramref name="massLevel"/> with the
        /// authored pair anchored at levels 0 and 10, extrapolated — NOT clamped — across the
        /// element system's full [-5, 15] band.
        /// </summary>
        public static float GrowthFactorForLevel(int massLevel, float atRestingMass, float atFullMass)
            => Mathf.Max(0.01f, Mathf.LerpUnclamped(atRestingMass, atFullMass, massLevel / 10f));

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            if (!execs) return;
            // The accuracy state is a per-vessel component, resolved here and handed down:
            // the SO is shared by every Sparrow in the match and must stay stateless.
            execs.Get<FullAutoActionExecutor>()?.Begin(this, execs.Get<GunSprayAccuracy>());
        }

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<FullAutoActionExecutor>()?.End();
    }
}
