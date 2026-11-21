// Ported verbatim from Assets/_Scripts/UI/ToastSystem/ToastAnimation.cs
// (UI-shell arc 2026-07-08). FULLY LIVE — pure enum (values frozen in EnumFreezeTests).

namespace CosmicShore.UI
{
    public enum ToastAnimation
    {
        ChatSubtleSlide,   // bottom->top, light fade+offset (default for prefix-only)
        Pop,               // for emphasis
        Fade               // minimal
    }
}
