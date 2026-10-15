using CosmicShore.UI;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XInput;
using UnityEngine.UI;
using static CosmicShore.UI.ScreenSwitcher;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    [RequireComponent(typeof(Button))]
    public class ControllerButtonPress : MonoBehaviour
    {
        [SerializeField] List<MenuScreens> ActiveMenuScreens;
        [SerializeField] List<ModalWindows> ActiveModalWindows;
        [SerializeField] GamepadButton activationButton;
        [SerializeField] Image activationButtonImage;
        [SerializeField] Sprite TriangleButtonSprite;
        [SerializeField] Sprite CircleButtonSprite;
        [SerializeField] Sprite CrossButtonSprite;
        [SerializeField] Sprite SquareButtonSprite;
        [SerializeField] Sprite YButtonSprite;
        [SerializeField] Sprite BButtonSprite;
        [SerializeField] Sprite AButtonSprite;
        [SerializeField] Sprite XButtonSprite;
        [SerializeField] CanvasGroup canvasGroup;
        //[SerializeField] 

        EventSystem eventSystem;
        ScreenSwitcher screenSwitcher;
        Button button;
        
        bool _wasCanvasGroupInteractableLastFrame;
        
        void Start()
        {
            if (canvasGroup)
                _wasCanvasGroupInteractableLastFrame = canvasGroup.interactable;
            
            eventSystem = FindAnyObjectByType<EventSystem>();
            screenSwitcher = FindAnyObjectByType<ScreenSwitcher>();
            button = GetComponent<Button>();

            if (!activationButtonImage)
                return;

            ApplyGlyph(Gamepad.current);
        }

        // Which glyph set a pad gets. Explicit values: never reorder.
        enum PadGlyphs
        {
            None = 0,
            PlayStation = 1,
            Xbox = 2,
        }

        // The pad whose glyph is showing. A browser reveals a pad only after one of its buttons is pressed,
        // which is usually after Start, and a pad can be plugged in mid-session, so Update re-picks the glyph
        // whenever Gamepad.current changes.
        Gamepad _glyphPad;

        void ApplyGlyph(Gamepad pad)
        {
            _glyphPad = pad;
            Sprite sprite = GlyphFamilyOf(pad) switch
            {
                PadGlyphs.PlayStation => activationButton switch
                {
                    GamepadButton.North => TriangleButtonSprite,
                    GamepadButton.East => CircleButtonSprite,
                    GamepadButton.South => CrossButtonSprite,
                    GamepadButton.West => SquareButtonSprite,
                    _ => null,
                },
                PadGlyphs.Xbox => activationButton switch
                {
                    GamepadButton.North => YButtonSprite,
                    GamepadButton.East => BButtonSprite,
                    GamepadButton.South => AButtonSprite,
                    GamepadButton.West => XButtonSprite,
                    _ => null,
                },
                _ => null,
            };
            if (sprite) activationButtonImage.sprite = sprite;

            // Hide the glyph when there is none to show. If the image sits on this button's own object, only
            // the image is switched, so this component keeps running and can show it again for the next pad.
            if (activationButtonImage.gameObject == gameObject) activationButtonImage.enabled = sprite;
            else activationButtonImage.gameObject.SetActive(sprite);
        }

        static PadGlyphs GlyphFamilyOf(Gamepad pad)
        {
            if (pad is DualShockGamepad) return PadGlyphs.PlayStation;
            if (pad is XInputController) return PadGlyphs.Xbox;

            // A browser hands every pad over as a generic standard-mapped gamepad (WebGLGamepad), laid out like
            // an Xbox pad. Its description carries the browser's id string, which names Sony's USB vendor id
            // (054c) for a PlayStation pad, e.g. "DualSense Wireless Controller (STANDARD GAMEPAD Vendor: 054c ...)".
            if (pad != null && Application.platform == RuntimePlatform.WebGLPlayer)
                return IsSonyPad(pad.description.product) || IsSonyPad(pad.description.capabilities)
                    ? PadGlyphs.PlayStation
                    : PadGlyphs.Xbox;

            return PadGlyphs.None;
        }

        static bool IsSonyPad(string id) =>
            !string.IsNullOrEmpty(id) &&
            (id.IndexOf("054c", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
             id.IndexOf("dualsense", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
             id.IndexOf("dualshock", System.StringComparison.OrdinalIgnoreCase) >= 0);

        void Update()
        {
            if (activationButtonImage && Gamepad.current != _glyphPad)
                ApplyGlyph(Gamepad.current);

            // The appshell-owns-the-gamepad signal: ScreenSwitcher's freestyle gate turns
            // sendNavigationEvents off while the player is flying a vessel. This component
            // polls the pad directly (bypassing the EventSystem), so it must consult the
            // same gate — otherwise appshell buttons (e.g. the daily-challenge card) keep
            // firing off vessel ability presses during freestyle. Screens are only FADED in
            // freestyle, so the ScreenIsActive checks below do not cover this.
            if (eventSystem && !eventSystem.sendNavigationEvents)
                return;

            // Remove the null check -> after making sure every place of ControllerButtonPress has a canvas group
            // reference added to them

            if (canvasGroup)
            {
                bool wasInteractableLastFrame = _wasCanvasGroupInteractableLastFrame;
                _wasCanvasGroupInteractableLastFrame = canvasGroup.interactable;    
                
                if (!wasInteractableLastFrame)
                    return;
            }
            
            if (screenSwitcher != null)
            {
                // If the screen context is active do the action
                foreach (var screen in ActiveMenuScreens)
                {
                    if (screenSwitcher.ScreenIsActive(screen))
                    {
                        if (Gamepad.current != null && Gamepad.current[activationButton].wasPressedThisFrame)
                        {
                            button.onClick.Invoke();
                            button.OnDeselect(new BaseEventData(eventSystem));
                            return;
                        }
                    }
                }

                // If the modal context is active do the action
                foreach (var modal in ActiveModalWindows)
                {
                    if (screenSwitcher.ModalIsActive(modal))
                    {
                        if (Gamepad.current != null && Gamepad.current[activationButton].wasPressedThisFrame)
                        {
                            button.onClick.Invoke();
                            button.OnDeselect(new BaseEventData(eventSystem));
                            return;
                        }
                    }
                }
            }
            // Kinda lame-o approach to keep in game UI working (which doesn't have nested modals or return to screens)
            else
            {
                if (Gamepad.current != null && Gamepad.current[activationButton].wasPressedThisFrame)
                {
                    button.onClick.Invoke();
                    button.OnDeselect(new BaseEventData(eventSystem));
                }
            }
        }
    }
}