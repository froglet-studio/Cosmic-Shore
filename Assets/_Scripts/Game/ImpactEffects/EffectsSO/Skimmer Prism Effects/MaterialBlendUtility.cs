using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Core.Visuals
{
    /// <summary>
    /// Gradually blends a renderer's material or material properties over time.
    /// Lightweight, safe, reusable. No FindObjectOfType or singletons.
    /// </summary>
    public static class MaterialBlendUtility
    {
        private static readonly Dictionary<Renderer, Coroutine> BlendMap = new();

        // Cached shader property IDs — avoids string hashing every frame in BlendRoutine
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
                BlendMap[renderer] = null;
            }

            var runnerHost = GetRunner(renderer);
            if (!runnerHost) return;

            // collect base material
            var mats = renderer.materials;
            Material baseMat = (mats == null || mats.Length == 0)
                ? renderer.material
                : mats[0];

            var overInstance = new Material(overMat);

            // add if desired
            if (addInsteadOfReplace)
            {
                var list = new List<Material>(mats);
                if (!list.Contains(overInstance))
                    list.Add(overInstance);
                renderer.materials = list.ToArray();
            }

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

            // property block fallback
            var mpb = new MaterialPropertyBlock();
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
                    renderer.GetPropertyBlock(mpb);
                    if (hasColor) mpb.SetColor(ColorID, Color.Lerp(fromColor, toColor, a));
                    if (hasEmis)  mpb.SetColor(EmissionColorID,  Color.Lerp(fromEmis,  toEmis,  a));
                    renderer.SetPropertyBlock(mpb);
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
                renderer.GetPropertyBlock(mpb);
                if (hasColor) mpb.SetColor(ColorID, toColor);
                if (hasEmis)  mpb.SetColor(EmissionColorID,  toEmis);
                renderer.SetPropertyBlock(mpb);
            }

            BlendMap[renderer] = null;
        }

        /// <summary>
        /// Clears property blocks and stops any active blend on this renderer.
        /// </summary>
        public static void ResetBlend(Renderer renderer)
        {
            if (!renderer) return;

            if (BlendMap.TryGetValue(renderer, out var co) && co != null)
            {
                var runner = renderer.GetComponent<BlendRunner>();
                if (runner) runner.StopCoroutine(co);
                BlendMap[renderer] = null;
            }

            var mpb = new MaterialPropertyBlock();
            renderer.SetPropertyBlock(mpb);
        }
    }
}