using UnityEngine;
using UnityEngine.EventSystems;

namespace CosmicShore.UI
{
    /// <summary>
    /// Opens <see cref="FriendProfilePopup"/> for a row or seat (Rev 7, Docs/MENU_REDESIGN_B.md §47.3; the owner,
    /// 2026-10-17: "once we hover over their picture we should be able to see their profile as a popup"). On the avatar
    /// it answers the pointer; on the row's buttons it answers pad focus, so a pad player sees the same card as they
    /// move down the list. A click also selects a button, so a selection the pointer made is left to the hover.
    /// </summary>
    public class FriendProfileHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [Tooltip("The row or seat whose pilot the card shows (an IFriendProfileSource: KitOnlineRow, KitPartySlot).")]
        [SerializeField] MonoBehaviour source;
        [Tooltip("What the card sits beside: the pilot's avatar.")]
        [SerializeField] RectTransform anchor;

        IFriendProfileSource Source => source as IFriendProfileSource;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Source != null && FriendProfilePopup.Active) FriendProfilePopup.Active.Hover(Source, anchor);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (Source != null && FriendProfilePopup.Active) FriendProfilePopup.Active.Unhover(Source);
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (eventData is PointerEventData) return;
            if (Source != null && FriendProfilePopup.Active) FriendProfilePopup.Active.Focus(Source, anchor);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (Source != null && FriendProfilePopup.Active) FriendProfilePopup.Active.Unfocus(Source);
        }

        void OnDisable()
        {
            if (Source != null && FriendProfilePopup.Active) FriendProfilePopup.Active.Release(Source);
        }
    }
}
