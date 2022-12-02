using UnityEngine;
using StarWriter.Core;
using TMPro;

// TODO: namespace
// TODO: should this be in GameManager?
public class Timer : MonoBehaviour
{
    private float _timeRemaining;
    public float timeRemaining;
    public TMP_Text textMeshPro;
    bool RoundEnded = false;

    private void OnEnable()
    {
        GameManager.onPlayGame += ResetTimer;
    }

    private void OnDisable()
    {
        GameManager.onPlayGame -= ResetTimer;
    }

    private void Start()
    {
        _timeRemaining = timeRemaining;
    }

    void Update()
    {
        if (RoundEnded) return;

        _timeRemaining -= Time.deltaTime;
        
        if (_timeRemaining <= 0)
        {
            GameManager.EndGame();
            _timeRemaining = 0;
            RoundEnded = true;
        }

        textMeshPro.text = Mathf.Round(_timeRemaining).ToString();
    }

    void ResetTimer()
    {
        RoundEnded = false;
        _timeRemaining = timeRemaining;
    }
}