using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// On the KitKeycap prefab: hides the keycap where controls are not named
    /// (<see cref="KitInputDevice.ShowsControlHints"/> - a phone or tablet with no pad), and closes
    /// up the slot it sits in so a button does not keep an empty gap where its cap was.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Keycap Visibility")]
    public class KitKeycapVisibility : MonoBehaviour
    {
        [Tooltip("The layout element that keeps room for the cap - normally the button's KeycapSlot. " +
                 "Empty: the parent's, when the cap is the parent's only child.")]
        [SerializeField] LayoutElement collapse;

        CanvasGroup _group;

        void OnEnable()
        {
            if (!_group) _group = GetComponent<CanvasGroup>();
            if (!collapse && transform.parent && transform.parent.childCount == 1)
                transform.parent.TryGetComponent(out collapse);
            KitInputDevice.Listen(Apply);
            Apply();
        }

        void OnDisable() => KitInputDevice.Unlisten(Apply);

        void Apply()
        {
            bool shown = KitInputDevice.ShowsControlHints;
            if (_group)
            {
                _group.alpha = shown ? 1f : 0f;
                _group.blocksRaycasts = false;
                _group.interactable = false;
            }
            if (collapse) collapse.ignoreLayout = !shown;
        }
    }
}
