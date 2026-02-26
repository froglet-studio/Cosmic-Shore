using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    public class DailyChallengePlayButton : MonoBehaviour
    {
        public void Play()
        {
            DailyChallengeSystem.Instance.PlayDailyChallenge();
        }
    }
}