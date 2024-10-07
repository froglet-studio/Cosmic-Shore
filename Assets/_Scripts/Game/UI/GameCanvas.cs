using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.UI
{
    public class GameCanvas : MonoBehaviour
    {
        [Header("HUD and Controls")]
        [SerializeField] public MiniGameHUD MiniGameHUD;
        [SerializeField] public ShipButtonPanel ShipButtonPanel;
        [SerializeField] public GameObject EndGameScreen;

        [Header("Scoring UI")]
        [SerializeField] public Scoreboard scoreboard;

        [Header("Goodies and Awards")]
        [SerializeField] public GameObject AwardsContainer;
        [SerializeField] public TMP_Text XPEarnedText;
        [SerializeField] public Image CrystalsEarnedImage;
        [SerializeField] public TMP_Text CrystalsEarnedText;
        [SerializeField] public Image EncounteredCaptainImage;
        [SerializeField] public TMP_Text EncounteredCaptainText;
    }
}