using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    public class CrystalManager : MonoBehaviour
    {
        [SerializeField] Crystal crystal;
        [SerializeField] Vector3 crystalStartPosition;
        [SerializeField] bool scaleCrystalPositionWithIntensity;
        
        [SerializeField] IntVariable intensityLevelData;
        
        void Initialize()
        {
            crystal.transform.position = scaleCrystalPositionWithIntensity ? 
                crystalStartPosition * intensityLevelData : crystal.transform.position;
        }
    }
}