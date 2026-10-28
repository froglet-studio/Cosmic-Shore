using System.Collections.Generic;
using TMPro;
using Unity.Profiling;
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
    ///
    /// <para><b>Asleep, it also parks the text under it</b> (#1485, the 10-29 row). Every enabled
    /// TextMeshProUGUI whose <see cref="TMP_Text.isTextObjectScaleStatic"/> is false runs
    /// <c>InternalUpdate</c> every frame inside <c>Canvas.SendWillRenderCanvases</c> (a lossy-scale read
    /// and an env-map check), seen or not. TMP stops that itself when the text's Canvas is disabled; this
    /// does the same for the menu's other two kinds of "hidden": the texts that were active and not
    /// static when the mask fell asleep are made static, and exactly those are given back on wake (or
    /// when the mask is disabled or destroyed). Nothing is drawn differently: a parked text is under an
    /// invisible group or an off-screen rect, and on wake TMP's next InternalUpdate compares the
    /// lossy scale against the value it held before parking, so a scale change made meanwhile is still
    /// picked up. No project text is authored static, so giving back <c>false</c> is the authored value.</para>
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

        // The texts this mask made static when it fell asleep - only these are given back.
        readonly List<TMP_Text> _parked = new();
        static readonly List<TMP_Text> TextScratch = new();
        static readonly List<KitRectMask2D> MaskScratch = new();

        // An awake mask's clipping pass: its call count under ClipperRegistry.Cull is the number of
        // kit masks awake that frame, which is what to read when Cull is higher than expected.
        static readonly ProfilerMarker ClipMarker = new("KitRectMask2D.Clip");
        static readonly ProfilerMarker ParkMarker = new("KitRectMask2D.ParkText");
        static readonly ProfilerMarker UnparkMarker = new("KitRectMask2D.UnparkText");

        /// <summary>True while the mask is skipping its clipping pass (nothing under it can be seen).</summary>
        public bool Asleep => _asleep;

        /// <summary>How many texts this mask is holding static while it sleeps.</summary>
        public int ParkedTextCount => _parked.Count;

        public override void PerformClipping()
        {
            if (CannotBeSeen())
            {
                if (_asleep) return;
                base.PerformClipping();
                _asleep = true;
                ParkTexts();
                return;
            }

            if (_asleep) Wake();
            using (ClipMarker.Auto())
                base.PerformClipping();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _groupsDirty = true;
            Wake();
        }

        protected override void OnDisable()
        {
            // A disabled mask no longer decides anything about the texts under it.
            Wake();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            Wake();
            base.OnDestroy();
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            _groupsDirty = true;
            Wake();
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            _groupsDirty = true;
            Wake();
        }

        // Awake again: give the parked texts back, then let any kit mask below that is still asleep
        // (a screen still off the strip when the strip itself wakes) take its own texts back out.
        void Wake()
        {
            _asleep = false;
            if (_parked.Count == 0) return;

            using (UnparkMarker.Auto())
            {
                for (int i = 0; i < _parked.Count; i++)
                {
                    var text = _parked[i];
                    if (!text) continue;
                    text.isTextObjectScaleStatic = false;
                    // The setter registers unconditionally; TMP itself keeps a disabled text, or one
                    // whose Canvas is disabled, out of the queue (its OnEnable / OnCanvasHierarchyChanged
                    // re-register it when that changes).
                    var canvas = text.canvas;
                    if (!text.isActiveAndEnabled || !canvas || !canvas.enabled)
                        TMP_UpdateManager.UnRegisterTextObjectForUpdate(text);
                }
                _parked.Clear();

                MaskScratch.Clear();
                GetComponentsInChildren(false, MaskScratch);
                for (int i = 0; i < MaskScratch.Count; i++)
                {
                    var mask = MaskScratch[i];
                    if (mask != this && mask._asleep) mask.ParkTexts();
                }
                MaskScratch.Clear();
            }
        }

        void ParkTexts()
        {
            using (ParkMarker.Auto())
            {
                TextScratch.Clear();
                GetComponentsInChildren(false, TextScratch);
                for (int i = 0; i < TextScratch.Count; i++)
                {
                    var text = TextScratch[i];
                    // Already static: authored so, or parked by a sleeping mask below this one.
                    if (!text.isActiveAndEnabled || text.isTextObjectScaleStatic) continue;
                    text.isTextObjectScaleStatic = true;
                    _parked.Add(text);
                }
                TextScratch.Clear();
            }
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
