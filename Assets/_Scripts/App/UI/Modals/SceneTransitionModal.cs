using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore
{
    public class SceneTransitionModal : MonoBehaviour
    {
        void Start()
        {
            Arcade.Instance.RegisterSceneTransitionAnimator(GetComponent<Animator>());
            GameManager.Instance.RegisterSceneTransitionAnimator(GetComponent<Animator>());
        }
    }
}