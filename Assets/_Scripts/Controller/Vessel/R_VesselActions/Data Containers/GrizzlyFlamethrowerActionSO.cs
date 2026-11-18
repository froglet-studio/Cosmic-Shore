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
    /// <para><b>Mass is what the claw FEEDS you</b> (2026-11-18, the element-placeholder pass). Every enemy prism the
    /// claw lights refills the Grizzly's energy (<see cref="energyPerIgnite"/>, 0.01 at rest -> 0.025 at level 10), the
    /// energy the cannon, the sniper and the Rush spend. Mass only grows what helps your own team (Garrett,
    /// 2026-11-16), so the claw's reach and bite stay fixed (that was the 2026-11-16 try, reverted) and the trail no
    /// longer grows with Mass (the placeholder before it). Read live at each tick, on the machine that runs the spray.</para>
    /// <para>Mass 5, Claimed Fire: the burn STEALS on burnout instead of destroying.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "GrizzlyFlamethrowerAction", menuName = "ScriptableObjects/Vessel Actions/Grizzly Flamethrower")]
    public class GrizzlyFlamethrowerActionSO : ShipActionSO
    {
        [Header("Cone")]
        [SerializeField, Tooltip("The claw's reach ahead of the vessel, u. Fixed: Mass only grows what helps the team " +
                                 "(Garrett, 2026-11-16); the Grizzly's Mass is the energy the claw feeds it.")]
        ElementalFloat reach = new(60f);
        [SerializeField, Tooltip("Half-angle of the ignite cone, degrees.")]
        float coneHalfAngle = 25f;
        [SerializeField, Tooltip("Ignites per second while held.")]
        float igniteTicksPerSecond = 4f;
        [SerializeField, Tooltip("The most prisms one tick lights. Fixed, like the reach.")]
        ElementalFloat bite = new(3f);

        [Header("Fuel (the Mass card)")]
        [SerializeField, Tooltip("Index of the vessel ResourceSystem resource the claw refills (the Grizzly's energy).")]
        int energyIndex = 0;
        [SerializeField, Tooltip("Energy each enemy prism the claw lights gives back. MASS scales it: 0.01 at rest, 0.025 at " +
                                 "level 10, floored at half (the fleet floor rule). A Rush costs 0.25, so a few seconds of " +
                                 "claw on enemy mass buys the next ram. Super-shielded prisms it cannot light give nothing.")]
        ElementalFloat energyPerIgnite = ElementalFloat.Multiplier(0.01f, 0.025f, Element.Mass, 0.005f);

        public int EnergyIndex => energyIndex;
        /// <summary>Energy one lit prism refills at this vessel's live Mass level.</summary>
        public float EnergyPerIgniteFor(IVesselStatus status) => Mathf.Max(0f, energyPerIgnite.EvaluateLive(status));

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
