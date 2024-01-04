using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Utility.Singleton;
using CosmicShore.Core;

namespace CosmicShore
{
    public class StateTracker : Singleton<StateTracker>
    {
        [SerializeField] TrailBlock trailBlock;

        struct StateDiff
        {
            public HashSet<BlockState> addSet;
            public HashSet<BlockState> removeSet;
        }

        struct BlockState
        {
            public BlockState(Vector3 position, Quaternion rotation, Vector3 scale, Teams team)
            {
                this.position = position;
                this.rotation = rotation;
                this.scale = scale;
                this.team = team;
            }
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
            public Teams team;
        }

        HashSet<BlockState> gameState;
        StateDiff stateDiff;


        // this is used to capture the current state so it is ready to be saved
        HashSet<BlockState> CaptureState()
        {
            var colliders = Physics.OverlapSphere(Vector3.zero, 10000f);
            foreach (var collider in colliders)
            {
                var trailBlock = collider.GetComponent<TrailBlock>();
                if (trailBlock != null)
                {
                    var transform = trailBlock.transform;
                    gameState.Add(new BlockState(transform.position, transform.rotation, transform.localScale, trailBlock.Team));
                }
            }
            return gameState;
        }

        // this loads from a previously saved state
        void LoadState(HashSet<BlockState> newState)
        {
            // TODO: load previous file to gameState
            
            foreach (var blockState in gameState)
            {
                var Block = Instantiate(trailBlock);
                Block.transform.position = blockState.position;
                Block.transform.rotation = blockState.rotation;
                Block.transform.localScale = blockState.scale;
                Block.Team = blockState.team;
            }

            // TODO: if no file to load, load default state

        }

        // this is used to save a new captured state
        private void SaveState()
        {
            // TODO: If file doesn't exist
            gameState = CaptureState();
            // TODO: then save state to new file
            // TODO: else update existing file with the following diff
            gameState = CalculateNewState(gameState, stateDiff);
        }

        HashSet<BlockState> CalculateNewState(HashSet<BlockState> gameState, StateDiff diff)
        {
            
            foreach (var trailBlock in diff.addSet)
            {
                gameState.Add(trailBlock);
            }
            foreach (var trailBlock in diff.removeSet)
            {
                gameState.Remove(trailBlock);
            }
            return gameState;
        }

        // this removes a block from the state
        public void RemoveBlock(Teams team, TrailBlockProperties trailBlockProperties)
        {
            var transform = trailBlockProperties.trailBlock.transform;
            stateDiff.removeSet.Add(new BlockState(transform.position, transform.rotation, transform.localScale, trailBlockProperties.trailBlock.Team));
        }

        // this adds a block to the state
        public void AddBlock(Teams team, TrailBlockProperties trailBlockProperties)
        {
            var transform = trailBlockProperties.trailBlock.transform;
            stateDiff.addSet.Add(new BlockState(transform.position, transform.rotation, transform.localScale, trailBlockProperties.trailBlock.Team));
        }

    }
}
