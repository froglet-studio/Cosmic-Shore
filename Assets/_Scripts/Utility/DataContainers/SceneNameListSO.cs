using UnityEngine;


namespace CosmicShore.Utility.DataContainers
{
    [CreateAssetMenu(fileName = "SceneNameListSO", menuName = "ScriptableObjects/SceneNameListSO")]
    public class SceneNameListSO : ScriptableObject
    {
        [Header("Core Flow")]
        public string BootstrapScene;
        public string AuthenticationScene;
        public string MainMenuScene;

        [Header("Multiplayer")]
        public string MultiplayerScene;
    }
}