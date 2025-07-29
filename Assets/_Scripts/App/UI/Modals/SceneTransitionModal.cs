using System;
using CosmicShore.Core;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    [RequireComponent(typeof(Animator))]
    public class SceneTransitionModal : MonoBehaviour
    {
        private readonly int start = Animator.StringToHash("Start");

        Animator _animator;
        
        private void Awake() => _animator = GetComponent<Animator>();
        
        public void StartTransition() => _animator.SetTrigger(start);
    }
}