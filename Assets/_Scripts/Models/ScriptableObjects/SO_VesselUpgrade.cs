using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Models.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Vessel Upgrade", menuName = "CosmicShore/VesselUpgrade", order = 30)]
    [System.Serializable]
    public class SO_VesselUpgrade : ScriptableObject
    {
        [SerializeField] public string vesselName;
        [SerializeField] public string description;
        [SerializeField] public Sprite image;
        [SerializeField] public Sprite icon;
        [SerializeField] public Element element;
        [SerializeField] public int upgradeLevel;
    }
}