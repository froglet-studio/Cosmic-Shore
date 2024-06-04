using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Models.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Captain Upgrade", menuName = "CosmicShore/Captain/CaptainUpgrade", order = 3)]
    [System.Serializable]
    public class SO_CaptainUpgrade : ScriptableObject
    {
        [SerializeField] public SO_Captain captain;
        [SerializeField] public string description;
        [SerializeField] public Sprite image;
        [SerializeField] public Sprite icon;
        [SerializeField] public Element element;
        [SerializeField] public int upgradeLevel;
    }
}