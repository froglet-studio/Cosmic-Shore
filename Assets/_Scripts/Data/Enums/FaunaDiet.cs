namespace CosmicShore.Data
{
    // Always assign static numeric values; Unity serialization drift on enum reordering
    // breaks scene-wired references and SOAP asset references silently.
    //
    // A fauna's diet - "what counts as prey." This is the single seam the predator /
    // herbivore split turns on (Docs/ECOSYSTEM.md §7, §10 step 2):
    //
    //   Herbivore - consumes prism MASS: opposing-domain prisms (flora canopy + vessel
    //               trails). This is the original, default fauna behavior; every existing
    //               fauna prefab is a Herbivore unless explicitly changed.
    //   Predator  - consumes herbivore FAUNA. Ignores prism mass for feeding. Starves
    //               (the shared Fauna starvation clock) when no herbivores are reachable.
    //
    // Both diets share the same starvation-based population bound, so layering a Predator
    // on top of Herbivores yields a two-tier food web (flora -> herbivores -> predators)
    // that oscillates by itself (Lotka-Volterra) instead of via a hard-coded culler.
    // Mass stays conserved: the only prism sinks are still active forces - herbivore
    // consumption and vessel abilities (Docs/ECOSYSTEM.md §0).
    public enum FaunaDiet
    {
        Herbivore = 0,
        Predator = 1,
    }
}
