// Ported verbatim from Assets/_Scripts/ScriptableObjects/SO_CaptainList.cs
// (CaptainManager unit 2026-07-10). Mechanical substitutions only (README).
using System.Collections.Generic;
using CosmicShore.Engine;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Captain List", menuName = "ScriptableObjects/Captain/CaptainList", order = 21)]
    [System.Serializable]
    public class SO_CaptainList : ScriptableObject
    {
        public List<SO_Captain> CaptainList;
    }
}
