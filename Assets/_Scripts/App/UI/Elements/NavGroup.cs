using UnityEngine;
using System.Collections.Generic;

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
        List<NavLink> navLinks = new();

        public void ActivateLink(NavLink linkToActivate)
        {
            int selectionIndex = 0;
            foreach (var link in navLinks)
            {
                link.SetActive(link.Index == linkToActivate.Index);
                switch (navGroupType)
                {
                    case NavGroupType.SelectView:
                        link.view.gameObject.SetActive(link.Index == linkToActivate.Index);
                        break;
                    case NavGroupType.UpdateView:
                        if (link.Index == linkToActivate.Index)
                            link.view.Select(selectionIndex);
                        break;
                    default:
                        Debug.LogWarning("NavGroup - ActivateLink: Unknown NavGroup Link Type.");
                        break;
                }
                selectionIndex++;
            }
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
    }
}