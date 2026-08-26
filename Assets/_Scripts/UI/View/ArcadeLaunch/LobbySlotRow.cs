using System;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The launch panel's roster: one seat per player the match will hold, humans first and AI
    /// filling the rest, plus the "fill the rest with AI" toggle that decides how many there are.
    ///
    /// <para><b>Kicking an AI is lowering the player count.</b> There is no AI to remove yet — the
    /// bots are spawned by <c>ServerPlayerVesselInitializerWithAI</c> from
    /// <c>GameDataSO.RequestedAIBackfillCount</c> once the scene loads — so a kick here is a
    /// request to seat one fewer, which is exactly what the player means by it. That also makes it
    /// impossible to kick a seat into an inconsistent state: the count is the only truth.</para>
    ///
    /// <para><b>Ready lights are a COUNT, not an identity.</b> <c>ArcadeConfigSyncManager</c>
    /// replicates how many humans have confirmed, not which ones, so seats light in roster order as
    /// that count climbs — with the local player's own seat lit the moment they confirm, since that
    /// one IS known locally. Per-seat identity needs the sync manager to replicate the ready SET;
    /// until it does, this is the honest reading and it is right in the case players actually
    /// watch (their own).</para>
    /// </summary>
    public class LobbySlotRow : MonoBehaviour
    {
        [Header("Seats")]
        [SerializeField, Tooltip("Container the seats are built under. Put a Horizontal Layout " +
                                 "Group on it; this component writes no rects.")]
        RectTransform slotContainer;

        [SerializeField, Tooltip("Seat prefab.")] LobbySlotView slotPrefab;

        [Header("Fill with AI")]
        [SerializeField, Tooltip("Toggle that fills every remaining seat with AI. Off seats only " +
                                 "the humans present. Hidden for non-host clients, who do not own " +
                                 "the player count.")]
        Toggle fillWithAIToggle;

        [SerializeField, Tooltip("Optional label beside the toggle, e.g. '3 AI'.")]
        TMP_Text fillSummaryText;

        [Header("Copy")]
        [SerializeField, Tooltip("Summary written when the roster is all human.")]
        string noAiSummary = "No AI";

        [SerializeField, Tooltip("Summary written when AI fill the rest. {0} is the AI count.")]
        string aiSummaryFormat = "{0} AI";

        /// <summary>The player asked to seat one fewer (the ✕ on an AI seat).</summary>
        public event Action OnKickAIRequested;

        /// <summary>The fill toggle moved. True = fill every remaining seat with AI.</summary>
        public event Action<bool> OnFillWithAIChanged;

        readonly List<LobbySlotView> _slots = new();
        bool _suppressToggleCallback;

        void Awake()
        {
            if (fillWithAIToggle) fillWithAIToggle.onValueChanged.AddListener(HandleFillToggled);
        }

        void OnDestroy()
        {
            if (fillWithAIToggle) fillWithAIToggle.onValueChanged.RemoveListener(HandleFillToggled);
        }

        /// <summary>
        /// Draw the roster.
        /// </summary>
        /// <param name="gameData">Source of the live human players. Null draws generic seats.</param>
        /// <param name="totalPlayers">Seats the match will hold (humans + AI backfill).</param>
        /// <param name="humanCount">How many of those seats are humans.</param>
        /// <param name="readyCount">How many humans have confirmed.</param>
        /// <param name="localReady">Whether the LOCAL player has confirmed — known exactly.</param>
        /// <param name="isHost">Only the host may kick or toggle fill.</param>
        public void Refresh(GameDataSO gameData, int totalPlayers, int humanCount,
                            int readyCount, bool localReady, bool isHost)
        {
            totalPlayers = Mathf.Max(1, totalPlayers);
            humanCount = Mathf.Clamp(humanCount, 0, totalPlayers);
            int aiCount = totalPlayers - humanCount;

            var humans = CollectHumans(gameData);
            ulong localId = NetworkManager.Singleton ? NetworkManager.Singleton.LocalClientId : 0UL;
            var dataService = PlayerDataService.Instance;

            int lit = Mathf.Clamp(readyCount, 0, humanCount);
            int seat = 0;

            for (; seat < humanCount; seat++)
            {
                var slot = SlotAt(seat);
                if (!slot) break;

                var player = seat < humans.Count ? humans[seat] : null;
                bool isLocal = player != null && player.OwnerClientId == localId;

                // The local seat's state is known exactly; the rest fill in roster order from the
                // replicated count. Ordering the local seat first would reshuffle the row as
                // players join, so instead it keeps its place and simply reads true.
                bool ready = isLocal ? localReady : seat < lit;

                Sprite avatar = player != null && dataService != null
                    ? dataService.GetAvatarSprite(player.NetAvatarId.Value)
                    : null;

                slot.ShowHuman(seat, player != null ? player.Name : null, avatar, ready, isLocal);
            }

            for (; seat < totalPlayers; seat++)
            {
                var slot = SlotAt(seat);
                if (!slot) break;

                slot.ShowAI(seat, "AI", kickable: isHost, onKick: HandleKick);
            }

            for (int i = seat; i < _slots.Count; i++)
                if (_slots[i]) _slots[i].gameObject.SetActive(false);

            RefreshFillControls(aiCount, isHost);

            CSDebug.LogVerbose(CSLogChannel.ArcadeLaunch,
                $"[ArcadeLaunch] Roster: {humanCount} human ({lit} ready, local={localReady}) " +
                $"+ {aiCount} AI = {totalPlayers}.");
        }

        /// <summary>Whether the fill toggle is on. False when this row has no toggle wired.</summary>
        public bool FillWithAI => fillWithAIToggle && fillWithAIToggle.isOn;

        /// <summary>
        /// Set the toggle without raising <see cref="OnFillWithAIChanged"/> — used when the panel
        /// derives the toggle's state from the player count rather than the other way round.
        /// </summary>
        public void SetFillWithAISilently(bool on)
        {
            if (!fillWithAIToggle || fillWithAIToggle.isOn == on) return;
            _suppressToggleCallback = true;
            fillWithAIToggle.isOn = on;
            _suppressToggleCallback = false;
        }

        void RefreshFillControls(int aiCount, bool isHost)
        {
            if (fillWithAIToggle)
            {
                fillWithAIToggle.gameObject.SetActive(isHost);
                fillWithAIToggle.interactable = isHost;
            }

            if (fillSummaryText)
                fillSummaryText.text = aiCount > 0
                    ? string.Format(aiSummaryFormat, aiCount)
                    : noAiSummary;
        }

        static List<Player> CollectHumans(GameDataSO gameData)
        {
            var humans = new List<Player>();
            if (gameData?.Players == null) return humans;

            foreach (var ip in gameData.Players)
                if (ip is Player p && p.NetIsAI != null && !p.NetIsAI.Value)
                    humans.Add(p);

            return humans;
        }

        LobbySlotView SlotAt(int index)
        {
            while (_slots.Count <= index)
            {
                if (!slotPrefab || !slotContainer)
                {
                    CSDebug.LogWarning("[ArcadeLaunch] LobbySlotRow needs both a slotPrefab and a " +
                                       "slotContainer to draw the roster.", this);
                    return null;
                }
                _slots.Add(Instantiate(slotPrefab, slotContainer));
            }
            return _slots[index];
        }

        void HandleKick(LobbySlotView _) => OnKickAIRequested?.Invoke();

        void HandleFillToggled(bool on)
        {
            if (_suppressToggleCallback) return;
            OnFillWithAIChanged?.Invoke(on);
        }
    }
}
