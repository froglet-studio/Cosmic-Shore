using CosmicShore.Game.Arcade;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CosmicShore.Game.UI
{
    public enum MiniGameType
    {
        Freestyle,
        CellularDuel
    }

    public class MiniGameHUDView : MonoBehaviour, IMiniGameHUDView
    {
        [Header("Common Elements")]
        [SerializeField] private MiniGameType miniGameType;
        public MiniGameType MiniGameHUDType => miniGameType;

        [SerializeField] private TMP_Text scoreDisplay;
        [SerializeField] private TMP_Text leftNumberDisplay;
        [SerializeField] private TMP_Text rightNumberDisplay;
        [SerializeField] private TMP_Text roundTimeDisplay;
        [SerializeField] private Image countdownDisplay;
        [SerializeField] private Button readyButton;
        [SerializeField] private CountdownTimer countdownTimer; // <-- MISSING IN YOUR VIEW, ADDED HERE
        [SerializeField] private GameObject pip;
        [SerializeField] private GameObject silhouette;
        [SerializeField] private GameObject trailDisplay;
        [SerializeField] private ButtonPanel buttonPanel;

        [Header("Bottom Buttons")]
        [SerializeField] private GameObject button1;
        [SerializeField] private GameObject button2;
        [SerializeField] private GameObject button3;

        [Header("Button Events (Inspector Assignable)")]
        public UnityEvent OnButton1Pressed;
        public UnityEvent OnButton2Pressed;
        public UnityEvent OnButton3Pressed;
        public UnityEvent OnButton1Released;
        public UnityEvent OnButton2Released;
        public UnityEvent OnButton3Released;

        public void Initialize(IMiniGameHUDController controller)
        {
            RemoveAllButtonListeners();

            if (button1 != null && button1.TryGetComponent<Button>(out var btn1))
            {
                btn1.onClick.AddListener(() => controller.OnButtonPressed(1));
                btn1.onClick.AddListener(() => OnButton1Pressed?.Invoke());
            }
            if (button2 != null && button2.TryGetComponent<Button>(out var btn2))
            {
                btn2.onClick.AddListener(() => controller.OnButtonPressed(2));
                btn2.onClick.AddListener(() => OnButton2Pressed?.Invoke());
            }
            if (button3 != null && button3.TryGetComponent<Button>(out var btn3))
            {
                btn3.onClick.AddListener(() => controller.OnButtonPressed(3));
                btn3.onClick.AddListener(() => OnButton3Pressed?.Invoke());
            }
        }

        public void RemoveAllButtonListeners()
        {
            if (button1 != null && button1.TryGetComponent<Button>(out var btn1)) btn1.onClick.RemoveAllListeners();
            if (button2 != null && button2.TryGetComponent<Button>(out var btn2)) btn2.onClick.RemoveAllListeners();
            if (button3 != null && button3.TryGetComponent<Button>(out var btn3)) btn3.onClick.RemoveAllListeners();
        }

        public TMP_Text ScoreDisplay => scoreDisplay;
        public TMP_Text LeftNumberDisplay => leftNumberDisplay;
        public TMP_Text RightNumberDisplay => rightNumberDisplay;
        public TMP_Text RoundTimeDisplay => roundTimeDisplay;
        public Image CountdownDisplay => countdownDisplay;
        public Button ReadyButton => readyButton;
        public CountdownTimer CountdownTimer => countdownTimer; // <-- ADDED
        public GameObject Pip => pip;
        public GameObject Silhouette => silhouette;
        public GameObject TrailDisplay => trailDisplay;
        public ButtonPanel ButtonPanel => buttonPanel;
        public GameObject Button1 => button1;
        public GameObject Button2 => button2;
        public GameObject Button3 => button3;
    }
}
