using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DialogueUIPrefabRefs : MonoBehaviour
{
    public RectTransform moveFromLeft, moveToLeft, moveFromRight, moveToRight;

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

}
