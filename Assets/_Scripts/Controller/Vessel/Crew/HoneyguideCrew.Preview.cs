using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The triggers' previews (Honeyguide Lab round 8: "the ability has a preview when pulled and then completes when
    /// released"). While a trigger is pulled, what its current ability will do is drawn in the world, on the pilot's own
    /// screen only: the line, the landing ring, the cone, the hook to its target, every Monte hop. World-space lines in the
    /// neutral blue (<c>previewColour</c>); a pull that would find no target draws nothing, which is the fizzle's warning.
    /// An AI crew draws none.
    /// </summary>
    public sealed partial class HoneyguideCrew
    {
        readonly List<LineRenderer> _pvLines = new();
        readonly List<Vector3> _pvPts = new();
        GameObject _pvRoot;
        int _pvUsed;
        static Material s_previewFallback;

        bool DrawsPreviews => !_aiCrew;

        void StepPreviews()
        {
            _pvUsed = 0;
            if (DrawsPreviews && _vessel != null && UnityLiveness.Alive(_vessel.Transform))
                for (int k = 0; k < 2; k++)
                    if (_pv[k]) DrawPreview(k);
            for (int i = _pvUsed; i < _pvLines.Count; i++)
                if (_pvLines[i] && _pvLines[i].enabled) _pvLines[i].enabled = false;
        }

        void HidePreviews()
        {
            _pvUsed = 0;
            foreach (var l in _pvLines) if (l) l.enabled = false;
        }

        void DestroyPreviews()
        {
            if (_pvRoot) Destroy(_pvRoot);
            _pvRoot = null;
            _pvLines.Clear();
        }

        void DrawPreview(int k)
        {
            var t = _vessel.Transform;
            Vector3 pos = t.position, fwd = t.forward, up = t.up;
            var b = Current(k);
            var kind = b != null ? b.Def.preview : k == 0 ? HoneyguidePreview.Line : HoneyguidePreview.Dart;
            float reach = b != null ? b.Def.reach : k == 0 ? _cfg.PeckRange : _cfg.FlitDistance;
            switch (kind)
            {
                case HoneyguidePreview.Line:
                    Segment(pos, pos + fwd * reach);
                    break;
                case HoneyguidePreview.Lob:
                {
                    var at = pos + fwd * _cfg.GrenadeThrow;
                    Segment(pos, at);
                    Ring(at, up, fwd, reach);
                    break;
                }
                case HoneyguidePreview.Cone:
                {
                    float a = _cfg.HarmonizerCone;
                    _pvPts.Clear();
                    _pvPts.Add(pos + Quaternion.AngleAxis(-a, up) * fwd * reach);
                    _pvPts.Add(pos);
                    _pvPts.Add(pos + Quaternion.AngleAxis(a, up) * fwd * reach);
                    Polyline(_pvPts, false);
                    break;
                }
                case HoneyguidePreview.Ring:
                    Ring(pos, up, fwd, reach);
                    break;
                case HoneyguidePreview.Thrust:
                {
                    var end = pos;
                    for (int i = 0; i < _cfg.ThrusterThrusts; i++)
                    {
                        var next = end + fwd * reach;
                        Segment(end + fwd * 1.5f, next);
                        end = next;
                    }
                    break;
                }
                case HoneyguidePreview.Hook:
                    if (!StrikeTargetAhead(pos, fwd, reach, _cfg.LassoCone, out var hooked)) break;
                    Segment(pos, hooked);
                    Ring(hooked, up, fwd, _cfg.StrikeRadius);
                    break;
                case HoneyguidePreview.Land:
                    if (b == null || !JumpLanding(b.Effect, reach, out var land, out _)) break;
                    Segment(pos, land);
                    Ring(land, up, fwd, _cfg.StrikeRadius);
                    break;
                case HoneyguidePreview.Hops:
                {
                    if (MonteHopList(reach, _hops) == 0) break;
                    var from = pos;
                    foreach (var h in _hops)
                    {
                        Segment(from, h);
                        Ring(h, up, fwd, _cfg.StrikeRadius);
                        from = h;
                    }
                    break;
                }
                case HoneyguidePreview.Dart:
                {
                    var at = pos + fwd * _cfg.FlitDistance;
                    Segment(pos, at);
                    Ring(at, up, fwd, _cfg.FlitStrikeRadius);
                    break;
                }
            }
        }

        void Segment(Vector3 a, Vector3 b)
        {
            _pvPts.Clear();
            _pvPts.Add(a);
            _pvPts.Add(b);
            Polyline(_pvPts, false);
        }

        // A ring of radius r round `at`, in the plane across the hull's up.
        void Ring(Vector3 at, Vector3 up, Vector3 fwd, float r)
        {
            if (r <= 0f) return;
            var side = Vector3.Cross(up, fwd);
            if (side.sqrMagnitude < 1e-6f) side = Vector3.Cross(Vector3.up, fwd);
            side.Normalize();
            var ahead = Vector3.Cross(side, up).normalized;
            int n = Mathf.Max(8, _cfg.PreviewRingSegments);
            _pvPts.Clear();
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                _pvPts.Add(at + (side * Mathf.Cos(a) + ahead * Mathf.Sin(a)) * r);
            }
            Polyline(_pvPts, true);
        }

        void Polyline(List<Vector3> pts, bool loop)
        {
            var line = NextLine();
            if (!line) return;
            line.loop = loop;
            line.positionCount = pts.Count;
            for (int i = 0; i < pts.Count; i++) line.SetPosition(i, pts[i]);
            if (!line.enabled) line.enabled = true;
        }

        LineRenderer NextLine()
        {
            if (_pvUsed < _pvLines.Count) return _pvLines[_pvUsed++];
            var material = _cfg.PreviewMaterial ? _cfg.PreviewMaterial : PreviewFallbackMaterial();
            if (!material) return null;   // headless: no graphics, no previews
            if (!_pvRoot) _pvRoot = new GameObject("HoneyguidePreviews");
            var go = new GameObject("Preview" + _pvLines.Count);
            go.transform.SetParent(_pvRoot.transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 2;
            line.widthMultiplier = _cfg.PreviewWidth;
            line.startColor = line.endColor = _cfg.PreviewColour;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.positionCount = 0;
            line.enabled = false;
            _pvLines.Add(line);
            _pvUsed++;
            return line;
        }

        static Material PreviewFallbackMaterial()
        {
            if (s_previewFallback) return s_previewFallback;
            var shader = Shader.Find("Sprites/Default");
            if (!shader) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) return null;
            s_previewFallback = new Material(shader) { name = "HoneyguidePreview (runtime)", renderQueue = 3000 };
            return s_previewFallback;
        }
    }
}
