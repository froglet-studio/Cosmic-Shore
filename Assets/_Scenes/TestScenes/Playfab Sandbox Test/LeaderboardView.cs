using UnityEngine;
using Button = UnityEngine.UI.Button;

namespace CosmicShore._Core.Playfab
{
    public class LeaderboardView : MonoBehaviour
    {
        [SerializeField] private Button GetLeaderboardButton;

        private void Start()
        {
            // LeaderboardManager.Instance
            // GetLeaderboardButton.onClick.AddListener(LeaderboardManager.Instance.RequestLeaderboard);
        }
    }
}

