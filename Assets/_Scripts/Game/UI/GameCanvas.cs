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
        [SerializeField] public ResourceDisplayGroup ResourceDisplayGroup;
        [SerializeField] public GameObject EndGameScreen;

        [Header("Scoring UI")]
        [SerializeField] public Scoreboard scoreboard;
    }
}