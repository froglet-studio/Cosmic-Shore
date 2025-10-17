using Unity.Netcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Tools.MiniGameMaker
{
    sealed class GameRootValidator : IValidator
    {
        public string Name => "Game Root + NetworkObject";
        public (Severity,string) Check()
        {
            var game = SceneUtil.Find("Game");
            if (!game) return (Severity.Fail, "Game root missing");
            var okT = game.transform.position == Vector3.zero && game.transform.localScale == Vector3.one;
            if (!okT) return (Severity.Warning, "Transform not default (0/0/0, scale 1)");
            return game.GetComponent<NetworkObject>() ? (Severity.Pass,"OK") : (Severity.Fail,"NetworkObject missing");
        }
        public void Fix()
        {
            var game = SceneUtil.Find("Game") ?? new GameObject("Game");
            if (!game.GetComponent<NetworkObject>()) game.AddComponent<NetworkObject>();
            game.transform.position = Vector3.zero;
            game.transform.rotation = Quaternion.identity;
            game.transform.localScale = Vector3.one;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}