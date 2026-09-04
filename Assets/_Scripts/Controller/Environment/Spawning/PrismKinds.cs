using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Applies a <see cref="PrismKind"/> to a live <see cref="Prism"/>. Two entry points:
    /// <see cref="Apply"/> (additive, for a freshly-spawned prism - Plain leaves the prefab's baked
    /// state intact so the environment system's ShieldedSpawnablePrism keeps its shield) and
    /// <see cref="Retheme"/> (clear-then-apply, the reversible re-theme the microscene conveyor uses
    /// when it re-poses its fixed prism stock into a new arrangement).
    ///
    /// Standardised on the <b>state-machine</b> shield path
    /// (<see cref="Prism.ActivateShield"/> / <see cref="Prism.ActivateSuperShield"/> /
    /// <see cref="Prism.DeactivateShields"/>): a single <c>DeactivateShields()</c> reverses
    /// <i>every</i> shielded/supershielded state and keeps the AOE registry in sync. The
    /// super-shield state now engages the STELLATED octahedron (Stella Octangula - the Skim Race
    /// track look) through <c>PrismStateManager</c>, which also disengages it on the normal-state
    /// clear - so the reversibility that once required avoiding the stellated component holds for
    /// it too. (<c>SegmentSpawner.SuperShieldSpawnedPrisms</c> predates this and still pokes the
    /// component directly.)
    ///
    /// Collider budget: Plain/Danger ride the LOD-cullable BoxCollider; Shielded/SuperShielded swap
    /// to an always-on convex MeshCollider - keep them rare per scene (enforced by the palette caps).
    /// </summary>
    public static class PrismKinds
    {
        /// <summary>
        /// Additively theme a freshly-spawned prism. Call AFTER <see cref="Prism.Initialize"/>.
        /// Plain is a no-op so a prefab's baked shield/danger survives (the environment path).
        /// </summary>
        public static void Apply(Prism prism, PrismKind kind)
        {
            if (!prism) return;
            switch (kind)
            {
                case PrismKind.Danger: prism.MakeDangerous(); break;
                case PrismKind.Shielded: prism.ActivateShield(); break;
                case PrismKind.SuperShielded: prism.ActivateSuperShield(); break;
                // Plain: leave as-is.
            }
        }

        /// <summary>
        /// Clear any kind state back to a plain, damageable prism. Reverses Danger (flag +
        /// speed-debuff) and, via <see cref="Prism.DeactivateShields"/>, both Shielded and
        /// SuperShielded (repaints plain, disengages the shield collider, syncs the AOE registry).
        /// </summary>
        public static void Clear(Prism prism)
        {
            if (!prism || prism.prismProperties == null) return;
            if (prism.prismProperties.IsDangerous)
            {
                prism.prismProperties.IsDangerous = false;
                prism.prismProperties.speedDebuffAmount = 0f;
            }
            prism.DeactivateShields();
        }

        /// <summary>Clear then apply - the full reversible re-theme for a recycled/re-posed prism.</summary>
        public static void Retheme(Prism prism, PrismKind kind)
        {
            Clear(prism);
            Apply(prism, kind);
        }

        /// <summary>
        /// The kind a LIVE prism is currently wearing - the read half of <see cref="Apply"/>.
        /// Read off <c>prismProperties</c> rather than <c>PrismStateManager.CurrentState</c>
        /// because the flags are the authoritative record: spawners set them pre-Initialize and
        /// <c>Prism.Initialize</c> re-engages the state machine from them on every pool reuse.
        ///
        /// The three flags are mutually exclusive by construction (<c>MakeDangerous</c> clears
        /// both shields; <c>ActivateSuperShield</c> clears danger and shield), so the ordering
        /// only decides what a CORRUPT prism reports. It matches gameplay precedence:
        /// super-shield first, because that is the flag that makes a prism invulnerable and
        /// stops an AOE dead regardless of anything else set alongside it.
        /// </summary>
        public static PrismKind Of(Prism prism) => Of(prism ? prism.prismProperties : null);

        /// <summary>Kind of a bare property bag - the pure, testable half of
        /// <see cref="Of(Prism)"/>. Null (a prism that has not run Awake) reads Plain.</summary>
        public static PrismKind Of(PrismProperties props)
        {
            if (props == null) return PrismKind.Plain;
            if (props.IsSuperShielded) return PrismKind.SuperShielded;
            if (props.IsDangerous) return PrismKind.Danger;
            if (props.IsShielded) return PrismKind.Shielded;
            return PrismKind.Plain;
        }
    }
}
