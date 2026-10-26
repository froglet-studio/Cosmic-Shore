using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The configure window's pilots as a lineup of pedestals (Docs/MENU_REDESIGN_B.md section 74, the
    /// approved R20ConfigureG2 board): every seat the match holds, each pilot standing on a pool of their
    /// team's light, and the open seats ready for an AI.
    ///
    /// <para>It is a VIEW. The modal still owns the roster - who is seated, which team each pilot and each
    /// placed AI is on, who may kick - and hands it here as a list of <see cref="Seat"/>s on every roster
    /// redraw. What a pilot does here goes back as an event the modal answers with the same methods its
    /// domain tiles always used (Add AI, a domain pick, a kick), so the network and the placement rules
    /// stay in one place.</para>
    ///
    /// <para>Teams: a seat's team stepper changes it with left / right (yours, or an AI's for the host).
    /// A new AI joins the team with the fewest pilots - the modal decides which that is.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI Kit/Configure Seat Lineup")]
    public class ConfigureSeatLineup : MonoBehaviour
    {
        public enum SeatKind { Human = 0, Ai = 1 }

        /// <summary>One filled seat, as the modal sees it.</summary>
        public struct Seat
        {
            public SeatKind Kind;
            public string Name;
            public Sprite Avatar;
            public Domains Domain;
            public bool IsLocal;
            public bool IsHost;
            /// <summary>The AI's index in the modal's placement list (AI seats only).</summary>
            public int AiOrdinal;
            /// <summary>Whether this pilot may change the seat's team here.</summary>
            public bool CanChangeTeam;
            /// <summary>Whether the ✕ shows (a placed AI the match can spare, for the host).</summary>
            public bool CanKick;
            /// <summary>The hull this seat flies (its icon), or null when none is decided yet.</summary>
            public Sprite Hull;
            /// <summary>Whether pressing the seat points the vessel strip at it (a card whose hulls are picked).</summary>
            public bool CanPickVessel;
            /// <summary>Whether the vessel strip picks this seat's hull now.</summary>
            public bool EditingVessel;
        }

        [Header("Seats")]
        [Tooltip("The pedestals, left to right. A card that seats fewer pilots hides the rest.")]
        [SerializeField] private ConfigureSeatPedestal[] pedestals = { };

        [Header("Add AI")]
        [Tooltip("The ADD AI PILOT prompt under the lineup (gamepad X).")]
        [SerializeField] private Button addAiButton;
        [Tooltip("The prompt's group: shown while an AI may be added.")]
        [SerializeField] private CanvasGroup addAiGroup;

        [Header("AI portraits")]
        [Tooltip("The AI roster (MainAIProfileList). An AI seat shows one of its portraits, the same one for " +
                 "the same seat while the lobby is open. The match draws its own pilots when it spawns, so the " +
                 "seat names no pilot.")]
        [SerializeField] private SO_AIProfileList aiProfiles;

        [Header("Words")]
        [Tooltip("An AI seat's name.")]
        [SerializeField] private string aiName = "AI pilot";
        [Tooltip("The status under the host's name.")]
        [SerializeField] private string hostLine = "Host";
        [Tooltip("The status under your own name when you are not the host.")]
        [SerializeField] private string youLine = "You";
        [Tooltip("The status under another pilot's name. Empty shows nothing.")]
        [SerializeField] private string pilotLine = "";
        [Tooltip("An AI's status where the card offers AI difficulty. {difficulty} is its name.")]
        [SerializeField] private string aiDifficultyLine = "{difficulty} AI";
        [Tooltip("An AI's status where the card does not offer difficulty.")]
        [SerializeField] private string aiLine = "AI";
        [Tooltip("Easy, Medium, Hard and Legendary, in that order (AIDifficulty 1-4).")]
        [SerializeField] private string[] difficultyNames = { "Easy", "Medium", "Hard", "Legendary" };
        [Tooltip("An open seat's name.")]
        [SerializeField] private string openName = "Open";
        [Tooltip("An open seat's line. {n} is its seat number.")]
        [SerializeField] private string seatLine = "Seat {n}";
        [Tooltip("The team names, in the order Jade, Ruby, Gold.")]
        [SerializeField] private string[] teamNames = { "Jade", "Ruby", "Gold" };

        /// <summary>The ADD AI PILOT prompt, an open pedestal or gamepad X.</summary>
        public event Action OnAddAiRequested;

        /// <summary>A seat's team stepper moved: (the seat's index in the last <see cref="Show"/>, -1 or +1).</summary>
        public event Action<int, int> OnTeamStepRequested;

        /// <summary>The ✕ on an AI seat: (that AI's placement ordinal).</summary>
        public event Action<int> OnKickRequested;

        /// <summary>A seat was pressed to pick its hull: (the seat's index in the last <see cref="Show"/>).</summary>
        public event Action<int> OnSeatPicked;

        bool _canAddAi;
        AIDifficulty? _difficulty;

        void Awake()
        {
            if (addAiButton) addAiButton.onClick.AddListener(RequestAddAi);
            for (int i = 0; i < pedestals.Length; i++)
            {
                if (!pedestals[i]) continue;
                int index = i;
                pedestals[i].OnOpenPressed += RequestAddAi;
                pedestals[i].OnTeamStep += dir => OnTeamStepRequested?.Invoke(index, dir);
                pedestals[i].OnKick += ordinal => OnKickRequested?.Invoke(ordinal);
                pedestals[i].OnPicked += () => OnSeatPicked?.Invoke(index);
            }
        }

        void Update()
        {
            // X adds an AI, as the prompt says - never while the preview holds the stick (the pad is the
            // vessel's then) and never while the lineup is faded or the prompt is down.
            if (!_canAddAi || ModePreviewWindow.AnyHasFocus) return;
            var pad = Gamepad.current;
            if (pad == null || !pad.buttonWest.wasPressedThisFrame) return;
            if (addAiGroup && !addAiGroup.interactable) return;
            RequestAddAi();
        }

        void RequestAddAi()
        {
            if (_canAddAi) OnAddAiRequested?.Invoke();
        }

        /// <summary>
        /// How the AI flies, or null where the card does not offer difficulty (the AI seats then say only "AI").
        /// </summary>
        public void SetDifficulty(AIDifficulty? difficulty)
        {
            _difficulty = difficulty;
            foreach (var p in pedestals)
                if (p && p.IsAi) p.SetStatus(AiStatus());
        }

        /// <summary>
        /// Draw the roster: <paramref name="seats"/> in order, then open seats up to <paramref name="seatCount"/>
        /// (the match's seats). <paramref name="canAddAi"/>: this pilot may add one (the host, with a seat free).
        /// <paramref name="showTeams"/>: the card has teams to step through (a one-team card hides the steppers).
        /// </summary>
        public void Show(IReadOnlyList<Seat> seats, int seatCount, bool canAddAi, bool showTeams = true)
        {
            _canAddAi = canAddAi && seats.Count < seatCount;
            if (addAiGroup) KitVisibility.SetShown(addAiGroup, _canAddAi);
            if (addAiButton) addAiButton.interactable = _canAddAi;

            int shown = Mathf.Min(seatCount, pedestals.Length);
            for (int i = 0; i < pedestals.Length; i++)
            {
                var p = pedestals[i];
                if (!p) continue;
                if (i >= shown) { p.Hide(); continue; }

                if (i < seats.Count)
                {
                    var s = seats[i];
                    bool ai = s.Kind == SeatKind.Ai;
                    p.ShowPilot(ai ? aiName : s.Name, ai ? AiPortrait(s.AiOrdinal) : s.Avatar, s.Domain,
                                TeamName(s.Domain), ai ? AiStatus() : HumanStatus(s), ai, s.IsLocal,
                                s.CanChangeTeam && showTeams, s.CanKick, s.AiOrdinal);
                    p.SetTeamShown(showTeams);
                    p.SetVessel(s.Hull, s.CanPickVessel, s.EditingVessel);
                }
                else
                {
                    p.ShowOpen(openName, TemplateText.Fill(seatLine, "n", (i + 1).ToString("00")), _canAddAi);
                }
            }
        }

        string HumanStatus(Seat s) => s.IsHost ? hostLine : s.IsLocal ? youLine : pilotLine;

        string AiStatus()
        {
            if (_difficulty is not { } d) return aiLine;
            int i = (int)d - 1;
            string name = difficultyNames != null && i >= 0 && i < difficultyNames.Length ? difficultyNames[i] : string.Empty;
            return TemplateText.Fill(aiDifficultyLine, "difficulty", name);
        }

        string TeamName(Domains domain)
        {
            int i = domain switch { Domains.Jade => 0, Domains.Ruby => 1, Domains.Gold => 2, _ => -1 };
            return teamNames != null && i >= 0 && i < teamNames.Length ? teamNames[i] : string.Empty;
        }

        /// <summary>A stable portrait for an AI seat: the same seat shows the same face while the lobby is open.</summary>
        Sprite AiPortrait(int ordinal)
        {
            var list = aiProfiles ? aiProfiles.AiProfiles : null;
            if (list == null || list.Count == 0) return null;
            return list[((ordinal % list.Count) + list.Count) % list.Count].AvatarSprite;
        }
    }
}
