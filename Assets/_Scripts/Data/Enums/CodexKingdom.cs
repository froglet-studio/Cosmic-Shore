namespace CosmicShore.Data
{
    // Always assign static numeric values - Unity serialization drift on enum reordering
    // silently rewrites every authored asset that stores one.

    /// <summary>
    /// The top-level division of the in-game encyclopedia.
    ///
    /// <para><b>Ethirion</b> is the player-facing name for a CRYSTAL. <b>Flora</b> and
    /// <b>Fauna</b> together are the player-facing <i>Ecology</i>. <b>Tool</b> is the
    /// player-facing name for a <i>Toy</i> - the freestyle stations you fly into. The split is
    /// deliberately the one a player can see - what you collect, what lives, what you play with -
    /// and NOT an implementation split: a crystal's impactor class (elemental / omni / team)
    /// decides who may collect it, which is mechanics, not encyclopedia content, and is never
    /// surfaced here.</para>
    ///
    /// <para><b>Naming hazard.</b> "Tool" here is a thing in the GAME. It has nothing to do with
    /// <c>FrogletTools</c>, which are editor tools. The codebase keeps calling the game object a
    /// <c>Toy</c> (the fundamental) precisely so the two never collide in code; only the
    /// player-facing surface says "Tool".</para>
    /// </summary>
    public enum CodexKingdom
    {
        /// <summary>A crystal. Charge / Mass / Space / Time / Omni.</summary>
        Ethirion = 0,

        /// <summary>A plant. Grows, reproduces, is grazed.</summary>
        Flora = 1,

        /// <summary>A creature. Feeds, starves, breeds, can be killed.</summary>
        Fauna = 2,

        /// <summary>
        /// A <b>Toy</b> - a freestyle station you fly into. No score, no end condition, nothing
        /// on a clock; a thing to play with indefinitely.
        /// </summary>
        Tool = 3,
    }
}
