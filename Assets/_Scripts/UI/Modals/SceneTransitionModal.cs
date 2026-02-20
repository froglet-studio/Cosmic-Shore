using System;
using UnityEngine;

namespace CosmicShore.Game
{
    [RequireComponent(typeof(Animator))]
    public class SceneTransitionModal : MonoBehaviour
    {
        private readonly int OPEN = Animator.StringToHash("open");

        [SerializeField]
        Animator _animator;

        private void Awake()
        {
            TransitionDoor(false);
        }

        public void TransitionDoor(bool open) => _animator.SetBool(OPEN, open);
    }
}