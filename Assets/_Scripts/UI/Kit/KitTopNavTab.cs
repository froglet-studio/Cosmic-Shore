using System.Collections.Generic;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One top-nav tab (KitTopNavTab.prefab: HANGAR, ARK, HOME, PORT, PROFILE) as a pad stop. The pad reaches
    /// the tab bar by D-pad, and only there do LT / RT page the screens (<see cref="ScreenSwitcher"/>; Yash,
    /// 2026-10-25: "you have to navigate the tab menu to access it with LT and RT"; Docs/MENU_REDESIGN_B.md §64).
    ///
    /// <para>It owns only the focus ring, the sky-blue Selection with the selection light, drawn while the pad
    /// or keyboard has the tab. Everything else stays where it was: the host's EventTrigger presses the tab (a
    /// click, and A through its Submit entry), ScreenSwitcher shows the current screen's word and underline,
    /// <see cref="KitHoverTint"/> lifts the word under the pointer, and a locked tab's word belongs to its
    /// <see cref="MenuAvailabilityView"/>. The Button's own onClick stays empty, so a click never presses twice.</para>
    /// </summary>
    [UnityEngine.AddComponentMenu("Cosmic Shore/UI Kit/Kit Top Nav Tab")]
    public class KitTopNavTab : KitSelectableBase
    {
        protected override void CollectColors(VisualState state, List<(Graphic graphic, UnityEngine.Color color)> into) { }
    }
}
