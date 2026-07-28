namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Per-vessel runtime state for the Rhino "energy sword". Implemented by
    /// <see cref="ShieldSkimmerScaleDriver"/> (the sword's brain, one per Rhino) and exposed
    /// through <see cref="Skimmer.SwordState"/> so the shared impact-effect ScriptableObjects
    /// — which are singletons and cannot hold per-vessel state — can read and drive it via
    /// <c>impactor.Skimmer.SwordState</c>. Null on every non-Rhino skimmer.
    ///
    /// The sword has NO damage gate: it always damages prisms on contact and always pops
    /// super-shielded prisms. "Energy" is the Rhino's Shield resource (normalized 0..1),
    /// banked per prism the sword destroys — it lengthens and heats the blade — and spent
    /// all at once when the sword collects an elemental crystal (a 3D burst + explosion
    /// scaled by the energy consumed). See <c>RHINO_ENERGY_SWORD.md</c>.
    /// </summary>
    public interface IRhinoSwordState
    {
        /// <summary>Current stored energy, normalized 0..1 (the Shield resource).</summary>
        float Energy01 { get; }

        /// <summary>Energy gained from destroying a prism, in normalized 0..1 units.</summary>
        void AddEnergy(float amount01);

        /// <summary>Called the instant the sword destroys a prism, for impact feedback:
        /// a blade flash pulse, plus a local camera shake when a super-shield popped.</summary>
        void NotifyPrismDestroyed(bool superShielded);

        /// <summary>Elemental-crystal hit: burst the blade in all three dimensions scaled by
        /// the current energy, then consume ALL of it (drain to 0). Full energy reproduces the
        /// authored max scale; less energy scales the burst down proportionally.</summary>
        void TriggerCrystalBurst();
    }
}
