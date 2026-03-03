using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Tournament", menuName = "ScriptableObjects/Game/Tournament")]
    public class SO_Tournament : ScriptableObject
    {
        [Header("Tournament Config")]
        [Tooltip("Display name shown in the tournament selection UI.")]
        public string TournamentName;

        [TextArea(2, 4)]
        [Tooltip("Description shown in the tournament selection UI.")]
        public string Description;

        [Tooltip("Icon shown in the tournament selection UI.")]
        public Sprite Icon;

        [Header("Rounds")]
        [Tooltip("Ordered list of game modes played in sequence. Each entry is an existing SO_ArcadeGame asset.")]
        public List<SO_ArcadeGame> Rounds;

        [Header("Scoring")]
        [Tooltip("Points awarded per placement. Index 0 = 1st place, index 1 = 2nd place, etc. Default: [4, 2, 1, 0].")]
        public List<int> PointsPerPlacement = new() { 4, 2, 1, 0 };

        [Header("Defaults")]
        [Range(1, 4)]
        [Tooltip("Default total player count (humans + AI).")]
        public int DefaultPlayerCount = 2;

        [Range(1, 4)]
        [Tooltip("Default intensity level for all rounds.")]
        public int DefaultIntensity = 1;

        public int GetPointsForPlacement(int placement)
        {
            if (PointsPerPlacement == null || PointsPerPlacement.Count == 0)
                return 0;

            int index = placement - 1;
            return index >= 0 && index < PointsPerPlacement.Count ? PointsPerPlacement[index] : 0;
        }
    }
}
