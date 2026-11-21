// Ported verbatim from Assets/_Scripts/ScriptableObjects/SO_GameList.cs
// (FormerlySerializedAs lives in CosmicShore.Engine — the UnityEngine.Serialization
// using is deleted per the README substitution table).
using System.Collections.Generic;
using CosmicShore.Engine;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Game List", menuName = "ScriptableObjects/Game/GameList", order = 21)]
    [System.Serializable]
    public class SO_GameList : ScriptableObject
    {
        [FormerlySerializedAs("GameList")]
        public List<SO_ArcadeGame> Games;
    }
}
