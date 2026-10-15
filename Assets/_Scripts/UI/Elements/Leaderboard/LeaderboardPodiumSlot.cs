using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One step of the weekly leaderboard's podium (Docs/MENU_REDESIGN_B.md section 14): the rank
    /// badge, the pilot's face, name and time for ranks 1, 2 and 3. The panel
    /// (<see cref="WeeklyChallengeLeaderboardPanel"/>) fills it; an empty step - fewer than three
    /// finishers this week - hides through its CanvasGroup.
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI Kit/Leaderboard Podium Slot")]
    public class LeaderboardPodiumSlot : MonoBehaviour
    {
        [Tooltip("The step's own group: hidden while nobody holds this rank.")]
        [SerializeField] private CanvasGroup group;
        [Tooltip("The pilot's avatar.")]
        [SerializeField] private Image avatar;
        [Tooltip("The pilot's name; the local pilot's carries the panel's suffix.")]
        [SerializeField] private TMP_Text nameText;
        [Tooltip("The finishing time.")]
        [SerializeField] private TMP_Text timeText;

        private Sprite _restAvatar;
        private bool _captured;

        public void Show(string displayName, string time, Sprite face)
        {
            Capture();
            KitVisibility.SetShown(group, true);
            if (nameText) nameText.text = displayName ?? string.Empty;
            if (timeText) timeText.text = time ?? string.Empty;
            if (avatar)
            {
                avatar.sprite = face ? face : _restAvatar;
                avatar.enabled = avatar.sprite;
            }
        }

        public void Clear()
        {
            Capture();
            KitVisibility.SetShown(group, false);
        }

        private void Capture()
        {
            if (_captured) return;
            _captured = true;
            _restAvatar = avatar ? avatar.sprite : null;
        }
    }
}
