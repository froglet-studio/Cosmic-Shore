using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameCanvas : MonoBehaviour
{
    [Header("HUD and Controls")]
    [SerializeField] public MiniGameHUD MiniGameHUD;
    [SerializeField] public ShipButtonPanel ShipButtonPanel;
    [SerializeField] public ResourceDisplayGroup ResourceDisplayGroup;
    [SerializeField] public GameObject RearView;
    [SerializeField] public GameObject EndGameScreen;

    [Header("Scoring UI")]
    [SerializeField] public VerticalLayoutGroup Scoreboard;
    [SerializeField] public TMP_Text WinnerNameContainer;
    [SerializeField] public Image WinnerBannerImage;
    [SerializeField] public Color GreenTeamWinColor = new(0.106f, 0.733f, 0.733f);
    [SerializeField] public Color RedTeamWinColor = new(0.831f, 0.18f, 0.573f);
    [SerializeField] public Color YellowTeamWinColor = new(0.988f, 0.647f, 0.247f);
}