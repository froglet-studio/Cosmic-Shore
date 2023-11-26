using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.StateSystem
{
    public abstract class StateManager<TState> : MonoBehaviour where TState : Enum
    {
        // States by state keys 
        protected Dictionary<TState, BaseState<TState>> States = new();
        
        // Current State
        protected BaseState<TState> CurrentState;
        
        // Is transitioning state flag
        protected bool IsTransitioningState = false;
        // Start with preset current state
        private void Start()
        {
            CurrentState.EnterState();
        }

        private void Update()
        {
            var nextStateKey = CurrentState.GetNextState();
            
            // If the same key and is not transitioning, keep using the current state update logic
            if(!IsTransitioningState && nextStateKey.Equals(CurrentState.StateKey))
            {
                CurrentState.UpdateState();
            }
            // If the state key has been updated and current state is not transitioning, go to the next state
            else if(!IsTransitioningState)
            {
                TransitionToState(nextStateKey);
            }
        }

        public void TransitionToState(TState StateKey)
        {
            // Set transitioning to true, state update happens every frame, using a flag to guard it against unnecessary updates
            IsTransitioningState = true;
            CurrentState.ExitState();
            CurrentState = States[StateKey];
            CurrentState.EnterState();
            IsTransitioningState = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            CurrentState.OnTriggerEnter(other);
        }

        private void OnTriggerStay(Collider other)
        {
            CurrentState.OnTriggerStay(other);
        }

        private void OnTriggerExit(Collider other)
        {
            CurrentState.OnTriggerExit(other);
        }
    }
}
