using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Tournament List", menuName = "ScriptableObjects/Game/TournamentList")]
    public class SO_TournamentList : ScriptableObject
    {
        public List<SO_Tournament> Tournaments;
    }
}
