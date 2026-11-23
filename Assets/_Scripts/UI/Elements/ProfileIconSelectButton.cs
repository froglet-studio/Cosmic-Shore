using CosmicShore.Core;
using CosmicShore.UI;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
namespace CosmicShore.UI
{
    public class ProfileIconSelectButton : MonoBehaviour
    {
        [Inject] AudioSystem audioSystem;

        [Tooltip("Legacy: the ring enabled while this avatar is the pick. Unused when a Kit tile is wired.")]
        [SerializeField] Image BorderImage;
        [SerializeField] Image IconImage;
        [Tooltip("Style-B (Docs/MENU_REDESIGN_B.md section 15.2): the octagon tile - the pick is its selected look, " +
                 "its focus ring the pad's, and its click this button's OnClick (wired in code).")]
        [SerializeField] KitTile tile;
        [HideInInspector]
        public ProfileIconSelectView IconView;
        ProfileIcon profileIcon;
        AvatarRippleBorder _border;
        bool _selected;
        public ProfileIcon ProfileIcon {
            get => profileIcon;
            set
            {
                profileIcon = value;
                if (IconImage) IconImage.sprite = value.IconSprite;
            }
        }

        void Awake()
        {
            if (tile) tile.onClick.AddListener(OnClick);
            // Rev 127 D (Docs/MENU_REDESIGN_B.md section 217): every avatar is round, ringed and glowing; in the
            // picker the pick ripples in the selection colour and the rest carry the quiet ring.
            _border = AvatarRippleBorder.EnsureFor(IconImage);
            ApplyBorder();
        }

        void ApplyBorder()
        {
            if (_border == null) return;
            if (_selected) _border.ShowToken(UIColorToken.Selection);
            else _border.ShowUnknown();
        }

        public void OnClick()
        {
            // audioSystem is DI-injected; null only if this prefab instance wasn't injected.
            // Guard so a missed injection degrades to "no click sound" instead of an NRE that
            // blocks the avatar selection (the SelectIcon call below).
            if (audioSystem != null)
                audioSystem.PlayMenuAudio(MenuAudioCategory.OptionClick);

            if (IconView != null)
                IconView.SelectIcon(this, ProfileIcon);
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            ApplyBorder();
            if (tile) tile.IsSelected = selected;
            else if (BorderImage) BorderImage.enabled = selected;
        }
    }
}
