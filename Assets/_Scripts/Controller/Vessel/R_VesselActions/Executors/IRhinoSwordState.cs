using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The energize ritual's phases, driven by <see cref="ShieldSkimmerScaleDriver"/> and
    /// read by <see cref="RhinoSwordFXController"/> for the blade's look:
    /// Idle → (hold the both-triggers chop stance) Charging → (hold met) Energized —
    /// danger red, pops super-shielded prisms, stays lit for a tail after leaving the
    /// stance → Cooldown → Idle.
    /// </summary>
    public enum RhinoSwordEnergizePhase
    {
        Idle = 0,
        Charging = 1,
        Energized = 2,
        Cooldown = 3
    }

    /// <summary>
    /// Per-vessel runtime state for the Rhino's sword. Implemented by
    /// <see cref="ShieldSkimmerScaleDriver"/> (the sword's brain, one per Rhino) and exposed
    /// through <see cref="Skimmer.SwordState"/> so the shared impact-effect ScriptableObjects
    /// — which are singletons and cannot hold per-vessel state — can read and drive it via
    /// <c>impactor.Skimmer.SwordState</c>. Null on every non-Rhino skimmer.
    ///
    /// The sword's ORDINARY cutting has NO gate: it always damages prisms on contact
    /// (normal prisms explode, shielded prisms lose their shield). The one gated act is
    /// popping a SUPER-shielded prism, which requires the Space level-5 upgrade
    /// (<see cref="SuperShieldUpgradeActive"/>) and the blade to be ENERGIZED: hold the
    /// both-triggers lower/chop stance (<see cref="SetInStance"/>) to charge; the blade
    /// ignites, stays lit for a tail after leaving the stance, then cools. There is no energy
    /// meter (discarded 2026-10-26): the cooldown is the only limit. An OMNI crystal SURGES the
    /// blade (energized with no stance and a ring of swords round the hull while it lasts -
    /// <see cref="Surge"/>). See <c>RHINO_ENERGY_SWORD.md</c>.
    /// </summary>
    public interface IRhinoSwordState
    {
        /// <summary>True while the blade is energized — danger red, and the supershield
        /// key is live (contact pops super-shielded prisms instead of binding in them).</summary>
        bool IsEnergized { get; }

        /// <summary>True when this vessel has the super-shield upgrade (the Rhino's Space level-5,
        /// Shield Breaker, 2026-10-26). Without it the blade binds in super-shielded prisms,
        /// energized or not; with it an energized blade pops them.</summary>
        bool SuperShieldUpgradeActive { get; }

        /// <summary>Called the instant the sword destroys a prism, for impact feedback:
        /// a blade flash + a crackle spark at the blade point nearest the prism, plus a
        /// local camera shake when a super-shield popped.</summary>
        void NotifyPrismDestroyed(bool superShielded, Vector3 prismWorldPosition);

        /// <summary>Called when a NON-energized blade touches a super-shielded prism (the
        /// BounceBack recoil): a dim spark at the contact, teaching that the blade needs
        /// the energize stance to pop this.</summary>
        void NotifyPopDenied(Vector3 prismWorldPosition);

        /// <summary>True while an omni-crystal SURGE holds the blade energized (<see cref="Surge"/>).
        /// Replicated with the energize phase, so every peer lays the surge's afterimages.</summary>
        bool IsSurging { get; }

        /// <summary>
        /// The omni-crystal pickup: hold the blade ENERGIZED for <paramref name="seconds"/> with no stance -
        /// overriding a cooldown - while a ring of trigger-driven copies of the blade surrounds the hull
        /// and the pilot's camera pulls back onto it (<see cref="RhinoSurgeSwordRing"/>), and the Mass
        /// afterimages, which otherwise wait for the energize to END, lay throughout.
        /// A no-op on a peer that does not own the sword's state (the owner's surge replicates).
        /// </summary>
        void Surge(float seconds);

        /// <summary>True when <paramref name="prism"/> is one of THIS sword's own afterimages. The
        /// blade cuts them like any prism; the AI's sword hand reads this only to aim at something
        /// else first. (Until 2026-10-17 the damage effect skipped them outright, which made the
        /// Mass ability's own mass immune to its pilot - reversed by design.)</summary>
        bool IsOwnAfterimage(Prism prism);

        /// <summary>Fed each frame by the swipe executor: true while the both-triggers
        /// "lower"/chop stance (the energize gesture) is held.</summary>
        void SetInStance(bool inStance);
    }
}
