namespace CosmicShore.UI
{
    /// <summary>
    /// What a menu toast is saying, which picks its leading glyph (Docs/MENU_REDESIGN_B.md section 20):
    /// a lock for a refusal, a check for a success, the danger diamond for an error, a crystal for a
    /// goal. <see cref="None"/> draws the toast with no glyph - what a bare-string toast does.
    /// </summary>
    public enum ToastKind
    {
        None = 0,
        Refusal = 1,
        Success = 2,
        Error = 3,
        Goal = 4,
    }
}
