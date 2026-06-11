namespace CosmicShore.UI
{
    /// <summary>
    /// Implemented by menu screen MonoBehaviours that need notification
    /// when the ScreenSwitcher navigates to or away from them.
    /// </summary>
    public interface IScreen
    {
        /// <summary>Called when this screen becomes the active screen.</summary>
        void OnScreenEnter();

        /// <summary>Called when the user navigates away from this screen.</summary>
        void OnScreenExit();
    }
}
