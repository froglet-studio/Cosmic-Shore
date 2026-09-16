using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Config for the Serpent's <b>sniper shot</b> — a single hitscan round down the scope's own
    /// line that destroys what it hits, <b>including SUPER-SHIELDED mass</b>, on a long cooldown.
    ///
    /// <para><b>It only exists while the scope is up.</b> The shot is bound to the same right
    /// trigger as the Serpent's cloak, and the two are told apart by
    /// <c>SniperScopeActionExecutor.IsScoped</c>: scoped fires the rifle, unscoped still cloaks.
    /// Neither ability has to know about the other's wiring — each asks the scope.</para>
    ///
    /// <para><b>Element → parameter: CHARGE owns this ability</b>, and the parameter is the
    /// RECOVERY — how long the pilot waits between shots. Charge is threat/energy fleet-wide, and
    /// on a weapon whose per-shot effect is already absolute (one round, one prism, whatever its
    /// armour) the only honest axis left is how often you get to use it. Its level-5 upgrade is
    /// <b>Pierce</b>: the round no longer stops at the first prism.</para>
    ///
    /// <para><b>Why this is allowed to break a super-shield.</b> Super-shielded mass is invulnerable
    /// to <c>Prism.Damage</c> outright; the ONE sanctioned teardown is to drop the shields first
    /// and then devastate, which is what the Rhino's energised blade does
    /// (<c>RhinoSkimmerDamagePrismEffectSO.PopSuperShield</c>). This is the fleet's second such
    /// force and it uses that exact sequence, so the stellation sheds as ordinary explosion debris
    /// and the prism dies the canonical animated death — mass is conserved and nothing pops out of
    /// existence. It is an ACTIVE force with a long cooldown and a scope you have to hold, which
    /// is what keeps it clear of the no-imposed-decay law.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "SniperShotAction", menuName = "ScriptableObjects/Vessel Actions/Sniper Shot")]
    public class SniperShotActionSO : ShipActionSO
    {
        [Header("Cooldown")]
        [Tooltip("Seconds between shots at the resting Charge level. This is the whole cost of " +
                 "the ability - the shot spends no resource and needs no ammunition, so the wait " +
                 "IS the balance.")]
        [SerializeField, Min(0.5f)] private float cooldownSeconds = 12f;

        [Tooltip("Multiplier on the cooldown at Charge level 10. BELOW 1 shortens the wait; at " +
                 "the resting level the authored value above is used exactly, per the " +
                 "anchored-at-1 elemental contract.")]
        [SerializeField, Range(0.1f, 1f)] private float cooldownMultiplierAtFullCharge = 0.45f;

        [Header("Ballistics")]
        [Tooltip("How far the round reaches, in world units. A hitscan, so this is the whole of " +
                 "its flight - there is no projectile to outrun or dodge.")]
        [SerializeField, Min(10f)] private float rangeUnits = 3000f;

        [Tooltip("Radius of the round's path, in world units. The hitscan is a thin capsule " +
                 "rather than a mathematical line: a line through a lattice of prism CENTRES " +
                 "misses almost everything, because the query tests centres (PrismSpatialIndex." +
                 "QuerySegment) and a prism is several units across.")]
        [SerializeField, Min(0.1f)] private float pathRadius = 4f;

        [Header("Charge 5 — Pierce")]
        [Tooltip("How many prisms a PIERCING round destroys before it stops. 0 is unlimited " +
                 "(everything on the line). Below the Charge-5 upgrade the round always stops at " +
                 "the first prism it reaches.")]
        [SerializeField, Min(0)] private int pierceCount = 3;

        [Header("Impact")]
        [Tooltip("Debris speed the destroyed prism's pieces carry, in world units/second - the " +
                 "TRUE velocity, on the proportional-debris contract, not a legacy inertia gain.")]
        [SerializeField, Min(0f)] private float debrisSpeed = 90f;

        [Tooltip("Ceiling on that debris speed. Passed alongside the true-velocity vector so the " +
                 "explosion prefab's own 33.33 u/s clamp - sized for the legacy gain - cannot " +
                 "flatten every sniper kill to the same speed.")]
        [SerializeField, Min(0f)] private float debrisSpeedLimit = 120f;

        [Tooltip("Camera shake on firing: intensity then duration in seconds. Zero intensity " +
                 "disables it.")]
        [SerializeField] private float shakeIntensity = 0.6f;
        [SerializeField, Min(0f)] private float shakeDuration = 0.18f;

        public float CooldownSeconds => cooldownSeconds;
        public float CooldownMultiplierAtFullCharge => cooldownMultiplierAtFullCharge;
        public float RangeUnits => rangeUnits;
        public float PathRadius => pathRadius;
        public int PierceCount => pierceCount;
        public float DebrisSpeed => debrisSpeed;
        public float DebrisSpeedLimit => debrisSpeedLimit;
        public float ShakeIntensity => shakeIntensity;
        public float ShakeDuration => shakeDuration;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<SniperShotActionExecutor>()?.Fire(this, vesselStatus);

        /// <summary>
        /// Nothing to stop: the shot is an instant, and the cooldown is the executor's own state.
        /// Releasing the trigger mid-cooldown must not shorten it.
        /// </summary>
        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus) { }
    }
}
