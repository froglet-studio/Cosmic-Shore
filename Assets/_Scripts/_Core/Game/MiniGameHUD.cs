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

    public void SetPipActive(bool active, bool mirrored)
    {
        Pip.SetActive(active);
        Pip.GetComponent<PipUI>().SetMirrored(mirrored);
    }
}

