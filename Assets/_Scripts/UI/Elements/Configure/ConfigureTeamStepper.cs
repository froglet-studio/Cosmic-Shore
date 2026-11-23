using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A seat's team, changed in place: with the stepper focused, left / right step the team instead of
    /// moving the focus (up / down still navigate), and its two arrows do the same by pointer. Shombith,
    /// 2026-10-26: "focus a seat, then press left/right".
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI Kit/Configure Team Stepper")]
    public class ConfigureTeamStepper : Selectable
    {
        [Header("Arrows")]
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [Tooltip("The arrows' group: shown only while the team can be changed here.")]
        [SerializeField] private CanvasGroup arrowsGroup;

        /// <summary>-1 or +1.</summary>
        public event Action<int> OnStep;

        protected override void Awake()
        {
            base.Awake();
            if (previousButton) previousButton.onClick.AddListener(() => Step(-1));
            if (nextButton) nextButton.onClick.AddListener(() => Step(+1));
        }

        /// <summary>Whether this pilot may change the seat's team: off, it is a plain label out of navigation.</summary>
        public void SetChangeable(bool changeable)
        {
            interactable = changeable;
            var nav = navigation;
            nav.mode = changeable ? Navigation.Mode.Automatic : Navigation.Mode.None;
            navigation = nav;
            KitVisibility.SetShown(arrowsGroup, changeable);
            if (previousButton) previousButton.interactable = changeable;
            if (nextButton) nextButton.interactable = changeable;
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (IsInteractable() && eventData.moveDir is MoveDirection.Left or MoveDirection.Right)
            {
                Step(eventData.moveDir == MoveDirection.Left ? -1 : +1);
                eventData.Use();
                return;
            }
            base.OnMove(eventData);
        }

        void Step(int direction)
        {
            if (IsInteractable()) OnStep?.Invoke(direction);
        }
    }
}
