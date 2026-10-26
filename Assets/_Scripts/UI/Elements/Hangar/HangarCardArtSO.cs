using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Hangar vessel list's own card art (Docs/MENU_REDESIGN_B.md section 82; Shombith, 2026-10-26: "Go ahead with the
    /// colourful card renders"): each hull's three-quarter view in its model's slot colours, cut from the vessel-model
    /// contact sheets by <c>Tools/Build/author_hangar_card_art.py</c>. Only the Hangar's list reads it; every other screen
    /// keeps <see cref="SO_Vessel.IconActive"/>, the fleet's card art in its colour scheme (Docs/VesselModels/COLOR.md).
    /// </summary>
    [CreateAssetMenu(fileName = "HangarCardArt", menuName = "ScriptableObjects/UI/Hangar Card Art")]
    public class HangarCardArtSO : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public SO_Vessel vessel;
            [Tooltip("The hull's colourful three-quarter view, on a 3:1 clear canvas.")]
            public Sprite art;
        }

        [SerializeField] private List<Entry> entries = new();

        /// <summary>The vessel's Hangar card art, or null when it has none (the list then shows its card icon).</summary>
        public Sprite For(SO_Vessel vessel)
        {
            if (!vessel) return null;
            foreach (var e in entries)
                if (e.vessel == vessel) return e.art;
            return null;
        }
    }
}
