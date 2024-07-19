using UnityEngine;
using System.Collections.Generic;
using System.Collections;

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
            Debug.LogWarning($"NavGroup.ActivateLink - {name}, {linkToActivate.gameObject.name}");
            int selectionIndex = 0;
            foreach (var link in navLinks)
            {
                Debug.LogWarning($"NavGroup.ActivateLink - {link.gameObject.name}, {link == linkToActivate}");
                link.SetActive(link == linkToActivate);
                switch (navGroupType)
                {
                    case NavGroupType.SelectView:
                        link.view.gameObject.SetActive(link == linkToActivate);
                        break;
                    case NavGroupType.UpdateView:
                        if (link == linkToActivate)
                            link.view.Select(selectionIndex);
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

            foreach (var navLink in navLinkContainer.GetComponentsInChildren<NavLink>(true))
            {
                navLink.navGroup = this;
                navLinks.Add(navLink);
            }

            if (navLinks.Count > 0)
                ActivateLink(navLinks[0]);
        }
    }
}