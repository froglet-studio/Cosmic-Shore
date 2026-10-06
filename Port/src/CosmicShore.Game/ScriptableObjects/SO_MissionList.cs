// Ported from Assets/_Scripts/ScriptableObjects/SO_MissionList.cs (Arc F 2b-iii) — verbatim.
using System.Collections.Generic;
using CosmicShore.Engine;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Mission List", menuName = "ScriptableObjects/Game/MissionList", order = 22)]
    [System.Serializable]
    public class SO_MissionList : ScriptableObject
    {
        public List<SO_Mission> Games;
    }
}
