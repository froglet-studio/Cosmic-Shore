using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    /// <summary>
    /// Represents an element for the purpose of displaying  in the UI
    /// </summary>
    [CreateAssetMenu(fileName = "New Element", menuName = "CosmicShore/Element", order = 29)]
    public class SO_Element : ScriptableObject
    {
        [SerializeField] public Element Element;
        [SerializeField] List<Sprite> ActiveIcons;
        [SerializeField] List<Sprite> InactiveIcons;

        public Sprite GetFullIcon(bool active)
        {
            return GetIcon(5, active);
        }

        public Sprite GetIcon(int level, bool active)
        {
            if (level < 0 || level > 5) throw new ArgumentException("Level must be between 0-5");

            if (active)
                return ActiveIcons[level];
            else
                return InactiveIcons[level];
        }
    }
}
