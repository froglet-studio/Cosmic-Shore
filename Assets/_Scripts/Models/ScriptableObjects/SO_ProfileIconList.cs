using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    [CreateAssetMenu(fileName = "New Profile Icon List", menuName = "CosmicShore/ProfileIconList", order = 20)]
    public class SO_ProfileIconList : ScriptableObject
    {
        [SerializeField] public List<ProfileIcon> profileIcons;
    }

    [System.Serializable]
    public struct ProfileIcon
    {
        public string Name;
        public int Id;
        public Sprite IconSprite;

        public ProfileIcon(string name, int id, Sprite iconSprite )
        {
            Name = name;
            Id = id;
            IconSprite = iconSprite;
        }
    }
}
