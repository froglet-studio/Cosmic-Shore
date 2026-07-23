namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Per-vessel runtime state for the Rhino "energy sword". Implemented by
    /// <see cref="ShieldSkimmerScaleDriver"/> (the sword's brain, one per Rhino) and exposed
    /// through <see cref="Skimmer.SwordState"/> so the shared impact-effect ScriptableObjects
    /// — which are singletons and cannot hold per-vessel state — can read and drive it via
    /// <c>impactor.Skimmer.SwordState</c>. Null on every non-Rhino skimmer.
    ///
    /// "Energy" is the Rhino's Shield resource (normalized 0..1). It is gained when a slash
    /// destroys a prism and spent two ways: a crystal hit (a 3D size burst + explosion scaled
    /// by energy, draining all of it) and energizing the blade (holding the lower/chop stance,
    /// costing 1/10 of max energy). See <c>RHINO_ENERGY_SWORD.md</c>.
    /// </summary>
    public interface IRhinoSwordState
    {
        /// <summary>True while the blade is energized — white, edge tracers lit, pops
        /// super-shielded prisms, and the slash cooldown is 0 (frenzy).</summary>
        bool IsEnergized { get; }

        /// <summary>Current stored energy, normalized 0..1 (the Shield resource).</summary>
        float Energy01 { get; }

        /// <summary>True when a slash may deal damage this frame: a single trigger is being
        /// pulled AND (the blade is energized OR the slash cooldown has elapsed).</summary>
        bool CanSlashDamage { get; }

        /// <summary>Called the instant a slash lands damage on a prism — starts the slash
        /// cooldown (0 while energized, the configured cooldown otherwise).</summary>
        void NotifySlashLanded();

        /// <summary>Energy gained from destroying a prism, in normalized 0..1 units.</summary>
        void AddEnergy(float amount01);

        /// <summary>Crystal hit: burst the blade in all three dimensions scaled by the current
        /// energy, then consume ALL of it (drain to 0). The magnitude at full energy reproduces
        /// the authored max scale; less energy scales the burst down proportionally.</summary>
        void TriggerCrystalBurst();

        /// <summary>Fed each frame by the swipe executor: true while the both-triggers
        /// "lower"/chop stance (the energize gesture) is held.</summary>
        void SetInStance(bool inStance);

        /// <summary>Fed each frame by the swipe executor: true while a single-trigger swipe
        /// (a slash) is being pulled.</summary>
        void SetSlashing(bool slashing);
    }
}
