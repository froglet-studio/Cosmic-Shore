using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    [CreateAssetMenu(fileName = "New Mission List", menuName = "CosmicShore/Game/MissionList", order = 22)]
    [System.Serializable]
    public class SO_MissionList : ScriptableObject
    {
        public List<SO_Mission> Games;
    }
}