using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Gradually blends a renderer's material or material properties over time.
    /// Lightweight, safe, reusable. No FindObjectOfType or singletons.
    /// </summary>
    public static class MaterialBlendUtility
    {
        private static readonly Dictionary<Renderer, Coroutine> BlendMap = new();

        // The overlay material instance created per blend. Tracked so a repeat
        // BeginBlend on the same renderer (pooled prisms re-entering danger mode)
        // destroys/replaces the previous instance instead of leaking it — the old
        // path appended one fresh instance to the renderer's material array per
        // call and never destroyed any of them.
        private static readonly Dictionary<Renderer, Material> OverlayMap = new();

        private static readonly MaterialPropertyBlock SharedMpb = new();
        private static readonly int ColorID = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

        // Runner component used to host coroutines without scene dependencies
        private sealed class BlendRunner : MonoBehaviour { }

        private static BlendRunner GetRunner(Renderer r)
        {
            if (!r) return null;
            if (!r.TryGetComponent(out BlendRunner runner))
                runner = r.gameObject.AddComponent<BlendRunner>();
            return runner;
        }

        /// <summary>
        /// Smoothly blends the material appearance from baseMat to overMat.
        /// Optionally adds overMat to the renderer before blending.
        /// </summary>
        public static void BeginBlend(
            Renderer renderer,
            Material overMat,
            float duration,
            bool addInsteadOfReplace = true)
        {
            if (!renderer || !overMat) return;

            // cancel in-flight blend
            if (BlendMap.TryGetValue(renderer, out var co) && co != null)
            {
                var runner = renderer.GetComponent<BlendRunner>();
                if (runner) runner.StopCoroutine(co);
                BlendMap.Remove(renderer);
            }

            var runnerHost = GetRunner(renderer);
            if (!runnerHost) return;

            // collect base material
            var mats = renderer.materials;
            Material baseMat = (mats == null || mats.Length == 0)
                ? renderer.material
                : mats[0];

            var overInstance = new Material(overMat);

            // Retire the previous blend's overlay instance (if any) so the material
            // array never grows past baseline+1 and the instance doesn't leak.
            bool overlayInArray = false;
            if (OverlayMap.TryGetValue(renderer, out var previous) && previous)
            {
                int previousIndex = mats != null ? System.Array.IndexOf(mats, previous) : -1;
                if (previousIndex >= 0 && addInsteadOfReplace)
                {
                    // Reuse the previous overlay's slot in place.
                    mats[previousIndex] = overInstance;
                    renderer.materials = mats;
                    overlayInArray = true;
                }
                else if (previousIndex >= 0)
                {
                    // Overlay no longer wanted — rebuild the array without it.
                    var trimmed = new Material[mats.Length - 1];
                    for (int i = 0, j = 0; i < mats.Length; i++)
                        if (i != previousIndex)
                            trimmed[j++] = mats[i];
                    renderer.materials = trimmed;
                }
                Object.Destroy(previous);
                OverlayMap.Remove(renderer);
            }

            // Append covers both the fresh-renderer case and an add-mode blend that
            // follows a replace-mode blend (previous overlay tracked but not in the
            // array) — a plain else-if here would silently skip the overlay.
            if (addInsteadOfReplace && !overlayInArray && mats != null)
            {
                var withOverlay = new Material[mats.Length + 1];
                System.Array.Copy(mats, withOverlay, mats.Length);
                withOverlay[mats.Length] = overInstance;
                renderer.materials = withOverlay;
            }

            OverlayMap[renderer] = overInstance;

            var coroutine = runnerHost.StartCoroutine(
                BlendRoutine(renderer, baseMat, overInstance, duration));
            BlendMap[renderer] = coroutine;
        }

        private static IEnumerator BlendRoutine(
            Renderer renderer, Material fromMat, Material toMat, float duration)
        {
            if (!renderer || fromMat == null || toMat == null)
                yield break;

            float t = 0f;
            bool sameShader = fromMat.shader == toMat.shader;
            var workMat = renderer.materials[0];

            bool hasColor = fromMat.HasProperty(ColorID) && toMat.HasProperty(ColorID);
            bool hasEmis = fromMat.HasProperty(EmissionColorID) && toMat.HasProperty(EmissionColorID);

            Color fromColor = hasColor ? fromMat.GetColor(ColorID) : Color.white;
            Color toColor   = hasColor ? toMat.GetColor(ColorID)   : Color.white;
            Color fromEmis  = hasEmis  ? fromMat.GetColor(EmissionColorID)  : Color.black;
            Color toEmis    = hasEmis  ? toMat.GetColor(EmissionColorID)    : Color.black;

            while (t < duration)
            {
                float a = duration <= 0f ? 1f : Mathf.Clamp01(t / duration);

                if (sameShader)
                {
                    workMat.Lerp(fromMat, toMat, a);
                }
                else
                {
                    // Shared MPB: each iteration is a full read-modify-write on this
                    // renderer, so concurrent blends on other renderers can't interfere.
                    renderer.GetPropertyBlock(SharedMpb);
                    if (hasColor) SharedMpb.SetColor(ColorID, Color.Lerp(fromColor, toColor, a));
                    if (hasEmis)  SharedMpb.SetColor(EmissionColorID,  Color.Lerp(fromEmis,  toEmis,  a));
                    renderer.SetPropertyBlock(SharedMpb);
                }

                t += Time.deltaTime;
                yield return null;
            }

            // final snap
            if (sameShader)
            {
                workMat.Lerp(fromMat, toMat, 1f);
            }
            else
            {
                renderer.GetPropertyBlock(SharedMpb);
                if (hasColor) SharedMpb.SetColor(ColorID, toColor);
                if (hasEmis)  SharedMpb.SetColor(EmissionColorID,  toEmis);
                renderer.SetPropertyBlock(SharedMpb);
            }

            BlendMap.Remove(renderer);
        }

        /// <summary>
        /// Clears property blocks, stops any active blend, and retires the overlay
        /// material instance on this renderer.
        /// </summary>
        public static void ResetBlend(Renderer renderer)
        {
            if (!renderer) return;

            if (BlendMap.TryGetValue(renderer, out var co) && co != null)
            {
                var runner = renderer.GetComponent<BlendRunner>();
                if (runner) runner.StopCoroutine(co);
                BlendMap.Remove(renderer);
            }

            if (OverlayMap.TryGetValue(renderer, out var overlay))
            {
                if (overlay)
                {
                    var mats = renderer.materials;
                    int overlayIndex = mats != null ? System.Array.IndexOf(mats, overlay) : -1;
                    if (overlayIndex >= 0)
                    {
                        var trimmed = new Material[mats.Length - 1];
                        for (int i = 0, j = 0; i < mats.Length; i++)
                            if (i != overlayIndex)
                                trimmed[j++] = mats[i];
                        renderer.materials = trimmed;
                    }
                    Object.Destroy(overlay);
                }
                OverlayMap.Remove(renderer);
            }

            SharedMpb.Clear();
            renderer.SetPropertyBlock(SharedMpb);
        }
    }
}
