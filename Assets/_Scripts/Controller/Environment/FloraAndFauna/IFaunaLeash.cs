using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A leash: something that steers a tamed creature and names the prism it eats next. The Angler holds one
    /// (<c>R_VesselActions/ANGLER.md</c>); the lab that designed it is <c>Docs/Studios/ANGLER_COMMAND_LAB.md</c>.
    ///
    /// <para><b>Behaviour only.</b> A leash bends where a creature swims and which edible prism its next mouthful
    /// faces. It never kills or recolours anything, and it cannot make inedible mass edible: the creature
    /// re-checks every ordered prism with its OWN edibility rule (shielded mass and the nucleus interior stay off the
    /// menu) and eats it with its OWN mouthful. A leashed creature keeps its starvation clock, so a squad that is never
    /// ordered to eat starves the way any creature does - with ONE fenced exception, <see cref="Sustains"/>.</para>
    /// </summary>
    public interface IFaunaLeash
    {
        /// <summary>
        /// Where the leashed creature should swim this frame, how fast (u/s), and how sharply it turns toward it
        /// (1/s; 0 = the creature's own turn rate). False = the holder has no steer for it this frame, and the creature
        /// keeps its own steering.
        /// </summary>
        bool TrySteer(Fauna creature, out Vector3 goal, out float speed, out float turnRate);

        /// <summary>The prism this creature is ordered to eat next, or null. A leashed creature with no order does
        /// not graze.</summary>
        Prism OrderedMeal(Fauna creature);

        /// <summary>The creature has left the leash (it died, was destroyed, or was unleashed by its own lifecycle).</summary>
        void Released(Fauna creature);

        /// <summary>
        /// True while the holder keeps this creature from STARVING: its KEEPER, the one creature an Angler always leads
        /// (the designer, 2026-10-16; <c>R_VesselActions/ANGLER.md</c> §1b). An AUTHORIZED EXCEPTION to "populations are
        /// bounded by consumption + starvation" (<c>Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md</c>, "No imposed death";
        /// <c>Docs/ECOSYSTEM.md</c> §0 and §61.4), fenced: its one reader is <see cref="Fauna"/>'s starvation test, a holder
        /// sustains at most ONE creature, and only starvation is waived - a keeper that is eaten or shot dies the ordinary
        /// way, heart and wither included. Not a precedent: a second holder that wants this needs its own sign-off.
        /// </summary>
        bool Sustains(Fauna creature);
    }
}
