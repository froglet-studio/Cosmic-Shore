using CosmicShore.Game.Multiplayer;
using UnityEngine;

namespace CosmicShore.UI.Elements
{
    public class DailyChallengePlayButton : MonoBehaviour
    {
        public void Play()
        {
            DailyChallengeSystem.Instance.PlayDailyChallenge();
        }
    }
}