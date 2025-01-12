using UnityEngine;


namespace CosmicShore.Utilities
{
    [CreateAssetMenu(fileName = "SceneNameListSO", menuName = "ScriptableObjects/SceneNameListSO")]
    public class SceneNameListSO : ScriptableObject
    {
        public string StartUpScene;
        public string MainMenuScene;
        public string CharSelectScene;
        public string MultiplayerScene;
    }
}