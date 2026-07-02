using System.Collections;
using CosmicShore.UI;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Which menu destination a <see cref="QuestNavigateNode"/> drives. Maps to the public
    /// <see cref="ScreenSwitcher"/> nav handlers. Arcade is a modal overlay, not a slide
    /// screen, hence <see cref="ArcadeModal"/>.
    /// </summary>
    public enum QuestNavTarget
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
    /// player to navigate and wait for them, use a <see cref="QuestHighlightCTANode"/>.
    /// </summary>
    public class QuestNavigateNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Gameplay;
        public override string TypeTooltip => "Force-navigates the menu to a screen or the arcade modal (scripted tour; use HighlightCTA to ask instead).";
        public override string EditorSummary => $"→ {target}";

        [Tooltip("Menu destination to navigate to.")]
        public QuestNavTarget target = QuestNavTarget.ArcadeModal;

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            if (ctx.ScreenSwitcher == null)
            {
                Debug.LogWarning("[Quest] NavigateNode: ScreenSwitcher not wired — skipping navigation.");
                advance(QuestPorts.Next);
                yield break;
            }

            switch (target)
            {
                case QuestNavTarget.Home: ctx.ScreenSwitcher.OnClickHomeNav(); break;
                case QuestNavTarget.Store: ctx.ScreenSwitcher.OnClickStoreNav(); break;
                case QuestNavTarget.Port: ctx.ScreenSwitcher.OnClickPortNav(); break;
                case QuestNavTarget.Hangar: ctx.ScreenSwitcher.OnClickHangarNav(); break;
                case QuestNavTarget.Profile: ctx.ScreenSwitcher.OnClickProfileNav(); break;
                case QuestNavTarget.ArcadeModal: ctx.ScreenSwitcher.OnClickArcadeNav(); break;
            }

            advance(QuestPorts.Next);
        }
    }
}
