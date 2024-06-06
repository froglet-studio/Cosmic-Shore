using UnityEngine;
using System.Collections.Generic;

namespace CosmicShore.App.UI
{
    public class NavGroup : MonoBehaviour
    {
        public List<NavLink> navLinks;

        public void ActivateLink(NavLink linkToActivate)
        {
            foreach (var link in navLinks)
            {
                link.SetActive(link == linkToActivate);
            }
        }

        void Start()
        {
            foreach (var link in navLinks)
                link.navGroup = this;

            if (navLinks.Count > 0)
                ActivateLink(navLinks[0]);
        }
    }
}