using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore
{
    public class FavoriteIcon : MonoBehaviour
    {
        [SerializeField] Sprite IconActive;
        [SerializeField] Sprite IconInActive;
        Image IconImage;

        bool favorited;
        public bool Favorited
        {
            get { return favorited; }
            set
            {
                favorited = value;
                UpdateIcon();
            }
        }

        void Start()
        {
            IconImage = GetComponent<Image>();
        }

        void UpdateIcon()
        {
            IconImage.sprite = Favorited ? IconActive : IconInActive;
        }
    }
}