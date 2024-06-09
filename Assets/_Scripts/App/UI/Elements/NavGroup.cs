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
        [SerializeField] List<NavLink> navLinks;

        public void ActivateLink(NavLink linkToActivate)
        {
            int selectionIndex = 0;
            foreach (var link in navLinks)
            {
                link.SetActive(link == linkToActivate);
                switch (navGroupType)
                {
                    case NavGroupType.SelectView:
                        link.selectView.SetActive(link == linkToActivate);
                        break;
                    case NavGroupType.UpdateView:
                        if (link == linkToActivate)
                            link.updateView.Select(selectionIndex);
                        break;
                }
                selectionIndex++;
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