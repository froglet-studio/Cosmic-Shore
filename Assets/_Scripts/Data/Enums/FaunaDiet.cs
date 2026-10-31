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
    //   Pollinator - PRODUCES rather than consumes (Garrett, 2026-10-31; Docs/ECOSYSTEM.md §66): a bee
    //               eats no flora and no prism mass; its visits hasten flora growth (Flora.Hasten),
    //               and what it carries home is mass stung off OPPOSING fauna (a transfer). A
    //               pollinator population is raised by its HIVE, which takes a FLORA slot in the
    //               spawn profile, so it counts against the flora budget, not the fauna one, and it
    //               never starves (a producer, like a plant). It is neither herbivore prey signal
    //               nor leashable (both read Diet == Herbivore).
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
        Pollinator = 2,
    }
}
