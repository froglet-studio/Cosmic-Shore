using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A <see cref="RectMask2D"/> that stops clipping while nothing under it can be seen
    /// (Docs/PERFORMANCE_OPTIMIZATION.md, the 10-26 Menu_Main row).
    ///
    /// <para>Every enabled RectMask2D re-culls every graphic under it every frame
    /// (<c>ClipperRegistry.Cull</c>), whether or not anything moved. The menu never deactivates a
    /// closed window (it hides by CanvasGroup alpha) or a screen slid off the strip, so the Arcade
    /// grid, the Profile quest cards and every off-screen hub screen paid that pass on every frame:
    /// 3-4 ms in the 10-25/26 captures, against 0.3 ms before the style-B menu.</para>
    ///
    /// <para>This mask sleeps in two cases, both of which leave nothing visible under it:</para>
    /// <list type="bullet">
    /// <item>a CanvasGroup above it has the effective alpha at zero (a closed window, freestyle);</item>
    /// <item>its rect (with any negative padding) lies wholly outside the root canvas (a screen slid
    /// off the strip, a card scrolled far out of a viewport that is itself off-screen).</item>
    /// </list>
    /// <para>On falling asleep it runs ONE full pass, so a mask that went off-screen culls everything
    /// under it before it stops. Waking is the same frame the window opens or the rect re-enters the
    /// canvas: the clipping pass runs after layout, so it sees the new state before anything draws.
    /// The one thing a sleeping mask does not do is clip a graphic ADDED under it while it sleeps;
    /// that graphic is under an invisible group or an off-screen rect, and is clipped on wake.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Rect Mask 2D")]
    public class KitRectMask2D : RectMask2D
    {
        // Effective alpha at or below this counts as hidden.
        const float HiddenAlpha = 0.001f;

        static readonly Vector3[] Corners = new Vector3[4];
        static readonly List<CanvasGroup> Scratch = new();

        // The CanvasGroups whose alpha reaches this mask, nearest first, up to and including the first
        // that ignores its parents. Rebuilt when the hierarchy changes.
        readonly List<CanvasGroup> _groups = new();
        bool _groupsDirty = true;
        RectTransform _root;

        // A full pass has run since it last fell asleep.
        bool _asleep;

        public override void PerformClipping()
        {
            if (CannotBeSeen())
            {
                if (_asleep) return;
                base.PerformClipping();
                _asleep = true;
                return;
            }

            _asleep = false;
            base.PerformClipping();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _groupsDirty = true;
            _asleep = false;
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            _groupsDirty = true;
            _asleep = false;
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            _groupsDirty = true;
            _asleep = false;
        }

        protected override void OnCanvasGroupChanged()
        {
            base.OnCanvasGroupChanged();
            // A group added or removed above the mask; the alpha itself is read every pass.
            _groupsDirty = true;
        }

        bool CannotBeSeen()
        {
            if (_groupsDirty) RebuildGroups();
            return HiddenByGroups() || OutsideRootCanvas();
        }

        bool HiddenByGroups()
        {
            float alpha = 1f;
            for (int i = 0; i < _groups.Count; i++)
            {
                var group = _groups[i];
                if (!group)
                {
                    _groupsDirty = true;
                    return false;
                }
                if (!group.enabled) continue;
                alpha *= group.alpha;
                if (alpha <= HiddenAlpha) return true;
                if (group.ignoreParentGroups) break;
            }
            return false;
        }

        bool OutsideRootCanvas()
        {
            if (!_root) return false;

            rectTransform.GetWorldCorners(Corners);
            Matrix4x4 toRoot = _root.worldToLocalMatrix;
            Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = toRoot.MultiplyPoint3x4(Corners[i]);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            // Negative padding grows the clip area past the rect (x left, y bottom, z right, w top).
            Vector4 pad = padding;
            min -= new Vector2(Mathf.Max(0f, -pad.x), Mathf.Max(0f, -pad.y));
            max += new Vector2(Mathf.Max(0f, -pad.z), Mathf.Max(0f, -pad.w));

            return !Rect.MinMaxRect(min.x, min.y, max.x, max.y).Overlaps(_root.rect, true);
        }

        void RebuildGroups()
        {
            _groupsDirty = false;
            _groups.Clear();

            for (var t = transform; t; t = t.parent)
            {
                t.GetComponents(Scratch);
                bool stop = false;
                foreach (var group in Scratch)
                {
                    _groups.Add(group);
                    stop |= group.ignoreParentGroups;
                }
                if (stop) break;
            }
            Scratch.Clear();

            var canvas = GetComponentInParent<Canvas>();
            var rootCanvas = canvas ? canvas.rootCanvas : null;
            _root = rootCanvas ? rootCanvas.transform as RectTransform : null;
        }
    }
}
