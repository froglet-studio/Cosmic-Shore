namespace CosmicShore.Data
{
    // Always assign static numeric values - Unity serialization drift on enum reordering
    // silently rewrites every authored asset that stores one.

    /// <summary>
    /// The top-level division of the in-game encyclopedia.
    ///
    /// <para><b>Ethirion</b> is the player-facing name for a CRYSTAL. <b>Flora</b> and
    /// <b>Fauna</b> together are the player-facing <i>Ecology</i>. The split is deliberately the
    /// one a player can see - what you collect vs. what lives - and NOT an implementation split:
    /// a crystal's impactor class (elemental / omni / team) decides who may collect it, which is
    /// mechanics, not encyclopedia content, and is never surfaced here.</para>
    /// </summary>
    public enum CodexKingdom
    {
        /// <summary>A crystal. Charge / Mass / Space / Time / Omni.</summary>
        Ethirion = 0,

        /// <summary>A plant. Grows, reproduces, is grazed.</summary>
        Flora = 1,

        /// <summary>A creature. Feeds, starves, breeds, can be killed.</summary>
        Fauna = 2,
    }
}
