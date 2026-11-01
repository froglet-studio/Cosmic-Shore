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
    ///
    /// <para>Rev 25 (Docs/MENU_REDESIGN_B.md section 79): the pilot stands on the hull they fly, and on a card
    /// whose hulls are picked (an Arena card, a voyage) pressing the pilot points the vessel strip under the
    /// lineup at this seat. A card with one team hides the team stepper.</para>
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
        [Tooltip("The stepper's group: hidden on a card with one team. Optional.")]
        [SerializeField] private CanvasGroup teamGroup;

        [Header("Vessel")]
        [Tooltip("The hull the pilot flies (SO_Vessel.IconActive), under their feet. Optional.")]
        [SerializeField] private Image hullImage;
        [Tooltip("The hull's group: hidden while the seat names no hull. Optional.")]
        [SerializeField] private CanvasGroup hullGroup;
        [Tooltip("The pilot and their hull as a button: points the vessel strip at this seat. Optional.")]
        [SerializeField] private Button pickButton;
        [Tooltip("The podium's glow, lit while the vessel strip picks this seat's hull (no outline round the pilot). Optional.")]
        [SerializeField] private CanvasGroup editingMark;
        [Tooltip("The glow's pool of light, tinted the team's colour like the pool. Optional.")]
        [SerializeField] private Image podiumGlow;

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

        /// <summary>The pilot was pressed: the vessel strip should pick this seat's hull.</summary>
        public event Action OnPicked;

        /// <summary>Whether the seat shows an AI.</summary>
        public bool IsAi { get; private set; }

        bool _open;
        bool _pickable;
        int _aiOrdinal = -1;

        void Awake()
        {
            if (seatButton) seatButton.onClick.AddListener(() => { if (_open) OnOpenPressed?.Invoke(); });
            if (kickButton) kickButton.onClick.AddListener(() => { if (IsAi && _aiOrdinal >= 0) OnKick?.Invoke(_aiOrdinal); });
            if (teamStepper) teamStepper.OnStep += dir => OnTeamStep?.Invoke(dir);
            if (pickButton) pickButton.onClick.AddListener(() => { if (_pickable) OnPicked?.Invoke(); });
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
            if (podiumGlow) podiumGlow.color = new Color(colour.r, colour.g, colour.b, podiumGlow.color.a);
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
            SetVessel(null, false, false);
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
            SetVessel(null, false, false);
        }

        /// <summary>
        /// The hull this pilot flies (null hides it), whether pressing the pilot points the vessel strip here, and
        /// whether the strip picks this seat's hull now.
        /// </summary>
        public void SetVessel(Sprite hull, bool pickable, bool editing)
        {
            if (hullImage)
            {
                hullImage.sprite = hull;
                hullImage.enabled = hull;
            }
            KitVisibility.SetShown(hullGroup, hull);
            _pickable = pickable;
            if (pickButton)
            {
                pickButton.interactable = pickable;
                var nav = pickButton.navigation;
                nav.mode = pickable ? Navigation.Mode.Automatic : Navigation.Mode.None;
                pickButton.navigation = nav;
            }
            KitVisibility.SetShown(editingMark, pickable && editing);
        }

        /// <summary>Whether the team stepper shows at all (a card with one team has nothing to step).</summary>
        public void SetTeamShown(bool shown) => KitVisibility.SetShown(teamGroup, shown);

        public void SetStatus(string status)
        {
            if (statusText) statusText.text = status ?? string.Empty;
        }
    }
}
