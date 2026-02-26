using UnityEngine;
using UnityEngine.Serialization;


namespace CosmicShore.Utility
{
    [CreateAssetMenu(fileName = "SceneNameListSO", menuName = "ScriptableObjects/SceneNameListSO")]
    public class SceneNameListSO : ScriptableObject
    {
        public string MainMenuScene;
        public string MultiplayerScene;
    }
}