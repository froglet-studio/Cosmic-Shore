using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Shows and hides a style-B part through its <see cref="CanvasGroup"/> - never SetActive
    /// (Docs/MENU_REDESIGN_B.md). A hidden part is invisible, takes no clicks and is skipped by
    /// gamepad navigation (a non-interactable group makes its Selectables non-interactable), and its
    /// OnEnable / OnDisable lifecycle never fires on a show or a hide.
    ///
    /// <para>A part that sits in a layout group also carries a <see cref="LayoutElement"/>; while it
    /// is hidden that element ignores layout, so the row or column closes up around it instead of
    /// keeping an empty gap.</para>
    ///
    /// <para>A hide first finishes the part's own <see cref="KitEntrance"/>. An entrance holds the
    /// group at alpha 0 through its delay, so a hide written then reads as "nothing changed" and the
    /// entrance fades the part back up to its old alpha - visible, but with raycasts and
    /// interactable still off. That is how the configure window's AI pilots row (Easy / Medium /
    /// Hard) came back dead on every card that does not offer it, after one that did.</para>
    /// </summary>
    public static class KitVisibility
    {
        public static void SetShown(CanvasGroup group, bool shown)
        {
            if (!group) return;
            if (!shown && group.TryGetComponent(out KitEntrance entrance)) entrance.Finish();
            group.alpha = shown ? 1f : 0f;
            group.interactable = shown;
            group.blocksRaycasts = shown;
            if (group.TryGetComponent(out LayoutElement layout))
                layout.ignoreLayout = !shown;
        }

        public static bool IsShown(CanvasGroup group) => group && group.alpha > 0f;
    }
}
