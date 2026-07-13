using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Completes the four-icon contract for a vessel: every vessel presents exactly four ability
    /// icons, where the icons the HUD ALREADY shows count first.
    ///
    /// Per slot, resolution order:
    ///   1. <see cref="slotImages"/> — an icon authored on this bar in the HUD prefab.
    ///   2. <c>VesselHUDView.GetAbilitySlotImage(i)</c> — an existing view icon BOUND to the slot.
    ///      Bound slots are presented and animated entirely by the view/controller's own logic;
    ///      the bar does not touch them, so adopting the contract is a pure refactor for a view
    ///      that already shows four icons (the player sees no change — e.g. Squirrel).
    ///   3. Fallback — the bar builds an icon showing the set's sprite, or the obvious
    ///      code-generated placeholder, and lights it while its input is held. Only genuinely
    ///      missing icons render here, so a vessel cannot present fewer than four.
    ///
    /// No pooling: a bar exists at most once per vessel and lives for the HUD's lifetime; the
    /// structure is never rebuilt (it survives HUD show/hide untouched).
    /// </summary>
    public sealed class VesselAbilityBar : MonoBehaviour
    {
        [Header("Data — the four player-facing abilities")]
        [SerializeField] private VesselAbilitySetSO abilitySet;

        [Header("Icons — authored on the bar (highest precedence, zero runtime alloc)")]
        [SerializeField] private Image[] slotImages = new Image[VesselAbilitySetSO.SlotCount];

        [Header("Fallback layout — only used to self-build genuinely missing slots")]
        [SerializeField] private RectTransform fallbackContainer;
        [SerializeField] private Vector2 fallbackIconSize = new(96f, 96f);
        [SerializeField] private float fallbackSpacing = 16f;
        [SerializeField] private Vector2 fallbackAnchoredOffset = new(0f, 24f);

        [Header("Active-state feel (fallback icons only)")]
        [SerializeField, Range(0f, 1f)] private float idleAlpha = 0.55f;
        [SerializeField, Range(0f, 1f)] private float activeAlpha = 1f;
        [SerializeField] private float activeScale = 1.15f;

        // Icons the bar OWNS (authored-on-bar or self-built fallback). View-bound slots stay null
        // here — their rendering belongs to the view and must never be repainted or lit by the bar.
        readonly Image[] _ownedIcons = new Image[VesselAbilitySetSO.SlotCount];
        R_VesselActionHandler _actions;
        VesselHUDView _view;
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

        public void Initialize(IVesselStatus status, VesselHUDView view = null)
        {
            _actions = status?.ActionHandler;
            if (view) _view = view;
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
                Repaint(); // re-Initialize (vessel swap) just repaints the owned icons
                return;
            }

            for (int i = 0; i < SlotCount; i++)
            {
                // 1. Authored on the bar in the HUD prefab.
                var authored = (slotImages != null && i < slotImages.Length) ? slotImages[i] : null;
                if (authored)
                {
                    _ownedIcons[i] = authored;
                    continue;
                }

                // 2. Bound to an existing view icon — the view presents it; the bar stays out.
                if (_view && _view.GetAbilitySlotImage(i))
                    continue;

                // 3. Genuinely missing — self-build with the set icon / placeholder.
                _ownedIcons[i] = BuildFallbackIcon(i);
            }

            _built = true;
            Repaint();

            if (!abilitySet)
                Debug.LogError($"[VesselAbilityBar] No VesselAbilitySetSO assigned on '{name}'. " +
                               "Showing placeholders — every vessel must have a 4-slot ability set.");
        }

        void Repaint()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                var img = _ownedIcons[i];
                if (!img) continue; // view-bound slot — the view owns its rendering

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

                var img = _ownedIcons[i];
                if (!img) continue; // view-bound slots animate via their own controller juice

                var c = img.color;
                c.a = active ? activeAlpha : idleAlpha;
                img.color = c;
                img.rectTransform.localScale = active ? Vector3.one * activeScale : Vector3.one;
            }
        }
    }
}
