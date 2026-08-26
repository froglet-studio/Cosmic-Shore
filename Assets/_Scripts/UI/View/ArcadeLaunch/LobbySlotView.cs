using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One seat in the launch panel's roster: a human (lit once they have confirmed), an AI that
    /// can be kicked, or an empty seat.
    ///
    /// <para>The three states are one component rather than three prefabs because a seat CHANGES
    /// state in place — an AI is kicked and the seat becomes empty, a human confirms and the same
    /// seat lights up — and swapping prefabs mid-panel would make the roster re-layout under the
    /// player's cursor.</para>
    /// </summary>
    public class LobbySlotView : MonoBehaviour
    {
        public enum SlotKind { Empty = 0, Human = 1, AI = 2 }

        [Header("Identity")]
        [SerializeField, Tooltip("Avatar / silhouette. Kept enabled in every state so an empty " +
                                 "seat still reads as a seat rather than disappearing.")]
        Image avatarImage;

        [SerializeField, Tooltip("Player or AI name. Blank on an empty seat.")]
        TMP_Text nameText;

        [Header("State artwork")]
        [SerializeField, Tooltip("Sprite for a seat nobody is in.")] Sprite emptySprite;
        [SerializeField, Tooltip("Fallback sprite for a human with no avatar loaded yet.")] Sprite humanSprite;
        [SerializeField, Tooltip("Sprite for an AI-filled seat.")] Sprite aiSprite;

        [SerializeField, Tooltip("Optional 'AI' badge, shown only on AI seats.")]
        GameObject aiBadge;

        [Header("Ready")]
        [SerializeField, Tooltip("Glow / ring switched on when this player has confirmed. AI seats " +
                                 "are never 'ready' - they have nothing to confirm.")]
        GameObject readyGlow;

        [SerializeField, Tooltip("Tint applied to the avatar while NOT ready. Ready seats draw at " +
                                 "full white.")]
        Color notReadyTint = new(0.55f, 0.55f, 0.6f, 1f);

        [Header("Kick")]
        [SerializeField, Tooltip("The kick control. Shown ONLY on an AI seat, and only to the " +
                                 "host - a human seat is never kickable from here.")]
        Button kickButton;

        System.Action<LobbySlotView> _onKick;

        /// <summary>What is in this seat.</summary>
        public SlotKind Kind { get; private set; } = SlotKind.Empty;

        /// <summary>Roster index this seat stands for, so a kick names a seat.</summary>
        public int SlotIndex { get; private set; } = -1;

        void Awake()
        {
            if (kickButton) kickButton.onClick.AddListener(HandleKickClicked);
        }

        void OnDestroy()
        {
            if (kickButton) kickButton.onClick.RemoveListener(HandleKickClicked);
        }

        /// <summary>An unoccupied seat.</summary>
        public void ShowEmpty(int slotIndex)
        {
            SlotIndex = slotIndex;
            Kind = SlotKind.Empty;

            Apply(emptySprite, string.Empty, ready: false, showAiBadge: false, kickable: false);
        }

        /// <summary>A human seat. <paramref name="ready"/> lights it.</summary>
        public void ShowHuman(int slotIndex, string playerName, Sprite avatar, bool ready, bool isLocal)
        {
            SlotIndex = slotIndex;
            Kind = SlotKind.Human;

            Apply(avatar ? avatar : humanSprite,
                  string.IsNullOrWhiteSpace(playerName) ? (isLocal ? "You" : "Player") : playerName,
                  ready, showAiBadge: false, kickable: false);
        }

        /// <summary>An AI seat. <paramref name="kickable"/> is the host's answer, not this view's.</summary>
        public void ShowAI(int slotIndex, string aiName, bool kickable, System.Action<LobbySlotView> onKick)
        {
            SlotIndex = slotIndex;
            Kind = SlotKind.AI;
            _onKick = onKick;

            Apply(aiSprite, string.IsNullOrWhiteSpace(aiName) ? "AI" : aiName,
                  // An AI has nothing to confirm, so it is drawn at full strength rather than
                  // dimmed as "not ready" — dimming it would read as a player who has not clicked.
                  ready: true, showAiBadge: true, kickable: kickable);
        }

        void Apply(Sprite sprite, string label, bool ready, bool showAiBadge, bool kickable)
        {
            if (avatarImage)
            {
                if (sprite) avatarImage.sprite = sprite;
                avatarImage.color = ready ? Color.white : notReadyTint;
                avatarImage.enabled = true;
            }

            if (nameText) nameText.text = label;
            if (readyGlow) readyGlow.SetActive(ready && Kind == SlotKind.Human);
            if (aiBadge) aiBadge.SetActive(showAiBadge);
            if (kickButton) kickButton.gameObject.SetActive(kickable);

            gameObject.SetActive(true);
        }

        void HandleKickClicked() => _onKick?.Invoke(this);
    }
}
