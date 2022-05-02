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
        [SerializeField]
        private bool tutorialEnabled = true;

        public bool IsMuted { get => isMuted; }
        public bool TutorialEnabled { get => tutorialEnabled; set => tutorialEnabled = value; }
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



