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
}
