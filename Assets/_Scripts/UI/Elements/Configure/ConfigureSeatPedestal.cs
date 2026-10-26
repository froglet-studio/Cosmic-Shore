using System;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One seat of <see cref="ConfigureSeatLineup"/>: a pilot's portrait standing on a pool of their team's
    /// light, their name, a status line and the team stepper - or, for an open seat, the + octagon that
    /// adds an AI. It draws what the lineup hands it and raises what the pilot presses; it decides nothing.
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI Kit/Configure Seat Pedestal")]
    public class ConfigureSeatPedestal : MonoBehaviour
    {
        [Header("Groups")]
        [Tooltip("The whole seat: hidden past the match's seat count.")]
        [SerializeField] private CanvasGroup seatGroup;
        [Tooltip("A taken seat's parts.")]
        [SerializeField] private CanvasGroup pilotGroup;
        [Tooltip("An open seat's parts.")]
        [SerializeField] private CanvasGroup openGroup;

        [Header("Pilot")]
        [Tooltip("The pilot's portrait.")]
        [SerializeField] private Image avatar;
        [Tooltip("The pool of light under the portrait, tinted the team's colour.")]
        [SerializeField] private Image pool;
        [Tooltip("The pool's rim, tinted the team's colour.")]
        [SerializeField] private Image poolRim;
        [Tooltip("Your own seat's glow. Optional.")]
        [SerializeField] private CanvasGroup localGlow;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text statusText;
        [Tooltip("The AI tag beside an AI's name.")]
        [SerializeField] private CanvasGroup aiTag;

        [Header("Team")]
        [Tooltip("Changes the seat's team with left / right; its arrows do the same by pointer.")]
        [SerializeField] private ConfigureTeamStepper teamStepper;
        [SerializeField] private TMP_Text teamText;

        [Header("Kick")]
        [Tooltip("The ✕ on a placed AI the host may remove.")]
        [SerializeField] private Button kickButton;
        [SerializeField] private CanvasGroup kickGroup;

        [Header("Open seat")]
        [Tooltip("The seat itself: on an open seat it adds an AI.")]
        [SerializeField] private Button seatButton;
        [SerializeField] private TMP_Text openNameText;
        [SerializeField] private TMP_Text openLineText;

        [Header("Colour")]
        [Tooltip("The domain palette: the pool and the team name take the team's colour.")]
        [SerializeField] private SO_ColorSet colorSet;
        [Tooltip("The chrome theme. Unwired falls back to the authored values.")]
        [SerializeField] private UIThemeSO theme;

        /// <summary>The open seat was pressed.</summary>
        public event Action OnOpenPressed;

        /// <summary>The team stepper moved (-1 or +1).</summary>
        public event Action<int> OnTeamStep;

        /// <summary>The ✕ was pressed (this AI's placement ordinal).</summary>
        public event Action<int> OnKick;

        /// <summary>Whether the seat shows an AI.</summary>
        public bool IsAi { get; private set; }

        bool _open;
        int _aiOrdinal = -1;

        void Awake()
        {
            if (seatButton) seatButton.onClick.AddListener(() => { if (_open) OnOpenPressed?.Invoke(); });
            if (kickButton) kickButton.onClick.AddListener(() => { if (IsAi && _aiOrdinal >= 0) OnKick?.Invoke(_aiOrdinal); });
            if (teamStepper) teamStepper.OnStep += dir => OnTeamStep?.Invoke(dir);
        }

        public void ShowPilot(string pilotName, Sprite portrait, Domains domain, string team, string status, bool isAi,
                              bool isLocal, bool canChangeTeam, bool canKick, int aiOrdinal)
        {
            _open = false;
            IsAi = isAi;
            _aiOrdinal = isAi ? aiOrdinal : -1;
            KitVisibility.SetShown(seatGroup, true);
            KitVisibility.SetShown(pilotGroup, true);
            KitVisibility.SetShown(openGroup, false);

            if (avatar)
            {
                avatar.sprite = portrait;
                avatar.enabled = portrait;
            }
            var colour = colorSet ? colorSet.GetDomainSignalColor(domain) : UITheme.Resolve(theme, UIColorToken.TextLight);
            if (pool) pool.color = new Color(colour.r, colour.g, colour.b, pool.color.a);
            if (poolRim) poolRim.color = new Color(colour.r, colour.g, colour.b, poolRim.color.a);
            KitVisibility.SetShown(localGlow, isLocal);
            KitVisibility.SetShown(aiTag, isAi);
            if (nameText) nameText.text = pilotName ?? string.Empty;
            SetStatus(status);
            if (teamText)
            {
                teamText.text = team ?? string.Empty;
                teamText.color = colorSet ? colorSet.GetDomainUIColor(domain) : colour;
            }
            if (teamStepper) teamStepper.SetChangeable(canChangeTeam);
            KitVisibility.SetShown(kickGroup, isAi && canKick);
            if (kickButton) kickButton.interactable = isAi && canKick;
            // A taken seat is not a button: the pad reaches its team stepper instead.
            if (seatButton) seatButton.interactable = false;
        }

        public void ShowOpen(string openName, string line, bool canAddAi)
        {
            _open = true;
            IsAi = false;
            _aiOrdinal = -1;
            KitVisibility.SetShown(seatGroup, true);
            KitVisibility.SetShown(pilotGroup, false);
            KitVisibility.SetShown(openGroup, true);
            KitVisibility.SetShown(kickGroup, false);
            if (kickButton) kickButton.interactable = false;
            if (teamStepper) teamStepper.SetChangeable(false);
            if (openNameText) openNameText.text = openName ?? string.Empty;
            if (openLineText) openLineText.text = line ?? string.Empty;
            if (seatButton) seatButton.interactable = canAddAi;
        }

        public void Hide()
        {
            _open = false;
            IsAi = false;
            _aiOrdinal = -1;
            KitVisibility.SetShown(seatGroup, false);
            if (seatButton) seatButton.interactable = false;
            if (kickButton) kickButton.interactable = false;
            if (teamStepper) teamStepper.SetChangeable(false);
        }

        public void SetStatus(string status)
        {
            if (statusText) statusText.text = status ?? string.Empty;
        }
    }
}
