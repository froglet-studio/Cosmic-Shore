using System;
namespace CosmicShore.Data
{
    [System.Serializable]
    public enum InputEvents
    {
        FullSpeedStraightAction = 0,        // Fly with full speed and straight
        RightStickAction = 1,               // Lifting a trigger or lifing a thumb
        LeftStickAction = 2,                // Lifting a trigger or lifing a thumb
        FlipAction = 3,                     // LEGACY, raised by nothing since 2026-10-27: RB / E raise PayloadAction (Docs/REFACTOR_BACKLOG.md S73). Kept for serialized bindings.
        IdleAction = 4,                     // Lift both thumbs or the right bumper
        MinimumSpeedStraightAction = 5,     // Fly with minimum speed and straight
        Button1Action = 6,                  // X or Onscreen UI buttons
        Button2Action = 7,                  // A or Onscreen UI buttons
        Button3Action = 8,                  // B or Onscreen UI buttons
        NodeTapAction = 9,                  //
        SelfTapAction = 10,
        OnlyRightStickAction = 11,
        OnlyLeftStickAction = 12,
        BothSticksAction = 13,//
        Button4Action = 14,                 // Y (pad north) / F; first bound by the Angler (dash 2026-10-16, recall since 2026-10-18)
        PayloadAction = 15,                 // RB (pad right shoulder) / E: grab and release the Ark's payload (PayloadCarrier, Docs/Pilots/ARTIFACTS.md §2)
    }
}