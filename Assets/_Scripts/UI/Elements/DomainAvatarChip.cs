using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One avatar slot inside a domain button's avatar strip. Pools are managed by
    /// <see cref="DomainInfoData"/> — never instantiate or destroy these directly.
    /// </summary>
    public class DomainAvatarChip : MonoBehaviour
    {
        [SerializeField] Image avatarImage;
        [Tooltip("Optional outline / ring GameObject enabled only for the local player's chip.")]
        [SerializeField] GameObject localPlayerOutline;

        public void Set(Sprite sprite, bool isLocal)
        {
            if (avatarImage)
            {
                avatarImage.sprite = sprite;
                avatarImage.enabled = sprite != null;
            }

            if (localPlayerOutline)
                localPlayerOutline.SetActive(isLocal);

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
