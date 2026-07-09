using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Renders a vessel's four player-facing ability icons — always exactly four. Any slot without
    /// an authored icon shows an obvious placeholder, so it is impossible for a vessel to present
    /// fewer than four ability icons. Each icon lights up while its ability's input is held.
    ///
    /// The bar self-builds its icon row if none is authored in the prefab, so a brand-new vessel
    /// gets four placeholder icons for free. Drive it from <see cref="VesselHUDController"/> by
    /// calling <see cref="Initialize"/>; the base controller resolves and initializes any bar found
    /// under the HUD, so existing HUDs are untouched until one is added.
    /// </summary>
    public sealed class VesselAbilityBar : MonoBehaviour
    {
        [Header("Data — the four player-facing abilities")]
        [SerializeField] private VesselAbilitySetSO abilitySet;

        [Header("Layout (self-built if the container is left empty)")]
        [SerializeField] private RectTransform iconContainer;
        [SerializeField] private Vector2 iconSize = new(96f, 96f);
        [SerializeField] private float spacing = 16f;
        [SerializeField] private Vector2 selfBuiltAnchoredOffset = new(0f, 24f);

        [Header("Active-state feel")]
        [SerializeField, Range(0f, 1f)] private float idleAlpha = 0.55f;
        [SerializeField, Range(0f, 1f)] private float activeAlpha = 1f;
        [SerializeField] private float activeScale = 1.15f;

        readonly List<Image> _icons = new();
        R_VesselActionHandler _actions;
        bool _subscribed;
        bool _built;

        public int SlotCount => VesselAbilitySetSO.SlotCount;
        public bool HasAbilitySet => abilitySet != null;

        public void Initialize(IVesselStatus status)
        {
            _actions = status?.ActionHandler;
            BuildIcons();
            Subscribe();
        }

        void OnEnable()
        {
            // Re-attach after a disable→enable cycle (pooled / toggled HUD). No-op before Initialize.
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

        void BuildIcons()
        {
            EnsureContainer();

            for (int i = 0; i < SlotCount; i++)
            {
                Image img = i < _icons.Count ? _icons[i] : null;
                if (!img)
                {
                    img = CreateIcon(i);
                    _icons.Add(img);
                }

                var slot = abilitySet ? abilitySet.GetSlot(i) : default;
                bool hasIcon = slot.HasIcon;

                img.sprite = hasIcon ? slot.Icon : AbilityIconPlaceholder.Sprite;
                img.color = new Color(1f, 1f, 1f, idleAlpha);
                img.rectTransform.localScale = Vector3.one;
                img.name = hasIcon ? $"AbilitySlot{i}_{slot.Label}" : $"AbilitySlot{i}_Placeholder";
            }

            _built = true;

            if (!abilitySet)
                Debug.LogError($"[VesselAbilityBar] No VesselAbilitySetSO assigned on '{name}'. " +
                               "Showing four placeholders — every vessel must have a 4-slot ability set.");
        }

        void EnsureContainer()
        {
            if (iconContainer) return;

            var go = new GameObject("AbilityIconContainer", typeof(RectTransform));
            iconContainer = go.GetComponent<RectTransform>();
            iconContainer.SetParent(transform, false);
            iconContainer.anchorMin = new Vector2(0.5f, 0f);
            iconContainer.anchorMax = new Vector2(0.5f, 0f);
            iconContainer.pivot = new Vector2(0.5f, 0f);
            iconContainer.anchoredPosition = selfBuiltAnchoredOffset;

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        Image CreateIcon(int index)
        {
            var go = new GameObject($"AbilitySlot{index}", typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(iconContainer, false);
            rt.sizeDelta = iconSize;

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }

        void HandleInputStarted(InputEvents input) => SetActive(input, true);
        void HandleInputStopped(InputEvents input) => SetActive(input, false);

        void SetActive(InputEvents input, bool active)
        {
            if (!_built || !abilitySet) return;

            for (int i = 0; i < SlotCount && i < _icons.Count; i++)
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
