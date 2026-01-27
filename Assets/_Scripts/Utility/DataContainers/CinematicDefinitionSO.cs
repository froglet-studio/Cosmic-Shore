using UnityEngine;

namespace CosmicShore.Game.Cinematics
{
    [CreateAssetMenu(
        fileName = "CinematicDefinition",
        menuName = "ScriptableObjects/Cinematics/Cinematic Definition")]
    public class CinematicDefinitionSO : ScriptableObject
    {
        [Header("Take control away from player")]
        public bool setLocalVesselToAI = true;

        [Tooltip("Seconds to wait before showing end screen.")]
        public float delayBeforeEndScreen = 2f;
    }
}