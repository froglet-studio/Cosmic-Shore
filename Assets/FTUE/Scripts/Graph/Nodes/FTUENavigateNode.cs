using System.Collections;
using CosmicShore.UI;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Which menu destination a <see cref="FTUENavigateNode"/> drives. Maps to the public
    /// <see cref="ScreenSwitcher"/> nav handlers. Arcade is a modal overlay, not a slide
    /// screen, hence <see cref="ArcadeModal"/>.
    /// </summary>
    public enum FTUENavTarget
    {
        Home,
        Store,
        Port,
        Hangar,
        Profile,
        ArcadeModal,
    }

    /// <summary>
    /// Programmatically navigates the menu to a destination via <see cref="ScreenSwitcher"/>,
    /// then advances. Use this to *force* navigation (scripted tour). To instead *ask* the
    /// player to navigate and wait for them, use a <see cref="FTUEHighlightCTANode"/>.
    /// </summary>
    public class FTUENavigateNode : FTUENodeSO
    {
        [Tooltip("Menu destination to navigate to.")]
        public FTUENavTarget target = FTUENavTarget.ArcadeModal;

        public override IEnumerator Execute(FTUERuntimeContext ctx, System.Action<string> advance)
        {
            if (ctx.ScreenSwitcher == null)
            {
                Debug.LogWarning("[FTUE] NavigateNode: ScreenSwitcher not wired — skipping navigation.");
                advance(FTUEPorts.Next);
                yield break;
            }

            switch (target)
            {
                case FTUENavTarget.Home: ctx.ScreenSwitcher.OnClickHomeNav(); break;
                case FTUENavTarget.Store: ctx.ScreenSwitcher.OnClickStoreNav(); break;
                case FTUENavTarget.Port: ctx.ScreenSwitcher.OnClickPortNav(); break;
                case FTUENavTarget.Hangar: ctx.ScreenSwitcher.OnClickHangarNav(); break;
                case FTUENavTarget.Profile: ctx.ScreenSwitcher.OnClickProfileNav(); break;
                case FTUENavTarget.ArcadeModal: ctx.ScreenSwitcher.OnClickArcadeNav(); break;
            }

            advance(FTUEPorts.Next);
        }
    }
}
