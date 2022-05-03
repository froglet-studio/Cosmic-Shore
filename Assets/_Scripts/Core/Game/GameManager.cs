using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Amoebius.Utility.Singleton;
using StarWriter.Core.Audio;
using System;
using UnityEngine.UI;

namespace StarWriter.Core
{
    [DefaultExecutionOrder(0)]
    [RequireComponent(typeof(GameSetting))]
    public class GameManager : SingletonPersistent<GameManager>
    {
        [SerializeField]
        private GameObject tutorialPanel;
        [SerializeField]
        private bool isTutorialEnabled = true;
        [SerializeField]
        private bool hasCompletedTutorial = false;

        private GameSetting gameSettings;

        public bool HasCompletedTutorial { get => hasCompletedTutorial; set => hasCompletedTutorial = value; }

        // Start is called before the first frame update
        void Start()
        {
            
           isTutorialEnabled = gameSettings.TutorialEnabled;
           tutorialPanel.SetActive(isTutorialEnabled);
        }

        // Update is called once per frame
        void Update()
        {
            if (!isTutorialEnabled) { return; }
            if (isTutorialEnabled)
            {
                tutorialPanel.SetActive(isTutorialEnabled);
            }
        }


        // Toggles the Tutorial Panel on/off 
        public void OnClickTutorialToggleButton()
        {
            gameSettings.TutorialEnabled = isTutorialEnabled = !isTutorialEnabled;
            tutorialPanel.SetActive(isTutorialEnabled);
        }
    }
}

