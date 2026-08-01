namespace CosmicShore.UI
{
    /// <summary>
    /// Implemented by a panel living inside a <see cref="ModalWindowManager"/>
    /// that needs to know when its modal is actually shown or hidden.
    ///
    /// <para>
    /// <b>Why this exists.</b> <c>ModalWindowManager</c> hides a modal by fading
    /// its <c>CanvasGroup</c> - <c>DisableWindow()</c> sets alpha /
    /// blocksRaycasts / interactable to 0 - and NEVER calls
    /// <c>SetActive(false)</c>. Its <c>Start</c> says so explicitly: "Parent
    /// containers stay active so OnEnable/OnDisable lifecycle fires for all
    /// children." The consequence is the opposite of what that comment
    /// suggests for the panel itself: because the GameObject is never
    /// deactivated, <c>OnEnable</c> fires exactly ONCE per scene load and never
    /// again when the user opens the modal.
    /// </para>
    ///
    /// <para>
    /// For a data-bound panel that is a real bug. <c>ArcadeLobbyList</c> did its
    /// entire enable-time bootstrap - subscribe, populate, and a
    /// <c>ForceRefreshNow()</c> pull of fresh lobby state - in <c>OnEnable</c>,
    /// so from the first frame of Menu_Main onwards it never re-read anything on
    /// open. Any write to <c>HostConnectionDataSO</c> that does not raise a SOAP
    /// event (the local player's display name and avatar are plain field
    /// assignments) was invisible to it forever.
    /// </para>
    ///
    /// <para>
    /// This is a parent-to-child dispatch inside a single prefab hierarchy - the
    /// same shape as <c>ScreenSwitcher</c> driving <c>IScreen.OnScreenEnter</c> -
    /// not cross-system communication, so it does not belong on a SOAP channel.
    /// </para>
    ///
    /// <para>
    /// Implementations must be idempotent and safe to call in either order:
    /// panels typically route <c>OnEnable</c>/<c>OnDisable</c> to the same
    /// methods so they also work in scenes where the object IS toggled.
    /// </para>
    /// </summary>
    public interface IModalPanel
    {
        /// <summary>Modal became visible. Subscribe and re-read from scratch.</summary>
        void OnModalOpened();

        /// <summary>Modal was dismissed. Unsubscribe.</summary>
        void OnModalClosed();
    }
}
