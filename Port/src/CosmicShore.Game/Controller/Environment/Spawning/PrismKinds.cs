using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Applies a <see cref="PrismKind"/> to a live <see cref="Prism"/>. Two entry points:
    /// <see cref="Apply"/> (additive, for a freshly-spawned prism — Plain leaves the prefab's baked
    /// state intact so the environment system's ShieldedSpawnablePrism keeps its shield) and
    /// <see cref="Retheme"/> (clear-then-apply, the reversible re-theme the microscene conveyor uses
    /// when it re-poses its fixed prism stock into a new arrangement).
    ///
    /// Standardised on the <b>state-machine</b> shield path
    /// (<see cref="Prism.ActivateShield"/> / <see cref="Prism.ActivateSuperShield"/> /
    /// <see cref="Prism.DeactivateShields"/>), NOT the stellated component that
    /// <c>SegmentSpawner.SuperShieldSpawnedPrisms</c> pokes directly. One reason: a single
    /// <c>DeactivateShields()</c> then reverses <i>every</i> shielded/supershielded state and keeps
    /// the AOE registry in sync. The stellated path can only be reversed by its own matching
    /// <c>Disengage</c>; a mismatched clear silently leaves an always-on convex MeshCollider engaged
    /// (see <c>PrismStateManager</c>) — unacceptable for a pool that re-poses the same instances.
    ///
    /// Collider budget: Plain/Danger ride the LOD-cullable BoxCollider; Shielded/SuperShielded swap
    /// to an always-on convex MeshCollider — keep them rare per scene (enforced by the palette caps).
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

        /// <summary>Clear then apply — the full reversible re-theme for a recycled/re-posed prism.</summary>
        public static void Retheme(Prism prism, PrismKind kind)
        {
            Clear(prism);
            Apply(prism, kind);
        }
    }
}
