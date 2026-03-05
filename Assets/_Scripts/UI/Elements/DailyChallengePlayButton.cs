using CosmicShore.App.Systems;
using CosmicShore.Core;
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