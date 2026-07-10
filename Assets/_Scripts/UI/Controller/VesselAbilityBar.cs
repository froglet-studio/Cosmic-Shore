using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Guarantees a vessel presents exactly four ability icons, and lights each while its ability's
    /// input is held.
    ///
    /// PREFERRED path — author the four icon <see cref="Image"/>s in the HUD prefab and assign them
    /// to <see cref="slotImages"/>. They then load with the HUD like any other element: zero runtime
    /// allocation, and no work on the vessel-spawn/swap hotpath.
    ///
    /// FALLBACK path — any slot left unassigned is built once at <see cref="Initialize"/> so a
    /// not-yet-authored vessel (or a stub) still shows four icons, with an obvious placeholder.
    /// This is the exceptional path, so its one-time construction cost is irrelevant.
    ///
    /// No pooling: a bar is created at most once per vessel and lives for the HUD's lifetime — there
    /// is nothing high-frequency to pool, and the structure is never rebuilt (it survives HUD
    /// show/hide untouched). Unfilled abilities show a code-generated placeholder sprite.
    /// </summary>
    public sealed class VesselAbilityBar : MonoBehaviour
    {
        [Header("Data — the four player-facing abilities")]
        [SerializeField] private VesselAbilitySetSO abilitySet;

        [Header("Icons — assign four in the HUD prefab (preferred, zero runtime alloc)")]
        [SerializeField] private Image[] slotImages = new Image[VesselAbilitySetSO.SlotCount];

        [Header("Fallback layout — only used to self-build unassigned slots")]
        [SerializeField] private RectTransform fallbackContainer;
        [SerializeField] private Vector2 fallbackIconSize = new(96f, 96f);
        [SerializeField] private float fallbackSpacing = 16f;
        [SerializeField] private Vector2 fallbackAnchoredOffset = new(0f, 24f);

        [Header("Active-state feel")]
        [SerializeField, Range(0f, 1f)] private float idleAlpha = 0.55f;
        [SerializeField, Range(0f, 1f)] private float activeAlpha = 1f;
        [SerializeField] private float activeScale = 1.15f;

        readonly Image[] _icons = new Image[VesselAbilitySetSO.SlotCount];
        R_VesselActionHandler _actions;
        bool _subscribed;
        bool _built;

        public int SlotCount => VesselAbilitySetSO.SlotCount;
        public bool HasAbilitySet => abilitySet != null;

        /// <summary>Assign the ability set at runtime (auto-adoption path). Call before
        /// <see cref="Initialize"/>; after init it takes effect on the next repaint.</summary>
        public void SetAbilitySet(VesselAbilitySetSO set)
        {
            abilitySet = set;
            if (_built) Repaint();
        }

        public void Initialize(IVesselStatus status)
        {
            _actions = status?.ActionHandler;
            ResolveIcons();
            Subscribe();
        }

        void OnEnable()
        {
            // Re-attach after a disable→enable cycle (HUD show/hide). The icon structure persists —
            // it is never released or rebuilt — so this only re-subscribes. No-op before Initialize.
            if (_actions != null) Subscribe();
        }

        void OnDisable() => Unsubscribe();
        void OnDestroy() => Unsubscribe();

        void Subscribe()
        {
            if (_subscribed || _actions == null) return;
            _actions.OnInputEventStarted += HandleInputStarted;
            _actions.OnInputEventStopped += HandleInputStopped;
            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed || _actions == null) return;
            _actions.OnInputEventStarted -= HandleInputStarted;
            _actions.OnInputEventStopped -= HandleInputStopped;
            _subscribed = false;
        }

        void ResolveIcons()
        {
            if (_built)
            {
                Repaint(); // re-Initialize (vessel swap) just repaints the existing structure
                return;
            }

            for (int i = 0; i < SlotCount; i++)
            {
                // Preferred: an icon authored in the prefab — no allocation.
                var img = (slotImages != null && i < slotImages.Length) ? slotImages[i] : null;
                // Fallback: self-build a missing slot once, so the four-icon contract still holds.
                if (!img) img = BuildFallbackIcon(i);
                _icons[i] = img;
            }

            _built = true;
            Repaint();

            if (!abilitySet)
                Debug.LogError($"[VesselAbilityBar] No VesselAbilitySetSO assigned on '{name}'. " +
                               "Showing four placeholders — every vessel must have a 4-slot ability set.");
        }

        void Repaint()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                var img = _icons[i];
                if (!img) continue;

                var slot = abilitySet ? abilitySet.GetSlot(i) : default;
                bool hasIcon = slot.HasIcon;

                img.sprite = hasIcon ? slot.Icon : AbilityIconPlaceholder.Sprite;
                if (!img.enabled) img.enabled = true;
                img.color = new Color(1f, 1f, 1f, idleAlpha);
                img.rectTransform.localScale = Vector3.one;
            }
        }

        Image BuildFallbackIcon(int index)
        {
            EnsureFallbackContainer();

            var go = new GameObject($"AbilitySlot{index}_fallback", typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(fallbackContainer, false);
            rt.sizeDelta = fallbackIconSize;

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }

        void EnsureFallbackContainer()
        {
            if (fallbackContainer) return;

            var go = new GameObject("AbilityIconContainer", typeof(RectTransform));
            fallbackContainer = go.GetComponent<RectTransform>();
            fallbackContainer.SetParent(transform, false);
            fallbackContainer.anchorMin = new Vector2(0.5f, 0f);
            fallbackContainer.anchorMax = new Vector2(0.5f, 0f);
            fallbackContainer.pivot = new Vector2(0.5f, 0f);
            fallbackContainer.anchoredPosition = fallbackAnchoredOffset;

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = fallbackSpacing;
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        void HandleInputStarted(InputEvents input) => SetActive(input, true);
        void HandleInputStopped(InputEvents input) => SetActive(input, false);

        void SetActive(InputEvents input, bool active)
        {
            if (!_built || !abilitySet) return;

            for (int i = 0; i < SlotCount; i++)
            {
                if (abilitySet.GetSlot(i).Input != input) continue;

                var img = _icons[i];
                if (!img) continue;

                var c = img.color;
                c.a = active ? activeAlpha : idleAlpha;
                img.color = c;
                img.rectTransform.localScale = active ? Vector3.one * activeScale : Vector3.one;
            }
        }
    }
}
