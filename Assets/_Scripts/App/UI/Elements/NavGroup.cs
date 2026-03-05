using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine.UI;
using CosmicShore.Utility;

namespace CosmicShore.App.UI
{
    public enum NavGroupType
    {
        SelectView = 0,
        UpdateView = 1,
    }

    public class NavGroup : MonoBehaviour
    {
        [SerializeField] NavGroupType navGroupType;
        [SerializeField] GameObject navLinkContainer;

        [Header("Controller Navigation")]
        [Tooltip("Enable DPad left/right to cycle between nav links in this group.")]
        [SerializeField] private bool enableDPadCycling = true;

        List<NavLink> navLinks = new();
        private int _activeIndex;

        public void ActivateLink(NavLink linkToActivate)
        {
            int selectionIndex = 0;
            foreach (var link in navLinks)
            {
                bool isTarget = link.Index == linkToActivate.Index;
                link.SetActive(isTarget);
                if (isTarget)
                    _activeIndex = navLinks.IndexOf(link);

                switch (navGroupType)
                {
                    case NavGroupType.SelectView:
                        link.view.gameObject.SetActive(isTarget);
                        break;
                    case NavGroupType.UpdateView:
                        if (isTarget)
                            link.view.Select(selectionIndex);
                        break;
                    default:
                        CSDebug.LogWarning("NavGroup - ActivateLink: Unknown NavGroup Link Type.");
                        break;
                }
                selectionIndex++;
            }
        }

        void Update()
        {
            if (!enableDPadCycling || Gamepad.current == null || navLinks.Count == 0)
                return;

            if (Gamepad.current.leftShoulder.wasPressedThisFrame)
                CyclePrevious();
            if (Gamepad.current.rightShoulder.wasPressedThisFrame)
                CycleNext();
        }

        private void CycleNext()
        {
            int next = (_activeIndex + 1) % navLinks.Count;
            ActivateLink(navLinks[next]);
        }

        private void CyclePrevious()
        {
            int prev = (_activeIndex - 1 + navLinks.Count) % navLinks.Count;
            ActivateLink(navLinks[prev]);
        }

        void Start()
        {
            Initialize();
        }

        /// <summary>
        /// Used for dynamic NavGroups - where the list of selectable models is dynamic.
        /// Calling this will clear the navLink list, then look in the navLinkContainer for children with a component of type NavLink and add them to the list
        /// </summary>
        public void Initialize()
        {
            navLinks.Clear();

            int index = 0;
            foreach (var navLink in navLinkContainer.GetComponentsInChildren<NavLink>(true))
            {
                navLink.navGroup = this;
                navLink.Index = index;
                index++;
                navLinks.Add(navLink);
            }

            if (navLinks.Count > 0)
                ActivateLink(navLinks[0]);
        }

        public void UpdateLayout()
        {
            HorizontalLayoutGroup layoutGroup = GetComponentInParent<HorizontalLayoutGroup>();
            if (layoutGroup != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(layoutGroup.GetComponent<RectTransform>());
            }
        }
    }
}