using System.Collections.Generic;
using CosmicShore.Models;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Captain List", menuName = "CosmicShore/Captain/CaptainList", order = 21)]
    [System.Serializable]
    public class SO_CaptainList : ScriptableObject
    {
        public List<SO_Captain> CaptainList;
    }
}
