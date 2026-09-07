using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The launch panel's roster: one seat per player the match will hold — humans first, then the
    /// AIs the host PLACED (each wearing its domain's colour), then empty seats for anything the
    /// card's minimum will top up at launch — plus the <b>Add AI</b> toggle: arm it and the next
    /// domain-tile tap seats an AI on THAT domain instead of picking your own.
    ///
    /// <para><b>Kicking an AI is removing its placement.</b> There is no AI to remove yet — the
    /// bots are spawned by <c>ServerPlayerVesselInitializerWithAI</c> from
    /// <c>GameDataSO.RequestedAIDomains</c> (+ balanced top-up to the card's minimum) once the
    /// scene loads — so the ✕ on an AI seat removes that entry from the placement list, which is
    /// exactly what the player means and the only representation that cannot go out of step with
    /// what spawns.</para>
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

        [Header("Add AI")]
        [SerializeField, Tooltip("The Add AI mode toggle (the control formerly labelled FILL AI - " +
                                 "same wiring, new meaning). While ON, tapping a domain tile seats " +
                                 "an AI on that domain instead of picking your own; toggle off to " +
                                 "stop placing. Hidden for non-host clients, who do not own the " +
                                 "roster.")]
        Toggle fillWithAIToggle;

        [SerializeField, Tooltip("Optional label beside the toggle, e.g. '3 AI' - or the prompt " +
                                 "while placement is armed.")]
        TMP_Text fillSummaryText;

        [Header("Copy")]
        [SerializeField, Tooltip("Summary written when the roster is all human.")]
        string noAiSummary = "No AI";

        [SerializeField, Tooltip("Summary written when AI are seated. {0} is the AI count.")]
        string aiSummaryFormat = "{0} AI";

        [SerializeField, Tooltip("Summary written while Add AI placement is armed.")]
        string addAiArmedSummary = "TAP A DOMAIN";

        /// <summary>The ✕ on an AI seat: remove the placed AI with this ordinal (0 = the first
        /// AI seat, i.e. roster index minus the humans).</summary>
        public event Action<int> OnKickAIRequested;

        /// <summary>The Add AI toggle moved. True = domain taps now place AI.</summary>
        public event Action<bool> OnAddAIModeChanged;

        readonly List<LobbySlotView> _slots = new();
        bool _suppressToggleCallback;
        bool _addAiArmed;

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
        /// <param name="totalPlayers">Seats the match will hold (humans + placed AI + any empty
        /// seats the card's minimum will top up at launch).</param>
        /// <param name="humanCount">How many of those seats are humans.</param>
        /// <param name="aiDomains">The placed AIs, in placement order - each seat wears its
        /// domain's signal colour. Null or short means the remaining seats draw EMPTY (they will
        /// be topped up balanced at launch).</param>
        /// <param name="readyCount">How many humans have confirmed.</param>
        /// <param name="localReady">Whether the LOCAL player has confirmed — known exactly.</param>
        /// <param name="isHost">Only the host may kick or place AI.</param>
        /// <param name="addAiArmed">Whether Add AI placement mode is armed (host only).</param>
        public void Refresh(GameDataSO gameData, int totalPlayers, int humanCount,
                            IReadOnlyList<Domains> aiDomains,
                            int readyCount, bool localReady, bool isHost, bool addAiArmed)
        {
            totalPlayers = Mathf.Max(1, totalPlayers);
            humanCount = Mathf.Clamp(humanCount, 0, totalPlayers);
            int placedAi = aiDomains?.Count ?? 0;

            var humans = CollectHumans(gameData);
            ulong localId = NetworkManager.Singleton ? NetworkManager.Singleton.LocalClientId : 0UL;
            var dataService = PlayerDataService.Instance;
            var colorSet = gameData && gameData.ThemeManagerData ? gameData.ThemeManagerData.ColorSet : null;

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

            for (int ai = 0; ai < placedAi && seat < totalPlayers; ai++, seat++)
            {
                var slot = SlotAt(seat);
                if (!slot) break;

                var domain = aiDomains[ai];
                int ordinal = ai;   // captured per seat - the kick names THIS placement

                // The live domain colour, resolved per draw (never snapshotted): the accessor
                // returns white for an unauthored domain, so a seat can mis-tint but never vanish.
                Color? tint = colorSet ? colorSet.GetDomainSignalColor(domain) : (Color?)null;

                slot.ShowAI(seat, $"{domain} AI".ToUpperInvariant(), kickable: isHost,
                            onKick: _ => OnKickAIRequested?.Invoke(ordinal), tint: tint);
            }

            // Seats the card's minimum still owes: drawn EMPTY, filled balanced at launch. An
            // empty seat is the honest read - nothing is placed there yet.
            for (; seat < totalPlayers; seat++)
            {
                var slot = SlotAt(seat);
                if (!slot) break;
                slot.ShowEmpty(seat);
            }

            for (int i = seat; i < _slots.Count; i++)
                if (_slots[i]) _slots[i].gameObject.SetActive(false);

            _addAiArmed = addAiArmed;
            SetAddAIModeSilently(addAiArmed);
            RefreshFillControls(placedAi, isHost);

            CSDebug.LogVerbose(CSLogChannel.ArcadeLaunch,
                $"[ArcadeLaunch] Roster: {humanCount} human ({lit} ready, local={localReady}) " +
                $"+ {placedAi} placed AI = {totalPlayers} seats (armed={addAiArmed}).");
        }

        /// <summary>Whether Add AI placement is armed. False when this row has no toggle wired.</summary>
        public bool AddAIModeArmed => fillWithAIToggle && fillWithAIToggle.isOn;

        /// <summary>
        /// Set the toggle without raising <see cref="OnAddAIModeChanged"/> — used when the panel
        /// re-asserts the mode from its own state rather than the other way round.
        /// </summary>
        public void SetAddAIModeSilently(bool on)
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
                fillSummaryText.text = _addAiArmed
                    ? addAiArmedSummary
                    : aiCount > 0 ? string.Format(aiSummaryFormat, aiCount) : noAiSummary;
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

        void HandleFillToggled(bool on)
        {
            if (_suppressToggleCallback) return;
            OnAddAIModeChanged?.Invoke(on);
        }
    }
}
