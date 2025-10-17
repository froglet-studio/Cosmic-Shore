using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Tools.MiniGameMaker
{
    static class SceneUtil
    {
        public static GameObject Find(string name) =>
            SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == name);
    }
}