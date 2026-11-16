using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Grizzly third weapon: the burning plasma-claw flamethrower, the MASS weapon.
    /// Costs NO energy (per the class doc). Sets enemy prisms - and enemy trails,
    /// which are prisms - on fire with a spreading burn (PrismBurnManager).
    ///
    /// <para><b>Mass is the claw's weight</b> (2026-11-16, the weakest-abilities pass: Mass used to grow the trail,
    /// a number nothing on the Weapon Systems card was about). Mass sets how far the claw reaches
    /// (<see cref="reach"/>, 60 u at rest -> 120 u at level 10) and how many prisms it lights per tick
    /// (<see cref="bite"/>, 3 -> 8), so a heavy Grizzly burns a wide, deep swathe and a light one a short tongue.
    /// Both are read live on every tick; the cone drawn on the hull is the reach of the moment.</para>
    /// <para>Mass 5, Claimed Fire: the burn STEALS on burnout instead of destroying.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "GrizzlyFlamethrowerAction", menuName = "ScriptableObjects/Vessel Actions/Grizzly Flamethrower")]
    public class GrizzlyFlamethrowerActionSO : ShipActionSO
    {
        [Header("Cone (Mass)")]
        [SerializeField, Tooltip("Mass: the claw's reach ahead of the vessel, u (60 at rest -> 120 at level 10, half " +
                                 "strength at level -5).")]
        ElementalFloat reach = ElementalFloat.Multiplier(60f, 120f, Element.Mass, 30f);
        [SerializeField, Tooltip("Half-angle of the ignite cone, degrees.")]
        float coneHalfAngle = 25f;
        [SerializeField, Tooltip("Ignites per second while held.")]
        float igniteTicksPerSecond = 4f;
        [SerializeField, Tooltip("Mass: the most prisms one tick lights (3 at rest -> 8 at level 10, half strength at -5).")]
        ElementalFloat bite = ElementalFloat.Multiplier(3f, 8f, Element.Mass, 1.5f);

        /// <summary>The claw's reach for this vessel's live Mass level.</summary>
        public float RangeFor(IVesselStatus status) => Mathf.Max(1f, reach.EvaluateLive(status));
        /// <summary>The most prisms one tick lights for this vessel's live Mass level (at least one).</summary>
        public int IgnitesPerTickFor(IVesselStatus status) => Mathf.Max(1, Mathf.RoundToInt(bite.EvaluateLive(status)));
        public float ConeHalfAngle => coneHalfAngle;
        public float IgniteTicksPerSecond => igniteTicksPerSecond;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<GrizzlyFlamethrowerActionExecutor>()?.BeginSpray(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<GrizzlyFlamethrowerActionExecutor>()?.EndSpray();
    }
}
