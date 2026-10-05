namespace CosmicShore.Data
{
    // Remember folks, only you can prevent Unity from arbitrarily swapping enum values in files.
    // Always assign a static numeric value to your enum types.

    /// <summary>
    /// WHO controls a cell before anybody has claimed it - authored per CELL on
    /// <c>CellConfigDataSO.InitialControllingDomain</c> (Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md:
    /// a biome's STARTING state is authored data, not a runtime call). Fauna still spawn in exactly
    /// ONE colour, the controller's; this only answers "who is the controller" for the window in
    /// which the cell's own prism-count leader (<c>Cell.DominantDomain</c>) is still empty. The
    /// moment a real leader exists, it wins.
    /// </summary>
    public enum InitialControllingDomain
    {
        /// <summary>Nothing authored: the legacy fallbacks (gameData's volume leader, then the local
        /// pilot's own domain, then Jade). In solo freestyle that seeds the cell in the PILOT's
        /// colour - a friendly cell.</summary>
        Unset = 0,

        /// <summary>A playable domain other than the authority's local pilot's, picked
        /// deterministically (<see cref="CellControlRules.Opposing"/>). The cell starts hostile to
        /// whoever is flying it solo; claiming the nucleus is how the pilot takes it.</summary>
        OpposingLocalPilot = 1,

        /// <summary>A fixed starting controller.</summary>
        Jade = 101,
        Ruby = 102,
        Gold = 104,
    }

    /// <summary>
    /// The controlling-domain resolver. Pure (no Unity) so Tools/Build/cell_control_harness runs the
    /// game's own code. <c>Cell.ControllingDomain</c> feeds it the cell's live reads.
    /// </summary>
    public static class CellControlRules
    {
        /// <summary>
        /// The domain a cell's fauna spawn in. Never returns <see cref="Domains.Blue"/> (the
        /// "no team" sentinel). Order:
        /// 1. <paramref name="dominant"/> - the cell's own prism-count leader;
        /// 2. <paramref name="startingController"/> - the authored starting controller
        ///    (<see cref="StartingController"/>; Blue when the cell authors none);
        /// 3. gameData's controlling team by remaining volume, when it holds any;
        /// 4. <paramref name="localPilot"/> - the local pilot's own domain;
        /// 5. Jade.
        /// The authored start sits ABOVE gameData's volume leader on purpose: that leader is the
        /// whole match's laid volume, not this cell's claim, and in solo freestyle it is always the
        /// pilot - ranking it first would hand the pilot the cell the moment they laid any trail.
        /// </summary>
        public static Domains ControllingDomain(Domains dominant, Domains startingController,
            Domains volumeLeader, float volumeLeaderVolume, Domains localPilot)
        {
            if (IsPlayable(dominant)) return dominant;
            if (IsPlayable(startingController)) return startingController;
            if (IsPlayable(volumeLeader) && volumeLeaderVolume > 0f) return volumeLeader;
            if (IsPlayable(localPilot)) return localPilot;
            return Domains.Jade;
        }

        /// <summary>
        /// The authored starting controller, resolved against the AUTHORITY's local pilot (the
        /// host's, on a networked cell - <c>CellNetworkSync</c> replicates the server's answer so
        /// every peer spawns the same colour). <see cref="Domains.Blue"/> for
        /// <see cref="InitialControllingDomain.Unset"/> or an unknown value.
        /// </summary>
        public static Domains StartingController(InitialControllingDomain rule, Domains localPilot) => rule switch
        {
            InitialControllingDomain.OpposingLocalPilot => Opposing(localPilot),
            InitialControllingDomain.Jade => Domains.Jade,
            InitialControllingDomain.Ruby => Domains.Ruby,
            InitialControllingDomain.Gold => Domains.Gold,
            _ => Domains.Blue,
        };

        /// <summary>
        /// The next playable domain after <paramref name="pilot"/> in Jade -> Ruby -> Gold -> Jade
        /// order. An unknown pilot (Blue - a dedicated server, or no pilot yet) is treated as the
        /// Jade default every unpicked pilot flies, so the answer is Ruby.
        /// </summary>
        public static Domains Opposing(Domains pilot) => pilot switch
        {
            Domains.Ruby => Domains.Gold,
            Domains.Gold => Domains.Jade,
            _ => Domains.Ruby,
        };

        static bool IsPlayable(Domains d) => d == Domains.Jade || d == Domains.Ruby || d == Domains.Gold;
    }
}
