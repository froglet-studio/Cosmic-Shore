using System;
using UnityEngine;
using UnityEngine.Events;

namespace Scenes.TestScenes.Firebase_Tests
{
    public class PlayerBehavior : MonoBehaviour
    {
        private static PlayerData _playerData;

        public static UnityEvent UpdatePlayerData; 
        // Start is called before the first frame update
        void Start()
        {
            _playerData = new();
            UpdatePlayerData = new();
        }

        
    }
}
