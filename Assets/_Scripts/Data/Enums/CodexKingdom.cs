namespace CosmicShore.Data
{
    // Always assign static numeric values - Unity serialization drift on enum reordering
    // silently rewrites every authored asset that stores one.

    /// <summary>
    /// The top-level division of the in-game encyclopedia.
    ///
    /// <para><b>Ethirion</b> is the player-facing name for a CRYSTAL. <b>Flora</b> and
    /// <b>Fauna</b> together are the player-facing <i>Ecology</i>. <b>Toy</b> is a freestyle
    /// station you fly into - and it is called a Toy here because that is what the platform
    /// calls it: this is the ONE kingdom whose name needs no translation. The split is
    /// deliberately the one a player can see - what you collect, what lives, what you play with -
    /// and NOT an implementation split: a crystal's impactor class (elemental / omni / team)
    /// decides who may collect it, which is mechanics, not encyclopedia content, and is never
    /// surfaced here.</para>
    ///
    /// <para><b>Named Toy, deliberately, everywhere.</b> An earlier pass called this kingdom
    /// "Tool" on the player-facing surface, on the Ethirion precedent. That was a mistake worth
    /// recording: <c>FrogletTools</c> are EDITOR tools, so the word already means something else
    /// in this repo, and a kingdom called Tool authored by a tool called the Codex reads as a
    /// tool listing tools. Toy is also simply what the thing is - CLAUDE.md's fundamental, the
    /// toybox, <c>ToyDefinitionSO</c> - so there was never a translation to make.</para>
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
        Toy = 3,
    }
}
