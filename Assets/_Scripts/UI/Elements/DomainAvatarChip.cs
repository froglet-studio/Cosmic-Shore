using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One avatar slot inside a domain button's avatar strip. Pools are managed by
    /// <see cref="DomainInfoData"/> - never instantiate or destroy these directly.
    ///
    /// <para>A chip standing for a PLACED AI (the launch panel's Add AI mode) can carry a ✕ -
    /// see <see cref="SetKickable"/>. Author the ✕ art as a Button on the prefab and wire
    /// <see cref="kickButton"/>; an unwired chip builds a plain functional ✕ from primitives so
    /// the feature works before the art lands, and steps aside the moment the field is wired.</para>
    /// </summary>
    public class DomainAvatarChip : MonoBehaviour
    {
        [SerializeField] Image avatarImage;
        [Tooltip("Optional outline / ring GameObject enabled only for the local player's chip.")]
        [SerializeField] GameObject localPlayerOutline;

        [Header("Kick")]
        [Tooltip("Optional ✕ shown on a kickable AI chip (Add AI placements, host only). Leave " +
                 "empty and a plain generated ✕ stands in until the art is authored.")]
        [SerializeField] Button kickButton;

        System.Action _onKick;
        Button _generatedKick;
        bool _kickWired;

        public void Set(Sprite sprite, bool isLocal)
        {
            // Keep the Image enabled even when sprite is null - that way a chip with
            // a missing/loading avatar still shows the prefab's placeholder sprite
            // instead of disappearing entirely.
            if (avatarImage)
            {
                if (sprite != null)
                    avatarImage.sprite = sprite;
                avatarImage.enabled = true;
            }

            if (localPlayerOutline)
                localPlayerOutline.SetActive(isLocal);

            // Chips are reused: a fresh Set is a fresh identity, so any kick state from the
            // previous occupant is stale until the owner re-asks for one.
            SetKickable(false);

            gameObject.SetActive(true);
        }

        /// <summary>
        /// Show or hide the chip's ✕. <paramref name="onKick"/> fires on click; the caller owns
        /// what a kick MEANS (the modal removes the placement - there is no AI object yet).
        /// </summary>
        public void SetKickable(bool kickable, System.Action onKick = null)
        {
            _onKick = kickable ? onKick : null;

            var button = kickButton ? kickButton : _generatedKick;
            if (!button && kickable)
                button = _generatedKick = BuildFallbackKick();
            if (!button) return;

            if (!_kickWired)
            {
                button.onClick.AddListener(HandleKickClicked);
                _kickWired = true;
            }

            button.gameObject.SetActive(kickable);
        }

        void HandleKickClicked() => _onKick?.Invoke();

        /// <summary>
        /// A functional ✕ built from primitives - a small dark disc with two crossed bars -
        /// so a kickable chip works before any art is authored. Replace it by wiring
        /// <see cref="kickButton"/> on the prefab; this never builds while that is set.
        /// </summary>
        Button BuildFallbackKick()
        {
            var go = new GameObject("KickX (generated)", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);   // top-right corner
            rect.pivot = new Vector2(0.75f, 0.75f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(16f, 16f);

            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.75f, 0.12f, 0.12f, 0.95f);

            for (int i = 0; i < 2; i++)
            {
                var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
                var barRect = (RectTransform)bar.transform;
                barRect.SetParent(rect, false);
                barRect.sizeDelta = new Vector2(10f, 2f);
                barRect.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 45f : -45f);
                bar.GetComponent<Image>().color = Color.white;
                bar.GetComponent<Image>().raycastTarget = false;
            }

            return go.GetComponent<Button>();
        }

        public void Hide()
        {
            SetKickable(false);
            gameObject.SetActive(false);
        }
    }
}
