using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MiniGameHUD : MonoBehaviour
{
    public TMP_Text ScoreDisplay;
    public TMP_Text RoundTimeDisplay;
    public Image CountdownDisplay;
    public Button ReadyButton;
    public CountdownTimer CountdownTimer;
    [SerializeField] GameObject Pip;


    public void SetPipActive(bool active)
    {
        Pip.SetActive(active);
    }
}

