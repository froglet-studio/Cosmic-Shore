using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "DomainColorPalette", menuName = "CosmicShore/UI/Domain Color Palette")]
    public class DomainColorPaletteSO : ScriptableObject
    {
        [Header("Fallback colors per Domain (used only if Prism tint cannot be read)")]
        public Color unassigned = Color.gray;
        public Color jade;
        public Color ruby;
        public Color blue;
        public Color gold;
        public Color danger;
        public Color none       = Color.white;

        public Color Get(Domains d)
        {
            return d switch
            {
                Domains.Jade        => jade,
                Domains.Ruby        => ruby,
                Domains.Blue        => blue,
                Domains.Gold        => gold,
                Domains.None        => none,
                Domains.Unassigned  => unassigned,
                _                   => Color.white,
            };
        }
    }
}