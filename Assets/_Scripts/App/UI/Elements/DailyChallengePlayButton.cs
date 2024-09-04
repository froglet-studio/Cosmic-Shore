using CosmicShore.App.Systems;
using UnityEngine;

namespace CosmicShore.App.UI
{
    public class DailyChallengePlayButton : MonoBehaviour
    {
        public void Play()
        {
            DailyChallengeSystem.Instance.PlayDailyChallenge();
        }
    }
}