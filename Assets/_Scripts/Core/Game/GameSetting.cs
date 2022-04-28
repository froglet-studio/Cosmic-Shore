using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace StarWriter.Core
{
    public class GameSetting : MonoBehaviour
    {

        #region Audio Settings
        [SerializeField]
        private bool isMuted = false;

        public bool IsMuted { get => isMuted; }
        #endregion

        private void Start()
        {
      
        }

        public void ToggleMusic()
        {
            isMuted = !isMuted;
        }
    }
}



