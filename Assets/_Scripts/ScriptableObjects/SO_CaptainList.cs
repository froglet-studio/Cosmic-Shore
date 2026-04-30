using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Captain List", menuName = "ScriptableObjects/Captain/CaptainList", order = 21)]
    [System.Serializable]
    public class SO_CaptainList : ScriptableObject
    {
        public List<SO_Captain> CaptainList;
    }
}
