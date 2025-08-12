using UnityEngine;
using Obvious.Soap;
using TMPro;

namespace CosmicShore.Game
{
    public class Timer : MonoBehaviour
    {
        [SerializeField] 
        ScriptableEventNoParam _onTimerEnded;
        
        [SerializeField] 
        ScriptableEventNoParam _onPlayGame;
        
        private float _timeRemaining;
        public float timeRemaining;
        public TMP_Text textMeshPro;
        bool _timerEnded = true;

        private void OnEnable()
        {
            _onPlayGame.OnRaised += ResetTimer;
        }

        private void OnDisable()
        {
            _onPlayGame.OnRaised -= ResetTimer;
        }

        private void Start() => _timeRemaining = timeRemaining;

        void Update()
        {
            if (_timerEnded) 
                return;

            _timeRemaining -= Time.deltaTime;

            if (_timeRemaining <= 0)
            {
                _timeRemaining = 0;
                _timerEnded = true;
                _onTimerEnded.Raise();
            }

            textMeshPro.text = Mathf.Round(_timeRemaining).ToString();
        }

        void ResetTimer()
        {
            _timerEnded = false;
            _timeRemaining = timeRemaining;
        }
    }
}