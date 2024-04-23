using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Models.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Guide Upgrade", menuName = "CosmicShore/GuideUpgrade", order = 30)]
    [System.Serializable]
    public class SO_GuideUpgrade : ScriptableObject
    {
        [FormerlySerializedAs("vesselName")]
        [SerializeField] public string guideName;
        [SerializeField] public string description;
        [SerializeField] public Sprite image;
        [SerializeField] public Sprite icon;
        [SerializeField] public Element element;
        [SerializeField] public int upgradeLevel;
    }
}