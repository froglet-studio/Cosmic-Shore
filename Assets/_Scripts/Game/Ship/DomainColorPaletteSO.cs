using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "DomainColorPalette", menuName = "CosmicShore/UI/Domain Color Palette")]
    public class DomainColorPaletteSO : ScriptableObject
    {
        [Header("Fallback colors per Domain (used only if Prism tint cannot be read)")]
        public Color unassigned = Color.gray;
        public Color jade  = new Color(0.4f, 1f, 0.6f);
        public Color ruby       = new Color(1f, 0.3f, 0.3f);
        public Color blue       = new Color(0.3f, 0.6f, 1f);
        public Color gold       = new Color(1f, 0.8f, 0.3f);
        public Color danger = new();
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