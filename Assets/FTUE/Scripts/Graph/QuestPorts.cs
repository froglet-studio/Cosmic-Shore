namespace CosmicShore.Core
{
    /// <summary>
    /// Canonical output-port names for quest graph edges.
    ///
    /// Most nodes emit a single <see cref="Next"/> port. Branching nodes expose
    /// additional named ports (e.g. <see cref="OnTimeout"/>) so the authored graph
    /// can route "the player did X" separately from "the player idled / backed out".
    /// Ports are matched by string so new branch shapes never require a schema change.
    /// </summary>
    public static class QuestPorts
    {
        /// <summary>Default linear continuation.</summary>
        public const string Next = "next";

        /// <summary>Optional branch taken when a wait node times out / the player idles.</summary>
        public const string OnTimeout = "onTimeout";

        /// <summary>Convenience array for the common single-output node.</summary>
        public static readonly string[] NextOnly = { Next };
    }
}
