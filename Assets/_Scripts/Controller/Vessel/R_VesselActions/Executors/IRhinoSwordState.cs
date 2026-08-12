using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The energize ritual's phases, driven by <see cref="ShieldSkimmerScaleDriver"/> and
    /// read by <see cref="RhinoSwordFXController"/> for the blade's look:
    /// Idle → (hold the both-triggers chop stance) Charging → (hold met) Energized —
    /// white-hot, pops super-shielded prisms, stays lit for a tail after leaving the
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
    /// Per-vessel runtime state for the Rhino "energy sword". Implemented by
    /// <see cref="ShieldSkimmerScaleDriver"/> (the sword's brain, one per Rhino) and exposed
    /// through <see cref="Skimmer.SwordState"/> so the shared impact-effect ScriptableObjects
    /// — which are singletons and cannot hold per-vessel state — can read and drive it via
    /// <c>impactor.Skimmer.SwordState</c>. Null on every non-Rhino skimmer.
    ///
    /// The sword's ORDINARY cutting has NO gate: it always damages prisms on contact
    /// (normal prisms explode, shielded prisms lose their shield). The one gated act is
    /// popping a SUPER-shielded prism, which requires the blade to be ENERGIZED: hold the
    /// both-triggers lower/chop stance (<see cref="SetInStance"/>) to charge; the blade
    /// ignites, stays lit for a tail after leaving the stance, then cools. "Energy" is the
    /// Rhino's Shield resource (normalized 0..1), banked per prism the sword destroys,
    /// spent as a fraction on each energize and drained whole by an elemental-crystal
    /// burst. See <c>RHINO_ENERGY_SWORD.md</c>.
    /// </summary>
    public interface IRhinoSwordState
    {
        /// <summary>True while the blade is energized — white-hot, and the supershield
        /// key is live (contact pops super-shielded prisms instead of bouncing).</summary>
        bool IsEnergized { get; }

        /// <summary>Current stored energy, normalized 0..1 (the Shield resource).</summary>
        float Energy01 { get; }

        /// <summary>Energy gained from destroying a prism, in normalized 0..1 units.</summary>
        void AddEnergy(float amount01);

        /// <summary>Called the instant the sword destroys a prism, for impact feedback:
        /// a blade flash + a crackle spark at the blade point nearest the prism, plus a
        /// local camera shake when a super-shield popped.</summary>
        void NotifyPrismDestroyed(bool superShielded, Vector3 prismWorldPosition);

        /// <summary>Called when a NON-energized blade touches a super-shielded prism (the
        /// BounceBack recoil): a dim spark at the contact, teaching that the blade needs
        /// the energize stance to pop this.</summary>
        void NotifyPopDenied(Vector3 prismWorldPosition);

        /// <summary>Elemental-crystal hit: burst the blade in all three dimensions scaled by
        /// the current energy, then consume ALL of it (drain to 0). Full energy reproduces the
        /// authored max scale; less energy scales the burst down proportionally.</summary>
        void TriggerCrystalBurst();

        /// <summary>Fed each frame by the swipe executor: true while the both-triggers
        /// "lower"/chop stance (the energize gesture) is held.</summary>
        void SetInStance(bool inStance);
    }
}
