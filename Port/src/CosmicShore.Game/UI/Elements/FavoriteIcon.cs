// Ported from Assets/_Scripts/UI/Elements/FavoriteIcon.cs (Arc F 2b-iii) — verbatim;
// UnityEngine / UnityEngine.UI → CosmicShore.Engine / CosmicShore.Engine.UI.
using CosmicShore.Engine;
using CosmicShore.Engine.UI;
using CosmicShore.Gameplay;
namespace CosmicShore.UI
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
