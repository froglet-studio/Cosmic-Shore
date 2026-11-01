using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The kit's hover (Rev 35 C, Docs/MENU_REDESIGN_B.md section 101.1; Shombith: "I like C for the hover effects"): when
    /// the pointer enters a control, one soft band (<see cref="UIThemeSO.hoverSheenSprite"/>) sweeps across it once,
    /// then the control rests. Every <see cref="KitSelectableBase"/> (buttons, tiles, the game cards) and every
    /// <see cref="KitHoverTint"/> (the nav tabs, keycaps) calls <see cref="Play"/>, so the look is the theme's and a
    /// change to it reaches every control at once.
    ///
    /// <para>Pointer only, never touch (a touch has no hover) and never the pad (focus keeps the blue ring). Built
    /// the first time a control is hovered, so a screen nobody points at carries nothing; the band is switched off
    /// between sweeps, so a resting control costs nothing per frame. Runs on unscaled time: menu screens other than
    /// HOME run at timeScale 0.</para>
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class KitHoverSheen : MonoBehaviour
    {
        RectTransform _clip, _band;
        Image _image;
        Coroutine _sweep;

        /// <summary>Sweep the theme's sheen across <paramref name="host"/> for a pointer entering it.</summary>
        public static void Play(Component host, UIThemeSO theme, PointerEventData eventData)
        {
            if (!Application.isPlaying || !host || !theme || !theme.hoverSheenSprite) return;
            if (eventData is ExtendedPointerEventData { pointerType: UIPointerType.Touch }) return;
            if (!host.TryGetComponent(out KitHoverSheen sheen)) sheen = host.gameObject.AddComponent<KitHoverSheen>();
            sheen.Run(theme);
        }

        void Run(UIThemeSO theme)
        {
            if (!isActiveAndEnabled) return;
            Build(theme);
            if (_sweep != null) StopCoroutine(_sweep);
            _sweep = StartCoroutine(Sweep(theme));
        }

        void Build(UIThemeSO theme)
        {
            if (_clip) return;
            var clip = new GameObject("HoverSheen", typeof(RectTransform), typeof(RectMask2D), typeof(LayoutElement));
            clip.layer = gameObject.layer;
            clip.GetComponent<LayoutElement>().ignoreLayout = true;
            _clip = (RectTransform)clip.transform;
            _clip.SetParent(transform, false);
            _clip.anchorMin = Vector2.zero;
            _clip.anchorMax = Vector2.one;
            _clip.offsetMin = new Vector2(1f, 1f);
            _clip.offsetMax = new Vector2(-1f, -1f);

            var band = new GameObject("Band", typeof(RectTransform), typeof(Image));
            band.layer = gameObject.layer;
            _band = (RectTransform)band.transform;
            _band.SetParent(_clip, false);
            _band.anchorMin = new Vector2(0f, 0f);
            _band.anchorMax = new Vector2(0f, 1f);
            _band.pivot = new Vector2(0.5f, 0.5f);
            _image = band.GetComponent<Image>();
            _image.raycastTarget = false;
            clip.SetActive(false);
        }

        IEnumerator Sweep(UIThemeSO theme)
        {
            _image.sprite = theme.hoverSheenSprite;
            _image.color = new Color(1f, 1f, 1f, theme.hoverSheenAlpha);
            _clip.SetAsLastSibling();
            _clip.gameObject.SetActive(true);

            float width = _clip.rect.width;
            float bandWidth = Mathf.Max(8f, width * theme.hoverSheenWidth);
            _band.sizeDelta = new Vector2(bandWidth, 0f);
            float from = -bandWidth * 0.5f, to = width + bandWidth * 0.5f;
            float seconds = Mathf.Max(0.05f, theme.hoverSheenSeconds);

            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / seconds, 3f);          // ease out
                _band.anchoredPosition = new Vector2(Mathf.LerpUnclamped(from, to, k), 0f);
                yield return null;
            }
            _clip.gameObject.SetActive(false);
            _sweep = null;
        }

        void OnDisable()
        {
            _sweep = null;
            if (_clip) _clip.gameObject.SetActive(false);
        }
    }
}
