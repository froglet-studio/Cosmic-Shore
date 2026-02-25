using UnityEngine;
using UnityEngine.Serialization;


namespace CosmicShore.Utility.DataContainers
{
    [CreateAssetMenu(fileName = "SceneNameListSO", menuName = "ScriptableObjects/SceneNameListSO")]
    public class SceneNameListSO : ScriptableObject
    {
        public string MainMenuScene;
        public string MultiplayerScene;
    }
}