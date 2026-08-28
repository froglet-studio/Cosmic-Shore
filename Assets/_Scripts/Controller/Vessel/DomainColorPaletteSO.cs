using CosmicShore.Data;

﻿using UnityEngine;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "DomainColorPalette", menuName = "ScriptableObjects/UI/Domain Color Palette")]
    public class DomainColorPaletteSO : ScriptableObject
    {
        // Blue is the "no team / unassigned / neutral entity" sentinel and is used
        // anywhere code previously asked for a None or Unassigned color.
        [Header("Fallback colors per Domain (used only if Prism tint cannot be read)")]
        public Color jade;
        public Color ruby;
        public Color gold;
        public Color blue;
        public Color amethyst;
        public Color danger;

        public Color Get(Domains d)
        {
            return d switch
            {
                Domains.Jade => jade,
                Domains.Ruby => ruby,
                Domains.Gold => gold,
                Domains.Blue => blue,
                Domains.Amethyst => amethyst,
                _            => Color.white,
            };
        }
    }
}
