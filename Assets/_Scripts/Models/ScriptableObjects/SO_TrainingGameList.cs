using CosmicShore;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Training Game List", menuName = "CosmicShore/Game/TrainingGameList", order = 21)]
[System.Serializable]
public class SO_TrainingGameList : ScriptableObject
{
    public List<SO_TrainingGame> GameList;
}