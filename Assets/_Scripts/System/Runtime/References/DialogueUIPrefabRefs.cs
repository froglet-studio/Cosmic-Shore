using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

namespace CosmicShore.Core
{
    public class DialogueUIPrefabRefs : MonoBehaviour
    {
        public RectTransform MonologueSpeakerRoot => monologueSpeakerRoot;
        public RectTransform MonologueBox => monologueBox;
        public TMP_Text MonologueSpeakerText => monologueSpeakerText;
        public TMP_Text MonologueDialogText => monologueDialogueText;
        public Image MonologuePortrait => monologuePortrait;


        [Header("Monologue Mode")]
        [SerializeField] private RectTransform monologueSpeakerRoot;
        [SerializeField] private RectTransform monologueBox;
        [SerializeField] private TMP_Text monologueSpeakerText;
        [SerializeField] private TMP_Text monologueDialogueText;
        [SerializeField] private Image monologuePortrait;

        [Header("Left Speaker")]
        [SerializeField] RectTransform leftSpeakerRoot;
        public RectTransform LeftSpeakerRoot => leftSpeakerRoot;
        [SerializeField] RectTransform leftBox;
        public RectTransform LeftBox => leftBox;
        public TMP_Text leftSpeakerName;
        public TMP_Text leftDialogueText;
        public Image leftPortrait;

        [Header("Right Speaker")]
        [SerializeField] RectTransform rightSpeakerRoot;
        public RectTransform RightSpeakerRoot => rightSpeakerRoot;
        [SerializeField] RectTransform rightBox;
        public RectTransform RightBox => rightBox;
        public TMP_Text rightSpeakerName;
        public TMP_Text rightDialogueText;
        public Image rightPortrait;

        [Header("Reward Panel")]
        [SerializeField] RectTransform rewardPanel;
        public RectTransform RewardPanel => rewardPanel;

        [Header("Buttons")]
        [SerializeField] Button nextButton;
        public Button NextButton => nextButton;
        [SerializeField] Button skipButton;
        public Button SkipButton => skipButton;

        /// <summary>
        /// Fired by an AnimationEvent at the end of "MonologuePopOut" & "DialoguePopOut"
        /// </summary>
        public Action OnAnimInComplete;
        public void HandleAnimInComplete() => OnAnimInComplete?.Invoke();

        /// <summary>
        /// Fired by an AnimationEvent at the end of "MonologuePopIn" & "DialoguePopIn"
        /// </summary>
        public Action OnAnimOutComplete;
        public void HandleAnimOutComplete() => OnAnimOutComplete?.Invoke();

    }
}
