public enum InputDeviceType
{
    Touch = 0,
    Gamepad = 1,
    Keyboard = 2,
    DualMouse = 3,

    // Desktop mouse + keyboard on a ONE-THUMB vessel: the mouse is the single stick, the
    // keyboard and the mouse buttons are the pad's buttons and triggers. See
    // SingleStickMouseInputStrategy. Anything that switches on this enum and treats "not
    // gamepad" as "binary triggers, needs easing" (VesselTransformer.GetTriggerSum and its two
    // ease sites) is already correct for it; anything that maps a device to a per-trigger
    // override table (R_VesselActionHandler.GetActiveOverrides) has to name it explicitly,
    // because this scheme raises the gamepad's trigger events.
    MouseKeyboard = 4
}
