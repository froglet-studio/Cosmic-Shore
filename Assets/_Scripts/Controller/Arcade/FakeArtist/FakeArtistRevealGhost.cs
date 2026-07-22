using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The post-vote reveal: a ghost blueprint of the WHOLE round artwork, regenerated
    /// locally on every peer from just (preset, size, seed, strokeCount) - the same
    /// deterministic <see cref="FakeArtistArtworkBuilder"/> the server dealt from, so no
    /// geometry crosses the wire and nothing is revealed before the votes are in.
    /// Local-space LineRenderers under one root so the whole ghost scale-blooms in and
    /// scales out (continuity law).
    /// </summary>
    public class FakeArtistRevealGhost : MonoBehaviour
    {
        const float BloomSeconds = 1.1f;
        const float GhostAlpha = 0.55f;
        const float GhostWidth = 2.2f;

        readonly List<LineRenderer> _lines = new();
        float _bloom;
        bool _fadingOut;

        public static FakeArtistRevealGhost Spawn(PaintingPreset preset, float size, int seed, int strokeCount,
            Vector3 origin, Quaternion rotation, ToyContext context)
        {
            var root = new GameObject($"FakeArtistReveal_{preset}");
            root.transform.SetPositionAndRotation(origin, rotation);
            var ghost = root.AddComponent<FakeArtistRevealGhost>();
            ghost.Build(preset, size, seed, strokeCount, context);
            return ghost;
        }

        void Build(PaintingPreset preset, float size, int seed, int strokeCount, ToyContext context)
        {
            var strokes = FakeArtistArtworkBuilder.BuildStrokes(preset, size, seed, strokeCount);
            foreach (var stroke in strokes)
            {
                if (stroke.points.Count < 2) continue;

                var lr = ToyFactory.CreateLine($"Ghost_{stroke.name}", transform, GhostWidth, worldSpace: false);
                lr.positionCount = stroke.points.Count;
                for (int i = 0; i < stroke.points.Count; i++)
                    lr.SetPosition(i, stroke.points[i]);

                var color = ToyFactory.DomainAccentColor(context, stroke.domain);
                color.a = 0f;
                lr.startColor = color;
                lr.endColor = color;
                _lines.Add(lr);
            }

            transform.localScale = Vector3.one * 0.01f;
        }

        void Update()
        {
            if (_fadingOut) return;

            _bloom = Mathf.MoveTowards(_bloom, 1f, Time.deltaTime / BloomSeconds);
            float eased = Mathf.SmoothStep(0f, 1f, _bloom);
            transform.localScale = Vector3.one * Mathf.Max(0.01f, eased);
            ApplyAlpha(eased * GhostAlpha);
        }

        void ApplyAlpha(float alpha)
        {
            foreach (var lr in _lines)
            {
                if (lr == null) continue;
                var c = lr.startColor;
                c.a = alpha;
                lr.startColor = c;
                var e = lr.endColor;
                e.a = alpha;
                lr.endColor = e;
            }
        }

        /// <summary>Scale-out teardown (continuity law - never a bare Destroy).</summary>
        public void FadeOutAndDestroy(float seconds = 0.6f)
        {
            if (_fadingOut) return;
            _fadingOut = true;
            FadeOutAsync(seconds).Forget();
        }

        async UniTaskVoid FadeOutAsync(float seconds)
        {
            float elapsed = 0f;
            var startScale = transform.localScale;
            float startAlpha = _lines.Count > 0 && _lines[0] != null ? _lines[0].startColor.a : GhostAlpha;
            while (elapsed < seconds && this != null && gameObject != null)
            {
                elapsed += Time.deltaTime;
                float t = 1f - Mathf.Clamp01(elapsed / seconds);
                transform.localScale = startScale * Mathf.Max(0.01f, t);
                ApplyAlpha(startAlpha * t);
                await UniTask.Yield(PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());
            }
            if (this != null && gameObject != null)
                Destroy(gameObject);
        }
    }
}
