using System;
using UnityEngine;

namespace CosmicShore.Game.StateSystem
{
    public abstract class BaseState<TState> where TState : Enum
    {
        public BaseState(TState key)
        {
            StateKey = key;
        }
        public TState StateKey { get; private set; }
        public abstract void EnterState();
        public abstract void ExitState();
        public abstract void UpdateState();
        public abstract TState GetNextState();
        public abstract void OnTriggerEnter(Collider other);
        public abstract void OnTriggerStay(Collider other);
        public abstract void OnTriggerExit(Collider other);
    }
}
