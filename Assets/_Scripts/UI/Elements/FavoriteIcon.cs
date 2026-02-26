using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Game.Managers;
namespace CosmicShore.UI.Elements
{
    public class FavoriteIcon : MonoBehaviour
    {
        [SerializeField] Sprite IconActive;
        [SerializeField] Sprite IconInActive;
        [SerializeField] Image IconImage;

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