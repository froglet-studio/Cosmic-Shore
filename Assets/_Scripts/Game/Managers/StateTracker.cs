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

        private static readonly string StateSaveFileName = "MiniGameState.data";
        private static DataAccessor _stateData;
        public class StateDiff
        {
            public HashSet<BlockState> AddSet { get; set; }
            public HashSet<BlockState> RemoveSet { get; set; }
        }

        public struct BlockState
        {
            public BlockState(Vector3 position, Quaternion rotation, Vector3 scale, Teams team)
            {
                _position = position;
                _rotation = rotation;
                _scale = scale;
                _team = team;
            }
            private Vector3 _position;
            private Quaternion _rotation;
            private Vector3 _scale;
            private Teams _team;
        }

        Dictionary<MiniGames,HashSet<BlockState>> _gameState;
        // Dictionary<MiniGames, StateDiff> _stateDiffs;
        private StateDiff _stateDiff;


        // this is used to capture the current state so it is ready to be saved
        public Dictionary<MiniGames,HashSet<BlockState>> CaptureState(MiniGames miniGame)
        {
            var colliders = Physics.OverlapSphere(Vector3.zero, 10000f);
            foreach (var collider in colliders)
            {
                var trailBlock = collider.GetComponent<TrailBlock>();
                if (trailBlock != null)
                {
                    var transform = trailBlock.transform;
                    _gameState[miniGame].Add(new BlockState(transform.position, transform.rotation, transform.localScale, trailBlock.Team));
                }
            }
            return _gameState;
        }

        // this loads from a previously saved state
        Dictionary<MiniGames, HashSet<BlockState>> LoadState()
        {
            _stateData ??= new(StateSaveFileName);

            return _stateData.Load<Dictionary<MiniGames, HashSet<BlockState>>>();
        }

        // this is used to save a new captured state
        private void SaveState(MiniGames miniGame)
        {
            _gameState ??= LoadState();
            _gameState = CaptureState(miniGame);
            CalculateNewState(miniGame);
        }

        void CalculateNewState(MiniGames miniGame)
        {
            
            foreach (var trailBlock in _stateDiff.AddSet)
            {
                _gameState[miniGame].Add(trailBlock);
            }
            foreach (var trailBlock in _stateDiff.RemoveSet)
            {
                _gameState[miniGame].Remove(trailBlock);
            }
        }

        // this removes a block from the state
        public void RemoveBlock(TrailBlockProperties trailBlockProperties, MiniGames miniGame)
        {
            var transform = trailBlockProperties.trailBlock.transform;
            _stateDiff.RemoveSet.Add(new BlockState(transform.position, transform.rotation, transform.localScale, trailBlockProperties.trailBlock.Team));
        }

        // this adds a block to the state
        public void AddBlock(TrailBlockProperties trailBlockProperties, MiniGames miniGame)
        {
            var transform = trailBlockProperties.trailBlock.transform;
            _stateDiff.AddSet.Add(new BlockState(transform.position, transform.rotation, transform.localScale, trailBlockProperties.trailBlock.Team));
        }

        public void Clear()
        {
            _stateData.Flush();
        }
    }
}
