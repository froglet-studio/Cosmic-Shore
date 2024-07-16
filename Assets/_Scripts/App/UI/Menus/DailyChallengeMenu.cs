using CosmicShore.App.Systems;
using UnityEngine;

namespace CosmicShore
{
    public class DailyChallengeMenu : MonoBehaviour
    {
        public void Play()
        {
            DailyChallengeSystem.Instance.PlayDailyChallenge();
        }
    }
}