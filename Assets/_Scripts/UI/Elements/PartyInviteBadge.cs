using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The unread-invite count on the party panel: a small disc with a number, shown while the
    /// service's <see cref="IncomingInviteInbox"/> holds open party invites the player has not
    /// yet had on screen in the friends panel, hidden otherwise. It sits next to the PARTY
    /// header of every <see cref="ArcadeLobbyList"/> (the friends panel's entry point), so an
    /// invite whose popup auto-hid is still announced wherever the player would go to answer it.
    ///
    /// <para>Reads <see cref="HostConnectionService.UnseenIncomingInviteCount"/>, which the
    /// friends panel clears on open (<see cref="HostConnectionService.MarkIncomingInvitesSeen"/>).
    /// Repaints on the invite SOAP events for immediacy and on a slow tick for the two changes
    /// that have no event (the panel marking the inbox seen, an invite expiring). Self-driven:
    /// the party panel does not reference it and no scene wiring beyond its own fields.</para>
    /// </summary>
    public class PartyInviteBadge : MonoBehaviour
    {
        [Header("SOAP Data")]
        [Tooltip("The party data asset whose OnInviteReceived / OnInviteResolved raises repaint the badge at once.")]
        [SerializeField] private HostConnectionDataSO connectionData;

        [Header("UI References")]
        [Tooltip("Faded to 0 while nothing is unread. Found on this GameObject when left empty.")]
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("The number on the disc.")]
        [SerializeField] private TMP_Text countText;

        [Header("Display")]
        [Tooltip("Counts above this read as \"N+\" so the disc never has to grow.")]
        [SerializeField] private int maxShownCount = 9;
        [Tooltip("Seconds between the slow repaints that catch the inbox being marked seen or an invite expiring.")]
        [SerializeField] private float pollIntervalSeconds = 0.5f;

        float _nextPoll;
        int _shown = -1;

        /// <summary>What the disc reads for a count, as pure text so it can be asserted without a scene.</summary>
        public static class Label
        {
            /// <summary>Empty for nothing unread, the number up to <paramref name="maxShown"/>, "N+" past it.</summary>
            public static string Format(int unseenCount, int maxShown)
            {
                if (unseenCount <= 0) return string.Empty;
                int cap = Mathf.Max(1, maxShown);
                return unseenCount > cap ? $"{cap}+" : unseenCount.ToString();
            }
        }

        void Awake()
        {
            if (!canvasGroup)
                TryGetComponent(out canvasGroup);

            Apply(0);
        }

        // No null guards on the SOAP events (CLAUDE.md: fail loud): an unwired connectionData is a
        // scene-wiring fault that must surface as an exception here, not as a badge that silently
        // repaints only on its slow tick.
        void OnEnable()
        {
            connectionData.OnInviteReceived.OnRaised += HandleInviteReceived;
            connectionData.OnInviteResolved.OnRaised += Refresh;

            Refresh();
        }

        void OnDisable()
        {
            connectionData.OnInviteReceived.OnRaised -= HandleInviteReceived;
            connectionData.OnInviteResolved.OnRaised -= Refresh;
        }

        void Update()
        {
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + Mathf.Max(0.1f, pollIntervalSeconds);
            Refresh();
        }

        void HandleInviteReceived(PartyInviteData _) => Refresh();

        /// <summary>Repaints from the service's unread count (0 without a service).</summary>
        public void Refresh()
        {
            var service = HostConnectionService.Instance;
            Apply(service != null ? service.UnseenIncomingInviteCount : 0);
        }

        void Apply(int unseen)
        {
            if (unseen == _shown) return;
            _shown = unseen;

            bool show = unseen > 0;
            if (canvasGroup)
            {
                canvasGroup.alpha = show ? 1f : 0f;
                // Never a click target: the "+" slot and the header under it keep their hits.
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            if (countText)
                countText.text = Label.Format(unseen, maxShownCount);
        }
    }
}
