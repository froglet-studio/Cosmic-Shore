using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

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
    public RectTransform leftSpeakerRoot;
    public RectTransform leftBox;
    public TMP_Text leftSpeakerName;
    public TMP_Text leftDialogueText;
    public Image leftPortrait;

    [Header("Right Speaker")]
    public RectTransform rightSpeakerRoot;
    public RectTransform rightBox;
    public TMP_Text rightSpeakerName;
    public TMP_Text rightDialogueText;
    public Image rightPortrait;

    [Header("Reward Panel")]
    public RectTransform rewardPanel;

    [Header("Buttons")]
    public Button nextButton;
    public Button skipButton;

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
