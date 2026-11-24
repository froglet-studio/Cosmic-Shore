namespace CosmicShore.UI
{
    /// <summary>
    /// One extra choice the running game may put in the pause window, above Settings: a voyage's "Complete and set sail"
    /// once its Crystal Run goal is met (Shombith, 2026-11-16). The pause window asks <see cref="Current"/> each time it
    /// opens; an empty <see cref="Label"/> hides the button. The label is the provider's authored text.
    /// </summary>
    public interface IPauseMenuAction
    {
        /// <summary>The button's words now, or empty for no button.</summary>
        string PauseActionLabel { get; }

        /// <summary>The button was pressed (the pause window has already closed and the game resumed).</summary>
        void OnPauseAction();
    }

    /// <summary>The one provider the pause window reads (<see cref="PauseMenu"/>).</summary>
    public static class PauseMenuAction
    {
        public static IPauseMenuAction Current { get; set; }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        /// <summary>Clear <see cref="Current"/> when it is still <paramref name="provider"/>.</summary>
        public static void Release(IPauseMenuAction provider)
        {
            if (ReferenceEquals(Current, provider)) Current = null;
        }

        /// <summary>The label to show now, or empty.</summary>
        public static string Label => Current?.PauseActionLabel ?? string.Empty;
    }

    /// <summary>
    /// A running game's own pause screen, shown in place of the shared pause window (Frogma Rev 134 Pause A: a voyage
    /// pauses on its chart). <see cref="PauseMenu"/> opens it whenever it would show its window and closes it whenever it
    /// hides; the window stays up but hidden and silent, so pausing, Settings and leaving keep the window's rules. The
    /// screen calls the menu's <see cref="PauseMenu.OnClickResumeGameButton"/>, <see cref="PauseMenu.OnClickSettings"/>
    /// and <see cref="PauseMenu.OnClickMainMenu"/>.
    /// </summary>
    public interface IPauseMenuStage
    {
        /// <summary>False when the game has nothing of its own to show now: the window shows instead.</summary>
        bool Wanted { get; }

        /// <summary>The pause opened: show the screen and take the pad's focus.</summary>
        void Open(PauseMenu menu);

        /// <summary>The pause closed.</summary>
        void Close();

        /// <summary>Settings opened over the pause (true) or closed again (false): step aside, then take the focus back.</summary>
        void Cover(bool covered);
    }

    /// <summary>The one pause screen the pause window yields to (null: the window itself).</summary>
    public static class PauseMenuStage
    {
        public static IPauseMenuStage Current { get; set; }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        public static void Release(IPauseMenuStage stage)
        {
            if (ReferenceEquals(Current, stage)) Current = null;
        }
    }
}
