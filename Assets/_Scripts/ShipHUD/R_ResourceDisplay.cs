using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    /// <summary>
    /// Refactored resource display that supports 3 modes:
    /// - SpriteFill:     fillImage.fillAmount (can also drive segmented Images; optional color when full)
    /// - SpriteSwap:     swap a sprite from a list onto a single Image
    /// - SpriteSequence: legacy "enable steps" using a list of GameObjects (or fallback to sprites)
    /// Attach this to the UI element representing a meter/resource and configure per mode.
    /// </summary>
    public class R_ResourceDisplay : MonoBehaviour
    {
        public enum DisplayMode { SpriteFill, SpriteSwap, SpriteSequence }

        [Header("General")]
        [Tooltip("For organizational reference only; not used by code unless you want to index displays by this.")]
        public int index = 0;
        public DisplayMode mode = DisplayMode.SpriteFill;
        public bool verbose = false;

        [Header("Sprite Fill")]
        [Tooltip("Primary image that uses fillAmount 0..1")]
        public Image fillImage;
        [Tooltip("Optional segmented images that also use fillAmount 0..1")]
        public List<Image> segments = new();
        [Tooltip("If true, switches color when >= 0.99")]
        public bool changeColorOnFull = false;
        public Color normalColor = Color.white;
        public Color fullColor   = Color.red;

        [Header("Sprite Swap / Sequence (sprite-based)")]
        [Tooltip("Target Image to set sprite on (for SpriteSwap or SpriteSequence when not using stepObjects).")]
        public Image spriteTarget;
        [Tooltip("Sprites used for SpriteSwap or SpriteSequence (if stepObjects not used).")]
        public List<Sprite> sprites = new();

        [Header("Sprite Sequence (object-based)")]
        [Tooltip("Optional: legacy step objects. For N steps, enables first k based on normalized value.")]
        public List<GameObject> stepObjects = new();

        // state
        float current = 0f;
        Coroutine animCo;

        /// <summary>Returns the current normalized value (0..1) last applied to the display.</summary>
        public float CurrentNormalized => current;

        /// <summary>Sets the display immediately to normalized value [0..1].</summary>
        public void SetImmediate(float normalized)
        {
            current = Mathf.Clamp01(normalized);
            Apply(current);
        }

        /// <summary>Animate from current value to target over duration.</summary>
        public void AnimateTo(float targetNormalized, float duration)
        {
            AnimateFromTo(current, targetNormalized, duration);
        }

        /// <summary>Animate from explicit start to target over duration.</summary>
        public void AnimateFromTo(float fromNormalized, float toNormalized, float duration)
        {
            fromNormalized = Mathf.Clamp01(fromNormalized);
            toNormalized   = Mathf.Clamp01(toNormalized);

            if (animCo != null) StopCoroutine(animCo);
            animCo = StartCoroutine(AnimateCo(fromNormalized, toNormalized, duration));
        }

        IEnumerator AnimateCo(float from, float to, float dur)
        {
            if (dur <= 0f)
            {
                current = to;
                Apply(current);
                yield break;
            }

            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                current = Mathf.Lerp(from, to, t / dur);
                Apply(current);
                yield return null;
            }
            current = to;
            Apply(current);
        }

        void Apply(float value)
        {
            switch (mode)
            {
                case DisplayMode.SpriteFill:
                {
                    // main bar
                    if (fillImage)
                    {
                        fillImage.fillAmount = value;
                        if (changeColorOnFull)
                            fillImage.color = (value >= 0.99f) ? fullColor : normalColor;
                    }

                    // segmented
                    if (segments is { Count: > 0 })
                    {
                        int total = segments.Count;
                        float per = 1f / total;
                        for (int i = 0; i < total; i++)
                        {
                            var seg = segments[i];
                            if (!seg) continue;
                            float segFill = Mathf.Clamp01((value - (i * per)) * total);
                            seg.fillAmount = segFill;
                        }
                    }
                    break;
                }

                case DisplayMode.SpriteSwap:
                {
                    if (spriteTarget && sprites.Count > 0)
                    {
                        int max = sprites.Count - 1;
                        int idx = Mathf.Clamp(Mathf.FloorToInt(value * max), 0, max);
                        spriteTarget.sprite = sprites[idx];
                    }
                    break;
                }

                case DisplayMode.SpriteSequence:
                {
                    if (stepObjects.Count > 0)
                    {
                        int max = stepObjects.Count; // number of steps
                        int activeCount = Mathf.Clamp(Mathf.CeilToInt(value * max), 0, max);
                        for (int i = 0; i < max; i++)
                        {
                            var go = stepObjects[i];
                            if (!go) continue;
                            go.SetActive(i < activeCount);
                        }
                    }
                    else if (spriteTarget && sprites.Count > 0)
                    {
                        // fallback to sprite-based stepping same as swap
                        int max = sprites.Count - 1;
                        int idx = Mathf.Clamp(Mathf.FloorToInt(value * max), 0, max);
                        spriteTarget.sprite = sprites[idx];
                    }
                    break;
                }
            }

            if (verbose) Debug.Log($"[R_ResourceDisplay:{name}] {mode} -> {value:0.000}", this);
        }
    }
}