using UnityEngine;
using CosmicShore.Core;
using TMPro;

namespace CosmicShore.Game
{
    public class Timer : MonoBehaviour
    {
        private float _timeRemaining;
        public float timeRemaining;
        public TMP_Text textMeshPro;
        bool RoundEnded = false;

        private void OnEnable()
        {
            GameManager.OnPlayGame += ResetTimer;
        }

        private void OnDisable()
        {
            GameManager.OnPlayGame -= ResetTimer;
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
}