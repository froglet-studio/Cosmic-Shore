using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CosmicShore.UI
{
    /// <summary>
    /// Makes an ability card (the lockup "totem": element plate over ability plate) a button for its
    /// ability. A finger down on the card presses the card's input and lifting it releases, so a tap
    /// is a tap and a hold is a hold - exactly as the control its chip names.
    ///
    /// <para>It raises the press on the pilot's <see cref="IInputStatus"/>, the channel every input
    /// strategy raises on, so the press takes the same path as the physical control: the same
    /// autopilot / mute / suppression gates in <see cref="R_VesselActionHandler"/>, the same
    /// replication (the press carries the device it resolved against), and the same card highlight.
    /// It never calls the handler's actions directly.</para>
    ///
    /// <para>Which input: the card's own, or - when that one does nothing on the device the pilot is
    /// holding - the first input of the same control that does (a trigger raises both the held and
    /// the "only" event; the Squirrel binds drift to different ones on touch and on a pad). A card
    /// with no input (the passive sentinel), a locked card, and a card driven by a gesture no button
    /// names are not pressable.</para>
    ///
    /// <para>Lives on the card's HOST (the icon's parent), so a hit on the icon or on either plate
    /// reaches it, and it shares the GameObject with the host's own press juice. Touches that start
    /// on the card are kept out of touch flight (<see cref="FlightTouchClaims"/>).</para>
    /// </summary>
    public class AbilityCardPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        IInputStatus _input;
        R_VesselActionHandler _actions;
        InputEvents _bound;
        bool _pressable;
        RectTransform _region;

        bool _holding;
        int _pointerId;
        InputEvents _held;

        public bool Pressable => _pressable;

        /// <summary>The rect that owns touches starting on it - the card, so both plates count.</summary>
        public void SetRegion(RectTransform region)
        {
            if (_region == region) return;
            FlightTouchClaims.Remove(_region);
            _region = region;
            RefreshClaim();
        }

        /// <summary>Who the press goes to. Set by the vessel HUD controller once it knows its pilot.</summary>
        public void Bind(IInputStatus input, R_VesselActionHandler actions)
        {
            if (_holding && _input != input) Release();
            _input = input;
            _actions = actions;
        }

        public void SetInput(InputEvents input, bool pressable)
        {
            if (_holding && (!pressable || input != _bound)) Release();
            _bound = input;
            _pressable = pressable && input != InputEvents.FullSpeedStraightAction;
            RefreshClaim();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_holding || !_pressable || _input == null || !isActiveAndEnabled) return;
            if (!TryResolveInput(out var ie)) return;

            _holding = true;
            _pointerId = eventData.pointerId;
            _held = ie;
            _input.OnButtonPressed.Raise(ie);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_holding && eventData.pointerId == _pointerId) Release();
        }

        void OnEnable() => RefreshClaim();

        // A hidden HUD, a vessel swap or a teardown must never leave the ability held.
        void OnDisable()
        {
            Release();
            FlightTouchClaims.Remove(_region);
        }

        void Release()
        {
            if (!_holding) return;
            _holding = false;
            _input?.OnButtonReleased.Raise(_held);
        }

        void RefreshClaim()
        {
            if (!_region) return;
            if (_pressable && isActiveAndEnabled) FlightTouchClaims.Add(_region);
            else FlightTouchClaims.Remove(_region);
        }

        bool TryResolveInput(out InputEvents ie)
        {
            ie = _bound;
            if (!_actions || _actions.ResolvesOnActiveDevice(_bound)) return true;

            // The same control's other events, pad table first (the keyboard's shifts name the same pair).
            for (int k = 0; k < 2; k++)
            {
                var binding = InputHintBindingMap.BindingFor(_bound, keyboard: k == 1);
                var siblings = InputHintBindingMap.InputEventsFor(binding);
                for (int i = 0; i < siblings.Count; i++)
                {
                    if (siblings[i] == _bound || !_actions.ResolvesOnActiveDevice(siblings[i])) continue;
                    ie = siblings[i];
                    return true;
                }
            }
            return false;
        }
    }
}
