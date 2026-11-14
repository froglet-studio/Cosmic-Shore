using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A POSSESSION: a herd of one whose holder is a pilot RIDING the creature (the Jewel Wasp, vessel class 31,
    /// <c>Controller/Vessel/R_VesselActions/JEWEL_WASP.md</c>; Garrett, 2026-11-14: "possess fauna and be able to fly around
    /// and consume prisms or attack vessels as that fauna"). The pilot's sticks are the creature's steering
    /// (<see cref="IFaunaLeash.TrySteer"/>, read every frame), and its triggers are the creature's mouth
    /// (<see cref="Fauna.PossessedSuction"/>) and its attack (<see cref="Fauna.PlayPossessionAttack"/>).
    ///
    /// <para>The breaks it carries, each signed off by Garrett on 2026-11-14 and recorded in <c>Docs/ECOSYSTEM.md</c> §0:</para>
    /// <list type="bullet">
    /// <item><b>Any host.</b> <see cref="Fauna.TryPossess"/> takes any diet (a predator, a pollinator), always from
    /// another holder ("any host should always be available"). A worm segment hands the possession to its colony's head,
    /// a swarm member to its whole murmuration (<see cref="Fauna.PossessionHost"/>).</item>
    /// <item><b>Every shield is food</b> for the host while possessed ("shielded prisms and supershielded prisms always
    /// edible"): <see cref="IFaunaHerd.EatsShields"/> is true and <c>Fauna.IsInedibleShield</c> also lets super-shielded
    /// mass through, which the host's bite breaks first (<see cref="Prism.DeactivateShields()"/>, the Rhino sword's
    /// sanctioned sequence) and then eats.</item>
    /// </list>
    /// Everything else keeps the ecology's rules: the host keeps its starvation clock (<see cref="IFaunaLeash.Sustains"/> is
    /// false, so a host that is never fed starves and throws its rider), it eats with its own suction and fills its own
    /// stomach, its offspring are born wild, and a host that is eaten or shot dies the ordinary way.
    /// </summary>
    public interface IFaunaPossession : IFaunaHerd
    {
        /// <summary>The rider's team: the domain a host eats AS while possessed (it eats rival mass, never its rider's).</summary>
        Domains PossessorDomain { get; }

        /// <summary>The rider's name, credited with what its host eats and kills.</summary>
        string PossessorName { get; }

        /// <summary>How much faster than its own cruise a host whose body the possession cannot set a speed on swims (a
        /// murmuration's members, which swim at their own speeds): 1 = its own. The Jewel Wasp's Time element.</summary>
        float CruiseScale(Fauna host);
    }
}
