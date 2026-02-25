using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.ShapeDrawing
{
    /// <summary>
    /// Manages pre-placed ShapeSign GameObjects in the scene.
    /// Signs are positioned by the designer in the editor — this script
    /// simply enables/disables them when entering/exiting the lobby.
    /// </summary>
    public class ShapeSignSpawner : MonoBehaviour
    {
        [Header("Signs")]
        [Tooltip("Pre-placed ShapeSign GameObjects in the scene.")]
        [SerializeField] List<GameObject> signs;

        /// <summary>Show all pre-placed signs (resets selection state via OnEnable).</summary>
        public void ShowSigns()
        {
            foreach (var s in signs)
            {
                if (s) s.SetActive(true);
            }
        }

        /// <summary>Hide all signs.</summary>
        public void HideSigns()
        {
            foreach (var s in signs)
                if (s) s.SetActive(false);
        }
    }
}
