// PORT Deviation — type-preserving SHELL of Assets/_Scripts/UI/FX/IconEmitter.cs
// (253 lines of UI juice: a burst of icon images arcing from a source to a target,
// used by GameplayRewardButton's claim flash and currency balance changes). Landed
// as a shell in the Hangar unit because the reward button only calls EmitIcons()
// fire-and-forget — presentation-only, no gameplay state. The real port lands with
// the UI-FX arc.
using CosmicShore.Engine;

namespace CosmicShore.UI
{
    public class IconEmitter : MonoBehaviour
    {
        public enum EmissionMode { RandomAngle, Sweep, Scatter }

        /// <summary>Shell: the icon-arc burst is presentation-only — no-op headless.</summary>
        public void EmitIcons() { }
    }
}
