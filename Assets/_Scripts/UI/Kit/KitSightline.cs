using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// "Can anyone see this right now?" - the test the kit's endless loops (<see cref="KitAmbient"/>,
    /// <see cref="KitSpark"/>) sleep on.
    ///
    /// <para>Menu windows and screens are never deactivated: a closed modal sits at CanvasGroup alpha 0
    /// and a screen the strip has slid away sits outside the strip's RectMask2D. An ambient loop under
    /// either kept moving its RectTransform every frame, and every move re-batches the whole canvas it
    /// lives on - Menu_Main's, thousands of graphics - for an object nobody could see. Roughly seventy
    /// such loops shipped across the closed windows and off-screen screens, plus two on GameCanvas that
    /// ran for a whole match (Docs/MENU_REDESIGN_B.md section 24).</para>
    ///
    /// <para>Two cheap reads, no scene search: the product of the CanvasGroups above the object
    /// (cached once, re-cached on a reparent), and whether a renderer that is not self-masked has been
    /// culled by a RectMask2D.</para>
    /// </summary>
    public sealed class KitSightline
    {
        /// <summary>Below this combined alpha the object counts as hidden.</summary>
        public const float HiddenAlpha = 0.01f;

        readonly List<CanvasGroup> _groups = new();
        readonly CanvasGroup _exclude;
        readonly CanvasRenderer _cullProbe;

        /// <param name="from">The animated object.</param>
        /// <param name="exclude">A group the caller animates itself (a Breathe) - left out of the product.</param>
        /// <param name="cullProbe">A renderer whose RectMask2D cull says the object is off-screen; null skips it.</param>
        public KitSightline(Transform from, CanvasGroup exclude, CanvasRenderer cullProbe)
        {
            _exclude = exclude;
            _cullProbe = cullProbe;
            Refresh(from);
        }

        /// <summary>Re-read the groups above <paramref name="from"/> (call on a reparent).</summary>
        public void Refresh(Transform from)
        {
            _groups.Clear();
            if (from) from.GetComponentsInParent(true, _groups);
        }

        /// <summary>True unless a group above zeroes the object or a mask has culled it.</summary>
        public bool Visible
        {
            get
            {
                float alpha = 1f;
                for (int i = 0; i < _groups.Count; i++)
                {
                    var group = _groups[i];
                    if (!group || !group.enabled) continue;
                    if (group != _exclude)
                    {
                        alpha *= group.alpha;
                        if (alpha < HiddenAlpha) return false;
                    }
                    if (group.ignoreParentGroups) break;
                }
                return !(_cullProbe && _cullProbe.cull);
            }
        }
    }
}
