namespace CosmicShore.Utility
{
    /// <summary>
    /// How many vessels a capture concept frames.
    ///
    /// <para>The two are not variations of one solve: a solo shot is a spherical offset around one
    /// subject, while a pair shot is constrained to the perpendicular bisector plane of the two so
    /// both land at the same distance from the lens. They are separate members because a concept
    /// must declare which guarantee it is asking for — a Pair concept is simply not drawn when the
    /// arena holds no pair, rather than quietly falling back to photographing one ship.</para>
    /// </summary>
    public enum ScreenshotFramingKind
    {
        /// <summary>The local pilot's vessel, alone.</summary>
        Solo = 0,

        /// <summary>Two vessels flying near each other, framed identically.</summary>
        Pair = 1,
    }
}
